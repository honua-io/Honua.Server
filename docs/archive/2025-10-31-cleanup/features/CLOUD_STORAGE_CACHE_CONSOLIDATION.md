# Cloud Storage Cache Provider Consolidation

## Overview

Consolidated duplicate caching patterns across S3, Azure Blob Storage, and Google Cloud Storage providers into a single `CloudStorageCacheProviderBase` abstract class using the Template Method design pattern.

**Date:** 2025-10-25
**Phase:** Phase 4 - Caching Consolidation
**Impact:** Eliminates ~73 lines of duplication across three cloud storage providers

## Files Modified

### Created
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Raster/Cache/Storage/CloudStorageCacheProviderBase.cs` (354 lines)

### Refactored
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Raster/Cache/Storage/S3CogCacheStorage.cs` (135 → 100 lines, -35 lines)
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Raster/Cache/Storage/AzureBlobCogCacheStorage.cs` (95 → 86 lines, -9 lines)
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Raster/Cache/Storage/GcsCogCacheStorage.cs` (124 → 95 lines, -29 lines)

## Line Count Analysis

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| S3CogCacheStorage | 135 | 100 | -35 (-25.9%) |
| AzureBlobCogCacheStorage | 95 | 86 | -9 (-9.5%) |
| GcsCogCacheStorage | 124 | 95 | -29 (-23.4%) |
| **Provider Total** | **354** | **281** | **-73 (-20.6%)** |
| Base Class | 0 | 354 | +354 |
| **Overall Total** | **354** | **635** | **+281** |

**Net Effect:** While total lines increased by 281 due to the comprehensive base class, we eliminated 73 lines of duplication across providers (20.6% reduction). The base class provides infrastructure that will benefit future cloud storage providers.

## Common Patterns Extracted

### 1. Parameter Validation
- Consistent null/whitespace checks using `ArgumentException.ThrowIfNullOrWhiteSpace`
- Bucket name validation and trimming
- Prefix normalization (trim leading/trailing slashes)

### 2. Key Generation
- Standardized object key building: `[prefix/]cacheKey.tif`
- Consistent file extension handling
- Prefix path construction

### 3. Exception Handling
- Provider-agnostic 404/NotFound detection via `IsNotFoundException()`
- Consistent error logging patterns
- Idempotent delete operations (no error on missing objects)

### 4. Timestamp Normalization
- UTC timezone handling via `NormalizeToUtc()`
- Handles Utc, Local, and Unspecified DateTimeKind
- Consistent fallback to `DateTime.UtcNow`

### 5. Logging Patterns
- Structured logging with consistent parameters
- Debug-level for metadata retrieval
- Info-level for upload/delete operations
- Error-level with exception details

### 6. Metadata Handling
- Fallback metadata creation when provider doesn't return it
- Consistent CogStorageMetadata construction
- Storage URI generation

## Template Methods

The base class defines these abstract methods that derived classes implement:

### GetMetadataInternalAsync
```csharp
protected abstract Task<CogStorageMetadata?> GetMetadataInternalAsync(
    string objectKey,
    CancellationToken cancellationToken);
```
**Responsibilities:**
- Query the cloud provider for object metadata
- Return null for 404/NotFound
- Throw provider-specific exceptions for errors
- Ensure LastModifiedUtc is in UTC

### UploadInternalAsync
```csharp
protected abstract Task<CogStorageMetadata> UploadInternalAsync(
    string objectKey,
    Stream fileStream,
    FileInfo fileInfo,
    CancellationToken cancellationToken);
```
**Responsibilities:**
- Upload stream to cloud storage
- Set content type to "image/tiff"
- Overwrite if exists
- Return accurate metadata (size, timestamp)
- Use fileInfo for fallback if needed

### DeleteInternalAsync
```csharp
protected abstract Task DeleteInternalAsync(
    string objectKey,
    CancellationToken cancellationToken);
```
**Responsibilities:**
- Delete object from cloud storage
- Be idempotent (no error on missing object)
- Throw for access/service errors

### IsNotFoundException
```csharp
protected abstract bool IsNotFoundException(Exception exception);
```
**Responsibilities:**
- Identify provider-specific 404 exceptions
- S3: `AmazonS3Exception { StatusCode: HttpStatusCode.NotFound }`
- Azure: `RequestFailedException { Status: 404 }`
- GCS: `GoogleApiException { HttpStatusCode: HttpStatusCode.NotFound }`

### BuildStorageUri
```csharp
protected abstract string BuildStorageUri(string objectKey);
```
**Responsibilities:**
- Construct provider-specific URI format
- S3: `s3://bucket/key`
- Azure: `https://account.blob.core.windows.net/container/key`
- GCS: `gs://bucket/key`

