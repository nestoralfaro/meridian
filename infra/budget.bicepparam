using './budget.bicep'

param amount = 5
param startDate = '2026-10-01'
param contactEmail = readEnvironmentVariable('BUDGET_EMAIL')
