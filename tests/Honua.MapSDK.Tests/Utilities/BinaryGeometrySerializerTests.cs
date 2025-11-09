// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Drawing;
using Honua.MapSDK.Utilities;
using Xunit;

namespace Honua.MapSDK.Tests.Utilities;

/// <summary>
/// Tests for BinaryGeometrySerializer to ensure correct binary format and performance.
/// </summary>
public class BinaryGeometrySerializerTests
{
    [Fact]
    public async Task SerializeMeshAsync_ValidData_CreatesCorrectBinaryFormat()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }; // 2 vertices
        var colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }; // 2 colors (RGBA)

        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.SerializeMeshAsync(stream, positions, colors);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var vertexCount = reader.ReadUInt32();
        Assert.Equal(2u, vertexCount);

        // Read positions
        for (int i = 0; i < positions.Length; i++)
        {
            Assert.Equal(positions[i], reader.ReadSingle());
        }

        // Read colors
        for (int i = 0; i < colors.Length; i++)
        {
            Assert.Equal(colors[i], reader.ReadByte());
        }
    }

    [Fact]
    public async Task SerializeMeshAsync_WithStructuredVertices_CreatesCorrectFormat()
    {
        // Arrange
        var vertices = new[]
        {
            new MeshVertex(1.0f, 2.0f, 3.0f, 255, 0, 0, 255),
            new MeshVertex(4.0f, 5.0f, 6.0f, 0, 255, 0, 255)
        };

        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.SerializeMeshAsync(stream, vertices);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var vertexCount = reader.ReadUInt32();
        Assert.Equal(2u, vertexCount);

        // Verify first vertex
        Assert.Equal(1.0f, reader.ReadSingle());
        Assert.Equal(2.0f, reader.ReadSingle());
        Assert.Equal(3.0f, reader.ReadSingle());

        // Verify second vertex
        Assert.Equal(4.0f, reader.ReadSingle());
        Assert.Equal(5.0f, reader.ReadSingle());
        Assert.Equal(6.0f, reader.ReadSingle());

        // Verify colors
        Assert.Equal(255, reader.ReadByte());
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(255, reader.ReadByte());

        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(255, reader.ReadByte());
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(255, reader.ReadByte());
    }

    [Fact]
    public async Task SerializeMeshAsync_InvalidPositionCount_ThrowsException()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f }; // Not a multiple of 3
        var colors = new byte[] { 255, 0, 0, 255 };

        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await BinaryGeometrySerializer.SerializeMeshAsync(stream, positions, colors)
        );
    }

    [Fact]
    public async Task SerializeMeshAsync_MismatchedCounts_ThrowsException()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f, 3.0f }; // 1 vertex
        var colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }; // 2 colors

        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await BinaryGeometrySerializer.SerializeMeshAsync(stream, positions, colors)
        );
    }

    [Fact]
    public async Task SerializePointCloudAsync_ValidData_CreatesCorrectFormat()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f, 3.0f }; // 1 point
        var colors = new byte[] { 255, 0, 0, 255 }; // 1 color
        var sizes = new float[] { 5.0f }; // 1 size

        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.SerializePointCloudAsync(stream, positions, colors, sizes);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var pointCount = reader.ReadUInt32();
        Assert.Equal(1u, pointCount);

        // Read position
        Assert.Equal(1.0f, reader.ReadSingle());
        Assert.Equal(2.0f, reader.ReadSingle());
        Assert.Equal(3.0f, reader.ReadSingle());

        // Read color
        Assert.Equal(255, reader.ReadByte());
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(255, reader.ReadByte());

        // Read size
        Assert.Equal(5.0f, reader.ReadSingle());
    }

    [Fact]
    public async Task SerializePointCloudAsync_NoSizes_UsesDefaultSizes()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f, 3.0f };
        var colors = new byte[] { 255, 0, 0, 255 };

        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.SerializePointCloudAsync(stream, positions, colors, null);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var pointCount = reader.ReadUInt32();
        reader.ReadSingle(); // x
        reader.ReadSingle(); // y
        reader.ReadSingle(); // z
        reader.ReadBytes(4); // color

        var size = reader.ReadSingle();
        Assert.Equal(1.0f, size); // Default size
    }

    [Fact]
    public async Task SerializeIndexedMeshAsync_ValidData_CreatesCorrectFormat()
    {
        // Arrange
        var positions = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f }; // 3 vertices
        var colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255 }; // 3 colors
        var indices = new uint[] { 0, 1, 2 }; // 1 triangle

        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.SerializeIndexedMeshAsync(stream, positions, colors, indices);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var vertexCount = reader.ReadUInt32();
        var indexCount = reader.ReadUInt32();

        Assert.Equal(3u, vertexCount);
        Assert.Equal(3u, indexCount);

        // Skip positions and colors
        reader.ReadBytes(positions.Length * 4 + colors.Length);

        // Read indices
        Assert.Equal(0u, reader.ReadUInt32());
        Assert.Equal(1u, reader.ReadUInt32());
        Assert.Equal(2u, reader.ReadUInt32());
    }

    [Fact]
    public async Task CreateTestCubeAsync_GeneratesValidCube()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.CreateTestCubeAsync(stream, 1);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var vertexCount = reader.ReadUInt32();
        Assert.Equal(8u, vertexCount); // Cube has 8 vertices
    }

    [Fact]
    public async Task CreateTestCubeAsync_MultipleCubes_GeneratesCorrectCount()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        await BinaryGeometrySerializer.CreateTestCubeAsync(stream, 5);

        // Assert
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var vertexCount = reader.ReadUInt32();
        Assert.Equal(40u, vertexCount); // 5 cubes * 8 vertices
    }

    [Fact]
    public void MeshVertex_Constructor_SetsPropertiesCorrectly()
    {
        // Act
        var vertex = new MeshVertex(1.0f, 2.0f, 3.0f, 255, 128, 64, 200);

        // Assert
        Assert.Equal(1.0f, vertex.X);
        Assert.Equal(2.0f, vertex.Y);
        Assert.Equal(3.0f, vertex.Z);
        Assert.Equal(255, vertex.R);
        Assert.Equal(128, vertex.G);
        Assert.Equal(64, vertex.B);
        Assert.Equal(200, vertex.A);
    }

    [Fact]
    public void MeshVertex_ColorConstructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var color = Color.FromArgb(200, 255, 128, 64);

        // Act
        var vertex = new MeshVertex(1.0f, 2.0f, 3.0f, color);

        // Assert
        Assert.Equal(1.0f, vertex.X);
        Assert.Equal(2.0f, vertex.Y);
        Assert.Equal(3.0f, vertex.Z);
        Assert.Equal(255, vertex.R);
        Assert.Equal(128, vertex.G);
        Assert.Equal(64, vertex.B);
        Assert.Equal(200, vertex.A);
    }

    [Fact]
    public async Task SerializeMesh_Performance_IsAcceptable()
    {
        // Arrange - Large dataset
        const int vertexCount = 100000;
        var positions = new float[vertexCount * 3];
        var colors = new byte[vertexCount * 4];

        var random = new Random(42);
        for (int i = 0; i < positions.Length; i++)
            positions[i] = (float)random.NextDouble();
        for (int i = 0; i < colors.Length; i++)
            colors[i] = (byte)random.Next(256);

        using var stream = new MemoryStream();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await BinaryGeometrySerializer.SerializeMeshAsync(stream, positions, colors);
        sw.Stop();

        // Assert - Should complete quickly (< 100ms for 100K vertices)
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"Serialization took {sw.ElapsedMilliseconds}ms, expected < 100ms");

        // Verify data size
        var expectedSize = 4 + (vertexCount * 3 * 4) + (vertexCount * 4); // header + positions + colors
        Assert.Equal(expectedSize, stream.Length);
    }
}
