// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.LocationServices;

/// <summary>
/// Provider interface for geocoding services (address ↔ coordinates).
/// Implementations can use Azure Maps, Google Maps, AWS Location, Nominatim, etc.
/// </summary>
public interface IGeocodingProvider
{
    /// <summary>
    /// Gets the provider identifier (e.g., "azure-maps", "nominatim", "aws-location").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Geocodes an address or query string to geographic coordinates.
    /// </summary>
    /// <param name="request">Geocoding request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Geocoding response with one or more results.</returns>
    Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse geocodes geographic coordinates to an address.
    /// </summary>
    /// <param name="request">Reverse geocoding request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Geocoding response with address results.</returns>
    Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the geocoding service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is reachable and operational.</returns>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);
}
