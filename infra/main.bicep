// GaiaSkyline — Azure infrastructure (Stage 8 Part B).
// Subscription-scope template: creates the resource group and delegates to resources.bicep.
// Deploy:  az deployment sub create --location westeurope --template-file main.bicep \
//            --parameters main.dev.bicepparam
targetScope = 'subscription'

@allowed(['dev', 'prod'])
param environment string

@description('Azure region for every resource.')
param location string = 'westeurope'

@description('The public custom domain (prod only; dev uses the default *.azurewebsites.net host). Placeholder until provided.')
param customDomain string = ''

@description('Object id of the owner/operator who gets Key Vault + Blob data access for break-glass administration.')
param operatorObjectId string = ''

@description('Entra admin for the SQL server (login name, e.g. the operator UPN). SQL uses Entra-only auth — no SQL passwords exist.')
param sqlEntraAdminLogin string = ''

@description('Entra admin object id for the SQL server.')
param sqlEntraAdminObjectId string = ''

@description('Operator email for monitoring alert notifications (Part D). Placeholder until provided.')
param operatorEmail string = ''

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-gaiaskyline-${environment}'
  location: location
}

module resources 'resources.bicep' = {
  name: 'gaiaskyline-${environment}'
  scope: rg
  params: {
    environment: environment
    location: location
    customDomain: customDomain
    operatorObjectId: operatorObjectId
    sqlEntraAdminLogin: sqlEntraAdminLogin
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    operatorEmail: operatorEmail
  }
}

output webAppName string = resources.outputs.webAppName
output webAppDefaultHost string = resources.outputs.webAppDefaultHost
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output storageAccountName string = resources.outputs.storageAccountName
output keyVaultName string = resources.outputs.keyVaultName
output frontDoorEndpointHost string = resources.outputs.frontDoorEndpointHost
output appInsightsConnectionString string = resources.outputs.appInsightsConnectionString
