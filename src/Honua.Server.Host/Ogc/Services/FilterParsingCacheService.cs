// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Filter;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Caches parsed CQL and CQL2-JSON filter expressions to reduce parsing overhead.
/// Cache keys are based on filter text hash and layer schema version to ensure
/// correctness when schema changes.
/// </summary>
public sealed class FilterParsingCacheService : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly FilterParsingCacheOptions _options;
    private readonly FilterParsingCacheMetrics _metrics;
    private readonly ILogger<FilterParsingCacheService> _logger;

    public FilterParsingCacheService(
        IOptions<FilterParsingCacheOptions> options,
        FilterParsingCacheMetrics metrics,
        ILogger<FilterParsingCacheService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create dedicated memory cache for filter parsing
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _options.MaxEntries,
            CompactionPercentage = 0.25 // Remove 25% of entries when limit reached
        });

        _logger.LogInformation(
            "FilterParsingCache initialized with maxEntries={MaxEntries}, maxSizeBytes={MaxSizeBytes}",
            _options.MaxEntries,
            _options.MaxSizeBytes);
    }

    /// <summary>
    /// Gets a cached parsed filter or parses and caches it if not found.
    /// </summary>
    /// <param name="filterText">The raw filter text (CQL or CQL2-JSON)</param>
    /// <param name="filterLanguage">The filter language: "cql-text" or "cql2-json"</param>
    /// <param name="layer">The layer definition (used for field resolution and schema versioning)</param>
    /// <param name="filterCrs">The CRS for spatial filters (CQL2-JSON only)</param>
    /// <param name="parseFunc">Function to parse the filter if not cached</param>
    /// <returns>The parsed QueryFilter</returns>
    public QueryFilter GetOrParse(
        string filterText,
        string filterLanguage,
        LayerDefinition layer,
        string? filterCrs,
        Func<QueryFilter> parseFunc)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            throw new ArgumentException("Filter text cannot be null or empty", nameof(filterText));
        }

        if (layer == null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        if (parseFunc == null)
        {
            throw new ArgumentNullException(nameof(parseFunc));
        }

        // Generate cache key based on filter text, language, layer schema, and CRS
        var cacheKey = GenerateCacheKey(filterText, filterLanguage, layer, filterCrs);

        // Try to get from cache
        if (_cache.TryGetValue<CachedFilterEntry>(cacheKey, out var cachedEntry))
        {
            _metrics.RecordCacheHit(layer.ServiceId, layer.Id, filterLanguage);
            _logger.LogDebug(
                "Filter cache HIT for service={ServiceId}, layer={LayerId}, language={Language}, key={CacheKey}",
                layer.ServiceId, layer.Id, filterLanguage, cacheKey);
            return cachedEntry.Filter;
        }

        // Cache miss - parse the filter
        var stopwatch = Stopwatch.StartNew();
        QueryFilter parsedFilter;

        try
        {
            parsedFilter = parseFunc();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Filter parsing failed for service={ServiceId}, layer={LayerId}, language={Language}. Not caching error.",
                layer.ServiceId, layer.Id, filterLanguage);
            throw;
        }

        stopwatch.Stop();
        var parseTimeMs = stopwatch.ElapsedMilliseconds;

        // Estimate size of cached entry for memory management
        var estimatedSize = EstimateFilterSize(filterText, parsedFilter);

        // Only cache if within size limit
        if (estimatedSize <= _options.MaxSizeBytes)
        {
            var entry = new CachedFilterEntry(parsedFilter, estimatedSize);

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1) // Each entry counts as 1 toward the SizeLimit
                .SetSlidingExpiration(TimeSpan.FromMinutes(_options.SlidingExpirationMinutes))
                .RegisterPostEvictionCallback(OnEviction, this);

            _cache.Set(cacheKey, entry, cacheEntryOptions);

            _metrics.RecordCacheMiss(layer.ServiceId, layer.Id, filterLanguage, parseTimeMs);
            _logger.LogDebug(
                "Filter cache MISS for service={ServiceId}, layer={LayerId}, language={Language}, parseTimeMs={ParseTimeMs}, estimatedSize={EstimatedSize}, key={CacheKey}",
                layer.ServiceId, layer.Id, filterLanguage, parseTimeMs, estimatedSize, cacheKey);
        }
        else
        {
            _metrics.RecordCacheMiss(layer.ServiceId, layer.Id, filterLanguage, parseTimeMs);
            _logger.LogWarning(
                "Filter too large to cache: service={ServiceId}, layer={LayerId}, estimatedSize={EstimatedSize}, maxSize={MaxSize}",
                layer.ServiceId, layer.Id, estimatedSize, _options.MaxSizeBytes);
        }

        return parsedFilter;
    }

    /// <summary>
    /// Clears all cached filter entries.
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Remove 100% of entries
            _logger.LogInformation("Filter parsing cache cleared");
        }
    }

    /// <summary>
    /// Generates a stable cache key based on filter text, language, layer schema, and CRS.
    /// Format: "filter:{hash(filter_text)}:{layer_schema_hash}:{crs_hash}"
    /// </summary>
    private static string GenerateCacheKey(
        string filterText,
        string filterLanguage,
        LayerDefinition layer,
        string? filterCrs)
    {
        // Compute hash of filter text
        var filterHash = ComputeStableHash(filterText);

        // Compute hash of layer schema (fields + geometry info)
        var schemaHash = ComputeLayerSchemaHash(layer);

        // Include CRS in key for CQL2-JSON spatial filters
        var crsHash = string.IsNullOrWhiteSpace(filterCrs) ? "0" : ComputeStableHash(filterCrs);

        // Include language in key to handle same text parsed differently
        var langHash = filterLanguage.GetHashCode(StringComparison.OrdinalIgnoreCase).ToString("x8");

        return $"filter:{filterHash}:{schemaHash}:{crsHash}:{langHash}";
    }

    /// <summary>
    /// Computes a stable hash of a string using SHA256 (first 16 bytes as hex string).
    /// </summary>
    private static string ComputeStableHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);

        // Take first 16 bytes (128 bits) for cache key
        var truncated = new byte[16];
        Array.Copy(hashBytes, truncated, 16);

        return Convert.ToHexString(truncated).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a hash representing the layer's schema.
    /// This ensures cache invalidation when field definitions change.
    /// </summary>
    private static string ComputeLayerSchemaHash(LayerDefinition layer)
    {
        var sb = new StringBuilder();

        // Include critical layer properties
        sb.Append(layer.Id);
        sb.Append('|');
        sb.Append(layer.IdField);
        sb.Append('|');
        sb.Append(layer.GeometryField);
        sb.Append('|');
        sb.Append(layer.GeometryType);

        // Include field definitions (name and type)
        foreach (var field in layer.Fields.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append('|');
            sb.Append(field.Name);
            sb.Append(':');
            sb.Append(field.DataType ?? field.StorageType ?? "string");
        }

        return ComputeStableHash(sb.ToString());
    }

    /// <summary>
    /// Estimates the memory size of a cached filter entry.
    /// This is used to enforce the MaxSizeBytes limit.
    /// </summary>
    private static long EstimateFilterSize(string filterText, QueryFilter filter)
    {
        // Base size: filter text + overhead
        long size = filterText.Length * 2; // UTF-16 characters

        // Add estimated size of QueryFilter tree
        // This is approximate - we count expressions recursively
        size += EstimateExpressionSize(filter.Expression);

        // Add overhead for cache entry object
        size += 128; // Approximate object overhead

        return size;
    }

    /// <summary>
    /// Recursively estimates the size of a query expression tree.
    /// </summary>
    private static long EstimateExpressionSize(Core.Query.Expressions.QueryExpression? expression)
    {
        if (expression == null)
        {
            return 0;
        }

        long size = 64; // Base object overhead

        // Estimate based on expression type
        size += expression switch
        {
            Core.Query.Expressions.QueryBinaryExpression binary =>
                EstimateExpressionSize(binary.Left) + EstimateExpressionSize(binary.Right),

            Core.Query.Expressions.QueryUnaryExpression unary =>
                EstimateExpressionSize(unary.Operand),

            Core.Query.Expressions.QueryFunctionExpression function =>
                function.Name.Length * 2 +
                function.Arguments.Sum(arg => EstimateExpressionSize(arg)),

            Core.Query.Expressions.QueryFieldReference field =>
                field.Name.Length * 2,

            Core.Query.Expressions.QueryConstant constant =>
                EstimateConstantSize(constant.Value),

            _ => 64
        };

        return size;
    }

    /// <summary>
    /// Estimates the size of a constant value.
    /// </summary>
    private static long EstimateConstantSize(object? value)
    {
        if (value == null) return 8;

        return value switch
        {
            string s => s.Length * 2,
            byte[] b => b.Length,
            _ => 16 // Primitive types
        };
    }

    /// <summary>
    /// Called when a cache entry is evicted.
    /// </summary>
    private void OnEviction(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is CachedFilterEntry entry)
        {
            _metrics.RecordEviction(reason.ToString(), entry.EstimatedSizeBytes);

            if (reason == EvictionReason.Capacity)
            {
                _logger.LogDebug(
                    "Filter cache entry evicted due to capacity: key={Key}, size={Size}",
                    key, entry.EstimatedSizeBytes);
            }
        }
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    /// <summary>
    /// Represents a cached filter entry with metadata.
    /// </summary>
    private sealed record CachedFilterEntry(QueryFilter Filter, long EstimatedSizeBytes);
}
