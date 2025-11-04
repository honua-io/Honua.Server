# CRUD Operation Consolidation Analysis Report

**Date:** October 31, 2025
**Scope:** Data Store Provider CRUD Operations
**Providers Analyzed:** MySQL, SQL Server, SQLite, PostgreSQL, Oracle

---

## Executive Summary

This analysis examines CRUD operation patterns across five data store providers to identify opportunities for code consolidation. The findings reveal significant duplication across providers, with an estimated **2,100-2,400 lines of duplicate code** that could be consolidated into base class methods and shared utilities.

**Key Finding:** PostgreSQL has already implemented the recommended pattern by delegating to specialized operation classes (PostgresFeatureOperations, PostgresBulkOperations), demonstrating a successful consolidation approach.

---

## 1. Common Patterns Across All Providers

### 1.1 Connection Management Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~60-80 lines
**Total duplicate code:** ~300-400 lines

**Pattern Structure:**
```csharp
// Connection creation with encryption support
private async Task<TConnection> CreateConnectionAsync(
    DataSourceDefinition dataSource,
    CancellationToken cancellationToken)
{
    if (dataSource.ConnectionString.IsNullOrWhiteSpace())
    {
        throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
    }

    var decryptedConnectionString = await DecryptConnectionStringAsync(
        dataSource.ConnectionString,
        cancellationToken).ConfigureAwait(false);

    var connectionString = NormalizeConnectionString(decryptedConnectionString);
    return CreateConnectionCore(connectionString);
}

// Connection string decryption caching
private async Task<string> DecryptConnectionStringAsync(
    string connectionString,
    CancellationToken cancellationToken = default)
{
    if (_encryptionService == null)
    {
        return connectionString;
    }

    return await _decryptionCache.GetOrAdd(connectionString,
        async cs => await _encryptionService.DecryptAsync(cs, cancellationToken)
            .ConfigureAwait(false)).ConfigureAwait(false);
}
```

**Providers with this pattern:**
- **MySqlDataStoreProvider** (lines 1139-1187): Includes connection pooling via MySqlDataSource
- **SqlServerDataStoreProvider** (lines 1080-1126): Uses SqlConnectionStringBuilder caching
- **SqliteDataStoreProvider** (lines 1083-1107): Simplified version without pooling
- **OracleDataStoreProvider**: Inherits from RelationalDataStoreProviderBase (already consolidated)
- **PostgresDataStoreProvider**: Delegates to PostgresConnectionManager (already consolidated)

---

### 1.2 Transaction Management Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~80-100 lines
**Total duplicate code:** ~320-400 lines

**Pattern Structure:**
```csharp
// Transaction extraction and connection handling
TConnection? connection = null;
TTransaction? transaction = null;
var shouldDisposeConnection = true;

if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> txWrapper)
{
    connection = txWrapper.Connection;
    transaction = txWrapper.Transaction;
    shouldDisposeConnection = false; // Transaction owns the connection
}
else
{
    connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
    await _retryPipeline.ExecuteAsync(async ct =>
        await connection.OpenAsync(ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);
}

try
{
    // Operation logic here
}
finally
{
    if (shouldDisposeConnection && connection != null)
    {
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
```

**Found in methods:**
- CreateAsync (all providers)
- UpdateAsync (all providers)
- DeleteAsync (all providers)
- SoftDeleteAsync (MySQL, SQL Server, SQLite)
- RestoreAsync (MySQL, SQL Server, SQLite)
- HardDeleteAsync (MySQL, SQL Server, SQLite)

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Lines 177-244 (CreateAsync), 268-324 (UpdateAsync), 345-382 (DeleteAsync), 402-446 (SoftDeleteAsync), 465-508 (RestoreAsync), 528-565 (HardDeleteAsync)
- **SqlServerDataStoreProvider**: Lines 192-250 (CreateAsync), 274-332 (UpdateAsync), 353-391 (DeleteAsync), 411-456 (SoftDeleteAsync), 475-519 (RestoreAsync), 539-577 (HardDeleteAsync)
- **SqliteDataStoreProvider**: Lines 180-244 (CreateAsync), 267-335 (UpdateAsync), 355-391 (DeleteAsync), 411-454 (SoftDeleteAsync), 473-515 (RestoreAsync), 535-571 (HardDeleteAsync)
- **OracleDataStoreProvider**: Uses base class (already consolidated)
- **PostgresFeatureOperations**: Lines 200-274 (CreateAsync), 300-407 (UpdateAsync), 437-477 (DeleteAsync) - slightly different but similar pattern

---

### 1.3 Query Execution Pattern (QueryAsync, CountAsync, GetAsync)

**Occurrences:** All 5 providers
**Lines per provider:** ~120-150 lines
**Total duplicate code:** ~480-600 lines

**Pattern Structure:**
```csharp
public async IAsyncEnumerable<FeatureRecord> QueryAsync(...)
{
    ThrowIfDisposed();
    Guard.NotNull(dataSource);
    Guard.NotNull(service);
    Guard.NotNull(layer);

    var normalizedQuery = query ?? new FeatureQuery();
    var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
    var targetSrid = CrsHelper.ParseCrs(normalizedQuery.Crs ?? service.Ogc.DefaultCrs);

    await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
    await _retryPipeline.ExecuteAsync(async ct =>
        await connection.OpenAsync(ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    var builder = CreateQueryBuilder(service, layer, storageSrid, targetSrid);
    var definition = builder.BuildSelect(normalizedQuery);

    await using var command = CreateCommand(connection, definition);
    await using var reader = await _retryPipeline.ExecuteAsync(async ct =>
        await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return CreateFeatureRecord(reader, layer);
    }
}
```

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Lines 45-80 (QueryAsync), 82-117 (CountAsync), 119-156 (GetAsync)
- **SqlServerDataStoreProvider**: Lines 56-92 (QueryAsync), 94-130 (CountAsync), 132-171 (GetAsync)
- **SqliteDataStoreProvider**: Lines 52-87 (QueryAsync), 89-121 (CountAsync), 123-160 (GetAsync)
- **OracleDataStoreProvider**: Lines 73-105 (QueryAsync), 107-135 (CountAsync), 137-173 (GetAsync)
- **PostgresFeatureOperations**: Lines 40-89 (QueryAsync), 91-136 (CountAsync), 138-180 (GetAsync) - uses query builder pool

