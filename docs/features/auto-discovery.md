# Auto-Discovery for PostGIS Tables

## Overview

Honua's **auto-discovery** feature enables **zero-configuration** deployment where you simply point Honua at a PostGIS database, and all spatial tables are instantly available through OData and OGC API Features with no manual configuration required.

Inspired by [pg_tileserv](https://github.com/CrunchyData/pg_tileserv)'s zero-config approach, this feature makes Honua the fastest way to expose PostGIS data as modern web APIs.

## Key Benefits

- **Zero Configuration**: No metadata files or manual layer configuration needed
- **Instant APIs**: All geometry tables automatically exposed via OData and OGC APIs
- **30-Second Demo**: The killer demo that sells Honua
- **Production Ready**: Security controls, caching, and performance optimizations built-in
- **Discoverable**: Automatic schema detection, friendly names, and OpenAPI documentation

## Quick Start

### 1. Basic Setup

```csharp
// In Program.cs or Startup.cs
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "my-postgis-db";  // Your PostGIS data source
});
```

That's it! Start Honua and all geometry tables are now available.

### 2. Browse Your Data

Open your browser to:

- **OData**: `http://localhost:5000/odata` - See all collections
- **OGC API**: `http://localhost:5000/collections` - See all feature collections
- **Admin**: `http://localhost:5000/admin/discovery/status` - View discovery status

### 3. Query Data

```bash
# OData query
curl http://localhost:5000/odata/my_roads?\$top=10&\$select=name,geom

# OGC API Features query
curl http://localhost:5000/collections/public_roads/items?limit=10

# Filter by spatial query
curl "http://localhost:5000/collections/public_roads/items?bbox=-122.5,37.7,-122.4,37.8"
```

## Configuration Options

### Basic Options

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    // Enable/disable entire feature
    options.Enabled = true;

    // Enable for specific APIs
    options.DiscoverPostGISTablesAsODataCollections = true;
    options.DiscoverPostGISTablesAsOgcCollections = true;

    // Specify data source (if you have multiple)
    options.DataSourceId = "main-postgis";

    // Use friendly collection names
    options.UseFriendlyNames = true;
});
```

### Filtering Options

```csharp
options.ExcludeSchemas = new[]
{
    "topology",      // Exclude topology schema
    "tiger",         // Exclude TIGER geocoder schema
    "private"        // Exclude your private schema
};

options.ExcludeTablePatterns = new[]
{
    "temp_*",        // Exclude temporary tables
    "staging_*",     // Exclude staging tables
    "_*",            // Exclude tables starting with underscore
    "*_backup"       // Exclude backup tables
};

// Only expose tables with spatial indexes (recommended for performance)
options.RequireSpatialIndex = true;

// Limit number of tables (safety mechanism)
options.MaxTables = 100;
```

### Performance Options

```csharp
// Cache discovery results (recommended)
options.CacheDuration = TimeSpan.FromMinutes(5);

// Background cache refresh
options.BackgroundRefresh = true;
options.BackgroundRefreshInterval = TimeSpan.FromMinutes(5);

// Compute extents on discovery (can be slow for large tables)
options.ComputeExtentOnDiscovery = false;  // Computed on-demand by default
```

### Organization Options

```csharp
// Group discovered tables in a folder
options.DefaultFolderId = "discovered";
options.DefaultFolderTitle = "Auto-discovered Tables";

// Generate OpenAPI documentation
options.GenerateOpenApiDocs = true;
```

## How It Works

### Discovery Process

1. **Scan PostGIS**: Queries `geometry_columns` view to find all spatial tables
2. **Introspect Schema**: Uses existing `PostgresSchemaDiscoveryService` to get columns, types, indexes
3. **Apply Filters**: Excludes tables based on configured patterns and requirements
4. **Generate Metadata**: Creates service and layer definitions on-the-fly
5. **Expose APIs**: Integrates with OData and OGC handlers
6. **Cache Results**: Stores discovery results for configured duration

### PostGIS Queries Used

```sql
-- Find all geometry tables
SELECT f_table_schema, f_table_name, f_geometry_column, type, srid
FROM geometry_columns
WHERE f_table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY f_table_schema, f_table_name;

-- Check for spatial index
SELECT COUNT(*)
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = 'my_table'
  AND indexdef LIKE '%USING gist%';

-- Get row count estimate
SELECT reltuples::bigint
FROM pg_class
WHERE oid = 'public.my_table'::regclass;

