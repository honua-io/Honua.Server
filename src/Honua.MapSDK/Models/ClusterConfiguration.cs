// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration for point clustering visualization
/// </summary>
public class ClusterConfiguration
{
    /// <summary>
    /// Cluster radius in pixels (default 50)
    /// Points within this radius will be clustered together
    /// </summary>
    public int ClusterRadius { get; set; } = 50;

    /// <summary>
    /// Minimum zoom level to show clusters
    /// Default: 0
    /// </summary>
    public int MinZoom { get; set; } = 0;

    /// <summary>
    /// Maximum zoom level to show clusters
    /// Default: 16
    /// </summary>
    public int MaxZoom { get; set; } = 16;

    /// <summary>
    /// Maximum zoom level to cluster points on
    /// At this zoom level and above, clusters will stop forming
    /// Default: 16
    /// </summary>
    public int ClusterMaxZoom { get; set; } = 16;

    /// <summary>
    /// Custom cluster property aggregations
    /// Key: property name, Value: aggregation function (sum, max, min, mean)
    /// </summary>
    public Dictionary<string, string>? ClusterProperties { get; set; }

    /// <summary>
    /// Enable spider-fy on cluster click
    /// Expands cluster to show individual points in a spiral pattern
    /// </summary>
    public bool EnableSpiderfy { get; set; } = true;

    /// <summary>
    /// Show cluster extent on hover
    /// Displays the bounding box of all points in the cluster
    /// </summary>
    public bool ShowClusterExtent { get; set; } = true;

    /// <summary>
    /// Animate cluster transitions on zoom
    /// </summary>
    public bool AnimateTransitions { get; set; } = true;

    /// <summary>
    /// Zoom into cluster on click
    /// </summary>
    public bool ZoomOnClick { get; set; } = true;

    /// <summary>
    /// Cluster styling configuration
    /// </summary>
    public ClusterStyle Style { get; set; } = new();

    /// <summary>
    /// Layer ID for the cluster layer (auto-generated if not specified)
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Layer ID for unclustered points (auto-generated if not specified)
    /// </summary>
    public string? UnclusteredLayerId { get; set; }
}

/// <summary>
/// Styling configuration for clusters
/// </summary>
public class ClusterStyle
{
    /// <summary>
    /// Color scale for clusters based on point count
    /// Key: point count threshold, Value: color
    /// </summary>
    public Dictionary<int, string> ColorScale { get; set; } = new()
    {
        { 10, "#51bbd6" },
        { 50, "#f1f075" },
        { 100, "#f28cb1" },
        { 500, "#ff6b6b" }
    };

    /// <summary>
    /// Size scale for clusters based on point count
    /// Key: point count threshold, Value: radius in pixels
    /// </summary>
    public Dictionary<int, int> SizeScale { get; set; } = new()
    {
        { 10, 20 },
        { 50, 30 },
        { 100, 40 },
        { 500, 50 }
    };

    /// <summary>
    /// Show count label on clusters
    /// </summary>
    public bool ShowCountLabel { get; set; } = true;

    /// <summary>
    /// Label font size in pixels
    /// </summary>
    public int LabelFontSize { get; set; } = 12;

    /// <summary>
    /// Label text color
    /// </summary>
    public string LabelColor { get; set; } = "#ffffff";

    /// <summary>
    /// Cluster stroke color
    /// </summary>
    public string StrokeColor { get; set; } = "#ffffff";

    /// <summary>
    /// Cluster stroke width in pixels
    /// </summary>
    public int StrokeWidth { get; set; } = 2;

    /// <summary>
    /// Cluster opacity (0-1)
    /// </summary>
    public double Opacity { get; set; } = 0.8;

    /// <summary>
    /// Unclustered point color
    /// </summary>
    public string UnclusteredColor { get; set; } = "#11b4da";

    /// <summary>
    /// Unclustered point radius in pixels
    /// </summary>
    public int UnclusteredRadius { get; set; } = 6;

