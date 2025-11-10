namespace Honua.Server.Core.Models.Geometry3D;

/// <summary>
/// Represents a 3D triangle mesh (vertices, faces, normals).
/// Used for OBJ, STL, glTF file formats.
/// </summary>
public class TriangleMesh
{
    /// <summary>
    /// Vertex positions as flat array [x1, y1, z1, x2, y2, z2, ...]
    /// </summary>
    public float[] Vertices { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Triangle indices as flat array [i1, i2, i3, i4, i5, i6, ...]
    /// Each triplet defines one triangle face
    /// </summary>
    public int[] Indices { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Vertex normals as flat array [nx1, ny1, nz1, nx2, ny2, nz2, ...]
    /// Optional - may be empty if not available
    /// </summary>
    public float[] Normals { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Texture coordinates as flat array [u1, v1, u2, v2, ...]
    /// Optional - may be empty if not available
    /// </summary>
    public float[] TexCoords { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Vertex colors as flat array [r1, g1, b1, a1, r2, g2, b2, a2, ...]
    /// Optional - may be empty if not available
    /// </summary>
    public float[] Colors { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Number of vertices in the mesh
    /// </summary>
    public int VertexCount => Vertices.Length / 3;

    /// <summary>
    /// Number of triangle faces in the mesh
    /// </summary>
    public int FaceCount => Indices.Length / 3;

    /// <summary>
    /// Calculates the bounding box of this mesh
    /// </summary>
    public BoundingBox3D GetBoundingBox()
    {
        if (Vertices.Length == 0)
            return new BoundingBox3D();

        var bbox = new BoundingBox3D();
        for (int i = 0; i < Vertices.Length; i += 3)
        {
            bbox.Expand(Vertices[i], Vertices[i + 1], Vertices[i + 2]);
        }
        return bbox;
    }

    /// <summary>
    /// Validates the mesh structure
    /// </summary>
    public bool IsValid()
    {
        // Must have vertices
        if (Vertices.Length == 0 || Vertices.Length % 3 != 0)
            return false;

        // Must have indices
        if (Indices.Length == 0 || Indices.Length % 3 != 0)
            return false;

        // Indices must be in range
        int maxVertex = VertexCount;
        foreach (var idx in Indices)
        {
            if (idx < 0 || idx >= maxVertex)
                return false;
        }

        // If normals exist, must match vertex count
        if (Normals.Length > 0 && Normals.Length != Vertices.Length)
            return false;

        // If texcoords exist, must be pairs
        if (TexCoords.Length > 0 && TexCoords.Length % 2 != 0)
            return false;

        // If colors exist, must be RGBA quads
        if (Colors.Length > 0 && Colors.Length % 4 != 0)
            return false;

        return true;
    }
}
