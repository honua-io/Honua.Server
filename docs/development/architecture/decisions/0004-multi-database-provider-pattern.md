# 4. Multi-Database Provider Pattern

Date: 2025-10-17

Status: Accepted

## Context

While PostgreSQL/PostGIS is the recommended database (ADR-0002), Honua needs to support multiple database backends to accommodate different deployment scenarios:

- **Development**: Lightweight SQLite for local development without Docker
- **Small Deployments**: SQLite for embedded or single-server scenarios
- **Enterprise Windows**: SQL Server for organizations with existing SQL Server infrastructure
- **Production**: PostgreSQL/PostGIS for full-featured spatial deployments
- **Testing**: Multiple backends for compatibility validation

Directly coupling the application to a single database would limit deployment flexibility and market reach.

**Existing Codebase Evidence:**
- Provider abstraction: `/src/Honua.Server.Core/Data/IDataStoreProvider.cs`
- Factory pattern: `/src/Honua.Server.Core/Data/DataStoreProviderFactory.cs`
- Concrete providers:
  - PostgreSQL: `/src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs`
  - SQLite: `/src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs`
  - SQL Server: `/src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs`
  - MySQL: `/src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs`

## Decision

We will implement a **provider pattern** for database abstraction, allowing Honua to support multiple database backends through a common interface.

**Architecture:**
```
┌──────────────────────────────────────┐
│   Application Layer                  │
│   (OGC API, WFS, STAC endpoints)     │
└────────────┬─────────────────────────┘
             │
             ▼
┌──────────────────────────────────────┐
│   IDataStoreProvider Interface       │
│   - QueryAsync()                     │
│   - CountAsync()                     │
│   - CreateAsync()                    │
│   - UpdateAsync()                    │
│   - DeleteAsync()                    │
│   - GenerateMvtTileAsync()           │
└────────────┬─────────────────────────┘
             │
      ┌──────┴──────┬──────────┬──────────┐
      ▼             ▼          ▼          ▼
┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│PostgreSQL│  │  SQLite  │  │SQL Server│  │  MySQL   │
│Provider  │  │ Provider │  │ Provider │  │ Provider │
└──────────┘  └──────────┘  └──────────┘  └──────────┘
```

**Key Design Principles:**
- **Interface-based abstraction**: `IDataStoreProvider` defines contract
- **Capability discovery**: `IDataStoreCapabilities` advertises provider-specific features
- **Factory pattern**: `DataStoreProviderFactory` selects provider based on configuration
- **Minimal lowest-common-denominator**: Interface targets richest provider (PostgreSQL)
- **Graceful degradation**: Providers return null for unsupported features
- **NetTopologySuite**: Common geometry representation across providers

**Provider Selection:**
```json
{
  "services": [
    {
      "id": "my-service",
      "provider": "postgres",  // "postgres", "sqlite", "sqlserver", "mysql"
      "connectionString": "Host=localhost;Database=gis;..."
    }
  ]
}
```

## Consequences

### Positive

- **Deployment Flexibility**: Choose database based on requirements, not technical constraints
- **Development Experience**: SQLite for local development without infrastructure
- **Market Reach**: Support enterprise SQL Server shops
- **Migration Path**: Start with SQLite, migrate to PostgreSQL as needs grow
- **Testing**: Validate compatibility across multiple databases
- **Future-Proof**: Can add new providers (e.g., Oracle Spatial) without core changes

### Negative

- **Maintenance Burden**: Must maintain multiple provider implementations
- **Testing Complexity**: Each provider needs comprehensive test coverage
- **Feature Fragmentation**: Not all features available on all providers
- **Performance Variation**: Different providers have different performance characteristics
- **Documentation**: Must document capabilities and limitations per provider
- **Lowest Common Denominator Risk**: May limit innovation to avoid breaking providers

### Neutral

- Provider-specific optimizations require conditional logic
- Must balance abstraction with provider-specific features
- Connection string formats vary by provider

## Alternatives Considered

### 1. PostgreSQL-Only (Single Database)

Support only PostgreSQL/PostGIS, no abstraction.

**Pros:**
- Simplest implementation (no abstraction overhead)
- Best performance (can use PostgreSQL-specific features)
- Smaller codebase
- Easier testing (one database)
- Clear documentation (one path)

**Cons:**
- **Limits deployment flexibility**
- No local development without Docker
- Excludes SQL Server shops
- Higher barrier to entry

**Verdict:** Rejected - too limiting for diverse user base

### 2. ORM-based Abstraction (Entity Framework Core)

Use EF Core for database abstraction with spatial extensions.

**Pros:**
- Well-established ORM
- Strong .NET ecosystem integration
- Migrations built-in
- LINQ query syntax

**Cons:**
- **Cannot express complex spatial queries** (ST_AsMVT, spatial functions)
- Performance overhead for spatial operations
- Spatial support varies by provider
- ORM impedance mismatch for GIS workloads
- Limited control over SQL generation

**Verdict:** Rejected - insufficient for spatial SQL requirements

### 3. Dapper-based with Provider-Specific SQL

Use Dapper micro-ORM with SQL files per provider.

**Pros:**
- Lightweight
- Full SQL control
- Good performance

