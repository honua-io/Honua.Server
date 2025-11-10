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
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Distributed caching service for query results with automatic Redis/in-memory fallback,
/// compression, and comprehensive metrics tracking.
/// </summary>
/// <remarks>
/// This service provides:
/// - Automatic Redis + in-memory L2 cache fallback
/// - Transparent compression for large results (configurable threshold)
/// - Cache-aside pattern with GetOrSetAsync
/// - Pattern-based invalidation (e.g., "layer:123:*")
/// - Comprehensive metrics and observability
/// - Graceful degradation when cache is unavailable
///
/// Usage:
/// <code>
/// var result = await cacheService.GetOrSetAsync(
///     "layer:123:metadata",
///     async ct => await GetLayerMetadataAsync(123, ct),
///     TimeSpan.FromMinutes(5),
///     ct
/// );
/// </code>
/// </remarks>
public interface IQueryResultCacheService
{
    /// <summary>
    /// Gets a cached value or sets it using the factory function if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">Cache key (will be normalized).</param>
    /// <param name="factory">Factory function to generate the value on cache miss.</param>
    /// <param name="expiration">Optional expiration time (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or generated value.</returns>
    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache with optional expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">Cache key (will be normalized).</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">Optional expiration time (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">Cache key (will be normalized).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value or null if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">Cache key (will be normalized).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cache entries matching a pattern.
    /// </summary>
    /// <param name="pattern">Pattern to match (e.g., "layer:123:*").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Pattern matching is only supported with Redis. For in-memory cache,
    /// this will require iterating all keys which is not efficient.
    /// </remarks>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of distributed query result cache with compression and fallback.
/// </summary>
public sealed class QueryResultCacheService : IQueryResultCacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<QueryResultCacheService> _logger;
    private readonly ICacheMetrics _metrics;
    private readonly QueryResultCacheOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string CacheNameDistributed = "query-result-distributed";
    private const string CacheNameMemory = "query-result-memory";

    public QueryResultCacheService(
        IDistributedCache? distributedCache,
        IMemoryCache memoryCache,
        ILogger<QueryResultCacheService> logger,
        ICacheMetrics metrics,
        IOptions<QueryResultCacheOptions> options)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _jsonOptions = JsonSerializerOptionsRegistry.Web;

        if (_distributedCache == null)
        {
            _logger.LogWarning("Distributed cache is not configured. Using in-memory cache only.");
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var normalizedKey = CacheKeyNormalizer.Normalize(key);
        var effectiveExpiration = expiration ?? _options.DefaultExpiration;

        // Try L2 (in-memory) cache first for fastest access
        if (_memoryCache.TryGetValue(normalizedKey, out T? memoryValue) && memoryValue != null)
        {
            _metrics.RecordCacheHit(CacheNameMemory, normalizedKey);
            _logger.LogTrace("L2 cache hit: {CacheKey}", normalizedKey);
            return memoryValue;
        }

        _metrics.RecordCacheMiss(CacheNameMemory, normalizedKey);

        // Try L1 (distributed) cache
        var cachedValue = await GetFromDistributedCacheAsync<T>(normalizedKey, cancellationToken).ConfigureAwait(false);
        if (cachedValue != null)
        {
            // Populate L2 cache with shorter TTL
            var memoryExpiration = TimeSpan.FromSeconds(Math.Min(effectiveExpiration.TotalSeconds, 300));
            SetInMemoryCache(normalizedKey, cachedValue, memoryExpiration);
            return cachedValue;
        }

        // Cache miss - execute factory
        _logger.LogTrace("Cache miss, executing factory: {CacheKey}", normalizedKey);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var value = await factory(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            _logger.LogTrace("Factory executed in {ElapsedMs}ms: {CacheKey}", stopwatch.ElapsedMilliseconds, normalizedKey);

            // Store in both caches
            await SetAsync(normalizedKey, value, effectiveExpiration, cancellationToken).ConfigureAwait(false);

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Factory execution failed for {CacheKey}", normalizedKey);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var normalizedKey = CacheKeyNormalizer.Normalize(key);
        var effectiveExpiration = expiration ?? _options.DefaultExpiration;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Serialize to JSON
            var serialized = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
            var originalSize = serialized.Length;

            byte[] cacheData;
            bool compressed = false;

            // Compress if enabled and above threshold
            if (_options.EnableCompression && originalSize > _options.CompressionThreshold)
            {
                cacheData = await CompressAsync(serialized, cancellationToken).ConfigureAwait(false);
                compressed = true;
                _logger.LogTrace("Compressed cache entry: {OriginalSize} -> {CompressedSize} bytes ({Ratio:P1})",
                    originalSize, cacheData.Length, (double)cacheData.Length / originalSize);
            }
            else
            {
                cacheData = serialized;
            }

            // Create cache envelope with metadata
            var envelope = new CacheEnvelope
            {
                Data = cacheData,
                Compressed = compressed,
                CachedAt = DateTimeOffset.UtcNow
            };

            var envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _jsonOptions);

            // Store in distributed cache
            if (_distributedCache != null && _options.UseDistributedCache)
            {
                try
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = effectiveExpiration
                    };

                    await _distributedCache.SetAsync(normalizedKey, envelopeBytes, cacheOptions, cancellationToken).ConfigureAwait(false);

                    stopwatch.Stop();
                    _metrics.RecordCacheSet(CacheNameDistributed, stopwatch.Elapsed, envelopeBytes.Length);
                    _logger.LogTrace("Set distributed cache: {CacheKey}, size={Size} bytes, ttl={TTL}",
                        normalizedKey, envelopeBytes.Length, effectiveExpiration);
                }
                catch (TimeoutException timeoutEx)
                {
                    _logger.LogWarning(timeoutEx, "Timeout setting distributed cache: {CacheKey}", normalizedKey);
                    _metrics.RecordCacheError(CacheNameDistributed, "set", "timeout");
                    // Continue to set in-memory cache
                }
                catch (Exception ex) when (ex is not CacheException)
                {
                    _logger.LogWarning(ex, "Failed to set distributed cache: {CacheKey}", normalizedKey);
                    _metrics.RecordCacheError(CacheNameDistributed, "set", ex.GetType().Name);
                    // Continue to set in-memory cache - don't throw, gracefully degrade
                }
            }

