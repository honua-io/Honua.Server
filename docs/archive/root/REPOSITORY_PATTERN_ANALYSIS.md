# Repository Pattern Analysis and Consolidation Opportunities

**Date:** 2025-10-25
**Scope:** Repository/Store/Provider pattern usage across HonuaIO codebase
**Focus Areas:** Data access layers, metadata providers, attachment stores, catalog stores

---

## Executive Summary

**Total Repositories Found:** 87+ repository/store/provider implementations
**Key Patterns Identified:** 6 distinct repository pattern families
**Consolidation Potential:** ~2,500-3,000 lines of duplicate code
**Priority Level:** HIGH - Significant code duplication in critical data access paths

### Top 5 Consolidation Opportunities (Ranked by Impact)

1. **Relational Database Repository Base Classes** (Est. savings: 800-1000 lines)
2. **CRUD Store Pattern Consolidation** (Est. savings: 600-800 lines)
3. **Attachment Store Pattern Unification** (Est. savings: 400-600 lines)
4. **Generic Connection Management** (Est. savings: 300-400 lines)
5. **Query Building Utilities** (Est. savings: 200-300 lines)

---

## 1. Repository Inventory

### 1.1 Authentication Repositories (4 implementations + 1 base)

**Interface:** `IAuthRepository`
**Base Class:** `RelationalAuthRepositoryBase` (984 lines)
**Implementations:**
- `PostgresAuthRepository` (130 lines) - Postgres-specific
- `MySqlAuthRepository` (110 lines) - MySQL-specific
- `SqlServerAuthRepository` - SQL Server-specific
- `SqliteAuthRepository` - SQLite-specific

**Pattern:** Abstract base with dialect pattern for SQL differences

**Common Operations:**
- User CRUD (Create, Read, Update, Delete)
- Credential management
- Role assignments
- Audit logging
- Bootstrap state management
- Connection pooling with retry policies
- Transaction management

**Key Duplication:**
- Connection creation logic (4x duplicated)
- Retry pipeline configuration (4x duplicated)
- Schema initialization (4x with slight variations)

---

### 1.2 STAC Catalog Stores (7 implementations + 1 base)

**Interface:** `IStacCatalogStore`
**Base Class:** `RelationalStacCatalogStore` (1,632 lines)
**Implementations:**
- `InMemoryStacCatalogStore` (487 lines) - In-memory with locking
- `PostgresStacCatalogStore` - Postgres with COPY bulk insert
- `MySqlStacCatalogStore` - MySQL-specific
- `SqlServerStacCatalogStore` - SQL Server-specific
- `SqliteStacCatalogStore` - SQLite-specific
- Plus enterprise variants

**Pattern:** Abstract relational base + in-memory implementation

**Common Operations:**
- Collection CRUD
- Item CRUD with bulk operations
- Search with spatial/temporal filters
- Pagination with continuation tokens
- ETag-based optimistic concurrency
- Bulk upsert with batching
- Count estimation for large datasets

**Key Duplication:**
- Search filter building (7x variations)
- Pagination logic (7x variations)
- ETag generation (7x duplicated)
- Continuation token parsing (7x duplicated)
- Bbox filtering (multiple implementations)

---

### 1.3 Attachment Stores (5 implementations)

**Interface:** `IAttachmentStore`
**Implementations:**
- `DatabaseAttachmentStore` (238 lines) - Multi-database support
- `FileSystemAttachmentStore` (358 lines) - Local file system
- `S3AttachmentStore` - AWS S3
- `AzureBlobAttachmentStore` - Azure Blob Storage
- `GcsAttachmentStore` - Google Cloud Storage

**Pattern:** Provider pattern with IAttachmentStoreProvider factory

**Common Operations:**
- Put (upload) with validation
- Get (download) with streaming
- Delete
- List with prefix filtering
- MIME type validation
- File size limits
- Security validation (path traversal, extension whitelist)

**Key Duplication:**
- File validation logic (5x variations)
- Stream copying with size limits (5x variations)
- MIME type checking (5x variations)
- Path security validation (at least 3x)

---

### 1.4 Metadata Providers (5 implementations)

