// Hosting for the VinylCollection Blazor Server app.
// Deploy at resource group scope:
//   az deployment group create -g slk-vc-rg-uks -f infra/main.bicep -p infra/main.bicepparam

@description('Azure region for all resources. Must have a code in regionCodes.')
@allowed([
  'uksouth'
  'ukwest'
])
param location string = 'uksouth'

// Naming scheme: slk-vc-[resourcetype]-[region]
var regionCodes = {
  uksouth: 'uks'
  ukwest: 'ukw'
}
var regionCode = regionCodes[location]
var namePrefix = 'slk-vc'

@description('App Service Plan SKU. B1 supports Always On; F1 is free but sleeps when idle.')
@allowed([
  'F1'
  'B1'
  'B2'
])
param skuName string = 'B1'

@description('Custom domain for the web app, e.g. vinyl-collection.co.uk. Leave empty for none.')
param customDomain string = ''

@description('''Issue a free App Service Managed Certificate for customDomain and bind it.
Deploy with false first (adds the domain), point the domain's A record at the app,
then deploy with true: the certificate is only issued once DNS resolves to the app.''')
param customDomainCertificate bool = false

var cosmosDatabaseName = 'VinylCollection'

// Built-in "Cosmos DB Built-in Data Reader" data-plane role: the app never writes
var cosmosDataReaderRoleId = '00000000-0000-0000-0000-000000000001'

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${namePrefix}-cosmos-${regionCode}' // globally unique
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [
      {
        name: 'EnableServerless' // pay per request; suits a small, low-traffic collection
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    minimalTlsVersion: 'Tls12'
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmos
  name: cosmosDatabaseName
  properties: {
    resource: {
      id: cosmosDatabaseName
    }
  }
}

// Must match VinylDbContext: container "Albums", partitioned on the album id
resource albumsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: cosmosDatabase
  name: 'Albums'
  properties: {
    resource: {
      id: 'Albums'
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${namePrefix}-asp-${regionCode}'
  location: location
  kind: 'linux'
  sku: {
    name: skuName
  }
  properties: {
    reserved: true // required for Linux plans
  }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: '${namePrefix}-app-${regionCode}' // globally unique
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned' // used to authenticate to Cosmos DB without keys
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: true // Blazor Server circuits need sticky sessions
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      webSocketsEnabled: true // Blazor Server uses SignalR over WebSockets
      alwaysOn: skuName != 'F1' // not available on the free tier
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Cosmos__Endpoint'
          value: cosmos.properties.documentEndpoint
        }
        {
          name: 'Cosmos__DatabaseName'
          value: cosmosDatabaseName
        }
      ]
    }
  }
}

resource appCosmosAccess 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, app.id, cosmosDataReaderRoleId)
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/${cosmosDataReaderRoleId}'
    principalId: app.identity.principalId
    scope: cosmos.id
  }
}

// ─── Custom domain ───
// Pass 1 (customDomainCertificate = false): add the domain without TLS. Azure verifies
// ownership with the asuid.<domain> TXT record, so this works before the A record moves.
resource hostNameBinding 'Microsoft.Web/sites/hostNameBindings@2024-04-01' = if (!empty(customDomain) && !customDomainCertificate) {
  parent: app
  name: !empty(customDomain) ? customDomain : 'none'
  properties: {
    siteName: app.name
    hostNameType: 'Verified'
    customHostNameDnsRecordType: 'A'
    sslState: 'Disabled'
  }
}

// Pass 2 (customDomainCertificate = true): the binding already exists from pass 1, so it
// isn't redeclared here (that would briefly strip TLS on every deploy). Issue the managed
// certificate, then switch the binding to SNI SSL in a module, since a resource can't be
// declared twice in one template.
resource certificate 'Microsoft.Web/certificates@2024-04-01' = if (!empty(customDomain) && customDomainCertificate) {
  name: '${namePrefix}-cert-${regionCode}'
  location: location
  properties: {
    serverFarmId: plan.id
    canonicalName: customDomain
  }
}

module sslBinding 'modules/hostNameSslBinding.bicep' = if (!empty(customDomain) && customDomainCertificate) {
  name: 'hostNameSslBinding'
  params: {
    appName: app.name
    hostName: customDomain
    thumbprint: certificate!.properties.thumbprint
  }
}

output appName string = app.name
output appUrl string = 'https://${!empty(customDomain) ? customDomain : app.properties.defaultHostName}'
output cosmosEndpoint string = cosmos.properties.documentEndpoint
