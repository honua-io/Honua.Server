// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Honua.Server.Core.Performance;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Fluent builder for constructing cache keys with consistent naming conventions.
/// Provides type-safe, hierarchical cache key generation with automatic hashing of complex parameters.
/// </summary>
/// <remarks>
/// Cache key conventions:
/// - Layers: "layer:{serviceId}:{layerId}:metadata"
/// - Tiles: "tile:{tileMatrixSet}:{z}:{x}:{y}:{format}"
/// - Queries: "query:{layerId}:{bboxHash}:{filterHash}"
/// - CRS: "crs:{sourceCrs}:{targetCrs}:{boundsHash}"
/// - STAC: "stac:{type}:{collectionId}:{itemId}"
///
/// Usage:
/// <code>
/// var key = CacheKeyBuilder.ForLayer("service1", "layer1")
///     .WithSuffix("metadata")
///     .Build();
/// // Result: "layer:service1:layer1:metadata"
///
/// var tileKey = CacheKeyBuilder.ForTile("WebMercatorQuad", 5, 10, 12, "pbf")
///     .Build();
/// // Result: "tile:WebMercatorQuad:5:10:12:pbf"
/// </code>
/// </remarks>
public sealed class CacheKeyBuilder
{
    private readonly StringBuilder _keyBuilder;
    private bool _isBuilt;

    private CacheKeyBuilder(string prefix)
    {
        _keyBuilder = new StringBuilder(prefix);
    }

    /// <summary>
    /// Creates a cache key builder for layer metadata.
    /// </summary>
    /// <param name="serviceId">Service identifier.</param>
    /// <param name="layerId">Layer identifier.</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForLayer(string serviceId, string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        var sanitizedServiceId = CacheKeyNormalizer.SanitizeForRedis(serviceId);
        var sanitizedLayerId = CacheKeyNormalizer.SanitizeForRedis(layerId);

