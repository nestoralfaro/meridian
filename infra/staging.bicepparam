using './main.bicep'

// Set by the deploy workflow (ghcr.io/<repo>:<commit>); deployment fails if it's missing.
param image = readEnvironmentVariable('IMAGE')

param env = 'staging'
param minReplicas = 0
param signInAttemptsPerMinute = 300 // the CD e2e gate runs every journey from one runner IP
