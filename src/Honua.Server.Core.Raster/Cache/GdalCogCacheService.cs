// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Interop;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Cache.Storage;
using Microsoft.Extensions.Logging;
using OSGeo.GDAL;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// GDAL-based implementation of COG cache service.
/// Converts source rasters (NetCDF, HDF5, GRIB2, GeoTIFF) to Cloud Optimized GeoTIFF (COG) format.
/// </summary>
/// <remarks>
/// Thread-safety: This service is thread-safe. Cache hit tracking uses atomic operations
/// to ensure accurate statistics under concurrent access.
///
/// Resource Management: This service properly disposes all GDAL Dataset, Driver, and Band objects
/// using 'using' statements to prevent memory leaks. The SemaphoreSlim is disposed via IDisposable
/// and IAsyncDisposable patterns.
/// </remarks>
public sealed class GdalCogCacheService : DisposableBase, IRasterCacheService
{
    private readonly ILogger<GdalCogCacheService> _logger;
    private readonly string _stagingDirectory;
    private readonly ICogCacheStorage _storage;
    private readonly ConcurrentDictionary<string, CacheEntry> _cacheIndex;
    private readonly SemaphoreSlim _conversionLock;
    private readonly TimeSpan _cacheTtl;
    private readonly bool _enforceCacheTtl;
    private long _cacheHits;
    private long _cacheMisses;
    private long _collisionDetections;

    public GdalCogCacheService(
        ILogger<GdalCogCacheService> logger,
        string stagingDirectory,
        ICogCacheStorage storage,
        TimeSpan? cacheTtl = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stagingDirectory = stagingDirectory ?? throw new ArgumentNullException(nameof(stagingDirectory));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _cacheIndex = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        _conversionLock = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent conversions

        Directory.CreateDirectory(_stagingDirectory);

        _cacheTtl = cacheTtl.GetValueOrDefault(TimeSpan.Zero);
        _enforceCacheTtl = cacheTtl.HasValue && cacheTtl.Value > TimeSpan.Zero;

        // Initialize GDAL for cloud-optimized operations
        GdalConfiguration.ConfigureForCloudOptimizedOperations();
    }

