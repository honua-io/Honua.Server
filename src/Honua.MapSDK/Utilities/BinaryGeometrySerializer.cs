// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Drawing;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Serializes 3D geometry data into optimized binary formats for efficient Blazor-JS interop.
/// Provides 6x faster transfer compared to JSON serialization.
/// </summary>
/// <remarks>
/// Binary formats are designed for zero-copy transfers using DotNetStreamReference.
/// See /docs/BLAZOR_3D_INTEROP_PERFORMANCE.md for benchmarks and usage patterns.
/// </remarks>
public static class BinaryGeometrySerializer
{
    /// <summary>
    /// Serializes a 3D mesh into binary format.
    /// Format: [vertexCount(uint32)][positions(float32[])][colors(uint8[])]
    /// </summary>
    /// <param name="stream">Target stream to write binary data</param>
    /// <param name="positions">Vertex positions as [x1,y1,z1, x2,y2,z2, ...]</param>
    /// <param name="colors">Vertex colors as [r1,g1,b1,a1, r2,g2,b2,a2, ...]</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SerializeMeshAsync(Stream stream, float[] positions, byte[] colors)
    {
        if (positions == null) throw new ArgumentNullException(nameof(positions));
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (positions.Length % 3 != 0) throw new ArgumentException("Positions must be a multiple of 3 (x,y,z)", nameof(positions));
        if (colors.Length % 4 != 0) throw new ArgumentException("Colors must be a multiple of 4 (r,g,b,a)", nameof(colors));

        int vertexCount = positions.Length / 3;
        if (colors.Length / 4 != vertexCount)
            throw new ArgumentException($"Color count ({colors.Length / 4}) must match vertex count ({vertexCount})");

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Write header
        writer.Write(vertexCount);

        // Write positions (float32[])
        foreach (var pos in positions)
        {
            writer.Write(pos);
        }

        // Write colors (uint8[])
        writer.Write(colors);

        await stream.FlushAsync();
    }

