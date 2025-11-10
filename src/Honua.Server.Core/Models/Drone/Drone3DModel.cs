namespace Honua.Server.Core.Models.Drone;

/// <summary>
/// Represents a 3D model from a drone survey
/// </summary>
public class Drone3DModel
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to parent survey
    /// </summary>
    public Guid SurveyId { get; set; }

    /// <summary>
    /// Model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Model type (OBJ, GLTF, GLB, 3DTILES)
    /// </summary>
    public string ModelType { get; set; } = "GLTF";

    /// <summary>
    /// Path to model file
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Storage URL
    /// </summary>
    public string? StorageUrl { get; set; }

    /// <summary>
    /// Bounding box as GeoJSON polygon
    /// </summary>
    public object? Bounds { get; set; }

    /// <summary>
    /// Number of vertices
    /// </summary>
    public long? VertexCount { get; set; }

    /// <summary>
    /// Number of textures
    /// </summary>
    public int? TextureCount { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for creating a 3D model
/// </summary>
public class Create3DModelDto
{
    public required Guid SurveyId { get; set; }
    public required string Name { get; set; }
    public required string ModelType { get; set; }
    public required string ModelPath { get; set; }
    public string? StorageUrl { get; set; }
    public object? Bounds { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
