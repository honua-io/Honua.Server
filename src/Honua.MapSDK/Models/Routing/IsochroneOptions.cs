// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.MapSDK.Models.Routing;

/// <summary>
/// Options for isochrone/service area calculation
/// </summary>
public class IsochroneOptions
{
    /// <summary>
    /// Center point for isochrone [longitude, latitude]
    /// </summary>
    public double[] Center { get; set; } = new double[2];

    /// <summary>
    /// Time intervals in minutes (e.g., [5, 10, 15, 30])
    /// </summary>
    public List<int> Intervals { get; set; } = new() { 5, 10, 15 };

    /// <summary>
    /// Travel mode
    /// </summary>
    public TravelMode TravelMode { get; set; } = TravelMode.Driving;

    /// <summary>
    /// Type of isochrone (time-based or distance-based)
    /// </summary>
    public IsochroneType Type { get; set; } = IsochroneType.Time;

    /// <summary>
    /// Smoothing factor for polygon generation (0-1)
    /// </summary>
    public double Smoothing { get; set; } = 0.5;

    /// <summary>
    /// Colors for each interval
    /// </summary>
    public List<string> Colors { get; set; } = new()
    {
        "#00FF00",
        "#FFFF00",
        "#FF8800",
        "#FF0000"
    };

    /// <summary>
    /// Opacity for isochrone polygons (0-1)
    /// </summary>
    public double Opacity { get; set; } = 0.3;
}

/// <summary>
/// Type of isochrone
/// </summary>
public enum IsochroneType
{
    /// <summary>
    /// Time-based isochrone (areas reachable within time limits)
    /// </summary>
    Time,

    /// <summary>
    /// Distance-based isochrone (areas reachable within distance limits)
    /// </summary>
    Distance
}

/// <summary>
/// Result of isochrone calculation
/// </summary>
public class IsochroneResult
{
    /// <summary>
    /// Center point used for calculation
    /// </summary>
    public double[] Center { get; set; } = new double[2];

    /// <summary>
    /// Isochrone polygons (one per interval)
    /// </summary>
    public List<IsochronePolygon> Polygons { get; set; } = new();

    /// <summary>
    /// Travel mode used
    /// </summary>
    public TravelMode TravelMode { get; set; }
}

/// <summary>
/// Single isochrone polygon
/// </summary>
public class IsochronePolygon
{
    /// <summary>
    /// Interval value (minutes or meters depending on type)
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// GeoJSON polygon geometry
    /// </summary>
    public object Geometry { get; set; } = new { };

    /// <summary>
    /// Color for this polygon
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Opacity for this polygon
    /// </summary>
    public double Opacity { get; set; } = 0.3;

    /// <summary>
    /// Area in square meters
    /// </summary>
    public double Area { get; set; }
}