---

### 1.4 Bulk Operations Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~180-250 lines
**Total duplicate code:** ~720-1000 lines

**Pattern Structure:**
```csharp
public async Task<int> BulkInsertAsync(...)
{
    ThrowIfDisposed();
    Guard.NotNull(dataSource);
    Guard.NotNull(service);
    Guard.NotNull(layer);
    Guard.NotNull(records);

    await using var connection = await CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
    await _retryPipeline.ExecuteAsync(async ct =>
        await connection.OpenAsync(ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    var count = 0;
    var batch = new List<FeatureRecord>();

    await foreach (var record in records.WithCancellation(cancellationToken))
    {
        batch.Add(record);

        if (batch.Count >= BulkBatchSize)
        {
            count += await ExecuteInsertBatchAsync(connection, table, layer, batch, srid, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
    }

    // Process remaining items
    if (batch.Count > 0)
    {
        count += await ExecuteInsertBatchAsync(connection, table, layer, batch, srid, cancellationToken).ConfigureAwait(false);
    }

    return count;
}
```

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Lines 567-701 (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync + batch execution methods)
- **SqlServerDataStoreProvider**: Lines 579-886 (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync + batch execution methods)
- **SqliteDataStoreProvider**: Lines 573-849 (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync + batch execution methods)
- **OracleDataStoreProvider**: Lines 307-636 (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync + array binding methods)
- **PostgresDataStoreProvider**: Delegates to PostgresBulkOperations class (already consolidated)

---

### 1.5 Error Handling and Retry Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~40-60 lines
**Total duplicate code:** ~160-240 lines

**Pattern Structure:**
```csharp
// Retry pipeline creation
_retryPipeline = DatabaseRetryPolicy.Create[Provider]RetryPipeline();

// Usage in operations
await _retryPipeline.ExecuteAsync(async ct =>
    await connection.OpenAsync(ct).ConfigureAwait(false),
    cancellationToken).ConfigureAwait(false);

var result = await _retryPipeline.ExecuteAsync(async ct =>
    await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
    cancellationToken).ConfigureAwait(false);
```

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Uses retry pipeline throughout (lines 63-65, 100-102, 138-140, etc.)
- **SqlServerDataStoreProvider**: Uses retry pipeline throughout (lines 74-76, 112-114, 152-154, etc.)
- **SqliteDataStoreProvider**: Uses retry pipeline throughout (lines 70-72, 104-106, 142-144, etc.)
- **OracleDataStoreProvider**: Inherits retry pipeline from base class
- **PostgresConnectionManager**: Centralizes retry pipeline usage

---

### 1.6 Parameter Handling Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~50-80 lines
**Total duplicate code:** ~200-320 lines

**Pattern Structure:**
```csharp
private static void AddParameters(TCommand command, IReadOnlyDictionary<string, object?> parameters)
{
    foreach (var pair in parameters)
    {
        if (command.Parameters.Contains(pair.Key))
        {
            command.Parameters[pair.Key].Value = pair.Value ?? DBNull.Value;
        }
        else
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = pair.Key;
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}

private static object NormalizeKeyParameter(LayerDefinition layer, string featureId)
{
    var field = layer.Fields.FirstOrDefault(f => string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase));
    var hint = field?.DataType ?? field?.StorageType;
    if (hint.IsNullOrWhiteSpace())
    {
        return featureId;
    }

    return hint.Trim().ToLowerInvariant() switch
    {
        "int" or "int32" or "integer" or "smallint" or "int16" => int.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : featureId,
        "long" or "int64" or "bigint" => long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : featureId,
        "double" or "float" or "real" => double.TryParse(featureId, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ? d : featureId,
        "decimal" or "numeric" => decimal.TryParse(featureId, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : featureId,
        "guid" or "uuid" or "uniqueidentifier" => Guid.TryParse(featureId, out var g) ? g : featureId,
        _ => featureId
    };
}
```

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Lines 1328-1344 (AddParameters), 1292-1310 (NormalizeKeyParameter)
- **SqlServerDataStoreProvider**: Lines 1277-1293 (AddParameters), 1241-1259 (NormalizeKeyParameter)
- **SqliteDataStoreProvider**: Lines 1385-1394 (AddParameters - simpler version)
- **OracleDataStoreProvider**: Lines 695-704 (AddParameters), 472-492 (MapToOracleDbType)
- **PostgresRecordMapper**: Similar functionality in a separate class (already consolidated)

---

### 1.7 Record Normalization Pattern

**Occurrences:** All 5 providers
**Lines per provider:** ~80-120 lines
**Total duplicate code:** ~320-480 lines

**Pattern Structure:**
```csharp
private static NormalizedRecord NormalizeRecord(
    LayerDefinition layer,
    IReadOnlyDictionary<string, object?> attributes,
    bool includeKey)
{
    var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
    var keyColumn = layer.Storage?.PrimaryKey ?? layer.IdField;
    var geometryColumn = layer.Storage?.GeometryColumn ?? layer.GeometryField;

    var columns = new List<NormalizedColumn>();
    var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
    var index = 0;

    foreach (var pair in attributes)
    {
        var columnName = pair.Key;
        if (!includeKey && string.Equals(columnName, keyColumn, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var isGeometry = string.Equals(columnName, geometryColumn, StringComparison.OrdinalIgnoreCase);
        var value = NormalizeValue(pair.Value, isGeometry);
        if (isGeometry && value is null)
        {
            continue;
        }

        var parameterName = $"@p{index++}";
        columns.Add(new NormalizedColumn(QuoteIdentifier(columnName), parameterName, value, isGeometry));
        parameters[parameterName] = value ?? DBNull.Value;
    }

    return new NormalizedRecord(columns, parameters, srid);
}
```

