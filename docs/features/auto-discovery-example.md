# Auto-Discovery Usage Examples

## Complete Integration Example

### Step 1: Configure Data Source

First, ensure you have a PostGIS data source configured in your `appsettings.json` or metadata:

```json
{
  "ConnectionStrings": {
    "PostGIS": "Host=localhost;Database=gis;Username=postgres;Password=postgres"
  },
  "honua": {
    "metadata": {
      "provider": "json",
      "path": "./metadata"
    }
  }
}
```

In your metadata JSON:

```json
{
  "catalog": {
    "id": "my-gis-catalog",
    "title": "My GIS Catalog"
  },
  "folders": [
    {
      "id": "discovered",
      "title": "Auto-discovered Tables"
    }
  ],
  "dataSources": [
    {
      "id": "main-postgis",
      "provider": "postgis",
      "connectionString": "{{ConnectionStrings:PostGIS}}"
    }
  ]
}
```

### Step 2: Enable Auto-Discovery in Program.cs

```csharp
// At the top of Program.cs, after builder creation

using Honua.Server.Core.Discovery;
using Honua.Server.Host.Discovery;

var builder = WebApplication.CreateBuilder(args);

// ... existing configuration ...

// Add auto-discovery BEFORE building the app
builder.Services.AddHonuaAutoDiscovery(options =>
{
    // Required: Specify which data source to discover from
    options.DataSourceId = "main-postgis";

    // Enable for both OData and OGC APIs
    options.DiscoverPostGISTablesAsODataCollections = true;
    options.DiscoverPostGISTablesAsOgcCollections = true;

    // Performance settings
    options.RequireSpatialIndex = builder.Environment.IsProduction();
    options.MaxTables = builder.Environment.IsProduction() ? 50 : 1000;
    options.CacheDuration = TimeSpan.FromMinutes(5);
    options.BackgroundRefresh = true;

    // Friendly names for collections
    options.UseFriendlyNames = true;

    // Exclude system schemas and temporary tables
    options.ExcludeSchemas = new[] { "topology", "tiger" };
    options.ExcludeTablePatterns = new[] { "temp_*", "staging_*", "_*" };

    // Organization
    options.DefaultFolderId = "discovered";
});

var app = builder.Build();

// ... existing middleware ...

// Map admin endpoints (requires authentication)
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Features:EnableDiscoveryAdmin"))
{
    app.MapDiscoveryAdminEndpoints();
}

app.Run();
```

### Step 3: Test It Out

Start Honua and navigate to:

1. **Discovery Status**: `http://localhost:5000/admin/discovery/status`
2. **List Tables**: `http://localhost:5000/admin/discovery/tables`
3. **OData Metadata**: `http://localhost:5000/odata/$metadata`
4. **OGC Collections**: `http://localhost:5000/collections`

## Scenario-Based Examples

### Scenario 1: Development - Maximum Visibility

**Goal**: Expose everything for rapid development and testing.

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "dev-postgis";
    options.RequireSpatialIndex = false;  // Even tables without indexes
    options.MaxTables = 0;                // No limit
    options.ComputeExtentOnDiscovery = false;  // Fast discovery
    options.UseFriendlyNames = true;
});
```

### Scenario 2: Production - Security & Performance

**Goal**: Only expose performant, production-ready tables.

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "prod-postgis";

    // Only tables with spatial indexes (required for good performance)
    options.RequireSpatialIndex = true;

    // Limit number of tables (prevent overwhelming API)
    options.MaxTables = 50;

    // Long cache duration to reduce database load
    options.CacheDuration = TimeSpan.FromHours(1);
    options.BackgroundRefresh = true;

    // Exclude sensitive and system schemas
    options.ExcludeSchemas = new[]
    {
        "topology",
        "tiger",
        "private",
        "internal",
        "audit"
    };

    // Exclude temporary and staging tables
    options.ExcludeTablePatterns = new[]
    {
        "temp_*",
        "tmp_*",
        "staging_*",
        "test_*",
        "_*",
        "*_backup",
        "*_old"
    };

    // Don't compute extent on discovery (too slow for large tables)
    options.ComputeExtentOnDiscovery = false;

    // Friendly names
    options.UseFriendlyNames = true;
});
```

