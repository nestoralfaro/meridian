// Meridian: per-environment application stack (staging | production).
// Deployed by .github/workflows/deploy.yml as a new revision on every change (docs/adr/0008-immutable-revision-deployments.md).
// See docs/adr/0002-hosting-azure-container-apps.md.
targetScope = 'resourceGroup'

@description('Deployment environment.')
@allowed(['staging', 'production'])
param env string

@description('Azure region. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('Container image, e.g. ghcr.io/nestoralfaro/meridian:<commit-sha>.')
param image string

@description('Port the ASP.NET Core container listens on.')
param targetPort int = 8080

@description('Revision suffix, unique per deployment (e.g. <sha7>-<run>-<attempt>). Empty lets Azure generate one.')
param revisionSuffix string = ''

@description('Revision that keeps 100% of traffic while the new revision is verified. Empty sends traffic to the latest revision (first deployment only).')
param trafficRevisionName string = ''

@description('Minimum replicas. 0 = scale to zero (free); 1 = always warm (a few USD/month).')
@minValue(0)
@maxValue(1)
param minReplicas int = 0

@description('Seed the synthetic demo portfolio and reset it daily (in-app; see DemoDataMaintenance).')
param demoDataEnabled bool = true

@description('Per-client-IP limit on sign-in/registration requests per minute. Strict in production; higher in staging, where the CD end-to-end suite runs every journey from one runner IP.')
@minValue(1)
param signInAttemptsPerMinute int = 10

@description('Npgsql connection string for the app role. Empty until the app needs a database.')
@secure()
param dbConnection string = ''

var name = 'meridian-${env}'
var tags = {
  project: 'meridian'
  env: env
}
var hasDb = !empty(dbConnection)

resource law 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'log-${name}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.1') // hard cap on ingestion cost
    }
  }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: law.id
  }
}

resource cae 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: 'cae-${name}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: law.properties.customerId
        sharedKey: law.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2025-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: cae.id
    configuration: {
      activeRevisionsMode: 'Multiple' // enables blue/green traffic splitting
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
        // New revisions start with 0% traffic; CD shifts traffic only after the revision passes its smoke test.
        traffic: empty(trafficRevisionName)
          ? [{ latestRevision: true, weight: 100 }]
          : [{ revisionName: trafficRevisionName, weight: 100 }]
      }
      secrets: hasDb ? [{ name: 'db-connection', value: dbConnection }] : []
    }
    template: {
      revisionSuffix: empty(revisionSuffix) ? null : revisionSuffix
      containers: [
        {
          name: 'web'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(
            [
              { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }
              // TLS terminates at the Container Apps ingress; honour X-Forwarded-Proto/For so the app sees https.
              { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
              { name: 'Demo__Enabled', value: string(demoDataEnabled) }
              { name: 'Auth__SignInAttemptsPerMinute', value: string(signInAttemptsPerMinute) }
            ],
            hasDb ? [{ name: 'ConnectionStrings__Meridian', secretRef: 'db-connection' }] : []
          )
          probes: [
            // Startup: allow up to 60 s for the process to start before liveness applies.
            { type: 'Startup', httpGet: { path: '/health/live', port: targetPort }, periodSeconds: 2, failureThreshold: 30 }
            // Liveness: the process only, so a database outage never restarts containers.
            { type: 'Liveness', httpGet: { path: '/health/live', port: targetPort }, periodSeconds: 30, failureThreshold: 3 }
            // Readiness: the database must be reachable before the replica receives traffic.
            { type: 'Readiness', httpGet: { path: '/health/ready', port: targetPort }, periodSeconds: 15, failureThreshold: 3 }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: 2
      }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
output appName string = app.name