-- Compute extent (optional, can be slow)
SELECT ST_Extent(geom) FROM public.my_table;
```

## Security Considerations

### Database Permissions

Auto-discovery **respects database permissions**. Tables are only discovered if the database user has `SELECT` permission.

```sql
-- Grant access to specific tables
GRANT SELECT ON schema.table1 TO honua_user;
GRANT SELECT ON schema.table2 TO honua_user;

-- Revoke access to hide tables
REVOKE SELECT ON schema.sensitive_table FROM honua_user;
```

### Authentication Required

Admin endpoints require authentication:

```csharp
// In Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
    {
        policy.RequireRole("Admin");
        // Or require specific claims
        policy.RequireClaim("scope", "admin.discovery");
    });
});
```

### Production Best Practices

```csharp
if (builder.Environment.IsProduction())
{
    options.RequireSpatialIndex = true;      // Only fast tables
    options.MaxTables = 50;                   // Safety limit
    options.ComputeExtentOnDiscovery = false; // Avoid slow queries
    options.BackgroundRefresh = true;         // Keep cache fresh
}
else
{
    // Development: more permissive
    options.RequireSpatialIndex = false;
    options.MaxTables = 1000;
}
```

### Rate Limiting

Discovery queries can be expensive. Use caching and rate limiting:

```csharp
// Increase cache duration in production
options.CacheDuration = builder.Environment.IsProduction()
    ? TimeSpan.FromHours(1)
    : TimeSpan.FromMinutes(5);

// Enable background refresh to avoid cache stampede
options.BackgroundRefresh = true;
```

## Admin Endpoints

### GET /admin/discovery/status

Get discovery status and statistics.

```bash
curl http://localhost:5000/admin/discovery/status
```

```json
{
  "enabled": true,
  "odataDiscoveryEnabled": true,
  "ogcDiscoveryEnabled": true,
  "cacheDuration": "00:05:00",
  "maxTables": 100,
  "requireSpatialIndex": true,
  "postGisDataSourceCount": 1,
  "configuredDataSourceId": "main-postgis",
  "backgroundRefreshEnabled": true,
  "backgroundRefreshInterval": "00:05:00"
}
```

### GET /admin/discovery/tables

List all discovered tables.

```bash
curl http://localhost:5000/admin/discovery/tables
```

```json
{
  "tables": [
    {
      "schema": "public",
      "tableName": "roads",
      "qualifiedName": "public.roads",
      "geometryColumn": "geom",
      "srid": 4326,
      "geometryType": "LineString",
      "primaryKeyColumn": "gid",
      "columns": { ... },
      "hasSpatialIndex": true,
      "estimatedRowCount": 50000,
      "extent": {
        "minX": -122.5,
        "minY": 37.7,
        "maxX": -122.4,
        "maxY": 37.8
      }
    }
  ],
  "totalCount": 1,
  "dataSourceId": "main-postgis"
}
```

### GET /admin/discovery/tables/{schema}/{table}

Get details for a specific table.

```bash
curl http://localhost:5000/admin/discovery/tables/public/roads
```

### POST /admin/discovery/refresh

Force refresh of discovery cache.

```bash
curl -X POST http://localhost:5000/admin/discovery/refresh
```

```json
{
  "success": true,
  "message": "Cache refreshed successfully",
  "tablesDiscovered": 25,
  "dataSourceId": "main-postgis"
}
```

### POST /admin/discovery/clear-cache

Clear all discovery caches.

```bash
curl -X POST http://localhost:5000/admin/discovery/clear-cache
```

## Integration with Existing Metadata

Auto-discovery can work **alongside** manually configured services:

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    // Discovered tables go in their own folder
    options.DefaultFolderId = "discovered";
    options.DefaultFolderTitle = "Auto-discovered";

    // Your manually configured services use different folders
    // They all coexist peacefully
});
```

Manually configured services take precedence over auto-discovered ones for the same table.

## Performance Optimization

### Query Builder Pool

Discovery integrates with Honua's query builder pool for fast query generation:

```csharp
// Warm the pool for frequently accessed tables
var provider = app.Services.GetRequiredService<PostgresDataStoreProvider>();
provider.WarmQueryBuilderCache(service, layer, storageSrid: 4326, targetSrid: 3857, count: 10);
```

### Connection Pooling

Uses Npgsql connection pooling automatically. Configure in connection string:

```
Host=localhost;Database=gis;Username=postgres;Pooling=true;MinPoolSize=5;MaxPoolSize=20;
```

### Spatial Indexes

For best performance, ensure all geometry columns have spatial indexes:

```sql
-- Create spatial index
CREATE INDEX idx_roads_geom ON public.roads USING GIST (geom);

-- Analyze for statistics
ANALYZE public.roads;
```

