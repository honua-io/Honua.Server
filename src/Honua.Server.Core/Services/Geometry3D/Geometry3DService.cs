using System.Security.Cryptography;
using Assimp;
using Honua.Server.Core.Models.Geometry3D;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Services.Geometry3D;

/// <summary>
/// Implementation of IGeometry3DService using AssimpNet for mesh import/export.
/// Supports OBJ, STL, glTF, FBX, and other 3D file formats.
/// </summary>
public class Geometry3DService : IGeometry3DService
{
    private readonly ILogger<Geometry3DService> _logger;
    private readonly AssimpContext _assimpContext;

    // In-memory storage for proof-of-concept
    // In production, this would be replaced with database + blob storage
    private readonly Dictionary<Guid, ComplexGeometry3D> _geometries = new();

    public Geometry3DService(ILogger<Geometry3DService> logger)
    {
        _logger = logger;
        _assimpContext = new AssimpContext();
    }

    public async Task<UploadGeometry3DResponse> ImportGeometryAsync(
        Stream stream,
        string fileName,
        UploadGeometry3DRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = new UploadGeometry3DResponse();

        try
        {
            // Detect format from file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var format = request.Format ?? extension.TrimStart('.');

            _logger.LogInformation("Importing 3D geometry file: {FileName} (format: {Format})", fileName, format);

            // Copy stream to memory (Assimp requires seekable stream)
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var fileData = memoryStream.ToArray();

            // Import using AssimpNet
            Scene scene;
            try
            {
                scene = _assimpContext.ImportFileFromStream(
                    new MemoryStream(fileData),
                    PostProcessSteps.Triangulate |
                    PostProcessSteps.GenerateNormals |
                    PostProcessSteps.JoinIdenticalVertices |
                    PostProcessSteps.OptimizeMeshes,
                    extension);
            }
            catch (AssimpException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Failed to import file: {ex.Message}";
                _logger.LogError(ex, "AssimpNet import failed for {FileName}", fileName);
                return response;
            }

            if (scene == null || !scene.HasMeshes)
            {
                response.Success = false;
                response.ErrorMessage = "No meshes found in the imported file";
                return response;
            }

            // Convert Assimp scene to our TriangleMesh format
            var triangleMesh = ConvertAssimpSceneToMesh(scene);

            if (!triangleMesh.IsValid())
            {
                response.Success = false;
                response.ErrorMessage = "Imported mesh failed validation";
                return response;
            }

            // Create ComplexGeometry3D object
            var geometry = new ComplexGeometry3D
            {
                Id = Guid.NewGuid(),
                FeatureId = request.FeatureId,
                Type = GeometryType3D.TriangleMesh,
                BoundingBox = triangleMesh.GetBoundingBox(),
                VertexCount = triangleMesh.VertexCount,
                FaceCount = triangleMesh.FaceCount,
                Metadata = request.Metadata ?? new Dictionary<string, object>(),
                SourceFormat = format,
                Mesh = triangleMesh,
                GeometryData = fileData,
                Checksum = ComputeSHA256(fileData),
                SizeBytes = fileData.Length,
                CreatedAt = DateTime.UtcNow
            };

            // Add metadata about original file
            geometry.Metadata["originalFileName"] = fileName;
            geometry.Metadata["importDate"] = DateTime.UtcNow;
            geometry.Metadata["meshCount"] = scene.MeshCount;
            geometry.Metadata["materialCount"] = scene.MaterialCount;

            // Store in memory (in production: save to database + blob storage)
            _geometries[geometry.Id] = geometry;

            // Build response
            response.Success = true;
            response.GeometryId = geometry.Id;
            response.Type = geometry.Type;
            response.VertexCount = geometry.VertexCount;
            response.FaceCount = geometry.FaceCount;
            response.BoundingBox = geometry.BoundingBox;

            if (scene.MeshCount > 1)
            {
                response.Warnings.Add($"File contains {scene.MeshCount} meshes - merged into single mesh");
            }

            _logger.LogInformation(
                "Successfully imported geometry {GeometryId}: {VertexCount} vertices, {FaceCount} faces",
                geometry.Id, geometry.VertexCount, geometry.FaceCount);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error importing geometry from {FileName}", fileName);
            response.Success = false;
            response.ErrorMessage = $"Unexpected error: {ex.Message}";
            return response;
        }
    }

