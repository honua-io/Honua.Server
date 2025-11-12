// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Complete elevation profile data for a route or path
/// </summary>
public class ElevationProfile
{
    /// <summary>
    /// Unique identifier for the profile
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Profile name
    /// </summary>
    public string Name { get; set; } = "Elevation Profile";

    /// <summary>
    /// Collection of elevation points along the path
    /// </summary>
    public List<ElevationPoint> Points { get; set; } = new();

    /// <summary>
    /// Total distance along the route in meters
    /// </summary>
    public double TotalDistance { get; set; }

    /// <summary>
    /// Total elevation gain in meters
    /// </summary>
    public double ElevationGain { get; set; }

    /// <summary>
    /// Total elevation loss in meters
    /// </summary>
    public double ElevationLoss { get; set; }

    /// <summary>
    /// Maximum elevation in meters
    /// </summary>
    public double MaxElevation { get; set; }

    /// <summary>
    /// Minimum elevation in meters
    /// </summary>
    public double MinElevation { get; set; }

    /// <summary>
    /// Average grade/slope as percentage
    /// </summary>
    public double AverageGrade { get; set; }

    /// <summary>
    /// Maximum grade/slope as percentage
    /// </summary>
    public double MaxGrade { get; set; }

    /// <summary>
    /// Minimum grade/slope as percentage
    /// </summary>
    public double MinGrade { get; set; }

    /// <summary>
    /// Net elevation change in meters (can be negative)
    /// </summary>
    public double NetElevationChange { get; set; }

    /// <summary>
    /// Cumulative elevation gain (sum of all positive changes)
    /// </summary>
    public double CumulativeElevationGain { get; set; }

    /// <summary>
    /// Cumulative elevation loss (sum of all negative changes)
    /// </summary>
    public double CumulativeElevationLoss { get; set; }

    /// <summary>
    /// Source of elevation data
    /// </summary>
    public ElevationSource Source { get; set; }

    /// <summary>
    /// Number of sample points along the route
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Date and time when profile was generated
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original path geometry (GeoJSON LineString)
    /// </summary>
    public object? PathGeometry { get; set; }

    /// <summary>
    /// Sections with steep grades (for highlighting)
    /// </summary>
    public List<SteepSection> SteepSections { get; set; } = new();

    /// <summary>
    /// Waypoints along the route
    /// </summary>
    public List<ElevationWaypoint> Waypoints { get; set; } = new();

