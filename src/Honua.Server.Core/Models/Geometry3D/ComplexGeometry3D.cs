// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Geometry3D;

/// <summary>
/// Represents a complex 3D geometry that extends beyond simple GeoJSON 3D coordinates.
/// Supports meshes, solids, and parametric surfaces for AEC workflows.
/// </summary>
public class ComplexGeometry3D
{
    /// <summary>
    /// Unique identifier for this geometry
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Optional reference to the feature this geometry belongs to
    /// </summary>
    public Guid? FeatureId { get; set; }

    /// <summary>
    /// Type of 3D geometry
    /// </summary>
    public GeometryType3D Type { get; set; }

    /// <summary>
    /// Bounding box for spatial indexing
    /// </summary>
    public BoundingBox3D BoundingBox { get; set; } = new();

    /// <summary>
    /// Number of vertices in the geometry
    /// </summary>
    public int VertexCount { get; set; }

    /// <summary>
    /// Number of faces/triangles in the geometry
    /// </summary>
    public int FaceCount { get; set; }

    /// <summary>
    /// Custom metadata (material properties, units, source file info, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Timestamp when geometry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original file format (obj, stl, gltf, step, etc.)
    /// </summary>
    public string? SourceFormat { get; set; }

    /// <summary>
    /// Binary geometry data (serialized mesh or solid)
    /// Not included in JSON serialization - loaded separately for performance
    /// </summary>
    [JsonIgnore]
    public byte[]? GeometryData { get; set; }

    /// <summary>
    /// For TriangleMesh types, the deserialized mesh object
    /// </summary>
    [JsonIgnore]
    public TriangleMesh? Mesh { get; set; }

    /// <summary>
    /// Storage path if geometry is stored in blob storage (S3, Azure Blob, etc.)
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// SHA256 checksum of the geometry data for integrity verification
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Size of geometry data in bytes
    /// </summary>
    public long SizeBytes { get; set; }
}

/// <summary>
/// Request model for uploading a 3D geometry file
/// </summary>
public class UploadGeometry3DRequest
{
    /// <summary>
    /// Optional feature ID to associate with
    /// </summary>
    public Guid? FeatureId { get; set; }

    /// <summary>
    /// File format (auto-detected from extension if not specified)
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Custom metadata to attach
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response model for geometry upload
/// </summary>
public class UploadGeometry3DResponse
{
    /// <summary>
    /// ID of the created geometry
    /// </summary>
    public Guid GeometryId { get; set; }

    /// <summary>
    /// Type of geometry that was imported
    /// </summary>
    public GeometryType3D Type { get; set; }

    /// <summary>
    /// Number of vertices imported
    /// </summary>
    public int VertexCount { get; set; }

    /// <summary>
    /// Number of faces imported
    /// </summary>
    public int FaceCount { get; set; }

    /// <summary>
    /// Bounding box of the imported geometry
    /// </summary>
    public BoundingBox3D BoundingBox { get; set; } = new();

    /// <summary>
    /// Any warnings encountered during import
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Whether the import was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if import failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Options for exporting geometry to a specific format
/// </summary>
public class ExportGeometry3DOptions
{
    /// <summary>
    /// Target format (obj, stl, gltf)
    /// </summary>
    public string Format { get; set; } = "obj";

    /// <summary>
    /// Whether to include normals in export
    /// </summary>
    public bool IncludeNormals { get; set; } = true;

    /// <summary>
    /// Whether to include texture coordinates
    /// </summary>
    public bool IncludeTexCoords { get; set; } = true;

    /// <summary>
    /// Whether to include vertex colors
    /// </summary>
    public bool IncludeColors { get; set; } = true;

    /// <summary>
    /// Binary format (for STL) vs ASCII
    /// </summary>
    public bool BinaryFormat { get; set; } = true;
}
