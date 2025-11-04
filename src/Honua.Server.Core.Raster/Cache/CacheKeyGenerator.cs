// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// Utility class for generating cache keys with collision-resistant path hashing.
/// Uses SHA256 to create filesystem-safe cache keys that include full path information.
/// </summary>
public static class CacheKeyGenerator
{
    private const int HashPrefixLength = 16; // Use first 16 characters of SHA256 hash (64 bits)

    /// <summary>
    /// Generates a cache key from a source URI and optional parameters.
    /// Uses SHA256 hash of the full path to prevent directory collisions.
    /// </summary>
    /// <param name="sourceUri">Full source URI/path</param>
    /// <param name="variableName">Optional variable name for multi-variable formats (NetCDF, HDF5)</param>
    /// <param name="timeIndex">Optional time index for time-series data</param>
    /// <returns>A filesystem-safe cache key that includes path hash</returns>
    public static string GenerateCacheKey(string sourceUri, string? variableName = null, int? timeIndex = null)
    {
        if (sourceUri.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Source URI cannot be null or empty", nameof(sourceUri));
        }

        // Generate hash of the full path to prevent collisions
        var pathHash = GeneratePathHash(sourceUri);

        // Extract filename without extension for readability
        var fileName = System.IO.Path.GetFileNameWithoutExtension(sourceUri);
        fileName = SanitizeForFilename(fileName);

        // Build cache key components
        var variable = variableName.IsNullOrWhiteSpace() ? "default" : SanitizeForFilename(variableName);
        var timeIdx = timeIndex?.ToString(CultureInfo.InvariantCulture) ?? "0";

        // Format: {pathHash}_{filename}_{variable}_{timeIndex}
        // Example: a1b2c3d4e5f6g7h8_temperature_air_temp_0
        return $"{pathHash}_{fileName}_{variable}_{timeIdx}";
    }

    /// <summary>
    /// Generates a cache key from a dataset ID and optional parameters.
    /// </summary>
    /// <param name="datasetId">Dataset identifier</param>
    /// <param name="variableName">Optional variable name</param>
    /// <param name="timeIndex">Optional time index</param>
    /// <returns>A filesystem-safe cache key</returns>
    public static string GenerateCacheKeyFromDatasetId(string datasetId, string? variableName = null, int? timeIndex = null)
    {
        if (datasetId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Dataset ID cannot be null or empty", nameof(datasetId));
        }

        var sanitizedId = SanitizeForFilename(datasetId);
        var variable = variableName.IsNullOrWhiteSpace() ? "default" : SanitizeForFilename(variableName);
        var timeIdx = timeIndex?.ToString(CultureInfo.InvariantCulture) ?? "0";

        return $"{sanitizedId}_{variable}_{timeIdx}";
    }