**Interface:** `IMetadataProvider`, `IMutableMetadataProvider`
**Implementations:**
- `JsonMetadataProvider` (494 lines) - File-based JSON
- `YamlMetadataProvider` - File-based YAML
- `PostgresMetadataProvider` - Database-backed
- `RedisMetadataProvider` - Redis cache
- `SqlServerMetadataProvider` - SQL Server

**Pattern:** Interface with optional mutability and versioning

**Common Operations:**
- Load metadata snapshot
- Save metadata snapshot
- Update individual layers
- Version management (snapshot/restore)
- Change notification (file watcher, Redis pub/sub, DB notify)

**Key Duplication:**
- Snapshot serialization/deserialization (5x)
- Change notification infrastructure (3x variations)
- File watching logic (2x duplicated)

---

### 1.5 Process State Stores (2 implementations)

**Interface:** `IProcessStateStore`
**Implementations:**
- `InMemoryProcessStateStore` (183 lines) - In-memory with ConcurrentDictionary
- `RedisProcessStateStore` - Redis-backed

**Common Operations:**
- Get/Save process info
- Update process status
- Get active processes
- Cancel process
- Delete process

**Key Duplication:**
- Status update logic (2x)
- Active process filtering (2x)
- Validation logic (2x duplicated)

---

### 1.6 Data Store Providers (13+ implementations)

**Interface:** `IDataStoreProvider`, `IDataStoreCapabilities`
**Implementations:**
- Core: Postgres, MySQL, SQL Server, SQLite (4)
- Enterprise: BigQuery, CosmosDB, Elasticsearch, MongoDB, Oracle, Redshift, Snowflake (7+)

**Common Operations:**
- Connection creation
- Feature querying
- Geometry operations
- Bulk operations
- Capability declaration

**Key Duplication:**
- Connection string parsing (13x)
- Capability flags (13x similar structures)
- Error handling patterns (13x)

---

## 2. Pattern Analysis

### 2.1 Common Repository Interfaces

**Observed Patterns:**

1. **CRUD Repository Pattern** (Used by: Auth, STAC, Attachments)
   ```csharp
   Task<T?> GetAsync(TKey id, CancellationToken ct);
   Task<IReadOnlyList<T>> ListAsync(...);
   Task SaveAsync(T entity, CancellationToken ct);
   Task<bool> DeleteAsync(TKey id, CancellationToken ct);
   ```

2. **Store Pattern with Pointers** (Used by: Attachments)
   ```csharp
   Task<TResult> PutAsync(Stream content, TRequest request, CancellationToken ct);
   Task<TData?> TryGetAsync(TPointer pointer, CancellationToken ct);
   ```

3. **Provider Factory Pattern** (Used by: Attachments, DataStores)
   ```csharp
   interface IProvider { string ProviderKey { get; } }
   interface IProviderFactory<T> { T Create(string profileId, TConfig config); }
   ```

4. **Relational Base with Dialect** (Used by: Auth, STAC)
   ```csharp
   abstract class RelationalBase {
       protected abstract DbConnection CreateConnection();
       protected abstract IDialect Dialect { get; }
   }
   ```

---

### 2.2 Common Operations Across Repositories

**Connection Management:**
- Opening connections with retry policies (Present in: Auth, STAC, Attachments)
- Connection pooling configuration (Present in: All DB-backed stores)
- Timeout handling (Present in: Auth, STAC)

**Transaction Handling:**
- Begin/commit/rollback (Present in: Auth, STAC)
- Distributed transaction support (Limited to: None currently)
- Savepoints (Present in: None currently)

**Query Building:**
- Parameter binding (Present in: Auth, STAC, DataStores)
- Dynamic WHERE clause building (Present in: STAC, DataStores)
- Pagination (offset/limit vs continuation tokens) (Present in: STAC)
- Filter expression parsing (Present in: STAC with CQL2)

**Error Handling:**
- Retry policies with exponential backoff (Present in: Auth, some DataStores)
- Database-specific error code handling (Scattered across implementations)
- Graceful degradation (Present in: STAC count estimation)
- Logging and telemetry (Inconsistent across implementations)

**Concurrency Control:**
- Optimistic locking with ETags (Present in: STAC)
- Row versioning (Not used currently)
- Distributed locks (Present in: WFS lock manager only)

---

