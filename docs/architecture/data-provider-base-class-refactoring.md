# Data Provider Base Class Refactoring - Design Document

## Executive Summary

This document outlines the refactoring of 12 database provider implementations to eliminate ~4,677 lines of duplicated code by extracting common patterns into an enhanced `RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>` class.

**Key Metrics:**
- **Providers Affected**: 12 (MySQL, SQL Server, SQLite, PostgreSQL, Oracle, BigQuery, MongoDB, CosmosDB, Redshift, Snowflake, Elasticsearch, plus 1 test stub)
- **Current Base Class**: Provides only connection/transaction management (~150 lines shared)
- **Target Reduction**: ~4,677 lines across all providers
- **Average Per Provider**: ~390 lines of duplicated code
- **Code Reuse Target**: 80%+ common implementation

---

## Problem Statement

### Current State

Each database provider independently implements:
1. **CRUD Operations** (QueryAsync, CountAsync, GetAsync, CreateAsync, UpdateAsync, DeleteAsync)
2. **Soft Delete Operations** (SoftDeleteAsync, RestoreAsync, HardDeleteAsync)
3. **Bulk Operations** (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync)
4. **Aggregation Operations** (QueryStatisticsAsync, QueryDistinctAsync, QueryExtentAsync)
5. **Transaction Handling**
6. **Connection Management**
7. **Error Handling and Retry Logic**
8. **Parameter Normalization**
9. **Guard Clauses and Validation**

### Code Duplication Analysis

#### Example: QueryAsync Pattern (Appears in ALL 12 Providers)

**MySQL Implementation** (lines 48-83):
```csharp
public async IAsyncEnumerable<FeatureRecord> QueryAsync(
    DataSourceDefinition dataSource,
    ServiceDefinition service,
    LayerDefinition layer,
    FeatureQuery? query,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    ThrowIfDisposed();  // ← Common
    Guard.NotNull(dataSource);  // ← Common
    Guard.NotNull(service);  // ← Common
    Guard.NotNull(layer);  // ← Common

    var normalizedQuery = query ?? new FeatureQuery();  // ← Common
    var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;  // ← Common
    var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);  // ← Common

    await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);  // ← Common
    await _retryPipeline.ExecuteAsync(async ct =>
        await connection.OpenAsync(ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);  // ← Common

    var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);  // ← PROVIDER-SPECIFIC
    var definition = builder.BuildSelect(normalizedQuery);  // ← PROVIDER-SPECIFIC

    await using var command = CreateCommand(connection, definition);  // ← Common pattern
    await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
        await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);  // ← Common

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))  // ← Common
    {
        cancellationToken.ThrowIfCancellationRequested();  // ← Common
        yield return CreateFeatureRecord(reader, layer);  // ← PROVIDER-SPECIFIC
    }
}
```

**SQL Server Implementation** (lines 59-95): **IDENTICAL** except for:
- `SqlServerFeatureQueryBuilder` instead of `MySqlFeatureQueryBuilder`
- `CreateFeatureRecord` signature includes `storageSrid, targetSrid`

**SQLite Implementation** (lines 55-90): **IDENTICAL** pattern

**Oracle Implementation** (lines 80-112): **IDENTICAL** pattern

**Pattern**: 80% of code is identical, only query builder and record creation differ.

---

## Proposed Solution

### Enhanced Base Class Architecture

