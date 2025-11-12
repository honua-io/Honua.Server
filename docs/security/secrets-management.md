# Secrets Management

This document describes the comprehensive secrets management system in Honua Server, which supports multiple cloud providers and local development workflows.

## Table of Contents

1. [Overview](#overview)
2. [Supported Providers](#supported-providers)
3. [Quick Start](#quick-start)
4. [Configuration](#configuration)
5. [Provider Setup](#provider-setup)
6. [Usage Examples](#usage-examples)
7. [Best Practices](#best-practices)
8. [Secret Rotation](#secret-rotation)
9. [Troubleshooting](#troubleshooting)

## Overview

Honua Server implements a unified secrets management interface (`ISecretsProvider`) that abstracts access to various secret stores. This allows you to:

- Store sensitive data (connection strings, API keys, certificates) securely
- Switch between providers without code changes
- Use different providers for different environments (local dev vs. production)
- Implement consistent secret rotation strategies
- Cache secrets in memory for performance

### Architecture

```
┌─────────────────────┐
│  Application Code   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  ISecretsProvider   │  (Unified Interface)
└──────────┬──────────┘
           │
     ┌─────┴──────┬──────────┬────────────┐
     ▼            ▼          ▼            ▼
┌─────────┐  ┌────────┐  ┌──────┐  ┌──────────┐
│  Azure  │  │  AWS   │  │Vault │  │  Local   │
│   KV    │  │Secrets │  │(HC)  │  │   Dev    │
└─────────┘  └────────┘  └──────┘  └──────────┘
```

## Supported Providers

| Provider | Use Case | Authentication Methods |
|----------|----------|------------------------|
| **Azure Key Vault** | Azure cloud deployments | Managed Identity, Service Principal, Default Credential |
| **AWS Secrets Manager** | AWS cloud deployments | IAM roles, Access Keys, Profiles |
| **HashiCorp Vault** | On-premises, Kubernetes, multi-cloud | Token, AppRole, Kubernetes |
| **Local Development** | Development environments | File-based (encrypted), User Secrets |

## Quick Start

### 1. Add Secrets Management to Your Application

In `Program.cs` or `Startup.cs`:

```csharp
using Honua.Server.Core.Security.Secrets;

// Add secrets management services
builder.Services.AddSecretsManagement(builder.Configuration);

// After building the app, you can load specific secrets
var app = builder.Build();

// Load connection strings from secrets
await app.Services.LoadConnectionStringsFromSecretsAsync(
    "DefaultConnection",
    "RedisConnection"
);

// Load API keys
var apiKeys = await app.Services.LoadApiKeysFromSecretsAsync(
    "OpenAI",
    "GoogleMaps"
);
```

### 2. Configure Your Provider

In `appsettings.json`:

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "EnableCaching": true,
    "CacheDurationSeconds": 300,
    "AzureKeyVault": {
      "VaultUri": "https://your-vault.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

### 3. Use Secrets in Your Code

```csharp
public class MyService
{
    private readonly ISecretsProvider _secrets;

    public MyService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task DoWorkAsync()
    {
        // Get a single secret
        var apiKey = await _secrets.GetSecretAsync("MyApiKey");

        // Get multiple secrets efficiently
        var secrets = await _secrets.GetSecretsAsync(new[]
        {
            "ConnectionString",
            "ApiKey",
            "CertificatePath"
        });

        // Get a certificate
        var cert = await _secrets.GetCertificateAsync("MyCertificate");
    }
}
```

## Configuration

### Common Configuration Options

All providers support these common configuration options:

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault|AwsSecretsManager|HashiCorpVault|Local",
    "EnableCaching": true,
    "CacheDurationSeconds": 300,
    "ThrowOnError": false
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Provider` | string | "Local" | The secrets provider to use |
| `EnableCaching` | bool | true | Whether to cache secrets in memory |
| `CacheDurationSeconds` | int | 300 | How long to cache secrets (seconds) |
| `ThrowOnError` | bool | false | Whether to throw exceptions or return null on errors |

## Provider Setup

### Azure Key Vault

#### Prerequisites

1. Azure subscription
2. Key Vault created in Azure
3. Appropriate access policies or RBAC roles configured

#### Authentication Options

**Option 1: Managed Identity (Recommended for Azure deployments)**

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://your-vault.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

**Option 2: Service Principal**

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://your-vault.vault.azure.net/",
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "UseManagedIdentity": false
    }
  }
}
```

**Option 3: Default Azure Credential (Development)**

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://your-vault.vault.azure.net/",
      "UseManagedIdentity": false
    }
  }
}
```

This uses the Azure SDK's default credential provider chain (Azure CLI, Visual Studio, etc.).

#### Azure CLI Setup for Development

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "Your Subscription Name"

# Grant yourself access to the Key Vault
az keyvault set-policy \
  --name your-vault-name \
  --upn your-email@example.com \
  --secret-permissions get list set delete
```

#### Creating Secrets in Azure Key Vault

```bash
# Create a secret
az keyvault secret set \
  --vault-name your-vault-name \
  --name ConnectionStrings--DefaultConnection \
  --value "Server=...;Database=...;"

# Create an API key
az keyvault secret set \
  --vault-name your-vault-name \
  --name ApiKeys--OpenAI \
  --value "sk-..."
```

### AWS Secrets Manager

#### Prerequisites

1. AWS account
2. IAM role or user with appropriate permissions
3. AWS CLI configured (for development)

#### Authentication Options

**Option 1: IAM Role (Recommended for AWS deployments)**

```json
{
  "Secrets": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1"
    }
  }
}
```

The SDK will automatically use the IAM role attached to your EC2 instance, ECS task, or Lambda function.

**Option 2: Access Keys**

```json
{
  "Secrets": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "AccessKeyId": "AKIAIOSFODNN7EXAMPLE",
      "SecretAccessKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
    }
  }
}
```

**Option 3: AWS Profile**

```json
{
  "Secrets": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "ProfileName": "default"
    }
  }
}
```

**Option 4: Assume Role**

```json
{
  "Secrets": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "RoleArn": "arn:aws:iam::123456789012:role/MyRole"
    }
  }
}
```

#### AWS CLI Setup for Development

```bash
# Configure AWS CLI
aws configure

# Create a secret
aws secretsmanager create-secret \
  --name ConnectionStrings:DefaultConnection \
  --secret-string "Server=...;Database=...;" \
  --region us-east-1

# Create an API key
aws secretsmanager create-secret \
  --name ApiKeys:OpenAI \
  --secret-string "sk-..." \
  --region us-east-1
```

#### Required IAM Permissions

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret",
        "secretsmanager:ListSecrets"
      ],
      "Resource": "*"
    }
  ]
}
```

### HashiCorp Vault

#### Prerequisites

1. Vault server deployed and initialized
2. Authentication method configured (Token, AppRole, or Kubernetes)

#### Authentication Options

**Option 1: Token**

```json
{
  "Secrets": {
    "Provider": "HashiCorpVault",
    "Vault": {
      "Address": "https://vault.example.com:8200",
      "Token": "hvs.CAESIJ...",
      "MountPoint": "secret",
      "KvVersion": 2
    }
  }
}
```

**Option 2: AppRole**

```json
{
  "Secrets": {
    "Provider": "HashiCorpVault",
    "Vault": {
      "Address": "https://vault.example.com:8200",
      "RoleId": "your-role-id",
      "SecretId": "your-secret-id",
      "MountPoint": "secret",
      "KvVersion": 2
    }
  }
}
```

**Option 3: Kubernetes**

```json
{
  "Secrets": {
    "Provider": "HashiCorpVault",
    "Vault": {
      "Address": "https://vault.example.com:8200",
      "KubernetesRole": "honua-server",
      "KubernetesTokenPath": "/var/run/secrets/kubernetes.io/serviceaccount/token",
      "MountPoint": "secret",
      "KvVersion": 2
    }
  }
}
```

#### Vault CLI Setup

```bash
# Set Vault address
export VAULT_ADDR='https://vault.example.com:8200'

# Login
vault login

# Enable KV v2 secrets engine (if not already enabled)
vault secrets enable -version=2 -path=secret kv

# Create a secret
vault kv put secret/ConnectionStrings/DefaultConnection value="Server=...;Database=...;"

# Create an API key
vault kv put secret/ApiKeys/OpenAI value="sk-..."
```

#### Vault Policies

Create a policy for your application:

```hcl
# honua-server-policy.hcl
path "secret/data/ConnectionStrings/*" {
  capabilities = ["read"]
}

path "secret/data/ApiKeys/*" {
  capabilities = ["read"]
}

path "secret/data/Certificates/*" {
  capabilities = ["read"]
}
```

Apply the policy:

```bash
vault policy write honua-server honua-server-policy.hcl
```

### Local Development

#### Configuration

```json
{
  "Secrets": {
    "Provider": "Local",
    "Local": {
      "SecretsFilePath": "secrets.json",
      "EncryptFile": true,
      "WatchForChanges": true,
      "UseUserSecrets": true
    }
  }
}
```

#### Option 1: Local Secrets File

Create a `secrets.json` file in your application directory:

```json
{
  "ConnectionStrings:DefaultConnection": "Server=localhost;Database=honua;...",
  "ApiKeys:OpenAI": "sk-...",
  "ApiKeys:GoogleMaps": "AIza..."
}
```

The file will be automatically encrypted using ASP.NET Core Data Protection.

#### Option 2: User Secrets (Recommended for Development)

```bash
# Initialize user secrets
cd src/Honua.Server.Host
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;..."
dotnet user-secrets set "ApiKeys:OpenAI" "sk-..."
```

User secrets are stored outside your project directory and never checked into source control.

## Usage Examples

### Basic Secret Retrieval

```csharp
public class DatabaseService
{
    private readonly ISecretsProvider _secrets;

    public DatabaseService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<string> GetConnectionStringAsync()
    {
        return await _secrets.GetSecretAsync("ConnectionStrings:DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
    }
}
```

### Multiple Secrets at Once

```csharp
public class ExternalApiService
{
    private readonly ISecretsProvider _secrets;

    public ExternalApiService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task InitializeAsync()
    {
        var secrets = await _secrets.GetSecretsAsync(new[]
        {
            "ApiKeys:OpenAI",
            "ApiKeys:GoogleMaps",
            "ApiKeys:Stripe"
        });

        // Use the secrets
        var openAiKey = secrets["ApiKeys:OpenAI"];
        var mapsKey = secrets["ApiKeys:GoogleMaps"];
    }
}
```

### Certificate Retrieval

```csharp
public class CertificateService
{
    private readonly ISecretsProvider _secrets;

    public CertificateService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<X509Certificate2> GetSigningCertificateAsync()
    {
        var cert = await _secrets.GetCertificateAsync("Certificates:Signing");
        return cert ?? throw new InvalidOperationException("Certificate not found");
    }
}
```

### Health Checks

```csharp
public class SecretsHealthCheck : IHealthCheck
{
    private readonly ISecretsProvider _secrets;

    public SecretsHealthCheck(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isHealthy = await _secrets.HealthCheckAsync(cancellationToken);

        return isHealthy
            ? HealthCheckResult.Healthy($"Secrets provider '{_secrets.ProviderName}' is healthy")
            : HealthCheckResult.Unhealthy($"Secrets provider '{_secrets.ProviderName}' is unhealthy");
    }
}
```

### Secret Metadata

```csharp
public class SecretAuditService
{
    private readonly ISecretsProvider _secrets;

    public SecretAuditService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<bool> IsSecretExpiredAsync(string secretName)
    {
        var metadata = await _secrets.GetSecretMetadataAsync(secretName);

        if (metadata?.ExpiresOn != null)
        {
            return metadata.ExpiresOn < DateTimeOffset.UtcNow;
        }

        return false;
    }
}
```

## Best Practices

### Security

1. **Never commit secrets to source control**
   - Use `.gitignore` to exclude secrets files
   - Use environment-specific configuration files

2. **Use appropriate authentication for each environment**
   - Development: User Secrets or local provider
   - Staging/Production: Managed Identity or IAM roles
   - Never use access keys in production if alternatives exist

3. **Implement least privilege**
   - Grant only the permissions needed
   - Use separate secrets for different environments
   - Regularly audit access

4. **Enable secret versioning**
   - Keep multiple versions of secrets
   - Allows rollback if needed
   - Track changes over time

5. **Monitor secret access**
   - Enable audit logging in your secrets provider
   - Alert on unusual access patterns
   - Track who accessed what and when

### Performance

1. **Enable caching**
   - Reduces load on secrets provider
   - Improves application performance
   - Use appropriate cache duration (5-15 minutes)

2. **Batch secret retrieval**
   - Use `GetSecretsAsync()` for multiple secrets
   - Reduces network round trips
   - More efficient than individual calls

3. **Load secrets at startup**
   - Pre-load frequently used secrets
   - Reduces latency during request handling
   - Use `LoadConnectionStringsFromSecretsAsync()`

### Organization

1. **Use consistent naming conventions**
   - `ConnectionStrings:DatabaseName`
   - `ApiKeys:ServiceName`
   - `Certificates:Purpose`

2. **Group related secrets**
   - Use prefixes or paths
   - Makes management easier
   - Enables granular permissions

3. **Document secret requirements**
   - Keep a list of required secrets
   - Document format and purpose
   - Include in deployment documentation

### Development Workflow

1. **Use user secrets for local development**
   - Keeps secrets out of source control
   - Easy to manage per developer
   - Works seamlessly with the local provider

2. **Provide example configuration**
   - Include `appsettings.Example.json`
   - Document all required secrets
   - Include instructions for setup

3. **Automate secret creation**
   - Use infrastructure as code (Terraform, ARM)
   - Include in deployment scripts
   - Reduces manual errors

## Secret Rotation

### Manual Rotation

#### Azure Key Vault

```bash
# Create a new version of a secret
az keyvault secret set \
  --vault-name your-vault \
  --name MySecret \
  --value "new-secret-value"

# Old versions are retained automatically
```

#### AWS Secrets Manager

```bash
# Update a secret (creates new version)
aws secretsmanager update-secret \
  --secret-id MySecret \
  --secret-string "new-secret-value"

# Rotate a secret using Lambda
aws secretsmanager rotate-secret \
  --secret-id MySecret \
  --rotation-lambda-arn arn:aws:lambda:...
```

#### HashiCorp Vault

```bash
# Update a secret (creates new version in KV v2)
vault kv put secret/MySecret value="new-secret-value"

# View versions
vault kv metadata get secret/MySecret
```

### Automatic Rotation

#### Azure Key Vault

Use Azure Key Vault's built-in rotation for:
- Storage account keys
- Managed storage accounts
- Keys (auto-rotation)

#### AWS Secrets Manager

AWS supports automatic rotation with Lambda functions:

```json
{
  "Rotation": {
    "Enabled": true,
    "RotationLambdaARN": "arn:aws:lambda:us-east-1:123456789012:function:SecretsManagerRotation",
    "RotationRules": {
      "AutomaticallyAfterDays": 30
    }
  }
}
```

#### HashiCorp Vault

Use Vault's dynamic secrets feature for automatic rotation:

```bash
# Enable database secrets engine
vault secrets enable database

# Configure PostgreSQL
vault write database/config/postgresql \
  plugin_name=postgresql-database-plugin \
  allowed_roles="honua-server" \
  connection_url="postgresql://{{username}}:{{password}}@localhost:5432/postgres"

# Create a role
vault write database/roles/honua-server \
  db_name=postgresql \
  creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}';" \
  default_ttl="1h" \
  max_ttl="24h"
```

### Application Handling

To handle secret rotation gracefully:

```csharp
public class RotationAwareService
{
    private readonly ISecretsProvider _secrets;
    private readonly ILogger _logger;
    private string? _cachedSecret;
    private DateTime _lastRefresh = DateTime.MinValue;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    public async Task<string> GetSecretWithRotationAsync(string secretName)
    {
        if (_cachedSecret == null || DateTime.UtcNow - _lastRefresh > _refreshInterval)
        {
            _logger.LogInformation("Refreshing secret '{SecretName}'", secretName);
            _cachedSecret = await _secrets.GetSecretAsync(secretName);
            _lastRefresh = DateTime.UtcNow;
        }

        return _cachedSecret ?? throw new InvalidOperationException("Secret not found");
    }
}
```

## Troubleshooting

### Common Issues

#### "Secret not found"

**Cause**: Secret doesn't exist or incorrect name

**Solution**:
1. Verify secret name (case-sensitive in some providers)
2. Check provider-specific path format
3. Ensure secret exists: `az keyvault secret show --vault-name your-vault --name your-secret`

#### "Authentication failed"

**Cause**: Insufficient permissions or invalid credentials

**Solution**:
1. Verify authentication configuration
2. Check access policies / IAM roles
3. Test credentials manually with CLI tools
4. Review audit logs for detailed error messages

#### "Timeout connecting to secrets provider"

**Cause**: Network issues or provider unavailable

**Solution**:
1. Check network connectivity
2. Verify firewall rules
3. Check provider status page
4. Increase timeout in configuration

#### "Certificate is not in a recognized format"

**Cause**: Certificate encoding issue

**Solution**:
1. Ensure certificate is base64-encoded
2. Include full certificate chain if needed
3. Verify certificate format (PFX, PEM, etc.)

### Debugging

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Core.Security.Secrets": "Debug"
    }
  }
}
```

### Testing Secrets Provider

```csharp
public class SecretsProviderTests
{
    [Fact]
    public async Task CanRetrieveSecret()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var secret = await provider.GetSecretAsync("TestSecret");

        // Assert
        Assert.NotNull(secret);
    }

    [Fact]
    public async Task HealthCheckPasses()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var isHealthy = await provider.HealthCheckAsync();

        // Assert
        Assert.True(isHealthy);
    }
}
```

## Migration Guide

### From Configuration to Secrets Provider

Before:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Password=secret123"
  }
}
```

After:
1. Move secrets to provider
2. Update configuration
3. Update code

```csharp
// Old code
var connectionString = configuration.GetConnectionString("DefaultConnection");

// New code
var connectionString = await secretsProvider.GetSecretAsync("ConnectionStrings:DefaultConnection");
```

### From One Provider to Another

The unified interface makes switching providers easy:

1. Update configuration (change `Provider` value)
2. Update provider-specific settings
3. Migrate secrets to new provider
4. Test thoroughly

No code changes required!

## Additional Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [AWS Secrets Manager Documentation](https://docs.aws.amazon.com/secretsmanager/)
- [HashiCorp Vault Documentation](https://www.vaultproject.io/docs)
- [ASP.NET Core User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)

## Support

For issues or questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review provider-specific documentation
3. Check application logs
4. Open an issue on GitHub