### 2.3 Shared Database Utilities

**Currently Duplicated Across Multiple Files:**

1. **Connection Factories** (13+ duplications)
   - Postgres connection creation
   - MySQL connection creation
   - SQL Server connection creation
   - SQLite connection creation

2. **Parameter Binding** (7+ variations)
   - `DbCommand.AddParameter()` extension
   - Different approaches to handling DBNull
   - Type conversion logic

3. **Retry Policies** (4+ duplications)
   - `DatabaseRetryPolicy.CreatePostgresRetryPipeline()`
   - `DatabaseRetryPolicy.CreateMySqlRetryPipeline()`
   - Similar patterns for other databases

4. **DateTime Handling** (Multiple duplications)
   - UTC normalization
   - DateTimeOffset conversion
   - Timezone handling

5. **Schema Initialization** (4+ duplications)
   - CREATE TABLE IF NOT EXISTS patterns
   - Index creation
   - Foreign key constraints
   - Default data insertion

---

## 3. Consolidation Opportunities

### 3.1 HIGH Priority: Generic Repository Base Classes

**Opportunity:** Create generic repository base classes that eliminate 60-70% of duplicate code.

**Proposed Structure:**

```csharp
// Generic base for all relational repositories
public abstract class RelationalRepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger _logger;
    private readonly ResiliencePipeline _retryPipeline;

    protected abstract string TableName { get; }
    protected abstract string PrimaryKeyColumn { get; }
    protected abstract TEntity MapFromReader(DbDataReader reader);
    protected abstract void MapToParameters(TEntity entity, DbCommand command);

    // Common CRUD operations implemented once
    public virtual async Task<TEntity?> GetAsync(TKey id, CancellationToken ct) { ... }
    public virtual async Task SaveAsync(TEntity entity, CancellationToken ct) { ... }
    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken ct) { ... }
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct) { ... }
}

// Specialized for entities with auditing
public abstract class AuditableRepositoryBase<TEntity, TKey>
    : RelationalRepositoryBase<TEntity, TKey>
{
    protected abstract void WriteAuditLog(string action, TEntity entity);
    // Override SaveAsync to add auditing
}

// Specialized for entities with ETags
public abstract class VersionedRepositoryBase<TEntity, TKey>
    : RelationalRepositoryBase<TEntity, TKey>
{
    public async Task SaveAsync(TEntity entity, string? expectedETag, CancellationToken ct) { ... }
}
```

**Benefits:**
- Eliminates 600-800 lines of duplicate CRUD code
- Standardizes error handling and retry logic
- Centralizes connection management
- Enforces consistent patterns

**Affected Classes:**
- `RelationalAuthRepositoryBase` → Extends new base
- `RelationalStacCatalogStore` → Extends new base
- `DatabaseAttachmentStore` → Uses utilities from base
- All concrete repository implementations → Simplified

**Estimated Line Savings:** 800-1000 lines

---

### 3.2 HIGH Priority: Connection and Transaction Utilities

**Opportunity:** Extract all connection management into reusable utilities.

**Proposed Structure:**

```csharp
// Centralized connection factory
public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
    string ProviderName { get; }
}

public class DbConnectionFactoryBuilder
{
    public static IDbConnectionFactory Create(string provider, string connectionString)
    {
        return provider.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => new PostgresConnectionFactory(connectionString),
            "mysql" or "mariadb" => new MySqlConnectionFactory(connectionString),
            "sqlserver" or "mssql" => new SqlServerConnectionFactory(connectionString),
            "sqlite" => new SqliteConnectionFactory(connectionString),
            _ => throw new NotSupportedException($"Provider '{provider}' not supported")
        };
    }
}

// Centralized transaction management
public class TransactionScope : IAsyncDisposable
{
    public async Task CommitAsync(CancellationToken ct) { ... }
    public async Task RollbackAsync(CancellationToken ct) { ... }
}

public static class DbConnectionExtensions
{
    public static async Task<TransactionScope> BeginTransactionScopeAsync(
        this DbConnection connection,
        CancellationToken ct = default) { ... }

    public static async Task OpenWithRetryAsync(
        this DbConnection connection,
        ResiliencePipeline pipeline,
        CancellationToken ct = default) { ... }
}
```

