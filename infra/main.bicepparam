using 'main.bicep'

param location = 'uksouth'
param skuName = 'B1'

// DNS is at GoDaddy: A record @ -> the app's IP, TXT asuid -> the app's verification ID.
// Set customDomainCertificate = true only once the A record points at slk-vc-app-uks.
param customDomain = 'vinyl-collection.co.uk'
param customDomainCertificate = false
