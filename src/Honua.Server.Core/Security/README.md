# Security Module

## Overview

The Security module provides comprehensive security infrastructure for Honua.Server, including connection string encryption, input validation, and sanitization services. It implements defense-in-depth security measures to protect against SQL injection, path traversal, and other common attack vectors.

## Purpose

This module ensures the confidentiality, integrity, and availability of sensitive data and system resources by:

- Encrypting sensitive connection strings at rest using ASP.NET Core Data Protection
- Validating and sanitizing SQL identifiers, connection strings, and file paths
- Preventing SQL injection, path traversal, and other injection attacks
- Providing secure input validation for URLs, proxy configurations, and ZIP archives

## Architecture

### Core Components

#### 1. Connection String Encryption

**Classes**: `ConnectionStringEncryptionService`, `IConnectionStringEncryptionService`

Encrypts database connection strings using ASP.NET Core Data Protection API, supporting multiple key storage providers.

**Key Features**:
- Automatic encryption/decryption with backward compatibility for unencrypted strings
- Multiple key storage backends (FileSystem, Azure Key Vault, AWS KMS, GCP KMS)
- Key rotation support
- Purpose-specific encryption isolation

**Configuration**: `ConnectionStringEncryptionOptions`, `DataProtectionConfiguration`

#### 2. Input Validators

**SQL Identifier Validator** (`SqlIdentifierValidator`)
- Validates table names, column names, and schema names
- Prevents SQL injection in dynamic query construction
- Database-specific quoting (PostgreSQL, MySQL, SQL Server, SQLite)
- Reserved keyword detection

**Connection String Validator** (`ConnectionStringValidator`)
- Validates connection string format and content
- Blocks SQL injection attempts via connection strings
- Provider-specific validation (PostgreSQL, MySQL, SQL Server, SQLite)
- Length and character constraints

**URL Validator** (`UrlValidator`)
- Validates HTTP/HTTPS URLs
- Prevents SSRF (Server-Side Request Forgery) attacks
- Blocks private IP ranges and internal addresses

**Secure Path Validator** (`SecurePathValidator`)
- Prevents path traversal attacks
- Validates file paths against allowed directories
- Blocks access to sensitive system directories

**Trusted Proxy Validator** (`TrustedProxyValidator`)
- Validates proxy IP addresses and CIDR ranges
- Prevents proxy spoofing attacks

**ZIP Archive Validator** (`ZipArchiveValidator`)
- Validates ZIP archive integrity
- Prevents ZIP bomb attacks
- File size and compression ratio validation

## Usage Examples

### Connection String Encryption

```csharp
// Configure in Startup.cs
services.Configure<ConnectionStringEncryptionOptions>(options =>
{
    options.Enabled = true;
    options.KeyStorageProvider = "filesystem";
    options.ApplicationName = "HonuaServer";
    options.KeyLifetimeDays = 90;
});

services.AddConnectionStringEncryption(
    Configuration.GetSection("ConnectionStringEncryption").Get<ConnectionStringEncryptionOptions>()
);

// Use the service
public class DataSourceManager
{
    private readonly IConnectionStringEncryptionService _encryptionService;

    public DataSourceManager(IConnectionStringEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task<string> StoreConnectionStringAsync(string plainText)
    {
        // Encrypt before storing
        var encrypted = await _encryptionService.EncryptAsync(plainText);
        // Store encrypted value in database
        return encrypted;
    }

    public async Task<string> GetConnectionStringAsync(string encryptedValue)
    {
        // Decrypt when retrieving
        return await _encryptionService.DecryptAsync(encryptedValue);
    }
}
```

### SQL Identifier Validation

```csharp
using Honua.Server.Core.Security;

// Validate table name
string tableName = userInput;
SqlIdentifierValidator.ValidateIdentifier(tableName, nameof(tableName));

// Validate and quote for PostgreSQL
string quotedTable = SqlIdentifierValidator.ValidateAndQuotePostgres("my_table");
// Result: "my_table"

// Validate and quote for MySQL
string quotedTable = SqlIdentifierValidator.ValidateAndQuoteMySql("my-table");
// Result: `my-table`

// Validate qualified names
string qualifiedName = SqlIdentifierValidator.ValidateAndQuotePostgres("schema.table");
// Result: "schema"."table"
```

### Connection String Validation

```csharp
using Honua.Server.Core.Security;

// Validate connection string
string connectionString = userInput;
ConnectionStringValidator.Validate(connectionString, "postgis");

// Provider-specific validation
ConnectionStringValidator.Validate(
    "Host=localhost;Database=geo;Username=user;Password=pass",
    "postgis"
);
```

