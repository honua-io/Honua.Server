# Data Module

## Purpose

The Data module is the core database abstraction layer for Honua Server, providing a unified interface to interact with 12+ different database providers. It enables seamless spatial data operations across relational databases (PostgreSQL, MySQL, SQL Server, SQLite), NoSQL databases (MongoDB, Cosmos DB), analytics platforms (BigQuery, Snowflake, Redshift), and search engines (Elasticsearch).

**Key capabilities:**
- **Provider-agnostic API**: Write once, run on any supported database
- **Native spatial support**: Leverages database-native geometry types and spatial indexes
- **Advanced spatial operations**: Intersections, buffering, CRS transformations, MVT tile generation
- **Optimistic concurrency**: Built-in versioning for concurrent updates
- **Soft delete support**: Mark records as deleted without permanent removal
- **Bulk operations**: Efficient batch inserts, updates, and deletes
- **Connection pooling**: Automatic pool management with warmup and metrics
- **Security**: Encrypted connection strings, SQL injection prevention, retry policies

---

## Architecture

### High-Level Design

The Data module uses the **Provider Pattern** to abstract database-specific implementations behind a common interface (`IDataStoreProvider`). This allows the application to work with any database without changing business logic.

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│         (Services, Controllers, Business Logic)             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              IDataStoreProvider Interface                   │
│  (Query, Create, Update, Delete, Bulk Ops, Transactions)   │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┴───────────────┬─────────────────┐
        ▼                              ▼                 ▼
┌───────────────┐            ┌──────────────┐   ┌─────────────┐
│ Core Providers│            │  Enterprise  │   │  In-Memory  │
│ - PostgreSQL  │            │  Providers   │   │   Stores    │
│ - MySQL       │            │ - BigQuery   │   │             │
│ - SQL Server  │            │ - MongoDB    │   │             │
│ - SQLite      │            │ - Oracle     │   │             │
│               │            │ - Cosmos DB  │   │             │
│               │            │ - Redshift   │   │             │
│               │            │ - Snowflake  │   │             │
│               │            │ - Elastic    │   │             │
└───────────────┘            └──────────────┘   └─────────────┘
```

### Key Design Patterns

#### 1. Provider Pattern
Each database has a dedicated provider implementing `IDataStoreProvider`:

```csharp
public interface IDataStoreProvider
{
    string Provider { get; }
    IDataStoreCapabilities Capabilities { get; }

    // Query operations
    IAsyncEnumerable<FeatureRecord> QueryAsync(...);
    Task<long> CountAsync(...);
    Task<FeatureRecord?> GetAsync(...);

    // CRUD operations
    Task<FeatureRecord> CreateAsync(...);
    Task<FeatureRecord?> UpdateAsync(...);
    Task<bool> DeleteAsync(...);

    // Soft delete operations
    Task<bool> SoftDeleteAsync(...);
    Task<bool> RestoreAsync(...);
    Task<bool> HardDeleteAsync(...);

    // Bulk operations
    Task<int> BulkInsertAsync(...);
    Task<int> BulkUpdateAsync(...);
    Task<int> BulkDeleteAsync(...);

    // Advanced operations
    Task<byte[]?> GenerateMvtTileAsync(...);
    Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(...);
    Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(...);
    Task<BoundingBox?> QueryExtentAsync(...);

