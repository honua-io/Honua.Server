// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Export;
using Honua.Server.Core.Raster;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Services for rendering raster tiles.
/// </summary>
public sealed record TileRenderingServices
{
    /// <summary>
    /// Renders raster data into tile image format.
    /// </summary>
    public required IRasterRenderer Renderer { get; init; }

    /// <summary>
    /// Exports tiles in PMTiles format for efficiency.
    /// </summary>
    public required IPmTilesExporter PMTilesExporter { get; init; }
}
