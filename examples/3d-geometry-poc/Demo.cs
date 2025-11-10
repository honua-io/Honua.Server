// Copyright (c) 2025 HonuaIO
// Phase 1.2 - Complex 3D Geometry Support - Proof of Concept Demo

using Honua.Server.Core.Models.Geometry3D;
using Honua.Server.Core.Services.Geometry3D;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.GeometryPoc;

/// <summary>
/// Demonstrates the 3D geometry import/export functionality
/// </summary>
public class Geometry3DDemo
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Honua.Server - 3D Geometry Support Demo ===\n");

        var logger = NullLogger<Geometry3DService>.Instance;
        var geometryService = new Geometry3DService(logger);

        // Demo 1: Import OBJ file
        Console.WriteLine("Demo 1: Importing cube.obj...");
        var cubeFile = Path.Combine(Directory.GetCurrentDirectory(), "cube.obj");

        if (!File.Exists(cubeFile))
        {
            Console.WriteLine($"Error: cube.obj not found at {cubeFile}");
            Console.WriteLine("Please run this demo from the examples/3d-geometry-poc directory");
            return;
        }

        using var fileStream = File.OpenRead(cubeFile);
        var uploadRequest = new UploadGeometry3DRequest
        {
            Metadata = new Dictionary<string, object>
            {
                { "name", "Test Cube" },
                { "description", "Simple cube for POC testing" }
            }
        };

        var uploadResponse = await geometryService.ImportGeometryAsync(
            fileStream,
            "cube.obj",
            uploadRequest);

        if (!uploadResponse.Success)
        {
            Console.WriteLine($"Import failed: {uploadResponse.ErrorMessage}");
            return;
        }

        Console.WriteLine($"✓ Import successful!");
        Console.WriteLine($"  Geometry ID: {uploadResponse.GeometryId}");
        Console.WriteLine($"  Type: {uploadResponse.Type}");
        Console.WriteLine($"  Vertices: {uploadResponse.VertexCount}");
        Console.WriteLine($"  Faces: {uploadResponse.FaceCount}");
        Console.WriteLine($"  Bounding Box:");
        Console.WriteLine($"    Min: ({uploadResponse.BoundingBox.MinX}, {uploadResponse.BoundingBox.MinY}, {uploadResponse.BoundingBox.MinZ})");
        Console.WriteLine($"    Max: ({uploadResponse.BoundingBox.MaxX}, {uploadResponse.BoundingBox.MaxY}, {uploadResponse.BoundingBox.MaxZ})");
        Console.WriteLine($"    Size: {uploadResponse.BoundingBox.Width} x {uploadResponse.BoundingBox.Height} x {uploadResponse.BoundingBox.Depth}");

        if (uploadResponse.Warnings.Count > 0)
        {
            Console.WriteLine($"  Warnings: {string.Join(", ", uploadResponse.Warnings)}");
        }

        var geometryId = uploadResponse.GeometryId;

        // Demo 2: Retrieve geometry metadata
        Console.WriteLine("\nDemo 2: Retrieving geometry metadata...");
        var geometry = await geometryService.GetGeometryAsync(geometryId, includeMeshData: false);

        if (geometry != null)
        {
            Console.WriteLine($"✓ Retrieved geometry {geometry.Id}");
            Console.WriteLine($"  Source Format: {geometry.SourceFormat}");
            Console.WriteLine($"  Size: {geometry.SizeBytes} bytes");
            Console.WriteLine($"  Checksum: {geometry.Checksum}");
            Console.WriteLine($"  Created: {geometry.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Metadata:");
            foreach (var (key, value) in geometry.Metadata)
            {
                Console.WriteLine($"    {key}: {value}");
            }
        }

        // Demo 3: Retrieve with mesh data
        Console.WriteLine("\nDemo 3: Retrieving full mesh data...");
        var geometryWithMesh = await geometryService.GetGeometryAsync(geometryId, includeMeshData: true);

        if (geometryWithMesh?.Mesh != null)
        {
            var mesh = geometryWithMesh.Mesh;
            Console.WriteLine($"✓ Retrieved mesh data");
            Console.WriteLine($"  Vertices: {mesh.VertexCount} ({mesh.Vertices.Length} floats)");
            Console.WriteLine($"  Indices: {mesh.Indices.Length} ({mesh.FaceCount} triangles)");
            Console.WriteLine($"  Normals: {(mesh.Normals.Length > 0 ? "Yes" : "No")}");
            Console.WriteLine($"  Tex Coords: {(mesh.TexCoords.Length > 0 ? "Yes" : "No")}");
            Console.WriteLine($"  Valid: {mesh.IsValid()}");

            // Print first few vertices
            Console.WriteLine($"\n  First 3 vertices:");
            for (int i = 0; i < Math.Min(3, mesh.VertexCount); i++)
            {
                int idx = i * 3;
                Console.WriteLine($"    v{i}: ({mesh.Vertices[idx]}, {mesh.Vertices[idx + 1]}, {mesh.Vertices[idx + 2]})");
            }
        }

        // Demo 4: Export to different formats
        Console.WriteLine("\nDemo 4: Exporting to different formats...");

        var exportFormats = new[] { "obj", "stl", "ply" };

        foreach (var format in exportFormats)
        {
            try
            {
                var exportOptions = new ExportGeometry3DOptions
                {
                    Format = format,
                    BinaryFormat = format == "stl" // Use binary for STL
                };

                using var exportStream = await geometryService.ExportGeometryAsync(
                    geometryId,
                    exportOptions);

                var outputFile = $"cube_exported.{format}";
                using var outputFileStream = File.Create(outputFile);
                await exportStream.CopyToAsync(outputFileStream);

                var fileInfo = new FileInfo(outputFile);
                Console.WriteLine($"✓ Exported to {format.ToUpper()}: {outputFile} ({fileInfo.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to export {format}: {ex.Message}");
            }
        }

        // Demo 5: Bounding box search
        Console.WriteLine("\nDemo 5: Spatial search by bounding box...");
        var searchBox = new BoundingBox3D(-5, -5, -5, 5, 5, 5);
        var foundGeometries = await geometryService.FindGeometriesByBoundingBoxAsync(searchBox);

        Console.WriteLine($"✓ Found {foundGeometries.Count()} geometries in bounding box");
        foreach (var g in foundGeometries)
        {
            Console.WriteLine($"  - {g.Id}: {g.VertexCount} vertices, {g.FaceCount} faces");
        }

        // Demo 6: Update metadata
        Console.WriteLine("\nDemo 6: Updating metadata...");
        var newMetadata = new Dictionary<string, object>
        {
            { "name", "Test Cube - Updated" },
            { "description", "Simple cube for POC testing" },
            { "modified", DateTime.UtcNow },
            { "tags", new[] { "test", "cube", "poc" } }
        };

        await geometryService.UpdateGeometryMetadataAsync(geometryId, newMetadata);
        Console.WriteLine($"✓ Metadata updated");

        var updatedGeometry = await geometryService.GetGeometryAsync(geometryId);
        Console.WriteLine($"  New metadata:");
        foreach (var (key, value) in updatedGeometry!.Metadata)
        {
            Console.WriteLine($"    {key}: {value}");
        }

        // Demo 7: Delete geometry
        Console.WriteLine("\nDemo 7: Deleting geometry...");
        await geometryService.DeleteGeometryAsync(geometryId);
        Console.WriteLine($"✓ Geometry deleted");

        var deletedGeometry = await geometryService.GetGeometryAsync(geometryId);
        Console.WriteLine($"  Verification: {(deletedGeometry == null ? "Not found (correct)" : "Still exists (error)")}");

        Console.WriteLine("\n=== Demo Complete ===");
    }
}
