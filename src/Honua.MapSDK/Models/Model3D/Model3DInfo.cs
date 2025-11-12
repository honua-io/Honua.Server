// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Model3D;

/// <summary>
/// Contains metadata and information about a loaded 3D model.
/// Provides details about the model structure, animations, and performance metrics.
/// </summary>
public sealed class Model3DInfo
{
    /// <summary>
    /// Unique identifier for the model instance
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// URL or path to the GLTF/GLB file
    /// </summary>
    public required string ModelUrl { get; init; }

    /// <summary>
    /// Model format (GLTF or GLB)
    /// </summary>
    public string Format { get; init; } = "GLB";

    /// <summary>
    /// List of animations available in the model
    /// </summary>
    public List<Model3DAnimation> Animations { get; init; } = new();

    /// <summary>
    /// Bounding box of the model in local space
    /// </summary>
    public BoundingBox3D? BoundingBox { get; init; }

    /// <summary>
    /// Total number of vertices in the model
    /// </summary>
    public int VertexCount { get; init; }

    /// <summary>
    /// Total number of triangles in the model
    /// </summary>
    public int TriangleCount { get; init; }

    /// <summary>
    /// Number of meshes in the model
    /// </summary>
    public int MeshCount { get; init; }

    /// <summary>
    /// Number of materials in the model
    /// </summary>
    public int MaterialCount { get; init; }

