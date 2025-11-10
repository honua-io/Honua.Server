# Azure DNS Provider Configuration

This document describes how to configure and use the Azure DNS provider in Honua for managing DNS records.

## Overview

The Azure DNS provider enables automated DNS record management using Azure DNS zones. It supports:

- **Multiple DNS record types**: A, AAAA, CNAME, TXT, MX, NS, SRV, CAA
- **Managed Identity authentication**: Seamless authentication in Azure environments
- **Service Principal authentication**: For CI/CD pipelines and automation
- **Azure CLI authentication**: For local development
- **Zone discovery**: Automatic discovery of DNS zones in subscriptions
- **Full CRUD operations**: Create, Read, Update, and Delete DNS records

## Authentication Methods

The Azure DNS provider supports multiple authentication methods through Azure Identity:

### 1. Managed Identity (Recommended for Production)

Managed Identity is the recommended authentication method for Azure-hosted applications.

#### System-Assigned Managed Identity

```csharp
var factory = new AzureDnsClientFactory(logger);
var armClient = factory.CreateManagedIdentityClient();

var dnsService = new DnsRecordService(logger);
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "12345678-1234-1234-1234-123456789012",
    resourceGroupName: "production-rg",
    zoneName: "example.com",
    recordName: "api",
    recordType: "A",
    recordValues: new List<string> { "203.0.113.10" },
    ttl: 3600,
    cancellationToken: CancellationToken.None);
```

#### User-Assigned Managed Identity

```csharp
var clientId = "87654321-4321-4321-4321-210987654321";
var armClient = factory.CreateManagedIdentityClient(clientId);
```

#### Required Azure RBAC Permissions

The managed identity requires the following role:

- **DNS Zone Contributor** on the DNS zone or resource group
  - Or: `Microsoft.Network/dnsZones/read`
  - And: `Microsoft.Network/dnsZones/*/read`
  - And: `Microsoft.Network/dnsZones/*/write`
  - And: `Microsoft.Network/dnsZones/*/delete`

**Assign permissions using Azure CLI:**

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(az identity show \
  --resource-group myapp-rg \
  --name myapp-identity \
  --query principalId \
  --output tsv)

# Assign DNS Zone Contributor role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "DNS Zone Contributor" \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{rg-name}/providers/Microsoft.Network/dnsZones/{zone-name}"
```

### 2. Service Principal (CI/CD)

Service Principal authentication is ideal for CI/CD pipelines and automation scenarios.

```csharp
var factory = new AzureDnsClientFactory(logger);
var armClient = factory.CreateServicePrincipalClient(
    tenantId: "11111111-1111-1111-1111-111111111111",
    clientId: "22222222-2222-2222-2222-222222222222",
    clientSecret: Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET")!);
```

**Environment Variables:**

```bash
export AZURE_TENANT_ID="11111111-1111-1111-1111-111111111111"
export AZURE_CLIENT_ID="22222222-2222-2222-2222-222222222222"
export AZURE_CLIENT_SECRET="your-client-secret"
```

**Create Service Principal:**

```bash
# Create service principal
az ad sp create-for-rbac \
  --name "honua-dns-automation" \
  --role "DNS Zone Contributor" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/{rg-name}" \
  --sdk-auth

# Output includes:
# - clientId (AZURE_CLIENT_ID)
# - clientSecret (AZURE_CLIENT_SECRET)
# - tenantId (AZURE_TENANT_ID)
```

### 3. Azure CLI (Local Development)

Azure CLI authentication is convenient for local development.

```csharp
var factory = new AzureDnsClientFactory(logger);
var armClient = factory.CreateAzureCliClient();
```

**Prerequisites:**

```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login
az login

# Set subscription
az account set --subscription "your-subscription-id"
```

### 4. DefaultAzureCredential (Automatic)

DefaultAzureCredential automatically tries multiple authentication methods in order:

```csharp
var factory = new AzureDnsClientFactory(logger);
var armClient = factory.CreateClient();
```

**Authentication Chain:**
1. Environment variables (Service Principal)
2. Managed Identity
3. Azure CLI
4. Azure PowerShell
5. Visual Studio
6. Visual Studio Code

## DNS Operations

### Create/Update DNS Records (Upsert)

#### A Record

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "www",
    recordType: "A",
    recordValues: new List<string> { "192.0.2.1", "192.0.2.2" },
    ttl: 3600,
    cancellationToken: CancellationToken.None);

if (result.Success)
{
    Console.WriteLine($"Record created: {result.Message}");
    Console.WriteLine($"Change ID: {result.ChangeId}");
}
```

#### CNAME Record

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "blog",
    recordType: "CNAME",
    recordValues: new List<string> { "example.com" },
    ttl: 3600,
    cancellationToken: CancellationToken.None);
