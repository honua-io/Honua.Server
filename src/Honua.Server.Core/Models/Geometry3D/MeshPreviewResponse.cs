// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Models.Geometry3D;

/// <summary>
/// Response model for 3D mesh preview data optimized for web rendering.
/// Supports both simplified mesh format and glTF for Deck.gl visualization.
/// </summary>
public class MeshPreviewResponse
{
    /// <summary>
    /// Geometry ID
    /// </summary>
    public Guid GeometryId { get; set; }

    /// <summary>
    /// Preview format (simple or gltf)
    /// </summary>
    public string Format { get; set; } = "simple";

    /// <summary>
    /// Level of detail (0-100, where 0 is highest quality, 100 is most simplified)
    /// </summary>
    public int LevelOfDetail { get; set; }

    /// <summary>
    /// Bounding box of the mesh
    /// </summary>
    public BoundingBox3D BoundingBox { get; set; } = new();

    /// <summary>
    /// Center position (geographic coordinates)
    /// </summary>
    public GeographicPosition Center { get; set; } = new();

    /// <summary>
    /// Number of vertices in the preview mesh
    /// </summary>
    public int VertexCount { get; set; }

    /// <summary>
    /// Number of faces in the preview mesh
    /// </summary>
    public int FaceCount { get; set; }

    /// <summary>
    /// Original vertex count (before LOD reduction)
    /// </summary>
    public int OriginalVertexCount { get; set; }

    /// <summary>
    /// Original face count (before LOD reduction)
    /// </summary>
    public int OriginalFaceCount { get; set; }

    /// <summary>
    /// Simplified mesh data (for 'simple' format)
    /// </summary>
    public SimpleMeshData? MeshData { get; set; }

    /// <summary>
    /// glTF JSON data (for 'gltf' format)
    /// </summary>
    public object? GltfData { get; set; }

    /// <summary>
    /// Source format of the original geometry
    /// </summary>
    public string? SourceFormat { get; set; }

    /// <summary>
    /// Any warnings generated during preview conversion
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Simplified mesh data optimized for web rendering with Deck.gl SimpleMeshLayer
/// </summary>
public class SimpleMeshData
{
    /// <summary>
    /// Vertex positions as flat array [x1, y1, z1, x2, y2, z2, ...]
    /// Coordinates are relative to the center position
    /// </summary>
    public float[] Positions { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Vertex normals as flat array [nx1, ny1, nz1, nx2, ny2, nz2, ...]
    /// </summary>
    public float[] Normals { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Triangle indices as flat array [i1, i2, i3, i4, i5, i6, ...]
    /// </summary>
    public int[] Indices { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Vertex colors as flat array [r1, g1, b1, a1, r2, g2, b2, a2, ...]
    /// Optional - values in range [0, 255]
    /// </summary>
    public byte[]? Colors { get; set; }

    /// <summary>
    /// Texture coordinates as flat array [u1, v1, u2, v2, ...]
    /// Optional - values in range [0, 1]
    /// </summary>
    public float[]? TexCoords { get; set; }

    /// <summary>
    /// Whether the mesh has vertex colors
    /// </summary>
    public bool HasColors => Colors != null && Colors.Length > 0;

    /// <summary>
    /// Whether the mesh has texture coordinates
    /// </summary>
    public bool HasTexCoords => TexCoords != null && TexCoords.Length > 0;
}

/// <summary>
/// Geographic position (longitude, latitude, altitude)
/// </summary>
public class GeographicPosition
{
    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Altitude in meters (optional)
    /// </summary>
    public double Altitude { get; set; }

    /// <summary>
    /// Creates a geographic position from X, Y, Z coordinates
    /// Assumes X=Longitude, Y=Latitude, Z=Altitude
    /// </summary>
    public static GeographicPosition FromXYZ(double x, double y, double z)
    {
        return new GeographicPosition
        {
            Longitude = x,
            Latitude = y,
            Altitude = z
        };
    }

    /// <summary>
    /// Creates a geographic position from the center of a bounding box
    /// </summary>
    public static GeographicPosition FromBoundingBox(BoundingBox3D bbox)
    {
        var center = bbox.Center;
        return new GeographicPosition
        {
            Longitude = center.X,
            Latitude = center.Y,
            Altitude = center.Z
        };
    }
}
