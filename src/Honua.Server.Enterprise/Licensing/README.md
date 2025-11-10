# Honua License Management System

A comprehensive JWT-based license management system with automatic credential revocation for Honua Server.

## Features

- **JWT-based License Keys**: Secure, cryptographically signed license keys with embedded claims
- **Tier Management**: Support for Free, Professional, and Enterprise tiers with customizable features
- **Automatic Expiration Tracking**: Background service monitors license expiration and sends warnings
- **Credential Revocation**: Automatically revokes cloud credentials (AWS, Azure, GCP, GitHub) when licenses expire
- **License Lifecycle Management**: Generate, upgrade, downgrade, renew, and revoke licenses
- **Database Storage**: Persistent license storage with PostgreSQL, MySQL, or SQLite
- **Email Notifications**: Automatic email warnings for licenses expiring soon
- **Audit Logging**: Complete audit trail of all credential revocations

## Architecture

### Components

1. **LicenseValidator** - Validates JWT license keys and checks permissions
2. **LicenseManager** - Manages license lifecycle (generate, upgrade, downgrade, revoke, renew)
3. **CredentialRevocationService** - Revokes cloud registry credentials
4. **LicenseExpirationBackgroundService** - Background job for monitoring expirations
5. **LicenseStore** - Database persistence layer using Dapper
6. **CredentialRevocationStore** - Audit log storage for revocations

### Data Models

- **LicenseInfo** - Complete license information
- **LicenseFeatures** - Feature flags and quotas per tier
- **LicenseTier** - Free, Professional, Enterprise
- **LicenseStatus** - Active, Expired, Suspended, Revoked, Pending
- **CredentialRevocation** - Revocation audit record

## Installation

### 1. Database Setup

Run the appropriate migration script for your database:

#### PostgreSQL
```bash
psql -U postgres -d honua_licenses -f src/Honua.Server.Core/Licensing/Storage/migrations-postgres.sql
```

#### MySQL
```bash
mysql -u root -p honua_licenses < src/Honua.Server.Core/Licensing/Storage/migrations-mysql.sql
```

#### SQLite
```bash
sqlite3 honua_licenses.db < src/Honua.Server.Core/Licensing/Storage/migrations-sqlite.sql
```

### 2. Configuration

Add licensing configuration to your `appsettings.json`:

```json
{
  "honua": {
    "licensing": {
      "signingKey": "your-base64-encoded-256-bit-key-here",
      "issuer": "https://license.honua.io",
      "audience": "honua-server",
      "connectionString": "Host=localhost;Database=honua_licenses;Username=postgres;Password=yourpassword",
      "provider": "postgres",
      "expirationCheckInterval": "01:00:00",
      "warningThresholdDays": 7,
      "enableAutomaticRevocation": true,
      "smtp": {
        "host": "smtp.gmail.com",
        "port": 587,
        "enableSsl": true,
        "username": "your-email@gmail.com",
        "password": "your-app-password",
        "fromEmail": "noreply@honua.io",
        "fromName": "Honua Licensing"
      }
    }
  }
}
```

**Generate a signing key:**
```bash
openssl rand -base64 32
```

### 3. Service Registration

Add licensing services to your DI container:

```csharp
using Honua.Server.Core.Licensing;
using Honua.Server.Core.Licensing.Storage;

services.AddLicensing(configuration);
```

Or manually:

```csharp
// Configuration
services.Configure<LicenseOptions>(configuration.GetSection(LicenseOptions.SectionName));

// Stores
services.AddSingleton<ILicenseStore, LicenseStore>();
services.AddSingleton<ICredentialRevocationStore, CredentialRevocationStore>();

// Services
services.AddSingleton<ILicenseValidator, LicenseValidator>();
services.AddSingleton<ILicenseManager, LicenseManager>();
services.AddSingleton<ICredentialRevocationService, CredentialRevocationService>();

// Background service
services.AddHostedService<LicenseExpirationBackgroundService>();
```

## Usage

### Generate a License

```csharp
public class LicenseController : ControllerBase
{
    private readonly ILicenseManager _licenseManager;

    public LicenseController(ILicenseManager licenseManager)
    {
        _licenseManager = licenseManager;
    }

    [HttpPost("licenses")]
    public async Task<IActionResult> CreateLicense([FromBody] LicenseGenerationRequest request)
    {
        var license = await _licenseManager.GenerateLicenseAsync(request);
        return Ok(new { licenseKey = license.LicenseKey });
    }
}
```

### Validate a License

```csharp
public class ApiController : ControllerBase
{
    private readonly ILicenseValidator _licenseValidator;

    [HttpGet("data")]
    public async Task<IActionResult> GetData([FromHeader(Name = "X-License-Key")] string licenseKey)
    {
        var result = await _licenseValidator.ValidateAsync(licenseKey);

        if (!result.IsValid)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        // Check tier access
        if (result.License.Tier < LicenseTier.Professional)
        {
            return Forbidden(new { error = "This feature requires Professional tier or higher" });
        }

        return Ok(/* your data */);
    }
}
```

### Upgrade a License