### Path Validation

```csharp
using Honua.Server.Core.Security;

// Validate file path
string uploadPath = "/var/uploads/data";
string userFilePath = userInput;

SecurePathValidator.ValidatePath(userFilePath, uploadPath);

// Prevent path traversal
try
{
    SecurePathValidator.ValidatePath("../../../etc/passwd", uploadPath);
}
catch (SecurityException ex)
{
    // Path traversal attempt blocked
}
```

### URL Validation

```csharp
using Honua.Server.Core.Security;

// Validate URL
string userUrl = userInput;

if (UrlValidator.IsValidUrl(userUrl))
{
    // Safe to use
    var response = await httpClient.GetAsync(userUrl);
}

// Block SSRF attempts
bool isValid = UrlValidator.IsValidUrl("http://localhost:8080/admin");
// Returns false - internal address blocked
```

## Configuration Options

### ConnectionStringEncryptionOptions

```json
{
  "ConnectionStringEncryption": {
    "Enabled": true,
    "KeyStorageProvider": "filesystem",
    "KeyStorageDirectory": "/var/honua/keys",
    "ApplicationName": "HonuaServer",
    "KeyLifetimeDays": 90
  }
}
```

**Properties**:
- `Enabled` (bool): Enable/disable encryption (default: true)
- `KeyStorageProvider` (string): Storage backend - "filesystem", "azurekeyvault", "awskms", "gcpkms"
- `KeyStorageDirectory` (string): Path for filesystem key storage
- `ApplicationName` (string): Application name for key isolation
- `KeyLifetimeDays` (int): Key rotation interval (default: 90 days)

### Key Storage Providers

#### FileSystem (Default)
- Keys stored in local file system
- Windows: Protected with DPAPI
- Linux/macOS: File permissions (chmod 700)

#### Azure Key Vault (requires Core.Cloud)
```json
{
  "KeyStorageProvider": "azurekeyvault",
  "AzureKeyVault": {
    "KeyVaultUri": "https://myvault.vault.azure.net/",
    "KeyIdentifier": "https://myvault.vault.azure.net/keys/honua-dataprotection"
  }
}
```

#### AWS KMS (requires Core.Cloud)
```json
{
  "KeyStorageProvider": "awskms",
  "AwsKms": {
    "KeyId": "arn:aws:kms:us-east-1:123456789:key/abc-123",
    "Region": "us-east-1"
  }
}
```

#### GCP KMS (requires Core.Cloud)
```json
{
  "KeyStorageProvider": "gcpkms",
  "GcpKms": {
    "ProjectId": "my-project",
    "LocationId": "global",
    "KeyRingId": "honua-keyring",
    "KeyId": "dataprotection-key"
  }
}
```

## Best Practices

### Connection String Encryption

1. **Always Enable in Production**: Set `Enabled = true` in production environments
2. **Use Cloud KMS for Production**: Prefer Azure Key Vault, AWS KMS, or GCP KMS over filesystem storage
3. **Regular Key Rotation**: Configure appropriate `KeyLifetimeDays` (90 days recommended)
4. **Backup Keys**: Ensure encryption keys are backed up and recoverable
5. **Test Key Rotation**: Regularly test key rotation procedures

### Input Validation

