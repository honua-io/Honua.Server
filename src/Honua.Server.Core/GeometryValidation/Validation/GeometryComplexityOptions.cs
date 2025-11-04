// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.GeometryValidation;

/// <summary>
/// Configuration options for geometry complexity validation to prevent DoS attacks
/// via resource exhaustion from extremely complex geometries.
/// </summary>
public sealed class GeometryComplexityOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.json.
    /// </summary>
    public const string SectionName = "GeometryComplexity";

    /// <summary>
    /// Gets or sets whether geometry complexity validation is enabled.
    /// Default: true (validation enabled for security).
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of vertices allowed per geometry.
    /// Prevents CPU exhaustion during validation, indexing, and rendering.
    /// Default: 10,000 vertices.
    /// </summary>
    public int MaxVertexCount { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of rings allowed in polygon geometries.
    /// Prevents excessive complexity from polygons with many holes.
    /// Default: 100 rings (1 exterior + 99 holes).
    /// </summary>
    public int MaxRingCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum nesting depth for geometry collections.
    /// Prevents stack overflow from deeply nested collections.
    /// Default: 3 levels deep.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of decimal places for coordinate precision.
    /// Prevents excessive precision that wastes storage and processing time.
    /// Default: 9 decimal places (~1mm precision at equator).
    /// </summary>
    public int MaxCoordinatePrecision { get; set; } = 9;

    /// <summary>
    /// Gets or sets the maximum geometry size in bytes when serialized.
    /// Prevents memory exhaustion from storing and transmitting large geometries.
    /// Default: 1,048,576 bytes (1 MB).
    /// </summary>
    public int MaxGeometrySizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the maximum total coordinate count for geometries.
    /// This is a secondary check in addition to vertex count.
    /// Default: 1,000,000 coordinates.
    /// </summary>
    public int MaxCoordinateCount { get; set; } = 1_000_000;

    /// <summary>
    /// Validates the configuration options are within reasonable bounds.
    /// </summary>
    public void Validate()
    {
        if (MaxVertexCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxVertexCount), "MaxVertexCount must be greater than 0");
        }

        if (MaxRingCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRingCount), "MaxRingCount must be greater than 0");
        }

        if (MaxNestingDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxNestingDepth), "MaxNestingDepth must be non-negative");
        }

        if (MaxCoordinatePrecision < 0 || MaxCoordinatePrecision > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCoordinatePrecision), "MaxCoordinatePrecision must be between 0 and 15");
        }

        if (MaxGeometrySizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxGeometrySizeBytes), "MaxGeometrySizeBytes must be greater than 0");
        }

        if (MaxCoordinateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCoordinateCount), "MaxCoordinateCount must be greater than 0");
        }
    }
}