**Cons:**
- **Massive SQL duplication** (same query × 4 providers)
- SQL dialect differences hard to manage
- Maintenance nightmare
- Difficult to test all variations

**Verdict:** Rejected - unsustainable maintenance burden

### 4. SQLite-Only (Embedded Database)

Support only SQLite/SpatiaLite for maximum simplicity.

**Pros:**
- Zero configuration
- File-based (easy deployment)
- Embedded (no server)

**Cons:**
- **No MVT generation** (major limitation)
- Poor concurrent write performance
- Limited spatial functions
- No horizontal scaling
- Not suitable for production

**Verdict:** Rejected - insufficient for production workloads

### 5. Cloud-Only Abstraction (AWS/Azure/GCP SDKs)

Abstract cloud-specific spatial services instead of databases.

**Pros:**
- Serverless options
- Managed infrastructure
- Auto-scaling

**Cons:**
- Vendor lock-in
- Ongoing costs
- Limited to cloud deployments
- Different APIs per provider
- Not suitable for on-premises

**Verdict:** Rejected - too constraining, not portable

## Implementation Details

### Interface Definition
```csharp
// /src/Honua.Server.Core/Data/IDataStoreProvider.cs
public interface IDataStoreProvider
{
    string Provider { get; }
    IDataStoreCapabilities Capabilities { get; }

    IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default);

    // MVT generation (PostGIS-specific, others return null)
    Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom, int x, int y,
        string? datetime = null,
        CancellationToken cancellationToken = default);
}
```

### Capability Discovery
```csharp
public interface IDataStoreCapabilities
{
    bool SupportsMvtGeneration { get; }
    bool SupportsTransactions { get; }
    bool SupportsConcurrentWrites { get; }
    bool SupportsStreamingResults { get; }
    int MaxFeatureCount { get; }
}
```

### Factory Pattern
```csharp
// /src/Honua.Server.Core/Data/DataStoreProviderFactory.cs
public class DataStoreProviderFactory : IDataStoreProviderFactory
{
    public IDataStoreProvider CreateProvider(ServiceDefinition service)
    {
        return service.Provider.ToLowerInvariant() switch
        {
            "postgres" => new PostgresDataStoreProvider(service.ConnectionString),
            "sqlite" => new SqliteDataStoreProvider(service.ConnectionString),
            "sqlserver" => new SqlServerDataStoreProvider(service.ConnectionString),
            "mysql" => new MySqlDataStoreProvider(service.ConnectionString),
            _ => throw new NotSupportedException($"Provider '{service.Provider}' not supported")
        };
    }
}
```

### Provider Capabilities Comparison

| Capability | PostgreSQL | SQLite | SQL Server | MySQL |
|------------|-----------|--------|------------|-------|
| Native MVT | ✅ Yes | ❌ No | ❌ No | ❌ No |
| Spatial Index | ✅ GIST | ✅ R-Tree | ✅ Spatial | ✅ Spatial |
| Transactions | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Concurrent Writes | ✅ Excellent | ⚠️ Limited | ✅ Good | ✅ Good |
| Streaming | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| Spatial Functions | ✅ 400+ | ⚠️ 50+ | ⚠️ 50+ | ⚠️ 40+ |

### NetTopologySuite Integration

All providers use NetTopologySuite (NTS) for geometry representation:
- **PostgreSQL**: `Npgsql.NetTopologySuite` extension
- **SQLite**: Manual WKB conversion to NTS
- **SQL Server**: System.Data.SqlTypes.SqlGeometry → NTS
- **MySQL**: WKB conversion to NTS

This ensures consistent geometry handling across providers.

## Documented Limitations

**SQLite:**
- No native MVT generation (built in-memory by application)
- Limited concurrent writes (WAL mode helps)
- Fewer spatial functions than PostGIS

**SQL Server:**
- No native MVT generation
- Geometry vs Geography type confusion
- More expensive licensing

**MySQL:**
- No native MVT generation
- Weaker spatial function library
- Spatial indexing less mature

**All documented in**: `/docs/configuration/README.md#database-providers`

## Testing Strategy

Each provider has dedicated integration tests:
- `/tests/Honua.Server.Core.Tests/Data/PostgresDataStoreProviderTests.cs`
- `/tests/Honua.Server.Core.Tests/Data/SqliteDataStoreProviderTests.cs`
- `/tests/Honua.Server.Core.Tests/Data/SqlServerDataStoreProviderTests.cs`

Tests validate:
- CRUD operations
- Spatial queries (bbox, intersects)
- MVT generation (if supported)
- Transaction semantics
- Error handling

## Migration Path

Users can migrate between providers:
1. Export data from source provider (GeoPackage, GeoJSON)
2. Import to target provider using standard tools
3. Update service configuration (provider + connection string)
4. Restart application

Future: Provide `honua migrate` CLI command for automated migration.

## References

- Provider Implementations: `/src/Honua.Server.Core/Data/`
- Factory Pattern: GoF Design Patterns
- NetTopologySuite: https://nettopologysuite.github.io/

## Notes

This abstraction prioritizes **deployment flexibility** over **maximum performance**. PostgreSQL remains the recommended choice (ADR-0002), but users can choose based on their constraints.

The abstraction has proven valuable for supporting diverse deployment scenarios: from local development (SQLite) to enterprise (SQL Server) to production (PostgreSQL).
