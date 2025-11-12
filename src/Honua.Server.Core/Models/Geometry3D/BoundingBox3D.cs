// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Models.Geometry3D;

/// <summary>
/// Represents a 3D axis-aligned bounding box.
/// </summary>
public class BoundingBox3D
{
    /// <summary>
    /// Minimum X coordinate
    /// </summary>
    public double MinX { get; set; }

    /// <summary>
    /// Minimum Y coordinate
    /// </summary>
    public double MinY { get; set; }

    /// <summary>
    /// Minimum Z coordinate
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// Maximum X coordinate
    /// </summary>
    public double MaxX { get; set; }

    /// <summary>
    /// Maximum Y coordinate
    /// </summary>
    public double MaxY { get; set; }

    /// <summary>
    /// Maximum Z coordinate
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// Width of the bounding box (X dimension)
    /// </summary>
    public double Width => MaxX - MinX;

    /// <summary>
    /// Height of the bounding box (Y dimension)
    /// </summary>
    public double Height => MaxY - MinY;

    /// <summary>
    /// Depth of the bounding box (Z dimension)
    /// </summary>
    public double Depth => MaxZ - MinZ;

    /// <summary>
    /// Center point of the bounding box
    /// </summary>
    public (double X, double Y, double Z) Center => (
        (MinX + MaxX) / 2,
        (MinY + MaxY) / 2,
        (MinZ + MaxZ) / 2
    );

    /// <summary>
    /// Creates an empty bounding box
    /// </summary>
    public BoundingBox3D()
    {
        MinX = MinY = MinZ = double.MaxValue;
        MaxX = MaxY = MaxZ = double.MinValue;
    }

    /// <summary>
    /// Creates a bounding box from min and max coordinates
    /// </summary>
    public BoundingBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    /// <summary>
    /// Expands the bounding box to include a point
    /// </summary>
    public void Expand(double x, double y, double z)
    {
        MinX = Math.Min(MinX, x);
        MinY = Math.Min(MinY, y);
        MinZ = Math.Min(MinZ, z);
        MaxX = Math.Max(MaxX, x);
        MaxY = Math.Max(MaxY, y);
        MaxZ = Math.Max(MaxZ, z);
    }

    /// <summary>
    /// Checks if this bounding box intersects another
    /// </summary>
    public bool Intersects(BoundingBox3D other)
    {
        return !(MaxX < other.MinX || MinX > other.MaxX ||
                 MaxY < other.MinY || MinY > other.MaxY ||
                 MaxZ < other.MinZ || MinZ > other.MaxZ);
    }

    /// <summary>
    /// Checks if this bounding box contains a point
    /// </summary>
    public bool Contains(double x, double y, double z)
    {
        return x >= MinX && x <= MaxX &&
               y >= MinY && y <= MaxY &&
               z >= MinZ && z <= MaxZ;
    }
}
