// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Validation;

/// <summary>
/// Validates 3D geometry configurations and metadata for layers with Z and M coordinates.
/// </summary>
public static class ThreeDimensionalValidator
{
    /// <summary>
    /// Validation result for 3D configuration.
    /// </summary>
    public sealed record ThreeDValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Suggestions)
    {
        public static ThreeDValidationResult Valid() => new(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        public static ThreeDValidationResult WithErrors(params string[] errors) => new(false, errors, Array.Empty<string>(), Array.Empty<string>());
        public static ThreeDValidationResult WithWarnings(params string[] warnings) => new(true, Array.Empty<string>(), warnings, Array.Empty<string>());
    }

    /// <summary>
    /// Validates a layer's 3D configuration for consistency and best practices.
    /// </summary>
    public static ThreeDValidationResult ValidateLayerConfiguration(LayerDefinition layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var errors = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();

        // Check consistency between layer hasZ and storage hasZ
        if (layer.HasZ && layer.Storage is not null && !layer.Storage.HasZ)
        {
            warnings.Add($"Layer '{layer.Id}' has hasZ=true but storage hasZ is false. Ensure database geometry type includes Z dimension.");
        }

        if (!layer.HasZ && layer.Storage is { HasZ: true })
        {
            warnings.Add($"Layer '{layer.Id}' storage has hasZ=true but layer hasZ is false. Consider setting layer hasZ=true to advertise 3D support.");
        }

        // Check if CRS list includes CRS84H for 3D layers
        if (layer.HasZ || (layer.Storage?.HasZ ?? false))
        {
            var hasCrs84H = layer.Crs.Any(crs =>
                crs.Equals("CRS84H", StringComparison.OrdinalIgnoreCase) ||
                crs.Contains("CRS84h", StringComparison.OrdinalIgnoreCase));

            if (!hasCrs84H)
            {
                suggestions.Add($"Layer '{layer.Id}' has 3D support but doesn't include CRS84H in the CRS list. Add 'CRS84H' to enable 3D coordinate requests.");
            }
        }

        // Validate extent bbox for 3D layers
        if ((layer.HasZ || (layer.Storage?.HasZ ?? false)) && layer.Extent?.Bbox is { Count: > 0 })
        {
            var bbox = layer.Extent.Bbox[0];
            if (bbox.Length == 4)
            {
                suggestions.Add($"Layer '{layer.Id}' is 3D but extent bbox has only 4 values (2D). Consider using 6-value bbox: [minX, minY, minZ, maxX, maxY, maxZ]");
            }
            else if (bbox.Length == 6)
            {
                // Validate Z range
                var minZ = bbox[2];
                var maxZ = bbox[5];
                if (minZ > maxZ)
                {
                    errors.Add($"Layer '{layer.Id}' extent bbox has minZ ({minZ}) greater than maxZ ({maxZ})");
                }
            }
            else if (bbox.Length != 4 && bbox.Length != 6)
            {
                errors.Add($"Layer '{layer.Id}' extent bbox must have 4 values (2D) or 6 values (3D), found {bbox.Length}");
            }
        }

        // Validate zField if specified
        if (!string.IsNullOrWhiteSpace(layer.ZField))
        {
            if (!layer.HasZ)
            {
                warnings.Add($"Layer '{layer.Id}' specifies zField but hasZ=false. Set hasZ=true to enable Z coordinate support.");
            }

            var zFieldExists = layer.Fields.Any(f =>
                f.Name.Equals(layer.ZField, StringComparison.OrdinalIgnoreCase));

            if (!zFieldExists)
            {
                errors.Add($"Layer '{layer.Id}' specifies zField='{layer.ZField}' but no matching field definition found");
            }
        }

        // Check geometry type compatibility with 3D
        if (layer.HasZ && !string.IsNullOrWhiteSpace(layer.GeometryType))
        {
            var is3DType = GeometryTypeHelper.IsGeometryType3D(layer.GeometryType);
            if (!is3DType)
            {
                suggestions.Add($"Layer '{layer.Id}' has hasZ=true. Consider using 3D geometry type names: '{layer.GeometryType}Z' for clarity in documentation.");
            }
        }

        return new ThreeDValidationResult(
            errors.Count == 0,
            errors,
            warnings,
            suggestions);
    }

    /// <summary>
    /// Validates that a bounding box array is properly formatted for 2D or 3D.
    /// </summary>
    public static ThreeDValidationResult ValidateBoundingBox(double[] bbox)
    {
        ArgumentNullException.ThrowIfNull(bbox);

        var errors = new List<string>();

        if (bbox.Length != 4 && bbox.Length != 6)
        {
            errors.Add($"Bounding box must have 4 values (2D) or 6 values (3D), found {bbox.Length}");
            return ThreeDValidationResult.WithErrors(errors.ToArray());
        }

        if (bbox.Length == 4)
        {
            // 2D bbox: [minX, minY, maxX, maxY]
            if (bbox[0] > bbox[2])
            {
                errors.Add($"minX ({bbox[0]}) must be <= maxX ({bbox[2]})");
            }
            if (bbox[1] > bbox[3])
            {
                errors.Add($"minY ({bbox[1]}) must be <= maxY ({bbox[3]})");
            }
        }
        else if (bbox.Length == 6)
        {
            // 3D bbox: [minX, minY, minZ, maxX, maxY, maxZ]
            if (bbox[0] > bbox[3])
            {
                errors.Add($"minX ({bbox[0]}) must be <= maxX ({bbox[3]})");
            }
            if (bbox[1] > bbox[4])
            {
                errors.Add($"minY ({bbox[1]}) must be <= maxY ({bbox[4]})");
            }
            if (bbox[2] > bbox[5])
            {
                errors.Add($"minZ ({bbox[2]}) must be <= maxZ ({bbox[5]})");
            }
        }

        return errors.Count == 0
            ? ThreeDValidationResult.Valid()
            : ThreeDValidationResult.WithErrors(errors.ToArray());
    }

    /// <summary>
    /// Validates Z coordinate values are within acceptable ranges.
    /// </summary>
    public static ThreeDValidationResult ValidateZCoordinate(double z, string context = "coordinate")
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for NaN or Infinity
        if (double.IsNaN(z))
        {
            warnings.Add($"Z {context} is NaN (not a number). This may indicate missing elevation data.");
            return ThreeDValidationResult.WithWarnings(warnings.ToArray());
        }

        if (double.IsInfinity(z))
        {
            errors.Add($"Z {context} is infinite, which is invalid for coordinates");
            return ThreeDValidationResult.WithErrors(errors.ToArray());
        }

        // Reasonable elevation range checks (Earth-centric)
        // Dead Sea: -430m, Mt. Everest: 8,849m, aircraft: ~40,000ft (12,000m), satellites: much higher
        const double marianaTrench = -11000; // meters below sea level
        const double exosphere = 100000; // meters above sea level (edge of space)

        if (z < marianaTrench)
        {
            warnings.Add($"Z {context} ({z:N2}m) is below the Mariana Trench depth. Verify this extreme depth is correct.");
        }
        else if (z > exosphere)
        {
            warnings.Add($"Z {context} ({z:N2}m) is above the exosphere. Verify this extreme altitude is correct.");
        }

        return warnings.Count > 0
            ? ThreeDValidationResult.WithWarnings(warnings.ToArray())
            : ThreeDValidationResult.Valid();
    }

    /// <summary>
    /// Checks if a geometry has consistent Z values across all coordinates.
    /// </summary>
    public static bool HasConsistentZCoordinates(Geometry geometry)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return true;
        }

        var coordinates = geometry.Coordinates;
        if (coordinates.Length == 0)
        {
            return true;
        }

        // Check if first coordinate has Z
        var firstHasZ = !double.IsNaN(coordinates[0].Z);

        // All coordinates should match
        return coordinates.All(c => !double.IsNaN(c.Z) == firstHasZ);
    }

    /// <summary>
    /// Gets statistics about Z coordinates in a geometry.
    /// </summary>
    public static ZCoordinateStatistics GetZStatistics(Geometry geometry)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return new ZCoordinateStatistics(false, 0, null, null, null, null);
        }

        var coordinates = geometry.Coordinates;
        var zValues = coordinates
            .Where(c => !double.IsNaN(c.Z))
            .Select(c => c.Z)
            .ToList();

        if (zValues.Count == 0)
        {
            return new ZCoordinateStatistics(false, 0, null, null, null, null);
        }

        return new ZCoordinateStatistics(
            HasZ: true,
            Count: zValues.Count,
            Min: zValues.Min(),
            Max: zValues.Max(),
            Mean: zValues.Average(),
            Range: zValues.Max() - zValues.Min());
    }

    /// <summary>
    /// Statistics about Z coordinates in a geometry.
    /// </summary>
    public sealed record ZCoordinateStatistics(
        bool HasZ,
        int Count,
        double? Min,
        double? Max,
        double? Mean,
        double? Range);
}
