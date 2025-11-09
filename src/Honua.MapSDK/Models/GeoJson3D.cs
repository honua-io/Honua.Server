using System.Text.Json;

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a 3D GeoJSON geometry with Z coordinates.
/// Provides dimension detection, Z-coordinate extraction, and validation.
/// </summary>
public sealed record GeoJson3D
{
    /// <summary>
    /// Geometry type (e.g., "Point", "LineString", "Polygon")
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Flattened coordinate array for efficient processing
    /// </summary>
    public required List<double> Coordinates { get; init; }

    /// <summary>
    /// Coordinate dimension (2, 3, or 4)
    /// </summary>
    public int Dimension { get; init; } = 3;

    /// <summary>
    /// Minimum Z value in the geometry
    /// </summary>
    public double? ZMin { get; init; }

    /// <summary>
    /// Maximum Z value in the geometry
    /// </summary>
    public double? ZMax { get; init; }

    /// <summary>
    /// Indicates whether the geometry has Z (elevation) coordinates
    /// </summary>
    public bool HasZ => Dimension >= 3;

    /// <summary>
    /// Get OGC geometry type name with dimension suffix (e.g., "PointZ", "LineStringZ")
    /// </summary>
    public string OgcTypeName => Dimension == 3 ? $"{Type}Z" :
                                  Dimension == 4 ? $"{Type}ZM" :
                                  Type;

    /// <summary>
    /// Parses a GeoJSON geometry object and extracts 3D information.
    /// </summary>
    /// <param name="geometryJson">GeoJSON geometry object</param>
    /// <returns>Parsed GeoJson3D instance</returns>
    /// <exception cref="ArgumentException">Thrown when geometry JSON is invalid</exception>
    public static GeoJson3D FromGeoJson(JsonElement geometryJson)
    {
        if (!geometryJson.TryGetProperty("type", out var typeElement))
        {
            throw new ArgumentException("Geometry must have a 'type' property", nameof(geometryJson));
        }

        if (!geometryJson.TryGetProperty("coordinates", out var coordsElement))
        {
            throw new ArgumentException("Geometry must have a 'coordinates' property", nameof(geometryJson));
        }

        var type = typeElement.GetString() ?? throw new ArgumentException("Geometry type cannot be null");
        var dimension = DetectDimension(coordsElement);
        var flatCoords = FlattenCoordinates(coordsElement);

        // Extract Z statistics if 3D
        double? zMin = null;
        double? zMax = null;
        if (dimension >= 3)
        {
            var zValues = ExtractZValues(flatCoords, dimension);
            if (zValues.Count > 0)
            {
                zMin = zValues.Min();
                zMax = zValues.Max();
            }
        }

        return new GeoJson3D
        {
            Type = type,
            Coordinates = flatCoords,
            Dimension = dimension,
            ZMin = zMin,
            ZMax = zMax
        };
    }

    /// <summary>
    /// Detects the coordinate dimension from a GeoJSON coordinates array.
    /// Navigates to the first leaf coordinate to determine if it's 2D, 3D, or 4D.
    /// </summary>
    private static int DetectDimension(JsonElement coords)
    {
        // Navigate to first coordinate (handle nested arrays for LineString, Polygon, etc.)
        var current = coords;
        while (current.ValueKind == JsonValueKind.Array &&
               current.GetArrayLength() > 0 &&
               current[0].ValueKind == JsonValueKind.Array)
        {
            current = current[0];
        }

        // Count elements in the coordinate array
        if (current.ValueKind == JsonValueKind.Array)
        {
            return current.GetArrayLength(); // 2, 3, or 4
        }

        return 2; // Default to 2D
    }

    /// <summary>
    /// Flattens nested coordinate arrays into a single list of doubles.
    /// </summary>
    private static List<double> FlattenCoordinates(JsonElement coords)
    {
        var result = new List<double>();
        FlattenRecursive(coords, result);
        return result;
    }

    /// <summary>
    /// Recursively flattens coordinate arrays.
    /// </summary>
    private static void FlattenRecursive(JsonElement element, List<double> result)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    result.Add(item.GetDouble());
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    FlattenRecursive(item, result);
                }
            }
        }
    }

    /// <summary>
    /// Extracts Z values from flattened coordinate array.
    /// </summary>
    private static List<double> ExtractZValues(List<double> flatCoords, int dimension)
    {
        var zValues = new List<double>();

        // Z coordinate is at index 2 in each coordinate tuple
        for (int i = 2; i < flatCoords.Count; i += dimension)
        {
            zValues.Add(flatCoords[i]);
        }

        return zValues;
    }

    /// <summary>
    /// Validates that all Z coordinates are within a reasonable range.
    /// </summary>
    /// <param name="minElevation">Minimum valid elevation (default: -500m, below sea level)</param>
    /// <param name="maxElevation">Maximum valid elevation (default: 9000m, Mt. Everest height)</param>
    /// <returns>True if all Z values are valid</returns>
    public bool ValidateZRange(double minElevation = -500, double maxElevation = 9000)
    {
        if (!HasZ || !ZMin.HasValue || !ZMax.HasValue)
        {
            return true; // No Z coordinates to validate
        }

        return ZMin.Value >= minElevation && ZMax.Value <= maxElevation;
    }

    /// <summary>
    /// Gets statistics about the Z (elevation) values in the geometry.
    /// </summary>
    public ZStatistics? GetZStatistics()
    {
        if (!HasZ || Coordinates.Count < Dimension)
        {
            return null;
        }

        var zValues = ExtractZValues(Coordinates, Dimension);
        if (zValues.Count == 0)
        {
            return null;
        }

        return new ZStatistics
        {
            Min = zValues.Min(),
            Max = zValues.Max(),
            Mean = zValues.Average(),
            Count = zValues.Count,
            Range = zValues.Max() - zValues.Min()
        };
    }

    /// <summary>
    /// Converts the geometry back to standard GeoJSON structure.
    /// Note: This reconstructs the coordinate structure based on geometry type.
    /// </summary>
    public string ToGeoJsonString()
    {
        // This is a simplified version - full implementation would need to
        // reconstruct the proper nested structure based on geometry type
        return $"{{\"type\":\"{Type}\",\"coordinates\":{System.Text.Json.JsonSerializer.Serialize(Coordinates)}}}";
    }
}

/// <summary>
/// Statistical information about Z (elevation) coordinates in a geometry.
/// </summary>
public sealed record ZStatistics
{
    /// <summary>Minimum elevation value</summary>
    public required double Min { get; init; }

    /// <summary>Maximum elevation value</summary>
    public required double Max { get; init; }

    /// <summary>Mean (average) elevation value</summary>
    public required double Mean { get; init; }

    /// <summary>Number of coordinates with Z values</summary>
    public required int Count { get; init; }

    /// <summary>Elevation range (Max - Min)</summary>
    public required double Range { get; init; }
}
