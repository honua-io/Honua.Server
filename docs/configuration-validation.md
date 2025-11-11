# Configuration Validation

This document describes all configuration validation rules implemented using the `IValidateOptions<T>` pattern in Honua Server.

## Overview

Configuration validation ensures that all configuration settings are valid before the application starts. This provides fast failure and clear error messages when configuration is incorrect.

All validators are automatically registered and executed at startup via the `ConfigurationValidationHostedService`.

## Validators

### 1. HonuaAuthenticationOptions

**Location:** `src/Honua.Server.Core/Configuration/HonuaAuthenticationOptions.cs`

#### Validation Rules

**Mode: Oidc**
- `Jwt.Authority` - Required, must not be null or whitespace
- `Jwt.Audience` - Required, must not be null or whitespace

**Mode: Local**
- `Local.Provider` - Must be one of: `sqlite`, `postgres`, `postgresql`, `mysql`, `sqlserver`
- `Local.SessionLifetime` - Must be greater than zero
- `Local.MaxFailedAttempts` - Must be greater than zero
- `Local.LockoutDuration` - Must be greater than zero
- `Local.StorePath` - Required when provider is `sqlite`
- Connection string - Required for non-SQLite providers (via `ConnectionString`, `ConnectionStringName`, or ConnectionStrings configuration)
- `Bootstrap.AdminPassword` - **SECURITY**: Must NOT be set in production environments

**Mode: QuickStart**
- `Enforce` - Cannot be true when using QuickStart mode
- `QuickStart.Enabled` - Must be false when Enforce is true for Oidc or Local mode

### 2. ConnectionStringOptions

**Location:** `src/Honua.Server.Core/Configuration/ConnectionStringOptions.cs`

#### Validation Rules

**Redis**
- Format: `host:port[,password=xxx][,ssl=true]`
- Example: `localhost:6379` or `redis.example.com:6380,password=secret,ssl=true`

**PostgreSQL**
- Must contain: `Host=` and `Database=`
- Example: `Host=localhost;Database=honua;Username=postgres;Password=secret`

**SQL Server**
- Must contain: `Server=` and `Database=`
- Example: `Server=localhost;Database=honua;User Id=sa;Password=secret`

**MySQL**
- Must contain: `Server=` and `Database=`
- Example: `Server=localhost;Database=honua;User=root;Password=secret`

### 3. OpenRosaOptions

**Location:** `src/Honua.Server.Core/Configuration/OpenRosaOptions.cs`

#### Validation Rules

When `Enabled = true`:
- `BaseUrl` - Required, must start with `/` or be an absolute URL
  - Example: `/openrosa` or `https://example.com/openrosa`
- `MaxSubmissionSizeMB` - Must be > 0, recommended maximum: 200 MB
- `AllowedMediaTypes` - Must contain at least one valid media type in format `type/subtype`
  - Example: `image/jpeg`, `image/png`
- `StagingRetentionDays` - Cannot be negative, recommended maximum: 365 days

**DigestAuth** (when enabled):
- `Realm` - Required, 1-100 characters
- `NonceLifetimeMinutes` - Must be > 0, recommended maximum: 60 minutes

### 4. GraphDatabaseOptions

**Location:** `src/Honua.Server.Core/Configuration/GraphDatabaseOptions.cs`

#### Validation Rules

When `Enabled = true`:
- `DefaultGraphName` - Required, max 63 characters, must match pattern `^[a-z][a-z0-9_]*$`
  - Must start with lowercase letter, contain only lowercase letters, digits, and underscores
  - Example: `honua_graph`
- `CommandTimeoutSeconds` - Must be positive, maximum: 3600 seconds (1 hour)
- `MaxRetryAttempts` - Cannot be negative, maximum: 10
- `QueryCacheExpirationMinutes` - Must be positive when cache is enabled, maximum: 1440 minutes (24 hours)
- `MaxTraversalDepth` - Must be positive, maximum: 100

### 5. CacheInvalidationOptions

**Location:** `src/Honua.Server.Core/Configuration/CacheInvalidationOptions.cs`

#### Validation Rules

- `RetryCount` - Cannot be negative, recommended maximum: 10
- `RetryDelayMs` - Must be positive, recommended maximum: 10000 ms (10 seconds)
- `MaxRetryDelayMs` - Must be positive and >= `RetryDelayMs`, recommended maximum: 60000 ms (60 seconds)
- `HealthCheckSampleSize` - Must be positive, recommended maximum: 10000
- `MaxDriftPercentage` - Cannot be negative, must be <= 100
- `ShortTtl` - Must be positive, recommended maximum: 1 hour
- `OperationTimeout` - Must be positive, recommended maximum: 5 minutes
- `Strategy` - Must be one of: `Strict`, `Eventual`, `ShortTTL`

### 6. CacheSizeLimitOptions

**Location:** `src/Honua.Server.Core/Caching/CacheSizeLimitOptions.cs`

#### Validation Rules

- `MaxTotalSizeMB` - Cannot be negative, maximum: 10000 MB (10 GB)
  - 0 means unlimited (not recommended in production)