**Providers with this pattern:**
- **MySqlDataStoreProvider**: Lines 1212-1243 (NormalizeRecord), 1245-1271 (NormalizeValue)
- **SqlServerDataStoreProvider**: Lines 1145-1176 (NormalizeRecord), 1178-1205 (NormalizeValue)
- **SqliteDataStoreProvider**: Lines 1262-1286 (NormalizeRecord), 1347-1367 (NormalizeValue)
- **OracleDataStoreProvider**: Builds records inline in ExecuteArrayInsertAsync/UpdateAsync
- **PostgresRecordMapper**: Separate utility class (already consolidated)

---

## 2. Duplicate Code Block Analysis

### 2.1 High-Priority Duplicates (>100 lines)

#### 2.1.1 Transaction-Aware CRUD Operations

**Location:**
- MySqlDataStoreProvider: CreateAsync (lines 158-245), UpdateAsync (lines 247-324), DeleteAsync (lines 326-382)
- SqlServerDataStoreProvider: CreateAsync (lines 173-251), UpdateAsync (lines 253-332), DeleteAsync (lines 334-391)
- SqliteDataStoreProvider: CreateAsync (lines 162-245), UpdateAsync (lines 247-335), DeleteAsync (lines 337-391)

**Duplicate Code Lines:** ~270 lines per provider × 3 providers = **~810 lines**

**Consolidation Opportunity:**
```csharp
protected abstract class RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>
{
    protected async Task<FeatureRecord> ExecuteTransactionalCreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var table = ResolveTableName(layer);
        var keyColumn = ResolvePrimaryKey(layer);

        var (connection, dbTransaction, shouldDispose) = await ExtractOrCreateConnectionAsync(
            dataSource, transaction, cancellationToken);

        try
        {
            var normalized = NormalizeRecord(layer, record.Attributes, includeKey: true);
            if (normalized.Columns.Count == 0)
            {
                throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
            }

            var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
            var valueList = string.Join(", ", normalized.Columns.Select(c => BuildValueExpression(c, normalized.Srid)));

            var keyValue = await ExecuteInsertWithReturnAsync(
                connection, dbTransaction, table, columnList, valueList,
                keyColumn, normalized.Parameters, cancellationToken);

            return await GetAsync(dataSource, service, layer, keyValue, null, cancellationToken)
                   ?? throw new InvalidOperationException("Failed to load feature after insert.");
        }
        finally
        {
            if (shouldDispose)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
```

**Estimated Line Savings:** ~650-700 lines

---

#### 2.1.2 Soft Delete Operations

**Location:**
- MySqlDataStoreProvider: SoftDeleteAsync (lines 384-446), RestoreAsync (lines 448-508), HardDeleteAsync (lines 510-565)
- SqlServerDataStoreProvider: SoftDeleteAsync (lines 393-456), RestoreAsync (lines 458-519), HardDeleteAsync (lines 521-577)
- SqliteDataStoreProvider: SoftDeleteAsync (lines 393-454), RestoreAsync (lines 456-515), HardDeleteAsync (lines 517-571)

**Duplicate Code Lines:** ~180 lines per provider × 3 providers = **~540 lines**

**Consolidation Opportunity:**
```csharp
protected async Task<bool> ExecuteSoftDeleteAsync(
    DataSourceDefinition dataSource,
    LayerDefinition layer,
    string featureId,
    string? deletedBy,
    IDataStoreTransaction? transaction,
    CancellationToken cancellationToken)
{
    var table = ResolveTableName(layer);
    var keyColumn = ResolvePrimaryKey(layer);

    var (connection, dbTransaction, shouldDispose) = await ExtractOrCreateConnectionAsync(
        dataSource, transaction, cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = BuildSoftDeleteSql(table, keyColumn);
        command.Parameters.AddWithValue("@key", NormalizeKeyParameter(layer, featureId));
        command.Parameters.AddWithValue("@deletedBy", (object?)deletedBy ?? DBNull.Value);
        command.CommandTimeout = DefaultCommandTimeoutSeconds;

        var affected = await _retryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return affected > 0;
    }
    finally
    {
        if (shouldDispose)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

// Provider-specific SQL generation
protected abstract string BuildSoftDeleteSql(string table, string keyColumn);
protected abstract string BuildRestoreSql(string table, string keyColumn);
protected abstract string GetCurrentTimestampExpression();
```

**Estimated Line Savings:** ~420-450 lines

---

#### 2.1.3 Bulk Operation Batching Logic

**Location:**
- MySqlDataStoreProvider: BulkInsertAsync (lines 567-610), BulkUpdateAsync (lines 612-656), BulkDeleteAsync (lines 658-701)
- SqlServerDataStoreProvider: BulkInsertAsync (lines 579-663), BulkUpdateAsync (lines 665-710), BulkDeleteAsync (lines 712-755)
- SqliteDataStoreProvider: BulkInsertAsync (lines 573-613), BulkUpdateAsync (lines 615-655), BulkDeleteAsync (lines 657-697)

**Duplicate Code Lines:** ~180 lines per provider × 3 providers = **~540 lines**