    // Transaction support
    Task<IDataStoreTransaction?> BeginTransactionAsync(...);
    Task TestConnectivityAsync(...);
}
```

#### 2. Capabilities Pattern
Each provider declares its capabilities to enable feature detection:

```csharp
public interface IDataStoreCapabilities
{
    bool SupportsNativeGeometry { get; }           // Native geometry types
    bool SupportsNativeMvt { get; }                // Vector tile generation
    bool SupportsTransactions { get; }             // Transaction support
    bool SupportsSpatialIndexes { get; }           // Spatial indexing
    bool SupportsServerSideGeometryOperations { get; } // Spatial functions
    bool SupportsCrsTransformations { get; }       // CRS conversion
    int MaxQueryParameters { get; }                // Parameter limit
    bool SupportsReturningClause { get; }          // RETURNING support
    bool SupportsBulkOperations { get; }           // Bulk operations
    bool SupportsSoftDelete { get; }               // Soft delete support
}
```

#### 3. Base Class Pattern
`RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>` eliminates ~3,500 lines of duplicated code by providing:
- Connection management with automatic disposal
- Transaction lifecycle management
- Connection string encryption/decryption with caching
- Retry pipeline integration (Polly)
- Security validation (SQL injection prevention)

#### 4. Query Builder Pattern
Each provider has a specialized query builder that generates database-specific SQL:
- `PostgresFeatureQueryBuilder` - PostgreSQL/PostGIS
- `MySqlFeatureQueryBuilder` - MySQL with spatial extensions
- `SqlServerFeatureQueryBuilder` - SQL Server spatial
- `SqliteFeatureQueryBuilder` - SQLite with SpatiaLite

#### 5. CRS Transformation Integration
The module integrates with `ICrsTransformProvider` to handle coordinate transformations:
- **PostgreSQL**: Native `ST_Transform()` function
- **MySQL**: Native `ST_Transform()` function (8.0+)
- **Others**: Client-side transformation using ProjNET

#### 6. Connection Pooling
Advanced connection pool management with:
- **Connection pooling**: Configurable min/max pool sizes per provider
- **Pool warmup**: `ConnectionPoolWarmupService` pre-establishes connections on startup
- **Metrics**: `PostgresConnectionPoolMetrics` tracks pool health
- **Prepared statement caching**: `PreparedStatementCache` reduces query planning overhead

---

## Supported Databases

### Core Providers (Open Source)

| Provider | Database | Key | Spatial Extension | Performance |
|----------|----------|-----|-------------------|-------------|
| **PostgreSQL** | PostgreSQL 12+ | `postgis` | PostGIS 3.0+ | **Excellent** |
| **MySQL** | MySQL 8.0+ | `mysql` | MySQL Spatial | **Good** |
| **SQL Server** | SQL Server 2019+ | `sqlserver` | Built-in Spatial | **Good** |
| **SQLite** | SQLite 3.35+ | `sqlite` | SpatiaLite 5.0+ | **Good** (single-user) |

### Enterprise Providers (Commercial)

| Provider | Database | Key | Spatial Support | Best For |
|----------|----------|-----|-----------------|----------|
| **BigQuery** | Google BigQuery | `bigquery` | GEOGRAPHY type | Analytics, massive datasets |
| **Cosmos DB** | Azure Cosmos DB | `cosmosdb` | GeoJSON | Global distribution, low latency |
| **MongoDB** | MongoDB 5.0+ | `mongodb` | GeoJSON, 2dsphere | Document-oriented, flexible schema |
| **Elasticsearch** | Elasticsearch 8.0+ | `elasticsearch` | geo_point, geo_shape | Full-text search + geo |
| **Oracle** | Oracle 19c+ | `oracle` | SDO_GEOMETRY | Enterprise, government |
| **Redshift** | AWS Redshift | `redshift` | PostGIS-compatible | Data warehousing, OLAP |
| **Snowflake** | Snowflake | `snowflake` | GEOGRAPHY/GEOMETRY | Cloud data warehouse |

---

## Provider Feature Matrix

| Feature | PostgreSQL | MySQL | SQL Server | SQLite | MongoDB | Oracle | Elasticsearch | BigQuery | Cosmos DB | Redshift | Snowflake |
|---------|------------|-------|------------|--------|---------|--------|---------------|----------|-----------|----------|-----------|
| Native Geometry | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Native MVT Tiles | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Transactions | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅* | ✅ | ✅ |
| Spatial Indexes | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| Server-Side Geo Ops | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| CRS Transformations | ✅ | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Max Parameters | 32,767 | 65,535 | 2,100 | 32,766 | 10,000 | 32,767 | 10,000 | 1,000 | 2,000 | 32,767 | 16,384 |
| RETURNING Clause | ✅ | ❌ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ |
| Bulk Operations | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Soft Delete | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

*Cosmos DB supports transactions within a single partition only.

### Recommendations by Use Case

| Use Case | Recommended Provider | Reason |
|----------|---------------------|---------|
| **General GIS** | PostgreSQL (PostGIS) | Best spatial support, open source, excellent performance |
| **High-throughput OLTP** | PostgreSQL, MySQL | Mature, battle-tested, great tooling |
| **Cloud-native** | Cosmos DB, MongoDB | Managed services, global distribution |
| **Analytics/OLAP** | BigQuery, Snowflake, Redshift | Columnar storage, massive scale |
| **Full-text + Geo** | Elasticsearch | Combined text search and spatial queries |
| **Enterprise/Government** | Oracle, SQL Server | Compliance, support contracts, ecosystem |
| **Development/Testing** | SQLite | Zero configuration, file-based |

---

## Usage Examples

### Registering Providers

**Core providers (automatic registration):**

```csharp
// In Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Core providers registered automatically
builder.Services.AddHonuaCore(builder.Configuration);

// This registers:
// - PostgresDataStoreProvider (key: "postgis")
// - MySqlDataStoreProvider (key: "mysql")
// - SqlServerDataStoreProvider (key: "sqlserver")
// - SqliteDataStoreProvider (key: "sqlite")
```

**Enterprise providers (opt-in):**

```csharp
// In Program.cs for enterprise features
builder.Services.AddHonuaEnterprise();

// This adds:
// - BigQueryDataStoreProvider (key: "bigquery")
// - CosmosDbDataStoreProvider (key: "cosmosdb")
// - MongoDbDataStoreProvider (key: "mongodb")
// - ElasticsearchDataStoreProvider (key: "elasticsearch")
// - OracleDataStoreProvider (key: "oracle")
// - RedshiftDataStoreProvider (key: "redshift")
// - SnowflakeDataStoreProvider (key: "snowflake")
```

### Querying Data

```csharp
// Inject factory
public class FeatureService
{
    private readonly IDataStoreProviderFactory _providerFactory;

