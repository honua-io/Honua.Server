// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models.Sky;

/// <summary>
/// Configuration for sky layer appearance and behavior
/// </summary>
public class SkyConfiguration
{
    /// <summary>
    /// Type of sky rendering
    /// </summary>
    public SkyType SkyType { get; set; } = SkyType.Atmosphere;

    /// <summary>
    /// Base sky color (CSS color string) for gradient or solid sky
    /// </summary>
    public string SkyColor { get; set; } = "#87CEEB";

    /// <summary>
    /// Horizon color (CSS color string) for gradient sky
    /// </summary>
    public string HorizonColor { get; set; } = "#FFA07A";

    /// <summary>
    /// Horizon blend factor (0.0 = sharp, 1.0 = smooth)
    /// </summary>
    public double HorizonBlend { get; set; } = 0.1;

    /// <summary>
    /// Enable atmospheric scattering effects
    /// </summary>
    public bool EnableAtmosphere { get; set; } = true;

    /// <summary>
    /// Atmosphere intensity (0.0 = none, 1.0 = maximum)
    /// </summary>
    public double AtmosphereIntensity { get; set; } = 1.0;

    /// <summary>
    /// Atmosphere color (CSS color string)
    /// </summary>
    public string AtmosphereColor { get; set; } = "#87CEEB";

    /// <summary>
    /// Enable stars/space background
    /// </summary>
    public bool EnableStars { get; set; } = true;

    /// <summary>
    /// Sun position (azimuth and altitude in degrees)
    /// </summary>
    public Vector2 SunPosition { get; set; } = new Vector2(180, 45);

    /// <summary>
    /// Enable automatic sun position calculation based on time and location
    /// </summary>
    public bool EnableAutoSunPosition { get; set; } = false;

    /// <summary>
    /// Location for sun position calculation (if EnableAutoSunPosition is true)
    /// </summary>
    public Coordinate? Location { get; set; }

    /// <summary>
    /// DateTime for sun position calculation (if EnableAutoSunPosition is true)
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Enable fog at horizon
    /// </summary>
    public bool EnableFog { get; set; } = false;

    /// <summary>
    /// Fog density (0.0 = none, 1.0 = maximum)
    /// </summary>
    public double FogDensity { get; set; } = 0.5;

    /// <summary>
    /// Fog color (CSS color string)
    /// </summary>
    public string FogColor { get; set; } = "#FFFFFF";
}

/// <summary>
/// Type of sky rendering
/// </summary>
public enum SkyType
{
    /// <summary>
    /// Gradient sky (color transitions from sky to horizon)
    /// </summary>
    Gradient,

    /// <summary>
    /// Atmospheric scattering with realistic day/night colors
    /// </summary>
    Atmosphere,

    /// <summary>
    /// Solid color sky
    /// </summary>
    Solid,

    /// <summary>
    /// Custom sky configuration
    /// </summary>
    Custom
}

/// <summary>
/// 2D vector for storing azimuth and altitude
/// </summary>
public class Vector2
{
    /// <summary>
    /// X component (azimuth in degrees: 0 = North, 90 = East, 180 = South, 270 = West)
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y component (altitude in degrees: 0 = horizon, 90 = zenith, -90 = nadir)
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Create a new Vector2 with zero components
    /// </summary>
    public Vector2() : this(0, 0) { }

    /// <summary>
    /// Create a new Vector2 with specified components
    /// </summary>
    /// <param name="x">X component (azimuth)</param>
    /// <param name="y">Y component (altitude)</param>
    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Get string representation
    /// </summary>
    public override string ToString() => $"({X:F2}, {Y:F2})";
}

/// <summary>
/// Geographic coordinate for sun position calculation
/// </summary>
public class Coordinate
{
    /// <summary>
    /// Latitude in degrees (-90 to 90)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in degrees (-180 to 180)
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Elevation in meters (optional)
    /// </summary>
    public double? Elevation { get; set; }

    /// <summary>
    /// Create a new Coordinate with zero values
    /// </summary>
    public Coordinate() : this(0, 0) { }

    /// <summary>
    /// Create a new Coordinate with specified latitude and longitude
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="elevation">Elevation in meters (optional)</param>
    public Coordinate(double latitude, double longitude, double? elevation = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        Elevation = elevation;
    }

    /// <summary>
    /// Get string representation
    /// </summary>
    public override string ToString() =>
        Elevation.HasValue
            ? $"({Latitude:F6}, {Longitude:F6}, {Elevation:F2}m)"
            : $"({Latitude:F6}, {Longitude:F6})";
}