**Consolidation Opportunity:**
```csharp
protected async Task<int> ExecuteBulkOperationAsync<T>(
    DataSourceDefinition dataSource,
    LayerDefinition layer,
    IAsyncEnumerable<T> items,
    Func<TConnection, LayerDefinition, List<T>, CancellationToken, Task<int>> batchExecutor,
    int batchSize,
    CancellationToken cancellationToken)
{
    await using var connection = await CreateConnectionAsync(dataSource, cancellationToken);
    await _retryPipeline.ExecuteAsync(async ct =>
        await connection.OpenAsync(ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    var count = 0;
    var batch = new List<T>(batchSize);

    await foreach (var item in items.WithCancellation(cancellationToken))
    {
        batch.Add(item);

        if (batch.Count >= batchSize)
        {
            count += await batchExecutor(connection, layer, batch, cancellationToken);
            batch.Clear();
        }
    }

    if (batch.Count > 0)
    {
        count += await batchExecutor(connection, layer, batch, cancellationToken);
    }

    return count;
}
```

**Estimated Line Savings:** ~420-450 lines

---

### 2.2 Medium-Priority Duplicates (50-100 lines)

#### 2.2.1 Connectivity Testing

**Location:**
- MySqlDataStoreProvider: TestConnectivityAsync (lines 1065-1084)
- SqlServerDataStoreProvider: TestConnectivityAsync (lines 983-1002)
- SqliteDataStoreProvider: TestConnectivityAsync (lines 1058-1081)
- OracleDataStoreProvider: Inherits from base (already consolidated)

**Duplicate Code Lines:** ~20 lines per provider × 3 providers = **~60 lines**

**Consolidation Opportunity:**
```csharp
public virtual async Task TestConnectivityAsync(
    DataSourceDefinition dataSource,
    CancellationToken cancellationToken = default)
{
    Guard.NotNull(dataSource);

    if (dataSource.ConnectionString.IsNullOrWhiteSpace())
    {
        throw new InvalidOperationException($"Data source '{dataSource.Id}' has no connection string configured.");
    }

    await using var connection = await CreateConnectionAsync(dataSource, cancellationToken);
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = GetConnectivityTestQuery();
    command.CommandTimeout = 5; // 5 second timeout for health checks

    await command.ExecuteScalarAsync(cancellationToken);
}

protected abstract string GetConnectivityTestQuery();
```

**Estimated Line Savings:** ~45-50 lines

---

#### 2.2.2 Transaction Initialization

**Location:**
- MySqlDataStoreProvider: BeginTransactionAsync (lines 870-902)
- SqlServerDataStoreProvider: BeginTransactionAsync (lines 902-934)
- SqliteDataStoreProvider: BeginTransactionAsync (lines 865-897)

**Duplicate Code Lines:** ~30 lines per provider × 3 providers = **~90 lines**

**Consolidation Opportunity:**
```csharp
public virtual async Task<IDataStoreTransaction?> BeginTransactionAsync(
    DataSourceDefinition dataSource,
    CancellationToken cancellationToken = default)
{
    ThrowIfDisposed();

    TConnection? connection = null;
    try
    {
        connection = await CreateConnectionAsync(dataSource, cancellationToken);
        await _retryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var transaction = await connection.BeginTransactionAsync(
            GetTransactionIsolationLevel(),
            cancellationToken).ConfigureAwait(false);

        return new RelationalDataStoreTransaction<TConnection, TTransaction>(connection, transaction);
    }
    catch
    {
        if (connection != null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        throw;
    }
}

protected virtual System.Data.IsolationLevel GetTransactionIsolationLevel()
    => System.Data.IsolationLevel.RepeatableRead;
```

**Estimated Line Savings:** ~70-75 lines

---

## 3. Opportunities for Base Class Methods

### 3.1 Recommended Base Class: RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>

**Status:** Already exists and used by OracleDataStoreProvider

**Recommended Extensions:**

#### 3.1.1 Connection and Transaction Management
```csharp
public abstract class RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>
    : IDataStoreProvider, IDisposable, IAsyncDisposable
    where TConnection : DbConnection
    where TTransaction : DbTransaction
    where TCommand : DbCommand
{
    // Core infrastructure
    protected readonly ResiliencePipeline RetryPipeline;
    protected readonly IConnectionStringEncryptionService? EncryptionService;
    protected readonly ConcurrentDictionary<string, Task<string>> DecryptionCache = new();

    // Abstract methods for provider-specific behavior
    protected abstract TConnection CreateConnectionCore(string connectionString);
    protected abstract string NormalizeConnectionString(string connectionString);
    protected abstract string GetConnectivityTestQuery();
    protected abstract string QuoteIdentifier(string identifier);

    // Common CRUD operations with transaction support
    protected async Task<(TConnection connection, TTransaction? transaction, bool shouldDispose)>
        ExtractOrCreateConnectionAsync(
            DataSourceDefinition dataSource,
            IDataStoreTransaction? transaction,
            CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> txWrapper)
        {
            return (txWrapper.Connection, txWrapper.Transaction, false);
        }

        var connection = await CreateConnectionAsync(dataSource, cancellationToken);
        await RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return (connection, null, true);
    }

    // Connection creation with decryption and caching
    protected async Task<TConnection> CreateConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken)
    {
        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var decrypted = await DecryptConnectionStringAsync(
            dataSource.ConnectionString, cancellationToken);
        var normalized = NormalizeConnectionString(decrypted);

        return CreateConnectionCore(normalized);
    }

    // Connectivity testing
    public virtual async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        /* Implementation as shown in 2.2.1 */
    }

    // Transaction management
    public virtual async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        /* Implementation as shown in 2.2.2 */
    }
}
```

**Implementation Complexity:** Medium
**Estimated Line Savings:** ~150-200 lines per provider
**Total Savings:** ~450-600 lines

---

### 3.2 Recommended Utility Class: RelationalCrudHelper<TConnection, TTransaction, TCommand>

**Purpose:** Extract common CRUD operation patterns into reusable helper methods

