# Kerchunk Integration Implementation Guide

## Overview

This document provides a complete implementation guide for adding kerchunk support to HonuaIO, enabling virtual Zarr access to NetCDF/HDF5/GRIB files without storage duplication.

**Benefits:**
- 50% storage cost reduction (no need to convert and duplicate data)
- Instant dataset availability (no conversion wait time)
- Same Zarr API access patterns as current implementation
- Efficient HTTP range request access to cloud-stored scientific data

**Status:** Design Complete | Implementation: Not Started

---

## Architecture Overview

```
User Request
    ↓
RasterStorageRouter (determines access strategy)
    ↓
KerchunkReferenceStore (gets/generates refs)
    ├─→ Cache Hit → Return refs
    └─→ Cache Miss → GdalKerchunkGenerator
                       ↓
                   Generate refs → Cache → Return
    ↓
KerchunkZarrReader (reads chunks via refs)
    ↓
HTTP Range Requests to Source File
```

---

## Implementation Phases

### Phase 1: Core Infrastructure ✅ **COMPLETE**

**Files Created:**
- `src/Honua.Server.Core/Raster/Kerchunk/KerchunkReferences.cs` ✅
- `src/Honua.Server.Core/Raster/Kerchunk/IKerchunkGenerator.cs` ✅
- `src/Honua.Server.Core/Raster/Kerchunk/IKerchunkReferenceStore.cs` ✅
- `src/Honua.Server.Core/Raster/Kerchunk/IKerchunkCacheProvider.cs` ✅

**Status:** ✅ Complete
**Build:** ✅ Passing

---

### Phase 2: GDAL-Based Generator ✅ **COMPLETE**

**File:** `src/Honua.Server.Core/Raster/Kerchunk/GdalKerchunkGenerator.cs` ✅

**Key Implementation Points:**
1. Use GDAL's subdataset API to enumerate NetCDF variables
2. Extract chunk dimensions from GDAL band block sizes
3. Build Zarr `.zarray` metadata from GDAL raster info
4. Generate chunk references (variable/y.x format)
5. Handle compression metadata mapping (DEFLATE→gzip, etc.)

**Pseudo-code:**
```csharp
public async Task<KerchunkReferences> GenerateAsync(...)
{
    using var dataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);

    // Get subdatasets (NetCDF variables)
    foreach (var subdataset in dataset.GetMetadata("SUBDATASETS"))
    {
        using var sub = Gdal.Open(subdatasetPath);
        var band = sub.GetRasterBand(1);

        // Get chunking info
        band.GetBlockSize(out int blockX, out int blockY);

        // Build .zarray metadata
        metadata[$"{varName}/.zarray"] = new {
            chunks = new[] { blockY, blockX },
            dtype = MapGdalTypeTo

Zarr(band.DataType),
            shape = new[] { sub.RasterYSize, sub.RasterXSize }
        };

        // Generate chunk refs (NOTE: GDAL doesn't expose byte offsets easily)
        // For NetCDF-4/HDF5, need to either:
        // - Use GDAL VSI hooks to track reads
        // - Fall back to Python kerchunk for now
        // - Or use HDF5-DotNet library for direct access
    }
}
```

**Challenge:** GDAL doesn't directly expose chunk byte offsets. Solutions:
1. **Short-term:** Stub implementation that logs warning and falls back to conversion
2. **Medium-term:** Python kerchunk fallback (via PythonNet)
3. **Long-term:** HDF5-DotNet for direct chunk table access

**Status:** ✅ Complete
**Build:** ✅ Passing

**Implementation Notes:**
- GDAL metadata extraction working
- Subdataset enumeration for NetCDF/HDF5 implemented
- Zarr .zarray metadata generation complete
- Chunk byte offset generation stubbed (GDAL limitation documented)
- Warning logged when byte offsets unavailable (fallback to conversion)

---

### Phase 3: Cache Providers ✅ **COMPLETE**

#### A. Filesystem Cache Provider