**Benefits:**
- Single source of truth for connection creation
- Consistent retry logic
- Standardized transaction handling
- Easier to add new database providers

**Affected Classes:**
- All repositories with database connections (15+)
- All attachment stores with database backend
- All data store providers

**Estimated Line Savings:** 300-400 lines

---

### 3.3 MEDIUM Priority: Query Builder Abstraction

**Opportunity:** Create a fluent query builder to eliminate duplicate query construction code.

**Proposed Structure:**

```csharp
public class QueryBuilder
{
    private readonly DbCommand _command;
    private readonly List<string> _whereClauses = new();
    private readonly List<string> _orderByClauses = new();

    public QueryBuilder Where(string field, string op, object value)
    {
        var paramName = $"@p{_command.Parameters.Count}";
        _whereClauses.Add($"{field} {op} {paramName}");
        _command.AddParameter(paramName, value);
        return this;
    }

    public QueryBuilder WhereIn(string field, IEnumerable<object> values) { ... }
    public QueryBuilder OrderBy(string field, bool descending = false) { ... }
    public QueryBuilder Limit(int limit) { ... }
    public QueryBuilder Offset(int offset) { ... }

    public string Build(string baseQuery)
    {
        var sb = new StringBuilder(baseQuery);
        if (_whereClauses.Any())
            sb.Append(" WHERE ").Append(string.Join(" AND ", _whereClauses));
        if (_orderByClauses.Any())
            sb.Append(" ORDER BY ").Append(string.Join(", ", _orderByClauses));
        return sb.ToString();
    }
}
```

**Benefits:**
- Eliminates manual WHERE clause building
- Type-safe parameter binding
- Consistent SQL generation
- Easier to add features like pagination tokens

**Affected Classes:**
- `RelationalStacCatalogStore` (BuildSearchFilter method)
- All DataStore query builders
- Custom query methods in repositories

**Estimated Line Savings:** 200-300 lines

---

### 3.4 MEDIUM Priority: Store Pattern Unification

**Opportunity:** Create unified base for all "store" patterns (attachment stores, state stores, etc.)

**Proposed Structure:**

```csharp
// Generic read-only store
public interface IReadOnlyStore<TEntity, TKey>
{
    Task<TEntity?> TryGetAsync(TKey key, CancellationToken ct = default);
    IAsyncEnumerable<TEntity> ListAsync(string? prefix = null, CancellationToken ct = default);
}

// Generic mutable store
public interface IMutableStore<TEntity, TKey> : IReadOnlyStore<TEntity, TKey>
{
    Task<TKey> PutAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(TKey key, CancellationToken ct = default);
}

// Base implementation for in-memory stores
public abstract class InMemoryStoreBase<TEntity, TKey> : IMutableStore<TEntity, TKey>
{
    private readonly ConcurrentDictionary<TKey, TEntity> _storage;
    private readonly ReaderWriterLockSlim _lock;

    protected abstract TKey GetKey(TEntity entity);

    // Common implementation for all in-memory stores
    public virtual async Task<TEntity?> TryGetAsync(TKey key, CancellationToken ct) { ... }
    public virtual async Task<TKey> PutAsync(TEntity entity, CancellationToken ct) { ... }
    // ... etc
}
```

**Benefits:**
- Standardizes store interface across codebase
- Enables polymorphic store access
- Reduces boilerplate in implementations
- Easier to swap implementations

**Affected Classes:**
- `InMemoryStacCatalogStore`
- `InMemoryProcessStateStore`
- `InMemoryRasterTileCacheMetadataStore`
- File-based stores can also adopt pattern

**Estimated Line Savings:** 400-600 lines

---

### 3.5 LOW Priority: Validation and Security Utilities

**Opportunity:** Extract common validation logic into shared utilities.

**Proposed Structure:**