### Scenario 3: Mixed Mode - Discovery + Manual Configuration

**Goal**: Use auto-discovery for simple tables, manual config for complex ones.

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "postgis";
    options.DefaultFolderId = "auto-discovered";
    options.DefaultFolderTitle = "Auto-discovered Layers";

    // Only discover tables in 'public' schema
    options.ExcludeSchemas = new[]
    {
        "topology",
        "tiger",
        "admin",  // Manually configured
        "special" // Manually configured
    };

    options.RequireSpatialIndex = true;
    options.MaxTables = 100;
});
```

Your metadata JSON can still have manually configured services:

```json
{
  "services": [
    {
      "id": "admin-service",
      "title": "Administrative Boundaries",
      "folder": "admin",
      "serviceType": "feature",
      "dataSourceId": "postgis"
      // ... custom configuration ...
    }
  ]
}
```

Both will coexist: manually configured services in their folders, auto-discovered in the "auto-discovered" folder.

### Scenario 4: Read-Only API

**Goal**: Expose data for read-only access, no editing.

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "readonly-postgis";
    options.DiscoverPostGISTablesAsODataCollections = true;
    options.DiscoverPostGISTablesAsOgcCollections = true;

    // All discovered layers will be read-only by default
    // (editing is not enabled in auto-discovered layers)

    options.RequireSpatialIndex = true;
    options.CacheDuration = TimeSpan.FromHours(2);  // Long cache for read-only
});
```

Use read-only database credentials:

```json
{
  "dataSources": [
    {
      "id": "readonly-postgis",
      "provider": "postgis",
      "connectionString": "Host=db.example.com;Database=gis;Username=readonly_user;Password=xxx"
    }
  ]
}
```

### Scenario 5: Multi-Schema Organization

**Goal**: Discover tables from multiple schemas, organized into logical folders.

```csharp
// You'll need to create multiple discovery configurations
// One approach: Use a custom service that wraps the discovery service

// For now, use exclusions to control what gets discovered
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "postgis";

    // Discover from specific schemas by excluding others
    // This discovers from 'public', 'infrastructure', 'environmental'
    options.ExcludeSchemas = new[]
    {
        "topology",
        "tiger",
        "pg_catalog",
        "information_schema"
        // Everything else gets discovered
    };

    options.DefaultFolderId = "geographic-data";
    options.UseFriendlyNames = true;
});
```

### Scenario 6: Conditional Discovery

**Goal**: Enable discovery only in certain environments or configurations.

```csharp
// Enable discovery only when explicitly configured
var enableDiscovery = builder.Configuration.GetValue<bool>("Features:AutoDiscovery", false);

if (enableDiscovery)
{
    builder.Services.AddHonuaAutoDiscovery(options =>
    {
        options.DataSourceId = builder.Configuration["Discovery:DataSourceId"] ?? "postgis";
        options.MaxTables = builder.Configuration.GetValue<int>("Discovery:MaxTables", 100);
        options.RequireSpatialIndex = builder.Configuration.GetValue<bool>("Discovery:RequireIndex", true);
    });
}
```

In `appsettings.json`:

```json
{
  "Features": {
    "AutoDiscovery": true
  },
  "Discovery": {
    "DataSourceId": "main-postgis",
    "MaxTables": 50,
    "RequireIndex": true
  }
}
```

In `appsettings.Production.json`:

```json
{
  "Features": {
    "AutoDiscovery": false  // Disable in production
  }
}
```

## Integration with Authentication

Auto-discovery respects both database permissions AND application-level authorization.

### Database-Level Security