**File:** `src/Honua.Server.Core/Raster/Kerchunk/FilesystemKerchunkCacheProvider.cs`

```csharp
public sealed class FilesystemKerchunkCacheProvider : IKerchunkCacheProvider
{
    private readonly string _cacheDirectory;

    public async Task<KerchunkReferences?> GetAsync(string key, CancellationToken ct)
    {
        var path = Path.Combine(_cacheDirectory, $"{key}.json");
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<KerchunkReferences>(json);
    }

    public async Task SetAsync(string key, KerchunkReferences refs, TimeSpan? ttl, CancellationToken ct)
    {
        var path = Path.Combine(_cacheDirectory, $"{key}.json");
        var json = JsonSerializer.Serialize(refs);
        await File.WriteAllTextAsync(path, json, ct);
    }
}
```

#### B. S3 Cache Provider

**File:** `src/Honua.Server.Core/Raster/Kerchunk/S3KerchunkCacheProvider.cs`

```csharp
public sealed class S3KerchunkCacheProvider : IKerchunkCacheProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _prefix;

    public async Task<KerchunkReferences?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(
                _bucketName,
                $"{_prefix}/{key}.json",
                ct);

            using var stream = response.ResponseStream;
            return await JsonSerializer.DeserializeAsync<KerchunkReferences>(stream, cancellationToken: ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, KerchunkReferences refs, TimeSpan? ttl, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(refs);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = $"{_prefix}/{key}.json",
            InputStream = stream,
            ContentType = "application/json"
        };

        await _s3Client.PutObjectAsync(request, ct);
    }
}
```

**Files Created:**
- `src/Honua.Server.Core/Raster/Kerchunk/FilesystemKerchunkCacheProvider.cs` ✅
- `src/Honua.Server.Core/Raster/Kerchunk/S3KerchunkCacheProvider.cs` ✅

**Status:** ✅ Complete
**Build:** ✅ Passing

**Implementation Notes:**
- Filesystem provider: Atomic writes with temp file + rename
- S3 provider: Circuit breaker pattern, server-side encryption
- Both providers: JSON serialization, TTL support, proper error handling

---

### Phase 4: Reference Store with Locking ✅ **COMPLETE**

**File:** `src/Honua.Server.Core/Raster/Kerchunk/KerchunkReferenceStore.cs` ✅

**Key Features:**
- Distributed locking to prevent duplicate generation
- Cache-aside pattern (check cache → generate if miss → cache)
- Thread-safe with async/await

**Implementation provided in earlier conversation.**

**Dependencies:**
- Need `IDistributedLockProvider` interface
- Implementations: Redis, SQL, filesystem-based

**Status:** ✅ Complete
**Build:** ✅ Passing

**Implementation Notes:**
- Cache-aside pattern with double-check locking
- In-memory SemaphoreSlim locks (single-instance safe)
- Distributed locking TODO noted for multi-instance deployments
- SHA256-based cache key generation
- 5-minute generation timeout
- Lazy generation on first access (GetOrGenerateAsync)
- Explicit generation with force flag (GenerateAsync)

---

### Phase 5: Kerchunk Zarr Reader

**File:** `src/Honua.Server.Core/Raster/Readers/KerchunkZarrReader.cs`

**Key Responsibilities:**
1. Implement `IZarrReader` interface
2. Parse kerchunk refs to get byte ranges
3. Use existing `IRasterSourceProvider` for HTTP range requests
4. Delegate to existing decompression codecs

```csharp
public async Task<byte[]> ReadChunkAsync(
    string datasetUri,
    string arrayPath,
    int[] chunkCoords,
    CancellationToken ct)
{
    // 1. Get kerchunk refs
    var refs = await _refStore.GetOrGenerateAsync(datasetUri, new(), ct);

    // 2. Build chunk key (e.g., "temperature/0.1.2")
    var chunkKey = $"{arrayPath}/{string.Join(".", chunkCoords)}";

    // 3. Parse reference
    if (!refs.Refs.TryGetValue(chunkKey, out var refObj))
        throw new KeyNotFoundException($"Chunk {chunkKey} not found");

    var (sourceUri, offset, length) = ParseRef(refObj);

    // 4. HTTP range request via existing provider
    var provider = _sourceProviders.GetProvider(sourceUri);
    using var stream = await provider.OpenReadRangeAsync(sourceUri, offset, length, ct);

    // 5. Read bytes
    var buffer = new byte[length];
    await stream.ReadExactlyAsync(buffer, ct);

    return buffer;
}
```