- `MaxTotalEntries` - Cannot be negative, maximum: 1,000,000
  - 0 means unlimited (not recommended in production)
- `ExpirationScanFrequencyMinutes` - Must be positive, range: 0.5 to 60 minutes
- `CompactionPercentage` - Must be between 0.1 (10%) and 0.5 (50%)

### 7. DataIngestionOptions

**Location:** `src/Honua.Server.Core/Configuration/DataIngestionOptions.cs`

#### Validation Rules

- `BatchSize` - Must be positive, recommended maximum: 100,000
- `ProgressReportInterval` - Must be positive, should not exceed `BatchSize`
- `MaxRetries` - Cannot be negative, recommended maximum: 10
- `BatchTimeout` - Must be positive, recommended maximum: 1 hour
- `TransactionTimeout` - Must be positive, recommended maximum: 4 hours
  - Should be greater than `BatchTimeout` when `UseTransactionalIngestion = true`
- `TransactionIsolationLevel` - Must be valid: `ReadCommitted`, `RepeatableRead`, `Serializable`
- `MaxGeometryCoordinates` - Must be positive, recommended maximum: 10,000,000
- `MaxValidationErrors` - Must be positive, recommended maximum: 10,000

**Logical Constraints:**
- `RejectInvalidGeometries` requires `ValidateGeometry = true`
- `AutoRepairGeometries` requires `ValidateGeometry = true`

### 8. DataAccessOptions

**Location:** `src/Honua.Server.Core/Data/DataAccessOptions.cs`

#### Validation Rules

**Timeout Settings:**
- `DefaultCommandTimeoutSeconds` - Must be positive, recommended maximum: 300 seconds (5 minutes)
- `LongRunningQueryTimeoutSeconds` - Must be positive and > `DefaultCommandTimeoutSeconds`, maximum: 3600 seconds
- `BulkOperationTimeoutSeconds` - Must be positive and > `LongRunningQueryTimeoutSeconds`
- `TransactionTimeoutSeconds` - Must be positive
- `HealthCheckTimeoutSeconds` - Must be positive, recommended maximum: 30 seconds

**SQL Server Pool Options:**
- `MinPoolSize` - Cannot be negative
- `MaxPoolSize` - Must be positive and >= `MinPoolSize`
- `ConnectionLifetime` - Must be positive
- `ConnectTimeout` - Must be positive

**PostgreSQL Pool Options:**
- `MinPoolSize` - Cannot be negative
- `MaxPoolSize` - Must be positive and >= `MinPoolSize`
- `ConnectionLifetime` - Must be positive
- `Timeout` - Must be positive

**MySQL Pool Options:**
- `MinimumPoolSize` - Cannot be negative
- `MaximumPoolSize` - Must be positive and >= `MinimumPoolSize`
- `ConnectionLifeTime` - Must be positive
- `ConnectionTimeout` - Must be positive

**SQLite Options:**
- `DefaultTimeout` - Must be positive
- `CacheMode` - Must be one of: `Default`, `Private`, `Shared`

**Optimistic Locking Options:**
- `MaxRetryAttempts` - Cannot be negative
- `RetryDelayMilliseconds` - Must be positive
- `VersionColumnName` - Required, cannot be empty

### 9. ObservabilityOptions

**Location:** `src/Honua.Server.Host/Observability/ObservabilityOptions.cs`

#### Validation Rules

**Metrics** (when enabled):
- `Endpoint` - Required, must start with `/`
  - Example: `/metrics`

**Tracing:**
- `Exporter` - Must be one of: `none`, `console`, `otlp`
- `OtlpEndpoint` - Required when exporter is `otlp`, must be valid HTTP/HTTPS URL
  - Example: `http://localhost:4317`

## Configuration Examples

### Minimal Valid Configuration

```yaml
honua:
  metadata:
    provider: json
    path: ./metadata.json

  authentication:
    mode: Local
    local:
      provider: sqlite
      storePath: data/auth/auth.db

ConnectionStrings:
  Redis: localhost:6379

GraphDatabase:
  enabled: true
  defaultGraphName: honua_graph

honua:
  caching:
    maxTotalSizeMB: 100
    maxTotalEntries: 10000

CacheInvalidation:
  retryCount: 3
  retryDelayMs: 100
  strategy: Strict

Honua:
  DataIngestion:
    batchSize: 1000
    useTransactionalIngestion: true
    validateGeometry: true

DataAccess:
  defaultCommandTimeoutSeconds: 30
  longRunningQueryTimeoutSeconds: 300
  bulkOperationTimeoutSeconds: 600
```

### Production Configuration Best Practices

