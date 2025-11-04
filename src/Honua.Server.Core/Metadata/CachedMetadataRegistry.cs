// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Caching.Resilience;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Metadata registry with Redis caching layer.
/// Wraps an existing IMetadataRegistry and adds distributed caching for performance.
/// </summary>
public sealed class CachedMetadataRegistry : DisposableBase, IMetadataRegistry
{
    private readonly IMetadataRegistry _innerRegistry;
    private readonly IDistributedCache? _distributedCache;
    private readonly IOptionsMonitor<MetadataCacheOptions> _optionsMonitor;
    private readonly IOptionsMonitor<CacheInvalidationOptions> _invalidationOptions;
    private readonly CacheInvalidationRetryPolicy? _retryPolicy;
    private readonly MetadataCacheMetrics? _metrics;
    private readonly ILogger<CachedMetadataRegistry> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly SemaphoreSlim _cacheMissLock = new(1, 1);
    private readonly IDisposable? _optionsChangeToken;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CachedMetadataRegistry(
        IMetadataRegistry innerRegistry,
        IDistributedCache? distributedCache,
        IOptionsMonitor<MetadataCacheOptions> optionsMonitor,
        IOptionsMonitor<CacheInvalidationOptions> invalidationOptions,
        ILogger<CachedMetadataRegistry> logger,
        CacheInvalidationRetryPolicy? retryPolicy = null,
        MetadataCacheMetrics? metrics = null)
    {
        _innerRegistry = innerRegistry ?? throw new ArgumentNullException(nameof(innerRegistry));
        _distributedCache = distributedCache;
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _invalidationOptions = invalidationOptions ?? throw new ArgumentNullException(nameof(invalidationOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy;
        _metrics = metrics;

        // Register change callback for hot reload support
        _optionsChangeToken = optionsMonitor.OnChange(OnConfigurationChanged);

        _logger.LogInformation(
            "Metadata cache initialized with hot reload support. TTL: {Ttl}, Compression: {Compression}, Cache Warming: {WarmCache}, Invalidation Strategy: {Strategy}",
            optionsMonitor.CurrentValue.Ttl,
            optionsMonitor.CurrentValue.EnableCompression,
            optionsMonitor.CurrentValue.WarmCacheOnStartup,
            invalidationOptions.CurrentValue.Strategy);
    }

    /// <summary>
    /// Handles configuration changes during hot reload.
    /// </summary>
    /// <param name="options">New configuration options.</param>
    private void OnConfigurationChanged(MetadataCacheOptions options)
    {
        _logger.LogInformation(
            "Metadata cache configuration reloaded. TTL: {Ttl}, Compression: {Compression}, Timeout: {Timeout}s",
            options.Ttl,
            options.EnableCompression,
            options.OperationTimeout.TotalSeconds);

        // Validate new configuration
        if (options.OperationTimeout <= TimeSpan.Zero)
        {
            _logger.LogWarning(
                "Invalid metadata cache configuration: OperationTimeout must be positive. Keeping previous configuration.");
            return;
        }

        // Invalidate cache to apply new settings
        // Fire-and-forget to avoid blocking configuration reload
        _ = Task.Run(async () =>
        {
            try
            {
                await InvalidateCacheAsync(CancellationToken.None);
                _logger.LogInformation("Metadata cache invalidated due to configuration change");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate metadata cache on configuration reload (non-critical)");
            }
        });
    }

    public MetadataSnapshot Snapshot => _innerRegistry.Snapshot;

    public bool IsInitialized => _innerRegistry.IsInitialized;

    public bool TryGetSnapshot(out MetadataSnapshot snapshot)
    {
        return _innerRegistry.TryGetSnapshot(out snapshot);
    }

    public async ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // If Redis is not configured, bypass cache
        if (_distributedCache is null)
        {
            return await _innerRegistry.GetSnapshotAsync(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Try to get from cache first
            var cachedSnapshot = await GetFromCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cachedSnapshot is not null)
            {
                _metrics?.RecordCacheHit();
                _metrics?.RecordOperationDuration(stopwatch.ElapsedMilliseconds, "get_hit");
                _logger.LogDebug("Metadata snapshot retrieved from cache (hit rate: {HitRate:P2})",
                    _metrics?.GetHitRate() ?? 0);
                return cachedSnapshot;
            }

            _metrics?.RecordCacheMiss();

            // Protect against cache stampede - use double-check locking
            // Prevents multiple threads from simultaneously reloading cache on miss
            await _cacheMissLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock - another thread may have populated cache
                cachedSnapshot = await GetFromCacheAsync(cancellationToken).ConfigureAwait(false);
                if (cachedSnapshot is not null)
                {
                    _metrics?.RecordCacheHit();
                    _metrics?.RecordOperationDuration(stopwatch.ElapsedMilliseconds, "get_hit_after_lock");
                    _logger.LogDebug("Metadata snapshot retrieved from cache after lock (cache stampede avoided)");
                    return cachedSnapshot;
                }

                // Cache miss - get from inner registry and cache it
                var snapshot = await _innerRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

                // CRITICAL FIX: Make cache write synchronous instead of fire-and-forget
                // Previously used fire-and-forget with 30s timeout which caused 100x performance degradation:
                //   - If cache write timed out or failed, cache was never populated
                //   - All subsequent requests became cache misses hitting disk every time
                //   - For government systems with hundreds of users, this caused total system failure
                //
                // Now we make the cache write synchronous:
                //   - First request after metadata reload is slightly slower (cache write included)
                //   - All subsequent requests are fast (cache hits)
                //   - This is the correct trade-off for production reliability
                //
                // Use a generous timeout since this only affects the first request after reload
                using var cacheCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try
                {
                    await SetCacheAsync(snapshot, cacheCts.Token).ConfigureAwait(false);
                    _logger.LogInformation("Metadata snapshot successfully cached after miss");
                }
                catch (OperationCanceledException)
                {
                    // CRITICAL: Cache write timed out - log as ERROR not DEBUG
                    // This will cause performance degradation for all subsequent requests
                    _metrics?.RecordCacheError();
                    _logger.LogError("CRITICAL: Cache write operation timed out after 2 minutes. " +
                                    "All subsequent metadata requests will hit disk causing severe performance degradation. " +
                                    "Check Redis connectivity and performance.");
                }
                catch (Exception ex)
                {
                    // CRITICAL: Cache write failed - log as ERROR not WARNING
                    _metrics?.RecordCacheError();
                    _logger.LogError(ex, "CRITICAL: Failed to cache metadata snapshot. " +
                                        "All subsequent metadata requests will hit disk causing severe performance degradation. " +
                                        "Check Redis connectivity and health.");
                }

                _metrics?.RecordOperationDuration(stopwatch.ElapsedMilliseconds, "get_miss");
                return snapshot;
            }
            finally
            {
                _cacheMissLock.Release();
            }
        }
        catch (Exception ex) when (_optionsMonitor.CurrentValue.FallbackToDiskOnFailure)
        {
            _metrics?.RecordCacheError();
            _logger.LogWarning(ex, "Cache operation failed, falling back to disk");
            return await _innerRegistry.GetSnapshotAsync(cancellationToken);
        }
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await _innerRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Warm cache on startup if enabled
        if (_optionsMonitor.CurrentValue.WarmCacheOnStartup && _distributedCache is not null)
        {
            await WarmCacheAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _innerRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache on reload - propagate failures based on strategy
        if (_distributedCache is not null)
        {
            await InvalidateCacheAsync(cancellationToken).ConfigureAwait(false);
        }

        // Warm cache after reload if enabled
        if (_optionsMonitor.CurrentValue.WarmCacheOnStartup && _distributedCache is not null)
        {
            await WarmCacheAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [Obsolete("Use UpdateAsync() instead. This method uses blocking calls and will be removed in a future version.")]
    public void Update(MetadataSnapshot snapshot)
    {
        _innerRegistry.Update(snapshot);

        // Invalidate cache asynchronously (fire and forget) to avoid blocking
        // Uses a short timeout to prevent indefinite background work
        if (_distributedCache is not null)
        {
            using var invalidateCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _ = Task.Run(async () =>
            {
                try
                {
                    await InvalidateCacheAsync(invalidateCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Cache invalidation timed out after 10 seconds (non-critical)");
                }
                catch (Exception ex)
                {
                    _metrics?.RecordCacheError();
                    _logger.LogWarning(ex, "Failed to invalidate cache on metadata update");
                }
            }, invalidateCts.Token);
        }
    }

    public async Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _innerRegistry.UpdateAsync(snapshot, cancellationToken).ConfigureAwait(false);

        // Invalidate cache after update - propagate failures based on strategy
        if (_distributedCache is not null)
        {
            await InvalidateCacheAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public IChangeToken GetChangeToken()
    {
        return _innerRegistry.GetChangeToken();
    }

    /// <summary>
    /// Warms the cache by loading and caching the current snapshot.
    /// </summary>
    private async Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        if (_distributedCache is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Warming metadata cache...");
            var snapshot = await _innerRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            await SetCacheAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Metadata cache warmed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm metadata cache (non-critical)");
        }
    }

    /// <summary>
    /// Invalidates the cached metadata snapshot with retry logic and fallback strategies.
    /// </summary>
    /// <exception cref="CacheInvalidationException">
    /// Thrown when invalidation fails and Strict strategy is configured.
    /// </exception>
    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        if (_distributedCache is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var key = _optionsMonitor.CurrentValue.GetSnapshotCacheKey();
        var strategy = _invalidationOptions.CurrentValue.Strategy;

        try
        {
            // Use retry policy if available
            if (_retryPolicy != null)
            {
                await _retryPolicy.InvalidateWithRetryAsync(
                    _distributedCache,
                    key,
                    "metadata",
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback to direct invalidation without retry
                await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Invalidated metadata cache key: {CacheKey}", key);
            _metrics?.RecordOperationDuration(stopwatch.ElapsedMilliseconds, "invalidate");
            _metrics?.RecordInvalidationSuccess();
        }
        catch (CacheInvalidationException ex)
        {
            _metrics?.RecordCacheError();
            _metrics?.RecordInvalidationFailure();

            // Handle based on configured strategy
            switch (strategy)
            {
                case CacheInvalidationStrategy.Strict:
                    // Propagate exception - fail the operation to ensure consistency
                    _logger.LogError(
                        ex,
                        "CRITICAL: Cache invalidation failed with Strict strategy. Operation will fail to prevent stale data. Key: {CacheKey}",
                        key);
                    throw;

                case CacheInvalidationStrategy.Eventual:
                    // Log error but allow operation to continue
                    // Cache will eventually be updated on next reload or TTL expiry
                    _logger.LogWarning(
                        ex,
                        "Cache invalidation failed with Eventual strategy. Cache may serve stale data until TTL expires. Key: {CacheKey}",
                        key);
                    break;

                case CacheInvalidationStrategy.ShortTTL:
                    // Attempt to set a short TTL on the entry to reduce stale data window
                    _logger.LogWarning(
                        ex,
                        "Cache invalidation failed. Attempting to set short TTL as fallback. Key: {CacheKey}",
                        key);

                    // Get current cached value and re-set with short TTL
                    try
                    {
                        var cachedBytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                        if (cachedBytes != null && _retryPolicy != null)
                        {
                            await _retryPolicy.TrySetShortTtlAsync(
                                _distributedCache,
                                key,
                                cachedBytes,
                                "metadata",
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogWarning(
                            fallbackEx,
                            "Failed to apply short TTL fallback strategy for key: {CacheKey}",
                            key);
                    }
                    break;

                default:
                    _logger.LogError(
                        ex,
                        "Cache invalidation failed with unknown strategy: {Strategy}. Key: {CacheKey}",
                        strategy,
                        key);
                    throw;
            }
        }
        catch (Exception ex)
        {
            _metrics?.RecordCacheError();
            _metrics?.RecordInvalidationFailure();

            // For non-CacheInvalidationException errors, always propagate in Strict mode
            if (strategy == CacheInvalidationStrategy.Strict)
            {
                _logger.LogError(
                    ex,
                    "CRITICAL: Cache invalidation failed with Strict strategy. Key: {CacheKey}",
                    key);
                throw;
            }

            _logger.LogWarning(
                ex,
                "Cache invalidation encountered unexpected error. Strategy: {Strategy}, Key: {CacheKey}",
                strategy,
                key);
        }
    }

    private async Task<MetadataSnapshot?> GetFromCacheAsync(CancellationToken cancellationToken)
    {
        if (_distributedCache is null)
        {
            return null;
        }

        try
        {
            var key = _optionsMonitor.CurrentValue.GetSnapshotCacheKey();
            var cachedBytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);

            if (cachedBytes is null || cachedBytes.Length == 0)
            {
                return null;
            }

            return DeserializeSnapshot(cachedBytes);
        }
        catch (Exception ex)
        {
            _metrics?.RecordCacheError();
            _logger.LogWarning(ex, "Failed to retrieve metadata from cache");

            if (!_optionsMonitor.CurrentValue.FallbackToDiskOnFailure)
            {
                throw;
            }

            return null;
        }
    }

    private async Task SetCacheAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_distributedCache is null)
        {
            return;
        }

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var stopwatch = Stopwatch.StartNew();
            var key = options.GetSnapshotCacheKey();
            var serializedBytes = SerializeSnapshot(snapshot);

            var cacheOptions = options.Ttl.HasValue && options.Ttl.Value > TimeSpan.Zero
                ? new CacheOptionsBuilder()
                    .WithAbsoluteExpiration(options.Ttl.Value)
                    .BuildDistributed()
                : CacheOptionsBuilder.ForMetadata().BuildDistributed();

            await _distributedCache.SetAsync(key, serializedBytes, cacheOptions, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Cached metadata snapshot: key={CacheKey}, size={SizeKB}KB, ttl={Ttl}",
                key,
                serializedBytes.Length / 1024.0,
                options.Ttl);

            _metrics?.RecordOperationDuration(stopwatch.ElapsedMilliseconds, "set");
        }
        catch (Exception ex)
        {
            _metrics?.RecordCacheError();
            _logger.LogWarning(ex, "Failed to cache metadata snapshot");

            if (!_optionsMonitor.CurrentValue.FallbackToDiskOnFailure)
            {
                throw;
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private byte[] SerializeSnapshot(MetadataSnapshot snapshot)
    {
        // Use JSON source generation for ~2-3x faster serialization
        var json = snapshot.SerializeToUtf8BytesFast();

        if (!_optionsMonitor.CurrentValue.EnableCompression)
        {
            return json;
        }

        // Compress using GZip with pooled MemoryStream to reduce allocations
        var stream = ObjectPools.MemoryStream.Get();
        try
        {
            using (var gzipStream = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzipStream.Write(json, 0, json.Length);
            }

            var compressed = stream.ToArray();
            var compressionRatio = (1.0 - (double)compressed.Length / json.Length) * 100;

            _logger.LogDebug(
                "Compressed metadata: {OriginalKB}KB -> {CompressedKB}KB ({Ratio:F1}% reduction)",
                json.Length / 1024.0,
                compressed.Length / 1024.0,
                compressionRatio);

            return compressed;
        }
        finally
        {
            ObjectPools.MemoryStream.Return(stream);
        }
    }

    private MetadataSnapshot DeserializeSnapshot(byte[] data)
    {
        byte[] json = data;

        if (_optionsMonitor.CurrentValue.EnableCompression)
        {
            // Decompress using GZip with pooled MemoryStream
            var outputStream = ObjectPools.MemoryStream.Get();
            try
            {
                using var inputStream = new MemoryStream(data);
                using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);

                gzipStream.CopyTo(outputStream);
                json = outputStream.ToArray();
            }
            finally
            {
                ObjectPools.MemoryStream.Return(outputStream);
            }
        }

        // Use JSON source generation for ~2-3x faster deserialization
        var snapshot = JsonSerializationExtensions.DeserializeFast(json);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Failed to deserialize metadata snapshot from cache");
        }

        return snapshot;
    }

    protected override void DisposeCore()
    {
        // Dispose options change token
        _optionsChangeToken?.Dispose();

        // Dispose semaphore locks
        _cacheLock?.Dispose();
        _cacheMissLock?.Dispose();

        // Dispose inner registry if disposable (sync path)
        if (_innerRegistry is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        // Dispose inner registry if async disposable
        if (_innerRegistry is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeCoreAsync().ConfigureAwait(false);
    }
}