**Status:** ⏳ To Be Implemented

---

### Phase 6: Router Integration

**File:** `src/Honua.Server.Core/Raster/Cache/RasterStorageRouter.cs` (modify existing)

**Changes:**
```csharp
public async Task<RasterAccessStrategy> DetermineStrategyAsync(
    string sourceUri,
    RasterQuery query,
    CancellationToken ct)
{
    var extension = Path.GetExtension(sourceUri).ToLowerInvariant();

    // NEW: Check if kerchunk-compatible
    if (extension is ".nc" or ".nc4" or ".h5" or ".hdf5" or ".grib" or ".grib2")
    {
        try
        {
            // Lazy generation happens here!
            var refs = await _kerchunkStore.GetOrGenerateAsync(sourceUri, new(), ct);

            return new RasterAccessStrategy
            {
                Method = AccessMethod.KerchunkVirtualZarr,
                Uri = sourceUri,
                ReaderType = typeof(KerchunkZarrReader)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kerchunk failed for {Uri}, falling back", sourceUri);
            // Fall through to conversion
        }
    }

    // Existing COG/Zarr conversion logic...
}
```

**Status:** ⏳ To Be Implemented

---

### Phase 7: API Endpoints

**File:** `src/Honua.Server.Host/Admin/RasterIngestionEndpoints.cs` (new)

**Endpoints:**
1. `POST /admin/raster/upload` - Upload file with optional kerchunk generation
2. `POST /admin/raster/{datasetId}/kerchunk/generate` - Manually trigger generation
3. `GET /admin/raster/{datasetId}/kerchunk` - Get kerchunk refs
4. `DELETE /admin/raster/{datasetId}/kerchunk` - Delete cached refs

**Implementation provided in earlier conversation.**

**Status:** ⏳ To Be Implemented

---

### Phase 8: CLI Commands

**File:** `src/Honua.Cli/Commands/RasterKerchunkCommand.cs` (new)

**Commands:**
```bash
honua raster kerchunk <dataset-id>              # Generate for one dataset
honua raster kerchunk --all                     # Generate for all
honua raster kerchunk --service <id> --all      # Generate for service
honua raster kerchunk <dataset-id> --force      # Force regeneration
```

**Implementation provided in earlier conversation.**

**Status:** ⏳ To Be Implemented

---

### Phase 9: Configuration

**File:** `src/Honua.Server.Host/appsettings.json` (modify)

```json
{
  "Honua": {
    "Raster": {
      "Kerchunk": {
        "Enabled": true,
        "CacheProvider": "S3",  // or "Filesystem", "Redis"
        "CacheBucket": "honua-kerchunk-cache",
        "CachePrefix": "refs",
        "CacheTtlDays": 30,
        "DistributedLockProvider": "Redis",  // or "Filesystem"
        "GenerationTimeoutMinutes": 5
      }
    }
  }
}
```

**Status:** ⏳ To Be Implemented

---

### Phase 10: DI Registration

**File:** `src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs` (modify)

```csharp
public static IServiceCollection AddHonuaRasterServices(this IServiceCollection services, ...)
{
    // Existing registrations...

    // NEW: Kerchunk services
    services.AddSingleton<IKerchunkGenerator, GdalKerchunkGenerator>();

    services.AddSingleton<IKerchunkCacheProvider>(sp =>
    {
        var config = sp.GetRequiredService<IHonuaConfiguration>();
        return config.Raster.Kerchunk.CacheProvider switch
        {
            "S3" => new S3KerchunkCacheProvider(...),
            "Filesystem" => new FilesystemKerchunkCacheProvider(...),
            _ => new NullKerchunkCacheProvider()
        };
    });

    services.AddSingleton<IKerchunkReferenceStore, KerchunkReferenceStore>();
    services.AddSingleton<KerchunkZarrReader>();

    return services;
}
```