```csharp
[HttpPost("licenses/{customerId}/upgrade")]
public async Task<IActionResult> UpgradeLicense(string customerId, [FromBody] UpgradeRequest request)
{
    var upgradedLicense = await _licenseManager.UpgradeLicenseAsync(
        customerId,
        request.NewTier);

    return Ok(new { licenseKey = upgradedLicense.LicenseKey });
}
```

### Revoke a License

```csharp
[HttpPost("licenses/{customerId}/revoke")]
public async Task<IActionResult> RevokeLicense(string customerId, [FromBody] RevokeRequest request)
{
    await _licenseManager.RevokeLicenseAsync(
        customerId,
        request.Reason,
        User.Identity.Name ?? "Admin");

    return Ok(new { message = "License revoked successfully" });
}
```

### Renew a License

```csharp
[HttpPost("licenses/{customerId}/renew")]
public async Task<IActionResult> RenewLicense(string customerId, [FromBody] RenewRequest request)
{
    var renewedLicense = await _licenseManager.RenewLicenseAsync(
        customerId,
        request.ExtensionDays);

    return Ok(new {
        licenseKey = renewedLicense.LicenseKey,
        expiresAt = renewedLicense.ExpiresAt
    });
}
```

## JWT License Structure

License keys are JWT tokens with the following claims:

```json
{
  "customer_id": "cust_123456",
  "tier": "Professional",
  "email": "customer@example.com",
  "features": {
    "maxUsers": 10,
    "maxCollections": 100,
    "advancedAnalytics": true,
    "cloudIntegrations": true,
    "stacCatalog": true,
    "rasterProcessing": true,
    "vectorTiles": true,
    "prioritySupport": false,
    "maxApiRequestsPerDay": 100000,
    "maxStorageGb": 100
  },
  "iat": 1704067200,
  "exp": 1735689600,
  "iss": "https://license.honua.io",
  "aud": "honua-server"
}
```

## Credential Revocation

When a license expires or is revoked, the system automatically revokes associated credentials:

### AWS IAM User Deletion
- Deletes all access keys
- Detaches all managed policies
- Deletes all inline policies
- Removes user from all groups
- Deletes the IAM user

### Azure Service Principal
- Deletes the service principal
- Revokes all associated credentials

### GCP Service Account
- Deletes the service account
- Revokes all keys

### GitHub PAT
- Revokes personal access tokens (requires GitHub org API access)

## Background Service

The `LicenseExpirationBackgroundService` runs every hour (configurable) and:

1. **Checks for licenses expiring within 7 days** (configurable)
   - Sends warning emails to customers
   - Includes renewal instructions

2. **Checks for expired licenses**
   - Automatically revokes credentials if `enableAutomaticRevocation` is true
   - Updates license status to `Expired`
   - Logs all actions

## Database Schema

### licenses table
- `id` - UUID primary key
- `customer_id` - Unique customer identifier
- `license_key` - JWT license key
- `tier` - License tier (Free, Professional, Enterprise)
- `status` - Current status (Active, Expired, Suspended, Revoked, Pending)
- `issued_at` - When license was issued
- `expires_at` - Expiration timestamp
- `features` - JSONB feature configuration
- `revoked_at` - Revocation timestamp (NULL if not revoked)
- `email` - Customer email for notifications
- `metadata` - Additional metadata (JSONB)

### credential_revocations table
- `id` - Auto-increment primary key
- `customer_id` - Customer identifier
- `registry_type` - Registry type (AWS, Azure, GCP, GitHub)
- `revoked_at` - Revocation timestamp
- `reason` - Revocation reason
- `revoked_by` - Who initiated revocation

## Security Considerations

1. **Signing Key**: Use a strong 256-bit key, store securely (Azure Key Vault, AWS KMS, etc.)
2. **Connection Strings**: Never commit connection strings to source control
3. **SMTP Credentials**: Use app-specific passwords, not main account passwords
4. **Clock Skew**: Default 5-minute tolerance for JWT validation
5. **Audit Trail**: All revocations are logged to `credential_revocations` table
6. **Production**: Never set admin credentials in production configuration

## Monitoring

Key metrics to monitor:

- Licenses expiring in next 7 days
- Failed credential revocations
- Email delivery failures
- License validation errors
- Background service health

## Testing

```bash
# Run unit tests
dotnet test tests/Honua.Server.Core.Tests/Licensing/

# Test license generation
curl -X POST https://your-server/api/licenses \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "test-customer",
    "email": "test@example.com",
    "tier": "Professional",
    "durationDays": 365
  }'

# Test license validation
curl https://your-server/api/data \
  -H "X-License-Key: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

## Troubleshooting

### License validation fails
- Check signing key matches between generation and validation
- Verify JWT issuer and audience match configuration
- Check system clock synchronization (clock skew)

### Emails not sending
- Verify SMTP credentials
- Check firewall allows outbound port 587/465
- Enable less secure apps if using Gmail (or use app password)

### Credential revocation fails
- Verify AWS/Azure/GCP credentials have sufficient permissions
- Check network connectivity to cloud providers
- Review logs for specific error messages

## License

Copyright (c) 2025 Honua. All rights reserved.