```csharp
public static class RelationalCrudHelper<TConnection, TTransaction, TCommand>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
    where TCommand : DbCommand
{
    public static async Task<FeatureRecord> ExecuteCreateAsync(
        TConnection connection,
        TTransaction? transaction,
        LayerDefinition layer,
        FeatureRecord record,
        ResiliencePipeline retryPipeline,
        Func<LayerDefinition, string> tableResolver,
        Func<LayerDefinition, string> keyResolver,
        Func<LayerDefinition, IReadOnlyDictionary<string, object?>, bool, NormalizedRecord> recordNormalizer,
        Func<NormalizedColumn, int, string> valueExpressionBuilder,
        Func<string, string> identifierQuoter,
        Func<DataSourceDefinition, ServiceDefinition, LayerDefinition, string, FeatureQuery?, CancellationToken, Task<FeatureRecord?>> getAsync,
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        CancellationToken cancellationToken)
    {
        var table = tableResolver(layer);
        var keyColumn = keyResolver(layer);

        var normalized = recordNormalizer(layer, record.Attributes, true);
        if (normalized.Columns.Count == 0)
        {
            throw new InvalidOperationException($"Create operation for layer '{layer.Id}' did not include any columns.");
        }

        var columnList = string.Join(", ", normalized.Columns.Select(c => c.ColumnName));
        var valueList = string.Join(", ", normalized.Columns.Select(c => valueExpressionBuilder(c, normalized.Srid)));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BuildInsertSql(table, columnList, valueList, identifierQuoter(keyColumn));
        AddParameters(command, normalized.Parameters);

        var keyValue = await ExecuteInsertAndGetKeyAsync(command, retryPipeline, cancellationToken);

        return await getAsync(dataSource, service, layer, keyValue, null, cancellationToken)
               ?? throw new InvalidOperationException("Failed to load feature after insert.");
    }

    // Similar methods for Update, Delete, SoftDelete, Restore, HardDelete
}
```

**Implementation Complexity:** Medium-High
**Estimated Line Savings:** ~200-300 lines per provider
**Total Savings:** ~600-900 lines

---

### 3.3 Recommended Utility Class: BulkOperationHelper<TConnection>

**Purpose:** Extract common bulk operation batching logic

```csharp
public static class BulkOperationHelper<TConnection> where TConnection : DbConnection
{
    public static async Task<int> ExecuteBatchedOperationAsync<T>(
        TConnection connection,
        IAsyncEnumerable<T> items,
        Func<TConnection, List<T>, CancellationToken, Task<int>> batchExecutor,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var count = 0;
        var batch = new List<T>(batchSize);

        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                count += await batchExecutor(connection, batch, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await batchExecutor(connection, batch, cancellationToken);
        }

        return count;
    }
}
```

**Implementation Complexity:** Low
**Estimated Line Savings:** ~50-80 lines per provider
**Total Savings:** ~150-240 lines

---

## 4. Provider-Specific Variations That Need Parameterization

### 4.1 SQL Dialect Differences

#### 4.1.1 Identity/Sequence Retrieval

**MySQL:**
```sql
SELECT LAST_INSERT_ID()
```

**SQL Server:**
```sql
INSERT INTO table (...) OUTPUT inserted.id VALUES (...)
```

**SQLite:**
```sql
SELECT last_insert_rowid()
```

**PostgreSQL:**
```sql
INSERT INTO table (...) VALUES (...) RETURNING id
```

**Oracle:**
```sql
INSERT INTO table (...) VALUES (...) RETURNING id INTO :out_id
```

**Consolidation Strategy:**
```csharp
protected abstract string BuildInsertWithReturnSql(
    string table,
    string columnList,
    string valueList,
    string keyColumn);

protected abstract Task<string> ExecuteInsertAndGetKeyAsync(
    TCommand command,
    CancellationToken cancellationToken);
```

---

#### 4.1.2 Geometry Function Differences

**MySQL:**
```sql
ST_GeomFromGeoJSON(@p0, NULL, 4326)
```

**SQL Server:**
```sql
geometry::STGeomFromText(@p0, 4326)
-- OR
geography::STGeomFromText(@p0, 4326)
```

**SQLite:**
```sql
-- Stores WKT directly, no special function
@p0
```

**PostgreSQL:**
```sql
ST_GeomFromGeoJSON(@p0)
```

**Oracle:**
```sql
SDO_UTIL.FROM_GEOJSON(@p0)
```

**Consolidation Strategy:**
```csharp
protected abstract string BuildGeometryValueExpression(
    string parameterName,
    int srid,
    bool isGeography = false);
```

---

#### 4.1.3 Date/Time Functions

**MySQL:**
```sql
NOW()
```

**SQL Server:**
```sql
GETUTCDATE()
```

**SQLite:**
```sql
datetime('now')
```

**PostgreSQL:**
```sql
NOW()
```

**Oracle:**
```sql
SYSTIMESTAMP
```

**Consolidation Strategy:**
```csharp
protected abstract string GetCurrentTimestampExpression();
```

---

#### 4.1.4 Boolean Representation

**MySQL:**
```sql
is_deleted = true
is_deleted = false
```

**SQL Server:**
```sql
is_deleted = 1
is_deleted = 0
```

**SQLite:**
```sql
is_deleted = 1
is_deleted = 0
```

**PostgreSQL:**
```sql
is_deleted = true
is_deleted = false
```

**Oracle:**
```sql
is_deleted = 1
is_deleted = 0
```

**Consolidation Strategy:**
```csharp
protected abstract string GetBooleanTrueValue();
protected abstract string GetBooleanFalseValue();
```

---

### 4.2 Connection Pooling Differences

**MySQL:** Uses MySqlDataSource (ADO.NET DataSource pattern)
**SQL Server:** Uses SqlConnectionStringBuilder with pooling configuration
**SQLite:** No connection pooling (file-based)
**PostgreSQL:** Uses NpgsqlDataSource (ADO.NET DataSource pattern)
**Oracle:** Uses OracleConnectionStringBuilder with specific Oracle pooling parameters

