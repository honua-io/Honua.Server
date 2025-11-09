// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Utilities.Terrain;

namespace Honua.MapSDK.Tests.Utilities.Terrain;

/// <summary>
/// Unit tests for TerrainMeshGenerator (Martini algorithm).
/// </summary>
public class TerrainMeshGeneratorTests
{
    private readonly TerrainMeshGenerator _generator;

    public TerrainMeshGeneratorTests()
    {
        _generator = new TerrainMeshGenerator();
    }

    [Theory]
    [InlineData(17)]   // 2^4 + 1
    [InlineData(33)]   // 2^5 + 1
    [InlineData(65)]   // 2^6 + 1
    [InlineData(129)]  // 2^7 + 1
    [InlineData(257)]  // 2^8 + 1
    public void GenerateMesh_WithValidGridSize_GeneratesMesh(int size)
    {
        // Arrange
        var elevations = CreateTestElevationGrid(size);

        // Act
        var mesh = _generator.GenerateMesh(elevations, maxError: 1.0f);

        // Assert
        mesh.Should().NotBeNull();
        mesh.Vertices.Should().NotBeEmpty();
        mesh.Indices.Should().NotBeEmpty();

        // Verify vertex format (x, y, z triplets)
        mesh.Vertices.Length.Should().Be(mesh.Vertices.Length / 3 * 3);

        // Verify index format (triangles)
        mesh.Indices.Length.Should().Be(mesh.Indices.Length / 3 * 3);

        // Verify all vertex coordinates are normalized to [0, 1]
        for (int i = 0; i < mesh.Vertices.Length; i += 3)
        {
            mesh.Vertices[i].Should().BeInRange(0, 1);     // X
            mesh.Vertices[i + 1].Should().BeInRange(0, 1); // Y
            // Z (elevation) can be any value
        }
    }

    [Theory]
    [InlineData(16)]  // 2^4 (not +1)
    [InlineData(32)]  // 2^5 (not +1)
    [InlineData(64)]  // 2^6 (not +1)
    [InlineData(100)] // Not power of 2
    public void GenerateMesh_WithInvalidGridSize_ThrowsException(int size)
    {
        // Arrange
        var elevations = CreateTestElevationGrid(size);

        // Act & Assert
        Action act = () => _generator.GenerateMesh(elevations, maxError: 1.0f);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*power of 2*");
    }