    public FeatureService(IDataStoreProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task<List<FeatureRecord>> GetFeaturesAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        BoundingBox bbox,
        CancellationToken cancellationToken)
    {
        // Create provider based on data source configuration
        var provider = _providerFactory.Create(dataSource.Provider);

        // Build query
        var query = new FeatureQuery(
            Limit: 1000,
            Bbox: bbox,
            PropertyNames: new[] { "name", "category", "population" },
            SortOrders: new[] { new FeatureSortOrder("name", FeatureSortDirection.Ascending) },
            Crs: "EPSG:4326"
        );

        // Execute query (streaming)
        var features = new List<FeatureRecord>();
        await foreach (var feature in provider.QueryAsync(dataSource, service, layer, query, cancellationToken))
        {
            features.Add(feature);
        }

        return features;
    }
}
```

### Creating Features

```csharp
public async Task<FeatureRecord> CreateCityAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    string name,
    Point location,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    var record = new FeatureRecord(
        Attributes: new Dictionary<string, object?>
        {
            ["name"] = name,
            ["population"] = 100000,
            ["geometry"] = location, // NetTopologySuite Point
            ["created_at"] = DateTimeOffset.UtcNow
        }
    );

    var created = await provider.CreateAsync(
        dataSource,
        service,
        layer,
        record,
        transaction: null,
        cancellationToken);

    // Returns record with generated ID and version
    return created;
}
```

### Bulk Operations

```csharp
public async Task<int> ImportCitiesAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    IAsyncEnumerable<City> cities,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    // Check if provider supports bulk operations
    if (!provider.Capabilities.SupportsBulkOperations)
    {
        throw new NotSupportedException($"Provider '{provider.Provider}' does not support bulk operations");
    }

    // Convert to feature records
    var records = cities.Select(city => new FeatureRecord(
        Attributes: new Dictionary<string, object?>
        {
            ["name"] = city.Name,
            ["population"] = city.Population,
            ["geometry"] = city.Location
        }
    ));

    // Bulk insert (optimized for performance)
    var count = await provider.BulkInsertAsync(
        dataSource,
        service,
        layer,
        records,
        cancellationToken);

    return count;
}
```

### Transaction Handling

```csharp
public async Task TransferPopulationAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    string fromCityId,
    string toCityId,
    int amount,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    // Begin transaction
    await using var transaction = await provider.BeginTransactionAsync(dataSource, cancellationToken);

    if (transaction == null)
    {
        throw new NotSupportedException($"Provider '{provider.Provider}' does not support transactions");
    }

    try
    {
        // Get source city
        var fromCity = await provider.GetAsync(dataSource, service, layer, fromCityId, null, cancellationToken);
        if (fromCity == null)
            throw new InvalidOperationException("Source city not found");

        // Get destination city
        var toCity = await provider.GetAsync(dataSource, service, layer, toCityId, null, cancellationToken);
        if (toCity == null)
            throw new InvalidOperationException("Destination city not found");

        // Update populations
        var fromPopulation = (int)fromCity.Attributes["population"]! - amount;
        var toPopulation = (int)toCity.Attributes["population"]! + amount;

        var fromUpdated = new FeatureRecord(
            Attributes: new Dictionary<string, object?>(fromCity.Attributes) { ["population"] = fromPopulation },
            Version: fromCity.Version
        );

        var toUpdated = new FeatureRecord(
            Attributes: new Dictionary<string, object?>(toCity.Attributes) { ["population"] = toPopulation },
            Version: toCity.Version
        );

        // Execute updates within transaction
        await provider.UpdateAsync(dataSource, service, layer, fromCityId, fromUpdated, transaction, cancellationToken);
        await provider.UpdateAsync(dataSource, service, layer, toCityId, toUpdated, transaction, cancellationToken);

        // Commit transaction
        await transaction.CommitAsync(cancellationToken);
    }
    catch
    {
        // Rollback on error
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
```

### Error Handling

```csharp
public async Task<FeatureRecord?> SafeUpdateAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    string featureId,
    FeatureRecord record,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    try
    {
        return await provider.UpdateAsync(
            dataSource,
            service,
            layer,
            featureId,
            record,
            transaction: null,
            cancellationToken);
    }
    catch (ConcurrencyException ex)
    {
        // Optimistic concurrency conflict - record was modified by another user
        _logger.LogWarning(ex, "Concurrency conflict updating feature {FeatureId}", featureId);
        return null; // Caller should retry or notify user
    }
    catch (SqlException ex) when (ex.Number == 2627) // SQL Server: Unique constraint violation
    {
        _logger.LogError(ex, "Duplicate key error updating feature {FeatureId}", featureId);
        throw new InvalidOperationException("Feature with this key already exists", ex);
    }
    catch (DbException ex)
    {
        // Database-specific error
        _logger.LogError(ex, "Database error updating feature {FeatureId}", featureId);
        throw;
    }
}
```

### Soft Delete Operations

```csharp
public async Task<bool> SoftDeleteCityAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    string cityId,
    string deletedBy,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    // Check if provider supports soft delete
    if (!provider.Capabilities.SupportsSoftDelete)
    {
        throw new NotSupportedException($"Provider '{provider.Provider}' does not support soft delete");
    }

    // Soft delete (marks as deleted, preserves data)
    var deleted = await provider.SoftDeleteAsync(
        dataSource,
        service,
        layer,
        cityId,
        deletedBy,
        transaction: null,
        cancellationToken);

    return deleted;
}