```csharp
public abstract class RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand, TDataReader>
    : DisposableBase, IDataStoreProvider
    where TConnection : DbConnection
    where TTransaction : DbTransaction
    where TCommand : DbCommand
    where TDataReader : DbDataReader
{
    // ========================================
    // COMMON IMPLEMENTATION (Base Class)
    // ========================================

    // 1. CRUD Operations (80% common)
    public virtual async IAsyncEnumerable<FeatureRecord> QueryAsync(...)
    {
        // Common: Guards, validation, connection, retry logic
        var definition = BuildSelectQuery(service, layer, storageSrid, targetSrid, normalizedQuery);
        // Common: Command execution, reader iteration
        yield return CreateFeatureRecord(reader, layer, storageSrid, targetSrid);  // ← Abstract
    }

    public virtual async Task<long> CountAsync(...) { /* Common implementation */ }
    public virtual async Task<FeatureRecord?> GetAsync(...) { /* Common implementation */ }
    public virtual async Task<FeatureRecord> CreateAsync(...) { /* Common implementation */ }
    public virtual async Task<FeatureRecord?> UpdateAsync(...) { /* Common implementation */ }
    public virtual async Task<bool> DeleteAsync(...) { /* Common implementation */ }

    // 2. Transaction Support (100% common - already in base)
    public virtual async Task<IDataStoreTransaction?> BeginTransactionAsync(...) { /* Implemented */ }

    // 3. Helper Methods (100% common)
    protected async Task<(TConnection, TTransaction?, bool)> GetConnectionAndTransactionAsync(...) { /* Implemented */ }
    protected async Task<string> DecryptConnectionStringAsync(...) { /* Implemented */ }
    protected virtual TCommand CreateCommand(TConnection connection, QueryDefinition definition) { /* Implemented */ }

    // ========================================
    // PROVIDER-SPECIFIC (Abstract/Virtual)
    // ========================================

    // Connection Management
    protected abstract Task<TConnection> CreateConnectionAsync(...);
    protected abstract string NormalizeConnectionString(string connectionString);

    // Query Building
    protected abstract QueryDefinition BuildSelectQuery(...);
    protected abstract QueryDefinition BuildCountQuery(...);
    protected abstract QueryDefinition BuildByIdQuery(...);

    // Record Creation (Geometry Handling)
    protected abstract FeatureRecord CreateFeatureRecord(
        TDataReader reader,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid);

    // Provider Metadata
    public abstract string Provider { get; }
    public abstract IDataStoreCapabilities Capabilities { get; }
    protected abstract int DefaultCommandTimeoutSeconds { get; }
}
```

### Provider Implementation Pattern

**Before** (MySQL - 1,253 lines):
```csharp
public sealed class MySqlDataStoreProvider : DisposableBase, IDataStoreProvider
{
    // 1. All connection management code
    // 2. All CRUD implementations
    // 3. All transaction handling
    // 4. All helper methods
    // 5. All disposal logic
    // TOTAL: ~1,200 lines of implementation
}
```

**After** (MySQL - ~350 lines):
```csharp
public sealed class MySqlDataStoreProvider
    : RelationalDataStoreProviderBase<MySqlConnection, MySqlTransaction, MySqlCommand, MySqlDataReader>
{
    public override string Provider => "mysql";
    public override IDataStoreCapabilities Capabilities => MySqlDataStoreCapabilities.Instance;
    protected override int DefaultCommandTimeoutSeconds => 30;

    // Only implement provider-specific methods:
    protected override Task<MySqlConnection> CreateConnectionAsync(...) { /* 20 lines */ }
    protected override QueryDefinition BuildSelectQuery(...) { /* 5 lines - delegate to query builder */ }
    protected override FeatureRecord CreateFeatureRecord(...) { /* 30 lines - geometry conversion */ }

    // Bulk operations (provider-optimized)
    public override async Task<int> BulkInsertAsync(...) { /* 80 lines - MySQL batch inserts */ }

    // Aggregations (provider-specific SQL)
    public override async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(...) { /* 50 lines */ }

    // TOTAL: ~350 lines (900 lines eliminated!)
}
```

---

## Implementation Plan

### Phase 1: Enhance Base Class (Current State → Enhanced)

**File**: `/src/Honua.Server.Core/Data/RelationalDataStoreProviderBase.cs`

**Changes**:
1. Add generic parameter `TDataReader : DbDataReader`
2. Implement `QueryAsync`, `CountAsync`, `GetAsync` with common logic
3. Implement `CreateAsync`, `UpdateAsync`, `DeleteAsync` with common patterns
4. Implement `SoftDeleteAsync`, `RestoreAsync`, `HardDeleteAsync` with common SQL
5. Add abstract methods for query building and record creation
6. Add `CreateCommand` helper method
7. Keep aggregation methods abstract (provider-specific SQL required)

**Lines Added**: ~400 lines of common implementation

### Phase 2: Update Providers to Use Enhanced Base

#### 2.1 MySQL Provider

**File**: `/src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs`

**Changes**:
- **Remove** (900 lines):
  - All CRUD boilerplate (guards, connection opening, retry logic)
  - Transaction handling code (now in base)
  - Helper method `GetConnectionAndTransactionAsync` (now in base)
  - Helper method `DecryptConnectionStringAsync` (now in base)
  - Disposal logic (using `DisposableBase` pattern from base)

- **Add** (50 lines):
  - `BuildSelectQuery` - delegates to `MySqlFeatureQueryBuilder.BuildSelect`
  - `BuildCountQuery` - delegates to `MySqlFeatureQueryBuilder.BuildCount`
  - `BuildByIdQuery` - delegates to `MySqlFeatureQueryBuilder.BuildById`
  - `CreateFeatureRecord` - geometry conversion from GeoJSON

