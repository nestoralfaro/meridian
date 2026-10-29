using './main.bicep'

// Set by the deploy workflow (ghcr.io/<repo>:<commit>); deployment fails if it's missing.
param image = readEnvironmentVariable('IMAGE')

param env = 'production'
param minReplicas = 0 // set to 1 while the demo is in active use to avoid cold starts
param signInAttemptsPerMinute = 10