    /// <summary>
    /// Number of textures in the model
    /// </summary>
    public int TextureCount { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Indicates if the model has been loaded successfully
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Time taken to load the model (milliseconds)
    /// </summary>
    public double? LoadTimeMs { get; init; }

    /// <summary>
    /// List of LOD (Level of Detail) variants if available
    /// </summary>
    public List<Model3DLodLevel> LodLevels { get; init; } = new();

    /// <summary>
    /// Indicates if the model supports PBR (Physically Based Rendering)
    /// </summary>
    public bool SupportsPBR { get; init; }

    /// <summary>
    /// Additional metadata from the GLTF file
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets a human-readable file size string
    /// </summary>
    public string GetFileSizeString()
    {
        if (!FileSizeBytes.HasValue) return "Unknown";

        var bytes = FileSizeBytes.Value;
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Gets performance category based on complexity
    /// </summary>
    public PerformanceCategory GetPerformanceCategory()
    {
        // Simple heuristic based on triangle count
        return TriangleCount switch
        {
            < 5000 => PerformanceCategory.Low,
            < 25000 => PerformanceCategory.Medium,
            < 100000 => PerformanceCategory.High,
            _ => PerformanceCategory.VeryHigh
        };
    }
}

/// <summary>
/// Represents an animation clip in a 3D model
/// </summary>
public sealed class Model3DAnimation
{
    /// <summary>
    /// Animation name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Animation index in the model
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public double Duration { get; init; }

    /// <summary>
    /// Number of animation channels (tracks)
    /// </summary>
    public int ChannelCount { get; init; }

    /// <summary>
    /// Animation type (e.g., "translation", "rotation", "scale")
    /// </summary>
    public string Type { get; init; } = "general";

    /// <summary>
    /// Indicates if this animation loops by default
    /// </summary>
    public bool Loop { get; init; } = true;
}

/// <summary>
/// Represents a 3D bounding box
/// </summary>
public sealed class BoundingBox3D
{
    /// <summary>
    /// Minimum coordinates (x, y, z)
    /// </summary>
    public required Vector3 Min { get; init; }

    /// <summary>
    /// Maximum coordinates (x, y, z)
    /// </summary>
    public required Vector3 Max { get; init; }

    /// <summary>
    /// Center of the bounding box
    /// </summary>
    public Vector3 Center => new()
    {
        X = (Min.X + Max.X) / 2,
        Y = (Min.Y + Max.Y) / 2,
        Z = (Min.Z + Max.Z) / 2
    };

    /// <summary>
    /// Size of the bounding box
    /// </summary>
    public Vector3 Size => new()
    {
        X = Max.X - Min.X,
        Y = Max.Y - Min.Y,
        Z = Max.Z - Min.Z
    };

    /// <summary>
    /// Maximum dimension of the bounding box
    /// </summary>
    public double MaxDimension => Math.Max(Math.Max(Size.X, Size.Y), Size.Z);
}

/// <summary>
/// Represents a 3D vector for position, rotation, or scale
/// </summary>
public sealed class Vector3
{
    /// <summary>
    /// X component
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y component
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Z component
    /// </summary>
    public double Z { get; init; }

    /// <summary>
    /// Zero vector (0, 0, 0)
    /// </summary>
    public static Vector3 Zero => new() { X = 0, Y = 0, Z = 0 };

    /// <summary>
    /// One vector (1, 1, 1)
    /// </summary>
    public static Vector3 One => new() { X = 1, Y = 1, Z = 1 };

    /// <summary>
    /// Converts to array [x, y, z]
    /// </summary>
    public double[] ToArray() => new[] { X, Y, Z };

    /// <summary>
    /// Creates Vector3 from array
    /// </summary>
    public static Vector3 FromArray(double[] array)
    {
        if (array.Length < 3)
            throw new ArgumentException("Array must have at least 3 elements", nameof(array));
        return new Vector3 { X = array[0], Y = array[1], Z = array[2] };
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>
/// Represents a Level of Detail variant for a 3D model
/// </summary>
public sealed class Model3DLodLevel
{
    /// <summary>
    /// LOD level (0 = highest quality)
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// URL to the LOD model file
    /// </summary>
    public required string ModelUrl { get; init; }

    /// <summary>
    /// Triangle count for this LOD level
    /// </summary>
    public int TriangleCount { get; init; }

    /// <summary>
    /// Minimum distance from camera (meters) where this LOD should be used
    /// </summary>
    public double MinDistance { get; init; }

    /// <summary>
    /// Maximum distance from camera (meters) where this LOD should be used
    /// </summary>
    public double MaxDistance { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; init; }
}

/// <summary>
/// Performance category based on model complexity
/// </summary>
public enum PerformanceCategory
{
    /// <summary>Low complexity, suitable for mobile devices</summary>
    Low,

    /// <summary>Medium complexity, suitable for most devices</summary>
    Medium,

    /// <summary>High complexity, may impact performance on lower-end devices</summary>
    High,

    /// <summary>Very high complexity, recommended for desktop only</summary>
    VeryHigh
}

/// <summary>
/// Configuration for model picking (raycasting)
/// </summary>
public sealed class Model3DPickerConfig
{
    /// <summary>
    /// Enable picking for this model
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum picking distance in meters
    /// </summary>
    public double MaxDistance { get; init; } = 10000;

    /// <summary>
    /// Pick only visible objects (respects occlusion)
    /// </summary>
    public bool PickOnlyVisible { get; init; } = true;

    /// <summary>
    /// Return first intersection only
    /// </summary>
    public bool FirstIntersectionOnly { get; init; } = true;

    /// <summary>
    /// Layer mask for picking (bit flags)
    /// </summary>
    public int LayerMask { get; init; } = -1; // All layers
}

/// <summary>
/// Result of a model picking operation
/// </summary>
public sealed class Model3DPickResult
{
    /// <summary>
    /// Model ID that was picked
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Point of intersection in world coordinates
    /// </summary>
    public required Vector3 Point { get; init; }

    /// <summary>
    /// Geographic coordinate of the pick
    /// </summary>
    public Coordinate3D? Coordinate { get; init; }

    /// <summary>
    /// Distance from camera to intersection point
    /// </summary>
    public double Distance { get; init; }

    /// <summary>
    /// Normal vector at intersection point
    /// </summary>
    public Vector3? Normal { get; init; }

    /// <summary>
    /// Index of the picked mesh within the model
    /// </summary>
    public int? MeshIndex { get; init; }

    /// <summary>
    /// Name of the picked mesh
    /// </summary>
    public string? MeshName { get; init; }

    /// <summary>
    /// UV coordinates at intersection (for texture mapping)
    /// </summary>
    public Vector2? UV { get; init; }
}

/// <summary>
/// Represents a 2D vector for UV coordinates
/// </summary>
public sealed class Vector2
{
    /// <summary>U component</summary>
    public double U { get; init; }

    /// <summary>V component</summary>
    public double V { get; init; }

    /// <summary>String representation</summary>
    public override string ToString() => $"({U:F3}, {V:F3})";
}
