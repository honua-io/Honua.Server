# Auto-Discovery for PostGIS Tables

This module provides **zero-configuration** auto-discovery of PostGIS tables, enabling instant exposure via OData and OGC API Features.

## Quick Start

```csharp
// In Program.cs
builder.Services.AddHonuaAutoDiscovery(options =>
{
    options.DataSourceId = "my-postgis-db";
});

// In endpoint configuration
app.MapDiscoveryAdminEndpoints();
```

That's it! All geometry tables are now available at:
- `/odata` - OData collections
- `/collections` - OGC API Features collections
- `/admin/discovery/status` - Discovery admin panel

## Architecture

### Core Components

1. **ITableDiscoveryService** - Main discovery interface
   - `PostGisTableDiscoveryService` - PostGIS implementation
   - `CachedTableDiscoveryService` - Caching decorator

2. **AutoDiscoveryOptions** - Configuration
   - Controls what gets discovered
   - Exclusion patterns
   - Performance settings

3. **Integration Providers**
   - `DynamicODataModelProvider` - OData EDM model generation
   - `DynamicOgcCollectionProvider` - OGC collection generation

4. **Admin Endpoints** - Management and monitoring
   - View discovered tables
   - Refresh cache
   - Get statistics

### Data Flow

```
PostGIS Database
      ↓
[Query geometry_columns]
      ↓
[Apply filters & exclusions]
      ↓
[Use PostgresSchemaDiscoveryService]
      ↓
[Build DiscoveredTable metadata]
      ↓
[Cache results]
      ↓
[Generate OData/OGC definitions]
      ↓
API Endpoints
```

## Features

- ✅ Zero-configuration table exposure
- ✅ Automatic schema introspection
- ✅ Friendly name generation
- ✅ Spatial index detection
- ✅ Row count estimation
- ✅ Extent computation (optional)
- ✅ Configurable caching
- ✅ Background cache refresh
- ✅ Admin endpoints
- ✅ Security controls

## Configuration Examples

### Development
```csharp
options.Enabled = true;
options.RequireSpatialIndex = false;  // Allow all tables
options.MaxTables = 1000;
options.CacheDuration = TimeSpan.FromMinutes(1);
```

### Production
```csharp
options.Enabled = true;
options.RequireSpatialIndex = true;   // Only indexed tables
options.MaxTables = 50;                // Safety limit
options.CacheDuration = TimeSpan.FromHours(1);
options.BackgroundRefresh = true;      // Keep cache fresh
options.ExcludeSchemas = new[] { "topology", "private" };
```

## Integration Points

### With Existing Metadata
Auto-discovery works alongside manually configured services. Manual configurations take precedence.

### With OData
Generates EDM models compatible with the existing `DynamicEdmModelBuilder`.

### With OGC API
Generates collections compatible with existing OGC handlers.

## Performance

- **Discovery queries**: Cached for configured duration (default 5 minutes)
- **Background refresh**: Optional automatic cache refresh
- **Spatial indexes**: Optionally require indexes for discovered tables
- **Connection pooling**: Uses Npgsql connection pooling

## Security

- Respects database permissions
- Admin endpoints require authorization
- Exclusion patterns prevent exposure
- Table count limits prevent abuse
- Safe query generation (no SQL injection)

## Limitations

- PostGIS only (currently)
- Simple table structures
- No relationship auto-discovery
- No temporal auto-detection
- No style generation

For advanced scenarios, use manual configuration.

## Testing

Unit tests: `PostGisTableDiscoveryServiceTests.cs`
Integration tests: Require real PostGIS database

## Related Documentation

- [Full Documentation](../../../../docs/features/AUTO_DISCOVERY.md)
- [Configuration Guide](../../../../docs/CONFIGURATION.md)
- [OData Integration](../../../Honua.Server.Host/OData/README.md)
- [OGC Integration](../../../Honua.Server.Host/Ogc/README.md)