    /// <summary>
    /// Time estimate for completion (optional)
    /// </summary>
    public TimeEstimate? TimeEstimate { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Individual point on the elevation profile
/// </summary>
public class ElevationPoint
{
    /// <summary>
    /// Geographic coordinates [longitude, latitude]
    /// </summary>
    public required double[] Coordinates { get; set; }

    /// <summary>
    /// Elevation at this point in meters
    /// </summary>
    public double Elevation { get; set; }

    /// <summary>
    /// Distance from start in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Grade/slope at this point as percentage
    /// </summary>
    public double Grade { get; set; }

    /// <summary>
    /// Cumulative elevation gain up to this point in meters
    /// </summary>
    public double CumulativeGain { get; set; }

    /// <summary>
    /// Cumulative elevation loss up to this point in meters
    /// </summary>
    public double CumulativeLoss { get; set; }

    /// <summary>
    /// Optional waypoint name if this is a named location
    /// </summary>
    public string? WaypointName { get; set; }

    /// <summary>
    /// Index in the point sequence
    /// </summary>
    public int Index { get; set; }
}

/// <summary>
/// Section of the route with steep grade
/// </summary>
public class SteepSection
{
    /// <summary>
    /// Starting distance in meters
    /// </summary>
    public double StartDistance { get; set; }

    /// <summary>
    /// Ending distance in meters
    /// </summary>
    public double EndDistance { get; set; }

    /// <summary>
    /// Average grade in this section as percentage
    /// </summary>
    public double AverageGrade { get; set; }

    /// <summary>
    /// Maximum grade in this section as percentage
    /// </summary>
    public double MaxGrade { get; set; }

    /// <summary>
    /// Elevation change in this section in meters
    /// </summary>
    public double ElevationChange { get; set; }

    /// <summary>
    /// Length of this section in meters
    /// </summary>
    public double Length { get; set; }

    /// <summary>
    /// Severity level: Low, Moderate, High, Extreme
    /// </summary>
    public GradeSeverity Severity { get; set; }

    /// <summary>
    /// Starting coordinates [longitude, latitude]
    /// </summary>
    public double[]? StartCoordinates { get; set; }

    /// <summary>
    /// Ending coordinates [longitude, latitude]
    /// </summary>
    public double[]? EndCoordinates { get; set; }
}

/// <summary>
/// Named waypoint along the route
/// </summary>
public class ElevationWaypoint
{
    /// <summary>
    /// Waypoint identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Waypoint name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Coordinates [longitude, latitude]
    /// </summary>
    public required double[] Coordinates { get; set; }

    /// <summary>
    /// Distance from start in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Elevation at waypoint in meters
    /// </summary>
    public double Elevation { get; set; }

    /// <summary>
    /// Waypoint type: Start, End, Summit, Valley, Junction, POI
    /// </summary>
    public ElevationWaypointType Type { get; set; }

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional icon
    /// </summary>
    public string? Icon { get; set; }
}

/// <summary>
/// Time estimate for completing the route
/// </summary>
public class TimeEstimate
{
    /// <summary>
    /// Estimated total time in minutes
    /// </summary>
    public double TotalMinutes { get; set; }

    /// <summary>
    /// Estimated moving time in minutes
    /// </summary>
    public double MovingMinutes { get; set; }

    /// <summary>
    /// Estimated break time in minutes
    /// </summary>
    public double BreakMinutes { get; set; }

    /// <summary>
    /// Activity type used for calculation
    /// </summary>
    public ActivityType ActivityType { get; set; }

    /// <summary>
    /// Average speed in km/h
    /// </summary>
    public double AverageSpeed { get; set; }

    /// <summary>
    /// Pace in minutes per km
    /// </summary>
    public double Pace { get; set; }

    /// <summary>
    /// Difficulty rating (1-5)
    /// </summary>
    public int Difficulty { get; set; }
}

/// <summary>
/// Options for generating elevation profile
/// </summary>
public class ElevationProfileOptions
{
    /// <summary>
    /// Number of points to sample along the route
    /// </summary>
    public int SamplePoints { get; set; } = 100;

    /// <summary>
    /// Elevation data source
    /// </summary>
    public ElevationSource Source { get; set; } = ElevationSource.MapLibreTerrain;

    /// <summary>
    /// API key for external elevation services
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Custom elevation service URL
    /// </summary>
    public string? CustomServiceUrl { get; set; }

    /// <summary>
    /// Measurement unit system
    /// </summary>
    public MeasurementUnitSystem Unit { get; set; } = MeasurementUnitSystem.Metric;

    /// <summary>
    /// Smoothing factor (0-1, 0=no smoothing, 1=maximum smoothing)
    /// </summary>
    public double Smoothing { get; set; } = 0.0;

    /// <summary>
    /// Grade threshold for identifying steep sections (percentage)
    /// </summary>
    public double SteepGradeThreshold { get; set; } = 10.0;

    /// <summary>
    /// Activity type for time estimation
    /// </summary>
    public ActivityType ActivityType { get; set; } = ActivityType.Hiking;

    /// <summary>
    /// Include waypoints in profile
    /// </summary>
    public bool IncludeWaypoints { get; set; } = true;

    /// <summary>
    /// Calculate time estimates
    /// </summary>
    public bool CalculateTimeEstimates { get; set; } = true;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Chart configuration for elevation profile display
/// </summary>
public class ElevationChartConfig
{
    /// <summary>
    /// Chart height in pixels
    /// </summary>
    public int Height { get; set; } = 300;

    /// <summary>
    /// Chart width (auto or specific pixels)
    /// </summary>
    public string Width { get; set; } = "100%";

    /// <summary>
    /// Show grid lines
    /// </summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Show grade coloring
    /// </summary>
    public bool ShowGradeColors { get; set; } = true;

    /// <summary>
    /// Color for flat sections
    /// </summary>
    public string FlatColor { get; set; } = "#10B981"; // green

    /// <summary>
    /// Color for moderate grade sections
    /// </summary>
    public string ModerateColor { get; set; } = "#F59E0B"; // yellow

    /// <summary>
    /// Color for steep sections
    /// </summary>
    public string SteepColor { get; set; } = "#EF4444"; // red

    /// <summary>
    /// Fill under curve
    /// </summary>
    public bool FillUnderCurve { get; set; } = true;

    /// <summary>
    /// Show waypoint markers
    /// </summary>
    public bool ShowWaypoints { get; set; } = true;

    /// <summary>
    /// Show tooltip on hover
    /// </summary>
    public bool ShowTooltip { get; set; } = true;

    /// <summary>
    /// Enable zoom and pan
    /// </summary>
    public bool EnableZoom { get; set; } = true;

    /// <summary>
    /// Chart theme: light or dark
    /// </summary>
    public string Theme { get; set; } = "light";

    /// <summary>
    /// Line thickness
    /// </summary>
    public int LineWidth { get; set; } = 2;

    /// <summary>
    /// Smooth curve rendering
    /// </summary>
    public bool SmoothCurve { get; set; } = true;
}

/// <summary>
/// Source for elevation data
/// </summary>
public enum ElevationSource
{
    /// <summary>
    /// Use MapLibre terrain layer (if available)
    /// </summary>
    MapLibreTerrain,

    /// <summary>
    /// Mapbox Terrain API (requires API key)
    /// </summary>
    MapboxAPI,

    /// <summary>
    /// Open-Elevation API (free, no key required)
    /// </summary>
    OpenElevation,

    /// <summary>
    /// USGS Elevation Point Query Service
    /// </summary>
    USGSAPI,

    /// <summary>
    /// Google Elevation API (requires API key)
    /// </summary>
    GoogleAPI,

    /// <summary>
    /// Custom elevation service
    /// </summary>
    Custom,

    /// <summary>
    /// Terrain RGB tiles
    /// </summary>
    TerrainRGB
}

/// <summary>
/// Type of waypoint
/// </summary>
public enum ElevationWaypointType
{
    Start,
    End,
    Summit,
    Valley,
    Junction,
    POI,
    Campsite,
    Water,
    Viewpoint,
    Hazard,
    Custom
}

/// <summary>
/// Activity type for time estimation
/// </summary>
public enum ActivityType
{
    Hiking,
    Running,
    Cycling,
    MountainBiking,
    Skiing,
    Walking,
    Custom
}

/// <summary>
/// Grade severity classification
/// </summary>
public enum GradeSeverity
{
    Flat,      // < 5%
    Low,       // 5-10%
    Moderate,  // 10-15%
    High,      // 15-20%
    Extreme    // > 20%
}

/// <summary>
/// Export format for elevation profile
/// </summary>
public enum ElevationExportFormat
{
    CSV,
    GPX,
    JSON,
    PNG,
    PDF,
    KML
}