public async Task<bool> RestoreCityAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    string cityId,
    CancellationToken cancellationToken)
{
    var provider = _providerFactory.Create(dataSource.Provider);

    // Restore soft-deleted record
    var restored = await provider.RestoreAsync(
        dataSource,
        service,
        layer,
        cityId,
        transaction: null,
        cancellationToken);

    return restored;
}
```

---

## Configuration

### Connection Strings

#### PostgreSQL

```json
{
  "DataSources": [
    {
      "Id": "my-postgres-db",
      "Provider": "postgis",
      "ConnectionString": "Host=localhost;Port=5432;Database=geodata;Username=postgres;Password=secret;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=50"
    }
  ]
}
```

**Key parameters:**
- `Host`: Database server hostname
- `Port`: Database port (default: 5432)
- `Database`: Database name
- `Username` / `Password`: Credentials
- `Pooling`: Enable connection pooling (default: true)
- `Minimum Pool Size`: Min connections (default: 2)
- `Maximum Pool Size`: Max connections (default: 50)
- `Timeout`: Connection timeout in seconds (default: 15)
- `Command Timeout`: Query timeout in seconds (default: 30)

#### MySQL

```json
{
  "DataSources": [
    {
      "Id": "my-mysql-db",
      "Provider": "mysql",
      "ConnectionString": "Server=localhost;Port=3306;Database=geodata;Uid=root;Pwd=secret;Pooling=true;Min Pool Size=2;Max Pool Size=50"
    }
  ]
}
```

**Key parameters:**
- `Server`: Database server hostname
- `Port`: Database port (default: 3306)
- `Database`: Database name
- `Uid` / `Pwd`: Credentials
- `Pooling`: Enable connection pooling (default: true)
- `Min Pool Size`: Min connections (default: 2)
- `Max Pool Size`: Max connections (default: 50)
- `Connection Timeout`: Connection timeout in seconds (default: 15)
- `Default Command Timeout`: Query timeout in seconds (default: 30)

#### SQL Server

```json
{
  "DataSources": [
    {
      "Id": "my-sqlserver-db",
      "Provider": "sqlserver",
      "ConnectionString": "Server=localhost,1433;Database=geodata;User Id=sa;Password=secret;TrustServerCertificate=true;Pooling=true;Min Pool Size=2;Max Pool Size=50"
    }
  ]
}
```

**Key parameters:**
- `Server`: Server hostname and optional port (format: `hostname,port`)
- `Database`: Database name
- `User Id` / `Password`: Credentials (or use `Integrated Security=true` for Windows auth)
- `TrustServerCertificate`: Accept self-signed certificates (development only)
- `Pooling`: Enable connection pooling (default: true)
- `Min Pool Size`: Min connections (default: 2)
- `Max Pool Size`: Max connections (default: 50)
- `Connect Timeout`: Connection timeout in seconds (default: 15)
- `Command Timeout`: Query timeout in seconds (default: 30)

#### SQLite

```json
{
  "DataSources": [
    {
      "Id": "my-sqlite-db",
      "Provider": "sqlite",
      "ConnectionString": "Data Source=/path/to/geodata.db;Mode=ReadWriteCreate;Cache=Shared;Foreign Keys=True"
    }
  ]
}
```

**Key parameters:**
- `Data Source`: Database file path
- `Mode`: Access mode (`ReadOnly`, `ReadWrite`, `ReadWriteCreate`)
- `Cache`: Cache mode (`Shared`, `Private`) - use `Shared` for multi-threading
- `Foreign Keys`: Enable foreign key constraints (default: false)
- `Journal Mode`: WAL mode for better concurrency (`WAL`)
- `Default Timeout`: Busy timeout in seconds (default: 30)

#### MongoDB

```json
{
  "DataSources": [
    {
      "Id": "my-mongodb",
      "Provider": "mongodb",
      "ConnectionString": "mongodb://username:password@localhost:27017/geodata?authSource=admin"
    }
  ]
}
```

#### Oracle

```json
{
  "DataSources": [
    {
      "Id": "my-oracle-db",
      "Provider": "oracle",
      "ConnectionString": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=geodata)));User Id=system;Password=secret;Pooling=true;Min Pool Size=2;Max Pool Size=50"
    }
  ]
}
```

#### Cosmos DB

```json
{
  "DataSources": [
    {
      "Id": "my-cosmosdb",
      "Provider": "cosmosdb",
      "ConnectionString": "AccountEndpoint=https://myaccount.documents.azure.com:443/;AccountKey=...;Database=geodata"
    }
  ]
}
```

#### BigQuery

```json
{
  "DataSources": [
    {
      "Id": "my-bigquery",
      "Provider": "bigquery",
      "ConnectionString": "ProjectId=my-project;DataSet=geodata;CredentialFile=/path/to/service-account.json"
    }
  ]
}
```

#### Snowflake

```json
{
  "DataSources": [
    {
      "Id": "my-snowflake",
      "Provider": "snowflake",
      "ConnectionString": "account=myaccount;user=myuser;password=secret;db=geodata;schema=public;warehouse=compute_wh"
    }
  ]
}
```

#### Redshift

```json
{
  "DataSources": [
    {
      "Id": "my-redshift",
      "Provider": "redshift",
      "ConnectionString": "Server=myredshift.region.redshift.amazonaws.com;Port=5439;Database=geodata;User ID=admin;Password=secret"
    }
  ]
}
```

#### Elasticsearch

```json
{
  "DataSources": [
    {
      "Id": "my-elasticsearch",
      "Provider": "elasticsearch",
      "ConnectionString": "https://localhost:9200;Username=elastic;Password=secret;CertificateFingerprint=..."
    }
  ]
}
```

### Connection String Encryption

For production deployments, encrypt connection strings:

```csharp
// Configure encryption in appsettings.json
{
  "DataAccess": {
    "EncryptionKeyId": "my-kms-key-id"
  }
}

// Register encryption service
builder.Services.AddSingleton<IConnectionStringEncryptionService, AesConnectionStringEncryptionService>();

