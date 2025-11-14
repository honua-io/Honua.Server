// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Represents a unique tile location in a tile matrix pyramid.
/// Follows OGC API - Tiles specification.
/// </summary>
public sealed record TileCoordinates
{
    /// <summary>
    /// Collection identifier (format: "serviceId::layerId").
    /// </summary>
    public required string CollectionId { get; init; }

    /// <summary>
    /// Tileset identifier (identifies the raster dataset).
    /// </summary>
    public required string TilesetId { get; init; }

    /// <summary>
    /// Tile matrix set identifier (defines the coordinate system).
    /// </summary>
    public required string TileMatrixSetId { get; init; }

    /// <summary>
    /// Tile matrix level (zoom level).
    /// </summary>
    public required string TileMatrix { get; init; }

    /// <summary>
    /// Tile row in the matrix at this zoom level (Y coordinate).
    /// </summary>
    public required int TileRow { get; init; }

    /// <summary>
    /// Tile column in the matrix at this zoom level (X coordinate).
    /// </summary>
    public required int TileCol { get; init; }
}
