// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Interface for isochrone service providers
/// </summary>
public interface IIsochroneProvider
{
    /// <summary>
    /// Provider identifier
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Display name for the provider
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider requires an API key
    /// </summary>
    bool RequiresApiKey { get; }

    /// <summary>
    /// Supported travel modes
    /// </summary>
    List<TravelMode> SupportedTravelModes { get; }

    /// <summary>
    /// Maximum number of intervals supported
    /// </summary>
    int MaxIntervals { get; }

    /// <summary>
    /// Calculate isochrone polygons
    /// </summary>
    /// <param name="options">Isochrone calculation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Isochrone result with polygons</returns>
    Task<IsochroneResult> CalculateAsync(
        IsochroneOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate if the provider is properly configured
    /// </summary>
    /// <returns>True if configured correctly</returns>
    bool IsConfigured();
}