// Connection strings are automatically decrypted at runtime
```

### Connection Pool Configuration

```json
{
  "DataAccess": {
    "DefaultCommandTimeoutSeconds": 30,
    "LongRunningQueryTimeoutSeconds": 300,
    "BulkOperationTimeoutSeconds": 600,
    "TransactionTimeoutSeconds": 120,
    "HealthCheckTimeoutSeconds": 5,

    "Postgres": {
      "Pooling": true,
      "MinPoolSize": 2,
      "MaxPoolSize": 50,
      "ConnectionLifetime": 600,
      "Timeout": 15,
      "ApplicationName": "Honua.Server"
    },

    "MySql": {
      "Pooling": true,
      "MinimumPoolSize": 2,
      "MaximumPoolSize": 50,
      "ConnectionLifeTime": 600,
      "ConnectionTimeout": 15,
      "ApplicationName": "Honua.Server"
    },

    "SqlServer": {
      "Pooling": true,
      "MinPoolSize": 2,
      "MaxPoolSize": 50,
      "ConnectionLifetime": 600,
      "ConnectTimeout": 15,
      "ApplicationName": "Honua.Server"
    },

    "Sqlite": {
      "Pooling": true,
      "EnableWalMode": true,
      "DefaultTimeout": 30,
      "CacheMode": "Shared"
    },

    "OptimisticLocking": {
      "Enabled": true,
      "VersionRequirement": "Lenient",
      "VersionColumnName": "row_version",
      "IncludeVersionInResponses": true,
      "MaxRetryAttempts": 0,
      "RetryDelayMilliseconds": 100
    }
  },

  "ConnectionPoolWarmup": {
    "Enabled": true,
    "EnableInDevelopment": false,
    "StartupDelayMs": 1000,
    "MaxConcurrentWarmups": 3,
    "MaxDataSources": 10,
    "TimeoutMs": 5000
  }
}
```

---

## Key Classes and Interfaces

### Core Interfaces

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IDataStoreProvider` | Main provider interface | `IDataStoreProvider.cs` |
| `IDataStoreCapabilities` | Provider capability declaration | `IDataStoreCapabilities.cs` |
| `IDataStoreProviderFactory` | Factory for creating providers | `IDataStoreProviderFactory.cs` |
| `IDataStoreTransaction` | Transaction abstraction | `IDataStoreProvider.cs` |
| `ICrsTransformProvider` | Coordinate transformation | `ICrsTransformProvider.cs` |

### Core Classes

| Class | Purpose | Location |
|-------|---------|----------|
| `RelationalDataStoreProviderBase<T, U, V>` | Base class for relational providers | `RelationalDataStoreProviderBase.cs` |
| `DataStoreProviderFactory` | DI-based provider factory | `DataStoreProviderFactory.cs` |
| `ConnectionPoolWarmupService` | Pool warmup on startup | `ConnectionPoolWarmupService.cs` |
| `DatabaseRetryPolicy` | Transient failure retry logic | `DatabaseRetryPolicy.cs` |
| `PreparedStatementCache` | Statement caching | `PreparedStatementCache.cs` |
| `FeatureRecordReader` | ADO.NET reader abstraction | `FeatureRecordReader.cs` |
| `GeometryReader` | Geometry deserialization | `GeometryReader.cs` |
| `SqlExceptionHelper` | Exception classification | `SqlExceptionHelper.cs` |

### Provider-Specific Classes

| Provider | Key Classes | Location |
|----------|-------------|----------|
| PostgreSQL | `PostgresDataStoreProvider`<br>`PostgresFeatureQueryBuilder`<br>`PostgresConnectionManager`<br>`PostgresVectorTileGenerator` | `Data/Postgres/` |
| MySQL | `MySqlDataStoreProvider`<br>`MySqlFeatureQueryBuilder`<br>`MySqlSpatialFilterTranslator` | `Data/MySql/` |
| SQL Server | `SqlServerDataStoreProvider`<br>`SqlServerFeatureQueryBuilder` | `Data/SqlServer/` |
| SQLite | `SqliteDataStoreProvider`<br>`SqliteFeatureQueryBuilder` | `Data/Sqlite/` |

### Query Builders

| Class | Purpose | Location |
|-------|---------|----------|
| `PostgresFeatureQueryBuilder` | PostgreSQL query generation | `Data/Postgres/PostgresFeatureQueryBuilder.cs` |
| `MySqlFeatureQueryBuilder` | MySQL query generation | `Data/MySql/MySqlFeatureQueryBuilder.cs` |
| `SqlServerFeatureQueryBuilder` | SQL Server query generation | `Data/SqlServer/SqlServerFeatureQueryBuilder.cs` |
| `SqliteFeatureQueryBuilder` | SQLite query generation | `Data/Sqlite/SqliteFeatureQueryBuilder.cs` |

### Query Helpers

| Class | Purpose | Location |
|-------|---------|----------|
| `QueryOptimizationHelper` | Query optimization utilities | `Data/Query/QueryOptimizationHelper.cs` |
| `SqlFilterTranslator` | Filter to SQL conversion | `Data/Query/SqlFilterTranslator.cs` |
| `SpatialFilterTranslator` | Spatial filter translation | `Data/Query/SpatialFilterTranslator.cs` |
| `PaginationHelper` | Pagination logic | `Data/Query/PaginationHelper.cs` |
| `KeysetPaginationHelper` | Keyset pagination (cursor-based) | `Data/Query/KeysetPaginationHelper.cs` |

### Authentication Repositories

| Class | Purpose | Location |
|-------|---------|----------|
| `IAuthRepository` | Auth data interface | `Data/Auth/IAuthRepository.cs` |
| `PostgresAuthRepository` | PostgreSQL auth implementation | `Data/Auth/PostgresAuthRepository.cs` |
| `MySqlAuthRepository` | MySQL auth implementation | `Data/Auth/MySqlAuthRepository.cs` |
| `SqlServerAuthRepository` | SQL Server auth implementation | `Data/Auth/SqlServerAuthRepository.cs` |
| `SqliteAuthRepository` | SQLite auth implementation | `Data/Auth/SqliteAuthRepository.cs` |

---

## Adding New Providers

### Step 1: Implement IDataStoreCapabilities