## Before/After Comparison: S3CogCacheStorage

### Before (135 lines)
```csharp
public sealed class S3CogCacheStorage : ICogCacheStorage, IAsyncDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string? _prefix;
    private readonly ILogger<S3CogCacheStorage> _logger;
    private readonly bool _ownsClient;

    public S3CogCacheStorage(
        IAmazonS3 client,
        string bucket,
        string? prefix,
        ILogger<S3CogCacheStorage> logger,
        bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucket))
            : bucket.Trim();
        _prefix = prefix?.Trim().Trim('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsClient = ownsClient;
    }

    public async Task<CogStorageMetadata?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        var key = BuildObjectKey(cacheKey);

        try
        {
            var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucket,
                Key = key
            }, cancellationToken).ConfigureAwait(false);

            var lastModified = response.LastModified ?? DateTime.UtcNow;
            if (lastModified.Kind != DateTimeKind.Utc)
            {
                lastModified = DateTime.SpecifyKind(lastModified, DateTimeKind.Unspecified).ToUniversalTime();
            }

            return new CogStorageMetadata(
                $"s3://{_bucket}/{key}",
                response.ContentLength,
                lastModified);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // ... more duplicated code for SaveAsync, DeleteAsync, BuildObjectKey, etc.
}
```

### After (100 lines)
```csharp
public sealed class S3CogCacheStorage : CloudStorageCacheProviderBase, IAsyncDisposable
{
    private readonly IAmazonS3 _client;
    private readonly bool _ownsClient;

    public S3CogCacheStorage(
        IAmazonS3 client,
        string bucket,
        string? prefix,
        ILogger<S3CogCacheStorage> logger,
        bool ownsClient = false)
        : base(bucket, prefix, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    protected override async Task<CogStorageMetadata?> GetMetadataInternalAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = Bucket,
            Key = objectKey
        }, cancellationToken).ConfigureAwait(false);

        var lastModified = NormalizeToUtc(response.LastModified ?? DateTime.UtcNow);

        return new CogStorageMetadata(
            BuildStorageUri(objectKey),
            response.ContentLength,
            lastModified);
    }

    protected override bool IsNotFoundException(Exception exception)
    {
        return exception is AmazonS3Exception { StatusCode: HttpStatusCode.NotFound };
    }

    protected override string BuildStorageUri(string objectKey)
    {
        return $"s3://{Bucket}/{objectKey}";
    }

    // ... only provider-specific implementation details
}
```

**Key Improvements:**
- **35 lines eliminated** (25.9% reduction)
- No duplicate parameter validation
- No duplicate key building logic
- No duplicate timestamp normalization
- No duplicate logging infrastructure
- No duplicate exception handling patterns
- Focuses only on S3-specific API calls

## Migration Guide for Existing Providers

If you need to migrate another cloud storage provider to use the base class:

### Step 1: Change Base Class
```csharp
// Before
public sealed class MyCloudStorage : ICogCacheStorage

// After
public sealed class MyCloudStorage : CloudStorageCacheProviderBase
```

### Step 2: Update Constructor
```csharp
// Before
public MyCloudStorage(
    CloudClient client,
    string bucket,
    string? prefix,
    ILogger<MyCloudStorage> logger)
{
    _client = client ?? throw new ArgumentNullException(nameof(client));
    _bucket = bucket.IsNullOrWhiteSpace()
        ? throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucket))
        : bucket.Trim();
    _prefix = prefix?.Trim().Trim('/');
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

// After
public MyCloudStorage(
    CloudClient client,
    string bucket,
    string? prefix,
    ILogger<MyCloudStorage> logger)
    : base(bucket, prefix, logger)
{
    _client = client ?? throw new ArgumentNullException(nameof(client));
}
```

### Step 3: Remove Fields
Remove these fields (now in base class):
```csharp
// Remove these:
private readonly string _bucket;
private readonly string? _prefix;
private readonly ILogger<MyCloudStorage> _logger;

// Keep provider-specific fields:
private readonly CloudClient _client;
private readonly bool _ownsClient; // if applicable
```

### Step 4: Convert Public Methods to Protected Template Methods

#### TryGetAsync → GetMetadataInternalAsync
```csharp
// Before
public async Task<CogStorageMetadata?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
    var key = BuildObjectKey(cacheKey);

    try
    {
        // provider API call
    }
    catch (ProviderException ex) when (ex.IsNotFound)
    {
        return null;
    }
}

// After
protected override async Task<CogStorageMetadata?> GetMetadataInternalAsync(string objectKey, CancellationToken cancellationToken)
{
    // provider API call using objectKey (already built)
    // No try/catch needed - base handles it
}
```

