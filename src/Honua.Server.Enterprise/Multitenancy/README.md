# Multitenancy Module

Enterprise-tier feature for SaaS and multi-tenant deployments.

## Overview

The Multitenancy module provides comprehensive tenant isolation, subscription management, and usage tracking for Honua Server deployments. It enables secure multi-tenant architectures with subdomain-based or header-based tenant routing, quota enforcement, and trial management.

**Key Capabilities:**
- Automatic tenant resolution from subdomain or HTTP headers
- Row-level security (RLS) with tenant isolation at the database layer
- Trial subscription management with expiration tracking
- Usage tracking and quota enforcement
- Feature flag management per tenant tier
- In-memory caching for performance (5-minute TTL)

## Architecture

### Tenant Resolution Flow

```
Client Request
    |
    v
[TenantMiddleware]
    |
    +-- Extract tenant ID from:
    |   1. X-Tenant-Id header (set by YARP proxy)
    |   2. Subdomain (e.g., acme.honua.io → "acme")
    |
    v
[ITenantResolver] (PostgresTenantResolver)
    |
    +-- Check memory cache
    |   |
    |   +-- Cache hit? → Return TenantContext
    |   |
    |   +-- Cache miss? → Query PostgreSQL
    |       |
    |       +-- Validate tenant status (active, trial)
    |       +-- Load feature flags and quotas
    |       +-- Cache for 5 minutes
    |
    v
[TenantContext] stored in HttpContext.Items
    |
    v
[QuotaEnforcementMiddleware] (Optional)
    |
    +-- Check usage against quotas
    +-- Return 429 if quota exceeded
    |
    v
[Application Endpoints]
    |
    +-- Access tenant via HttpContext.GetTenantContext()
    +-- Filter queries by tenant_id for isolation
```

### Database Schema

Tenant data is stored in two primary tables:

**customers table:**
```sql
CREATE TABLE customers (
    customer_id TEXT PRIMARY KEY,        -- Tenant identifier (e.g., "acme")
    organization_name TEXT,              -- Display name
    tier TEXT,                           -- trial, core, pro, enterprise, asp
    subscription_status TEXT,            -- trial, active, suspended, cancelled
    created_at TIMESTAMPTZ NOT NULL,
    deleted_at TIMESTAMPTZ               -- Soft delete
);
```

**licenses table:**
```sql
CREATE TABLE licenses (
    license_id UUID PRIMARY KEY,
    customer_id TEXT REFERENCES customers(customer_id),
    status TEXT,                         -- active, trial, expired
    trial_expires_at TIMESTAMPTZ,        -- Trial expiration date
    features JSONB,                      -- Feature flags
    max_builds_per_month INT,
    max_concurrent_builds INT,
    max_registries INT
);
```

### Tenant Context

The `TenantContext` class provides tenant information to application code:

```csharp
public class TenantContext
{
    public string TenantId { get; init; }              // "acme"
    public string? CustomerId { get; init; }           // Database ID
    public string? OrganizationName { get; init; }     // "ACME Corporation"
    public string? Tier { get; init; }                 // "pro"
    public string? SubscriptionStatus { get; init; }   // "active"
    public DateTimeOffset? TrialExpiresAt { get; init; }
    public TenantFeatures Features { get; init; }      // Feature flags

    // Computed properties
    public bool IsTrial { get; }
    public bool IsTrialExpired { get; }
    public bool IsActive { get; }
}
```

## API Integration

### Middleware Setup

Add tenant resolution middleware in `Program.cs`:

```csharp
// In Program.cs (before UseAuthorization)
app.UseTenantResolution();  // Extracts tenant from subdomain or header
app.UseQuotaEnforcement();  // Optional: enforce quotas
```

### Accessing Tenant Context in Controllers

```csharp
[ApiController]
[Route("api/v1/data")]
public class DataController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetData()
    {
        // Get tenant context from middleware
        var tenantContext = HttpContext.GetTenantContext();

        if (tenantContext == null)
        {
            // Single-tenant mode or no tenant middleware
            return Ok(await GetAllData());
        }

        // Multi-tenant mode: filter by tenant
        var tenantId = tenantContext.TenantId;
        return Ok(await GetDataForTenant(tenantId));
    }

    // Alternative: require tenant context
    [HttpPost]
    public async Task<IActionResult> CreateData([FromBody] CreateDataRequest request)
    {
        var tenantId = HttpContext.GetRequiredTenantId(); // Throws if missing

        // Check feature access
        var tenantContext = HttpContext.GetTenantContext()!;
        if (!tenantContext.Features.RasterProcessing)
        {
            return Forbid("Raster processing not enabled for this tenant");
        }

        // Create data with tenant isolation
        await CreateDataForTenant(tenantId, request);
        return Created();
    }
}
```

