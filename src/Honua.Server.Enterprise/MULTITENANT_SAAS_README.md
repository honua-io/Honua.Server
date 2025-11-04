# Honua Enterprise Multitenant SaaS Module

## 🎯 Overview

The **Honua.Server.Enterprise** module provides a complete **multitenant SaaS platform** with:

- ✅ **Subdomain-based tenant isolation** (`acme.honua.io`)
- ✅ **Azure Container Apps + Dapr** service mesh with mTLS
- ✅ **YARP API Gateway** for intelligent routing
- ✅ **Automated demo signup** with Azure Functions
- ✅ **Azure DNS automation** for subdomain provisioning
- ✅ **Tenant quotas and usage metering** with enforcement
- ✅ **Trial lifecycle management** with automatic cleanup
- ✅ **Multi-backend routing** (OData, Raster ECS, Vector Lambda, STAC)

## 🏗️ Architecture

```
User Request: https://acme.honua.io/api/collections
    ↓
┌─────────────────────────────────────────────┐
│           Azure DNS (*.honua.io)            │
│         Routes to YARP Gateway IP           │
└──────────────────┬──────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│      YARP Gateway (Container App)           │
│  • Terminates SSL (wildcard *.honua.io)    │
│  • Extracts tenant: "acme"                  │
│  • Routes by path: /api, /raster, /vector  │
│  • Adds X-Tenant-Id: acme header            │
└──────────────────┬──────────────────────────┘
                   ↓ (Dapr mTLS)
┌─────────────────────────────────────────────┐
│      Backend Container Apps (Dapr)          │
│  ┌────────────────────────────────────────┐ │
│  │ TenantMiddleware                       │ │
│  │  • Validates tenant exists             │ │
│  │  • Checks subscription status          │ │
│  │  • Loads tenant context                │ │
│  └────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────┐ │
│  │ QuotaEnforcementMiddleware             │ │
│  │  • Checks API request quota            │ │
│  │  • Checks storage quota                │ │
│  │  • Checks processing quotas            │ │
│  │  • Records usage                       │ │
│  └────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────┐ │
│  │ Business Logic                         │ │
│  │  • Filters data by tenant ID           │ │
│  │  • Returns tenant-specific results     │ │
│  └────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

## 📁 Module Structure

```
Honua.Server.Enterprise/
├── Multitenancy/
│   ├── TenantContext.cs              # Tenant metadata
│   ├── TenantMiddleware.cs           # Subdomain extraction & validation
│   ├── TenantQuotas.cs               # Per-tier resource limits
│   ├── ITenantResolver.cs            # Interface for tenant resolution
│   ├── PostgresTenantResolver.cs     # Database-backed resolver
│   ├── QuotaEnforcementMiddleware.cs # Quota enforcement
│   ├── TenantUsageTracker.cs         # Usage metering service
│   └── README.md (this file)

Honua.Server.Enterprise.Functions/
├── DemoSignupFunction.cs             # POST /demo/signup
├── TrialCleanupFunction.cs           # Daily cleanup job
├── Program.cs
├── host.json
└── local.settings.json

Honua.Server.Gateway/
├── appsettings.Azure.json            # Dapr routing config
├── Program.cs                        # YARP with transforms
└── Dockerfile

deployment/azure/
├── bicep/
│   └── main.bicep                    # Infrastructure as Code
└── MULTITENANT_DEPLOYMENT.md         # Deployment guide
```

## 🚀 Quick Start

### 1. Add to Your Backend Service

```csharp
// Program.cs or Startup.cs

// Add tenant resolution
builder.Services.AddSingleton<ITenantResolver>(sp =>
    new PostgresTenantResolver(
        connectionString: builder.Configuration.GetConnectionString("Postgres"),
        cache: sp.GetRequiredService<IMemoryCache>(),
        logger: sp.GetRequiredService<ILogger<PostgresTenantResolver>>()
    ));

// Add usage tracking
builder.Services.AddSingleton<ITenantUsageTracker>(sp =>
    new TenantUsageTracker(
        connectionString: builder.Configuration.GetConnectionString("Postgres"),
        logger: sp.GetRequiredService<ILogger<TenantUsageTracker>>()
    ));

// Register middleware
app.UseTenantResolution();        // Extract & validate tenant
app.UseQuotaEnforcement();        // Enforce quotas & meter usage
```

### 2. Filter Data by Tenant

```csharp
// In your controller/endpoint
var tenantId = HttpContext.GetRequiredTenantId();

// Query data filtered by tenant
var collections = await _db.QueryAsync<Collection>(@"
    SELECT * FROM collections
    WHERE tenant_id = @TenantId
    ORDER BY created_at DESC",
    new { TenantId = tenantId });