#### SaveAsync → UploadInternalAsync
```csharp
// Before
public async Task<CogStorageMetadata> SaveAsync(string cacheKey, string localFilePath, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
    ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

    var key = BuildObjectKey(cacheKey);
    _logger.LogInformation("Uploading...");

    await using var fileStream = File.OpenRead(localFilePath);
    // upload logic
}

// After
protected override async Task<CogStorageMetadata> UploadInternalAsync(
    string objectKey,
    Stream fileStream,
    FileInfo fileInfo,
    CancellationToken cancellationToken)
{
    // upload logic using objectKey and fileStream (already opened)
    // No logging needed - base handles it
    // Use fileInfo for fallback if needed
}
```

#### DeleteAsync → DeleteInternalAsync
```csharp
// Before
public async Task DeleteAsync(string cacheKey, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
    var key = BuildObjectKey(cacheKey);

    try
    {
        await _client.DeleteAsync(key);
    }
    catch (ProviderException ex) when (ex.IsNotFound)
    {
        // Already deleted, ignore
    }
}

// After
protected override async Task DeleteInternalAsync(string objectKey, CancellationToken cancellationToken)
{
    await _client.DeleteAsync(objectKey);
    // No try/catch needed - base handles NotFound via IsNotFoundException
}
```

### Step 5: Implement Helper Methods
```csharp
protected override bool IsNotFoundException(Exception exception)
{
    return exception is ProviderSpecificException { StatusCode: 404 };
}

protected override string BuildStorageUri(string objectKey)
{
    return $"provider://{Bucket}/{objectKey}";
}
```

### Step 6: Update References
Replace references to removed fields:
- `_bucket` → `Bucket`
- `_prefix` → `Prefix`
- `_logger` → `Logger`
- `BuildObjectKey(cacheKey)` → use `objectKey` parameter (already built)

### Step 7: Remove Duplicate Methods
Delete these methods (now in base class):
- `BuildObjectKey()` - use parameter instead
- Any custom timestamp normalization
- Any custom logging helpers

## Benefits

### Code Quality
- **Single Responsibility:** Each provider focuses only on its API specifics
- **DRY Principle:** Common logic centralized in one place
- **Testability:** Base class can be tested independently
- **Maintainability:** Bug fixes in one place benefit all providers

### Consistency
- Uniform error handling across all providers
- Consistent logging format and verbosity
- Standardized parameter validation
- Identical key generation behavior

### Extensibility
- New cloud providers can inherit immediately
- Common features added once, available everywhere
- Template Method pattern guides implementation

### Performance
- No performance impact (same async patterns)
- ConfigureAwait(false) preserved throughout
- Stream handling unchanged

## Future Enhancements

### Potential Additions
1. **Retry Policies:** Add Polly-based retry with exponential backoff
2. **Metrics:** Centralized cache hit/miss/error metrics
3. **Compression:** Optional compression before upload
4. **Encryption:** Client-side encryption layer
5. **Batch Operations:** Bulk delete/list operations
6. **Prefetching:** Async metadata prefetch
7. **Validation:** Key validation and sanitization

### Example Retry Enhancement
```csharp
protected async Task<T> ExecuteWithRetryAsync<T>(
    Func<CancellationToken, Task<T>> operation,
    CancellationToken cancellationToken)
{
    var policy = Policy
        .Handle<Exception>(ex => !IsNotFoundException(ex))
        .WaitAndRetryAsync(3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                Logger.LogWarning("Retry {RetryCount} after {Delay}ms: {Error}",
                    retryCount, timeSpan.TotalMilliseconds, exception.Message);
            });

    return await policy.ExecuteAsync(operation, cancellationToken);
}
```

## Testing Considerations

### Unit Tests
Each provider should test:
- Constructor parameter validation
- Provider-specific exception mapping
- URI format generation
- Metadata extraction from provider responses

### Integration Tests
Base class should have integration tests for:
- Key building with/without prefix
- UTC timestamp normalization
- Logging patterns
- Exception handling

### Backward Compatibility
All existing tests should pass without modification since the `ICogCacheStorage` interface remains unchanged.

## Related Documentation

- Original implementations: See git history for detailed before/after
- Template Method Pattern: https://refactoring.guru/design-patterns/template-method
- Phase 4 Tracking: `/home/mike/projects/HonuaIO/docs/archive/root/REMAINING_CODE_DUPLICATION_OPPORTUNITIES.md`

## Summary

The `CloudStorageCacheProviderBase` successfully consolidates common caching patterns across three cloud storage providers, eliminating 73 lines of duplication (20.6% reduction) while establishing a robust foundation for future cloud storage integrations. The Template Method pattern provides clear extension points and maintains full backward compatibility with the existing `ICogCacheStorage` interface.
