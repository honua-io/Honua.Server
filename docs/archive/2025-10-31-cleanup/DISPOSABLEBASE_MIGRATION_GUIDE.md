# DisposableBase Migration Guide

## Overview

The `DisposableBase` abstract class eliminates 500-800 lines of duplicate disposal pattern code across 40+ classes in the Honua codebase. Previously, each class implementing `IDisposable` and/or `IAsyncDisposable` had to manually implement the same boilerplate:

- Manage a private `volatile bool _disposed` field
- Check disposal state with `ObjectDisposedException.ThrowIf(_disposed, this)` in every method (197 occurrences)
- Implement standard `Dispose()` and `DisposeAsync()` methods
- Call `GC.SuppressFinalize(this)` in both paths

This pattern is now centralized in `DisposableBase`, allowing each class to save 15-20 lines while maintaining full functionality.

## Migration Statistics

**Before Migration:**
- 40+ classes with duplicate disposal code
- 197 instances of `ObjectDisposedException.ThrowIf(_disposed, this)`
- ~500-800 lines of boilerplate code

**After Migration (per class):**
- Removes `_disposed` field (1 line)
- Removes `Dispose()` method (8-10 lines)
- Removes `DisposeAsync()` method (8-10 lines)
- Replaces `ObjectDisposedException.ThrowIf()` with `ThrowIfDisposed()` (1 line)
- **Net savings: 15-20 lines per class**

## How to Use DisposableBase

### Step 1: Change Base Class

**Before:**
```csharp
public sealed class MyCache : IDisposable
{
    private bool _disposed;
    // ...
}
```

**After:**
```csharp
public sealed class MyCache : DisposableBase
{
    // _disposed field is removed
    // ...
}
```

### Step 2: Remove Disposal Check Field

Remove the `private volatile bool _disposed;` field entirely. DisposableBase manages this internally.

### Step 3: Replace Disposal Checks

**Before:**
```csharp
public void GetValue()
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    // ... work
}
```

**After:**
```csharp
public void GetValue()
{
    ThrowIfDisposed();
    // ... work
}
```

### Step 4: Implement DisposeCore()

Replace `Dispose()` and `DisposeAsync()` methods with abstract overrides:

**Before:**
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    _lock.Dispose();
    _cache.Clear();
}

public async ValueTask DisposeAsync()
{
    if (_disposed) return;
    _disposed = true;

    await _storage.DisposeAsync().ConfigureAwait(false);
    _lock.Dispose();
}
```

**After:**
```csharp
protected override void DisposeCore()
{
    _lock.Dispose();
    _cache.Clear();
}

protected override async ValueTask DisposeCoreAsync()
{
    await _storage.DisposeAsync().ConfigureAwait(false);
    _lock.Dispose();
}
```

## DisposableBase API

### Public Methods
- `void Dispose()` - Synchronous disposal (inherited from IDisposable)
- `async ValueTask DisposeAsync()` - Asynchronous disposal (inherited from IAsyncDisposable)

### Protected Methods
- `void ThrowIfDisposed()` - Throws ObjectDisposedException if already disposed
- `bool IsDisposed` - Property to check if object is disposed

### Abstract Methods to Override
- `protected abstract void DisposeCore()` - Implement synchronous cleanup
- `protected virtual async ValueTask DisposeCoreAsync()` - Implement async cleanup (optional)

## Implementation Details

### Disposal Order

1. `Dispose()` or `DisposeAsync()` is called on DisposableBase
2. Sets `_disposed = true` atomically
3. For async disposal:
   - Calls `DisposeCoreAsync()` with proper exception handling
   - Falls back to `DisposeCore()` in finally block
4. Calls `GC.SuppressFinalize(this)`

### Thread Safety

- `_disposed` is a `volatile bool` for safe cross-thread access
- Derived classes should implement their own synchronization for shared resources

### Async Disposal Pattern

DisposableBase properly implements the async disposal pattern:
- Calls async cleanup first (`DisposeCoreAsync()`)
- Always calls sync cleanup as fallback (`DisposeCore()`)
- Preserves async exceptions even if sync cleanup fails
- Ensures all resources are cleaned up in both paths

This is more robust than many manual implementations.

## Examples of Migrated Classes

### Example 1: RasterMetadataCache (Sync Only)

**Lines saved: 16**

```csharp
// BEFORE (80 lines total)
public sealed class RasterMetadataCache : IDisposable
{
    private readonly IMemoryCache _cache;
    private bool _disposed;

    public void SetMetadata(string uri, GeoRasterMetadata metadata)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // ...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_cache is MemoryCache mc) mc.Dispose();
    }
}

// AFTER (64 lines total)
public sealed class RasterMetadataCache : DisposableBase
{
    private readonly IMemoryCache _cache;

    public void SetMetadata(string uri, GeoRasterMetadata metadata)
    {
        ThrowIfDisposed();
        // ...
    }

    protected override void DisposeCore()
    {
        if (_cache is MemoryCache mc) mc.Dispose();
    }
}
```

### Example 2: PreparedStatementCache (Sync Only)

**Lines saved: 18**

```csharp
// BEFORE (212 lines)
public sealed class PreparedStatementCache : IDisposable
{
    private bool _disposed;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, ...)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // ...
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // ...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        _lruLock.Dispose();
    }
}