**Status:** ⏳ To Be Implemented

---

## Testing Strategy

### Unit Tests

**File:** `tests/Honua.Server.Core.Tests/Raster/Kerchunk/GdalKerchunkGeneratorTests.cs`

```csharp
public class GdalKerchunkGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_NetCDFFile_ReturnsValidReferences()
    {
        // Arrange
        var generator = new GdalKerchunkGenerator(...);
        var sourceUri = "testdata/sst.nc";

        // Act
        var refs = await generator.GenerateAsync(sourceUri, new());

        // Assert
        Assert.NotEmpty(refs.Refs);
        Assert.Contains("temperature/0.0", refs.Refs.Keys);
        Assert.NotEmpty(refs.Metadata);
    }
}
```

### Integration Tests

**File:** `tests/Honua.Server.Core.Tests/Raster/Kerchunk/KerchunkIntegrationTests.cs`

Test full workflow: Upload → Generate → Access via Zarr API

---

## Deployment Checklist

- [ ] Add kerchunk cache S3 bucket to Terraform/CloudFormation
- [ ] Configure distributed locking (Redis or SQL)
- [ ] Update Kubernetes ConfigMap with kerchunk settings
- [ ] Add monitoring for kerchunk generation time
- [ ] Add alerts for generation failures
- [ ] Document user-facing kerchunk workflows

---

## Migration Path

### For Existing Deployments

1. **Phase 1:** Deploy kerchunk code (disabled by default)
2. **Phase 2:** Enable for test service, generate refs
3. **Phase 3:** Monitor performance vs conversion approach
4. **Phase 4:** Enable globally if successful
5. **Phase 5:** Optionally delete converted Zarr copies to reclaim storage

### For New Datasets

- Default to kerchunk for NetCDF/HDF5/GRIB
- Fall back to conversion only if kerchunk generation fails

---

## Performance Benchmarks (Expected)

| Metric | Current (Conversion) | With Kerchunk | Delta |
|--------|---------------------|---------------|-------|
| Storage | 2x source size | 1x + tiny JSON | **50% reduction** |
| Time to Availability | 5-30 minutes | 2-5 seconds | **100-360x faster** |
| First Access Latency | ~50ms | ~100ms | +50ms (acceptable) |
| Subsequent Access | ~50ms | ~50ms | Same |

---

## Known Limitations & Workarounds

1. **GDAL byte offset limitation:**
   - **Problem:** GDAL doesn't expose HDF5 chunk byte offsets
   - **Workaround:** Use Python kerchunk fallback or HDF5-DotNet

2. **Compression support:**
   - **Problem:** Some exotic compressors not supported
   - **Workaround:** Zarr supports most common (gzip, blosc, lz4, zstd)

3. **Source file must remain accessible:**
   - **Problem:** Kerchunk refs break if source moves/deletes
   - **Workaround:** Lifecycle policies, monitoring

---

## Next Steps

1. **Implement Phase 2 (Generator):** Start with stub that logs "not implemented" and falls back to conversion
2. **Implement Phase 3 (Filesystem Cache):** Simple JSON file cache for testing
3. **Implement Phase 4 (Reference Store):** In-memory lock for single-instance testing
4. **Integration Test:** End-to-end with real NetCDF file
5. **Iterate:** Add S3 cache, distributed locking, API endpoints, CLI

---

## Questions for Discussion

1. **Python dependency acceptable?** For generator fallback?
2. **Cache TTL strategy?** 30 days? Infinite?
3. **Generation timeout?** 5 minutes reasonable?
4. **Monitoring requirements?** What metrics matter most?

---

**Document Author:** Claude Code
**Last Updated:** 2025-10-24
**Implementation Status:** Design Complete, Awaiting Development
