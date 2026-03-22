param location string = resourceGroup().location
param appName string = 'flightprep'
param dbAdminPassword string

var planName = '${appName}-plan'
var appServiceName = '${appName}-web'
var pgServerName = '${appName}-db'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: 'B2'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Host=${pgServer.properties.fullyQualifiedDomainName};Port=5432;Database=flightprep;Username=fpuser;Password=${dbAdminPassword};Ssl Mode=Require'
        }
      ]
    }
  }
}

resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: pgServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: 'fpuser'
    administratorLoginPassword: dbAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
    }
  }
}

resource pgDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: pgServer
  name: 'flightprep'
}

output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output pgServerFqdn string = pgServer.properties.fullyQualifiedDomainName