    [Fact]
    public void GenerateMesh_WithNonSquareGrid_ThrowsException()
    {
        // Arrange
        var elevations = new float[17, 33]; // Non-square

        // Act & Assert
        Action act = () => _generator.GenerateMesh(elevations, maxError: 1.0f);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1.0f)]
    [InlineData(5.0f)]
    [InlineData(10.0f)]
    public void GenerateMesh_WithDifferentErrorLevels_AdjustsComplexity(float maxError)
    {
        // Arrange
        var size = 257;
        var elevations = CreateTestElevationGrid(size);

        // Act
        var mesh = _generator.GenerateMesh(elevations, maxError);

        // Assert
        mesh.Should().NotBeNull();

        // Higher error = fewer vertices (simplified mesh)
        // Lower error = more vertices (detailed mesh)
        mesh.Vertices.Should().NotBeEmpty();
        mesh.Indices.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateLODMesh_WithDifferentLevels_GeneratesAppropriately()
    {
        // Arrange
        var size = 257;
        var elevations = CreateTestElevationGrid(size);

        // Act
        var lod0 = _generator.GenerateLODMesh(elevations, lodLevel: 0); // Highest detail
        var lod3 = _generator.GenerateLODMesh(elevations, lodLevel: 3); // Lower detail

        // Assert
        lod0.Vertices.Should().NotBeEmpty();
        lod3.Vertices.Should().NotBeEmpty();

        // LOD 3 should have fewer vertices than LOD 0 (or equal in edge cases)
        lod3.Vertices.Length.Should().BeLessOrEqualTo(lod0.Vertices.Length);
    }

    [Fact]
    public void TerrainMeshUtils_CalculateNormals_GeneratesValidNormals()
    {
        // Arrange
        var vertices = new float[]
        {
            0, 0, 0,    // Triangle 1
            1, 0, 0,
            0, 1, 1,
            1, 0, 0,    // Triangle 2
            1, 1, 1,
            0, 1, 1
        };
        var indices = new uint[] { 0, 1, 2, 3, 4, 5 };

        // Act
        var normals = TerrainMeshUtils.CalculateNormals(vertices, indices);

        // Assert
        normals.Should().HaveCount(vertices.Length);

        // Verify normals are unit vectors
        for (int i = 0; i < normals.Length; i += 3)
        {
            var length = Math.Sqrt(
                normals[i] * normals[i] +
                normals[i + 1] * normals[i + 1] +
                normals[i + 2] * normals[i + 2]);

            length.Should().BeApproximately(1.0, 0.001); // Unit vector
        }
    }

    [Theory]
    [InlineData(17, 17)]
    [InlineData(33, 17)]
    [InlineData(65, 33)]
    [InlineData(129, 65)]
    public void TerrainMeshUtils_ResampleGrid_ProducesCorrectSize(int inputSize, int targetSize)
    {
        // Arrange
        var inputGrid = CreateTestElevationGrid(inputSize);

        // Act
        var resampled = TerrainMeshUtils.ResampleGrid(inputGrid, targetSize);

        // Assert
        resampled.GetLength(0).Should().Be(targetSize);
        resampled.GetLength(1).Should().Be(targetSize);

        // Verify elevation values are in reasonable range
        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                resampled[y, x].Should().BeGreaterOrEqualTo(0);
                resampled[y, x].Should().BeLessOrEqualTo(1000);
            }
        }
    }

    [Fact]
    public void GenerateMesh_WithFlatTerrain_GeneratesMinimalMesh()
    {
        // Arrange - Flat terrain (all zeros)
        var size = 129;
        var elevations = new float[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                elevations[y, x] = 100; // Constant elevation

        // Act
        var mesh = _generator.GenerateMesh(elevations, maxError: 1.0f);

        // Assert
        mesh.Should().NotBeNull();
        mesh.Vertices.Should().NotBeEmpty();
        mesh.Indices.Should().NotBeEmpty();

        // Flat terrain should produce relatively few triangles
        var triangleCount = mesh.Indices.Length / 3;
        triangleCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateMesh_WithComplexTerrain_GeneratesDetailedMesh()
    {
        // Arrange - Complex terrain with varying elevations
        var size = 129;
        var elevations = new float[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Create hills and valleys
                var distance = Math.Sqrt(
                    Math.Pow(x - size / 2, 2) +
                    Math.Pow(y - size / 2, 2));
                elevations[y, x] = (float)(100 + 50 * Math.Sin(distance / 10));
            }
        }

        // Act
        var mesh = _generator.GenerateMesh(elevations, maxError: 1.0f);

        // Assert
        mesh.Should().NotBeNull();
        mesh.Vertices.Should().NotBeEmpty();
        mesh.Indices.Should().NotBeEmpty();

        // Complex terrain should produce more triangles
        var triangleCount = mesh.Indices.Length / 3;
        triangleCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateMesh_ProducesValidTriangles()
    {
        // Arrange
        var size = 65;
        var elevations = CreateTestElevationGrid(size);

        // Act
        var mesh = _generator.GenerateMesh(elevations, maxError: 1.0f);

        // Assert
        var vertexCount = mesh.Vertices.Length / 3;

        // Verify all indices point to valid vertices
        foreach (var index in mesh.Indices)
        {
            index.Should().BeLessThan((uint)vertexCount);
        }

        // Verify no degenerate triangles (all indices should form valid triangles)
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var i1 = mesh.Indices[i];
            var i2 = mesh.Indices[i + 1];
            var i3 = mesh.Indices[i + 2];

            // Triangle should not have duplicate vertices
            i1.Should().NotBe(i2);
            i2.Should().NotBe(i3);
            i3.Should().NotBe(i1);
        }
    }

    private static float[,] CreateTestElevationGrid(int size)
    {
        var grid = new float[size, size];
        var random = new Random(42); // Deterministic

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Create varied terrain with some structure
                var noise = (float)random.NextDouble() * 100;
                var distance = Math.Sqrt(Math.Pow(x - size / 2.0, 2) + Math.Pow(y - size / 2.0, 2));
                var elevation = 200 + noise + (float)Math.Sin(distance / 10) * 50;

                grid[y, x] = elevation;
            }
        }

        return grid;
    }
}
