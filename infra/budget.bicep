// Meridian: monthly cost budget for the whole resource group (deployed once, shared by all environments).
targetScope = 'resourceGroup'

@description('Monthly budget in the billing currency.')
param amount int = 5

@description('Who gets budget alerts. Passed at deploy time from a secret; never committed.')
param contactEmail string

@description('First day of the month the budget starts. Fixed so redeploys never try to change it.')
param startDate string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'budget-meridian'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate }
    notifications: {
      actual50: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 50, thresholdType: 'Actual', contactEmails: [contactEmail] }
      actual80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: [contactEmail] }
      actual100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [contactEmail] }
      forecast100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Forecasted', contactEmails: [contactEmail] }
    }
  }
}