```csharp
public static class AttachmentValidation
{
    private static readonly HashSet<string> AllowedExtensions = new(...);
    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(...);

    public static void ValidateFileUpload(string fileName, string? mimeType, long? sizeBytes = null)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Extension '{extension}' not allowed");

        if (mimeType != null && !ValidateMimeType(extension, mimeType))
            throw new InvalidOperationException($"MIME type mismatch");

        if (sizeBytes.HasValue && sizeBytes.Value > MaxFileSizeBytes)
            throw new InvalidOperationException("File too large");
    }

    public static string SanitizePath(string path, string rootPath)
    {
        // Common path traversal protection
        // Currently duplicated in FileSystemAttachmentStore and others
    }
}

public static class SqlIdentifierValidation
{
    public static void ValidateIdentifier(string identifier) { ... }
    public static void ValidateTableName(string tableName) { ... }
}
```

**Benefits:**
- Centralizes security-critical validation
- Easier to audit and update security rules
- Consistent behavior across stores

**Affected Classes:**
- `FileSystemAttachmentStore` (validation logic)
- `DatabaseAttachmentStore` (validation logic)
- All cloud storage attachment stores
- SQL injection protection across repositories

**Estimated Line Savings:** 150-250 lines

---

## 4. Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
**Goal:** Establish core abstractions without breaking existing code

1. Create `Honua.Server.Core/Data/Repositories/` namespace
2. Implement `IDbConnectionFactory` and provider-specific factories
3. Implement `DbConnectionExtensions` for retry and transaction management
4. Add comprehensive unit tests for new utilities

**Deliverables:**
- `IDbConnectionFactory.cs`
- `DbConnectionFactoryBuilder.cs`
- `DbConnectionExtensions.cs`
- `TransactionScope.cs`
- 50+ unit tests

**Risk:** LOW - Additive changes only

---

### Phase 2: Repository Base Classes (Week 3-4)
**Goal:** Create reusable repository base classes

1. Implement `RelationalRepositoryBase<TEntity, TKey>`
2. Implement `AuditableRepositoryBase<TEntity, TKey>`
3. Implement `VersionedRepositoryBase<TEntity, TKey>`
4. Create migration guide for existing repositories

**Deliverables:**
- Base repository classes
- Migration guide
- Example migrations for 2-3 repositories
- Integration tests

**Risk:** MEDIUM - Requires careful interface design

---

### Phase 3: Migrate Auth Repositories (Week 5)
**Goal:** Prove pattern with existing complex repository

1. Refactor `RelationalAuthRepositoryBase` to extend new base
2. Simplify concrete implementations (Postgres, MySQL, SQLServer, SQLite)
3. Validate no regression in tests
4. Measure line count reduction

**Deliverables:**
- Refactored auth repositories
- All existing tests passing
- Performance benchmarks (should be same or better)

**Risk:** MEDIUM - Critical auth path, needs thorough testing

---

### Phase 4: Migrate STAC Stores (Week 6-7)
**Goal:** Apply pattern to largest repository

1. Refactor `RelationalStacCatalogStore` to use new base
2. Extract query building to `QueryBuilder` utility
3. Simplify database-specific implementations
4. Add bulk operation optimizations

**Deliverables:**
- Refactored STAC stores
- New `QueryBuilder` utility
- Performance improvements for bulk operations

**Risk:** HIGH - Complex queries, needs extensive testing

---

### Phase 5: Store Pattern Unification (Week 8)
**Goal:** Standardize all store interfaces

1. Implement `InMemoryStoreBase<TEntity, TKey>`
2. Migrate in-memory stores to use base
3. Create adapter pattern for existing stores not yet migrated
4. Update documentation

**Deliverables:**
- `InMemoryStoreBase` implementation
- 3-4 migrated store implementations
- Updated architecture documentation

**Risk:** LOW - In-memory stores are simpler

---

### Phase 6: Attachment Stores (Week 9)
**Goal:** Consolidate attachment validation and error handling

1. Extract `AttachmentValidation` utility
2. Standardize error messages across providers
3. Add missing validation to cloud providers
4. Security audit of path traversal protection

**Deliverables:**
- Centralized validation utilities
- Security audit report
- Consistent error handling

**Risk:** LOW - Mostly extraction, not rewrite

---

### Phase 7: Data Store Providers (Week 10-11)
**Goal:** Reduce duplication in data store providers

1. Create `DataStoreProviderBase` for common operations
2. Migrate core providers (Postgres, MySQL, SQLServer, SQLite)
3. Update enterprise providers
4. Benchmark performance impact

**Deliverables:**
- Base provider class
- Migrated implementations
- Performance comparison report

