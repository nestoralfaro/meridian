// Meridian: per-environment application stack (staging | production).
// Deployed by .github/workflows/infra.yml. See docs/adr/0002-hosting-azure-container-apps.md.
targetScope = 'resourceGroup'

@description('Deployment environment.')
@allowed(['staging', 'production'])
param env string

@description('Azure region. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('Container image. A public placeholder until the app image exists in GHCR.')
param image string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the container listens on. 80 for the placeholder, 8080 for ASP.NET Core.')
param targetPort int = 80

@description('Minimum replicas. 0 = scale to zero (free); 1 = always warm (a few USD/month).')
@minValue(0)
@maxValue(1)
param minReplicas int = 0

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
      }
      secrets: hasDb ? [{ name: 'db-connection', value: dbConnection }] : []
    }
    template: {
      containers: [
        {
          name: 'web'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(
            [{ name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }],
            hasDb ? [{ name: 'ConnectionStrings__Meridian', secretRef: 'db-connection' }] : []
          )
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
