# Connection String Encryption

This document describes the connection string encryption feature in Honua Server, which provides security for sensitive database credentials stored in configuration files.

## Overview

Connection string encryption uses ASP.NET Core Data Protection API to encrypt connection strings at rest. This protects sensitive information like database passwords from being exposed in configuration files or metadata.

## Features

- **Automatic Encryption/Decryption**: Connection strings are automatically decrypted when loaded by data store providers
- **Multiple Key Storage Options**: Support for file system, Azure Key Vault, AWS KMS, and GCP KMS
- **Backward Compatible**: Unencrypted connection strings continue to work
- **Key Rotation**: Support for rotating encryption keys without downtime
- **Migration Tool**: CLI tool to encrypt existing connection strings

## Configuration

### Basic Configuration (File System)

```json
{
  "Honua": {
    "Security": {
      "ConnectionStringEncryption": {
        "Enabled": true,
        "KeyStorageProvider": "FileSystem",
        "KeyStorageDirectory": "/var/honua/keys",
        "ApplicationName": "Honua.Server",
        "KeyLifetimeDays": 90
      }
    }
  }
}
```

### Azure Key Vault

```json
{
  "Honua": {
    "Security": {
      "ConnectionStringEncryption": {
        "Enabled": true,
        "KeyStorageProvider": "AzureKeyVault",
        "AzureKeyVaultUri": "https://myvault.vault.azure.net/",
        "AzureKeyVaultKeyName": "honua-connection-strings",
        "ApplicationName": "Honua.Server"
      }
    }
  }
}
```

**Requirements**:
- Azure.Identity package (included)
- Managed Identity or Service Principal authentication configured
- Key Vault permissions: Get, List, Create, Encrypt, Decrypt

### AWS KMS

```json
{
  "Honua": {
    "Security": {
      "ConnectionStringEncryption": {
        "Enabled": true,
        "KeyStorageProvider": "AwsKms",
        "AwsKmsKeyId": "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
        "AwsRegion": "us-east-1",
        "ApplicationName": "Honua.Server"
      }
    }
  }
}
```

**Note**: Currently uses file system storage with KMS planned for future enhancement. See implementation TODOs.

### GCP KMS

```json
{
  "Honua": {
    "Security": {
      "ConnectionStringEncryption": {
        "Enabled": true,
        "KeyStorageProvider": "GcpKms",
        "GcpKmsKeyResourceName": "projects/my-project/locations/us-central1/keyRings/honua/cryptoKeys/connection-strings",
        "ApplicationName": "Honua.Server"
      }
    }
  }
}
```

**Note**: Currently uses file system storage with KMS planned for future enhancement. See implementation TODOs.

## Configuration Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `Enabled` | Enable/disable connection string encryption | No | `true` |
| `KeyStorageProvider` | Key storage backend: FileSystem, AzureKeyVault, AwsKms, GcpKms | Yes | `FileSystem` |
| `KeyStorageDirectory` | Directory for file system key storage | No | OS-specific app data directory |
| `ApplicationName` | Application name for key isolation | No | `Honua.Server` |
| `KeyLifetimeDays` | Key lifetime before rotation recommended | No | `90` |
| `AzureKeyVaultUri` | Azure Key Vault URI | For AzureKeyVault | - |
| `AzureKeyVaultKeyName` | Azure Key Vault key name | For AzureKeyVault | - |
| `AwsKmsKeyId` | AWS KMS key ID or ARN | For AwsKms | - |
| `AwsRegion` | AWS region | For AwsKms | - |
| `GcpKmsKeyResourceName` | GCP KMS key resource name | For GcpKms | - |

## Using Encryption

### Encrypting Connection Strings

Use the CLI tool to encrypt connection strings in your metadata file:

```bash
# Dry run to see what would be encrypted
honua encrypt-connections --input metadata.json --dry-run

# Encrypt and create new file
honua encrypt-connections --input metadata.json --output metadata.encrypted.json

# Encrypt in place (creates backup)
honua encrypt-connections --input metadata.json --in-place
```

### Encrypted Connection String Format

Encrypted connection strings have the prefix `ENC:` followed by Base64-encoded encrypted data:

```json
{
  "dataSources": [
    {
      "id": "main-db",
      "provider": "postgis",
      "connectionString": "ENC:Q2lwaGVyVGV4dEhlcmU..."
    }
  ]
}
```

### Manual Encryption

You can also use the encryption service programmatically:

```csharp
var encryptionService = serviceProvider.GetRequiredService<IConnectionStringEncryptionService>();

// Encrypt
var plainText = "Server=localhost;Database=mydb;User=admin;Password=secret;";
var encrypted = await encryptionService.EncryptAsync(plainText);

// Decrypt
var decrypted = await encryptionService.DecryptAsync(encrypted);

// Check if encrypted
bool isEncrypted = encryptionService.IsEncrypted(encrypted);
```

## Key Rotation

### When to Rotate Keys

- Every 90 days (default key lifetime)
- When keys may have been compromised
- When changing key storage providers
- As part of security compliance requirements

### How to Rotate Keys

1. **Configure new key** in Data Protection (automatic with ASP.NET Core Data Protection)
2. **Re-encrypt connection strings** using the migration tool:

```bash
# The encryption service automatically uses the latest key
honua encrypt-connections --input metadata.json --in-place
```

3. **Verify** all connection strings work with new keys

The Data Protection API maintains old keys for a grace period, allowing gradual migration.

## Security Considerations