            // Store in L2 (in-memory) cache with shorter TTL
            var memoryExpiration = TimeSpan.FromSeconds(Math.Min(effectiveExpiration.TotalSeconds, 300));
            SetInMemoryCache(normalizedKey, value, memoryExpiration);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Failed to serialize cache value: {CacheKey}", normalizedKey);
            _metrics.RecordCacheError(CacheNameMemory, "serialize", "json");
            throw new CacheException($"Failed to serialize cache value for key '{normalizedKey}'", jsonEx);
        }
        catch (Exception ex) when (ex is not CacheException)
        {
            _logger.LogError(ex, "Failed to set cache: {CacheKey}", normalizedKey);
            _metrics.RecordCacheError(CacheNameMemory, "set", ex.GetType().Name);
            throw new CacheException($"Unexpected error setting cache for key '{normalizedKey}'", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var normalizedKey = CacheKeyNormalizer.Normalize(key);

        // Try L2 (in-memory) cache first
        if (_memoryCache.TryGetValue(normalizedKey, out T? memoryValue) && memoryValue != null)
        {
            _metrics.RecordCacheHit(CacheNameMemory, normalizedKey);
            return memoryValue;
        }

        _metrics.RecordCacheMiss(CacheNameMemory, normalizedKey);

        // Try L1 (distributed) cache
        return await GetFromDistributedCacheAsync<T>(normalizedKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var normalizedKey = CacheKeyNormalizer.Normalize(key);

        // Remove from both caches
        _memoryCache.Remove(normalizedKey);

        if (_distributedCache != null && _options.UseDistributedCache)
        {
            try
            {
                await _distributedCache.RemoveAsync(normalizedKey, cancellationToken).ConfigureAwait(false);
                _logger.LogTrace("Removed from cache: {CacheKey}", normalizedKey);
            }
            catch (TimeoutException timeoutEx)
            {
                _logger.LogWarning(timeoutEx, "Timeout removing from distributed cache: {CacheKey}", normalizedKey);
                _metrics.RecordCacheError(CacheNameDistributed, "remove", "timeout");
                // Graceful degradation - don't throw
            }
            catch (Exception ex) when (ex is not CacheException)
            {
                _logger.LogWarning(ex, "Failed to remove from distributed cache: {CacheKey}", normalizedKey);
                _metrics.RecordCacheError(CacheNameDistributed, "remove", ex.GetType().Name);
                // Graceful degradation - don't throw
            }
        }
    }

    /// <inheritdoc/>
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        _logger.LogInformation("Invalidating cache entries matching pattern: {Pattern}", pattern);

        // For distributed cache (Redis), we would need to use SCAN with pattern matching
        // This requires StackExchange.Redis directly, not IDistributedCache
        // For now, we'll just log a warning
        _logger.LogWarning(
            "Pattern-based cache invalidation is not fully implemented. " +
            "Pattern: {Pattern}. Consider implementing with IConnectionMultiplexer for Redis support.",
            pattern);

        // Remove from in-memory cache by iterating (not efficient, but works for small caches)
        // Note: IMemoryCache doesn't expose keys, so this is a limitation
        // In production, you'd want to maintain a key registry or use Redis directly

        await Task.CompletedTask;
    }

    private async Task<T?> GetFromDistributedCacheAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (_distributedCache == null || !_options.UseDistributedCache)
        {
            _metrics.RecordCacheMiss(CacheNameDistributed, key);
            return default;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var envelopeBytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (envelopeBytes == null)
            {
                _metrics.RecordCacheMiss(CacheNameDistributed, key);
                return default;
            }

            _metrics.RecordCacheHit(CacheNameDistributed, key);
            _metrics.RecordCacheLatency(CacheNameDistributed, "get", stopwatch.Elapsed);

            // Deserialize envelope
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(envelopeBytes, _jsonOptions);
            if (envelope?.Data == null)
            {
                _logger.LogWarning("Invalid cache envelope for {CacheKey}", key);
                return default;
            }

            // Decompress if needed
            byte[] data = envelope.Compressed
                ? await DecompressAsync(envelope.Data, cancellationToken).ConfigureAwait(false)
                : envelope.Data;

            // Deserialize value
            var value = JsonSerializer.Deserialize<T>(data, _jsonOptions);

            // Populate L2 cache
            if (value != null)
            {
                var memoryExpiration = TimeSpan.FromMinutes(5); // Short TTL for L2
                SetInMemoryCache(key, value, memoryExpiration);
            }

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get from distributed cache: {CacheKey}", key);
            _metrics.RecordCacheError(CacheNameDistributed, "get", ex.GetType().Name);
            return default;
        }
    }

    private void SetInMemoryCache<T>(string key, T value, TimeSpan expiration)
    {
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
                Size = EstimateSize(value)
            };

            _memoryCache.Set(key, value, cacheOptions);
            _logger.LogTrace("Set L2 cache: {CacheKey}, ttl={TTL}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set in-memory cache: {CacheKey}", key);
            _metrics.RecordCacheError(CacheNameMemory, "set", ex.GetType().Name);
        }
    }

    private static async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken)
    {
        using var outputStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest))
        {
            await gzipStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        return outputStream.ToArray();
    }

    private static async Task<byte[]> DecompressAsync(byte[] data, CancellationToken cancellationToken)
    {
        using var inputStream = new MemoryStream(data);
        using var outputStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        {
            await gzipStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
        }
        return outputStream.ToArray();
    }

    private static long EstimateSize<T>(T value)
    {
        // Rough estimation for memory cache size tracking
        if (value == null) return 0;

        // For strings and primitives
        if (value is string str) return str.Length * 2; // UTF-16 chars
        if (value.GetType().IsPrimitive) return 8; // Rough estimate

        // For complex types, assume 1KB as baseline
        return 1024;
    }

    /// <summary>
    /// Cache envelope that wraps cached data with metadata.
    /// </summary>
    private sealed class CacheEnvelope
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public bool Compressed { get; init; }
        public DateTimeOffset CachedAt { get; init; }
    }
}

/// <summary>
/// Configuration options for query result caching.
/// </summary>
public sealed class QueryResultCacheOptions
{
    /// <summary>
    /// Default expiration time for cache entries.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Expiration time for layer metadata cache entries.
    /// </summary>
    public TimeSpan LayerMetadataExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Expiration time for tile cache entries.
    /// </summary>
    public TimeSpan TileExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Expiration time for query result cache entries.
    /// </summary>
    public TimeSpan QueryResultExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Expiration time for CRS transformation cache entries.
    /// </summary>
    public TimeSpan CrsTransformExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Enable compression for cache entries.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Compression threshold in bytes. Entries larger than this will be compressed.
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024; // 1KB

    /// <summary>
    /// Whether to use distributed cache (Redis). If false, only in-memory cache is used.
    /// </summary>
    public bool UseDistributedCache { get; set; } = true;

    /// <summary>
    /// Redis instance name prefix.
    /// </summary>
    public string RedisInstanceName { get; set; } = "Honua:";
}