```csharp
namespace Honua.Server.Enterprise.Data.MyDatabase;

internal sealed class MyDatabaseCapabilities : IDataStoreCapabilities
{
    public static readonly MyDatabaseCapabilities Instance = new();

    private MyDatabaseCapabilities() { }

    public bool SupportsNativeGeometry => true; // Does your DB have geometry types?
    public bool SupportsNativeMvt => false; // Can it generate MVT tiles?
    public bool SupportsTransactions => true; // Does it support transactions?
    public bool SupportsSpatialIndexes => true; // Can you create spatial indexes?
    public bool SupportsServerSideGeometryOperations => true; // ST_* functions?
    public bool SupportsCrsTransformations => false; // ST_Transform()?
    public int MaxQueryParameters => 10000; // What's the parameter limit?
    public bool SupportsReturningClause => false; // RETURNING support?
    public bool SupportsBulkOperations => false; // Bulk insert/update/delete?
    public bool SupportsSoftDelete => false; // Soft delete tracking?
}
```

### Step 2: Implement IDataStoreProvider

For **relational databases**, extend `RelationalDataStoreProviderBase`:

```csharp
public sealed class MyDatabaseProvider : RelationalDataStoreProviderBase<MyDbConnection, MyDbTransaction, MyDbCommand>
{
    public const string ProviderKey = "mydatabase";

    public MyDatabaseProvider(
        IOptions<DataAccessOptions>? options = null,
        IConnectionStringEncryptionService? encryptionService = null,
        ILoggerFactory? loggerFactory = null)
        : base(ProviderKey, CreateRetryPipeline(), encryptionService)
    {
        // Provider-specific initialization
    }

    public override string Provider => ProviderKey;
    public override IDataStoreCapabilities Capabilities => MyDatabaseCapabilities.Instance;

    protected override MyDbConnection CreateConnectionCore(string connectionString)
    {
        return new MyDbConnection(connectionString);
    }

    protected override string NormalizeConnectionString(string connectionString)
    {
        // Validate and add defaults
        ConnectionStringValidator.Validate(connectionString); // CRITICAL: Prevents SQL injection
        return connectionString; // Add provider-specific defaults if needed
    }

    protected override IsolationLevel GetDefaultIsolationLevel()
    {
        return IsolationLevel.RepeatableRead; // Or your preferred level
    }

    // Implement abstract methods: QueryAsync, CountAsync, GetAsync, CreateAsync, UpdateAsync, DeleteAsync, etc.
    public override IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        // Your implementation
    }

    // ... implement remaining methods
}
```

For **NoSQL databases**, implement `IDataStoreProvider` directly:

```csharp
public sealed class MyNoSqlProvider : IDataStoreProvider
{
    public const string ProviderKey = "mynosql";

    public string Provider => ProviderKey;
    public IDataStoreCapabilities Capabilities => MyNoSqlCapabilities.Instance;

    // Implement all interface methods
}
```

### Step 3: Create Query Builder

```csharp
internal sealed class MyDatabaseQueryBuilder
{
    public string BuildSelectQuery(
        LayerDefinition layer,
        FeatureQuery? query,
        int storageSrid,
        int targetSrid)
    {
        // Build SELECT statement with:
        // - Column selection
        // - Geometry transformation (if needed)
        // - WHERE clause (filters, bbox, temporal)
        // - ORDER BY
        // - LIMIT/OFFSET or pagination

        return sql;
    }
}
```

### Step 4: Register Provider

```csharp
// In your ServiceCollectionExtensions
public static IServiceCollection AddMyDatabaseProvider(this IServiceCollection services)
{
    services.AddKeyedSingleton<IDataStoreProvider>(
        MyDatabaseProvider.ProviderKey,
        (sp, _) => new MyDatabaseProvider(
            sp.GetService<IOptions<DataAccessOptions>>(),
            sp.GetService<IConnectionStringEncryptionService>(),
            sp.GetService<ILoggerFactory>()
        ));

    return services;
}
```

### Step 5: Write Tests

Create test class extending `DataStoreProviderTestsBase`:

```csharp
public class MyDatabaseProviderTests : DataStoreProviderTestsBase
{
    protected override IDataStoreProvider CreateProvider()
    {
        return new MyDatabaseProvider();
    }

    protected override DataSourceDefinition CreateDataSource()
    {
        return new DataSourceDefinition
        {
            Id = "test-mydatabase",
            Provider = MyDatabaseProvider.ProviderKey,
            ConnectionString = "..." // Test database connection string
        };
    }

    // Test methods are inherited from base class:
    // - QueryAsync_WithBbox_ReturnsMatchingFeatures
    // - CreateAsync_ValidRecord_ReturnsCreatedFeature
    // - UpdateAsync_ExistingFeature_UpdatesSuccessfully
    // - DeleteAsync_ExistingFeature_DeletesSuccessfully
    // - BulkInsertAsync_MultipleRecords_InsertsAll
    // - BeginTransactionAsync_CommitChanges_Persists
    // - BeginTransactionAsync_RollbackChanges_Reverts
}
```

### Step 6: Document Capabilities

Update this README with:
- Provider description
- Connection string format
- Feature matrix entry
- Performance characteristics
- Best practices

---

## Performance Considerations

### 1. Use Spatial Indexes

**Always create spatial indexes on geometry columns:**

```sql
-- PostgreSQL
CREATE INDEX idx_features_geom ON features USING GIST(geom);

-- MySQL
CREATE SPATIAL INDEX idx_features_geom ON features(geom);

-- SQL Server
CREATE SPATIAL INDEX sidx_features_geom ON features(geom)
WITH (BOUNDING_BOX = (-180, -90, 180, 90));

-- SQLite (requires SpatiaLite)
SELECT CreateSpatialIndex('features', 'geom');
```