```

### 3. Check Tenant Features

```csharp
var tenantContext = HttpContext.GetTenantContext();

if (!tenantContext.Features.RasterProcessing)
{
    return Forbid("Raster processing not available in your plan");
}

// Check tier
if (tenantContext.Tier == "trial")
{
    // Show upgrade prompt
}
```

## 🔐 Tenant Quotas by Tier

| Resource | Trial | Core | Pro | Enterprise |
|----------|-------|------|-----|------------|
| **Storage** | 5 GB | 50 GB | 500 GB | Unlimited |
| **Datasets** | 10 | 100 | 1,000 | Unlimited |
| **API Requests/Month** | 10,000 | 100,000 | 1,000,000 | Unlimited |
| **Concurrent Requests** | 5 | 10 | 50 | 100 |
| **Raster Processing/Month** | 60 min | 300 min | 3,000 min | Unlimited |
| **Vector Requests/Month** | 1,000 | 10,000 | 100,000 | Unlimited |
| **Builds/Month** | 10 | 100 | 1,000 | Unlimited |
| **Max Export Size** | 100 MB | 500 MB | 2 GB | Unlimited |
| **Rate Limit (req/min)** | 30 | 60 | 300 | 1,000 |

## 📊 Usage Tracking

All tenant resource usage is automatically tracked:

```csharp
// Usage is recorded automatically by middleware, but you can also track manually:

var usageTracker = serviceProvider.GetRequiredService<ITenantUsageTracker>();

// Record raster processing
await usageTracker.RecordRasterProcessingAsync(tenantId, durationMinutes: 15);

// Record storage usage
await usageTracker.RecordStorageUsageAsync(tenantId, bytesUsed: 1024 * 1024 * 500);

// Get current usage
var usage = await usageTracker.GetCurrentUsageAsync(tenantId);
Console.WriteLine($"API Requests this month: {usage.ApiRequests}");
```

## 🎫 Demo Signup Flow

### User Signs Up

```bash
POST https://honua-demo-prod.azurewebsites.net/demo/signup
Content-Type: application/json

{
  "email": "john@acme.com",
  "organizationName": "Acme Corp",
  "name": "John Doe"
}
```

### Backend Processes

1. **Validates** email and organization name
2. **Generates** tenant ID: `acme-corp`
3. **Creates** database records:
   - `customers` table (tenant metadata)
   - `licenses` table (trial license, 14-day expiration)
4. **Creates** Azure DNS A record: `acme-corp.honua.io → YARP IP`
5. **Returns** response with URL

### Response

```json
{
  "tenantId": "acme-corp",
  "organizationName": "Acme Corp",
  "email": "john@acme.com",
  "url": "https://acme-corp.honua.io",
  "trialExpiresAt": "2025-11-12T10:30:00Z",
  "message": "Demo environment created successfully! Your 14-day trial has started."
}
```

### User Accesses Tenant

```bash
# All requests to acme-corp.honua.io are isolated to their data
GET https://acme-corp.honua.io/api/collections
GET https://acme-corp.honua.io/stac/collections
POST https://acme-corp.honua.io/api/datasets
```

## 🧹 Trial Cleanup

Azure Function runs **daily at 2 AM UTC** to clean up expired trials:

1. **Finds** trials expired > 7 days ago (grace period)
2. **Soft deletes** customer records
3. **Revokes** licenses
4. **Deletes** DNS A record
5. **Logs** cleanup activity

Manual trigger:

```bash
az functionapp function trigger \
  --resource-group honua-prod \
  --name honua-demo-prod \
  --function-name TrialCleanup