**Risk:** MEDIUM - Complex domain logic

---

### Phase 8: Documentation and Training (Week 12)
**Goal:** Ensure team can use new patterns

1. Write comprehensive developer guide
2. Create code templates for common scenarios
3. Record video walkthrough
4. Update PR review checklist

**Deliverables:**
- Developer guide (markdown)
- VS Code snippets
- 30-min training video
- PR review checklist updates

**Risk:** LOW - Documentation only

---

## 5. Examples: Before/After

### Example 1: Auth Repository Simplification

**Before (PostgresAuthRepository.cs - 130 lines):**
```csharp
internal sealed class PostgresAuthRepository : RelationalAuthRepositoryBase
{
    private readonly string _connectionString;

    public PostgresAuthRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<PostgresAuthRepository> logger,
        AuthMetrics? metrics,
        string connectionString,
        string? schema = null)
        : base(authOptions, logger, metrics, DatabaseRetryPolicy.CreatePostgresRetryPipeline(), new PostgresAuthDialect(schema))
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    private sealed class PostgresAuthDialect : IRelationalAuthDialect
    {
        private readonly string _schemaPrefix;
        private readonly IReadOnlyList<string> _statements;

        public PostgresAuthDialect(string? schema)
        {
            var normalizedSchema = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
            _schemaPrefix = string.IsNullOrWhiteSpace(normalizedSchema) ? string.Empty : $"\"{normalizedSchema}\".";

            // 100+ lines of schema DDL statements...
        }

        public string ProviderName => "postgres";
        public IReadOnlyList<string> SchemaStatements => _statements;
        public string LimitClause(string parameterName) => $"LIMIT {parameterName}";
    }
}
```

**After (PostgresAuthRepository.cs - ~40 lines):**
```csharp
internal sealed class PostgresAuthRepository : AuditableRepositoryBase<AuthUser, string>
{
    public PostgresAuthRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<PostgresAuthRepository> logger,
        AuthMetrics? metrics,
        IDbConnectionFactory connectionFactory)
        : base(connectionFactory, logger, metrics)
    {
        Options = authOptions;
    }

    protected override string TableName => "auth_users";
    protected override string PrimaryKeyColumn => "id";

    protected override AuthUser MapFromReader(DbDataReader reader) => new(
        id: reader.GetString("id"),
        username: reader.GetString("username"),
        email: reader.GetStringOrNull("email"),
        isActive: reader.GetBoolean("is_active"),
        isLocked: reader.GetBoolean("is_locked"),
        roles: Array.Empty<string>() // Loaded separately
    );

    protected override void MapToParameters(AuthUser entity, DbCommand command)
    {
        command.AddParameter("@id", entity.Id);
        command.AddParameter("@username", entity.Username);
        command.AddParameter("@email", entity.Email);
        // ... other fields
    }

    // Schema DDL moved to shared SchemaRepository or migration scripts
}
```

**Line Reduction:** 130 → 40 lines (69% reduction)

---

### Example 2: STAC Store Query Building

**Before (RelationalStacCatalogStore.cs - BuildSearchFilter method):**
```csharp
private string BuildSearchFilter(DbCommand command, StacSearchParameters parameters, bool includePagination)
{
    var clauses = new List<string>();

    if (parameters.Collections is { Count: > 0 })
    {
        var collectionNames = new List<string>();
        for (var index = 0; index < parameters.Collections.Count; index++)
        {
            var paramName = $"@collection{index}";
            collectionNames.Add(paramName);
            AddParameter(command, paramName, parameters.Collections[index]);
        }
        clauses.Add($"collection_id IN ({string.Join(", ", collectionNames)})");
    }

    if (parameters.Ids is { Count: > 0 })
    {
        var idNames = new List<string>();
        for (var index = 0; index < parameters.Ids.Count; index++)
        {
            var paramName = $"@id{index}";
            idNames.Add(paramName);
            AddParameter(command, paramName, parameters.Ids[index]);
        }
        clauses.Add($"id IN ({string.Join(", ", idNames)})");
    }

    // 50+ more lines of similar logic...

    if (clauses.Count == 0)
        return string.Empty;
    return "WHERE " + string.Join(" AND ", clauses);
}
```