        return new CacheKeyBuilder($"layer:{sanitizedServiceId}:{sanitizedLayerId}");
    }

    /// <summary>
    /// Creates a cache key builder for tile data.
    /// </summary>
    /// <param name="tileMatrixSet">Tile matrix set identifier (e.g., "WebMercatorQuad").</param>
    /// <param name="z">Zoom level.</param>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="format">Tile format (e.g., "pbf", "png", "webp").</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForTile(string tileMatrixSet, int z, int x, int y, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tileMatrixSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var sanitizedTms = CacheKeyNormalizer.SanitizeForRedis(tileMatrixSet);
        var sanitizedFormat = CacheKeyNormalizer.SanitizeForRedis(format);

        return new CacheKeyBuilder(
            $"tile:{sanitizedTms}:{z}:{x}:{y}:{sanitizedFormat}");
    }

    /// <summary>
    /// Creates a cache key builder for query results.
    /// </summary>
    /// <param name="layerId">Layer identifier.</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForQuery(string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        var sanitizedLayerId = CacheKeyNormalizer.SanitizeForRedis(layerId);
        return new CacheKeyBuilder($"query:{sanitizedLayerId}");
    }

    /// <summary>
    /// Creates a cache key builder for CRS transformation data.
    /// </summary>
    /// <param name="sourceCrs">Source CRS code (e.g., "EPSG:4326").</param>
    /// <param name="targetCrs">Target CRS code (e.g., "EPSG:3857").</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForCrsTransform(string sourceCrs, string targetCrs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCrs);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCrs);

        var sanitizedSource = CacheKeyNormalizer.SanitizeForRedis(sourceCrs);
        var sanitizedTarget = CacheKeyNormalizer.SanitizeForRedis(targetCrs);

        return new CacheKeyBuilder($"crs:{sanitizedSource}:{sanitizedTarget}");
    }

    /// <summary>
    /// Creates a cache key builder for STAC collections.
    /// </summary>
    /// <param name="collectionId">Collection identifier (optional for list operations).</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForStacCollection(string? collectionId = null)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return new CacheKeyBuilder("stac:collections");
        }

        var sanitizedId = CacheKeyNormalizer.SanitizeForRedis(collectionId);
        return new CacheKeyBuilder($"stac:collection:{sanitizedId}");
    }

    /// <summary>
    /// Creates a cache key builder for STAC items.
    /// </summary>
    /// <param name="collectionId">Collection identifier.</param>
    /// <param name="itemId">Item identifier (optional for list operations).</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForStacItem(string collectionId, string? itemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        var sanitizedCollectionId = CacheKeyNormalizer.SanitizeForRedis(collectionId);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return new CacheKeyBuilder($"stac:collection:{sanitizedCollectionId}:items");
        }

        var sanitizedItemId = CacheKeyNormalizer.SanitizeForRedis(itemId);
        return new CacheKeyBuilder($"stac:item:{sanitizedCollectionId}:{sanitizedItemId}");
    }

    /// <summary>
    /// Creates a cache key builder for STAC search results.
    /// </summary>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForStacSearch()
    {
        return new CacheKeyBuilder("stac:search");
    }

    /// <summary>
    /// Creates a cache key builder for OGC API metadata.
    /// </summary>
    /// <param name="api">API type (e.g., "features", "tiles", "coverages").</param>
    /// <returns>Cache key builder.</returns>
    public static CacheKeyBuilder ForOgcApi(string api)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(api);

        var sanitizedApi = CacheKeyNormalizer.SanitizeForRedis(api);
        return new CacheKeyBuilder($"ogc:{sanitizedApi}");
    }

    /// <summary>
    /// Appends a simple string component to the cache key.
    /// </summary>
    /// <param name="component">Component to append.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithComponent(string component)
    {
        EnsureNotBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        var sanitized = CacheKeyNormalizer.SanitizeForRedis(component);
        _keyBuilder.Append(':').Append(sanitized);
        return this;
    }

    /// <summary>
    /// Appends a suffix to the cache key (same as WithComponent but more semantic).
    /// </summary>
    /// <param name="suffix">Suffix to append.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithSuffix(string suffix)
    {
        return WithComponent(suffix);
    }

    /// <summary>
    /// Appends a bounding box hash to the cache key.
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithBoundingBox(double minX, double minY, double maxX, double maxY)
    {
        EnsureNotBuilt();

        // Create a deterministic hash of the bbox
        var bboxString = FormattableString.Invariant(
            $"{minX:F6},{minY:F6},{maxX:F6},{maxY:F6}");
        var hash = ComputeShortHash(bboxString);

        _keyBuilder.Append(':').Append(hash);
        return this;
    }

    /// <summary>
    /// Appends a filter hash to the cache key.
    /// </summary>
    /// <param name="filter">Filter expression or JSON.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithFilter(string filter)
    {
        EnsureNotBuilt();

        if (string.IsNullOrWhiteSpace(filter))
        {
            return this;
        }

        // Hash the filter to keep key length manageable
        var hash = ComputeShortHash(filter);
        _keyBuilder.Append(':').Append(hash);
        return this;
    }

    /// <summary>
    /// Appends a hash of an object to the cache key.
    /// Useful for query parameters, filter objects, etc.
    /// </summary>
    /// <typeparam name="T">Type of object to hash.</typeparam>
    /// <param name="obj">Object to hash.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithObjectHash<T>(T obj)
    {
        EnsureNotBuilt();

        if (obj == null)
        {
            return this;
        }

        // Serialize to JSON and hash
        var json = JsonSerializer.Serialize(obj, JsonSerializerOptionsRegistry.Web);
        var hash = ComputeShortHash(json);

        _keyBuilder.Append(':').Append(hash);
        return this;
    }

    /// <summary>
    /// Appends a timestamp component to the cache key.
    /// </summary>
    /// <param name="timestamp">Timestamp to include.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithTimestamp(DateTimeOffset timestamp)
    {
        EnsureNotBuilt();

        var formatted = CacheKeyNormalizer.FormatTimestamp(timestamp);
        _keyBuilder.Append(':').Append(formatted);
        return this;
    }

    /// <summary>
    /// Appends a version component to the cache key.
    /// </summary>
    /// <param name="version">Version number.</param>
    /// <returns>This builder for chaining.</returns>
    public CacheKeyBuilder WithVersion(int version)
    {
        EnsureNotBuilt();

        _keyBuilder.Append(":v").Append(version.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    /// <summary>
    /// Builds the final cache key string.
    /// </summary>
    /// <returns>Normalized cache key.</returns>
    public string Build()
    {
        EnsureNotBuilt();
        _isBuilt = true;

        var key = _keyBuilder.ToString();
        return CacheKeyNormalizer.Normalize(key);
    }

    private void EnsureNotBuilt()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("Cache key has already been built. Create a new builder.");
        }
    }

    /// <summary>
    /// Computes a short (16-character) hash of the input string for cache keys.
    /// Uses SHA256 and takes the first 64 bits as hexadecimal.
    /// </summary>
    private static string ComputeShortHash(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "empty";
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);

        // Take first 8 bytes (64 bits) for a shorter hash
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }
}
