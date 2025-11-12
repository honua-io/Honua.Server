// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Drone;

/// <summary>
/// Represents a drone survey mission with metadata
/// </summary>
public class DroneSurvey
{
    /// <summary>
    /// Unique identifier for the survey
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Survey name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Survey description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Date and time of the survey
    /// </summary>
    public DateTime SurveyDate { get; set; }

    /// <summary>
    /// Flight altitude in meters
    /// </summary>
    public double? FlightAltitudeM { get; set; }

    /// <summary>
    /// Ground resolution in centimeters
    /// </summary>
    public double? GroundResolutionCm { get; set; }

    /// <summary>
    /// Coverage area as GeoJSON polygon
    /// </summary>
    [JsonPropertyName("coverage_area")]
    public object? CoverageArea { get; set; }

    /// <summary>
    /// Area in square meters
    /// </summary>
    public double? AreaSqm { get; set; }

    /// <summary>
    /// Total point count in the survey
    /// </summary>
    public long PointCount { get; set; }

    /// <summary>
    /// URL to orthophoto COG file
    /// </summary>
    public string? OrthophotoUrl { get; set; }

    /// <summary>
    /// URL to DEM COG file
    /// </summary>
    public string? DemUrl { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the survey
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for creating a new drone survey
/// </summary>
public class CreateDroneSurveyDto
{
    /// <summary>
    /// Survey name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Survey description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Date and time of the survey
    /// </summary>
    public DateTime SurveyDate { get; set; }

    /// <summary>
    /// Flight altitude in meters
    /// </summary>
    public double? FlightAltitudeM { get; set; }

    /// <summary>
    /// Ground resolution in centimeters
    /// </summary>
    public double? GroundResolutionCm { get; set; }

    /// <summary>
    /// Coverage area as GeoJSON polygon
    /// </summary>
    public object? CoverageArea { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Summary information about a drone survey
/// </summary>
public class DroneSurveySummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SurveyDate { get; set; }
    public long PointCount { get; set; }
    public double? AreaSqm { get; set; }
    public bool HasOrthophoto { get; set; }
    public bool HasPointCloud { get; set; }
    public bool Has3DModel { get; set; }
}