**Consolidation Strategy:**
```csharp
protected abstract class ConnectionPoolConfiguration
{
    public virtual bool EnablePooling => true;
    public virtual int MinPoolSize => 2;
    public virtual int MaxPoolSize => 50;
    public virtual int ConnectionLifetime => 600;
    public virtual int ConnectionTimeout => 15;
}

protected abstract ConnectionPoolConfiguration GetPoolConfiguration();
```

---

### 4.3 Parameter Binding Differences

**MySQL, SQL Server, SQLite, PostgreSQL:** Standard parameter binding
**Oracle:** Supports array binding for bulk operations

**Consolidation Strategy:**
```csharp
protected virtual bool SupportsArrayBinding => false;

protected virtual async Task<int> ExecuteBulkInsertAsync(
    TConnection connection,
    LayerDefinition layer,
    List<FeatureRecord> batch,
    CancellationToken cancellationToken)
{
    if (SupportsArrayBinding)
    {
        return await ExecuteArrayBindingInsertAsync(connection, layer, batch, cancellationToken);
    }
    else
    {
        return await ExecuteStandardBulkInsertAsync(connection, layer, batch, cancellationToken);
    }
}
```

---

### 4.4 Geometry Storage Differences

**MySQL:** Uses GEOMETRY columns with ST_* functions
**SQL Server:** Uses GEOMETRY or GEOGRAPHY columns with different spatial types
**SQLite:** Stores as WKT or GeoJSON text
**PostgreSQL:** Uses PostGIS GEOMETRY columns
**Oracle:** Uses SDO_GEOMETRY type

**Consolidation Strategy:**
```csharp
protected abstract class GeometryHandling
{
    public abstract bool RequiresGeometryTypeDetection { get; }
    public abstract string GeometryToGeoJsonExpression(string columnName);
    public abstract string GeoJsonToGeometryExpression(string parameterName, int srid);
}

protected abstract GeometryHandling GetGeometryHandling();
```

---

## 5. Consolidation Strategies

### 5.1 Strategy 1: Extend RelationalDataStoreProviderBase (RECOMMENDED)

**Approach:**
1. Enhance existing `RelationalDataStoreProviderBase<TConnection, TTransaction, TCommand>` with common CRUD patterns
2. Migrate MySqlDataStoreProvider, SqlServerDataStoreProvider, and SqliteDataStoreProvider to inherit from it
3. Extract provider-specific SQL generation into abstract methods
4. Keep OracleDataStoreProvider as-is (already uses this pattern)

**Advantages:**
- Builds on existing architecture (OracleDataStoreProvider already uses this)
- Minimal disruption to existing code
- Provider-specific optimizations can still be implemented
- Clear separation between common logic and provider-specific SQL

**Disadvantages:**
- Requires refactoring three existing providers
- May introduce temporary breaking changes during migration
- Base class could become complex with many abstract methods

**Estimated Line Savings:** 1,200-1,500 lines
**Implementation Complexity:** Medium
**Risk Level:** Low-Medium

---

### 5.2 Strategy 2: Delegate to Specialized Operation Classes (PostgreSQL Pattern)

**Approach:**
1. Follow PostgreSQL's pattern: separate concerns into specialized classes
   - `ConnectionManager` - Connection pooling and creation
   - `FeatureOperations` - Single-record CRUD
   - `BulkOperations` - Batch operations
   - `RecordMapper` - Record transformation utilities
2. Each provider creates its own set of operation classes
3. Provider class becomes a thin orchestrator

**Advantages:**
- Excellent separation of concerns
- Each operation class can be independently tested
- Proven pattern (already working in PostgreSQL)
- Easier to maintain and extend
- Supports different optimization strategies per provider

**Disadvantages:**
- More classes to maintain
- Higher initial implementation cost
- More complex for simple providers like SQLite

**Estimated Line Savings:** 800-1,000 lines (but better organized)
**Implementation Complexity:** High
**Risk Level:** Medium

**Example Structure:**
```
MySql/
  MySqlDataStoreProvider.cs (orchestrator)
  MySqlConnectionManager.cs
  MySqlFeatureOperations.cs
  MySqlBulkOperations.cs
  MySqlRecordMapper.cs

SqlServer/
  SqlServerDataStoreProvider.cs (orchestrator)
  SqlServerConnectionManager.cs
  SqlServerFeatureOperations.cs
  SqlServerBulkOperations.cs
  SqlServerRecordMapper.cs
```

---

### 5.3 Strategy 3: Create Shared Utility Classes

**Approach:**
1. Extract common patterns into static utility classes
2. Keep provider implementations but call utility methods
3. Lower-risk, incremental approach

**Advantages:**
- Minimal disruption to existing code
- Can be done incrementally
- Easy to adopt and test
- No inheritance complexity

**Disadvantages:**
- Less line savings than other strategies
- Utility method signatures may become complex
- Doesn't enforce consistency across providers

**Estimated Line Savings:** 400-600 lines
**Implementation Complexity:** Low
**Risk Level:** Low

**Example Utilities:**
```csharp
RelationalTransactionHelper.cs
RelationalBulkOperationHelper.cs
RelationalParameterHelper.cs
RelationalRecordNormalizer.cs
```

---

### 5.4 Strategy 4: Hybrid Approach (RECOMMENDED FOR GRADUAL MIGRATION)

**Approach:**
1. **Phase 1:** Create shared utility classes (Strategy 3)
2. **Phase 2:** Enhance RelationalDataStoreProviderBase with common patterns (Strategy 1)
3. **Phase 3:** Optionally refactor complex providers to use specialized classes (Strategy 2)

**Advantages:**
- Lowest risk - incremental improvements
- Can measure impact at each phase
- Backwards compatible at each step
- Provides immediate value with utilities

**Disadvantages:**
- Takes longer to achieve full consolidation
- May result in mixed patterns during transition

**Implementation Roadmap:**

