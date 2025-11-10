# License Management System Architecture

## Component Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Client Applications                         │
│                    (API Requests with License Key)                   │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        LicenseValidator                              │
│  - Validates JWT license keys                                        │
│  - Checks expiration, revocation, tier access                        │
│  - Returns LicenseValidationResult                                   │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    ▼                         ▼
┌──────────────────────────────┐  ┌──────────────────────────────────┐
│      LicenseStore            │  │     LicenseManager               │
│  - Database operations       │  │  - GenerateLicenseAsync()        │
│  - GetByCustomerIdAsync()    │  │  - UpgradeLicenseAsync()         │
│  - GetExpiringLicensesAsync()│  │  - DowngradeLicenseAsync()       │
│  - GetExpiredLicensesAsync() │  │  - RevokeLicenseAsync()          │
│  - CreateAsync()             │  │  - RenewLicenseAsync()           │
│  - UpdateAsync()             │  │                                  │
└──────────────┬───────────────┘  └────────────┬─────────────────────┘
               │                               │
               │                               ▼
               │                  ┌──────────────────────────────────┐
               │                  │ CredentialRevocationService      │
               │                  │  - RevokeExpiredCredentialsAsync()│
               │                  │  - RevokeCustomerCredentialsAsync()│
               │                  │  - RevokeAwsCredentialsAsync()   │
               │                  │  - RevokeAzureCredentialsAsync() │
               │                  │  - RevokeGcpCredentialsAsync()   │
               │                  │  - RevokeGitHubCredentialsAsync()│
               │                  └────────────┬─────────────────────┘
               │                               │
               ▼                               ▼
┌──────────────────────────────┐  ┌──────────────────────────────────┐
│   PostgreSQL/MySQL/SQLite    │  │  CredentialRevocationStore       │
│                              │  │  - RecordRevocationAsync()       │
│  Tables:                     │  │  - GetRevocationsByCustomerIdAsync()│
│  - licenses                  │  │                                  │
│  - credential_revocations    │  │                                  │
└──────────────────────────────┘  └──────────────────────────────────┘
               ▲
               │
               │
┌──────────────┴───────────────────────────────────────────────────────┐
│           LicenseExpirationBackgroundService                         │
│  - Runs every hour (configurable)                                    │
│  - Checks for licenses expiring within 7 days (sends email warnings) │
│  - Checks for expired licenses (triggers credential revocation)      │
│  - Updates license status to Expired                                 │
└──────────────────────────────────────────────────────────────────────┘
```

## Data Flow

### 1. License Generation Flow

```
Admin Request
    │
    ▼
LicenseManager.GenerateLicenseAsync()
    │
    ├── Validate customer doesn't have existing license
    │
    ├── Generate JWT with claims:
    │   - customer_id
    │   - tier
    │   - email
    │   - features (JSON)
    │   - iat, exp
    │
    ├── Create LicenseInfo object
    │
    ▼
LicenseStore.CreateAsync()
    │
    ▼
Database INSERT
    │
    ▼
Return license with JWT key
```

### 2. License Validation Flow

```
Client API Request (with X-License-Key header)
    │
    ▼
LicenseValidator.ValidateAsync()
    │
    ├── Parse JWT token
    │
    ├── Validate signature with signing key
    │
    ├── Validate issuer and audience
    │
    ├── Check expiration (with clock skew tolerance)
    │
    ├── Extract customer_id from claims
    │
    ▼
LicenseStore.GetByCustomerIdAsync()
    │
    ├── Check license status (not Revoked/Suspended)
    │
    ├── Verify not expired
    │
    ├── Return LicenseValidationResult
    │
    ▼
API Endpoint Decision
    │
    ├── Valid → Process request
    │
    └── Invalid → Return 401 Unauthorized
```

### 3. License Expiration and Revocation Flow

```
Background Service Timer (every hour)
    │
    ▼
Check Expiring Licenses
    │
    ├── Query licenses expiring within 7 days
    │
    ├── For each license:
    │   │
    │   ├── Send warning email (SMTP)
    │   │
    │   └── Log warning
    │
    ▼
Check Expired Licenses
    │
    ├── Query licenses past expiration
    │
    ├── For each expired license:
    │   │
    │   ├── Update status to Expired
    │   │
    │   └── If enableAutomaticRevocation:
    │       │
    │       ▼
    │   CredentialRevocationService.RevokeCustomerCredentialsAsync()
    │       │
    │       ├── RevokeAwsCredentialsAsync()
    │       │   ├── Delete access keys
    │       │   ├── Detach policies
    │       │   ├── Remove from groups
    │       │   └── Delete IAM user
    │       │
    │       ├── RevokeAzureCredentialsAsync()
    │       │   └── Delete service principal
    │       │
    │       ├── RevokeGcpCredentialsAsync()
    │       │   └── Delete service account
    │       │
    │       └── RevokeGitHubCredentialsAsync()
    │           └── Revoke PAT
    │
    │   ▼
    │   Record Revocation
    │       │
    │       ▼
    │   CredentialRevocationStore.RecordRevocationAsync()
    │       │
    │       ▼
    │   Database INSERT (audit trail)
