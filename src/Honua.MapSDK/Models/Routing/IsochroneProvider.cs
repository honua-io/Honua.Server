// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Enumeration of supported isochrone providers
/// </summary>
public enum IsochroneProvider
{
    /// <summary>
    /// Mapbox Isochrone API
    /// </summary>
    Mapbox,

    /// <summary>
    /// OpenRouteService Isochrone API
    /// </summary>
    OpenRouteService,

    /// <summary>
    /// GraphHopper Isochrone API
    /// </summary>
    GraphHopper,

    /// <summary>
    /// Custom provider endpoint
    /// </summary>
    Custom
}

/// <summary>
/// Configuration for an isochrone provider
/// </summary>
public class IsochroneProviderConfig
{
    /// <summary>
    /// Provider identifier
    /// </summary>
    public required IsochroneProvider Provider { get; init; }

    /// <summary>
    /// Display name for the provider
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// API key for the provider (if required)
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Custom endpoint URL (for Custom provider)
    /// </summary>
    public string? EndpointUrl { get; init; }

    /// <summary>
    /// Whether this provider is enabled
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Maximum number of intervals supported
    /// </summary>
    public int MaxIntervals { get; init; } = 4;

    /// <summary>
    /// Maximum interval value in minutes
    /// </summary>
    public int MaxIntervalMinutes { get; init; } = 60;

    /// <summary>
    /// Supported travel modes for this provider
    /// </summary>
    public List<TravelMode> SupportedTravelModes { get; init; } = new();
}