    /// <summary>
    /// Serializes a 3D mesh from structured vertex data.
    /// </summary>
    /// <param name="stream">Target stream to write binary data</param>
    /// <param name="vertices">Array of vertices with position and color</param>
    public static async Task SerializeMeshAsync(Stream stream, MeshVertex[] vertices)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));

        var positions = new float[vertices.Length * 3];
        var colors = new byte[vertices.Length * 4];

        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            int posIdx = i * 3;
            int colorIdx = i * 4;

            positions[posIdx] = v.X;
            positions[posIdx + 1] = v.Y;
            positions[posIdx + 2] = v.Z;

            colors[colorIdx] = v.R;
            colors[colorIdx + 1] = v.G;
            colors[colorIdx + 2] = v.B;
            colors[colorIdx + 3] = v.A;
        }

        await SerializeMeshAsync(stream, positions, colors);
    }

    /// <summary>
    /// Serializes a point cloud into binary format.
    /// Format: [pointCount(uint32)][positions(float32[])][colors(uint8[])][sizes(float32[])]
    /// </summary>
    /// <param name="stream">Target stream to write binary data</param>
    /// <param name="positions">Point positions as [x1,y1,z1, x2,y2,z2, ...]</param>
    /// <param name="colors">Point colors as [r1,g1,b1,a1, r2,g2,b2,a2, ...]</param>
    /// <param name="sizes">Point sizes (optional, defaults to 1.0 for all points)</param>
    public static async Task SerializePointCloudAsync(
        Stream stream,
        float[] positions,
        byte[] colors,
        float[]? sizes = null)
    {
        if (positions == null) throw new ArgumentNullException(nameof(positions));
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (positions.Length % 3 != 0) throw new ArgumentException("Positions must be a multiple of 3 (x,y,z)", nameof(positions));
        if (colors.Length % 4 != 0) throw new ArgumentException("Colors must be a multiple of 4 (r,g,b,a)", nameof(colors));

        int pointCount = positions.Length / 3;
        if (colors.Length / 4 != pointCount)
            throw new ArgumentException($"Color count ({colors.Length / 4}) must match point count ({pointCount})");

        if (sizes != null && sizes.Length != pointCount)
            throw new ArgumentException($"Size count ({sizes.Length}) must match point count ({pointCount})");

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Write header
        writer.Write(pointCount);

        // Write positions (float32[])
        foreach (var pos in positions)
        {
            writer.Write(pos);
        }

        // Write colors (uint8[])
        writer.Write(colors);

        // Write sizes (float32[])
        if (sizes != null)
        {
            foreach (var size in sizes)
            {
                writer.Write(size);
            }
        }
        else
        {
            // Default size of 1.0 for all points
            for (int i = 0; i < pointCount; i++)
            {
                writer.Write(1.0f);
            }
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Serializes an indexed mesh with triangle indices.
    /// Format: [vertexCount(uint32)][indexCount(uint32)][positions(float32[])][colors(uint8[])][indices(uint32[])]
    /// </summary>
    /// <param name="stream">Target stream to write binary data</param>
    /// <param name="positions">Vertex positions as [x1,y1,z1, x2,y2,z2, ...]</param>
    /// <param name="colors">Vertex colors as [r1,g1,b1,a1, r2,g2,b2,a2, ...]</param>
    /// <param name="indices">Triangle indices (3 per triangle)</param>
    public static async Task SerializeIndexedMeshAsync(
        Stream stream,
        float[] positions,
        byte[] colors,
        uint[] indices)
    {
        if (positions == null) throw new ArgumentNullException(nameof(positions));
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (positions.Length % 3 != 0) throw new ArgumentException("Positions must be a multiple of 3 (x,y,z)", nameof(positions));
        if (colors.Length % 4 != 0) throw new ArgumentException("Colors must be a multiple of 4 (r,g,b,a)", nameof(colors));
        if (indices.Length % 3 != 0) throw new ArgumentException("Indices must be a multiple of 3 (triangles)", nameof(indices));

        int vertexCount = positions.Length / 3;
        if (colors.Length / 4 != vertexCount)
            throw new ArgumentException($"Color count ({colors.Length / 4}) must match vertex count ({vertexCount})");

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Write header
        writer.Write(vertexCount);
        writer.Write(indices.Length);

        // Write positions (float32[])
        foreach (var pos in positions)
        {
            writer.Write(pos);
        }

        // Write colors (uint8[])
        writer.Write(colors);

        // Write indices (uint32[])
        foreach (var index in indices)
        {
            writer.Write(index);
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Creates a simple test mesh (colored cube) for benchmarking.
    /// </summary>
    /// <param name="stream">Target stream</param>
    /// <param name="sizeMultiplier">Number of cubes to generate (for stress testing)</param>
    public static async Task CreateTestCubeAsync(Stream stream, int sizeMultiplier = 1)
    {
        var cubeVertices = new List<MeshVertex>();

        for (int i = 0; i < sizeMultiplier; i++)
        {
            float offset = i * 2.0f;

            // Simple cube vertices (8 vertices)
            cubeVertices.AddRange(new[]
            {
                new MeshVertex(-1 + offset, -1, -1, 255, 0, 0, 255),
                new MeshVertex(1 + offset, -1, -1, 0, 255, 0, 255),
                new MeshVertex(1 + offset, 1, -1, 0, 0, 255, 255),
                new MeshVertex(-1 + offset, 1, -1, 255, 255, 0, 255),
                new MeshVertex(-1 + offset, -1, 1, 255, 0, 255, 255),
                new MeshVertex(1 + offset, -1, 1, 0, 255, 255, 255),
                new MeshVertex(1 + offset, 1, 1, 128, 128, 128, 255),
                new MeshVertex(-1 + offset, 1, 1, 255, 255, 255, 255),
            });
        }

        await SerializeMeshAsync(stream, cubeVertices.ToArray());
    }
}

/// <summary>
/// Represents a single vertex in a 3D mesh.
/// </summary>
public struct MeshVertex
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public MeshVertex(float x, float y, float z, byte r, byte g, byte b, byte a)
    {
        X = x;
        Y = y;
        Z = z;
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public MeshVertex(float x, float y, float z, Color color)
    {
        X = x;
        Y = y;
        Z = z;
        R = color.R;
        G = color.G;
        B = color.B;
        A = color.A;
    }
}