    /// <summary>
    /// Generates a SHA256 hash of the path and returns the first 16 hex characters.
    /// This provides 64 bits of collision resistance while keeping cache keys readable.
    /// </summary>
    /// <param name="path">Full path to hash</param>
    /// <returns>First 16 characters of SHA256 hash (hex encoded)</returns>
    public static string GeneratePathHash(string path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        // Normalize path separators for consistent hashing across platforms
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();

        var bytes = Encoding.UTF8.GetBytes(normalizedPath);
        var hashBytes = SHA256.HashData(bytes);

        // Convert first 8 bytes (64 bits) to hex string
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Validates that a cache key is properly formatted and collision-resistant.
    /// </summary>
    /// <param name="cacheKey">Cache key to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateCacheKey(string cacheKey)
    {
        if (cacheKey.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check for path hash prefix (16 hex chars followed by underscore)
        if (cacheKey.Length < 17 || cacheKey[16] != '_')
        {
            return false;
        }

        var hashPrefix = cacheKey.Substring(0, 16);
        return IsValidHexString(hashPrefix);
    }

    /// <summary>
    /// Checks if two cache keys could potentially collide.
    /// </summary>
    /// <param name="key1">First cache key</param>
    /// <param name="key2">Second cache key</param>
    /// <returns>True if keys are identical (collision), false otherwise</returns>
    public static bool DetectCollision(string key1, string key2)
    {
        return string.Equals(key1, key2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sanitizes a string for use in a filename by replacing invalid characters.
    /// </summary>
    private static string SanitizeForFilename(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "default";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == '/' || ch == '\\' || ch == ':' || ch == '*' || ch == '?' ||
                ch == '"' || ch == '<' || ch == '>' || ch == '|' || char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString();

        // Trim leading/trailing dashes and limit length
        result = result.Trim('-');
        if (result.Length > 64)
        {
            result = result.Substring(0, 64).TrimEnd('-');
        }

        return result.IsNullOrEmpty() ? "default" : result;
    }

    /// <summary>
    /// Checks if a string contains only valid hexadecimal characters.
    /// </summary>
    private static bool IsValidHexString(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Generates a cache key for metadata caching with optional prefix.
    /// Format: {prefix}snapshot:v{version} or metadata:snapshot:v{version}
    /// </summary>
    /// <param name="provider">Metadata provider identifier (e.g., "json", "yaml", "postgres")</param>
    /// <param name="version">Schema version for backward compatibility (default: 1)</param>
    /// <param name="cacheKeyPrefix">Optional cache key prefix for namespacing (default: "honua:metadata:")</param>
    /// <returns>A cache key for metadata snapshots</returns>
    public static string GenerateMetadataKey(string provider, int version = 1, string? cacheKeyPrefix = null)
    {
        if (provider.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Provider cannot be null or empty", nameof(provider));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1");
        }

        var prefix = cacheKeyPrefix.IsNullOrWhiteSpace() ? "honua:metadata:" : cacheKeyPrefix;
        var sanitizedProvider = SanitizeForFilename(provider);

        return $"{prefix}snapshot:v{version}";
    }

    /// <summary>
    /// Generates a cache key for authorization caching.
    /// Format: authz:{resourceType}:{resourceId}:{operation}:{userId}
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="resourceType">Resource type (e.g., "layer", "collection")</param>
    /// <param name="resourceId">Resource identifier</param>
    /// <param name="operation">Operation being performed (e.g., "read", "write")</param>
    /// <param name="cacheKeyPrefix">Optional cache key prefix (default: "authz:")</param>
    /// <returns>A cache key for authorization results</returns>
    public static string GenerateAuthorizationKey(
        string userId,
        string resourceType,
        string resourceId,
        string operation,
        string? cacheKeyPrefix = null)
    {
        if (userId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        if (resourceType.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));
        }

        if (resourceId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
        }

        if (operation.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Operation cannot be null or empty", nameof(operation));
        }

        var prefix = cacheKeyPrefix.IsNullOrWhiteSpace() ? "authz:" : cacheKeyPrefix;

        return $"{prefix}{resourceType}:{resourceId}:{operation}:{userId}";
    }

    /// <summary>
    /// Generates a cache key prefix for authorization resource invalidation.
    /// Format: authz:{resourceType}:{resourceId}:
    /// </summary>
    /// <param name="resourceType">Resource type (e.g., "layer", "collection")</param>
    /// <param name="resourceId">Resource identifier</param>
    /// <param name="cacheKeyPrefix">Optional cache key prefix (default: "authz:")</param>
    /// <returns>A cache key prefix for authorization invalidation</returns>
    public static string GenerateAuthorizationKeyPrefix(
        string resourceType,
        string resourceId,
        string? cacheKeyPrefix = null)
    {
        if (resourceType.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));
        }

        if (resourceId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
        }

        var prefix = cacheKeyPrefix.IsNullOrWhiteSpace() ? "authz:" : cacheKeyPrefix;

        return $"{prefix}{resourceType}:{resourceId}:";
    }

    /// <summary>
    /// Generates a cache key for vector tile caching.
    /// Format: {datasetId}:{tileMatrixSetId}:{z}/{x}/{y}:{styleId}:{format}:{transparency}:{tileSize}[:time={time}]
    /// </summary>
    /// <param name="datasetId">Dataset/layer identifier</param>
    /// <param name="tileMatrixSetId">Tile matrix set ID (e.g., "WebMercatorQuad")</param>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Column (X coordinate)</param>
    /// <param name="y">Row (Y coordinate)</param>
    /// <param name="styleId">Style identifier (default: "default")</param>
    /// <param name="format">Output format (default: "image/png")</param>
    /// <param name="transparent">Whether the tile is transparent</param>
    /// <param name="tileSize">Tile size in pixels (default: 256)</param>
    /// <param name="time">Optional temporal filter</param>
    /// <returns>A cache key for vector tiles</returns>
    public static string GenerateVectorTileKey(
        string datasetId,
        string tileMatrixSetId,
        int z,
        int x,
        int y,
        string? styleId = null,
        string? format = null,
        bool transparent = true,
        int tileSize = 256,
        string? time = null)
    {
        if (datasetId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Dataset ID cannot be null or empty", nameof(datasetId));
        }

        if (tileMatrixSetId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Tile matrix set ID cannot be null or empty", nameof(tileMatrixSetId));
        }

        if (z < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(z), "Zoom level must be >= 0");
        }

        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X coordinate must be >= 0");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y coordinate must be >= 0");
        }

        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be > 0");
        }

        var style = styleId.IsNullOrWhiteSpace() ? "default" : styleId;
        var fmt = format.IsNullOrWhiteSpace() ? "image/png" : format;
        var transparency = transparent ? "alpha" : "opaque";

        var baseKey = $"{datasetId}:{tileMatrixSetId}:{z}/{x}/{y}:{style}:{fmt}:{transparency}:{tileSize}";

        return time.IsNullOrWhiteSpace() ? baseKey : $"{baseKey}:time={time}";
    }

    /// <summary>
    /// Generates a cache key for kerchunk reference caching.
    /// Uses SHA256 hash to ensure consistent key length and avoid filesystem/S3 key issues.
    /// Format: {hashHex} (64-character SHA256 hash)
    /// </summary>
    /// <param name="sourceUri">Source URI for the dataset</param>
    /// <param name="variables">Optional array of variable names</param>
    /// <param name="includeCoordinates">Whether to include coordinates (default: true)</param>
    /// <param name="consolidateMetadata">Whether to consolidate metadata (default: true)</param>
    /// <returns>A cache key for kerchunk references</returns>
    public static string GenerateKerchunkKey(
        string sourceUri,
        string[]? variables = null,
        bool includeCoordinates = true,
        bool consolidateMetadata = true)
    {
        if (sourceUri.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Source URI cannot be null or empty", nameof(sourceUri));
        }

        var components = new StringBuilder();
        components.Append(sourceUri);

        if (variables != null && variables.Length > 0)
        {
            components.Append("|vars:");
            components.Append(string.Join(",", variables));
        }

        if (!includeCoordinates)
        {
            components.Append("|no-coords");
        }

        if (!consolidateMetadata)
        {
            components.Append("|no-consolidate");
        }

        var keyString = components.ToString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

        return hashHex;
    }

    /// <summary>
    /// Generates a cache key for Zarr chunk caching.
    /// Format: zarr:{zarrUri}:{variableName}:{chunkCoords}
    /// </summary>
    /// <param name="zarrUri">Zarr dataset URI</param>
    /// <param name="variableName">Variable/array name</param>
    /// <param name="chunkCoords">Chunk coordinates as an array</param>
    /// <param name="cacheKeyPrefix">Optional cache key prefix (default: "zarr:")</param>
    /// <returns>A cache key for Zarr chunks</returns>
    public static string GenerateZarrChunkKey(
        string zarrUri,
        string variableName,
        int[] chunkCoords,
        string? cacheKeyPrefix = null)
    {
        if (zarrUri.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Zarr URI cannot be null or empty", nameof(zarrUri));
        }

        if (variableName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Variable name cannot be null or empty", nameof(variableName));
        }

        if (chunkCoords == null || chunkCoords.Length == 0)
        {
            throw new ArgumentException("Chunk coordinates cannot be null or empty", nameof(chunkCoords));
        }

        var prefix = cacheKeyPrefix.IsNullOrWhiteSpace() ? "zarr:" : cacheKeyPrefix;
        var coordStr = string.Join(".", chunkCoords);

        return $"{prefix}{zarrUri}:{variableName}:{coordStr}";
    }
}
