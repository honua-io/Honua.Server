// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Service for retrieving elevation data for geographic coordinates.
/// Supports multiple elevation sources including database columns, external APIs, and DEMs.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// Gets elevation for a single point.
    /// </summary>
    /// <param name="longitude">Longitude in WGS84 (EPSG:4326)</param>
    /// <param name="latitude">Latitude in WGS84 (EPSG:4326)</param>
    /// <param name="context">Context for elevation lookup (layer, feature attributes, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Elevation in meters above mean sea level, or null if not available</returns>
    Task<double?> GetElevationAsync(
        double longitude,
        double latitude,
        ElevationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets elevations for multiple points in a batch operation.
    /// More efficient than calling GetElevationAsync multiple times.
    /// </summary>
    /// <param name="coordinates">Array of (longitude, latitude) pairs in WGS84</param>
    /// <param name="context">Context for elevation lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of elevations (same length as input), with null values where elevation is not available</returns>
    Task<double?[]> GetElevationsAsync(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates,
        ElevationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this elevation service can provide elevation data for the given context.
    /// </summary>
    /// <param name="context">Context to check</param>
    /// <returns>True if this service can provide elevation data for this context</returns>
    bool CanProvideElevation(ElevationContext context);
}

/// <summary>
/// Context information for elevation lookups.
/// Provides layer metadata and feature attributes to help determine elevation source.
/// </summary>
public sealed record ElevationContext
{
    /// <summary>
    /// Service ID this feature belongs to
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Layer ID this feature belongs to
    /// </summary>
    public required string LayerId { get; init; }

    /// <summary>
    /// Feature attributes (for database column-based elevation)
    /// </summary>
    public IReadOnlyDictionary<string, object?>? FeatureAttributes { get; init; }

    /// <summary>
    /// Elevation configuration from layer metadata
    /// </summary>
    public ElevationConfiguration? Configuration { get; init; }
}

/// <summary>
/// Configuration for how to retrieve elevation data for a layer.
/// Can be specified in layer metadata.
/// </summary>
public sealed record ElevationConfiguration
{
    /// <summary>
    /// Elevation source type (e.g., "attribute", "external", "dem")
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// For attribute source: name of the attribute/column containing elevation
    /// </summary>
    public string? ElevationAttribute { get; init; }

    /// <summary>
    /// For attribute source: name of the attribute/column containing building height (for extrusion)
    /// </summary>
    public string? HeightAttribute { get; init; }

    /// <summary>
    /// Default elevation to use if no elevation data is available (meters)
    /// </summary>
    public double? DefaultElevation { get; init; }

    /// <summary>
    /// Vertical offset to add to all elevations (meters)
    /// Useful for adjusting between different vertical datums or adding clearance
    /// </summary>
    public double VerticalOffset { get; init; } = 0;

    /// <summary>
    /// External elevation service URL (for future external API support)
    /// </summary>
    public string? ExternalServiceUrl { get; init; }

    /// <summary>
    /// Whether to include building height for 3D extrusion
    /// </summary>
    public bool IncludeHeight { get; init; } = false;
}
