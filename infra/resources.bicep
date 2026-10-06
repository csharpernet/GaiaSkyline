// GaiaSkyline — all resources for one environment (Stage 8 Part B). Resource-group scope; called by
// main.bicep. Sizing: dev = B1 + SQL Basic (cheapest that runs Always On); prod = P0v3 + SQL S1
// (ADR 0023 documents the cost choice). Secrets live in Key Vault; the web app reaches Key Vault,
// Blob and SQL with its system-assigned managed identity — no connection-string passwords anywhere.

@allowed(['dev', 'prod'])
param environment string

param location string
param customDomain string
param operatorObjectId string
param sqlEntraAdminLogin string
param sqlEntraAdminObjectId string

var isProd = environment == 'prod'
var suffix = 'gaiaskyline-${environment}'
// Storage account names: 3-24 lowercase alphanumerics.
var storageName = 'stgaiaskyline${environment}'
var sqlDatabaseName = 'gaiaskyline'
var frontDoorProfileName = 'afd-${suffix}'

// ---------- Log Analytics + Application Insights ----------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${suffix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: isProd ? 90 : 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

// ---------- Storage (media, invoices, calendar, exports, data-protection keys) ----------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: isProd ? 'Standard_ZRS' : 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }

  resource blobService 'blobServices' = {
    name: 'default'
    properties: {
      deleteRetentionPolicy: { enabled: true, days: 30 }
      containerDeleteRetentionPolicy: { enabled: true, days: 30 }
    }

    resource media 'containers' = { name: 'media' }
    resource invoices 'containers' = { name: 'invoices' }
    resource calendar 'containers' = { name: 'calendar' }
    resource exports 'containers' = { name: 'exports' }
    resource dataprotection 'containers' = { name: 'dataprotection' }
  }
}

// Old exports age out on their own; everything else is kept (media is the live site content).
resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'expire-old-exports'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: { blobTypes: ['blockBlob'], prefixMatch: ['exports/'] }
            actions: { baseBlob: { delete: { daysAfterModificationGreaterThan: 90 } } }
          }
        }
      ]
    }
  }
}

// ---------- Key Vault (RBAC mode) ----------

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${suffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    enablePurgeProtection: true
  }

  // Wraps the Data Protection key ring at rest (the app's mandatory production key story).
  resource dataProtectionKey 'keys' = {
    name: 'dataprotection'
    properties: {
      kty: 'RSA'
      keySize: 2048
      keyOps: ['wrapKey', 'unwrapKey']
    }
  }
}

// ---------- Azure SQL (Entra-only authentication) ----------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${suffix}'
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // App Service outbound is not VNet-integrated at this size; SQL firewall below.
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: empty(sqlEntraAdminLogin) ? 'placeholder-admin' : sqlEntraAdminLogin
      sid: empty(sqlEntraAdminObjectId) ? '00000000-0000-0000-0000-000000000000' : sqlEntraAdminObjectId
      tenantId: subscription().tenantId
    }
  }

  resource allowAzureServices 'firewallRules' = {
    name: 'AllowAzureServices'
    properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: isProd ? { name: 'S1', tier: 'Standard' } : { name: 'Basic', tier: 'Basic' }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: isProd ? 'Zone' : 'Local'
  }
}

// Point-in-time restore: 7 days dev / 35 days prod; weekly long-term retention kept 8 weeks.
resource pitr 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = {
  parent: sqlDatabase
  name: 'default'
  properties: { retentionDays: isProd ? 35 : 7 }
}

resource ltr 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01-preview' = {
  parent: sqlDatabase
  name: 'default'
  properties: { weeklyRetention: 'P8W' }
}

// ---------- App Service ----------

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-${suffix}'
  location: location
  kind: 'linux'
  sku: isProd ? { name: 'P0v3', tier: 'Premium0V3' } : { name: 'B1', tier: 'Basic' }
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${suffix}'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      // Only Front Door may talk to the app directly (prod); dev stays open for simpler verification.
      ipSecurityRestrictionsDefaultAction: isProd ? 'Deny' : 'Allow'
      ipSecurityRestrictions: isProd
        ? [
            {
              name: 'front-door-only'
              priority: 100
              action: 'Allow'
              tag: 'ServiceTag'
              ipAddress: 'AzureFrontDoor.Backend'
            }
          ]
        : []
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Development' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsights.properties.ConnectionString }
        { name: 'Azure__StorageAccountName', value: storage.name }
        { name: 'Azure__KeyVaultUri', value: keyVault.properties.vaultUri }
        { name: 'Azure__DataProtectionKeyName', value: 'dataprotection' }
        { name: 'BackgroundJobs__Enabled', value: 'true' }
        { name: 'Features__SeedContentOnStartup', value: isProd ? 'false' : 'true' }
        { name: 'Email__SiteBaseUrl', value: isProd && !empty(customDomain) ? 'https://${customDomain}' : 'https://app-${suffix}.azurewebsites.net' }
        {
          // Entra-authenticated SQL: the managed identity is the principal; no password in the string.
          name: 'ConnectionStrings__Default'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;'
        }
      ]
    }
  }
}

// Prod deploys land on the staging slot first; the pipeline smoke-tests then swaps (Part C).
resource stagingSlot 'Microsoft.Web/sites/slots@2024-04-01' = if (isProd) {
  parent: webApp
  name: 'staging'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: webApp.properties.siteConfig
  }
}

// ---------- RBAC: managed identity → Blob, Key Vault; operator break-glass ----------

