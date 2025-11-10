// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Interface for routing services
/// Supports multiple routing engines (OSRM, Mapbox, GraphHopper, etc.)
/// </summary>
public interface IRoutingService
{
    /// <summary>
    /// Calculate a route between waypoints
    /// </summary>
    /// <param name="waypoints">List of waypoints to route through</param>
    /// <param name="options">Routing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Calculated route</returns>
    Task<Route> CalculateRouteAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate alternative routes
    /// </summary>
    /// <param name="waypoints">List of waypoints to route through</param>
    /// <param name="options">Routing options (MaxAlternatives determines how many)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of alternative routes</returns>
    Task<List<Route>> GetAlternativesAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate isochrone/service area
    /// </summary>
    /// <param name="options">Isochrone options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Isochrone result with polygons</returns>
    Task<IsochroneResult> CalculateIsochroneAsync(
        IsochroneOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Name of the routing provider
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider requires an API key
    /// </summary>
    bool RequiresApiKey { get; }

    /// <summary>
    /// Supported travel modes for this provider
    /// </summary>
    List<TravelMode> SupportedTravelModes { get; }
}