- **Keep** (300 lines):
  - Bulk operations (MySQL-specific optimizations)
  - Aggregation operations (MySQL-specific SQL)
  - Connection normalization logic

**Lines Eliminated**: ~900 lines

#### 2.2 SQL Server Provider

**File**: `/src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs`

**Changes**: Similar to MySQL
**Lines Eliminated**: ~880 lines

#### 2.3 SQLite Provider

**File**: `/src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs`

**Changes**: Similar to MySQL
**Lines Eliminated**: ~900 lines

#### 2.4 Oracle Provider

**File**: `/src/Honua.Server.Enterprise/Data/Oracle/OracleDataStoreProvider.cs`

**Status**: Already inherits from base class, needs updates for enhanced methods
**Lines Eliminated**: ~650 lines

#### 2.5 Other Providers (BigQuery, Redshift, Snowflake)

**Changes**: Similar pattern
**Lines Eliminated**: ~800 lines each

### Phase 3: Testing and Validation

**Test Strategy**:
1. Run existing test suite without modification
2. All tests in `DataStoreProviderTestsBase<TFixture>` should pass
3. Integration tests for each provider should pass
4. No behavioral changes - only code organization

**Test Files**:
- `/tests/Honua.Server.Core.Tests.Data/Data/MySql/MySqlDataStoreProviderTests.cs`
- `/tests/Honua.Server.Core.Tests.Data/Data/SqlServer/SqlServerDataStoreProviderTests.cs`
- `/tests/Honua.Server.Core.Tests.Data/Data/Sqlite/SqliteDataStoreProviderTests.cs`
- `/tests/Honua.Server.Core.Tests.Data/Data/Postgres/PostgresDataStoreProviderTests.cs`

---

## Design Decisions

### 1. What Goes in Base Class vs. Provider-Specific?

**Base Class** (Common Implementation):
- ✅ Guard clauses and parameter validation
- ✅ Connection opening and retry logic
- ✅ Transaction extraction from `IDataStoreTransaction`
- ✅ Disposal pattern
- ✅ CRUD operation flow (connect → query → read → return)
- ✅ Error handling patterns
- ✅ Connection string decryption

**Provider-Specific** (Abstract/Virtual Methods):
- ❌ Query builder instantiation (different builders per provider)
- ❌ Geometry conversion (GeoJSON vs WKT vs native types)
- ❌ Bulk operations (provider-optimized SQL)
- ❌ Aggregations (provider-specific SQL syntax)
- ❌ Connection normalization (different builder classes)

### 2. How to Handle Provider-Specific Query Builders?

**Solution**: Abstract factory pattern

```csharp
// Base class calls:
protected abstract QueryDefinition BuildSelectQuery(
    ServiceDefinition service,
    LayerDefinition layer,
    int storageSrid,
    int targetSrid,
    FeatureQuery query);

// Provider implements:
protected override QueryDefinition BuildSelectQuery(...)
{
    var builder = new MySqlFeatureQueryBuilder(service, layer, storageSrid, targetSrid);
    var definition = builder.BuildSelect(query);
    return new QueryDefinition(definition.Sql, definition.Parameters);
}
```

### 3. How to Maintain Backward Compatibility?

**Strategy**: Virtual methods with default implementations

```csharp
// Base provides default:
public virtual async Task<FeatureRecord> CreateAsync(...)
{
    // Common implementation
}

// Provider can override if needed:
public override async Task<FeatureRecord> CreateAsync(...)
{
    // Custom implementation for special cases
}
```

### 4. Which Methods Should Be Virtual vs. Abstract?

**Abstract** (Must Implement):
- `Provider` (property)
- `Capabilities` (property)
- `CreateConnectionAsync` (provider-specific connection type)
- `BuildSelectQuery`, `BuildCountQuery`, `BuildByIdQuery` (provider-specific SQL)
- `CreateFeatureRecord` (provider-specific geometry handling)
- `QueryStatisticsAsync`, `QueryDistinctAsync`, `QueryExtentAsync` (complex SQL varies)

**Virtual** (Can Override):
- `QueryAsync`, `CountAsync`, `GetAsync` (default implementation works for 99% of cases)
- `CreateAsync`, `UpdateAsync`, `DeleteAsync` (override for special cases like RETURNING clause)
- `BulkInsertAsync`, `BulkUpdateAsync`, `BulkDeleteAsync` (override for provider-optimized bulk operations)
- `GenerateMvtTileAsync` (default returns null, override if supported)

