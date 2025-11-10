// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a 3D geographic coordinate with optional measure value.
/// Supports 2D (lon, lat), 3D (lon, lat, z), and 4D (lon, lat, z, m) coordinates.
/// </summary>
public sealed record Coordinate3D
{
    /// <summary>
    /// Longitude (X) in degrees
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Latitude (Y) in degrees
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Elevation/Altitude (Z) in meters.
    /// Null indicates a 2D coordinate.
    /// </summary>
    public double? Elevation { get; init; }

    /// <summary>
    /// Measure (M) value for linear referencing.
    /// Used for route mileage, time, or other linear measurements.
    /// </summary>
    public double? Measure { get; init; }

    /// <summary>
    /// Gets the coordinate dimension (2, 3, or 4).
    /// - 2D: lon, lat
    /// - 3D: lon, lat, z OR lon, lat, m
    /// - 4D: lon, lat, z, m
    /// </summary>
    public int Dimension => Measure.HasValue
        ? (Elevation.HasValue ? 4 : 3)
        : (Elevation.HasValue ? 3 : 2);

    /// <summary>
    /// Indicates whether this coordinate has a Z (elevation) component.
    /// </summary>
    public bool HasZ => Elevation.HasValue;

    /// <summary>
    /// Indicates whether this coordinate has an M (measure) component.
    /// </summary>
    public bool HasM => Measure.HasValue;

    /// <summary>
    /// WGS84 3D SRID (EPSG:4979).
    /// Use this for 3D geographic coordinates with ellipsoidal height.
    /// </summary>
    public const int Srid3D = 4979;

    /// <summary>
    /// Converts the coordinate to a GeoJSON coordinate array.
    /// Returns [lon, lat] for 2D, [lon, lat, z] for 3D, or [lon, lat, z, m] for 4D.
    /// </summary>
    /// <returns>Array representation of the coordinate</returns>
    public double[] ToArray() => Dimension switch
    {
        2 => [Longitude, Latitude],
        3 when Elevation.HasValue => [Longitude, Latitude, Elevation.Value],
        3 when Measure.HasValue => [Longitude, Latitude, Measure.Value],
        4 => [Longitude, Latitude, Elevation!.Value, Measure!.Value],
        _ => [Longitude, Latitude]
    };

    /// <summary>
    /// Parses a coordinate from a GeoJSON coordinate array.
    /// Supports [lon, lat], [lon, lat, z], and [lon, lat, z, m] formats.
    /// </summary>
    /// <param name="coords">Coordinate array from GeoJSON</param>
    /// <returns>Parsed Coordinate3D instance</returns>
    /// <exception cref="ArgumentException">Thrown when coordinate array is invalid</exception>
    public static Coordinate3D FromArray(double[] coords) => coords.Length switch
    {
        >= 4 => new() { Longitude = coords[0], Latitude = coords[1],
                        Elevation = coords[2], Measure = coords[3] },
        >= 3 => new() { Longitude = coords[0], Latitude = coords[1],
                        Elevation = coords[2] },
        >= 2 => new() { Longitude = coords[0], Latitude = coords[1] },
        _ => throw new ArgumentException("Coordinate array must have at least 2 elements (lon, lat)", nameof(coords))
    };

    /// <summary>
    /// Creates a 2D coordinate (lon, lat).
    /// </summary>
    public static Coordinate3D Create2D(double longitude, double latitude) => new()
    {
        Longitude = longitude,
        Latitude = latitude
    };

    /// <summary>
    /// Creates a 3D coordinate (lon, lat, elevation).
    /// </summary>
    public static Coordinate3D Create3D(double longitude, double latitude, double elevation) => new()
    {
        Longitude = longitude,
        Latitude = latitude,
        Elevation = elevation
    };

    /// <summary>
    /// Creates a 4D coordinate (lon, lat, elevation, measure).
    /// </summary>
    public static Coordinate3D Create4D(double longitude, double latitude, double elevation, double measure) => new()
    {
        Longitude = longitude,
        Latitude = latitude,
        Elevation = elevation,
        Measure = measure
    };

    /// <summary>
    /// Gets the OGC geometry type suffix based on coordinate dimension.
    /// Returns empty string for 2D, "Z" for 3D with elevation, "M" for 3D with measure, or "ZM" for 4D.
    /// </summary>
    public string GetOgcTypeSuffix() => Dimension switch
    {
        4 => "ZM",
        3 when Elevation.HasValue => "Z",
        3 when Measure.HasValue => "M",
        _ => ""
    };

    /// <summary>
    /// Validates that the coordinate is within valid WGS84 bounds.
    /// Longitude must be between -180 and 180, latitude between -90 and 90.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return Longitude >= -180 && Longitude <= 180 &&
               Latitude >= -90 && Latitude <= 90;
    }

    /// <summary>
    /// Returns a string representation of the coordinate.
    /// Format: (lon, lat, z) or (lon, lat, z, m)
    /// </summary>
    public override string ToString()
    {
        return Dimension switch
        {
            4 => $"({Longitude}, {Latitude}, {Elevation}, {Measure})",
            3 when Elevation.HasValue => $"({Longitude}, {Latitude}, {Elevation})",
            3 when Measure.HasValue => $"({Longitude}, {Latitude}, M={Measure})",
            _ => $"({Longitude}, {Latitude})"
        };
    }
}
