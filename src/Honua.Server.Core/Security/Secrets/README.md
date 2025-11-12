# Secrets Management

This directory contains the implementation of Honua Server's comprehensive secrets management system.

## Overview

The secrets management system provides a unified interface (`ISecretsProvider`) for accessing secrets across multiple cloud providers and local development environments.

## Files

| File | Description |
|------|-------------|
| `ISecretsProvider.cs` | Core interface defining secrets provider contract |
| `SecretsConfiguration.cs` | Configuration models for all providers |
| `AzureKeyVaultSecretsProvider.cs` | Azure Key Vault implementation |
| `AwsSecretsManagerProvider.cs` | AWS Secrets Manager implementation |
| `HashiCorpVaultProvider.cs` | HashiCorp Vault implementation |
| `LocalDevelopmentSecretsProvider.cs` | Local file-based implementation for development |
| `SecretsServiceCollectionExtensions.cs` | DI registration extensions |

## Quick Start

### 1. Register Secrets Provider

In `Program.cs`:

```csharp
using Honua.Server.Core.Security.Secrets;

builder.Services.AddSecretsManagement(builder.Configuration);
```

### 2. Configure Provider

In `appsettings.json`:

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

### 3. Use in Your Code

```csharp
public class MyService
{
    private readonly ISecretsProvider _secrets;

    public MyService(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<string> GetApiKeyAsync()
    {
        return await _secrets.GetSecretAsync("ApiKeys:MyService")
            ?? throw new InvalidOperationException("API key not found");
    }
}
```

## Supported Providers

### Azure Key Vault

Best for: Azure cloud deployments

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

### AWS Secrets Manager

Best for: AWS cloud deployments

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

### HashiCorp Vault

Best for: On-premises, Kubernetes, multi-cloud

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

### Local Development

Best for: Local development environments

```json
{
  "Secrets": {
    "Provider": "Local",
    "Local": {
      "UseUserSecrets": true,
      "EncryptFile": true
    }
  }
}
```

## Interface Methods

### Core Operations

- `GetSecretAsync(string secretName)` - Retrieve a single secret
- `GetSecretVersionAsync(string secretName, string version)` - Get specific version
- `GetSecretsAsync(IEnumerable<string> secretNames)` - Retrieve multiple secrets
- `SetSecretAsync(string secretName, string secretValue)` - Store/update a secret
- `DeleteSecretAsync(string secretName)` - Delete a secret

### Special Operations

- `GetCertificateAsync(string certificateName)` - Retrieve X509 certificate
- `ListSecretsAsync()` - List all available secret names
- `HealthCheckAsync()` - Check provider health
- `GetSecretMetadataAsync(string secretName)` - Get metadata without value

## Features

- **Unified Interface**: Switch providers without code changes
- **Caching**: In-memory caching for performance
- **Multiple Authentication**: Support for managed identity, IAM roles, tokens, etc.
- **Certificate Support**: Retrieve X509 certificates
- **Versioning**: Access specific secret versions (where supported)
- **Health Checks**: Verify provider connectivity
- **Error Handling**: Configurable exception vs. null behavior

## Best Practices

1. **Use appropriate provider per environment**
   - Development: Local provider with user secrets
   - Production: Cloud provider with managed identity

2. **Enable caching**
   - Reduces load on secrets provider
   - Default: 5 minutes

3. **Never commit secrets**
   - Use `.gitignore` for local secrets files
   - Use user secrets for development

4. **Implement rotation**
   - Regular rotation of sensitive secrets
   - Use provider's rotation features

5. **Monitor access**
   - Enable audit logging in your provider
   - Alert on unusual patterns

## Documentation

For detailed documentation, see:
- [Comprehensive Secrets Management Guide](../../../../../../docs/security/secrets-management.md)
- [SECURITY.md](../../../../../../SECURITY.md) - Security overview
- [Example Configurations](../../../../../../docs/security/secrets-examples/)

## Dependencies

- **Azure.Security.KeyVault.Secrets** - Azure Key Vault client
- **Azure.Security.KeyVault.Certificates** - Azure certificate support
- **Azure.Identity** - Azure authentication
- **AWSSDK.SecretsManager** - AWS Secrets Manager client
- **AWSSDK.SecurityToken** - AWS STS for role assumption
- **HttpClient** - HashiCorp Vault HTTP API

## Testing

Example test setup:

```csharp
public class SecretsProviderTests
{
    [Fact]
    public async Task CanRetrieveSecret()
    {
        var config = new SecretsConfiguration
        {
            Provider = SecretsProviders.Local,
            Local = new LocalSecretsConfiguration
            {
                UseUserSecrets = false,
                SecretsFilePath = "test-secrets.json"
            }
        };

        var provider = new LocalDevelopmentSecretsProvider(
            Options.Create(config),
            new NullLogger<LocalDevelopmentSecretsProvider>());

        var secret = await provider.GetSecretAsync("TestSecret");
        Assert.NotNull(secret);
    }
}
```

## Support

For issues or questions:
1. Review the comprehensive documentation
2. Check provider-specific documentation
3. Open an issue on GitHub