**Phase 1: Utility Classes (1-2 weeks)**
- Create `RelationalTransactionHelper`
- Create `RelationalParameterHelper`
- Create `RelationalBulkOperationHelper`
- Update MySQL, SQL Server, SQLite to use utilities
- **Estimated savings:** 400-600 lines

**Phase 2: Base Class Enhancement (2-3 weeks)**
- Enhance `RelationalDataStoreProviderBase` with common CRUD
- Migrate MySQL to inherit from base
- Migrate SQL Server to inherit from base
- Migrate SQLite to inherit from base
- **Estimated savings:** 800-1,000 lines (cumulative: 1,200-1,600)

**Phase 3: Optional Specialized Classes (3-4 weeks, as needed)**
- For providers with complex logic, refactor to specialized classes
- PostgreSQL already done
- Consider for SQL Server (geometry type detection complexity)
- **Estimated savings:** Better organization, not necessarily fewer lines

**Total Estimated Savings:** 1,200-1,600 lines
**Total Implementation Time:** 6-9 weeks
**Risk Level:** Low

---

## 6. Detailed Consolidation Summary

### 6.1 Total Duplicate Code Analysis

| Pattern | Lines per Provider | Providers | Total Duplicate Lines |
|---------|-------------------|-----------|----------------------|
| Connection Management | 60-80 | 3* | 180-240 |
| Transaction Management | 80-100 | 3 | 240-300 |
| Query Execution | 120-150 | 3* | 360-450 |
| Bulk Operations | 180-250 | 3* | 540-750 |
| Error Handling | 40-60 | 3 | 120-180 |
| Parameter Handling | 50-80 | 3 | 150-240 |
| Record Normalization | 80-120 | 3 | 240-360 |
| **TOTAL** | | | **1,830-2,520** |

*Excludes PostgreSQL (already consolidated) and Oracle (uses base class)

### 6.2 Consolidation Impact by Strategy

| Strategy | Line Savings | Implementation Weeks | Risk | Recommendation |
|----------|-------------|---------------------|------|----------------|
| Strategy 1: Base Class | 1,200-1,500 | 4-5 | Low-Medium | Good for immediate impact |
| Strategy 2: Specialized Classes | 800-1,000 | 6-8 | Medium | Best long-term architecture |
| Strategy 3: Utility Classes | 400-600 | 1-2 | Low | Quick wins, low risk |
| Strategy 4: Hybrid | 1,200-1,600 | 6-9 | Low | **RECOMMENDED** |

### 6.3 Recommended Implementation Priority

**High Priority (Do First):**
1. Create `RelationalTransactionHelper` - handles transaction extraction pattern (saves ~240-300 lines)
2. Create `RelationalBulkOperationHelper` - handles batching logic (saves ~400-600 lines)
3. Enhance `RelationalDataStoreProviderBase` with connectivity testing (saves ~50-60 lines)

**Medium Priority (Do Second):**
4. Add soft delete methods to base class (saves ~400-500 lines)
5. Extract parameter handling to utilities (saves ~150-240 lines)
6. Standardize record normalization (saves ~240-360 lines)

**Low Priority (Optional/As Needed):**
7. Consider specialized classes for complex providers
8. Optimize query builder patterns
9. Consolidate metrics and logging

---

## 7. PostgreSQL Success Story

PostgreSQL demonstrates the effectiveness of the specialized class pattern:

**Architecture:**
```
PostgresDataStoreProvider (orchestrator, ~365 lines)
  ├── PostgresConnectionManager (connection pooling, encryption, retry)
  ├── PostgresFeatureOperations (CRUD operations, ~800+ lines)
  ├── PostgresBulkOperations (bulk operations)
  ├── PostgresVectorTileGenerator (MVT generation)
  ├── PostgresRecordMapper (record transformation utilities)
  └── QueryBuilderPool (query builder pooling for performance)
```

**Benefits Demonstrated:**
- Clear separation of concerns
- Each class has a single responsibility
- Easy to test in isolation
- Provider class is simple and readable
- Optimizations (query builder pooling) are isolated
- Excellent maintainability

**Lessons Learned:**
- Delegation is better than inheritance for complex providers
- Specialized classes reduce cognitive load
- Connection management deserves its own class
- Utility classes (RecordMapper) eliminate duplication
- Query builder pooling provides measurable performance benefits

---

## 8. Implementation Recommendations

### 8.1 Immediate Actions (Next Sprint)

1. **Create `RelationalTransactionHelper` utility class**
   - Extract transaction extraction pattern
   - Use in MySQL, SQL Server, SQLite
   - Test thoroughly with existing tests
   - **Expected savings:** 240-300 lines
   - **Risk:** Low
   - **Effort:** 3-5 days

2. **Create `RelationalBulkOperationHelper` utility class**
   - Extract batching logic
   - Support configurable batch sizes
   - **Expected savings:** 400-600 lines
   - **Risk:** Low
   - **Effort:** 3-5 days

3. **Add `TestConnectivityAsync` to `RelationalDataStoreProviderBase`**
   - Implement as virtual method
   - Support provider-specific test queries via abstract method
   - **Expected savings:** 50-60 lines
   - **Risk:** Very Low
   - **Effort:** 1-2 days

**Total Sprint Impact:** 690-960 lines saved, 7-12 days effort

---

### 8.2 Next Quarter Goals

1. **Enhance `RelationalDataStoreProviderBase` with soft delete operations**
   - Add `ExecuteSoftDeleteAsync`, `ExecuteRestoreAsync`, `ExecuteHardDeleteAsync`
   - Parameterize SQL generation
   - **Expected savings:** 400-500 lines
   - **Effort:** 1-2 weeks

2. **Migrate MySQL to inherit from base class**
   - Refactor to use enhanced base class
   - Implement abstract methods for MySQL-specific SQL
   - **Expected savings:** 300-400 lines
   - **Effort:** 2-3 weeks

3. **Migrate SQL Server to inherit from base class**
   - Handle geometry type detection complexity
   - Consider specialized classes for complex areas
   - **Expected savings:** 350-450 lines
   - **Effort:** 2-3 weeks

