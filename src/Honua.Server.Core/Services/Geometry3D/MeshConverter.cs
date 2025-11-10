// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models.Geometry3D;

namespace Honua.Server.Core.Services.Geometry3D;

/// <summary>
/// Implementation of mesh conversion service for web preview rendering.
/// Converts TriangleMesh to SimpleMesh and glTF formats optimized for Deck.gl.
/// </summary>
public class MeshConverter : IMeshConverter
{
    /// <inheritdoc/>
    public async Task<MeshPreviewResponse> ToSimpleMeshAsync(
        TriangleMesh mesh,
        int levelOfDetail = 0,
        Guid? geometryId = null,
        string? sourceFormat = null)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (!mesh.IsValid())
        {
            throw new ArgumentException("Invalid mesh structure", nameof(mesh));
        }

        // Store original counts
        var originalVertexCount = mesh.VertexCount;
        var originalFaceCount = mesh.FaceCount;

        // Apply LOD if requested
        var processedMesh = mesh;
        if (levelOfDetail > 0)
        {
            processedMesh = await ApplyLevelOfDetailAsync(mesh, levelOfDetail);
        }

        // Calculate bounding box and center
        var bbox = processedMesh.GetBoundingBox();
        var center = bbox.Center;

        // Convert vertices to positions relative to center
        var positions = new float[processedMesh.Vertices.Length];
        for (int i = 0; i < processedMesh.Vertices.Length; i += 3)
        {
            positions[i] = processedMesh.Vertices[i] - (float)center.X;
            positions[i + 1] = processedMesh.Vertices[i + 1] - (float)center.Y;
            positions[i + 2] = processedMesh.Vertices[i + 2] - (float)center.Z;
        }

        // Generate normals if not present
        var normals = processedMesh.Normals;
        if (normals.Length == 0)
        {
            normals = GenerateNormals(processedMesh);
        }

        // Convert vertex colors if present
        byte[]? colors = null;
        if (processedMesh.Colors.Length > 0)
        {
            colors = ConvertColorsToBytes(processedMesh.Colors);
        }

        var meshData = new SimpleMeshData
        {
            Positions = positions,
            Normals = normals,
            Indices = processedMesh.Indices,
            Colors = colors,
            TexCoords = processedMesh.TexCoords.Length > 0 ? processedMesh.TexCoords : null
        };

        var response = new MeshPreviewResponse
        {
            GeometryId = geometryId ?? Guid.NewGuid(),
            Format = "simple",
            LevelOfDetail = levelOfDetail,
            BoundingBox = bbox,
            Center = GeographicPosition.FromXYZ(center.X, center.Y, center.Z),
            VertexCount = processedMesh.VertexCount,
            FaceCount = processedMesh.FaceCount,
            OriginalVertexCount = originalVertexCount,
            OriginalFaceCount = originalFaceCount,
            MeshData = meshData,
            SourceFormat = sourceFormat
        };

        // Add warnings if LOD was applied
        if (levelOfDetail > 0)
        {
            var reductionPercent = (1.0 - (double)processedMesh.VertexCount / originalVertexCount) * 100;
            response.Warnings.Add($"LOD {levelOfDetail} applied: reduced from {originalVertexCount:N0} to {processedMesh.VertexCount:N0} vertices ({reductionPercent:F1}% reduction)");
        }