```

#### TXT Record (ACME Challenge)

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "_acme-challenge",
    recordType: "TXT",
    recordValues: new List<string> { "verification-token-12345" },
    ttl: 300,
    cancellationToken: CancellationToken.None);
```

#### MX Record

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "@",
    recordType: "MX",
    recordValues: new List<string>
    {
        "10 mail1.example.com",
        "20 mail2.example.com"
    },
    ttl: 3600,
    cancellationToken: CancellationToken.None);
```

#### CAA Record (Certificate Authority Authorization)

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "@",
    recordType: "CAA",
    recordValues: new List<string>
    {
        "0 issue letsencrypt.org",
        "0 issuewild letsencrypt.org"
    },
    ttl: 3600,
    cancellationToken: CancellationToken.None);
```

### Read DNS Records

```csharp
var record = await dnsService.GetAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "www",
    recordType: "A",
    cancellationToken: CancellationToken.None);

if (record != null)
{
    Console.WriteLine($"Name: {record.Name}");
    Console.WriteLine($"Type: {record.Type}");
    Console.WriteLine($"TTL: {record.Ttl}");
    Console.WriteLine($"Values: {string.Join(", ", record.Values)}");
}
```

### Delete DNS Records

```csharp
var result = await dnsService.DeleteAzureDnsRecordAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "rg-name",
    zoneName: "example.com",
    recordName: "www",
    recordType: "A",
    cancellationToken: CancellationToken.None);

if (result.Success)
{
    Console.WriteLine("Record deleted successfully");
}
```

### List DNS Zones

#### List all zones in a subscription

```csharp
var zones = await dnsService.ListAzureDnsZonesAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: null, // null = all resource groups
    cancellationToken: CancellationToken.None);

foreach (var zone in zones)
{
    Console.WriteLine($"Zone: {zone}");
}
```

#### List zones in a specific resource group

```csharp
var zones = await dnsService.ListAzureDnsZonesAsync(
    armClient,
    subscriptionId: "sub-id",
    resourceGroupName: "production-rg",
    cancellationToken: CancellationToken.None);
```

## Supported DNS Record Types

| Record Type | Description | Example Values |
|-------------|-------------|----------------|
| **A** | IPv4 address | `192.0.2.1` |
| **AAAA** | IPv6 address | `2001:0db8::1` |
| **CNAME** | Canonical name | `example.com` |
| **TXT** | Text record | `v=spf1 include:_spf.google.com ~all` |
| **MX** | Mail exchange | `10 mail.example.com` |
| **NS** | Name server | `ns1.example.com` |
| **SRV** | Service record | `10 5 5060 sipserver.example.com` |
| **CAA** | Certificate authority | `0 issue letsencrypt.org` |

## Error Handling

All DNS operations return a `DnsOperationResult` with success/failure status:

```csharp
var result = await dnsService.UpsertAzureDnsRecordAsync(...);

if (result.Success)
{
    logger.LogInformation("DNS operation succeeded: {Message}", result.Message);
    logger.LogInformation("Change ID: {ChangeId}", result.ChangeId);
}
else
{
    logger.LogError("DNS operation failed: {Message}", result.Message);
    // Implement retry logic or alerting
}
```

## Common Patterns

### ACME DNS-01 Challenge

