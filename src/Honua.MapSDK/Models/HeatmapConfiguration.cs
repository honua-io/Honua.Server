namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration for heatmap visualization
/// </summary>
public class HeatmapConfiguration
{
    /// <summary>
    /// Heatmap radius in pixels (blur distance)
    /// Default: 30
    /// </summary>
    public int Radius { get; set; } = 30;

    /// <summary>
    /// Heatmap intensity (0-2)
    /// Controls the brightness of the heatmap
    /// Default: 1.0
    /// </summary>
    public double Intensity { get; set; } = 1.0;

    /// <summary>
    /// Heatmap opacity (0-1)
    /// Default: 0.6
    /// </summary>
    public double Opacity { get; set; } = 0.6;

    /// <summary>
    /// Color gradient preset
    /// </summary>
    public HeatmapGradient Gradient { get; set; } = HeatmapGradient.Hot;

    /// <summary>
    /// Custom gradient color stops (density -> color)
    /// Only used when Gradient is set to Custom
    /// </summary>
    public Dictionary<double, string>? CustomGradient { get; set; }

    /// <summary>
    /// Property name to use for weighting points
    /// If null, all points have equal weight
    /// </summary>
    public string? WeightProperty { get; set; }

    /// <summary>
    /// Maximum zoom level to show heatmap
    /// Beyond this zoom, switch to individual points
    /// </summary>
    public int? MaxZoom { get; set; }

    /// <summary>
    /// Minimum zoom level to show heatmap
    /// </summary>
    public int? MinZoom { get; set; }

    /// <summary>
    /// Layer ID for the heatmap layer
    /// Auto-generated if not specified
    /// </summary>
    public string? LayerId { get; set; }
}

/// <summary>
/// Predefined color gradients for heatmaps
/// </summary>
public enum HeatmapGradient
{
    /// <summary>
    /// Yellow → Orange → Red (classic heat)
    /// </summary>
    Hot,

    /// <summary>
    /// Cyan → Blue → Navy (cool tones)
    /// </summary>
    Cool,

    /// <summary>
    /// Violet → Red (full color spectrum)
    /// </summary>
    Rainbow,

    /// <summary>
    /// Yellow → Green → Blue → Purple (perceptually uniform, colorblind-friendly)
    /// </summary>
    Viridis,

    /// <summary>
    /// Yellow → Pink → Purple → Blue (high contrast)
    /// </summary>
    Plasma,

    /// <summary>
    /// Yellow → Orange → Red → Black (dramatic)
    /// </summary>
    Inferno,

    /// <summary>
    /// User-defined custom gradient
    /// Requires CustomGradient property to be set
    /// </summary>
    Custom
}

/// <summary>
/// Heatmap style configuration for MapLibre layer
/// </summary>
public class HeatmapStyle
{
    /// <summary>
    /// Layer ID
    /// </summary>
    public required string LayerId { get; set; }

    /// <summary>
    /// Source ID
    /// </summary>
    public required string SourceId { get; set; }

    /// <summary>
    /// Paint properties for heatmap layer
    /// </summary>
    public Dictionary<string, object> Paint { get; set; } = new();
}

/// <summary>
/// Heatmap statistics and metadata
/// </summary>
public class HeatmapStatistics
{
    /// <summary>
    /// Total number of points in the heatmap
    /// </summary>
    public int PointCount { get; set; }

    /// <summary>
    /// Maximum density value
    /// </summary>
    public double MaxDensity { get; set; }

    /// <summary>
    /// Minimum density value
    /// </summary>
    public double MinDensity { get; set; }

    /// <summary>
    /// Average density value
    /// </summary>
    public double AverageDensity { get; set; }

    /// <summary>
    /// If using weighted heatmap, the sum of all weights
    /// </summary>
    public double? TotalWeight { get; set; }

    /// <summary>
    /// If using weighted heatmap, the maximum weight value
    /// </summary>
    public double? MaxWeight { get; set; }

    /// <summary>
    /// If using weighted heatmap, the minimum weight value
    /// </summary>
    public double? MinWeight { get; set; }

    /// <summary>
    /// Bounds of the heatmap data [west, south, east, north]
    /// </summary>
    public double[]? Bounds { get; set; }
}

/// <summary>
/// Event args for heatmap updated event
/// </summary>
public class HeatmapUpdatedEventArgs
{
    /// <summary>
    /// Heatmap layer ID
    /// </summary>
    public required string LayerId { get; init; }

    /// <summary>
    /// Updated configuration
    /// </summary>
    public required HeatmapConfiguration Configuration { get; init; }

    /// <summary>
    /// Statistics about the heatmap
    /// </summary>
    public HeatmapStatistics? Statistics { get; init; }
}
