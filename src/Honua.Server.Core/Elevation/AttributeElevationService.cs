// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Elevation service that retrieves elevation from feature attributes (database columns).
/// Supports both base elevation and building height for 3D extrusion.
/// </summary>
public sealed class AttributeElevationService : IElevationService
{
    /// <inheritdoc />
    public Task<double?> GetElevationAsync(
        double longitude,
        double latitude,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(context);

        var elevation = GetElevationFromAttributes(context);
        return Task.FromResult(elevation);
    }

    /// <inheritdoc />
    public Task<double?[]> GetElevationsAsync(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(coordinates);
        Guard.NotNull(context);

        // For attribute-based elevation, all points in the same feature have the same elevation
        var elevation = GetElevationFromAttributes(context);
        var results = new double?[coordinates.Count];
        Array.Fill(results, elevation);

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public bool CanProvideElevation(ElevationContext context)
    {
        Guard.NotNull(context);

        // Can provide if configuration specifies attribute source and attribute name
        return context.Configuration?.Source == "attribute" &&
               !string.IsNullOrWhiteSpace(context.Configuration.ElevationAttribute) &&
               context.FeatureAttributes is not null;
    }

    /// <summary>
    /// Extracts elevation from feature attributes based on configuration.
    /// </summary>
    private static double? GetElevationFromAttributes(ElevationContext context)
    {
        if (context.Configuration is null || context.FeatureAttributes is null)
        {
            return null;
        }

        var config = context.Configuration;
        double? baseElevation = null;

        // Try to get base elevation from configured attribute
        if (!string.IsNullOrWhiteSpace(config.ElevationAttribute) &&
            context.FeatureAttributes.TryGetValue(config.ElevationAttribute, out var elevationValue))
        {
            baseElevation = ConvertToDouble(elevationValue);
        }

        // Use default elevation if no attribute value found
        if (!baseElevation.HasValue && config.DefaultElevation.HasValue)
        {
            baseElevation = config.DefaultElevation.Value;
        }

        // Apply vertical offset if configured
        if (baseElevation.HasValue)
        {
            baseElevation = baseElevation.Value + config.VerticalOffset;
        }

        return baseElevation;
    }

    /// <summary>
    /// Attempts to get building height from feature attributes.
    /// This is used for 3D extrusion of buildings.
    /// </summary>
    /// <param name="context">Elevation context with feature attributes</param>
    /// <returns>Building height in meters, or null if not available</returns>
    public static double? GetBuildingHeight(ElevationContext context)
    {
        Guard.NotNull(context);

        if (context.Configuration is null ||
            context.FeatureAttributes is null ||
            !context.Configuration.IncludeHeight)
        {
            return null;
        }

        var config = context.Configuration;

        // Try to get height from configured attribute
        if (!string.IsNullOrWhiteSpace(config.HeightAttribute) &&
            context.FeatureAttributes.TryGetValue(config.HeightAttribute, out var heightValue))
        {
            return ConvertToDouble(heightValue);
        }

        return null;
    }

    /// <summary>
    /// Converts an object value to double, handling various numeric types.
    /// </summary>
    private static double? ConvertToDouble(object? value)
    {
        if (value is null)
        {
            return null;
        }

        // Direct numeric types
        if (value is double d)
        {
            return d;
        }
        if (value is float f)
        {
            return f;
        }
        if (value is int i)
        {
            return i;
        }
        if (value is long l)
        {
            return l;
        }
        if (value is decimal dec)
        {
            return (double)dec;
        }

        // Try string conversion
        if (value is string str && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
