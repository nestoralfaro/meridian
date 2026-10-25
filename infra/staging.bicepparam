using './main.bicep'

// Set by the deploy workflow (ghcr.io/<repo>:<commit>); deployment fails if it's missing.
param image = readEnvironmentVariable('IMAGE')

param env = 'staging'
param minReplicas = 0