Then enable the requirement:

```csharp
options.RequireSpatialIndex = true;  // Only expose indexed tables
```

## Troubleshooting

### No Tables Discovered

**Problem**: `/admin/discovery/tables` returns empty list.

**Solutions**:
1. Check data source is configured and is PostGIS
2. Verify database user has `SELECT` permissions
3. Check tables have geometry columns registered in `geometry_columns`
4. Review exclusion patterns
5. Check `RequireSpatialIndex` setting

```sql
-- Verify geometry_columns registration
SELECT * FROM geometry_columns WHERE f_table_schema = 'public';

-- If missing, register manually
SELECT Populate_Geometry_Columns('public.my_table'::regclass);
```

### Slow Discovery

**Problem**: Discovery takes too long.

**Solutions**:
1. Disable extent computation: `options.ComputeExtentOnDiscovery = false`
2. Increase cache duration: `options.CacheDuration = TimeSpan.FromHours(1)`
3. Enable background refresh: `options.BackgroundRefresh = true`
4. Create spatial indexes on all tables
5. Run `ANALYZE` on tables

### Too Many Tables

**Problem**: Hundreds of tables discovered, overwhelming the API.

**Solutions**:
1. Use exclusion patterns: `options.ExcludeTablePatterns`
2. Set max tables limit: `options.MaxTables = 50`
3. Require spatial indexes: `options.RequireSpatialIndex = true`
4. Use database permissions to hide tables

## Examples

### Example 1: Development Setup

Zero configuration, expose everything:

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "dev-postgis";
});
```

### Example 2: Production Setup

Strict controls for performance and security:

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "prod-postgis";
    options.RequireSpatialIndex = true;
    options.MaxTables = 50;
    options.CacheDuration = TimeSpan.FromHours(1);
    options.BackgroundRefresh = true;
    options.ExcludeSchemas = new[] { "topology", "tiger", "private" };
    options.ExcludeTablePatterns = new[] { "temp_*", "staging_*", "_*" };
});
```

### Example 3: Selective Exposure

Only expose specific schemas:

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "postgis";
    // Exclude all schemas except 'public' and 'gis'
    options.ExcludeSchemas = new[]
    {
        "pg_catalog",
        "information_schema",
        "topology",
        "tiger"
        // Don't list 'public' or 'gis' here
    };
});
```

Then use database permissions to control table visibility within allowed schemas.

### Example 4: Custom Folder Organization

```csharp
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DefaultFolderId = "infrastructure";
    options.DefaultFolderTitle = "Infrastructure Data";
    options.UseFriendlyNames = true;
});
```

## Comparison with Manual Configuration

| Aspect | Auto-Discovery | Manual Configuration |
|--------|----------------|---------------------|
| Setup Time | Instant | Hours/Days |
| Maintenance | Zero | Manual updates needed |
| Flexibility | Limited | Full control |
| Performance | Good with caching | Optimized |
| Best For | Rapid prototyping, simple datasets | Production, complex requirements |

## Recommended Approach

**Start with Auto-Discovery**, then selectively override with manual configuration:

1. Use auto-discovery to get up and running quickly
2. Identify tables that need custom configuration
3. Create manual layer definitions for those tables
4. Let auto-discovery handle the rest

Manual configurations take precedence, so you get best of both worlds.

## Limitations

- **PostGIS Only**: Currently only supports PostGIS databases
- **Simple Schemas**: Works best with straightforward table structures
- **No Relationships**: Doesn't auto-discover table relationships
- **Limited Styling**: No automatic style generation
- **No Temporal**: Temporal fields not auto-detected

For these advanced scenarios, use manual configuration.

## Future Enhancements

Planned improvements:

- **Multi-database support**: MySQL Spatial, SQL Server Spatial
- **Relationship detection**: Auto-discover foreign keys and relationships
- **Temporal auto-detection**: Detect date/timestamp columns for temporal queries
- **Style generation**: Basic style generation from geometry type
- **View support**: Expose PostGIS views as read-only collections
- **Function support**: Expose PostGIS functions as stored queries

## Related Documentation

- [Configuration Guide](../CONFIGURATION.md)
- [OData Documentation](../protocols/ODATA.md)
- [OGC API Features](../protocols/OGC_API_FEATURES.md)
- [Performance Tuning](../operations/PERFORMANCE.md)
- [Security Best Practices](../operations/SECURITY.md)

## Support

For issues or questions about auto-discovery:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Review [GitHub Issues](https://github.com/honua-io/honua/issues)
3. Ask on [GitHub Discussions](https://github.com/honua-io/honua/discussions)
