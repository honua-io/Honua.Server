// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Styling options for route visualization on the map.
/// </summary>
public sealed record RouteStyle
{
    /// <summary>
    /// Route line color in hex format (e.g., "#4285F4").
    /// </summary>
    public string Color { get; init; } = "#4285F4";

    /// <summary>
    /// Alternative route color (typically lighter/grayed).
    /// </summary>
    public string AlternativeColor { get; init; } = "#888888";

    /// <summary>
    /// Active/selected route color.
    /// </summary>
    public string ActiveColor { get; init; } = "#1967D2";

    /// <summary>
    /// Line width in pixels.
    /// </summary>
    public double Width { get; init; } = 6.0;

    /// <summary>
    /// Line opacity (0.0 to 1.0).
    /// </summary>
    public double Opacity { get; init; } = 0.8;

    /// <summary>
    /// Outline color for better contrast.
    /// </summary>
    public string? OutlineColor { get; init; } = "#FFFFFF";

    /// <summary>
    /// Outline width in pixels.
    /// </summary>
    public double OutlineWidth { get; init; } = 2.0;

    /// <summary>
    /// Z-index for layering routes.
    /// </summary>
    public int ZIndex { get; init; } = 1000;

    /// <summary>
    /// Traffic color scheme for congestion visualization.
    /// </summary>
    public TrafficColorScheme? TrafficColors { get; init; }
}

/// <summary>
/// Traffic congestion color scheme.
/// </summary>
public sealed record TrafficColorScheme
{
    /// <summary>
    /// Color for free-flowing traffic.
    /// </summary>
    public string FreeFlow { get; init; } = "#4CAF50";

    /// <summary>
    /// Color for moderate traffic.
    /// </summary>
    public string Moderate { get; init; } = "#FFC107";

    /// <summary>
    /// Color for heavy traffic.
    /// </summary>
    public string Heavy { get; init; } = "#FF5722";

    /// <summary>
    /// Color for severe congestion.
    /// </summary>
    public string Severe { get; init; } = "#B71C1C";
}

/// <summary>
/// Styling options for turn-by-turn markers.
/// </summary>
public sealed record TurnMarkerStyle
{
    /// <summary>
    /// Marker icon size in pixels.
    /// </summary>
    public double Size { get; init; } = 32.0;

    /// <summary>
    /// Start marker icon (data URL or path).
    /// </summary>
    public string StartIcon { get; init; } = "📍";

    /// <summary>
    /// End marker icon.
    /// </summary>
    public string EndIcon { get; init; } = "🏁";

    /// <summary>
    /// Waypoint marker icon.
    /// </summary>
    public string WaypointIcon { get; init; } = "📌";

    /// <summary>
    /// Turn instruction marker icon.
    /// </summary>
    public string TurnIcon { get; init; } = "➡️";

    /// <summary>
    /// Whether to show instruction markers along route.
    /// </summary>
    public bool ShowInstructionMarkers { get; init; } = true;

    /// <summary>
    /// Whether to cluster nearby turn markers.
    /// </summary>
    public bool ClusterMarkers { get; init; } = false;
}

/// <summary>
/// Animation options for route drawing.
/// </summary>
public sealed record RouteAnimationOptions
{
    /// <summary>
    /// Whether to animate the route drawing.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Animation duration in milliseconds.
    /// </summary>
    public int DurationMs { get; init; } = 2000;

    /// <summary>
    /// Animation easing function (e.g., "linear", "ease-in-out").
    /// </summary>
    public string Easing { get; init; } = "ease-in-out";

    /// <summary>
    /// Whether to fit map bounds to route after animation.
    /// </summary>
    public bool FitBoundsAfter { get; init; } = true;

    /// <summary>
    /// Padding around route bounds in pixels.
    /// </summary>
    public int BoundsPadding { get; init; } = 50;
}

/// <summary>
/// State for highlighting portions of a route.
/// </summary>
public sealed record RouteHighlight
{
    /// <summary>
    /// Route identifier being highlighted.
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// Start index of highlighted segment.
    /// </summary>
    public int StartSegmentIndex { get; init; }

    /// <summary>
    /// End index of highlighted segment.
    /// </summary>
    public int EndSegmentIndex { get; init; }

    /// <summary>
    /// Highlight color.
    /// </summary>
    public string HighlightColor { get; init; } = "#FFEB3B";

    /// <summary>
    /// Highlight width multiplier.
    /// </summary>
    public double WidthMultiplier { get; init; } = 1.5;
}

/// <summary>
/// Waypoint with draggable and editable properties.
/// </summary>
public sealed class Waypoint
{
    /// <summary>
    /// Unique identifier for this waypoint.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Coordinates [longitude, latitude].
    /// </summary>
    public required double[] Coordinates { get; set; }

    /// <summary>
    /// Display address for the waypoint.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Display name for the waypoint.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this waypoint can be dragged on the map.
    /// </summary>
    public bool IsDraggable { get; set; } = true;

    /// <summary>
    /// Order index in the route.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this is a via point (won't be in turn-by-turn instructions).
    /// </summary>
    public bool IsViaPoint { get; set; }
}

/// <summary>
/// Elevation profile data point.
/// </summary>
public sealed record ElevationPoint
{
    /// <summary>
    /// Distance from start in meters.
    /// </summary>
    public required double DistanceMeters { get; init; }

    /// <summary>
    /// Elevation in meters.
    /// </summary>
    public required double ElevationMeters { get; init; }

    /// <summary>
    /// Location [longitude, latitude].
    /// </summary>
    public double[]? Location { get; init; }
}

/// <summary>
/// Route comparison metrics.
/// </summary>
public sealed record RouteComparisonMetrics
{
    /// <summary>
    /// Route identifier.
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// Provider name.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Total distance in meters.
    /// </summary>
    public required double DistanceMeters { get; init; }

    /// <summary>
    /// Total duration in seconds.
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Duration with traffic in seconds (if available).
    /// </summary>
    public double? DurationWithTrafficSeconds { get; init; }

    /// <summary>
    /// Estimated fuel cost (if available).
    /// </summary>
    public double? EstimatedFuelCost { get; init; }

    /// <summary>
    /// Estimated toll cost (if available).
    /// </summary>
    public double? EstimatedTollCost { get; init; }

    /// <summary>
    /// Number of toll roads on route.
    /// </summary>
    public int TollRoadCount { get; init; }

    /// <summary>
    /// Number of ferries on route.
    /// </summary>
    public int FerryCount { get; init; }

    /// <summary>
    /// Route warnings.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }
}

/// <summary>
/// Maneuver icon mapping for turn-by-turn instructions.
/// </summary>
public static class ManeuverIcons
{
    /// <summary>
    /// Get icon for a maneuver type.
    /// </summary>
    public static string GetIcon(string? maneuverType) => maneuverType?.ToLowerInvariant() switch
    {
        "turn-left" => "↰",
        "turn-right" => "↱",
        "sharp-left" => "⮨",
        "sharp-right" => "⮩",
        "slight-left" => "↖",
        "slight-right" => "↗",
        "straight" => "↑",
        "u-turn" => "⮌",
        "roundabout-left" => "⟲",
        "roundabout-right" => "⟳",
        "merge" => "⇥",
        "fork-left" => "⑃",
        "fork-right" => "⑂",
        "ramp-left" => "⤶",
        "ramp-right" => "⤷",
        "arrive" => "🏁",
        "depart" => "📍",
        "ferry" => "⛴",
        _ => "➡"
    };
}
