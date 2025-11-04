// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Scrutor;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Decorator for <see cref="IDistributedCache"/> that adds comprehensive observability.
/// Automatically tracks cache hits, misses, operation durations, and errors using OpenTelemetry.
/// </summary>
/// <remarks>
/// This decorator transparently wraps any <see cref="IDistributedCache"/> implementation
/// and provides standardized metrics and structured logging without modifying cache behavior.
///
/// <para><b>Metrics Recorded:</b></para>
/// <list type="bullet">
/// <item>cache.hits - Counter of successful cache retrievals</item>
/// <item>cache.misses - Counter of cache misses</item>
/// <item>cache.operation.duration - Histogram of operation latencies</item>
/// <item>cache.errors - Counter of cache operation failures</item>
/// <item>cache.write_size - Histogram of bytes written to cache</item>
/// </list>
///
/// <para><b>Usage Example:</b></para>
/// <code>
/// services.AddStackExchangeRedisCache(options => { ... });
/// services.Decorate&lt;IDistributedCache, ObservableDistributedCache&gt;();
/// </code>
/// </remarks>
public sealed class ObservableDistributedCache : IDistributedCache
{
    private readonly IDistributedCache _inner;
    private readonly ILogger<ObservableDistributedCache> _logger;
    private readonly string _cacheName;