```yaml
honua:
  authentication:
    mode: Oidc  # Use OIDC in production, not Local
    enforce: true
    jwt:
      authority: https://auth.example.com
      audience: https://api.example.com
      requireHttpsMetadata: true
    bootstrap:
      # NEVER set AdminPassword in production!
      # Use OIDC or create users through admin interface

ConnectionStrings:
  Redis: redis.production.example.com:6380,password=<secret>,ssl=true
  Postgres: Host=db.example.com;Database=honua_prod;Username=honua;Password=<secret>;SSL Mode=Require

GraphDatabase:
  enabled: true
  defaultGraphName: honua_graph
  commandTimeoutSeconds: 30
  maxRetryAttempts: 3
  maxTraversalDepth: 10

CacheInvalidation:
  strategy: Strict  # Strict consistency for production
  retryCount: 3
  operationTimeout: 00:00:10

honua:
  caching:
    maxTotalSizeMB: 500  # Adjust based on available memory
    enableAutoCompaction: true
    enableMetrics: true

Honua:
  DataIngestion:
    batchSize: 1000
    useTransactionalIngestion: true  # Critical for data integrity
    validateGeometry: true
    rejectInvalidGeometries: true
    validateSchema: true
    schemaValidationMode: Strict

DataAccess:
  defaultCommandTimeoutSeconds: 30
  longRunningQueryTimeoutSeconds: 300
  healthCheckTimeoutSeconds: 5
  sqlServer:
    maxPoolSize: 100
    minPoolSize: 5
  postgres:
    maxPoolSize: 100
    minPoolSize: 5
  optimisticLocking:
    enabled: true
    versionRequirement: Strict

observability:
  metrics:
    enabled: true
    endpoint: /metrics
    usePrometheus: true
  tracing:
    exporter: otlp
    otlpEndpoint: http://jaeger:4317
```

## Troubleshooting

### Common Validation Errors

**Error:** `"AdminPassword must NOT be set in production configuration"`
- **Fix:** Remove `HonuaAuthentication:Bootstrap:AdminPassword` from production config
- **Reason:** Hardcoded passwords in configuration are a security risk

**Error:** `"Jwt Authority must be provided when authentication mode is Oidc"`
- **Fix:** Set `honua:authentication:jwt:authority` to your OIDC provider URL
- **Example:** `https://auth.example.com`

**Error:** `"Connection string is required for local authentication provider"`
- **Fix:** Set connection string via `ConnectionString`, `ConnectionStringName`, or `ConnectionStrings__<provider>`
- **Example:** `ConnectionStrings:Postgres: Host=localhost;Database=honua`

**Error:** `"TransactionTimeout should be greater than BatchTimeout"`
- **Fix:** Ensure `TransactionTimeout` > `BatchTimeout` in DataIngestion configuration
- **Reason:** Transaction must allow enough time for all batches to complete

**Error:** `"MaxPoolSize must be greater than or equal to MinPoolSize"`
- **Fix:** Adjust pool size settings so MaxPoolSize >= MinPoolSize
- **Example:** Set `minPoolSize: 2` and `maxPoolSize: 50`

## Validator Registration

All validators are registered in `ConfigurationValidationExtensions.cs`:

```csharp
services.AddSingleton<IValidateOptions<HonuaConfiguration>, HonuaConfigurationValidator>();
services.AddSingleton<IValidateOptions<HonuaAuthenticationOptions>, HonuaAuthenticationOptionsValidator>();
services.AddSingleton<IValidateOptions<OpenRosaOptions>, OpenRosaOptionsValidator>();
services.AddSingleton<IValidateOptions<ConnectionStringOptions>, ConnectionStringOptionsValidator>();
services.AddSingleton<IValidateOptions<GraphDatabaseOptions>, GraphDatabaseOptionsValidator>();
services.AddSingleton<IValidateOptions<CacheInvalidationOptions>, CacheInvalidationOptionsValidator>();
services.AddSingleton<IValidateOptions<CacheSizeLimitOptions>, CacheSizeLimitOptionsValidator>();
services.AddSingleton<IValidateOptions<DataIngestionOptions>, DataIngestionOptionsValidator>();
services.AddSingleton<IValidateOptions<DataAccessOptions>, DataAccessOptionsValidator>();
```

Validators are automatically invoked at startup by `ConfigurationValidationHostedService`.

## Testing

All validators have comprehensive unit tests in `tests/Honua.Server.Core.Tests/Configuration/`:

- `GraphDatabaseOptionsValidatorTests.cs`
- `CacheInvalidationOptionsValidatorTests.cs`
- `CacheSizeLimitOptionsValidatorTests.cs`
- `DataIngestionOptionsValidatorTests.cs`
- `DataAccessOptionsValidatorTests.cs`

Run tests with:
```bash
dotnet test tests/Honua.Server.Core.Tests/
```

## Adding New Validators

To add a new validator:

1. Create validator class implementing `IValidateOptions<T>`:
```csharp
public sealed class MyOptionsValidator : IValidateOptions<MyOptions>
{
    public ValidateOptionsResult Validate(string? name, MyOptions options)
    {
        var failures = new List<string>();

        // Add validation logic
        if (string.IsNullOrEmpty(options.RequiredField))
            failures.Add("RequiredField is required");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

2. Register in `ConfigurationValidationExtensions.cs`:
```csharp
services.AddSingleton<IValidateOptions<MyOptions>, MyOptionsValidator>();
```

3. Add unit tests in `tests/Honua.Server.Core.Tests/Configuration/`

4. Update this documentation

## References

- [ASP.NET Core Options Validation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [IValidateOptions Interface](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ivalidateoptions-1)
