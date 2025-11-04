// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for cache operations (Redis, in-memory, distributed).
/// Tracks cache hit/miss rates, latency, and eviction patterns.
/// </summary>
public interface ICacheMetrics
{
    void RecordCacheHit(string cacheName, string? cacheKey, string? region = null);
    void RecordCacheMiss(string cacheName, string? cacheKey, string? region = null);
    void RecordCacheSet(string cacheName, TimeSpan duration, long? sizeBytes = null);
    void RecordCacheEviction(string cacheName, string evictionReason);
    void RecordCacheLatency(string cacheName, string operation, TimeSpan duration);
    void RecordCacheSize(string cacheName, long sizeBytes, int itemCount);
    void RecordCacheError(string cacheName, string operation, string errorType);
}

/// <summary>
/// Implementation of cache metrics using OpenTelemetry.
/// </summary>
public sealed class CacheMetrics : ICacheMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _cacheWrites;
    private readonly Counter<long> _cacheEvictions;
    private readonly Counter<long> _cacheErrors;
    private readonly Histogram<double> _cacheLatency;
    private readonly Histogram<double> _cacheWriteSize;

    public CacheMetrics()
    {
        _meter = new Meter("Honua.Server.Cache", "1.0.0");

        _cacheHits = _meter.CreateCounter<long>(
            "honua.cache.hits",
            unit: "{hit}",
            description: "Number of cache hits by cache name and region");

        _cacheMisses = _meter.CreateCounter<long>(
            "honua.cache.misses",
            unit: "{miss}",
            description: "Number of cache misses by cache name and region");

        _cacheWrites = _meter.CreateCounter<long>(
            "honua.cache.writes",
            unit: "{write}",
            description: "Number of cache write operations");

        _cacheEvictions = _meter.CreateCounter<long>(
            "honua.cache.evictions",
            unit: "{eviction}",
            description: "Number of cache evictions by reason");

        _cacheErrors = _meter.CreateCounter<long>(
            "honua.cache.errors",
            unit: "{error}",
            description: "Number of cache operation errors");

        _cacheLatency = _meter.CreateHistogram<double>(
            "honua.cache.operation_duration",
            unit: "ms",
            description: "Cache operation latency by cache name and operation type");

        _cacheWriteSize = _meter.CreateHistogram<double>(
            "honua.cache.write_size",
            unit: "bytes",
            description: "Size of data written to cache");
    }

    public void RecordCacheHit(string cacheName, string? cacheKey, string? region = null)
    {
        _cacheHits.Add(1,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("cache.region", Normalize(region)),
            new("cache.key.pattern", GetKeyPattern(cacheKey)));
    }

    public void RecordCacheMiss(string cacheName, string? cacheKey, string? region = null)
    {
        _cacheMisses.Add(1,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("cache.region", Normalize(region)),
            new("cache.key.pattern", GetKeyPattern(cacheKey)));
    }

    public void RecordCacheSet(string cacheName, TimeSpan duration, long? sizeBytes = null)
    {
        _cacheWrites.Add(1,
            new KeyValuePair<string, object?>[] { new("cache.name", NormalizeCacheName(cacheName)) });

        _cacheLatency.Record(duration.TotalMilliseconds,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("operation", "set"));

        if (sizeBytes.HasValue)
        {
            _cacheWriteSize.Record(sizeBytes.Value,
                new("cache.name", NormalizeCacheName(cacheName)),
                new("size.bucket", GetSizeBucket(sizeBytes.Value)));
        }
    }

    public void RecordCacheEviction(string cacheName, string evictionReason)
    {
        _cacheEvictions.Add(1,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("eviction.reason", NormalizeEvictionReason(evictionReason)));
    }

    public void RecordCacheLatency(string cacheName, string operation, TimeSpan duration)
    {
        _cacheLatency.Record(duration.TotalMilliseconds,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("operation", NormalizeOperation(operation)));
    }

    public void RecordCacheSize(string cacheName, long sizeBytes, int itemCount)
    {
        // This would typically be recorded via an observable gauge
        // For now, we'll use a histogram to track size distributions
        _cacheWriteSize.Record(sizeBytes,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("metric.type", "total_size"));
    }

    public void RecordCacheError(string cacheName, string operation, string errorType)
    {
        _cacheErrors.Add(1,
            new("cache.name", NormalizeCacheName(cacheName)),
            new("operation", NormalizeOperation(operation)),
            new("error.type", Normalize(errorType)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string NormalizeCacheName(string? cacheName)
    {
        if (string.IsNullOrWhiteSpace(cacheName))
            return "unknown";

        return cacheName.ToLowerInvariant() switch
        {
            var name when name.Contains("redis") => "redis",
            var name when name.Contains("memory") => "memory",
            var name when name.Contains("raster") => "raster",
            var name when name.Contains("vector") => "vector",
            var name when name.Contains("tile") => "tile",
            var name when name.Contains("metadata") => "metadata",
            var name when name.Contains("session") => "session",
            _ => cacheName.ToLowerInvariant()
        };
    }

    private static string NormalizeOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return "unknown";

        return operation.ToLowerInvariant() switch
        {
            "get" or "read" => "get",
            "set" or "write" or "put" => "set",
            "delete" or "remove" => "delete",
            "exists" or "contains" => "exists",
            "clear" or "flush" => "clear",
            _ => operation.ToLowerInvariant()
        };
    }

    private static string NormalizeEvictionReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "unknown";

        return reason.ToLowerInvariant() switch
        {
            var r when r.Contains("capacity") || r.Contains("size") => "capacity",
            var r when r.Contains("ttl") || r.Contains("expir") => "expiration",
            var r when r.Contains("memory") => "memory_pressure",
            var r when r.Contains("manual") || r.Contains("explicit") => "manual",
            _ => reason.ToLowerInvariant()
        };
    }

    private static string GetKeyPattern(string? cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return "unknown";

        // Extract pattern from cache key (e.g., "tile:123:456" -> "tile")
        var parts = cacheKey.Split(':', 2);
        return parts.Length > 0 ? parts[0] : "unknown";
    }

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
