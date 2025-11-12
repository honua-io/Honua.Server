// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Drone;

/// <summary>
/// Represents an orthomosaic (orthophoto) from a drone survey
/// </summary>
public class DroneOrthomosaic
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
    /// Orthomosaic name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to raster file (local or relative)
    /// </summary>
    public string RasterPath { get; set; } = string.Empty;

    /// <summary>
    /// Storage URL (S3, Azure Blob, etc.)
    /// </summary>
    public string? StorageUrl { get; set; }

    /// <summary>
    /// Bounding box as GeoJSON polygon
    /// </summary>
    public object? Bounds { get; set; }

    /// <summary>
    /// Resolution in centimeters
    /// </summary>
    public double ResolutionCm { get; set; }

    /// <summary>
    /// Tile matrix set (e.g., WebMercatorQuad)
    /// </summary>
    public string TileMatrixSet { get; set; } = "WebMercatorQuad";

    /// <summary>
    /// Format (COG, GeoTIFF, etc.)
    /// </summary>
    public string Format { get; set; } = "COG";

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
/// DTO for creating an orthomosaic
/// </summary>
public class CreateOrthomosaicDto
{
    public required Guid SurveyId { get; set; }
    public required string Name { get; set; }
    public required string RasterPath { get; set; }
    public string? StorageUrl { get; set; }
    public object? Bounds { get; set; }
    public double ResolutionCm { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