1. **Validate Early**: Validate all user input at the entry point
2. **Use Specific Validators**: Choose the appropriate validator for each input type
3. **Whitelist Over Blacklist**: Validators use whitelist approaches (allowed characters/patterns)
4. **Sanitize Before Use**: Always sanitize input before using in SQL, file paths, or URLs
5. **Fail Securely**: Reject invalid input with clear error messages (don't expose system details)

### SQL Injection Prevention

1. **Always Use Parameterized Queries**: Prefer parameterized queries over dynamic SQL
2. **Validate Identifiers**: Use `SqlIdentifierValidator` for table/column names in dynamic SQL
3. **Quote Identifiers**: Use database-specific quoting methods (`ValidateAndQuote*` methods)
4. **Avoid String Concatenation**: Never concatenate user input into SQL strings
5. **Use ORM Features**: Leverage Entity Framework or Dapper parameter binding

### Path Traversal Prevention

1. **Validate All Paths**: Use `SecurePathValidator` for all file operations involving user input
2. **Define Allowed Directories**: Explicitly specify allowed base directories
3. **Canonicalize Paths**: Resolve paths to absolute forms before validation
4. **Block Relative Paths**: Reject paths containing `..` or other traversal sequences
5. **Least Privilege**: Run file operations with minimal required permissions

### SSRF Prevention

1. **Validate URLs**: Use `UrlValidator` for all external URL requests
2. **Block Private IPs**: Configure validators to reject private/internal IP ranges
3. **Use Allowlists**: Maintain allowlists of permitted external domains
4. **Timeout Requests**: Set appropriate timeouts for external HTTP requests
5. **Limit Redirects**: Configure maximum redirect counts and validate redirect targets

## Security Considerations

### Encryption Key Management

- **Key Compromise**: If encryption keys are compromised, all encrypted connection strings must be re-encrypted with new keys
- **Key Loss**: Loss of encryption keys makes encrypted connection strings unrecoverable
- **Key Rotation**: Regular key rotation limits the impact of potential key compromise
- **Key Backup**: Maintain secure backups of encryption keys in separate locations

### Input Validation Limitations

- **Defense in Depth**: Input validation is one layer; also use parameterized queries, least privilege, etc.
- **Evolution of Attacks**: Regularly update validators to address new attack patterns
- **Context-Specific**: Choose validators appropriate for the context (SQL, file path, URL, etc.)
- **User Experience**: Balance security with usability; provide clear error messages

### Performance Considerations

- **Encryption Overhead**: Connection string encryption adds minimal overhead (< 1ms per operation)
- **Validation Overhead**: Validators use compiled regex and efficient algorithms
- **Caching**: Consider caching validated/quoted identifiers for frequently-used values
- **Async Operations**: Use async methods for I/O-bound operations (encryption, key retrieval)

## Related Modules

- **Core.Cloud**: Provides Azure Key Vault, AWS KMS, and GCP KMS integration
- **Data**: Uses connection string encryption for data source management
- **Import**: Uses validators for file path and schema validation
- **Export**: Uses validators for file path sanitization

## Testing

```csharp
// Unit test example
[Fact]
public void ValidateIdentifier_RejectsInvalidCharacters()
{
    // Arrange
    string invalidIdentifier = "table; DROP TABLE users--";

    // Act & Assert
    Assert.Throws<ArgumentException>(() =>
        SqlIdentifierValidator.ValidateIdentifier(invalidIdentifier)
    );
}

[Fact]
public async Task EncryptAsync_ProducesEncryptedString()
{
    // Arrange
    var service = CreateEncryptionService();
    string plainText = "Host=localhost;Database=test";

    // Act
    string encrypted = await service.EncryptAsync(plainText);

    // Assert
    Assert.True(service.IsEncrypted(encrypted));
    Assert.StartsWith("ENC:", encrypted);
}
```

## Performance Characteristics

| Operation | Average Time | Notes |
|-----------|-------------|-------|
| Connection String Encryption | < 1ms | In-memory encryption using Data Protection API |
| Connection String Decryption | < 1ms | In-memory decryption |
| SQL Identifier Validation | < 0.1ms | Compiled regex pattern matching |
| Connection String Validation | < 0.5ms | String parsing and pattern matching |
| Path Validation | < 0.2ms | Path canonicalization and prefix checking |
| URL Validation | < 0.3ms | URI parsing and IP address validation |

## Common Issues and Solutions

### Issue: "Encrypted connection string detected but encryption is disabled"

**Solution**: Enable encryption in configuration:
```json
{
  "ConnectionStringEncryption": {
    "Enabled": true
  }
}
```

### Issue: "Failed to decrypt connection string"

**Causes**:
- Encryption keys not available
- Keys changed/rotated without re-encrypting connection strings
- Different application name or purpose string

**Solution**: Ensure keys are available and consistent across deployments

### Issue: "SQL identifier contains invalid characters"

**Solution**: Use only alphanumeric characters and underscores, or use quoted identifiers:
```csharp
// Use quoting method for special characters
string quoted = SqlIdentifierValidator.ValidateAndQuotePostgres("my-table-name");
```

### Issue: Path validation fails for valid paths

**Solution**: Ensure the base directory is correctly specified and paths are absolute:
```csharp
string basePath = Path.GetFullPath("/var/uploads");
string filePath = Path.GetFullPath(userInput);
SecurePathValidator.ValidatePath(filePath, basePath);
```

## Version History

- **v1.0**: Initial release with connection string encryption and basic validators
- **v1.1**: Added cloud KMS support (Azure Key Vault, AWS KMS, GCP KMS)
- **v1.2**: Enhanced SQL identifier validation with database-specific quoting
- **v1.3**: Added URL validator and SSRF protection
- **v1.4**: Added ZIP archive validator and improved path validation