var roleStorageBlobDataContributor = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var roleKeyVaultCryptoUser = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions', '12338af0-0e69-4776-bea7-57ae8d297424')
var roleKeyVaultSecretsUser = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var roleKeyVaultAdministrator = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions', '00482a5a-887f-4fb3-b363-3b7fe8e74483')

resource appBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, webApp.id, roleStorageBlobDataContributor)
  scope: storage
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: roleStorageBlobDataContributor
    principalType: 'ServicePrincipal'
  }
}

resource appKvCryptoRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, roleKeyVaultCryptoUser)
  scope: keyVault
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: roleKeyVaultCryptoUser
    principalType: 'ServicePrincipal'
  }
}

resource appKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, roleKeyVaultSecretsUser)
  scope: keyVault
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: roleKeyVaultSecretsUser
    principalType: 'ServicePrincipal'
  }
}

resource slotBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (isProd) {
  name: guid(storage.id, '${webApp.id}-staging', roleStorageBlobDataContributor)
  scope: storage
  properties: {
    principalId: stagingSlot.identity.principalId
    roleDefinitionId: roleStorageBlobDataContributor
    principalType: 'ServicePrincipal'
  }
}

resource slotKvCryptoRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (isProd) {
  name: guid(keyVault.id, '${webApp.id}-staging', roleKeyVaultCryptoUser)
  scope: keyVault
  properties: {
    principalId: stagingSlot.identity.principalId
    roleDefinitionId: roleKeyVaultCryptoUser
    principalType: 'ServicePrincipal'
  }
}

resource slotKvSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (isProd) {
  name: guid(keyVault.id, '${webApp.id}-staging', roleKeyVaultSecretsUser)
  scope: keyVault
  properties: {
    principalId: stagingSlot.identity.principalId
    roleDefinitionId: roleKeyVaultSecretsUser
    principalType: 'ServicePrincipal'
  }
}

resource operatorKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(operatorObjectId)) {
  name: guid(keyVault.id, operatorObjectId, roleKeyVaultAdministrator)
  scope: keyVault
  properties: {
    principalId: operatorObjectId
    roleDefinitionId: roleKeyVaultAdministrator
    principalType: 'User'
  }
}

resource operatorBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(operatorObjectId)) {
  name: guid(storage.id, operatorObjectId, roleStorageBlobDataContributor)
  scope: storage
  properties: {
    principalId: operatorObjectId
    roleDefinitionId: roleStorageBlobDataContributor
    principalType: 'User'
  }
}

// ---------- Front Door Standard ----------

resource frontDoor 'Microsoft.Cdn/profiles@2024-02-01' = {
  name: frontDoorProfileName
  location: 'global'
  sku: { name: 'Standard_AzureFrontDoor' }
}

resource fdEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-02-01' = {
  parent: frontDoor
  name: suffix
  location: 'global'
  properties: { enabledState: 'Enabled' }
}

resource fdOriginGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: frontDoor
  name: 'app'
  properties: {
    loadBalancingSettings: { sampleSize: 4, successfulSamplesRequired: 3, additionalLatencyInMilliseconds: 50 }
    healthProbeSettings: {
      probePath: '/health/live'
      probeRequestType: 'GET'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 60
    }
  }
}

resource fdOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: fdOriginGroup
  name: 'webapp'
  properties: {
    hostName: webApp.properties.defaultHostName
    httpPort: 80
    httpsPort: 443
    originHostHeader: webApp.properties.defaultHostName
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
  }
}

// The custom domain exists only in prod and only once the owner provides it (DNS steps are manual).
resource fdCustomDomain 'Microsoft.Cdn/profiles/customDomains@2024-02-01' = if (isProd && !empty(customDomain)) {
  parent: frontDoor
  name: replace(customDomain, '.', '-')
  properties: {
    hostName: customDomain
    tlsSettings: { certificateType: 'ManagedCertificate', minimumTlsVersion: 'TLS12' }
  }
}

// /media and static assets are cached at the edge; the query string stays in the cache key so the
// media version token (?v=) busts stale copies (Stage 8 Part B user addition).
resource fdRouteMedia 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: fdEndpoint
  name: 'media'
  properties: {
    originGroup: { id: fdOriginGroup.id }
    patternsToMatch: ['/media/*', '/css/*', '/js/*', '/favicon*']
    supportedProtocols: ['Https']
    httpsRedirect: 'Enabled'
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    customDomains: isProd && !empty(customDomain) ? [{ id: fdCustomDomain.id }] : []
    cacheConfiguration: {
      queryStringCachingBehavior: 'UseQueryString'
      compressionSettings: {
        isCompressionEnabled: true
        contentTypesToCompress: [
          'text/css'
          'text/javascript'
          'application/javascript'
          'image/svg+xml'
        ]
      }
    }
  }
  dependsOn: [fdOrigin]
}

resource fdRouteApp 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: fdEndpoint
  name: 'app'
  properties: {
    originGroup: { id: fdOriginGroup.id }
    patternsToMatch: ['/*']
    supportedProtocols: ['Https']
    httpsRedirect: 'Enabled'
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    customDomains: isProd && !empty(customDomain) ? [{ id: fdCustomDomain.id }] : []
    // Dynamic pages are not edge-cached; the app's output cache owns that layer.
  }
  dependsOn: [fdOrigin]
}

// ---------- Outputs ----------

output webAppName string = webApp.name
output webAppDefaultHost string = webApp.properties.defaultHostName
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output storageAccountName string = storage.name
output keyVaultName string = keyVault.name
output frontDoorEndpointHost string = fdEndpoint.properties.hostName
output appInsightsConnectionString string = appInsights.properties.ConnectionString