**After (using QueryBuilder):**
```csharp
private string BuildSearchFilter(QueryBuilder query, StacSearchParameters parameters)
{
    if (parameters.Collections is { Count: > 0 })
        query.WhereIn("collection_id", parameters.Collections);

    if (parameters.Ids is { Count: > 0 })
        query.WhereIn("id", parameters.Ids);

    if (parameters.Start.HasValue)
        query.Where("COALESCE(end_datetime, datetime)", ">=", parameters.Start.Value);

    if (parameters.End.HasValue)
        query.Where("COALESCE(start_datetime, datetime)", "<=", parameters.End.Value);

    if (parameters.Bbox is { Length: >= 4 } && SupportsBboxFiltering)
        query.WhereBboxIntersects(parameters.Bbox);

    return query.BuildWhereClause();
}
```

**Line Reduction:** 80 → 15 lines (81% reduction)
**Benefits:** More readable, less error-prone, easier to test

---

### Example 3: Store Pattern Unification

**Before (InMemoryProcessStateStore.cs):**
```csharp
public class InMemoryProcessStateStore : IProcessStateStore
{
    private readonly ConcurrentDictionary<string, ProcessInfo> _processes;
    private readonly ILogger<InMemoryProcessStateStore> _logger;

    public InMemoryProcessStateStore(ILogger<InMemoryProcessStateStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processes = new ConcurrentDictionary<string, ProcessInfo>();
    }

    public Task<ProcessInfo?> GetProcessAsync(string processId, CancellationToken ct = default)
    {
        if (processId.IsNullOrWhiteSpace())
            throw new ArgumentException("Process ID cannot be null", nameof(processId));

        _processes.TryGetValue(processId, out var processInfo);
        return Task.FromResult(processInfo);
    }

    public Task SaveProcessAsync(ProcessInfo processInfo, CancellationToken ct = default)
    {
        if (processInfo == null)
            throw new ArgumentNullException(nameof(processInfo));

        _processes[processInfo.ProcessId] = processInfo;
        return Task.CompletedTask;
    }

    // ... 100+ more lines of similar CRUD operations
}
```

**After (using InMemoryStoreBase):**
```csharp
public class InMemoryProcessStateStore : InMemoryStoreBase<ProcessInfo, string>, IProcessStateStore
{
    public InMemoryProcessStateStore(ILogger<InMemoryProcessStateStore> logger)
        : base(logger)
    {
    }

    protected override string GetKey(ProcessInfo entity) => entity.ProcessId;

    public Task<ProcessInfo?> GetProcessAsync(string processId, CancellationToken ct = default)
        => TryGetAsync(processId, ct);

    public Task SaveProcessAsync(ProcessInfo processInfo, CancellationToken ct = default)
        => PutAsync(processInfo, ct);

    // Only custom business logic methods remain (UpdateProcessStatusAsync, etc.)
    // Basic CRUD is inherited from base
}
```

**Line Reduction:** 183 → 60 lines (67% reduction)

---

## 6. Risk Assessment

### High Risk Areas

1. **STAC Catalog Stores**
   - Complex queries with spatial/temporal filters
   - High-traffic production path
   - Mitigation: Extensive integration tests, performance benchmarks

2. **Auth Repositories**
   - Security-critical path
   - Must maintain audit trail integrity
   - Mitigation: Security review, pen testing, gradual rollout

3. **Attachment Stores**
   - File system security (path traversal)
   - Data integrity concerns
   - Mitigation: Security audit, fuzz testing

### Medium Risk Areas

1. **Data Store Providers**
   - Many database dialects to support
   - Performance-sensitive code
   - Mitigation: Per-provider benchmarks

2. **Metadata Providers**
   - Hot-reload functionality must be preserved
   - File watching can be OS-dependent
   - Mitigation: Cross-platform testing

### Low Risk Areas

1. **In-memory stores**
   - Simple implementations
   - Easy to test
   - Low production impact

2. **Utilities and extensions**
   - Additive changes
   - Can coexist with old code
   - Easy to rollback

---

## 7. Success Metrics

### Quantitative Metrics

1. **Code Reduction**
   - Target: 2,500-3,000 lines removed
   - Measure: Line count diff before/after