### Database Queries with Tenant Isolation

Always filter queries by tenant ID for row-level security:

```csharp
public async Task<List<Asset>> GetAssetsAsync(string tenantId)
{
    const string sql = @"
        SELECT id, name, geometry, properties
        FROM assets
        WHERE tenant_id = @TenantId
          AND deleted_at IS NULL
        ORDER BY created_at DESC";

    await using var connection = new NpgsqlConnection(_connectionString);
    return (await connection.QueryAsync<Asset>(sql, new { TenantId = tenantId }))
        .AsList();
}
```

## Tenant Routing Strategies

### 1. Subdomain-Based Routing (Recommended for SaaS)

**URL Pattern:** `https://{tenant}.honua.io/api/v1/...`

**Examples:**
- `https://acme.honua.io/api/v1/assets` → tenant = "acme"
- `https://demo-123.honua.io/api/v1/collections` → tenant = "demo-123"

**Configuration:**

```csharp
// TenantMiddleware automatically extracts subdomain
// Excludes known system subdomains: www, api, intake, orchestrator, prometheus, grafana, admin
```

**YARP Reverse Proxy Config (appsettings.json):**

```json
{
  "ReverseProxy": {
    "Routes": {
      "tenant-route": {
        "ClusterId": "honua-cluster",
        "Match": {
          "Hosts": ["*.honua.io"]
        },
        "Transforms": [
          {
            "RequestHeader": "X-Tenant-Id",
            "Set": "{MatchedHost:subdomain}"
          }
        ]
      }
    }
  }
}
```

### 2. Header-Based Routing (For API integrations)

**Header:** `X-Tenant-Id: acme`

**Example:**

```bash
curl -H "X-Tenant-Id: acme" \
     -H "Authorization: Bearer <token>" \
     https://api.honua.io/api/v1/assets
```

**Priority:** Header takes precedence over subdomain if both are present.

## Trial and Subscription Management

### Trial Lifecycle

1. **Trial Creation:**
   ```sql
   INSERT INTO licenses (customer_id, status, trial_expires_at)
   VALUES ('new-customer', 'trial', NOW() + INTERVAL '14 days');
   ```

2. **Trial Expiration Check:**
   - `TenantMiddleware` checks `IsTrialExpired` property
   - Returns HTTP 403 with message: "Trial period has expired. Please upgrade to continue using Honua."

3. **Trial to Paid Conversion:**
   ```sql
   UPDATE licenses
   SET status = 'active',
       trial_expires_at = NULL
   WHERE customer_id = 'customer-id';

   UPDATE customers
   SET subscription_status = 'active',
       tier = 'pro'
   WHERE customer_id = 'customer-id';
   ```

### Subscription Status Flow

```
trial → active → suspended → cancelled
  |        |         |
  +--------+---------+
         (reactivation)
```

**Status Handling:**
- `active` or `trial` (not expired): Tenant allowed
- `suspended`: HTTP 403 - "This account is not active. Please contact support."
- `cancelled`: HTTP 404 - Tenant not found

## Feature Flags

Feature flags control which capabilities are available to each tenant based on their tier:

```csharp
public class TenantFeatures
{
    public bool AiIntake { get; init; } = true;
    public bool CustomModules { get; init; } = true;
    public bool PriorityBuilds { get; init; } = false;
    public bool DedicatedCache { get; init; } = false;
    public bool SlaGuarantee { get; init; } = false;
    public bool RasterProcessing { get; init; } = true;
    public bool VectorProcessing { get; init; } = true;
    public bool ODataApi { get; init; } = true;
    public bool StacApi { get; init; } = true;

    public int? MaxBuildsPerMonth { get; init; } = 100;
    public int MaxConcurrentBuilds { get; init; } = 1;
    public int MaxRegistries { get; init; } = 3;
}
```

### Feature Flag Storage (PostgreSQL JSONB)

```sql
UPDATE licenses
SET features = '{
  "ai_intake": true,
  "priority_builds": true,
  "dedicated_cache": true,
  "sla_guarantee": true,
  "max_builds_per_month": null,
  "max_concurrent_builds": 5,
  "max_registries": 10
}'::jsonb
WHERE customer_id = 'enterprise-customer';
```

### Checking Features in Code