// AFTER (194 lines)
public sealed class PreparedStatementCache : DisposableBase
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, ...)
    {
        ThrowIfDisposed();
        // ...
    }

    public void Clear()
    {
        ThrowIfDisposed();
        // ...
    }

    protected override void DisposeCore()
    {
        Clear();
        _lruLock.Dispose();
    }
}
```

### Example 3: QueryBuilderPool (Sync Only)

**Lines saved: 14**

```csharp
// BEFORE (304 lines)
public sealed class QueryBuilderPool : IDisposable
{
    private bool _disposed;

    internal void Return(...) {
        if (_disposed || builder == null) return;
        // ...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lruLock.Dispose();
        _metrics.Dispose();
        _pools.Clear();
    }
}

// AFTER (290 lines)
public sealed class QueryBuilderPool : DisposableBase
{
    internal void Return(...) {
        if (IsDisposed || builder == null) return;
        // ...
    }

    protected override void DisposeCore()
    {
        _lruLock.Dispose();
        _metrics.Dispose();
        _pools.Clear();
    }
}
```

### Example 4: PostgresConnectionManager (Async + Sync)

**Lines saved: 22**

```csharp
// BEFORE (384 lines)
internal sealed class PostgresConnectionManager : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public async Task<NpgsqlConnection> CreateConnectionAsync(...)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // ...
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated) continue;
            entry.Value.Dispose();
        }

        _dataSources.Clear();
        _decryptionLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated) continue;
            await entry.Value.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        _decryptionLock.Dispose();
    }
}

// AFTER (362 lines)
internal sealed class PostgresConnectionManager : DisposableBase
{
    public async Task<NpgsqlConnection> CreateConnectionAsync(...)
    {
        ThrowIfDisposed();
        // ...
    }

    protected override void DisposeCore()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated) continue;
            entry.Value.Dispose();
        }

        _dataSources.Clear();
        _decryptionLock.Dispose();
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated) continue;
            await entry.Value.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        _decryptionLock.Dispose();
    }
}
```

## Common Patterns to Migrate

### Pattern 1: Simple Sync Disposal

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    _resource?.Dispose();
}
```

Migrate to:

```csharp
protected override void DisposeCore()
{
    _resource?.Dispose();
}
```

### Pattern 2: Multiple Resource Cleanup

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    _lock.Dispose();
    _cache.Clear();
    _pool.Drain();
}
```

Migrate to:

```csharp
protected override void DisposeCore()
{
    _lock.Dispose();
    _cache.Clear();
    _pool.Drain();
}
```

### Pattern 3: Async + Sync Cleanup

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _lock.Dispose();
}

public async ValueTask DisposeAsync()
{
    if (_disposed) return;
    _disposed = true;

    await _storage.DisposeAsync().ConfigureAwait(false);
    _lock.Dispose();
}
```

Migrate to:

```csharp
protected override void DisposeCore()
{
    _lock.Dispose();
}

protected override async ValueTask DisposeCoreAsync()
{
    await _storage.DisposeAsync().ConfigureAwait(false);
    _lock.Dispose();
}
```

### Pattern 4: Conditional Disposal Checks

```csharp
internal void Return(object item)
{
    if (_disposed || item == null) return;
    // ...
}
```

Migrate to:

```csharp
internal void Return(object item)
{
    if (IsDisposed || item == null) return;
    // ...
}
```

## Remaining Classes to Migrate

The following 35+ classes still use the old manual pattern and are candidates for migration:

### Data Providers (13 classes)
- `MySqlDataStoreProvider`
- `SqlServerDataStoreProvider`
- `PostgresDataStoreProvider`
- `SqliteDataStoreProvider`
- `BigQueryDataStoreProvider`
- `CosmosDbDataStoreProvider`
- `ElasticsearchDataStoreProvider`
- `MongoDbDataStoreProvider`
- `OracleDataStoreProvider`
- `RedshiftDataStoreProvider`
- `SnowflakeDataStoreProvider`
- `ElasticsearchDataStoreProvider`
- `RelationalDeletionAuditStore`

### Cache/Storage Services (8+ classes)
- `GdalCogCacheService`
- `RasterStacCatalogSynchronizer`
- `VectorStacCatalogSynchronizer`
- `RelationalStacCatalogStore`
- `RelationalStacCatalogStore.SoftDelete`
- `AzureBlobAttachmentStore`
- `S3AttachmentStore`
- `SnowflakeConnectionManager`
- `PostgresConnectionManager` (already migrated)

### Raster Processing (5+ classes)
- `ZarrStream`
- `AzureBlobRasterTileCacheProvider`
- `S3RasterTileCacheProvider`
- `AzureBlobCogCacheStorage`
- `S3KerchunkCacheProvider`

## Testing Migrations

After migrating a class, verify:

1. **Compilation**: Class compiles without errors
2. **Disposal Behavior**: Object throws `ObjectDisposedException` after disposal
3. **Resource Cleanup**: All resources are properly disposed
4. **Async Disposal**: If applicable, async disposal works correctly
5. **Existing Tests**: All existing unit tests pass

## Performance Considerations

- **Zero overhead**: DisposableBase uses the same patterns as manual implementations
- **Memory**: Slight improvement by removing duplicate `_disposed` fields
- **Performance**: No performance impact (same IL generated)

## See Also

- `DisposableBase.cs` - Base class implementation
- Migrated classes:
  - `/src/Honua.Server.Core/Raster/RasterMetadataCache.cs`
  - `/src/Honua.Server.Core/Data/PreparedStatementCache.cs`
  - `/src/Honua.Server.Core/Data/Postgres/QueryBuilderPool.cs`
  - `/src/Honua.Server.Core/Data/Postgres/PostgresConnectionManager.cs`
