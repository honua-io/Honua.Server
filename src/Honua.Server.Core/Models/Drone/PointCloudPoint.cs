// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Drone;

/// <summary>
/// Represents a single point in a point cloud
/// </summary>
public record PointCloudPoint(
    double X,
    double Y,
    double Z,
    ushort Red,
    ushort Green,
    ushort Blue,
    byte Classification,
    ushort? Intensity = null)
{
    /// <summary>
    /// Gets the RGB color as a normalized tuple [0-255]
    /// </summary>
    public (byte R, byte G, byte B) GetNormalizedColor()
    {
        return (
            (byte)(Red / 256),
            (byte)(Green / 256),
            (byte)(Blue / 256)
        );
    }

    /// <summary>
    /// Gets the classification name
    /// </summary>
    public string GetClassificationName() => Classification switch
    {
        0 => "Never Classified",
        1 => "Unclassified",
        2 => "Ground",
        3 => "Low Vegetation",
        4 => "Medium Vegetation",
        5 => "High Vegetation",
        6 => "Building",
        7 => "Low Point (Noise)",
        8 => "Reserved",
        9 => "Water",
        10 => "Rail",
        11 => "Road Surface",
        12 => "Reserved",
        13 => "Wire - Guard (Shield)",
        14 => "Wire - Conductor (Phase)",
        15 => "Transmission Tower",
        16 => "Wire-Structure Connector",
        17 => "Bridge Deck",
        18 => "High Noise",
        _ => $"Custom ({Classification})"
    };
}

/// <summary>
/// Bounding box for 3D spatial queries
/// </summary>
public record BoundingBox3D(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ)
{
    public static BoundingBox3D World => new(-180, -90, -10000, 180, 90, 10000);

    /// <summary>
    /// Checks if this bounding box intersects with another
    /// </summary>
    public bool Intersects(BoundingBox3D other)
    {
        return !(other.MinX > MaxX || other.MaxX < MinX ||
                 other.MinY > MaxY || other.MaxY < MinY ||
                 other.MinZ > MaxZ || other.MaxZ < MinZ);
    }

    /// <summary>
    /// Gets the volume of the bounding box
    /// </summary>
    public double GetVolume()
    {
        return (MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ);
    }
}

/// <summary>
/// Level of detail for point cloud rendering
/// </summary>
public enum PointCloudLodLevel
{
    /// <summary>Full resolution - 100% of points</summary>
    Full = 0,

    /// <summary>Coarse resolution - ~10% of points</summary>
    Coarse = 1,

    /// <summary>Sparse resolution - ~1% of points</summary>
    Sparse = 2
}

/// <summary>
/// Point cloud query options
/// </summary>
public class PointCloudQueryOptions
{
    /// <summary>
    /// Bounding box for spatial filtering
    /// </summary>
    public BoundingBox3D? BoundingBox { get; set; }

    /// <summary>
    /// Level of detail
    /// </summary>
    public PointCloudLodLevel LodLevel { get; set; } = PointCloudLodLevel.Full;

    /// <summary>
    /// Filter by classification codes (e.g., [2, 6] for ground and buildings)
    /// </summary>
    public int[]? ClassificationFilter { get; set; }

    /// <summary>
    /// Maximum number of points to return
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Offset for pagination
    /// </summary>
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Point cloud statistics
/// </summary>
public class PointCloudStatistics
{
    public long TotalPoints { get; set; }
    public BoundingBox3D? BoundingBox { get; set; }
    public Dictionary<byte, long>? ClassificationCounts { get; set; }
    public double? AveragePointDensity { get; set; }
    public double? MinZ { get; set; }
    public double? MaxZ { get; set; }
}
