// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Services.Drawing;

/// <summary>
/// Service for measuring distances, areas, bearings, and elevation profiles
/// </summary>
public class MeasurementManager
{
    private const double EarthRadiusMeters = 6371000;

    public MeasurementUnit DefaultDistanceUnit { get; set; } = MeasurementUnit.Meters;
    public MeasurementUnit DefaultAreaUnit { get; set; } = MeasurementUnit.SquareMeters;

    /// <summary>
    /// Calculate distance between two coordinates
    /// </summary>
    public MeasurementResult MeasureDistance(double[] coord1, double[] coord2, MeasurementUnit? unit = null)
    {
        var meters = CalculateHaversineDistance(coord1, coord2);
        var targetUnit = unit ?? DefaultDistanceUnit;
        var converted = ConvertDistance(meters, MeasurementUnit.Meters, targetUnit);

        return new MeasurementResult
        {
            Type = MeasurementType.Distance,
            Value = converted,
            Unit = targetUnit,
            FormattedValue = FormatDistance(converted, targetUnit),
            RawValueInMeters = meters
        };
    }

    /// <summary>
    /// Calculate total distance along a line (polyline)
    /// </summary>
    public MeasurementResult MeasureLineDistance(List<double[]> coordinates, MeasurementUnit? unit = null)
    {
        if (coordinates.Count < 2)
            throw new ArgumentException("At least 2 coordinates required", nameof(coordinates));

        var totalMeters = 0.0;
        for (int i = 0; i < coordinates.Count - 1; i++)
        {
            totalMeters += CalculateHaversineDistance(coordinates[i], coordinates[i + 1]);
        }

        var targetUnit = unit ?? DefaultDistanceUnit;
        var converted = ConvertDistance(totalMeters, MeasurementUnit.Meters, targetUnit);

        return new MeasurementResult
        {
            Type = MeasurementType.Distance,
            Value = converted,
            Unit = targetUnit,
            FormattedValue = FormatDistance(converted, targetUnit),
            RawValueInMeters = totalMeters,
            SegmentCount = coordinates.Count - 1
        };
    }

    /// <summary>
    /// Calculate area of a polygon
    /// </summary>
    public MeasurementResult MeasureArea(List<double[]> coordinates, MeasurementUnit? unit = null)
    {
        if (coordinates.Count < 3)
            throw new ArgumentException("At least 3 coordinates required for area", nameof(coordinates));

        var squareMeters = CalculateGeodesicArea(coordinates);
        var targetUnit = unit ?? DefaultAreaUnit;
        var converted = ConvertArea(squareMeters, MeasurementUnit.SquareMeters, targetUnit);

        return new MeasurementResult
        {
            Type = MeasurementType.Area,
            Value = converted,
            Unit = targetUnit,
            FormattedValue = FormatArea(converted, targetUnit),
            RawValueInSquareMeters = squareMeters
        };
    }

