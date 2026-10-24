using './main.bicep'

param env = 'production'
param minReplicas = 0 // set to 1 while actively interviewing to avoid cold starts