```csharp
// 1. Create TXT record for ACME challenge
var createResult = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId,
    resourceGroupName,
    zoneName,
    recordName: "_acme-challenge",
    recordType: "TXT",
    recordValues: new List<string> { challengeToken },
    ttl: 60,
    cancellationToken);

// 2. Wait for DNS propagation
await Task.Delay(TimeSpan.FromSeconds(30));

// 3. Verify propagation
var verified = await dnsService.VerifyDnsPropagationAsync(
    $"_acme-challenge.{zoneName}",
    "TXT",
    challengeToken);

// 4. Complete ACME challenge
// ...

// 5. Clean up TXT record
var deleteResult = await dnsService.DeleteAzureDnsRecordAsync(
    armClient,
    subscriptionId,
    resourceGroupName,
    zoneName,
    "_acme-challenge",
    "TXT",
    cancellationToken);
```

### Blue-Green Deployment

```csharp
// Switch traffic to green environment
var result = await dnsService.UpsertAzureDnsRecordAsync(
    armClient,
    subscriptionId,
    resourceGroupName,
    zoneName,
    recordName: "api",
    recordType: "A",
    recordValues: new List<string> { greenEnvironmentIp },
    ttl: 300, // Low TTL for quick rollback
    cancellationToken);

// Monitor for issues
await Task.Delay(TimeSpan.FromMinutes(5));

// Rollback if needed
if (healthCheckFailed)
{
    await dnsService.UpsertAzureDnsRecordAsync(
        armClient,
        subscriptionId,
        resourceGroupName,
        zoneName,
        recordName: "api",
        recordType: "A",
        recordValues: new List<string> { blueEnvironmentIp },
        ttl: 300,
        cancellationToken);
}
```

## Troubleshooting

### Authentication Issues

**Problem**: `Azure.Identity.CredentialUnavailableException`

**Solution**:
```bash
# Verify Azure CLI is logged in
az account show

# Re-login if needed
az login

# Verify subscription
az account set --subscription "your-subscription-id"
```

### Permission Issues

**Problem**: `AuthorizationFailed` or `Forbidden` errors

**Solution**:
```bash
# Check role assignments
az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/{sub-id}/resourceGroups/{rg-name}/providers/Microsoft.Network/dnsZones/{zone-name}"

# Assign DNS Zone Contributor role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "DNS Zone Contributor" \
  --scope "/subscriptions/{sub-id}/resourceGroups/{rg-name}/providers/Microsoft.Network/dnsZones/{zone-name}"
```

### DNS Zone Not Found

**Problem**: `ResourceNotFound` error

**Solution**:
```bash
# List all DNS zones
az network dns zone list --resource-group your-rg --output table

# Verify zone exists
az network dns zone show \
  --resource-group your-rg \
  --name example.com
```

### Propagation Delays

DNS records can take time to propagate globally. Use the built-in verification:

```csharp
var propagated = await dnsService.VerifyDnsPropagationAsync(
    domain: "www.example.com",
    recordType: "A",
    expectedValue: "192.0.2.1",
    maxAttempts: 30, // 30 attempts * 2 seconds = 60 seconds
    cancellationToken: CancellationToken.None);
```

## Best Practices

1. **Use Managed Identity in production** for enhanced security
2. **Set appropriate TTL values**:
   - High TTL (3600+) for stable records
   - Low TTL (60-300) for records that may change frequently
3. **Implement retry logic** for transient failures
4. **Log all DNS operations** for audit trails
5. **Validate input parameters** before making API calls
6. **Use cancellation tokens** for long-running operations
7. **Test in development environment** before production changes
8. **Monitor DNS changes** through Azure Monitor

## Integration with Certificate Management

The Azure DNS provider integrates seamlessly with Let's Encrypt certificate automation:

```csharp
// Certificate renewal process
var certService = new CertificateRenewalService(dnsService, logger);
var result = await certService.RenewCertificateAsync(
    domain: "example.com",
    dnsProvider: "azure",
    azureConfig: new AzureDnsConfig
    {
        SubscriptionId = "sub-id",
        ResourceGroupName = "rg-name",
        ZoneName = "example.com"
    },
    cancellationToken: CancellationToken.None);
```

## References

- [Azure DNS Documentation](https://docs.microsoft.com/azure/dns/)
- [Azure.ResourceManager.Dns Package](https://www.nuget.org/packages/Azure.ResourceManager.Dns)
- [Azure Identity Package](https://www.nuget.org/packages/Azure.Identity)
- [Managed Identity Overview](https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)
- [Azure RBAC Documentation](https://docs.microsoft.com/azure/role-based-access-control/)