    public async Task<string> GetOrConvertToCogAsync(RasterDatasetDefinition dataset, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (dataset is null)
        {
            throw new ArgumentNullException(nameof(dataset));
        }

        var cacheKey = GetCacheKey(dataset);
        var existing = await TryGetCachedUriAsync(cacheKey, dataset.Source.Uri, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogDebug("COG cache hit for dataset {DatasetId}", dataset.Id);
            UpdateCacheHit(cacheKey);
            Interlocked.Increment(ref _cacheHits);
            return existing;
        }

        _logger.LogInformation("COG cache miss for dataset {DatasetId}, converting...", dataset.Id);

        var options = new CogConversionOptions
        {
            VariableName = GetVariableName(dataset),
            TimeIndex = GetTimeIndex(dataset),
        };

        return await ConvertToCogAsync(dataset.Source.Uri, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ConvertToCogAsync(string sourceUri, CogConversionOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (sourceUri.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Source URI cannot be null or empty", nameof(sourceUri));
        }

        Guard.NotNull(options);

        var cacheKey = GetCacheKeyFromUri(sourceUri, options);
        var existing = await TryGetCachedUriAsync(cacheKey, sourceUri, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            _logger.LogDebug("COG cache hit for {SourceUri}", sourceUri);
            UpdateCacheHit(cacheKey);
            Interlocked.Increment(ref _cacheHits);
            return existing;
        }

        await _conversionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Validate cache key format
            if (!CacheKeyGenerator.ValidateCacheKey(cacheKey))
            {
                _logger.LogWarning("Generated invalid cache key {CacheKey} for {SourceUri}", cacheKey, sourceUri);
            }

            DetectAndLogPotentialCollisions(cacheKey, sourceUri);

            return await ConvertToCogInternalAsync(cacheKey, sourceUri, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _conversionLock.Release();
        }
    }

    private async Task<string?> TryGetCachedUriAsync(string cacheKey, string sourceUri, CancellationToken cancellationToken)
    {
        if (_cacheIndex.TryGetValue(cacheKey, out var entry))
        {
            if (!IsCacheEntryStale(cacheKey, entry.Metadata, sourceUri))
            {
                return entry.Metadata.Uri;
            }
        }

        var metadata = await _storage.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        if (IsCacheEntryStale(cacheKey, metadata, sourceUri))
        {
            return null;
        }

        var cacheEntry = new CacheEntry
        {
            CacheKey = cacheKey,
            SourceUri = sourceUri,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            HitCount = 0
        };

        _cacheIndex[cacheKey] = cacheEntry;

        return metadata.Uri;
    }

    private async Task<string> ConvertToCogInternalAsync(
        string cacheKey,
        string sourceUri,
        CogConversionOptions options,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _cacheMisses);

        var stagingPath = GetStagingFilePath(cacheKey);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);

        if (File.Exists(stagingPath))
        {
            File.Delete(stagingPath);
        }

        _logger.LogInformation("Converting {SourceUri} to COG (cache key: {CacheKey})", sourceUri, cacheKey);

        // Build GDAL translate options
        var gdalOptions = BuildGdalTranslateOptions(options);

        // Handle special formats (NetCDF, HDF5, GRIB2)
        var inputUri = BuildInputUri(sourceUri, options);

        // Perform conversion using GDAL
        using var sourceDataset = Gdal.Open(inputUri, Access.GA_ReadOnly);
        if (sourceDataset == null)
        {
            throw new InvalidOperationException($"Failed to open source raster: {sourceUri}");
        }

        bool conversionSuccessful = false;

        try
        {
            using var cogDriver = Gdal.GetDriverByName("COG");
            if (cogDriver == null)
            {
                // Fallback to GTiff driver if COG not available
                _logger.LogWarning("COG driver not available, using GTiff with COG-like options");
                using var gtiffDriver = Gdal.GetDriverByName("GTiff");
                if (gtiffDriver == null)
                {
                    throw new InvalidOperationException("GTiff driver not available");
                }

                var gtiffOptions = new[]
                {
                    $"COMPRESS={options.Compression}",
                    $"BLOCKXSIZE={options.BlockSize}",
                    $"BLOCKYSIZE={options.BlockSize}",
                    "TILED=YES",
                    "COPY_SRC_OVERVIEWS=YES",
                    $"NUM_THREADS={options.NumThreads}"
                };

                using var cogDataset = gtiffDriver.CreateCopy(stagingPath, sourceDataset, 0, gtiffOptions, null, null);
                if (cogDataset == null)
                {
                    throw new InvalidOperationException($"Failed to convert {sourceUri} to COG");
                }

                cogDataset.FlushCache();
            }
            else
            {
                var cogOptions = new[]
                {
                    $"COMPRESS={options.Compression}",
                    $"BLOCKSIZE={options.BlockSize}",
                    $"OVERVIEW_RESAMPLING={options.OverviewResampling}",
                    $"NUM_THREADS={options.NumThreads}"
                };

                using var cogDataset = cogDriver.CreateCopy(stagingPath, sourceDataset, 0, cogOptions, null, null);
                if (cogDataset == null)
                {
                    throw new InvalidOperationException($"Failed to convert {sourceUri} to COG");
                }

                cogDataset.FlushCache();
            }

            conversionSuccessful = true;
        }
        finally
        {
            if (!conversionSuccessful && File.Exists(stagingPath))
            {
                try
                {
                    File.Delete(stagingPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up staging file {Path} after conversion failure", stagingPath);
                }
            }
        }

        var metadata = await _storage.SaveAsync(cacheKey, stagingPath, cancellationToken).ConfigureAwait(false);

        // Clean up staging file if it still exists (storage implementations may move the file)
        if (File.Exists(stagingPath))
        {
            try
            {
                File.Delete(stagingPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete staging file {Path} after upload", stagingPath);
            }
        }

        var cacheEntry = new CacheEntry
        {
            CacheKey = cacheKey,
            SourceUri = sourceUri,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            HitCount = 0
        };

        _cacheIndex[cacheKey] = cacheEntry;

        _logger.LogInformation(
            "Successfully converted {SourceUri} to COG at {Uri} ({SizeMb:F2} MB)",
            sourceUri,
            metadata.Uri,
            metadata.SizeBytes / 1024.0 / 1024.0);

        return metadata.Uri;
    }

    public async Task<bool> IsCacheStaleAsync(string cachedUri, string sourceUri, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (cachedUri.IsNullOrWhiteSpace())
        {
            return true;
        }

        var cacheKey = ExtractCacheKey(cachedUri);
        if (cacheKey.IsNullOrWhiteSpace())
        {
            return true;
        }

        var metadata = await _storage.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return true;
        }

        return IsCacheEntryStale(cacheKey, metadata, sourceUri);
    }

    public async Task InvalidateCacheAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (datasetId.IsNullOrWhiteSpace())
        {
            return;
        }

        var entriesToRemove = _cacheIndex
            .Where(kvp => kvp.Key.Contains(datasetId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var cacheKey in entriesToRemove)
        {
            _cacheIndex.TryRemove(cacheKey, out _);
            try
            {
                await _storage.DeleteAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deleted cached COG for {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cached COG for {CacheKey}", cacheKey);
            }
        }
    }

    public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var entries = _cacheIndex.Values.ToList();
        var totalHits = entries.Sum(e => e.HitCount);
        var totalAccesses = entries.Count > 0 ? totalHits + entries.Count : 0;

        var stats = new CacheStatistics
        {
            TotalEntries = entries.Count,
            TotalSizeBytes = entries.Sum(e => e.Metadata.SizeBytes),
            HitRate = totalAccesses > 0 ? (double)totalHits / totalAccesses : 0,
            LastCleanup = DateTime.UtcNow,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            CollisionDetections = _collisionDetections
        };

        return Task.FromResult(stats);
    }

    private string GetCacheKey(RasterDatasetDefinition dataset)
    {
        var variable = GetVariableName(dataset);
        var timeIndex = GetTimeIndex(dataset);

        // Use dataset ID-based cache key for metadata-driven datasets
        return CacheKeyGenerator.GenerateCacheKeyFromDatasetId(dataset.Id, variable, timeIndex);
    }

    private string GetCacheKeyFromUri(string sourceUri, CogConversionOptions options)
    {
        // Use path hash-based cache key to prevent directory collisions
        return CacheKeyGenerator.GenerateCacheKey(sourceUri, options.VariableName, options.TimeIndex);
    }

    private string GetStagingFilePath(string cacheKey)
    {
        return Path.Combine(_stagingDirectory, $"{cacheKey}.tmp.tif");
    }

    private bool IsCacheEntryStale(string cacheKey, CogStorageMetadata metadata, string sourceUri)
    {
        // Prefer comparing timestamps when source is a local file
        if (IsLocalFile(sourceUri) && File.Exists(sourceUri))
        {
            var sourceModified = File.GetLastWriteTimeUtc(sourceUri);
            var cacheModified = metadata.LastModifiedUtc;
            return sourceModified > cacheModified;
        }

        // For remote sources fall back to TTL if configured
        if (_enforceCacheTtl)
        {
            var age = DateTime.UtcNow - metadata.LastModifiedUtc;
            if (age > _cacheTtl)
            {
                _logger.LogDebug(
                    "Cached COG for {CacheKey} exceeded TTL ({Age} > {Ttl}), marking as stale",
                    cacheKey, age, _cacheTtl);
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalFile(string path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            return uri.IsFile || string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
        }

        return !path.Contains("://", StringComparison.Ordinal);
    }

    private static string? ExtractCacheKey(string cachedUri)
    {
        if (cachedUri.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(cachedUri, UriKind.Absolute, out var uri))
            {
                var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
                return Path.GetFileNameWithoutExtension(lastSegment);
            }

            return Path.GetFileNameWithoutExtension(cachedUri);
        }
        catch
        {
            return null;
        }
    }

    private string? GetVariableName(RasterDatasetDefinition dataset)
    {
        // Try to extract from source URI using GDAL
        try
        {
            var sourceUri = dataset.Source.Uri;
            var extension = Path.GetExtension(sourceUri).ToLowerInvariant();

            // Only attempt extraction for NetCDF/HDF5
            if (extension is not (".nc" or ".nc4" or ".netcdf" or ".h5" or ".hdf" or ".hdf5"))
            {
                return null;
            }

            using var gdalDataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
            if (gdalDataset == null)
            {
                return null;
            }

            // Check for subdatasets
            var subdatasets = gdalDataset.GetMetadata("SUBDATASETS");
            if (subdatasets != null && subdatasets.Length > 0)
            {
                // Parse first subdataset to extract variable name
                foreach (string item in subdatasets)
                {
                    if (item.Contains("SUBDATASET_1_NAME=", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = item.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var subdatasetPath = parts[1];
                            // Extract variable name from NETCDF:"file.nc":variable or HDF5:"file.h5"://variable
                            var lastColon = subdatasetPath.LastIndexOf(':');
                            if (lastColon > 0)
                            {
                                var varName = subdatasetPath.Substring(lastColon + 1).Trim('"', ' ', '/');
                                if (varName.HasValue())
                                {
                                    _logger.LogDebug("Extracted variable name from subdataset: {VariableName}", varName);
                                    return varName;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract variable name for dataset {DatasetId}", dataset.Id);
        }

        return null;
    }

    private int? GetTimeIndex(RasterDatasetDefinition dataset)
    {
        // Check if temporal metadata is enabled
        if (dataset.Temporal?.Enabled == true)
        {
            // If temporal is enabled but no default value, assume first time step
            if (dataset.Temporal.DefaultValue.IsNullOrWhiteSpace())
            {
                return 0;
            }

            // If fixed values are provided, use the index of the default value
            if (dataset.Temporal.FixedValues != null && dataset.Temporal.FixedValues.Count > 0)
            {
                for (int i = 0; i < dataset.Temporal.FixedValues.Count; i++)
                {
                    if (dataset.Temporal.FixedValues[i] == dataset.Temporal.DefaultValue)
                    {
                        return i;
                    }
                }
                // Default value not found in fixed values, use first time step
                return 0;
            }

            // If temporal is enabled with default value but no fixed values, return 0
            return 0;
        }

        return null;
    }

    private GDALTranslateOptions BuildGdalTranslateOptions(CogConversionOptions options)
    {
        var args = new[]
        {
            "-of", "COG",
            "-co", $"COMPRESS={options.Compression}",
            "-co", $"BLOCKSIZE={options.BlockSize}",
            "-co", $"OVERVIEW_RESAMPLING={options.OverviewResampling}",
            "-co", $"NUM_THREADS={options.NumThreads}",
        };

        return new GDALTranslateOptions(args);
    }

    private string BuildInputUri(string sourceUri, CogConversionOptions options)
    {
        // Handle special formats that require subdataset syntax
        var extension = Path.GetExtension(sourceUri).ToLowerInvariant();

        if (extension is ".nc" or ".nc4" or ".netcdf" && options.VariableName != null)
        {
            return $"NETCDF:{sourceUri}:{options.VariableName}";
        }

        if (extension is ".h5" or ".hdf" or ".hdf5" && options.VariableName != null)
        {
            return $"HDF5:{sourceUri}://{options.VariableName}";
        }

        if (extension is ".grib" or ".grib2" or ".grb" or ".grb2")
        {
            // GRIB files are multi-message, GDAL automatically opens first band
            // For specific bands, use: GRIB:file.grib:band_number
            return sourceUri;
        }

        // Default: assume GDAL can open directly
        return sourceUri;
    }

    /// <summary>
    /// Detects and logs potential cache key collisions.
    /// </summary>
    /// <param name="cacheKey">The cache key to check</param>
    /// <param name="sourceUri">The source URI generating this key</param>
    private void DetectAndLogPotentialCollisions(string cacheKey, string sourceUri)
    {
        if (_cacheIndex.TryGetValue(cacheKey, out var existingEntry))
        {
            // Check if the source URI differs (potential collision)
            if (!string.Equals(existingEntry.SourceUri, sourceUri, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _collisionDetections);
                _logger.LogWarning(
                    "Potential cache key collision detected! Key: {CacheKey}, " +
                    "Existing Source: {ExistingSource}, New Source: {NewSource}",
                    cacheKey, existingEntry.SourceUri, sourceUri);
            }
        }
    }

    /// <summary>
    /// Updates cache hit statistics atomically to prevent race conditions.
    /// </summary>
    /// <param name="cacheKey">The cache key to update.</param>
    /// <remarks>
    /// Thread-safety: Uses Interlocked.Increment for atomic counter updates.
    /// LastAccessedAt updates are non-critical and use simple assignment.
    /// </remarks>
    private void UpdateCacheHit(string cacheKey)
    {
        if (_cacheIndex.TryGetValue(cacheKey, out var entry))
        {
            // Use atomic increment to prevent lost updates in concurrent scenarios
            Interlocked.Increment(ref entry.HitCount);

            // LastAccessedAt update doesn't need to be atomic - approximate timestamp is acceptable
            entry.LastAccessedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Cache entry with thread-safe counter for concurrent access.
    /// </summary>
    /// <remarks>
    /// HitCount is accessed via Interlocked operations to ensure thread-safety.
    /// </remarks>
    protected override void DisposeCore()
    {
        _conversionLock?.Dispose();
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        // For async cleanup, we need to wait for any pending conversions
        // This prevents disposing the semaphore while conversions are in progress
        if (_conversionLock != null)
        {
            // Wait for all slots to be available (no conversions in progress)
            var maxSlots = _conversionLock.CurrentCount;
            for (int i = 0; i < Environment.ProcessorCount - maxSlots; i++)
            {
                await _conversionLock.WaitAsync().ConfigureAwait(false);
            }

            // Release all acquired slots
            for (int i = 0; i < Environment.ProcessorCount - maxSlots; i++)
            {
                _conversionLock.Release();
            }

            _conversionLock.Dispose();
        }

        await base.DisposeCoreAsync().ConfigureAwait(false);
    }

    private sealed class CacheEntry
    {
        public required string CacheKey { get; init; }
        public required string SourceUri { get; init; }
        public required CogStorageMetadata Metadata { get; set; }
        public required DateTime CreatedAt { get; init; }

        /// <summary>
        /// Last access timestamp. Updates are non-atomic but this is acceptable
        /// as the exact timestamp is not critical for cache statistics.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Number of cache hits. This is a field (not a property) to support
        /// Interlocked.Increment for thread-safe concurrent updates.
        /// </summary>
        public long HitCount;
    }
}
