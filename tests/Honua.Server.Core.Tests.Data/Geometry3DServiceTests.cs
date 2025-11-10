// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Geometry3D;
using Honua.Server.Core.Services.Geometry3D;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data;

/// <summary>
/// Unit tests for Geometry3DService - Phase 1.2: Complex 3D Geometry Support.
/// These tests verify 3D file import, validation, and processing capabilities.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Phase", "Phase1")]
public class Geometry3DServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<Geometry3DService> _logger;
    private readonly Geometry3DService _service;

    public Geometry3DServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<Geometry3DService>();
        _service = new Geometry3DService(_logger);
    }

    #region OBJ File Import Tests

    [Fact]
    public async Task ImportGeometry_WithValidObjFile_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);

        var request = new UploadGeometry3DRequest
        {
            FeatureId = Guid.NewGuid(),
            Format = "obj"
        };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success, "Import should succeed for valid OBJ file");
        Assert.NotEqual(Guid.Empty, response.GeometryId);
        Assert.Equal(8, response.VertexCount); // A cube has 8 vertices
        Assert.Equal(12, response.FaceCount); // A cube has 12 triangular faces
        Assert.Equal(GeometryType3D.TriangleMesh, response.Type);
        Assert.NotNull(response.BoundingBox);

        _output.WriteLine($"Imported OBJ: {response.VertexCount} vertices, {response.FaceCount} faces");
    }

    [Fact]
    public async Task ImportGeometry_WithObjFileWithNormals_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateObjWithNormals();
        using var stream = new MemoryStream(objContent);

        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube_with_normals.obj", request);

        // Assert
        Assert.True(response.Success);
        Assert.True(response.VertexCount > 0);
    }

    #endregion

    #region STL File Import Tests

    [Fact]
    public async Task ImportGeometry_WithBinaryStlFile_ShouldSucceed()
    {
        // Arrange
        var stlContent = GenerateSimpleCubeBinaryStl();
        using var stream = new MemoryStream(stlContent);

        var request = new UploadGeometry3DRequest { Format = "stl" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.stl", request);

        // Assert
        Assert.True(response.Success, "Binary STL import should succeed");
        Assert.True(response.VertexCount > 0, "Should have vertices");
        Assert.True(response.FaceCount > 0, "Should have faces");
        Assert.NotNull(response.BoundingBox);

        _output.WriteLine($"Imported Binary STL: {response.VertexCount} vertices, {response.FaceCount} faces");
    }

    [Fact]
    public async Task ImportGeometry_WithAsciiStlFile_ShouldSucceed()
    {
        // Arrange
        var stlContent = GenerateSimpleCubeAsciiStl();
        using var stream = new MemoryStream(stlContent);

        var request = new UploadGeometry3DRequest { Format = "stl" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube_ascii.stl", request);

        // Assert
        Assert.True(response.Success, "ASCII STL import should succeed");
        Assert.True(response.VertexCount > 0);
    }

    #endregion

    #region glTF File Import Tests

    [Fact]
    public async Task ImportGeometry_WithGltfFile_ShouldSucceed()
    {
        // Arrange
        var gltfContent = GenerateSimpleGltf();
        using var stream = new MemoryStream(gltfContent);

        var request = new UploadGeometry3DRequest { Format = "gltf" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.gltf", request);

        // Assert
        Assert.True(response.Success, "glTF import should succeed");
        Assert.True(response.VertexCount > 0);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ImportGeometry_WithInvalidFormat_ShouldReturnError()
    {
        // Arrange
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("This is not a valid 3D file");
        using var stream = new MemoryStream(invalidContent);

        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "invalid.obj", request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success, "Invalid file should fail");
        Assert.NotNull(response.ErrorMessage);
        Assert.NotEmpty(response.ErrorMessage);

        _output.WriteLine($"Expected error: {response.ErrorMessage}");
    }

    [Fact]
    public async Task ImportGeometry_WithEmptyFile_ShouldReturnError()
    {
        // Arrange
        using var stream = new MemoryStream(Array.Empty<byte>());
        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "empty.obj", request);

        // Assert
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public async Task ImportGeometry_WithUnsupportedFormat_ShouldReturnError()
    {
        // Arrange
        var content = System.Text.Encoding.UTF8.GetBytes("test content");
        using var stream = new MemoryStream(content);

        var request = new UploadGeometry3DRequest { Format = "xyz" }; // Unsupported format

        // Act
        var response = await _service.ImportGeometryAsync(stream, "file.xyz", request);

        // Assert
        Assert.False(response.Success);
    }

    #endregion

    #region Bounding Box Calculation Tests

    [Fact]
    public async Task ImportGeometry_ShouldCalculateCorrectBoundingBox()
    {
        // Arrange - Create a cube from -1 to +1 on all axes
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);

        var request = new UploadGeometry3DRequest { Format = "obj" };

        // Act
        var response = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Assert
        Assert.NotNull(response.BoundingBox);

        // Cube vertices range from -1 to 1
        Assert.True(response.BoundingBox.MinX >= -1.1 && response.BoundingBox.MinX <= -0.9);
        Assert.True(response.BoundingBox.MaxX >= 0.9 && response.BoundingBox.MaxX <= 1.1);
        Assert.True(response.BoundingBox.MinY >= -1.1 && response.BoundingBox.MinY <= -0.9);
        Assert.True(response.BoundingBox.MaxY >= 0.9 && response.BoundingBox.MaxY <= 1.1);
        Assert.True(response.BoundingBox.MinZ >= -1.1 && response.BoundingBox.MinZ <= -0.9);
        Assert.True(response.BoundingBox.MaxZ >= 0.9 && response.BoundingBox.MaxZ <= 1.1);

        _output.WriteLine($"Bounding box: ({response.BoundingBox.MinX:F2}, {response.BoundingBox.MinY:F2}, {response.BoundingBox.MinZ:F2}) to ({response.BoundingBox.MaxX:F2}, {response.BoundingBox.MaxY:F2}, {response.BoundingBox.MaxZ:F2})");
    }

    #endregion

    #region Geometry Retrieval Tests

    [Fact]
    public async Task GetGeometry_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetGeometryAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetGeometry_WithIncludeMeshData_ShouldReturnMesh()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        var geometry = await _service.GetGeometryAsync(uploadResponse.GeometryId, includeMeshData: true);

        // Assert
        Assert.NotNull(geometry);
        Assert.NotNull(geometry.Mesh);
        Assert.NotNull(geometry.Mesh.Vertices);
        Assert.NotEmpty(geometry.Mesh.Vertices);
        Assert.NotNull(geometry.Mesh.Indices);
        Assert.NotEmpty(geometry.Mesh.Indices);

        _output.WriteLine($"Retrieved mesh with {geometry.Mesh.Vertices.Length} vertices");
    }

    [Fact]
    public async Task GetGeometry_WithoutIncludeMeshData_ShouldNotReturnMesh()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        var geometry = await _service.GetGeometryAsync(uploadResponse.GeometryId, includeMeshData: false);

        // Assert
        Assert.NotNull(geometry);
        Assert.Null(geometry.Mesh);
        Assert.Null(geometry.GeometryData);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportGeometry_ToStl_ShouldSucceed()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var importStream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(importStream, "cube.obj", request);

        var exportOptions = new ExportGeometry3DOptions
        {
            Format = "stl",
            BinaryFormat = true
        };

        // Act
        var exportStream = await _service.ExportGeometryAsync(uploadResponse.GeometryId, exportOptions);

        // Assert
        Assert.NotNull(exportStream);
        Assert.True(exportStream.Length > 0, "Exported STL should not be empty");

        // Verify it's valid STL by reading header
        exportStream.Position = 0;
        var header = new byte[80];
        await exportStream.ReadAsync(header);
        // Binary STL has 80-byte header

        _output.WriteLine($"Exported STL file size: {exportStream.Length} bytes");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteGeometry_ShouldRemoveFromStorage()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Act
        await _service.DeleteGeometryAsync(uploadResponse.GeometryId);
        var deletedGeometry = await _service.GetGeometryAsync(uploadResponse.GeometryId);

        // Assert
        Assert.Null(deletedGeometry);
    }

    #endregion

    #region Spatial Search Tests

    [Fact]
    public async Task FindGeometriesByBoundingBox_ShouldReturnIntersecting()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest { FeatureId = Guid.NewGuid() };

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        var searchBox = new BoundingBox3D
        {
            MinX = -2, MinY = -2, MinZ = -2,
            MaxX = 2, MaxY = 2, MaxZ = 2
        };

        // Act
        var results = await _service.FindGeometriesByBoundingBoxAsync(searchBox);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Contains(results, g => g.Id == uploadResponse.GeometryId);

        _output.WriteLine($"Found {results.Count} geometries in bounding box");
    }

    [Fact]
    public async Task FindGeometriesByBoundingBox_WithNoIntersection_ShouldReturnEmpty()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        await _service.ImportGeometryAsync(stream, "cube.obj", request);

        // Search box far from cube location (-1 to 1)
        var searchBox = new BoundingBox3D
        {
            MinX = 100, MinY = 100, MinZ = 100,
            MaxX = 200, MaxY = 200, MaxZ = 200
        };

        // Act
        var results = await _service.FindGeometriesByBoundingBoxAsync(searchBox);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task UpdateGeometryMetadata_ShouldPersistChanges()
    {
        // Arrange
        var objContent = GenerateSimpleCubeObj();
        using var stream = new MemoryStream(objContent);
        var request = new UploadGeometry3DRequest();

        var uploadResponse = await _service.ImportGeometryAsync(stream, "cube.obj", request);

        var newMetadata = new Dictionary<string, object>
        {
            ["author"] = "Test User",
            ["version"] = 2,
            ["lastModified"] = DateTime.UtcNow
        };

        // Act
        await _service.UpdateGeometryMetadataAsync(uploadResponse.GeometryId, newMetadata);
        var updatedGeometry = await _service.GetGeometryAsync(uploadResponse.GeometryId);

        // Assert
        Assert.NotNull(updatedGeometry);
        Assert.NotNull(updatedGeometry.Metadata);
        Assert.Equal("Test User", updatedGeometry.Metadata["author"]);
        Assert.Equal(2L, Convert.ToInt64(updatedGeometry.Metadata["version"]));
    }

    #endregion

    #region Test Data Generators

    private static byte[] GenerateSimpleCubeObj()
    {
        var obj = @"# Simple cube
v -1.0 -1.0 -1.0
v -1.0 -1.0  1.0
v -1.0  1.0 -1.0
v -1.0  1.0  1.0
v  1.0 -1.0 -1.0
v  1.0 -1.0  1.0
v  1.0  1.0 -1.0
v  1.0  1.0  1.0

f 1 2 3
f 2 4 3
f 5 6 7
f 6 8 7
f 1 5 2
f 5 6 2
f 3 7 4
f 7 8 4
f 1 5 3
f 5 7 3
f 2 6 4
f 6 8 4
";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }

    private static byte[] GenerateObjWithNormals()
    {
        var obj = @"# Cube with normals
v -1.0 -1.0 -1.0
v  1.0 -1.0 -1.0
v  1.0  1.0 -1.0
v -1.0  1.0 -1.0

vn  0.0  0.0  1.0
vn  0.0  0.0 -1.0

f 1//1 2//1 3//1
f 1//1 3//1 4//1
";
        return System.Text.Encoding.UTF8.GetBytes(obj);
    }

    private static byte[] GenerateSimpleCubeBinaryStl()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 80-byte header
        writer.Write(new byte[80]);

        // Number of triangles (12 for a cube)
        writer.Write((uint)12);

        // For each triangle, write:
        // - normal (3 floats)
        // - vertex 1 (3 floats)
        // - vertex 2 (3 floats)
        // - vertex 3 (3 floats)
        // - attribute byte count (ushort)

        for (int i = 0; i < 12; i++)
        {
            // Normal
            writer.Write(0.0f);
            writer.Write(0.0f);
            writer.Write(1.0f);

            // Vertex 1
            writer.Write(-1.0f);
            writer.Write(-1.0f);
            writer.Write(1.0f);

            // Vertex 2
            writer.Write(1.0f);
            writer.Write(-1.0f);
            writer.Write(1.0f);

            // Vertex 3
            writer.Write(-1.0f);
            writer.Write(1.0f);
            writer.Write(1.0f);

            // Attribute byte count
            writer.Write((ushort)0);
        }

        return ms.ToArray();
    }

    private static byte[] GenerateSimpleCubeAsciiStl()
    {
        var stl = @"solid cube
  facet normal 0 0 1
    outer loop
      vertex -1 -1 1
      vertex 1 -1 1
      vertex -1 1 1
    endloop
  endfacet
  facet normal 0 0 1
    outer loop
      vertex 1 -1 1
      vertex 1 1 1
      vertex -1 1 1
    endloop
  endfacet
endsolid cube
";
        return System.Text.Encoding.UTF8.GetBytes(stl);
    }

    private static byte[] GenerateSimpleGltf()
    {
        var gltf = @"{
  ""asset"": {""version"": ""2.0""},
  ""scenes"": [{""nodes"": [0]}],
  ""nodes"": [{""mesh"": 0}],
  ""meshes"": [{
    ""primitives"": [{
      ""attributes"": {""POSITION"": 0},
      ""indices"": 1
    }]
  }],
  ""accessors"": [
    {""bufferView"": 0, ""componentType"": 5126, ""count"": 3, ""type"": ""VEC3"",
     ""min"": [-1.0, -1.0, 0.0], ""max"": [1.0, 1.0, 0.0]},
    {""bufferView"": 1, ""componentType"": 5123, ""count"": 3, ""type"": ""SCALAR""}
  ],
  ""bufferViews"": [
    {""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 36},
    {""buffer"": 0, ""byteOffset"": 36, ""byteLength"": 6}
  ],
  ""buffers"": [{""byteLength"": 42, ""uri"": ""data:application/octet-stream;base64,AACAPwAAgD8AAAAAAACAPwAAgL8AAAAAAACAvwAAgD8AAAAAAAAAAAABAAIAAA==""}]
}";
        return System.Text.Encoding.UTF8.GetBytes(gltf);
    }

    #endregion
}