### File System Storage

- Keys stored in: `{KeyStorageDirectory}` or OS app data directory
- On Windows: Protected using DPAPI (machine-level)
- On Linux/Mac: File permissions set to 700 (owner only)
- **Recommended for**: Development, small deployments

### Azure Key Vault

- Keys encrypted at rest in Azure Key Vault HSM
- Authentication via Managed Identity or Service Principal
- Audit logging available
- **Recommended for**: Azure deployments, enterprise production

### AWS KMS

- Keys encrypted at rest in AWS KMS
- IAM-based access control
- CloudTrail audit logging
- **Recommended for**: AWS deployments, enterprise production

### GCP KMS

- Keys encrypted at rest in GCP Cloud KMS
- IAM-based access control
- Cloud Audit Logs available
- **Recommended for**: GCP deployments, enterprise production

### Best Practices

1. **Enable encryption by default** - Connection strings contain sensitive credentials
2. **Use cloud key management** for production (Azure Key Vault, AWS KMS, GCP KMS)
3. **Rotate keys regularly** - Default 90 days is recommended
4. **Backup encrypted metadata** - Keep encrypted versions in source control
5. **Secure key storage directory** - Use restrictive file permissions
6. **Monitor key access** - Enable audit logging in cloud key management services
7. **Test decryption** - Verify connection strings work after encryption

## Backward Compatibility

The encryption feature is backward compatible:

- **Unencrypted connection strings** continue to work
- **Mixed environments** supported (some encrypted, some not)
- **Gradual migration** possible - encrypt connection strings incrementally
- **Disable encryption** by setting `Enabled: false` (not recommended for production)

## Troubleshooting

### "Failed to decrypt connection string"

**Causes**:
- Encryption keys not available
- Keys rotated without re-encrypting connection strings
- Different key storage location

**Solutions**:
- Verify key storage configuration
- Check key file permissions
- Re-encrypt connection strings with current keys

### "Encrypted connection string detected but encryption is disabled"

**Cause**: Connection string is encrypted but `Enabled: false` in configuration

**Solution**: Set `Enabled: true` or decrypt connection strings manually

### Connection fails with encrypted connection string

**Causes**:
- Decryption failed silently
- Connection string corrupted

**Solutions**:
- Check application logs for decryption errors
- Verify connection string format (starts with `ENC:`)
- Re-encrypt the connection string

## Integration

### Service Registration

Register encryption service in `Startup.cs` or `Program.cs`:

```csharp
// Configure options from configuration
services.Configure<ConnectionStringEncryptionOptions>(
    configuration.GetSection(ConnectionStringEncryptionOptions.SectionName));

// Register encryption service
var encryptionOptions = configuration
    .GetSection(ConnectionStringEncryptionOptions.SectionName)
    .Get<ConnectionStringEncryptionOptions>() ?? new();
services.AddConnectionStringEncryption(encryptionOptions);

// Register data store providers with encryption
services.AddSingleton<IDataStoreProvider>(sp =>
{
    var encryption = sp.GetService<IConnectionStringEncryptionService>();
    return new PostgresDataStoreProvider(encryptionService: encryption);
});
```

### Data Store Provider Support

All built-in data store providers support encryption:

- `PostgresDataStoreProvider`
- `SqliteDataStoreProvider`
- `MySqlDataStoreProvider`
- `SqlServerDataStoreProvider`

Enterprise providers (if implemented):
- `MongoDbDataStoreProvider`
- `CosmosDbDataStoreProvider`
- `SnowflakeDataStoreProvider`
- `BigQueryDataStoreProvider`
- `RedshiftDataStoreProvider`
- `OracleDataStoreProvider`

## Architecture

### Components

1. **IConnectionStringEncryptionService**: Core encryption interface
2. **ConnectionStringEncryptionService**: Implementation using Data Protection API
3. **ConnectionStringEncryptionOptions**: Configuration options
4. **DataProtectionConfiguration**: Sets up Data Protection with various backends
5. **EncryptConnectionStringsCommand**: CLI migration tool

### Encryption Flow

```
Configuration File (metadata.json)
    ↓
DataSourceDefinition.ConnectionString (encrypted)
    ↓
DataStoreProvider.DecryptConnectionString()
    ↓
IConnectionStringEncryptionService.DecryptAsync()
    ↓
Plain Text Connection String
    ↓
Database Connection
```

### Key Management Flow

```
Application Startup
    ↓
Load ConnectionStringEncryptionOptions
    ↓
Configure Data Protection
    ↓
Set Key Storage Backend (FileSystem/KeyVault/KMS)
    ↓
Create IDataProtectionProvider
    ↓
Create IConnectionStringEncryptionService
```

## Implementation Status

### Completed ✅
- Core encryption service with Data Protection API
- File system key storage
- Azure Key Vault integration
- Backward compatibility with unencrypted strings
- All core data store providers updated
- CLI migration tool
- Comprehensive unit tests
- Configuration system

### Future Enhancements 🚧
- Full AWS KMS integration (custom IXmlEncryptor)
- Full GCP KMS integration (custom IXmlEncryptor)
- Enterprise data store provider updates
- Key rotation automation
- Health checks for key availability
- Metrics for encryption/decryption operations

## References

- [ASP.NET Core Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [Azure Key Vault Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers)
- [AWS KMS Documentation](https://docs.aws.amazon.com/kms/)
- [GCP Cloud KMS Documentation](https://cloud.google.com/kms/docs)