    /// <summary>
    /// Calculate bearing/azimuth from point 1 to point 2
    /// </summary>
    public BearingResult CalculateBearing(double[] coord1, double[] coord2)
    {
        var lon1 = coord1[0] * Math.PI / 180;
        var lat1 = coord1[1] * Math.PI / 180;
        var lon2 = coord2[0] * Math.PI / 180;
        var lat2 = coord2[1] * Math.PI / 180;

        var dLon = lon2 - lon1;
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) -
                Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x) * 180 / Math.PI;
        bearing = (bearing + 360) % 360; // Normalize to 0-360

        return new BearingResult
        {
            Degrees = bearing,
            Radians = bearing * Math.PI / 180,
            CardinalDirection = GetCardinalDirection(bearing),
            FormattedValue = $"{bearing:F2}°"
        };
    }

    /// <summary>
    /// Generate elevation profile along a line
    /// This would integrate with a DEM (Digital Elevation Model) service
    /// </summary>
    public async Task<ElevationProfile> GenerateElevationProfileAsync(
        List<double[]> coordinates,
        Func<double, double, Task<double>> elevationProvider,
        int samplePoints = 100)
    {
        if (coordinates.Count < 2)
            throw new ArgumentException("At least 2 coordinates required", nameof(coordinates));

        var profile = new ElevationProfile
        {
            Coordinates = new List<double[]>(coordinates),
            SamplePoints = new List<ElevationPoint>()
        };

        // Sample points along the line
        var totalDistance = 0.0;
        var samples = new List<(double distance, double[] coord)>();

        // Add first point
        samples.Add((0, coordinates[0]));

        // Interpolate points along segments
        for (int i = 0; i < coordinates.Count - 1; i++)
        {
            var segmentDistance = CalculateHaversineDistance(coordinates[i], coordinates[i + 1]);
            var pointsInSegment = (int)(samplePoints * (segmentDistance / MeasureLineDistance(coordinates).RawValueInMeters));

            for (int j = 1; j <= pointsInSegment; j++)
            {
                var fraction = (double)j / pointsInSegment;
                var interpolated = InterpolateCoordinate(coordinates[i], coordinates[i + 1], fraction);
                var distanceFromStart = totalDistance + segmentDistance * fraction;
                samples.Add((distanceFromStart, interpolated));
            }

            totalDistance += segmentDistance;
        }

        // Query elevation for each sample point
        foreach (var (distance, coord) in samples)
        {
            var elevation = await elevationProvider(coord[0], coord[1]);
            profile.SamplePoints.Add(new ElevationPoint
            {
                Distance = distance,
                Elevation = elevation,
                Coordinate = coord
            });
        }

        // Calculate statistics
        if (profile.SamplePoints.Count > 0)
        {
            profile.MinElevation = profile.SamplePoints.Min(p => p.Elevation);
            profile.MaxElevation = profile.SamplePoints.Max(p => p.Elevation);
            profile.TotalDistance = totalDistance;
            profile.ElevationGain = CalculateElevationGain(profile.SamplePoints);
            profile.ElevationLoss = CalculateElevationLoss(profile.SamplePoints);
        }

        return profile;
    }

    /// <summary>
    /// Convert distance between units
    /// </summary>
    public double ConvertDistance(double value, MeasurementUnit from, MeasurementUnit to)
    {
        // Convert to meters first
        var meters = from switch
        {
            MeasurementUnit.Meters => value,
            MeasurementUnit.Kilometers => value * 1000,
            MeasurementUnit.Miles => value * 1609.34,
            MeasurementUnit.Feet => value * 0.3048,
            MeasurementUnit.NauticalMiles => value * 1852,
            _ => value
        };

        // Convert from meters to target unit
        return to switch
        {
            MeasurementUnit.Meters => meters,
            MeasurementUnit.Kilometers => meters / 1000,
            MeasurementUnit.Miles => meters / 1609.34,
            MeasurementUnit.Feet => meters / 0.3048,
            MeasurementUnit.NauticalMiles => meters / 1852,
            _ => meters
        };
    }

    /// <summary>
    /// Convert area between units
    /// </summary>
    public double ConvertArea(double value, MeasurementUnit from, MeasurementUnit to)
    {
        // Convert to square meters first
        var squareMeters = from switch
        {
            MeasurementUnit.SquareMeters => value,
            MeasurementUnit.SquareKilometers => value * 1_000_000,
            MeasurementUnit.SquareMiles => value * 2_589_988.11,
            MeasurementUnit.SquareFeet => value * 0.092903,
            MeasurementUnit.Acres => value * 4046.86,
            MeasurementUnit.Hectares => value * 10_000,
            _ => value
        };

        // Convert from square meters to target unit
        return to switch
        {
            MeasurementUnit.SquareMeters => squareMeters,
            MeasurementUnit.SquareKilometers => squareMeters / 1_000_000,
            MeasurementUnit.SquareMiles => squareMeters / 2_589_988.11,
            MeasurementUnit.SquareFeet => squareMeters / 0.092903,
            MeasurementUnit.Acres => squareMeters / 4046.86,
            MeasurementUnit.Hectares => squareMeters / 10_000,
            _ => squareMeters
        };
    }

    // Private helper methods

    private double CalculateHaversineDistance(double[] coord1, double[] coord2)
    {
        var lat1 = coord1[1] * Math.PI / 180;
        var lat2 = coord2[1] * Math.PI / 180;
        var deltaLat = (coord2[1] - coord1[1]) * Math.PI / 180;
        var deltaLon = (coord2[0] - coord1[0]) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private double CalculateGeodesicArea(List<double[]> coordinates)
    {
        // Simplified area calculation using spherical excess formula
        // For more accurate results, use NetTopologySuite with a proper geodetic calculator

        if (coordinates.Count < 3)
            return 0;

        var area = 0.0;
        var p1 = coordinates[^1]; // Last point

        for (int i = 0; i < coordinates.Count; i++)
        {
            var p2 = coordinates[i];
            var lon1 = p1[0] * Math.PI / 180;
            var lat1 = p1[1] * Math.PI / 180;
            var lon2 = p2[0] * Math.PI / 180;
            var lat2 = p2[1] * Math.PI / 180;

            area += (lon2 - lon1) * (2 + Math.Sin(lat1) + Math.Sin(lat2));
            p1 = p2;
        }

        area = area * EarthRadiusMeters * EarthRadiusMeters / 2;
        return Math.Abs(area);
    }

    private string FormatDistance(double value, MeasurementUnit unit)
    {
        var unitStr = unit switch
        {
            MeasurementUnit.Meters => value < 1000 ? "m" : "km",
            MeasurementUnit.Kilometers => "km",
            MeasurementUnit.Miles => "mi",
            MeasurementUnit.Feet => "ft",
            MeasurementUnit.NauticalMiles => "nm",
            _ => "m"
        };

        // Auto-convert to larger unit if appropriate
        if (unit == MeasurementUnit.Meters && value >= 1000)
        {
            value = value / 1000;
            unitStr = "km";
        }
        else if (unit == MeasurementUnit.Feet && value >= 5280)
        {
            value = value / 5280;
            unitStr = "mi";
        }

        return $"{value:F2} {unitStr}";
    }

    private string FormatArea(double value, MeasurementUnit unit)
    {
        var unitStr = unit switch
        {
            MeasurementUnit.SquareMeters => value < 10000 ? "m²" : "km²",
            MeasurementUnit.SquareKilometers => "km²",
            MeasurementUnit.SquareMiles => "mi²",
            MeasurementUnit.SquareFeet => "ft²",
            MeasurementUnit.Acres => "acres",
            MeasurementUnit.Hectares => "ha",
            _ => "m²"
        };

        // Auto-convert to larger unit if appropriate
        if (unit == MeasurementUnit.SquareMeters && value >= 10000)
        {
            value = value / 1_000_000;
            unitStr = "km²";
        }

        return $"{value:F2} {unitStr}";
    }

    private string GetCardinalDirection(double degrees)
    {
        var directions = new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        var index = (int)Math.Round(degrees / 22.5) % 16;
        return directions[index];
    }

    private double[] InterpolateCoordinate(double[] coord1, double[] coord2, double fraction)
    {
        return new[]
        {
            coord1[0] + (coord2[0] - coord1[0]) * fraction,
            coord1[1] + (coord2[1] - coord1[1]) * fraction
        };
    }

    private double CalculateElevationGain(List<ElevationPoint> points)
    {
        var gain = 0.0;
        for (int i = 1; i < points.Count; i++)
        {
            var diff = points[i].Elevation - points[i - 1].Elevation;
            if (diff > 0)
                gain += diff;
        }
        return gain;
    }

    private double CalculateElevationLoss(List<ElevationPoint> points)
    {
        var loss = 0.0;
        for (int i = 1; i < points.Count; i++)
        {
            var diff = points[i].Elevation - points[i - 1].Elevation;
            if (diff < 0)
                loss += Math.Abs(diff);
        }
        return loss;
    }
}

