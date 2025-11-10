// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Coordinate display format options
/// </summary>
public enum CoordinateFormat
{
    /// <summary>
    /// Decimal Degrees (DD): 37.7749, -122.4194
    /// </summary>
    DecimalDegrees,

    /// <summary>
    /// Degrees Decimal Minutes (DDM): 37°46.494'N 122°25.164'W
    /// </summary>
    DegreesDecimalMinutes,

    /// <summary>
    /// Degrees Minutes Seconds (DMS): 37°46'29.6"N 122°25'9.8"W
    /// </summary>
    DegreesMinutesSeconds,

    /// <summary>
    /// UTM (Universal Transverse Mercator): Zone 10N, 551316mE 4180969mN
    /// </summary>
    UTM,

    /// <summary>
    /// MGRS (Military Grid Reference System): 10SEG5131680969
    /// </summary>
    MGRS,

    /// <summary>
    /// USNG (United States National Grid): 10SEG5131680969
    /// </summary>
    USNG
}

/// <summary>
/// Measurement unit system
/// </summary>
public enum MeasurementUnitSystem
{
    /// <summary>
    /// Metric system (meters, kilometers)
    /// </summary>
    Metric,

    /// <summary>
    /// Imperial system (feet, miles)
    /// </summary>
    Imperial,

    /// <summary>
    /// Nautical system (nautical miles)
    /// </summary>
    Nautical
}

/// <summary>
/// Configuration for coordinate display
/// </summary>
public class CoordinateDisplayOptions
{
    /// <summary>
    /// Coordinate format to use
    /// </summary>
    public CoordinateFormat Format { get; set; } = CoordinateFormat.DecimalDegrees;

    /// <summary>
    /// Number of decimal places for decimal degrees
    /// </summary>
    public int Precision { get; set; } = 6;

    /// <summary>
    /// Show scale information
    /// </summary>
    public bool ShowScale { get; set; } = true;

    /// <summary>
    /// Show zoom level
    /// </summary>
    public bool ShowZoom { get; set; } = true;

    /// <summary>
    /// Show elevation at cursor (requires terrain)
    /// </summary>
    public bool ShowElevation { get; set; } = false;

    /// <summary>
    /// Show bearing/heading if map is rotated
    /// </summary>
    public bool ShowBearing { get; set; } = false;

    /// <summary>
    /// Measurement unit system
    /// </summary>
    public MeasurementUnitSystem Unit { get; set; } = MeasurementUnitSystem.Metric;

    /// <summary>
    /// Allow user to switch coordinate formats
    /// </summary>
    public bool AllowFormatSwitch { get; set; } = true;

    /// <summary>
    /// Position on the map (top-left, top-right, bottom-left, bottom-right)
    /// </summary>
    public string Position { get; set; } = "bottom-left";
}

/// <summary>
/// Coordinate information
/// </summary>
public class CoordinateInfo
{
    /// <summary>
    /// Longitude
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Elevation (if available)
    /// </summary>
    public double? Elevation { get; set; }

    /// <summary>
    /// Formatted coordinate string
    /// </summary>
    public string Formatted { get; set; } = string.Empty;

    /// <summary>
    /// Scale ratio (e.g., 25000 for 1:25,000)
    /// </summary>
    public double? Scale { get; set; }

    /// <summary>
    /// Zoom level
    /// </summary>
    public double? ZoomLevel { get; set; }

    /// <summary>
    /// Map bearing/rotation in degrees
    /// </summary>
    public double? Bearing { get; set; }
}