    // OpenTelemetry metrics
    private readonly Counter<long>? _hitCounter;
    private readonly Counter<long>? _missCounter;
    private readonly Counter<long>? _errorCounter;
    private readonly Histogram<double>? _operationDuration;
    private readonly Histogram<long>? _writeSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableDistributedCache"/> class.
    /// </summary>
    /// <param name="inner">The inner cache implementation to wrap.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="meterFactory">Optional meter factory for OpenTelemetry metrics. If null, metrics are disabled.</param>
    /// <param name="cacheName">Optional name to identify this cache instance in metrics. Defaults to "distributed".</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="logger"/> is null.</exception>
    public ObservableDistributedCache(
        IDistributedCache inner,
        ILogger<ObservableDistributedCache> logger,
        IMeterFactory? meterFactory = null,
        string? cacheName = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheName = cacheName ?? "distributed";

        if (meterFactory != null)
        {
            var meter = meterFactory.Create("Honua.Server.Cache");

            _hitCounter = meter.CreateCounter<long>(
                "honua.cache.hits",
                unit: "{hit}",
                description: "Number of cache hits");

            _missCounter = meter.CreateCounter<long>(
                "honua.cache.misses",
                unit: "{miss}",
                description: "Number of cache misses");

            _errorCounter = meter.CreateCounter<long>(
                "honua.cache.errors",
                unit: "{error}",
                description: "Number of cache operation errors");

            _operationDuration = meter.CreateHistogram<double>(
                "honua.cache.operation.duration",
                unit: "ms",
                description: "Cache operation duration in milliseconds");

            _writeSize = meter.CreateHistogram<long>(
                "honua.cache.write_size",
                unit: "bytes",
                description: "Size of data written to cache");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// COMPATIBILITY FIX (Issue #27): Provide synchronous wrapper for libraries that require it
    /// (e.g., ASP.NET Core Data Protection). We delegate to GetAsync with proper blocking.
    /// While not ideal, this prevents runtime failures while still logging the synchronous usage.
    /// </remarks>
    public byte[]? Get(string key)
    {
        _logger.LogWarning("Synchronous cache Get called for key {CacheKey}. Consider using GetAsync.", key);
        return GetAsync(key).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _inner.GetAsync(key, token).ConfigureAwait(false);
            stopwatch.Stop();

            if (result != null)
            {
                RecordCacheHit(key, stopwatch.Elapsed);
                _logger.LogDebug("Cache hit: {CacheKey} ({Duration}ms)",
                    key, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                RecordCacheMiss(key, stopwatch.Elapsed);
                _logger.LogDebug("Cache miss: {CacheKey} ({Duration}ms)",
                    key, stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordCacheError("get", ex, stopwatch.Elapsed);
            _logger.LogWarning(ex, "Cache get failed: {CacheKey} ({Duration}ms)",
                key, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// COMPATIBILITY FIX (Issue #27): Provide synchronous wrapper for libraries that require it.
    /// </remarks>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _logger.LogWarning("Synchronous cache Set called for key {CacheKey}. Consider using SetAsync.", key);
        SetAsync(key, value, options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _inner.SetAsync(key, value, options, token).ConfigureAwait(false);
            stopwatch.Stop();

            RecordCacheSet(key, value.Length, stopwatch.Elapsed);
            _logger.LogDebug("Cache set: {CacheKey}, size={Size} bytes ({Duration}ms)",
                key, value.Length, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordCacheError("set", ex, stopwatch.Elapsed);
            _logger.LogWarning(ex, "Cache set failed: {CacheKey}, size={Size} bytes ({Duration}ms)",
                key, value.Length, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// COMPATIBILITY FIX (Issue #27): Provide synchronous wrapper for libraries that require it.
    /// </remarks>
    public void Refresh(string key)
    {
        _logger.LogWarning("Synchronous cache Refresh called for key {CacheKey}. Consider using RefreshAsync.", key);
        RefreshAsync(key).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _inner.RefreshAsync(key, token).ConfigureAwait(false);
            stopwatch.Stop();

            RecordCacheOperation("refresh", stopwatch.Elapsed);
            _logger.LogDebug("Cache refresh: {CacheKey} ({Duration}ms)",
                key, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordCacheError("refresh", ex, stopwatch.Elapsed);
            _logger.LogWarning(ex, "Cache refresh failed: {CacheKey} ({Duration}ms)",
                key, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// COMPATIBILITY FIX (Issue #27): Provide synchronous wrapper for libraries that require it.
    /// </remarks>
    public void Remove(string key)
    {
        _logger.LogWarning("Synchronous cache Remove called for key {CacheKey}. Consider using RemoveAsync.", key);
        RemoveAsync(key).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _inner.RemoveAsync(key, token).ConfigureAwait(false);
            stopwatch.Stop();

            RecordCacheOperation("remove", stopwatch.Elapsed);
            _logger.LogDebug("Cache remove: {CacheKey} ({Duration}ms)",
                key, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordCacheError("remove", ex, stopwatch.Elapsed);
            _logger.LogWarning(ex, "Cache remove failed: {CacheKey} ({Duration}ms)",
                key, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private void RecordCacheHit(string key, TimeSpan duration)
    {
        _hitCounter?.Add(1,
            new("cache.name", _cacheName),
            new("cache.key.pattern", GetKeyPattern(key)));

        _operationDuration?.Record(duration.TotalMilliseconds,
            new("cache.name", _cacheName),
            new("operation", "get"),
            new("result", "hit"));
    }

    private void RecordCacheMiss(string key, TimeSpan duration)
    {
        _missCounter?.Add(1,
            new("cache.name", _cacheName),
            new("cache.key.pattern", GetKeyPattern(key)));

        _operationDuration?.Record(duration.TotalMilliseconds,
            new("cache.name", _cacheName),
            new("operation", "get"),
            new("result", "miss"));
    }

    private void RecordCacheSet(string key, long sizeBytes, TimeSpan duration)
    {
        _operationDuration?.Record(duration.TotalMilliseconds,
            new("cache.name", _cacheName),
            new("operation", "set"));

        _writeSize?.Record(sizeBytes,
            new("cache.name", _cacheName),
            new("size.bucket", GetSizeBucket(sizeBytes)));
    }

    private void RecordCacheOperation(string operation, TimeSpan duration)
    {
        _operationDuration?.Record(duration.TotalMilliseconds,
            new("cache.name", _cacheName),
            new("operation", operation));
    }

    private void RecordCacheError(string operation, Exception ex, TimeSpan duration)
    {
        _errorCounter?.Add(1,
            new("cache.name", _cacheName),
            new("operation", operation),
            new("error.type", ex.GetType().Name));

        _operationDuration?.Record(duration.TotalMilliseconds,
            new("cache.name", _cacheName),
            new("operation", operation),
            new("result", "error"));
    }

    // Cache SearchValues for improved performance (CA1870)
    private static readonly SearchValues<char> _separatorChars = SearchValues.Create([':', '-', '_']);

    /// <summary>
    /// Extracts a pattern from the cache key for cardinality reduction in metrics.
    /// For example, "tile:123:456" becomes "tile", "metadata:layer:foo" becomes "metadata".
    /// </summary>
    private static string GetKeyPattern(string? cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return "unknown";
        }

        // Extract pattern from cache key (e.g., "tile:123:456" -> "tile")
        var separatorIndex = cacheKey.AsSpan().IndexOfAny(_separatorChars);
        return separatorIndex > 0 ? cacheKey.Substring(0, separatorIndex) : "unknown";
    }

    /// <summary>
    /// Categorizes cache entry size into buckets for better metric aggregation.
    /// </summary>
    private static string GetSizeBucket(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 1024 => "tiny",              // < 1KB
            < 10240 => "small",            // < 10KB
            < 102400 => "medium",          // < 100KB
            < 1048576 => "large",          // < 1MB
            < 10485760 => "very_large",    // < 10MB
            _ => "huge"                    // >= 10MB
        };
    }
}

/// <summary>
/// Decorator for <see cref="IMemoryCache"/> that adds comprehensive observability.
/// Automatically tracks cache hits, misses, and evictions using OpenTelemetry.
/// </summary>
/// <remarks>
/// This decorator transparently wraps any <see cref="IMemoryCache"/> implementation
/// and provides standardized metrics and structured logging without modifying cache behavior.
///
/// <para><b>Usage Example:</b></para>
/// <code>
/// services.AddMemoryCache();
/// services.Decorate&lt;IMemoryCache, ObservableMemoryCache&gt;();
/// </code>
/// </remarks>
public sealed class ObservableMemoryCache : IMemoryCache
{
    private readonly IMemoryCache _inner;
    private readonly ILogger<ObservableMemoryCache> _logger;
    private readonly string _cacheName;

    // Cache SearchValues for improved performance (CA1870)
    private static readonly SearchValues<char> _memoryCacheSeparatorChars = SearchValues.Create([':', '-', '_']);

    // OpenTelemetry metrics
    private readonly Counter<long>? _hitCounter;
    private readonly Counter<long>? _missCounter;
    private readonly Counter<long>? _evictionCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableMemoryCache"/> class.
    /// </summary>
    /// <param name="inner">The inner cache implementation to wrap.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="meterFactory">Optional meter factory for OpenTelemetry metrics. If null, metrics are disabled.</param>
    /// <param name="cacheName">Optional name to identify this cache instance in metrics. Defaults to "memory".</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="logger"/> is null.</exception>
    public ObservableMemoryCache(
        IMemoryCache inner,
        ILogger<ObservableMemoryCache> logger,
        IMeterFactory? meterFactory = null,
        string? cacheName = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheName = cacheName ?? "memory";

        if (meterFactory != null)
        {
            var meter = meterFactory.Create("Honua.Server.Cache");

            _hitCounter = meter.CreateCounter<long>(
                "honua.cache.hits",
                unit: "{hit}",
                description: "Number of cache hits");

            _missCounter = meter.CreateCounter<long>(
                "honua.cache.misses",
                unit: "{miss}",
                description: "Number of cache misses");

            _evictionCounter = meter.CreateCounter<long>(
                "honua.cache.evictions",
                unit: "{eviction}",
                description: "Number of cache evictions");
        }
    }

    /// <inheritdoc />
    public bool TryGetValue(object key, out object? value)
    {
        var found = _inner.TryGetValue(key, out value);

        if (found)
        {
            _hitCounter?.Add(1,
                new("cache.name", _cacheName),
                new("cache.key.pattern", GetKeyPattern(key)));

            _logger.LogTrace("Memory cache hit: {CacheKey}", key);
        }
        else
        {
            _missCounter?.Add(1,
                new("cache.name", _cacheName),
                new("cache.key.pattern", GetKeyPattern(key)));

            _logger.LogTrace("Memory cache miss: {CacheKey}", key);
        }

        return found;
    }

    /// <inheritdoc />
    public ICacheEntry CreateEntry(object key)
    {
        var entry = _inner.CreateEntry(key);

        // Wrap the entry to intercept eviction callbacks
        return new ObservableCacheEntry(entry, key, _logger, _evictionCounter, _cacheName);
    }

    /// <inheritdoc />
    public void Remove(object key)
    {
        _inner.Remove(key);
        _logger.LogTrace("Memory cache remove: {CacheKey}", key);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _inner.Dispose();
    }

    private static string GetKeyPattern(object? key)
    {
        if (key == null)
        {
            return "unknown";
        }

        var keyString = key.ToString();
        if (string.IsNullOrWhiteSpace(keyString))
        {
            return "unknown";
        }

        var separatorIndex = keyString.AsSpan().IndexOfAny(_memoryCacheSeparatorChars);
        return separatorIndex > 0 ? keyString.Substring(0, separatorIndex) : "unknown";
    }

    /// <summary>
    /// Wrapper for <see cref="ICacheEntry"/> that intercepts eviction callbacks for metrics.
    /// </summary>
    private sealed class ObservableCacheEntry : ICacheEntry
    {
        private readonly ICacheEntry _inner;
        private readonly object _key;
        private readonly ILogger _logger;
        private readonly Counter<long>? _evictionCounter;
        private readonly string _cacheName;

        public ObservableCacheEntry(
            ICacheEntry inner,
            object key,
            ILogger logger,
            Counter<long>? evictionCounter,
            string cacheName)
        {
            _inner = inner;
            _key = key;
            _logger = logger;
            _evictionCounter = evictionCounter;
            _cacheName = cacheName;

            // Register our own eviction callback
            _inner.RegisterPostEvictionCallback(OnEviction);
        }

        public object Key => _inner.Key;
        public object? Value
        {
            get => _inner.Value;
            set => _inner.Value = value;
        }
        public DateTimeOffset? AbsoluteExpiration
        {
            get => _inner.AbsoluteExpiration;
            set => _inner.AbsoluteExpiration = value;
        }
        public TimeSpan? AbsoluteExpirationRelativeToNow
        {
            get => _inner.AbsoluteExpirationRelativeToNow;
            set => _inner.AbsoluteExpirationRelativeToNow = value;
        }
        public TimeSpan? SlidingExpiration
        {
            get => _inner.SlidingExpiration;
            set => _inner.SlidingExpiration = value;
        }
        public IList<IChangeToken> ExpirationTokens => _inner.ExpirationTokens;
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks => _inner.PostEvictionCallbacks;
        public CacheItemPriority Priority
        {
            get => _inner.Priority;
            set => _inner.Priority = value;
        }
        public long? Size
        {
            get => _inner.Size;
            set => _inner.Size = value;
        }

        public void Dispose() => _inner.Dispose();

        private void OnEviction(object key, object? value, EvictionReason reason, object? state)
        {
            _evictionCounter?.Add(1,
                new("cache.name", _cacheName),
                new("eviction.reason", NormalizeEvictionReason(reason)));

            _logger.LogTrace("Memory cache eviction: {CacheKey}, reason={Reason}", key, reason);
        }

        private static string NormalizeEvictionReason(EvictionReason reason)
        {
            return reason switch
            {
                EvictionReason.Removed => "removed",
                EvictionReason.Replaced => "replaced",
                EvictionReason.Expired => "expired",
                EvictionReason.TokenExpired => "token_expired",
                EvictionReason.Capacity => "capacity",
                EvictionReason.None => "none",
                _ => "unknown"
            };
        }
    }
}

/// <summary>
/// Extension methods for easily registering observable cache decorators.
/// </summary>
public static class CacheObservabilityExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="IDistributedCache"/> with <see cref="ObservableDistributedCache"/>
    /// to add automatic metrics and logging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cacheName">Optional name to identify this cache in metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method uses the Decorator pattern to wrap an existing IDistributedCache registration.
    /// It must be called AFTER the cache has been registered (e.g., AddStackExchangeRedisCache).
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// services.AddStackExchangeRedisCache(options => { ... });
    /// services.AddObservableDistributedCache("redis");
    /// </code>
    /// </remarks>
    public static IServiceCollection AddObservableDistributedCache(
        this IServiceCollection services,
        string? cacheName = null)
    {
        services.Decorate<IDistributedCache>((inner, provider) =>
        {
            var logger = provider.GetRequiredService<ILogger<ObservableDistributedCache>>();
            var meterFactory = provider.GetService<IMeterFactory>();
            return new ObservableDistributedCache(inner, logger, meterFactory, cacheName);
        });

        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="IMemoryCache"/> with <see cref="ObservableMemoryCache"/>
    /// to add automatic metrics and logging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cacheName">Optional name to identify this cache in metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method uses the Decorator pattern to wrap an existing IMemoryCache registration.
    /// It must be called AFTER the cache has been registered (e.g., AddMemoryCache).
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// services.AddMemoryCache();
    /// services.AddObservableMemoryCache("inmemory");
    /// </code>
    /// </remarks>
    public static IServiceCollection AddObservableMemoryCache(
        this IServiceCollection services,
        string? cacheName = null)
    {
        services.Decorate<IMemoryCache>((inner, provider) =>
        {
            var logger = provider.GetRequiredService<ILogger<ObservableMemoryCache>>();
            var meterFactory = provider.GetService<IMeterFactory>();
            return new ObservableMemoryCache(inner, logger, meterFactory, cacheName);
        });

        return services;
    }

    /// <summary>
    /// Simple service decorator extension to wrap an existing service with a decorator.
    /// </summary>
    private static void Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> decorator)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(TService).Name} is not registered.");
        }

        services.Remove(descriptor);

        services.Add(new ServiceDescriptor(
            typeof(TService),
            provider =>
            {
                TService inner;
                if (descriptor.ImplementationInstance != null)
                {
                    inner = (TService)descriptor.ImplementationInstance;
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    inner = (TService)descriptor.ImplementationFactory(provider);
                }
                else if (descriptor.ImplementationType != null)
                {
                    inner = (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
                }
                else
                {
                    throw new InvalidOperationException("Unable to resolve inner service instance.");
                }

                return decorator(inner, provider);
            },
            descriptor.Lifetime));
    }
}
