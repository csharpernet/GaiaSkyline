using 'main.bicep'

param environment = 'dev'
param location = 'westeurope'
// Dev runs on the default *.azurewebsites.net host; no custom domain.
param customDomain = ''
// PLACEHOLDERS — the operator's object id and the SQL Entra admin come from the owner before the
// first deploy (az ad signed-in-user show --query id).
param operatorObjectId = ''
param sqlEntraAdminLogin = ''
param sqlEntraAdminObjectId = ''
