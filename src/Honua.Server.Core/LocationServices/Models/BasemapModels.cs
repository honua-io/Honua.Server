// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.LocationServices.Models;

/// <summary>
/// Represents a basemap tileset configuration.
/// </summary>
public sealed record BasemapTileset
{
    /// <summary>
    /// Unique identifier for this tileset.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for this tileset.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of the tileset.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Tile format ("raster" or "vector").
    /// </summary>
    public required TileFormat Format { get; init; }

    /// <summary>
    /// Tile size in pixels (typically 256 or 512).
    /// </summary>
    public int TileSize { get; init; } = 256;

    /// <summary>
    /// Minimum zoom level.
    /// </summary>
    public int MinZoom { get; init; } = 0;

    /// <summary>
    /// Maximum zoom level.
    /// </summary>
    public int MaxZoom { get; init; } = 22;

    /// <summary>
    /// Bounding box coverage [west, south, east, north].
    /// </summary>
    public double[]? Bounds { get; init; }

    /// <summary>
    /// Center point [longitude, latitude, zoom].
    /// </summary>
    public double[]? Center { get; init; }

    /// <summary>
    /// Attribution text required by the provider.
    /// </summary>
    public string? Attribution { get; init; }

    /// <summary>
    /// URL template for tiles with {z}, {x}, {y} placeholders.
    /// </summary>
    public required string TileUrlTemplate { get; init; }

    /// <summary>
    /// Additional metadata about the tileset.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Tile format enumeration.
/// </summary>
public enum TileFormat
{
    /// <summary>
    /// Raster tiles (PNG, JPEG, WebP).
    /// </summary>
    Raster,

    /// <summary>
    /// Vector tiles (MVT/PBF).
    /// </summary>
    Vector
}

/// <summary>
/// Request for retrieving a map tile.
/// </summary>
public sealed record TileRequest
{
    /// <summary>
    /// Tileset identifier.
    /// </summary>
    public required string TilesetId { get; init; }

    /// <summary>
    /// Zoom level.
    /// </summary>
    public required int Z { get; init; }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public required int X { get; init; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public required int Y { get; init; }

    /// <summary>
    /// Optional scale factor for high-DPI displays (1 = standard, 2 = @2x).
    /// </summary>
    public int Scale { get; init; } = 1;

    /// <summary>
    /// Optional image format for raster tiles ("png", "jpg", "webp").
    /// </summary>
    public string? ImageFormat { get; init; }

    /// <summary>
    /// Optional language code for labels (ISO 639-1).
    /// </summary>
    public string? Language { get; init; }
}

/// <summary>
/// Response containing map tile data.
/// </summary>
public sealed record TileResponse
{
    /// <summary>
    /// Tile content bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Content type (e.g., "image/png", "application/vnd.mapbox-vector-tile").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Optional cache control header value.
    /// </summary>
    public string? CacheControl { get; init; }

    /// <summary>
    /// Optional ETag for caching.
    /// </summary>
    public string? ETag { get; init; }
}