        return await Task.FromResult(response);
    }

    /// <inheritdoc/>
    public async Task<MeshPreviewResponse> ToGltfJsonAsync(
        TriangleMesh mesh,
        int levelOfDetail = 0,
        Guid? geometryId = null,
        string? sourceFormat = null)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (!mesh.IsValid())
        {
            throw new ArgumentException("Invalid mesh structure", nameof(mesh));
        }

        // Store original counts
        var originalVertexCount = mesh.VertexCount;
        var originalFaceCount = mesh.FaceCount;

        // Apply LOD if requested
        var processedMesh = mesh;
        if (levelOfDetail > 0)
        {
            processedMesh = await ApplyLevelOfDetailAsync(mesh, levelOfDetail);
        }

        // Calculate bounding box and center
        var bbox = processedMesh.GetBoundingBox();
        var center = bbox.Center;

        // Build glTF JSON structure
        // This is a simplified glTF structure for proof-of-concept
        // For production, consider using a proper glTF library
        var gltfData = new
        {
            asset = new
            {
                version = "2.0",
                generator = "Honua.Server MeshConverter"
            },
            scene = 0,
            scenes = new[]
            {
                new { nodes = new[] { 0 } }
            },
            nodes = new[]
            {
                new
                {
                    mesh = 0,
                    translation = new[] { center.X, center.Y, center.Z }
                }
            },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new
                            {
                                POSITION = 0,
                                NORMAL = 1
                            },
                            indices = 2,
                            mode = 4 // TRIANGLES
                        }
                    }
                }
            },
            accessors = new object[]
            {
                // POSITION accessor
                new
                {
                    bufferView = 0,
                    componentType = 5126, // FLOAT
                    count = processedMesh.VertexCount,
                    type = "VEC3",
                    max = new[] { bbox.MaxX, bbox.MaxY, bbox.MaxZ },
                    min = new[] { bbox.MinX, bbox.MinY, bbox.MinZ }
                },
                // NORMAL accessor
                new
                {
                    bufferView = 1,
                    componentType = 5126, // FLOAT
                    count = processedMesh.VertexCount,
                    type = "VEC3"
                },
                // INDICES accessor
                new
                {
                    bufferView = 2,
                    componentType = 5125, // UNSIGNED_INT
                    count = processedMesh.Indices.Length,
                    type = "SCALAR"
                }
            },
            bufferViews = new[]
            {
                // Position buffer view
                new { buffer = 0, byteOffset = 0, byteLength = processedMesh.Vertices.Length * 4 },
                // Normal buffer view
                new { buffer = 0, byteOffset = processedMesh.Vertices.Length * 4, byteLength = processedMesh.Normals.Length * 4 },
                // Indices buffer view
                new { buffer = 0, byteOffset = (processedMesh.Vertices.Length + processedMesh.Normals.Length) * 4, byteLength = processedMesh.Indices.Length * 4 }
            },
            buffers = new[]
            {
                new
                {
                    byteLength = (processedMesh.Vertices.Length + processedMesh.Normals.Length) * 4 + processedMesh.Indices.Length * 4
                }
            }
        };

        var response = new MeshPreviewResponse
        {
            GeometryId = geometryId ?? Guid.NewGuid(),
            Format = "gltf",
            LevelOfDetail = levelOfDetail,
            BoundingBox = bbox,
            Center = GeographicPosition.FromXYZ(center.X, center.Y, center.Z),
            VertexCount = processedMesh.VertexCount,
            FaceCount = processedMesh.FaceCount,
            OriginalVertexCount = originalVertexCount,
            OriginalFaceCount = originalFaceCount,
            GltfData = gltfData,
            SourceFormat = sourceFormat,
            Warnings = new List<string> { "glTF format is experimental - binary data not included in JSON" }
        };

        if (levelOfDetail > 0)
        {
            var reductionPercent = (1.0 - (double)processedMesh.VertexCount / originalVertexCount) * 100;
            response.Warnings.Add($"LOD {levelOfDetail} applied: reduced from {originalVertexCount:N0} to {processedMesh.VertexCount:N0} vertices ({reductionPercent:F1}% reduction)");
        }

        return await Task.FromResult(response);
    }

    /// <inheritdoc/>
    public async Task<TriangleMesh> ApplyLevelOfDetailAsync(TriangleMesh mesh, int levelOfDetail)
    {
        if (levelOfDetail <= 0 || levelOfDetail > 100)
        {
            return mesh;
        }

        // Simple vertex decimation based on LOD level
        // For production, consider using proper mesh simplification algorithms
        // (e.g., quadric error metrics, edge collapse)

        var targetReduction = levelOfDetail / 100.0;
        var targetVertexCount = (int)(mesh.VertexCount * (1.0 - targetReduction));

        // Ensure minimum vertex count
        targetVertexCount = Math.Max(targetVertexCount, 12); // At least 4 triangles

        if (targetVertexCount >= mesh.VertexCount)
        {
            return mesh;
        }

        // Simple uniform sampling for proof-of-concept
        // This is NOT optimal - proper mesh simplification should preserve shape better
        var step = mesh.VertexCount / targetVertexCount;
        var newVertexCount = targetVertexCount;

        var newVertices = new float[newVertexCount * 3];
        var newNormals = mesh.Normals.Length > 0 ? new float[newVertexCount * 3] : Array.Empty<float>();
        var vertexMap = new Dictionary<int, int>();

        // Sample vertices
        int newIdx = 0;
        for (int i = 0; i < mesh.VertexCount && newIdx < newVertexCount; i += step)
        {
            vertexMap[i] = newIdx;

            newVertices[newIdx * 3] = mesh.Vertices[i * 3];
            newVertices[newIdx * 3 + 1] = mesh.Vertices[i * 3 + 1];
            newVertices[newIdx * 3 + 2] = mesh.Vertices[i * 3 + 2];

            if (newNormals.Length > 0)
            {
                newNormals[newIdx * 3] = mesh.Normals[i * 3];
                newNormals[newIdx * 3 + 1] = mesh.Normals[i * 3 + 1];
                newNormals[newIdx * 3 + 2] = mesh.Normals[i * 3 + 2];
            }

            newIdx++;
        }

        // Rebuild indices - skip triangles with unmapped vertices
        var newIndicesList = new List<int>();
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var v1 = mesh.Indices[i];
            var v2 = mesh.Indices[i + 1];
            var v3 = mesh.Indices[i + 2];

            if (vertexMap.ContainsKey(v1) && vertexMap.ContainsKey(v2) && vertexMap.ContainsKey(v3))
            {
                newIndicesList.Add(vertexMap[v1]);
                newIndicesList.Add(vertexMap[v2]);
                newIndicesList.Add(vertexMap[v3]);
            }
        }

        var simplifiedMesh = new TriangleMesh
        {
            Vertices = newVertices,
            Normals = newNormals,
            Indices = newIndicesList.ToArray(),
            TexCoords = Array.Empty<float>(), // Don't preserve texcoords in simplified mesh
            Colors = Array.Empty<float>() // Don't preserve colors in simplified mesh
        };

        return await Task.FromResult(simplifiedMesh);
    }

    /// <summary>
    /// Generates vertex normals from face normals (simple averaging)
    /// </summary>
    private float[] GenerateNormals(TriangleMesh mesh)
    {
        var normals = new float[mesh.Vertices.Length];
        var normalCounts = new int[mesh.VertexCount];

        // Calculate face normals and accumulate
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var i1 = mesh.Indices[i];
            var i2 = mesh.Indices[i + 1];
            var i3 = mesh.Indices[i + 2];

            // Get triangle vertices
            var v1x = mesh.Vertices[i1 * 3];
            var v1y = mesh.Vertices[i1 * 3 + 1];
            var v1z = mesh.Vertices[i1 * 3 + 2];

            var v2x = mesh.Vertices[i2 * 3];
            var v2y = mesh.Vertices[i2 * 3 + 1];
            var v2z = mesh.Vertices[i2 * 3 + 2];

            var v3x = mesh.Vertices[i3 * 3];
            var v3y = mesh.Vertices[i3 * 3 + 1];
            var v3z = mesh.Vertices[i3 * 3 + 2];

            // Calculate edge vectors
            var e1x = v2x - v1x;
            var e1y = v2y - v1y;
            var e1z = v2z - v1z;

            var e2x = v3x - v1x;
            var e2y = v3y - v1y;
            var e2z = v3z - v1z;

            // Calculate face normal (cross product)
            var nx = e1y * e2z - e1z * e2y;
            var ny = e1z * e2x - e1x * e2z;
            var nz = e1x * e2y - e1y * e2x;

            // Normalize
            var len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len > 0)
            {
                nx /= len;
                ny /= len;
                nz /= len;
            }

            // Accumulate to vertex normals
            normals[i1 * 3] += nx;
            normals[i1 * 3 + 1] += ny;
            normals[i1 * 3 + 2] += nz;
            normalCounts[i1]++;

            normals[i2 * 3] += nx;
            normals[i2 * 3 + 1] += ny;
            normals[i2 * 3 + 2] += nz;
            normalCounts[i2]++;

            normals[i3 * 3] += nx;
            normals[i3 * 3 + 1] += ny;
            normals[i3 * 3 + 2] += nz;
            normalCounts[i3]++;
        }

        // Average and normalize
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            if (normalCounts[i] > 0)
            {
                var nx = normals[i * 3] / normalCounts[i];
                var ny = normals[i * 3 + 1] / normalCounts[i];
                var nz = normals[i * 3 + 2] / normalCounts[i];

                var len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 0)
                {
                    normals[i * 3] = nx / len;
                    normals[i * 3 + 1] = ny / len;
                    normals[i * 3 + 2] = nz / len;
                }
            }
        }

        return normals;
    }

    /// <summary>
    /// Converts float colors [0-1] to byte colors [0-255]
    /// </summary>
    private byte[] ConvertColorsToBytes(float[] colors)
    {
        var bytes = new byte[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            bytes[i] = (byte)(Math.Clamp(colors[i], 0f, 1f) * 255);
        }
        return bytes;
    }
}