```sql
-- Create read-only user
CREATE USER honua_readonly WITH PASSWORD 'secure_password';

-- Grant access only to specific schemas
GRANT USAGE ON SCHEMA public TO honua_readonly;
GRANT USAGE ON SCHEMA infrastructure TO honua_readonly;

-- Grant SELECT only (no write access)
GRANT SELECT ON ALL TABLES IN SCHEMA public TO honua_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA infrastructure TO honua_readonly;

-- Automatically grant SELECT on future tables
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON TABLES TO honua_readonly;
```

### Application-Level Authorization

```csharp
// In Program.cs

builder.Services.AddAuthorization(options =>
{
    // Admin endpoints require admin role
    options.AddPolicy("AdminPolicy", policy =>
    {
        policy.RequireRole("Admin");
    });

    // Data access can be controlled per collection
    options.AddPolicy("DataAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// Apply authorization to discovered endpoints
// This is handled automatically by the OData/OGC handlers
```

## Admin Dashboard Integration

If you're building an admin dashboard, here's how to integrate discovery:

```typescript
// TypeScript/React example

interface DiscoveryStatus {
  enabled: boolean;
  odataDiscoveryEnabled: boolean;
  ogcDiscoveryEnabled: boolean;
  postGisDataSourceCount: number;
  // ... other properties
}

interface DiscoveredTable {
  schema: string;
  tableName: string;
  qualifiedName: string;
  geometryColumn: string;
  srid: number;
  geometryType: string;
  primaryKeyColumn: string;
  hasSpatialIndex: boolean;
  estimatedRowCount: number;
}

// Get discovery status
async function getDiscoveryStatus(): Promise<DiscoveryStatus> {
  const response = await fetch('/admin/discovery/status', {
    headers: {
      'Authorization': `Bearer ${authToken}`
    }
  });
  return response.json();
}

// List discovered tables
async function getDiscoveredTables(): Promise<DiscoveredTable[]> {
  const response = await fetch('/admin/discovery/tables', {
    headers: {
      'Authorization': `Bearer ${authToken}`
    }
  });
  const data = await response.json();
  return data.tables;
}

// Refresh cache
async function refreshDiscoveryCache(): Promise<void> {
  await fetch('/admin/discovery/refresh', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${authToken}`
    }
  });
}
```

## Testing Your Setup

### 1. Verify Discovery is Enabled

```bash
curl http://localhost:5000/admin/discovery/status
```

Should return:
```json
{
  "enabled": true,
  "odataDiscoveryEnabled": true,
  "ogcDiscoveryEnabled": true,
  // ...
}
```

### 2. List Discovered Tables

```bash
curl http://localhost:5000/admin/discovery/tables
```

Should return list of tables with metadata.

### 3. Test OData Access

```bash
# Get OData service document
curl http://localhost:5000/odata

# Get metadata
curl http://localhost:5000/odata/\$metadata

# Query a collection
curl "http://localhost:5000/odata/roads?\$top=10&\$select=name,geom"
```

### 4. Test OGC API Access

```bash
# Get collections
curl http://localhost:5000/collections

# Get specific collection
curl http://localhost:5000/collections/public_roads

# Query items
curl "http://localhost:5000/collections/public_roads/items?limit=10"
```

### 5. Performance Test

```bash
# Measure discovery time
time curl http://localhost:5000/admin/discovery/refresh

# Verify caching
time curl http://localhost:5000/admin/discovery/tables
time curl http://localhost:5000/admin/discovery/tables  # Should be faster (cached)
```

## Common Issues and Solutions

See the [Troubleshooting section](AUTO_DISCOVERY.md#troubleshooting) in the main documentation.

## Next Steps

1. Enable auto-discovery in your environment
2. Test with your PostGIS database
3. Configure exclusions and limits for production
4. Set up admin access and monitoring
5. Optionally: Add manual configuration for complex layers
6. Document your discovered tables for users