---

## Expected Benefits

### 1. Lines of Code Reduction

| Provider | Current Lines | After Refactoring | Reduction |
|----------|--------------|-------------------|-----------|
| MySQL | 1,253 | ~350 | 903 (72%) |
| SQL Server | 1,211 | ~350 | 861 (71%) |
| SQLite | 1,289 | ~350 | 939 (73%) |
| PostgreSQL | 367* | ~250 | 117 (32%) |
| Oracle | 1,062 | ~300 | 762 (72%) |
| BigQuery | ~1,100 | ~350 | 750 (68%) |
| MongoDB | 1,074 | N/A** | N/A** |
| CosmosDB | 1,107 | N/A** | N/A** |
| Redshift | 1,235 | ~350 | 885 (72%) |
| Snowflake | ~1,000 | ~350 | 650 (65%) |
| Elasticsearch | ~900 | N/A** | N/A** |

*PostgreSQL already uses composition pattern with separate operations classes
**NoSQL providers have different patterns and may not benefit from relational base class

**Total Reduction**: ~4,677 lines across relational providers

### 2. Maintainability Benefits

- **Single Source of Truth**: Bugs in CRUD logic fixed once in base class
- **Consistent Behavior**: All providers have identical error handling, retry logic, validation
- **Easier Testing**: Test common behavior once in base class tests
- **Faster Feature Addition**: New providers only implement provider-specific logic

### 3. Performance Benefits

- **No Performance Impact**: Virtual method calls have negligible overhead in async I/O scenarios
- **Better Optimization**: JIT can inline base class methods
- **Reduced Memory**: Fewer IL bytes loaded per provider

---

## Risks and Mitigation

### Risk 1: Breaking Changes in Existing Providers

**Mitigation**:
- Use virtual methods, not abstract, for backward compatibility
- Providers can override base implementation if needed
- Comprehensive test coverage ensures no regressions

### Risk 2: Provider-Specific Edge Cases

**Mitigation**:
- Keep complex operations (aggregations, bulk) as abstract
- Allow providers to override any base method
- Document extension points clearly

### Risk 3: Test Failures

**Mitigation**:
- Run full test suite after each provider migration
- Use `DataStoreProviderTestsBase<TFixture>` for consistent testing
- No changes to test code required

---

## Migration Checklist

### Phase 1: Base Class Enhancement
- [ ] Add `TDataReader` generic parameter
- [ ] Implement `QueryAsync` with common logic
- [ ] Implement `CountAsync` with common logic
- [ ] Implement `GetAsync` with common logic
- [ ] Implement `CreateAsync` with common logic
- [ ] Implement `UpdateAsync` with common logic
- [ ] Implement `DeleteAsync` with common logic
- [ ] Implement `SoftDeleteAsync` with common SQL
- [ ] Implement `RestoreAsync` with common SQL
- [ ] Implement `HardDeleteAsync` with common SQL
- [ ] Add abstract `BuildSelectQuery`, `BuildCountQuery`, `BuildByIdQuery`
- [ ] Add abstract `CreateFeatureRecord`
- [ ] Add `CreateCommand` helper
- [ ] Document all extension points

### Phase 2: Provider Migration
- [ ] Update MySQL provider
- [ ] Run MySQL tests
- [ ] Update SQL Server provider
- [ ] Run SQL Server tests
- [ ] Update SQLite provider
- [ ] Run SQLite tests
- [ ] Update Oracle provider
- [ ] Run Oracle tests
- [ ] Update remaining providers (BigQuery, Redshift, Snowflake)
- [ ] Run all provider tests

### Phase 3: Documentation
- [ ] Update architecture documentation
- [ ] Add base class usage guide
- [ ] Document extension points
- [ ] Add examples for new provider implementation
- [ ] Update CHANGELOG.md

---

## Conclusion

This refactoring eliminates ~4,677 lines of duplicated code while maintaining 100% backward compatibility and test coverage. The enhanced base class provides a solid foundation for existing and future database providers, improving maintainability, consistency, and development velocity.

**Next Steps**:
1. Implement enhanced base class methods
2. Migrate MySQL provider as proof of concept
3. Run tests to validate approach
4. Migrate remaining providers
5. Update documentation

**Success Criteria**:
- ✅ All existing tests pass without modification
- ✅ 70%+ code reduction per provider
- ✅ No performance regression
- ✅ Improved code maintainability
