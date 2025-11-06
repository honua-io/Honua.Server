// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Provider interface for basemap tile services.
/// Implementations can use Azure Maps, OpenStreetMap, Mapbox, AWS Location, etc.
/// </summary>
public interface IBasemapTileProvider
{
    /// <summary>
    /// Gets the provider identifier (e.g., "azure-maps", "openstreetmap", "mapbox").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the list of available tilesets from this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available tilesets.</returns>
    Task<IReadOnlyList<BasemapTileset>> GetAvailableTilesetsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific map tile.
    /// </summary>
    /// <param name="request">Tile request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tile response with image or vector data.</returns>
    Task<TileResponse> GetTileAsync(
        TileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tile URL template for client-side rendering.
    /// </summary>
    /// <param name="tilesetId">Tileset identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>URL template with {z}, {x}, {y} placeholders.</returns>
    Task<string> GetTileUrlTemplateAsync(
        string tilesetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the tile service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is reachable and operational.</returns>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);
}