```csharp
var tenantContext = HttpContext.GetTenantContext()!;

if (!tenantContext.Features.RasterProcessing)
{
    return StatusCode(403, new
    {
        error = "feature_disabled",
        message = "Raster processing is not enabled for your subscription tier."
    });
}

// Check quota limits
if (currentBuildCount >= tenantContext.Features.MaxBuildsPerMonth)
{
    return StatusCode(429, new
    {
        error = "quota_exceeded",
        message = $"Monthly build quota ({tenantContext.Features.MaxBuildsPerMonth}) exceeded."
    });
}
```

## Usage Tracking and Quotas

The `TenantUsageTracker` service monitors resource consumption:

```csharp
public class TenantUsageTracker
{
    /// <summary>
    /// Records a build execution for usage tracking
    /// </summary>
    Task RecordBuildAsync(string tenantId, BuildMetrics metrics);

    /// <summary>
    /// Gets current month's usage for a tenant
    /// </summary>
    Task<TenantUsage> GetCurrentUsageAsync(string tenantId);

    /// <summary>
    /// Checks if tenant can execute action without exceeding quota
    /// </summary>
    Task<bool> CanExecuteAsync(string tenantId, string resourceType);
}
```

### Quota Enforcement Middleware

```csharp
public class QuotaEnforcementMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantContext = context.GetTenantContext();
        if (tenantContext == null)
        {
            await _next(context);
            return;
        }

        // Check if endpoint requires quota enforcement
        var endpoint = context.GetEndpoint();
        var quotaAttribute = endpoint?.Metadata.GetMetadata<EnforceQuotaAttribute>();

        if (quotaAttribute != null)
        {
            var usage = await _usageTracker.GetCurrentUsageAsync(tenantContext.TenantId);

            if (usage.BuildsThisMonth >= tenantContext.Features.MaxBuildsPerMonth)
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "quota_exceeded",
                    message = "Monthly build quota exceeded. Please upgrade your plan.",
                    quota = tenantContext.Features.MaxBuildsPerMonth,
                    current = usage.BuildsThisMonth
                });
                return;
            }
        }

        await _next(context);
    }
}
```

## Performance and Scalability

### Caching Strategy

- **Cache Duration:** 5 minutes (configurable)
- **Cache Key:** `tenant:{tenant_id}`
- **Cache Invalidation:** Manual via `IMemoryCache.Remove()` when tenant data changes

**Cache Hit Ratio:** Target > 95% for typical workloads

**Performance Impact:**
- Cache hit: ~1ms (in-memory lookup)
- Cache miss: ~5-15ms (PostgreSQL query + cache write)

### SQL Query Optimization

All tenant resolution queries use indexed columns:

```sql
CREATE INDEX idx_customers_customer_id ON customers(customer_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_licenses_customer_id ON licenses(customer_id);
```

### Database Connection Pooling

Use connection pooling for optimal performance:

```csharp
"ConnectionStrings": {
  "Tenants": "Host=localhost;Database=honua;Username=honua;Password=***;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
}
```

## Security Considerations

### SQL Injection Prevention

- All tenant IDs are validated with regex: `^[a-zA-Z0-9\-_]+$`
- Maximum length: 100 characters
- Sanitization applied before cache key generation
- Parameterized queries used for all database operations

```csharp
private static readonly Regex TenantIdPattern = new(@"^[a-zA-Z0-9\-_]+$");

private bool ValidateTenantId(string tenantId)
{
    if (tenantId.Length > 100) return false;
    if (!TenantIdPattern.IsMatch(tenantId)) return false;
    return true;
}
```

### Cache Poisoning Prevention

- Tenant IDs sanitized before use as cache keys
- Invalid tenant IDs rejected at middleware level
- Cache entries scoped to application memory (no shared cache vulnerabilities)

### Data Isolation

**Row-Level Security (RLS):**

All queries MUST include `WHERE tenant_id = @TenantId` clause:

```sql
-- CORRECT: Tenant-isolated query
SELECT * FROM assets WHERE tenant_id = @TenantId;

-- INCORRECT: Missing tenant filter (security vulnerability!)
SELECT * FROM assets;
```

**PostgreSQL RLS Policies (Advanced):**

For defense-in-depth, enable PostgreSQL row-level security:

```sql
ALTER TABLE assets ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON assets
    USING (tenant_id = current_setting('app.current_tenant_id')::text);

-- Set tenant context in connection
SET app.current_tenant_id = 'acme';
```

## Configuration

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
services.AddMemoryCache();

services.AddSingleton<ITenantResolver>(sp =>
{
    var connectionString = configuration.GetConnectionString("Tenants");
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<PostgresTenantResolver>>();
    return new PostgresTenantResolver(connectionString, cache, logger);
});