```

## 🔒 Security Features

### 1. **Tenant Isolation**
- Database queries filtered by `tenant_id`
- Middleware validates tenant on every request
- No cross-tenant data leakage

### 2. **Dapr mTLS**
- All service-to-service communication encrypted
- Automatic certificate rotation
- Zero-trust networking

### 3. **Quota Enforcement**
- Prevents resource abuse
- Returns proper HTTP status codes (429, 507)
- Monthly reset

### 4. **Trial Expiration**
- Automatic enforcement
- Grace period before cleanup
- Email notifications (TODO: add email service)

## 🌐 Multi-Backend Routing

YARP routes to different backend types based on path:

| Path | Backend | Service Type | Use Case |
|------|---------|--------------|----------|
| `/odata/**` | OData Container App | Non-AOT, Reflection | Complex queries |
| `/raster/**` | Raster Container App | Heavy compute, GPU | Raster processing |
| `/vector/**` | Vector Azure Function | Serverless | Lightweight vector ops |
| `/stac/**` | STAC Container App | Standard API | STAC catalog |
| `/api/**` | Core API Container App | Main API | General endpoints |

## 📈 Monitoring

### Application Insights Queries

```kusto
// Top tenants by API usage
traces
| where message contains "tenant"
| extend tenantId = tostring(customDimensions.TenantId)
| summarize RequestCount = count() by tenantId
| top 10 by RequestCount desc

// Quota exceeded events
traces
| where message contains "exceeded quota"
| project timestamp, tenantId = customDimensions.TenantId, message

// Trial signups
traces
| where message contains "Demo signup completed"
| project timestamp, tenantId = customDimensions.TenantId
```

### Container App Metrics

```bash
az monitor metrics list \
  --resource /subscriptions/.../providers/Microsoft.App/containerApps/honua-gateway \
  --metric "Requests"
```

## 🔧 Configuration

### Environment Variables (Backend Services)

```bash
# Database
ConnectionStrings__Postgres="Host=...;Database=honua;..."

# Redis (optional, for distributed rate limiting)
Redis__ConnectionString="redis-cache.redis.cache.windows.net:6380,password=...,ssl=True"

# Tenant resolution cache TTL
TenantResolver__CacheDurationMinutes=5
```

### YARP Gateway Configuration

See `src/Honua.Server.Gateway/appsettings.Azure.json` for Dapr routing configuration.

## 🚢 Deployment

See [deployment/azure/MULTITENANT_DEPLOYMENT.md](../../deployment/azure/MULTITENANT_DEPLOYMENT.md) for full deployment guide.

Quick deploy:

```bash
cd deployment/azure/bicep
az deployment sub create \
  --location eastus2 \
  --template-file main.bicep \
  --parameters parameters.prod.json
```

## 🧪 Testing

### Local Development

```bash
# Run PostgreSQL
docker run -d -p 5432:5432 \
  -e POSTGRES_PASSWORD=postgres \
  postgres:16

# Apply migrations
psql -h localhost -U postgres -d postgres < src/Honua.Server.Core/Data/Migrations/001_InitialSchema.sql
psql -h localhost -U postgres -d postgres < src/Honua.Server.Core/Data/Migrations/006_TenantUsageTracking.sql

# Run backend service
cd src/Honua.Server.Host
dotnet run

# Test with tenant header
curl http://localhost:5000/api/collections \
  -H "X-Tenant-Id: test-tenant"
```

### Integration Tests

```csharp
[Fact]
public async Task TenantMiddleware_ExtractsTenantFromSubdomain()
{
    var host = "acme.honua.io";
    var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/api/test");

    var response = await _client.SendAsync(request);

    // Verify tenant was extracted
    var tenantId = response.Headers.GetValues("X-Resolved-Tenant-Id").FirstOrDefault();
    Assert.Equal("acme", tenantId);
}
```

## 📚 Related Documentation

- [YARP Gateway Configuration](../Honua.Server.Gateway/README.md)
- [Azure Deployment Guide](../../deployment/azure/MULTITENANT_DEPLOYMENT.md)
- [Database Migrations](../Honua.Server.Core/Data/Migrations/README.md)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Dapr Documentation](https://docs.dapr.io/)

## 🆘 Troubleshooting

### Tenant Not Found Error

```
Status: 404
Error: "tenant_not_found"
```

**Solution**: Check if tenant exists in database:

```sql
SELECT customer_id, organization_name, subscription_status
FROM customers
WHERE customer_id = 'acme' AND deleted_at IS NULL;
```

### Quota Exceeded Error

```
Status: 429
Error: "quota_exceeded"
```

**Solution**: Check current usage:

```sql
SELECT * FROM tenant_usage
WHERE tenant_id = 'acme'
  AND period_start = DATE_TRUNC('month', NOW());
```

### DNS Not Resolving

```bash
nslookup acme.honua.io
# Should return YARP gateway IP
```

**Solution**: Verify DNS record exists:

```bash
az network dns record-set a show \
  --resource-group honua-dns \
  --zone-name honua.io \
  --name acme
```

## 💰 Cost Estimates (Azure)

| Component | Tier | Monthly Cost |
|-----------|------|--------------|
| Container Apps (3 apps) | Consumption | ~$50 |
| Azure Functions | Consumption | ~$5 |
| PostgreSQL Flexible Server | Burstable B2s | ~$80 |
| Azure DNS | Standard | ~$0.50 |
| **Total Dev/Test** | | **~$135** |
| **Total Production** | With scaling | **~$500** |

## 📋 License

Copyright (c) 2025 HonuaIO. All rights reserved.

This is an **Enterprise module** - requires Honua Enterprise license.