```

## Database Schema

### licenses Table

| Column       | Type         | Description                          |
|-------------|--------------|--------------------------------------|
| id          | UUID         | Primary key                          |
| customer_id | VARCHAR(100) | Unique customer identifier           |
| license_key | TEXT         | JWT license key                      |
| tier        | VARCHAR(20)  | Free/Professional/Enterprise         |
| status      | VARCHAR(20)  | Active/Expired/Suspended/Revoked     |
| issued_at   | TIMESTAMPTZ  | When license was issued              |
| expires_at  | TIMESTAMPTZ  | Expiration timestamp                 |
| features    | JSONB        | Feature flags and quotas             |
| revoked_at  | TIMESTAMPTZ  | When revoked (NULL if active)        |
| email       | VARCHAR(255) | Customer email for notifications     |
| metadata    | JSONB        | Additional metadata (optional)       |

**Indexes:**
- `idx_licenses_customer_id` on `customer_id`
- `idx_licenses_expires_at` on `expires_at`
- `idx_licenses_status` on `status`
- `idx_licenses_active_expiring` on `expires_at` WHERE status='Active' AND revoked_at IS NULL

### credential_revocations Table

| Column        | Type         | Description                        |
|--------------|--------------|------------------------------------|
| id           | SERIAL       | Primary key                        |
| customer_id  | VARCHAR(100) | Customer identifier                |
| registry_type| VARCHAR(20)  | AWS/Azure/GCP/GitHub               |
| revoked_at   | TIMESTAMPTZ  | Revocation timestamp               |
| reason       | TEXT         | Revocation reason                  |
| revoked_by   | VARCHAR(100) | Who initiated (user or System)     |

**Indexes:**
- `idx_credential_revocations_customer_id` on `customer_id`
- `idx_credential_revocations_revoked_at` on `revoked_at`

## Security Model

### JWT Claims

```json
{
  "customer_id": "cust_abc123",
  "tier": "Professional",
  "email": "customer@example.com",
  "features": {
    "maxUsers": 10,
    "maxCollections": 100,
    "advancedAnalytics": true,
    "cloudIntegrations": true,
    "maxApiRequestsPerDay": 100000
  },
  "iat": 1704067200,
  "exp": 1735689600,
  "iss": "https://license.honua.io",
  "aud": "honua-server"
}
```

### Validation Checks

1. **Signature Verification**: HMAC-SHA256 with 256-bit secret key
2. **Issuer Check**: Must match configured issuer
3. **Audience Check**: Must match configured audience
4. **Expiration Check**: Current time must be before exp claim
5. **Database Status Check**: License must be Active (not Revoked/Suspended)
6. **Clock Skew Tolerance**: 5 minutes (configurable)

### Credential Revocation Security

- **Resilience Pipeline**: 3 retry attempts with exponential backoff
- **Timeout Protection**: 5-minute timeout per revocation operation
- **Audit Trail**: All revocations logged with timestamp, reason, and initiator
- **Parallel Execution**: Credentials for all registries revoked in parallel
- **Graceful Degradation**: Failure to revoke one registry doesn't block others

## Configuration

### Required Settings

```json
{
  "honua:licensing:signingKey": "Base64-encoded 256-bit key (REQUIRED)",
  "honua:licensing:issuer": "https://license.honua.io",
  "honua:licensing:audience": "honua-server",
  "honua:licensing:connectionString": "Database connection string (REQUIRED)",
  "honua:licensing:provider": "postgres|mysql|sqlite"
}
```

### Optional Settings

```json
{
  "honua:licensing:expirationCheckInterval": "01:00:00",
  "honua:licensing:warningThresholdDays": 7,
  "honua:licensing:enableAutomaticRevocation": true,
  "honua:licensing:smtp": {
    "host": "smtp.gmail.com",
    "port": 587,
    "enableSsl": true,
    "username": "email@example.com",
    "password": "app-password",
    "fromEmail": "noreply@honua.io",
    "fromName": "Honua Licensing"
  }
}
```

## Performance Characteristics

### Database Queries

- **License Validation**: Single SELECT by customer_id (indexed) - ~1-5ms
- **Expiring Licenses**: Range query on expires_at (indexed) - ~5-20ms
- **Expired Licenses**: Range query on expires_at (indexed) - ~5-20ms

### Background Service

- **Default Interval**: 1 hour
- **Minimum Recommended**: 5 minutes
- **Impact**: Low (batch processing during off-peak)

### Credential Revocation

- **AWS IAM**: ~2-5 seconds per customer
- **Azure**: ~1-3 seconds per customer
- **GCP**: ~1-3 seconds per customer
- **GitHub**: ~1-2 seconds per customer

**Parallel Execution**: All registries revoked simultaneously per customer

## Error Handling

### Validation Errors

| Error Code          | HTTP Status | Description                     |
|--------------------|-------------|---------------------------------|
| InvalidFormat      | 400         | Malformed license key           |
| InvalidSignature   | 401         | Signature verification failed   |
| InvalidIssuer      | 401         | Issuer mismatch                 |
| InvalidAudience    | 401         | Audience mismatch               |
| Expired            | 401         | License past expiration         |
| Revoked            | 401         | License has been revoked        |
| Suspended          | 401         | License temporarily suspended   |
| NotFound           | 404         | License not in database         |

### Retry Strategies

- **Database Operations**: 3 retries, exponential backoff (100ms, 200ms, 400ms)
- **Credential Revocation**: 3 retries, exponential backoff (1s, 2s, 4s)
- **Email Sending**: No retries (logged and continue)

## Deployment Considerations

### High Availability

- **Database**: Use connection pooling, read replicas for queries
- **Background Service**: Run single instance with distributed lock (Redis)
- **Stateless Design**: All services are stateless, can scale horizontally

### Monitoring

- **Metrics**: License validation rate, expiration warnings sent, credentials revoked
- **Alerts**: Background service failures, credential revocation failures
- **Logging**: Structured logging with customer_id, tier, operation

### Disaster Recovery

- **Database Backups**: Daily backups of licenses table
- **Audit Trail**: credential_revocations table provides complete history
- **License Regeneration**: Can regenerate JWT keys from database records