services.AddScoped<TenantUsageTracker>();
services.AddScoped<TenantUsageAnalyticsService>();
```

### Middleware Registration

```csharp
// In Program.cs (before UseAuthorization, after UseRouting)
app.UseTenantResolution();
app.UseQuotaEnforcement(); // Optional
```

### Environment Variables

```bash
# Database connection for tenant resolution
TENANT_DB_CONNECTION_STRING="Host=postgres;Database=honua;..."

# Cache duration (optional, default: 5 minutes)
TENANT_CACHE_DURATION_MINUTES=5

# Enable quota enforcement (optional, default: false)
ENABLE_QUOTA_ENFORCEMENT=true
```

## Integration with Core Services

### Integration Points

1. **RBAC (Role-Based Access Control):**
   - Permissions scoped to tenant
   - Users belong to single tenant
   - Admin users can access multiple tenants (via `is_system_admin` flag)

2. **OGC API Features:**
   - All collections scoped to tenant
   - Spatial queries filtered by `tenant_id`

3. **Geoprocessing Jobs:**
   - Jobs queued per-tenant
   - Resource quotas enforced
   - Results isolated by tenant

4. **SensorThings API:**
   - Things/Observations scoped to tenant
   - DataArrays processed per-tenant

## Monitoring and Analytics

### Tenant Usage Metrics

The `TenantUsageAnalyticsService` provides insights:

```csharp
public class TenantUsageAnalytics
{
    public string TenantId { get; set; }
    public int BuildsThisMonth { get; set; }
    public int ConcurrentBuilds { get; set; }
    public long StorageUsedGB { get; set; }
    public int ApiRequestsToday { get; set; }
    public double AverageResponseTimeMs { get; set; }
}
```

### Prometheus Metrics (Example)

```csharp
// Custom metrics for monitoring
private static readonly Counter TenantRequestsTotal = Metrics.CreateCounter(
    "honua_tenant_requests_total",
    "Total requests per tenant",
    new CounterConfiguration { LabelNames = new[] { "tenant_id", "status_code" } }
);

private static readonly Histogram TenantResponseTime = Metrics.CreateHistogram(
    "honua_tenant_response_time_seconds",
    "Response time per tenant",
    new HistogramConfiguration { LabelNames = new[] { "tenant_id" } }
);
```

## Troubleshooting

### Common Issues

**1. Tenant Not Found (404)**

**Cause:** Tenant doesn't exist in database or is soft-deleted.

**Solution:**
```sql
SELECT * FROM customers WHERE customer_id = 'tenant-id' AND deleted_at IS NULL;
```

**2. Trial Expired (403)**

**Cause:** Trial period ended.

**Solution:**
```sql
UPDATE licenses SET status = 'active', trial_expires_at = NULL
WHERE customer_id = 'tenant-id';
```

**3. Cache Not Refreshing**

**Cause:** Tenant data updated but cache not invalidated.

**Solution:**
```csharp
// Manually invalidate cache
_cache.Remove($"tenant:{tenantId}");
```

**4. Subdomain Not Recognized**

**Cause:** Subdomain in excluded list or invalid format.

**Solution:** Ensure subdomain is not in excluded list:
```csharp
var excludedSubdomains = new[] { "www", "api", "intake", "orchestrator", "prometheus", "grafana", "admin" };
```

## Best Practices

1. **Always Filter by Tenant:** Include `tenant_id` in all queries to prevent data leakage.

2. **Use Extension Methods:** Use `HttpContext.GetTenantContext()` for consistent access.

3. **Validate Input:** Never trust client-provided tenant IDs; always use middleware-resolved tenant.

4. **Cache Appropriately:** Use 5-minute cache for tenant context; invalidate on updates.

5. **Monitor Quotas:** Track usage proactively to alert tenants before quota exceeded.

6. **Audit Tenant Access:** Log all tenant resolution and quota enforcement for security audits.

7. **Test Isolation:** Use integration tests to verify tenant data isolation.

## Related Documentation

- [MULTITENANT_SAAS_README.md](/home/user/Honua.Server/src/Honua.Server.Enterprise/MULTITENANT_SAAS_README.md) - SaaS deployment guide
- [ENTERPRISE_FEATURES.md](/home/user/Honua.Server/src/Honua.Server.Enterprise/ENTERPRISE_FEATURES.md) - Enterprise features overview
- Authentication Module - User authentication and JWT tokens
- RBAC Module - Role-based access control with tenant scoping