2. **Test Coverage**
   - Target: Maintain or improve (currently ~75%)
   - Measure: Code coverage reports

3. **Performance**
   - Target: No regression, 5-10% improvement in some paths
   - Measure: Benchmark suite

4. **Build Time**
   - Target: 10-15% reduction (less code to compile)
   - Measure: CI pipeline duration

### Qualitative Metrics

1. **Developer Productivity**
   - Faster to add new repository implementations
   - Easier to understand data access patterns
   - Fewer bugs in new code

2. **Maintainability**
   - Single place to fix bugs
   - Easier to add features (e.g., distributed tracing)
   - Clearer separation of concerns

3. **Consistency**
   - Standardized error handling
   - Consistent logging format
   - Uniform retry behavior

---

## 8. Appendix: Full Repository List

### Authentication (4)
- PostgresAuthRepository
- MySqlAuthRepository
- SqlServerAuthRepository
- SqliteAuthRepository

### STAC Catalog (7)
- InMemoryStacCatalogStore
- PostgresStacCatalogStore
- MySqlStacCatalogStore
- SqlServerStacCatalogStore
- SqliteStacCatalogStore
- RelationalStacCatalogStore (base)

### Attachments (5)
- FileSystemAttachmentStore
- DatabaseAttachmentStore
- S3AttachmentStore
- AzureBlobAttachmentStore
- GcsAttachmentStore

### Metadata (5)
- JsonMetadataProvider
- YamlMetadataProvider
- PostgresMetadataProvider
- RedisMetadataProvider
- SqlServerMetadataProvider

### Process State (2)
- InMemoryProcessStateStore
- RedisProcessStateStore

### Data Stores - Core (4)
- PostgresDataStoreProvider
- MySqlDataStoreProvider
- SqlServerDataStoreProvider
- SqliteDataStoreProvider

### Data Stores - Enterprise (7)
- BigQueryDataStoreProvider
- CosmosDbDataStoreProvider
- ElasticsearchDataStoreProvider
- MongoDbDataStoreProvider
- OracleDataStoreProvider
- RedshiftDataStoreProvider
- SnowflakeDataStoreProvider

### Raster Caching (4)
- FileSystemRasterTileCacheProvider
- S3RasterTileCacheProvider
- AzureBlobRasterTileCacheProvider
- GcsRasterTileCacheProvider

### Metadata Stores (3)
- InMemoryRasterTileCacheMetadataStore
- RedisRasterTileCacheMetadataStore
- FileMetadataSnapshotStore

### Vector Search (3)
- PostgresVectorSearchProvider
- InMemoryVectorSearchProvider
- AzureVectorSearchProvider

### Knowledge Stores (2)
- AzureAISearchKnowledgeStore
- VectorDeploymentPatternKnowledgeStore

### Agent History (1)
- PostgresAgentHistoryStore

### Feature Repositories (2)
- FeatureRepository
- IFeatureAttachmentRepository implementations

### Style Repositories (1)
- FileSystemStyleRepository

### Git Repositories (1)
- LibGit2SharpRepository

### Other Stores (10+)
- FileStateStore (deployment)
- KerchunkReferenceStore (raster)
- FileMetadataSnapshotStore
- FileConsultantSessionStore
- HonuaCliConfigStore
- MapFishPrintApplicationStore
- SqliteSubmissionRepository (OpenRosa)
- And various test stubs/mocks

**Total:** 87+ distinct repository/store/provider implementations

---

## Conclusion

The HonuaIO codebase exhibits significant duplication across repository and store patterns. By implementing the proposed consolidation strategy, we can:

1. **Reduce code by 2,500-3,000 lines** (15-20% of repository code)
2. **Standardize patterns** across 87+ implementations
3. **Improve maintainability** through centralized utilities
4. **Reduce bug surface** by eliminating duplicate logic
5. **Accelerate development** of new features and providers

The 12-week roadmap provides a structured, low-risk approach to achieving these benefits while maintaining backward compatibility and production stability.

**Recommended Next Steps:**
1. Review and approve this analysis with tech leads
2. Allocate dedicated engineering time for Phase 1
3. Set up tracking metrics and benchmarks
4. Begin implementation with foundation utilities (Week 1-2)