    /// <summary>
    /// Unclustered point stroke color
    /// </summary>
    public string UnclusteredStrokeColor { get; set; } = "#ffffff";

    /// <summary>
    /// Unclustered point stroke width in pixels
    /// </summary>
    public int UnclusteredStrokeWidth { get; set; } = 1;
}

/// <summary>
/// Cluster statistics and metadata
/// </summary>
public class ClusterStatistics
{
    /// <summary>
    /// Total number of points in the dataset
    /// </summary>
    public int TotalPoints { get; set; }

    /// <summary>
    /// Number of clusters at current zoom level
    /// </summary>
    public int ClusterCount { get; set; }

    /// <summary>
    /// Number of unclustered points at current zoom level
    /// </summary>
    public int UnclusteredCount { get; set; }

    /// <summary>
    /// Current zoom level
    /// </summary>
    public double ZoomLevel { get; set; }

    /// <summary>
    /// Largest cluster point count
    /// </summary>
    public int MaxClusterSize { get; set; }

    /// <summary>
    /// Average cluster size
    /// </summary>
    public double AverageClusterSize { get; set; }

    /// <summary>
    /// Bounds of all points [west, south, east, north]
    /// </summary>
    public double[]? Bounds { get; set; }
}

/// <summary>
/// Information about a specific cluster
/// </summary>
public class ClusterInfo
{
    /// <summary>
    /// Cluster ID
    /// </summary>
    public required int ClusterId { get; init; }

    /// <summary>
    /// Number of points in the cluster
    /// </summary>
    public required int PointCount { get; init; }

    /// <summary>
    /// Center coordinates [longitude, latitude]
    /// </summary>
    public required double[] Coordinates { get; init; }

    /// <summary>
    /// Bounds of the cluster [west, south, east, north]
    /// </summary>
    public double[]? Bounds { get; init; }

    /// <summary>
    /// Aggregated properties for the cluster
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>
    /// Expansion zoom (zoom level to see cluster children)
    /// </summary>
    public int ExpansionZoom { get; init; }
}

/// <summary>
/// Event args for cluster clicked event
/// </summary>
public class ClusterClickedEventArgs
{
    /// <summary>
    /// Cluster information
    /// </summary>
    public required ClusterInfo Cluster { get; init; }

    /// <summary>
    /// Map ID
    /// </summary>
    public required string MapId { get; init; }

    /// <summary>
    /// Component ID
    /// </summary>
    public required string ComponentId { get; init; }
}

/// <summary>
/// Event args for cluster updated event
/// </summary>
public class ClusterUpdatedEventArgs
{
    /// <summary>
    /// Cluster layer ID
    /// </summary>
    public required string LayerId { get; init; }

    /// <summary>
    /// Updated configuration
    /// </summary>
    public required ClusterConfiguration Configuration { get; init; }

    /// <summary>
    /// Statistics about the clusters
    /// </summary>
    public ClusterStatistics? Statistics { get; init; }
}

/// <summary>
/// Spider-fy configuration
/// </summary>
public class SpiderfyConfiguration
{
    /// <summary>
    /// Enable spider-fy feature
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Spiral length factor (controls distance between points)
    /// </summary>
    public double SpiralLengthFactor { get; set; } = 50.0;

    /// <summary>
    /// Maximum number of points to spider-fy
    /// Above this number, zoom in instead
    /// </summary>
    public int MaxPoints { get; set; } = 50;

    /// <summary>
    /// Animation duration in milliseconds
    /// </summary>
    public int AnimationDuration { get; set; } = 300;

    /// <summary>
    /// Show connecting lines from cluster center to spiderfied points
    /// </summary>
    public bool ShowLegs { get; set; } = true;

    /// <summary>
    /// Leg line color
    /// </summary>
    public string LegColor { get; set; } = "#888888";

    /// <summary>
    /// Leg line width in pixels
    /// </summary>
    public int LegWidth { get; set; } = 1;
}