/// <summary>
/// Sun position calculation result
/// </summary>
public class SunPosition
{
    /// <summary>
    /// Solar azimuth in degrees (0 = North, 90 = East, 180 = South, 270 = West)
    /// </summary>
    public double Azimuth { get; set; }

    /// <summary>
    /// Solar altitude (elevation) in degrees (0 = horizon, 90 = zenith, negative = below horizon)
    /// </summary>
    public double Altitude { get; set; }

    /// <summary>
    /// Zenith angle in degrees (complement of altitude)
    /// </summary>
    public double Zenith => 90 - Altitude;

    /// <summary>
    /// Whether the sun is above the horizon
    /// </summary>
    public bool IsAboveHorizon => Altitude > 0;

    /// <summary>
    /// Time of day category based on sun position
    /// </summary>
    public TimeOfDay TimeOfDay
    {
        get
        {
            if (Altitude < -18) return TimeOfDay.Night;
            if (Altitude < -12) return TimeOfDay.AstronomicalTwilight;
            if (Altitude < -6) return TimeOfDay.NauticalTwilight;
            if (Altitude < 0) return TimeOfDay.CivilTwilight;
            if (Altitude < 6) return TimeOfDay.Sunrise;
            if (Altitude > 84) return TimeOfDay.HighNoon;
            return TimeOfDay.Day;
        }
    }

    /// <summary>
    /// Create a new SunPosition
    /// </summary>
    /// <param name="azimuth">Solar azimuth in degrees</param>
    /// <param name="altitude">Solar altitude in degrees</param>
    public SunPosition(double azimuth, double altitude)
    {
        Azimuth = azimuth;
        Altitude = altitude;
    }

    /// <summary>
    /// Get string representation
    /// </summary>
    public override string ToString() => $"Azimuth: {Azimuth:F2}°, Altitude: {Altitude:F2}° ({TimeOfDay})";
}

/// <summary>
/// Time of day categories based on sun position
/// </summary>
public enum TimeOfDay
{
    /// <summary>
    /// Night time (sun altitude &lt; -18°)
    /// </summary>
    Night,

    /// <summary>
    /// Astronomical twilight (-18° &lt; sun altitude &lt; -12°)
    /// </summary>
    AstronomicalTwilight,

    /// <summary>
    /// Nautical twilight (-12° &lt; sun altitude &lt; -6°)
    /// </summary>
    NauticalTwilight,

    /// <summary>
    /// Civil twilight (-6° &lt; sun altitude &lt; 0°)
    /// </summary>
    CivilTwilight,

    /// <summary>
    /// Sunrise/Sunset (0° &lt; sun altitude &lt; 6°)
    /// </summary>
    Sunrise,

    /// <summary>
    /// Day time (6° &lt; sun altitude &lt; 84°)
    /// </summary>
    Day,

    /// <summary>
    /// High noon (sun altitude &gt; 84°)
    /// </summary>
    HighNoon
}

/// <summary>
/// Sun times for a specific date and location
/// </summary>
public class SunTimes
{
    /// <summary>
    /// Sunrise time (sun crosses horizon upward)
    /// </summary>
    public DateTime? Sunrise { get; set; }

    /// <summary>
    /// Solar noon (sun reaches highest point)
    /// </summary>
    public DateTime? SolarNoon { get; set; }

    /// <summary>
    /// Sunset time (sun crosses horizon downward)
    /// </summary>
    public DateTime? Sunset { get; set; }

    /// <summary>
    /// Civil twilight begin (sun altitude = -6°)
    /// </summary>
    public DateTime? CivilTwilightBegin { get; set; }

    /// <summary>
    /// Civil twilight end (sun altitude = -6°)
    /// </summary>
    public DateTime? CivilTwilightEnd { get; set; }

    /// <summary>
    /// Day length in hours
    /// </summary>
    public double? DayLength
    {
        get
        {
            if (Sunrise.HasValue && Sunset.HasValue)
            {
                return (Sunset.Value - Sunrise.Value).TotalHours;
            }
            return null;
        }
    }

    /// <summary>
    /// Whether it's currently polar night (sun never rises)
    /// </summary>
    public bool IsPolarNight => !Sunrise.HasValue && !Sunset.HasValue;

    /// <summary>
    /// Whether it's currently polar day (sun never sets)
    /// </summary>
    public bool IsPolarDay => Sunrise.HasValue && !Sunset.HasValue;
}
