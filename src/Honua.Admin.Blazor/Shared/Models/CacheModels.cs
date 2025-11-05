// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Overall cache statistics.
/// </summary>
public sealed class CacheStatistics
{
    [JsonPropertyName("totalHits")]
    public long TotalHits { get; set; }

    [JsonPropertyName("totalMisses")]
    public long TotalMisses { get; set; }

    [JsonPropertyName("totalEvictions")]
    public long TotalEvictions { get; set; }

    [JsonPropertyName("totalEntries")]
    public int TotalEntries { get; set; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("hitRate")]
    public double HitRate { get; set; }

    [JsonPropertyName("missRate")]
    public double MissRate { get; set; }
}

/// <summary>
/// Cache statistics for a specific dataset.
/// </summary>
public sealed class DatasetCacheStatistics
{
    [JsonPropertyName("datasetId")]
    public required string DatasetId { get; set; }

    [JsonPropertyName("hits")]
    public long Hits { get; set; }

    [JsonPropertyName("misses")]
    public long Misses { get; set; }

    [JsonPropertyName("evictions")]
    public long Evictions { get; set; }

    [JsonPropertyName("entries")]
    public int Entries { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("hitRate")]
    public double HitRate { get; set; }

    [JsonPropertyName("lastAccessed")]
    public DateTimeOffset? LastAccessed { get; set; }
}

/// <summary>
/// Request to create a raster tile preseed job.
/// </summary>
public sealed class CreatePreseedJobRequest
{
    [JsonPropertyName("datasetIds")]
    public List<string> DatasetIds { get; set; } = new();

    [JsonPropertyName("tileMatrixSetId")]
    public string? TileMatrixSetId { get; set; }

    [JsonPropertyName("minZoom")]
    public int? MinZoom { get; set; }

    [JsonPropertyName("maxZoom")]
    public int? MaxZoom { get; set; }

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("transparent")]
    public bool? Transparent { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("overwrite")]
    public bool? Overwrite { get; set; }

    [JsonPropertyName("tileSize")]
    public int? TileSize { get; set; }
}

/// <summary>
/// Preseed job snapshot.
/// </summary>
public sealed class PreseedJobSnapshot
{
    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("datasetIds")]
    public List<string> DatasetIds { get; set; } = new();

    [JsonPropertyName("tileMatrixSetId")]
    public string? TileMatrixSetId { get; set; }

    [JsonPropertyName("minZoom")]
    public int MinZoom { get; set; }

    [JsonPropertyName("maxZoom")]
    public int MaxZoom { get; set; }

    [JsonPropertyName("tilesGenerated")]
    public long TilesGenerated { get; set; }

    [JsonPropertyName("totalTiles")]
    public long? TotalTiles { get; set; }

    [JsonPropertyName("percentComplete")]
    public double PercentComplete { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to purge cache entries.
/// </summary>
public sealed class PurgeCacheRequest
{
    [JsonPropertyName("datasetIds")]
    public List<string> DatasetIds { get; set; } = new();
}

/// <summary>
/// Result of cache purge operation.
/// </summary>
public sealed class PurgeCacheResult
{
    [JsonPropertyName("purged")]
    public List<string> Purged { get; set; } = new();

    [JsonPropertyName("failed")]
    public List<string> Failed { get; set; } = new();
}

/// <summary>
/// Cache TTL policy options.
/// </summary>
public static class CacheTtlPolicyOptions
{
    public static readonly Dictionary<string, string> Policies = new()
    {
        { "VeryShort", "1 minute (real-time data)" },
        { "Short", "5 minutes (dynamic content)" },
        { "Medium", "1 hour (semi-static content)" },
        { "Long", "24 hours (static content)" },
        { "VeryLong", "7 days (immutable content)" },
        { "Permanent", "30 days (permanent content)" }
    };

    public static readonly Dictionary<string, TimeSpan> PolicyDurations = new()
    {
        { "VeryShort", TimeSpan.FromMinutes(1) },
        { "Short", TimeSpan.FromMinutes(5) },
        { "Medium", TimeSpan.FromHours(1) },
        { "Long", TimeSpan.FromHours(24) },
        { "VeryLong", TimeSpan.FromDays(7) },
        { "Permanent", TimeSpan.FromDays(30) }
    };
}

/// <summary>
/// Tile format options for preseed jobs.
/// </summary>
public static class TileFormatOptions
{
    public static readonly List<string> Formats = new()
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };
}

/// <summary>
/// Tile matrix set options.
/// </summary>
public static class TileMatrixSetOptions
{
    public static readonly List<string> Sets = new()
    {
        "WorldWebMercatorQuad",
        "WorldCRS84Quad"
    };
}