### 2. Use Query Builders

Query builders optimize SQL generation:
- **Spatial index operators**: PostgreSQL `&&`, SQL Server index hints
- **Filter pushdown**: WHERE clauses before geometry operations
- **Column selection**: Only fetch needed columns
- **Pagination**: LIMIT/OFFSET or keyset pagination

```csharp
// Good: Uses query builder (optimized)
var query = new FeatureQuery(
    Limit: 100,
    PropertyNames: new[] { "name", "population" }, // Only fetch these columns
    Bbox: bbox // Spatial filter pushed to database
);
var features = await provider.QueryAsync(dataSource, service, layer, query, cancellationToken);

// Bad: Fetching all data then filtering in memory
var allFeatures = await provider.QueryAsync(dataSource, service, layer, null, cancellationToken);
var filtered = allFeatures.Where(f => IsInBbox(f)); // SLOW!
```

### 3. Use Bulk Operations

For inserting/updating many records, use bulk operations:

```csharp
// Good: Bulk insert (1 query for 10,000 records)
await provider.BulkInsertAsync(dataSource, service, layer, records, cancellationToken);

// Bad: Individual inserts (10,000 queries!)
foreach (var record in records)
{
    await provider.CreateAsync(dataSource, service, layer, record, null, cancellationToken);
}
```

### 4. Use Connection Pooling

Enable pooling in connection strings:

```json
"ConnectionString": "Host=localhost;Database=geodata;Pooling=true;Min Pool Size=2;Max Pool Size=50"
```

Monitor pool metrics:

```csharp
// PostgreSQL pool metrics
var metrics = postgresProvider.GetConnectionPoolMetrics();
Console.WriteLine($"Active: {metrics.ActiveConnections}, Idle: {metrics.IdleConnections}");
```

### 5. Use Prepared Statement Caching

For frequently-executed queries, use prepared statements:

```csharp
// PreparedStatementCache automatically caches common queries
var cache = new PreparedStatementCache(maxSize: 100);
var cached = cache.GetOrCreate("SELECT * FROM features WHERE id = @id", () => CreateCommand());
```

### 6. Use Keyset Pagination

For large datasets, use keyset (cursor-based) pagination instead of OFFSET:

```csharp
// Good: Keyset pagination (O(1) performance)
var query = new FeatureQuery(
    Limit: 100,
    Cursor: "eyJpZCI6MTIzNDU2fQ==" // Base64-encoded cursor
);

// Bad: Offset pagination (O(n) performance, skips first N rows every time)
var query = new FeatureQuery(
    Limit: 100,
    Offset: 10000 // Skips 10,000 rows - SLOW!
);
```

### 7. Use Database-Side Aggregations

Push statistical operations to the database:

```csharp
// Good: Database-side aggregation (fast)
var statistics = await provider.QueryStatisticsAsync(
    dataSource,
    service,
    layer,
    statistics: new[] { new StatisticDefinition("population", StatisticType.Sum) },
    groupByFields: new[] { "country" },
    filter: null,
    cancellationToken
);

// Bad: In-memory aggregation (loads all data into memory)
var features = await provider.QueryAsync(...).ToListAsync();
var totalPopulation = features.Sum(f => (int)f.Attributes["population"]); // SLOW!
```

### 8. Optimize Geometry Simplification

For vector tiles, simplify geometries at appropriate zoom levels:

```csharp
// PostgreSQL ST_Simplify with zoom-aware tolerance
var tolerance = zoom switch
{
    < 5 => 0.1,    // Very aggressive simplification
    < 8 => 0.01,   // Aggressive
    < 12 => 0.001, // Moderate
    < 15 => 0.0001, // Light
    _ => 0.0       // No simplification
};
```

### 9. Monitor Query Performance

Use database-specific monitoring:

```sql
-- PostgreSQL: pg_stat_statements
SELECT query, calls, mean_exec_time, total_exec_time
FROM pg_stat_statements
WHERE query LIKE '%features%'
ORDER BY mean_exec_time DESC
LIMIT 10;

-- SQL Server: Query Store
SELECT TOP 10 qt.query_sql_text, rs.avg_duration / 1000.0 AS avg_duration_ms
FROM sys.query_store_query_text qt
JOIN sys.query_store_query q ON qt.query_text_id = q.query_text_id
JOIN sys.query_store_plan p ON q.query_id = p.query_id
JOIN sys.query_store_runtime_stats rs ON p.plan_id = rs.plan_id
ORDER BY rs.avg_duration DESC;
```

### 10. PostgreSQL-Specific Optimizations

For PostgreSQL, consider using optimized database functions:

```sql
-- Install optimized PostgreSQL functions (10x faster for MVT tiles)
\i Migrations/014_PostgresOptimizations.sql
```

See `Data/Postgres/README_OPTIMIZATIONS.md` for details.

### Performance Benchmarks

| Operation | Standard | Optimized | Improvement |
|-----------|----------|-----------|-------------|
| **MVT Tile Generation** | 1200ms | 120ms | 10x |
| **Feature Query (1000)** | 850ms | 170ms | 5x |
| **Count Query** | 450ms | 90ms | 5x |
| **Spatial Aggregation** | 2100ms | 105ms | 20x |
| **Point Clustering** | 800ms | 100ms | 8x |
| **Bulk Insert (10K)** | 45s | 2.5s | 18x |

---

## Testing

### Unit Tests

Test providers using `DataStoreProviderTestsBase`:

```csharp
public class PostgresDataStoreProviderTests : DataStoreProviderTestsBase
{
    protected override IDataStoreProvider CreateProvider()
    {
        return new PostgresDataStoreProvider();
    }

    protected override DataSourceDefinition CreateDataSource()
    {
        return new DataSourceDefinition
        {
            Id = "test-postgres",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Database=honua_test;Username=postgres;Password=postgres"
        };
    }
}
```

Base class provides:
- ✅ Query tests (bbox, temporal, filters)
- ✅ CRUD tests (create, read, update, delete)
- ✅ Bulk operation tests
- ✅ Transaction tests
- ✅ Soft delete tests
- ✅ Concurrency tests

### Integration Tests

Test with real databases using Docker:

```bash
# Start test databases
docker-compose -f tests/docker-compose.yml up -d

# Run integration tests
dotnet test tests/Honua.Server.Core.Tests.Integration

# Clean up
docker-compose -f tests/docker-compose.yml down -v
```

### Smoke Tests

Multi-provider smoke tests verify basic functionality:

```bash
dotnet test tests/Honua.Server.Core.Tests.Integration --filter "FullyQualifiedName~ProviderSmokeTests"
```

### Performance Tests

Benchmark provider performance:

```bash
cd tests/Honua.Server.Benchmarks
dotnet run -c Release --filter "*DataStore*"
```

### Testing Checklist

When implementing a new provider:

- [ ] Create provider class
- [ ] Implement all `IDataStoreProvider` methods
- [ ] Create capabilities class
- [ ] Create query builder
- [ ] Write unit tests extending `DataStoreProviderTestsBase`
- [ ] Write integration tests with test database
- [ ] Write benchmarks comparing to PostgreSQL baseline
- [ ] Test connection pooling and cleanup
- [ ] Test transaction commit and rollback
- [ ] Test concurrent access (race conditions)
- [ ] Test error handling (network failures, constraint violations)
- [ ] Test with large datasets (10K+ features)
- [ ] Test with complex geometries (multi-polygons, holes)
- [ ] Document connection string format
- [ ] Update feature matrix in README

---

## Related Documentation

- **Query Optimization**: `Data/Query/QueryOptimizationExamples.md`
- **PostgreSQL Optimizations**: `Data/Postgres/README_OPTIMIZATIONS.md`
- **Migrations**: `Data/Migrations/`
- **Soft Delete**: `Data/SoftDelete/`
- **Validation**: `Data/Validation/`

---

## Architecture Diagrams

### Query Execution Flow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ HTTP Request
       ▼
┌──────────────────────┐
│   API Controller     │
│  (OData, Features)   │
└──────┬───────────────┘
       │
       │ Service Call
       ▼
┌──────────────────────────┐
│   Feature Service        │
│  (Business Logic)        │
└──────┬───────────────────┘
       │
       │ Provider Factory
       ▼
┌──────────────────────────┐
│ IDataStoreProviderFactory│
│  Create(providerKey)     │
└──────┬───────────────────┘
       │
       │ Returns Provider
       ▼
┌──────────────────────────┐
│   IDataStoreProvider     │
│   (PostgreSQL/MySQL/etc) │
└──────┬───────────────────┘
       │
       │ Query Builder
       ▼
┌──────────────────────────┐
│  Query Builder           │
│  - Build SQL             │
│  - Add parameters        │
│  - Optimize indexes      │
└──────┬───────────────────┘
       │
       │ Execute Query
       ▼
┌──────────────────────────┐
│  Database (ADO.NET)      │
│  - Connection Pool       │
│  - Spatial Indexes       │
│  - PostGIS/Spatial       │
└──────┬───────────────────┘
       │
       │ Result Stream
       ▼
┌──────────────────────────┐
│  FeatureRecordReader     │
│  - Read geometries       │
│  - Parse attributes      │
│  - Transform CRS         │
└──────┬───────────────────┘
       │
       │ Return Results
       ▼
┌──────────────────────────┐
│  Client (GeoJSON/MVT)    │
└──────────────────────────┘
```

### Transaction Flow

```
┌──────────────────┐
│ Application Code │
└────────┬─────────┘
         │
         │ BeginTransactionAsync()
         ▼
┌────────────────────────────┐
│ IDataStoreProvider         │
│ BeginTransactionAsync()    │
└────────┬───────────────────┘
         │
         │ Create Connection
         ▼
┌────────────────────────────┐
│ RelationalProviderBase     │
│ CreateConnectionAsync()    │
└────────┬───────────────────┘
         │
         │ Open Connection
         ▼
┌────────────────────────────┐
│ Database Connection        │
│ connection.BeginTransaction│
└────────┬───────────────────┘
         │
         │ Return Transaction
         ▼
┌────────────────────────────┐
│ IDataStoreTransaction      │
│ (owns connection)          │
└────────┬───────────────────┘
         │
         ├─► CreateAsync(tx)
         ├─► UpdateAsync(tx)
         ├─► DeleteAsync(tx)
         │
         │ Success?
         ├─────Yes────► CommitAsync()
         └─────No─────► RollbackAsync()
                              │
                              ▼
                      ┌───────────────┐
                      │ DisposeAsync()│
                      │ (auto-cleanup)│
                      └───────────────┘
```

---

## Contributing

When contributing to the Data module:

1. **Follow existing patterns**: Use `RelationalDataStoreProviderBase` for relational databases
2. **Security first**: Always validate connection strings, use parameterized queries
3. **Test thoroughly**: Write unit tests, integration tests, and benchmarks
4. **Document everything**: Update README, add code comments, document limitations
5. **Optimize queries**: Use spatial indexes, minimize data transfer, push logic to database
6. **Handle errors gracefully**: Classify exceptions, provide helpful messages, support retries

---

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