4. **Migrate SQLite to inherit from base class**
   - Simplest provider, good learning experience
   - **Expected savings:** 250-350 lines
   - **Effort:** 1-2 weeks

**Total Quarter Impact:** 1,300-1,700 lines saved

---

### 8.3 Long-Term Vision (Next Year)

1. **Evaluate specialized classes pattern for SQL Server**
   - Complexity warrants separate concern classes
   - Geometry type detection is a good candidate
   - Follow PostgreSQL's proven pattern

2. **Create comprehensive test suite for base classes**
   - Ensure all providers behave consistently
   - Use abstract test base classes
   - Measure code coverage

3. **Document patterns and best practices**
   - Create developer guide for adding new providers
   - Document when to use inheritance vs. delegation
   - Share lessons learned from consolidation

4. **Consider performance optimizations**
   - Query builder pooling (from PostgreSQL)
   - Connection pooling improvements
   - Batch size tuning

---

## 9. Risk Assessment

### 9.1 Implementation Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking changes during refactoring | Medium | High | Comprehensive test suite, feature flags |
| Performance regression | Low | Medium | Benchmark before/after, monitor metrics |
| Provider-specific bugs | Medium | Medium | Thorough testing per provider |
| Complexity in base class | Low | Medium | Regular code reviews, keep methods focused |
| Incomplete abstraction | Medium | Low | Start with utilities, iterate to base classes |

### 9.2 Testing Strategy

**Unit Tests:**
- Test each utility class independently
- Test base class methods with mock connections
- Maintain existing provider tests

**Integration Tests:**
- Run full test suite against each provider
- Test transaction behavior thoroughly
- Verify bulk operations with large datasets

**Performance Tests:**
- Benchmark CRUD operations before/after
- Measure bulk operation throughput
- Monitor connection pool efficiency

---

## 10. Conclusion

The analysis reveals significant opportunities for consolidation across the data store providers, with **1,830-2,520 lines of duplicate code** identified. The hybrid approach (Strategy 4) is recommended for gradual, low-risk migration that provides immediate value while working toward a cleaner architecture.

**Key Takeaways:**

1. **PostgreSQL's architecture is exemplary** - Consider adopting its specialized class pattern for complex providers

2. **Quick wins are available** - Utility classes can save 400-600 lines with minimal risk in 1-2 weeks

3. **Base class enhancement is valuable** - Can save an additional 800-1,000 lines with medium effort

4. **Provider-specific variations are manageable** - Abstract methods and parameterization handle SQL dialect differences

5. **Incremental approach reduces risk** - Hybrid strategy allows measurement and validation at each phase

**Recommended Next Steps:**

1. Create `RelationalTransactionHelper` and `RelationalBulkOperationHelper` utilities
2. Enhance `RelationalDataStoreProviderBase` with connectivity testing
3. Migrate one provider (SQLite) as a proof of concept
4. Evaluate results and proceed with other providers
5. Consider specialized classes for SQL Server's complexity

**Expected Total Savings:** 1,200-1,600 lines of code
**Implementation Timeline:** 6-9 weeks
**Risk Level:** Low (with incremental approach)

---

## Appendix A: Code Examples

### A.1 RelationalTransactionHelper

```csharp
public static class RelationalTransactionHelper<TConnection, TTransaction>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    public static async Task<(TConnection connection, TTransaction? transaction, bool shouldDispose)>
        ExtractOrCreateConnectionAsync(
            Func<CancellationToken, Task<TConnection>> connectionFactory,
            IDataStoreTransaction? transaction,
            CancellationToken cancellationToken)
    {
        if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> txWrapper)
        {
            return (txWrapper.Connection, txWrapper.Transaction, false);
        }

        var connection = await connectionFactory(cancellationToken);
        return (connection, null, true);
    }

    public static async Task DisposeIfNeededAsync(
        TConnection? connection,
        bool shouldDispose)
    {
        if (shouldDispose && connection != null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
```

### A.2 RelationalBulkOperationHelper

```csharp
public static class RelationalBulkOperationHelper
{
    public static async Task<int> ExecuteBatchedAsync<T>(
        IAsyncEnumerable<T> items,
        Func<List<T>, CancellationToken, Task<int>> batchExecutor,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var count = 0;
        var batch = new List<T>(batchSize);

        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                count += await batchExecutor(batch, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await batchExecutor(batch, cancellationToken);
        }

        return count;
    }
}
```

---

## Appendix B: Provider Comparison Matrix

| Feature | MySQL | SQL Server | SQLite | PostgreSQL | Oracle |
|---------|-------|-----------|--------|-----------|--------|
| Architecture | Monolithic | Monolithic | Monolithic | Specialized Classes | Base Class |
| Line Count | ~1,390 | ~1,377 | ~1,440 | ~365 (orchestrator) | ~1,076 |
| Connection Pooling | MySqlDataSource | Builder Cache | None | NpgsqlDataSource | Builder |
| Transaction Support | Yes | Yes | Yes | Yes | Yes |
| Soft Delete | Yes | Yes | Yes | Yes | No |
| Bulk Operations | Batched SQL | SqlBulkCopy + Batched | Batched SQL | Delegated | Array Binding |
| Geometry Handling | ST_GeomFromGeoJSON | STGeomFromText | WKT String | ST_GeomFromGeoJSON | SDO_UTIL |
| Query Builder | MySqlFeatureQueryBuilder | SqlServerFeatureQueryBuilder | SqliteFeatureQueryBuilder | Pooled Builders | OracleFeatureQueryBuilder |
| Retry Policy | Yes | Yes | Yes | Yes | Yes |
| Encryption Support | Yes | Yes | Yes | Yes | Yes |

---

**Report Generated:** October 31, 2025
**Analysis Tool:** Manual code review
**Confidence Level:** High