    public Task<ComplexGeometry3D?> GetGeometryAsync(
        Guid geometryId,
        bool includeMeshData = false,
        CancellationToken cancellationToken = default)
    {
        if (!_geometries.TryGetValue(geometryId, out var geometry))
        {
            return Task.FromResult<ComplexGeometry3D?>(null);
        }

        // Clone to avoid exposing internal state
        var result = new ComplexGeometry3D
        {
            Id = geometry.Id,
            FeatureId = geometry.FeatureId,
            Type = geometry.Type,
            BoundingBox = geometry.BoundingBox,
            VertexCount = geometry.VertexCount,
            FaceCount = geometry.FaceCount,
            Metadata = new Dictionary<string, object>(geometry.Metadata),
            SourceFormat = geometry.SourceFormat,
            StoragePath = geometry.StoragePath,
            Checksum = geometry.Checksum,
            SizeBytes = geometry.SizeBytes,
            CreatedAt = geometry.CreatedAt
        };

        if (includeMeshData)
        {
            result.Mesh = geometry.Mesh;
            result.GeometryData = geometry.GeometryData;
        }

        return Task.FromResult<ComplexGeometry3D?>(result);
    }

    public async Task<Stream> ExportGeometryAsync(
        Guid geometryId,
        ExportGeometry3DOptions options,
        CancellationToken cancellationToken = default)
    {
        var geometry = await GetGeometryAsync(geometryId, includeMeshData: true, cancellationToken);

        if (geometry == null)
        {
            throw new InvalidOperationException($"Geometry {geometryId} not found");
        }

        if (geometry.Mesh == null)
        {
            throw new InvalidOperationException($"Geometry {geometryId} has no mesh data");
        }

        _logger.LogInformation("Exporting geometry {GeometryId} to format {Format}", geometryId, options.Format);

        // Convert our mesh back to Assimp format
        var scene = ConvertMeshToAssimpScene(geometry.Mesh);

        // Determine export format
        var exportFormatId = GetExportFormatId(options.Format);

        // Export to temporary file first (AssimpNet requires a file path)
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"geometry_{geometryId}.{exportFormatId}");

