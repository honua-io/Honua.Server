// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Provider interface for routing services (directions, travel time, distance).
/// Implementations can use Azure Maps, Google Maps, AWS Location, OSRM, etc.
/// </summary>
public interface IRoutingProvider
{
    /// <summary>
    /// Gets the provider identifier (e.g., "azure-maps", "osrm", "aws-location").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Calculates a route between waypoints.
    /// </summary>
    /// <param name="request">Routing request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing response with one or more route options.</returns>
    Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the routing service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is reachable and operational.</returns>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);
}
