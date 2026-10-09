// Updates an existing custom domain binding on a web app to use SNI SSL with the given certificate.

@description('Name of the existing web app.')
param appName string

@description('Custom domain already bound to the app.')
param hostName string

@description('Thumbprint of the certificate to bind.')
param thumbprint string

resource app 'Microsoft.Web/sites@2024-04-01' existing = {
  name: appName
}

resource binding 'Microsoft.Web/sites/hostNameBindings@2024-04-01' = {
  parent: app
  name: hostName
  properties: {
    siteName: appName
    hostNameType: 'Verified'
    customHostNameDnsRecordType: 'A'
    sslState: 'SniEnabled'
    thumbprint: thumbprint
  }
}
