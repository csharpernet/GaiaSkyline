using 'main.bicep'

param environment = 'prod'
param location = 'westeurope'
// PLACEHOLDERS — provided by the owner before the prod deploy (never invented here):
// the public domain Front Door serves, the operator's object id, and the SQL Entra admin.
param customDomain = ''
param operatorObjectId = ''
param sqlEntraAdminLogin = ''
param sqlEntraAdminObjectId = ''