/// <summary>
/// Result of a measurement operation
/// </summary>
public class MeasurementResult
{
    public required MeasurementType Type { get; init; }
    public required double Value { get; init; }
    public required MeasurementUnit Unit { get; init; }
    public required string FormattedValue { get; init; }
    public double? RawValueInMeters { get; init; }
    public double? RawValueInSquareMeters { get; init; }
    public int? SegmentCount { get; init; }
}

/// <summary>
/// Result of a bearing calculation
/// </summary>
public class BearingResult
{
    public required double Degrees { get; init; }
    public required double Radians { get; init; }
    public required string CardinalDirection { get; init; }
    public required string FormattedValue { get; init; }
}

/// <summary>
/// Elevation profile along a line
/// </summary>
public class ElevationProfile
{
    public required List<double[]> Coordinates { get; init; }
    public required List<ElevationPoint> SamplePoints { get; init; }
    public double MinElevation { get; set; }
    public double MaxElevation { get; set; }
    public double TotalDistance { get; set; }
    public double ElevationGain { get; set; }
    public double ElevationLoss { get; set; }
}

/// <summary>
/// Point along an elevation profile
/// </summary>
public class ElevationPoint
{
    public required double Distance { get; init; } // Distance from start in meters
    public required double Elevation { get; init; } // Elevation in meters
    public required double[] Coordinate { get; init; }
}

/// <summary>
/// Types of measurements
/// </summary>
public enum MeasurementType
{
    Distance,
    Area,
    Bearing,
    Elevation
}

/// <summary>
/// Units of measurement
/// </summary>
public enum MeasurementUnit
{
    // Distance
    Meters,
    Kilometers,
    Feet,
    Miles,
    NauticalMiles,

    // Area
    SquareMeters,
    SquareKilometers,
    SquareFeet,
    SquareMiles,
    Acres,
    Hectares
}