        try
        {
            var exportSuccess = _assimpContext.ExportFile(
                scene,
                tempFilePath,
                exportFormatId,
                options.BinaryFormat ? PostProcessSteps.None : PostProcessSteps.None);

            if (!exportSuccess)
            {
                throw new InvalidOperationException($"Failed to export geometry to {options.Format}");
            }

            // Read the exported file into a memory stream
            var outputStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(tempFilePath, CancellationToken.None));
            return outputStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export geometry {GeometryId} to {Format}", geometryId, options.Format);
            throw;
        }
        finally
        {
            // Clean up temporary file
            try
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary export file: {FilePath}", tempFilePath);
            }
        }
    }

    public Task DeleteGeometryAsync(Guid geometryId, CancellationToken cancellationToken = default)
    {
        _geometries.Remove(geometryId);
        _logger.LogInformation("Deleted geometry {GeometryId}", geometryId);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ComplexGeometry3D>> GetGeometriesForFeatureAsync(
        Guid featureId,
        CancellationToken cancellationToken = default)
    {
        var geometries = _geometries.Values
            .Where(g => g.FeatureId == featureId)
            .ToList();

        return Task.FromResult<IEnumerable<ComplexGeometry3D>>(geometries);
    }

    public Task<IEnumerable<ComplexGeometry3D>> FindGeometriesByBoundingBoxAsync(
        BoundingBox3D bbox,
        CancellationToken cancellationToken = default)
    {
        var geometries = _geometries.Values
            .Where(g => g.BoundingBox.Intersects(bbox))
            .ToList();

        return Task.FromResult<IEnumerable<ComplexGeometry3D>>(geometries);
    }

    public Task UpdateGeometryMetadataAsync(
        Guid geometryId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        if (!_geometries.TryGetValue(geometryId, out var geometry))
        {
            throw new InvalidOperationException($"Geometry {geometryId} not found");
        }

        geometry.Metadata = metadata;
        _logger.LogInformation("Updated metadata for geometry {GeometryId}", geometryId);
        return Task.CompletedTask;
    }

    #region Private Helper Methods

    private TriangleMesh ConvertAssimpSceneToMesh(Scene scene)
    {
        var vertices = new List<float>();
        var indices = new List<int>();
        var normals = new List<float>();
        var texCoords = new List<float>();

        int vertexOffset = 0;

        // Merge all meshes in the scene
        foreach (var mesh in scene.Meshes)
        {
            // Add vertices
            foreach (var vertex in mesh.Vertices)
            {
                vertices.Add(vertex.X);
                vertices.Add(vertex.Y);
                vertices.Add(vertex.Z);
            }

            // Add normals
            if (mesh.HasNormals)
            {
                foreach (var normal in mesh.Normals)
                {
                    normals.Add(normal.X);
                    normals.Add(normal.Y);
                    normals.Add(normal.Z);
                }
            }

            // Add texture coordinates (first channel only)
            if (mesh.HasTextureCoords(0))
            {
                foreach (var texCoord in mesh.TextureCoordinateChannels[0])
                {
                    texCoords.Add(texCoord.X);
                    texCoords.Add(texCoord.Y);
                }
            }

            // Add faces (already triangulated by post-processing)
            foreach (var face in mesh.Faces)
            {
                if (face.IndexCount != 3)
                {
                    _logger.LogWarning("Non-triangle face found (indices: {Count}), skipping", face.IndexCount);
                    continue;
                }

                indices.Add(vertexOffset + face.Indices[0]);
                indices.Add(vertexOffset + face.Indices[1]);
                indices.Add(vertexOffset + face.Indices[2]);
            }

            vertexOffset += mesh.VertexCount;
        }

        return new TriangleMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            Normals = normals.ToArray(),
            TexCoords = texCoords.ToArray()
        };
    }

    private Scene ConvertMeshToAssimpScene(TriangleMesh mesh)
    {
        var scene = new Scene();
        var assimpMesh = new Mesh(PrimitiveType.Triangle);

        // Add vertices
        for (int i = 0; i < mesh.Vertices.Length; i += 3)
        {
            assimpMesh.Vertices.Add(new Vector3D(
                mesh.Vertices[i],
                mesh.Vertices[i + 1],
                mesh.Vertices[i + 2]));
        }

        // Add normals
        if (mesh.Normals.Length > 0)
        {
            for (int i = 0; i < mesh.Normals.Length; i += 3)
            {
                assimpMesh.Normals.Add(new Vector3D(
                    mesh.Normals[i],
                    mesh.Normals[i + 1],
                    mesh.Normals[i + 2]));
            }
        }

        // Add texture coordinates
        if (mesh.TexCoords.Length > 0)
        {
            var texChannel = new List<Vector3D>();
            for (int i = 0; i < mesh.TexCoords.Length; i += 2)
            {
                texChannel.Add(new Vector3D(
                    mesh.TexCoords[i],
                    mesh.TexCoords[i + 1],
                    0));
            }
            assimpMesh.TextureCoordinateChannels[0] = texChannel;
        }

        // Add faces
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var face = new Face();
            face.Indices.Add(mesh.Indices[i]);
            face.Indices.Add(mesh.Indices[i + 1]);
            face.Indices.Add(mesh.Indices[i + 2]);
            assimpMesh.Faces.Add(face);
        }

        // Create a default material
        var material = new Material();
        material.Name = "DefaultMaterial";
        scene.Materials.Add(material);
        assimpMesh.MaterialIndex = 0;

        scene.Meshes.Add(assimpMesh);

        // Create root node
        scene.RootNode = new Node("root");
        scene.RootNode.MeshIndices.Add(0);

        return scene;
    }

    private string GetExportFormatId(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "obj" => "obj",
            "stl" => "stl",
            "stlb" => "stlb", // Binary STL
            "gltf" => "gltf2",
            "glb" => "glb2",
            "ply" => "ply",
            "collada" => "collada",
            "dae" => "collada",
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    private string ComputeSHA256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}
