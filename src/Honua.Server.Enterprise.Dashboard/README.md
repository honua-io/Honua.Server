# Honua Enterprise Admin Dashboard

Blazor Server-based admin dashboard for monitoring and managing multi-tenant SaaS deployments.

## Features

- **Dashboard Overview**: Real-time metrics showing active tenants, trial status, and resource usage
- **Tenant List**: Searchable/filterable list of all tenants with status indicators
- **Tenant Details**: Detailed view of individual tenant usage, quotas, and history
- **Usage Analytics**: Visual representation of tenant resource consumption
- **Quota Monitoring**: Progress bars showing current usage vs. limits per tier

## Architecture

### Components

```
Components/
├── Pages/
│   ├── Home.razor              # Dashboard overview with metrics cards
│   ├── Tenants.razor           # Searchable tenant list
│   └── TenantDetail.razor      # Detailed tenant view with usage charts
└── Layout/
    ├── MainLayout.razor        # Main layout wrapper
    └── NavMenu.razor           # Navigation sidebar
```

### Services

- **TenantUsageAnalyticsService** (`Honua.Server.Enterprise.Multitenancy`):
  - `GetActiveTenantSummariesAsync()` - Get all active tenants with summary data
  - `GetTenantDetailedUsageAsync(tenantId)` - Get detailed usage for a specific tenant
  - `GetTenantUsageHistoryAsync(tenantId, months)` - Get historical usage data
  - `GetDashboardOverviewAsync()` - Get aggregate statistics for dashboard

### Data Models

- `TenantSummary` - Summary info for tenant list
- `TenantDetailedUsage` - Detailed usage metrics for single tenant
- `UsageHistoryPoint` - Historical data point for charts
- `DashboardOverview` - Aggregate statistics

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=honua;Username=postgres;Password=postgres"
  }
}
```

For production, set via environment variables or Azure Configuration:

```bash
ConnectionStrings__Postgres="Host=honua-db.postgres.database.azure.com;Database=honua;..."
```

### Azure Container Apps

Deploy as a Container App alongside other services:

```bicep
resource dashboardApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'honua-dashboard'
  properties: {
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
      }
      secrets: [
        {
          name: 'postgres-connection'
          value: postgresConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'dashboard'
          image: 'honua.azurecr.io/honua-dashboard:latest'
          env: [
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection'
            }
          ]
        }
      ]
    }
  }
}
```

## Running Locally

### Prerequisites

1. .NET 9 SDK
2. PostgreSQL with Honua database
3. Migrations applied (including `006_TenantUsageTracking.sql`)

### Steps

```bash
cd src/Honua.Server.Enterprise.Dashboard

# Update connection string in appsettings.Development.json
dotnet run
```

Navigate to https://localhost:5001

## Security Considerations

This is an **admin-only** dashboard. In production:

1. **Add Authentication**: Integrate Azure AD or other identity provider
   ```csharp
   // Program.cs
   builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options => { ... });

   app.UseAuthentication();
   app.UseAuthorization();
   ```

2. **Add Authorization**: Restrict access to admin roles
   ```razor
   @attribute [Authorize(Roles = "Admin")]
   ```

3. **Network Isolation**: Deploy in private VNet or restrict ingress
   ```bicep
   ingress: {
     external: false  // Internal only
   }
   ```

4. **Audit Logging**: Log all admin actions
   ```csharp
   _logger.LogInformation("Admin {User} viewed tenant {TenantId}", user, tenantId);
   ```

## Database Schema

The dashboard queries these tables:

- `customers` - Tenant metadata
- `licenses` - Trial expiration and tier info
- `tenant_usage` - Monthly aggregated usage
- `tenant_usage_events` - Detailed event log (future)

See `src/Honua.Server.Core/Data/Migrations/006_TenantUsageTracking.sql`

## Screenshots

### Dashboard Overview
Shows active/trial tenant counts, total API requests, storage usage, and recent tenant list.

### Tenant List
Searchable/filterable table with:
- Search by tenant ID, organization, or email
- Filter by subscription status (active/trial/expired)
- Filter by tier (trial/core/pro/enterprise)

### Tenant Details
Detailed view showing:
- Tenant information (ID, organization, contact, tier)
- Current month usage (API requests, storage, processing)
- Quota limits with progress bars
- Usage history table (last 6 months)
- Link to tenant's public URL

## Future Enhancements

- [ ] Real-time charts with Chart.js/ApexCharts (currently using Blazorise.Charts)
- [ ] Export usage reports to CSV/Excel
- [ ] Tenant management actions (suspend, extend trial, upgrade tier)
- [ ] Email notifications for quota warnings
- [ ] Detailed event log viewer
- [ ] Cost analysis per tenant
- [ ] Billing integration

## Troubleshooting

### "Connection refused" error

Check PostgreSQL connection string and ensure database is accessible:

```bash
psql -h localhost -U postgres -d honua -c "SELECT 1;"
```

### "No tenants found"

Ensure tenant data exists:

```sql
SELECT customer_id, organization_name, subscription_status
FROM customers
WHERE deleted_at IS NULL;
```

### Quotas not showing

Check if usage tracking migration was applied:

```sql
SELECT * FROM tenant_usage LIMIT 1;
```

If table doesn't exist, run migration:

```bash
psql -h localhost -U postgres -d honua < src/Honua.Server.Core/Data/Migrations/006_TenantUsageTracking.sql
```

## Related Documentation

- [Enterprise Multitenancy README](../Honua.Server.Enterprise/MULTITENANT_SAAS_README.md)
- [Tenant Usage Tracking Migration](../Honua.Server.Core/Data/Migrations/006_TenantUsageTracking.sql)
- [Azure Deployment Guide](../../deployment/azure/MULTITENANT_DEPLOYMENT.md)

## License

Copyright (c) 2025 HonuaIO. All rights reserved.

This is an **Enterprise module** - requires Honua Enterprise license.
