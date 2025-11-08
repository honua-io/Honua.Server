using Honua.MapSDK.Models;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility class for converting coordinates between different formats
/// </summary>
public static class CoordinateConverter
{
    /// <summary>
    /// Convert coordinates to decimal degrees format
    /// </summary>
    public static string ToDecimalDegrees(double longitude, double latitude, int precision = 6)
    {
        var latDir = latitude >= 0 ? "N" : "S";
        var lonDir = longitude >= 0 ? "E" : "W";
        var lat = Math.Abs(latitude);
        var lon = Math.Abs(longitude);

        return $"{lat.ToString($"F{precision}")}°{latDir}, {lon.ToString($"F{precision}")}°{lonDir}";
    }

    /// <summary>
    /// Convert coordinates to degrees decimal minutes format
    /// </summary>
    public static string ToDegreesDecimalMinutes(double longitude, double latitude, int precision = 3)
    {
        var latDir = latitude >= 0 ? "N" : "S";
        var lonDir = longitude >= 0 ? "E" : "W";

        var (latDeg, latMin) = ToDegreeMinutes(Math.Abs(latitude));
        var (lonDeg, lonMin) = ToDegreeMinutes(Math.Abs(longitude));

        return $"{latDeg}°{latMin.ToString($"F{precision}")}'{latDir} {lonDeg}°{lonMin.ToString($"F{precision}")}'{lonDir}";
    }

    /// <summary>
    /// Convert coordinates to degrees minutes seconds format
    /// </summary>
    public static string ToDegreesMinutesSeconds(double longitude, double latitude, int precision = 1)
    {
        var latDir = latitude >= 0 ? "N" : "S";
        var lonDir = longitude >= 0 ? "E" : "W";

        var (latDeg, latMin, latSec) = ToDegreeMinuteSeconds(Math.Abs(latitude));
        var (lonDeg, lonMin, lonSec) = ToDegreeMinuteSeconds(Math.Abs(longitude));

        return $"{latDeg}°{latMin}'{latSec.ToString($"F{precision}")}\" {latDir} {lonDeg}°{lonMin}'{lonSec.ToString($"F{precision}")}\" {lonDir}";
    }

    /// <summary>
    /// Convert coordinates to UTM format
    /// </summary>
    public static string ToUTM(double longitude, double latitude)
    {
        var (zone, hemisphere, easting, northing) = ToUTMComponents(longitude, latitude);
        return $"{zone}{hemisphere} {easting:F0}mE {northing:F0}mN";
    }

    /// <summary>
    /// Convert coordinates to MGRS format
    /// </summary>
    public static string ToMGRS(double longitude, double latitude)
    {
        var (zone, hemisphere, easting, northing) = ToUTMComponents(longitude, latitude);

        // Get the 100km grid square letters
        var (col, row) = GetMGRSGridSquare(zone, easting, northing, hemisphere);

        // Get 5-digit easting and northing within the grid square
        var e = ((int)easting % 100000).ToString("D5");
        var n = ((int)northing % 100000).ToString("D5");

        return $"{zone:D2}{GetMGRSLatitudeBand(latitude)}{col}{row}{e}{n}";
    }

    /// <summary>
    /// Convert coordinates to USNG format (same as MGRS)
    /// </summary>
    public static string ToUSNG(double longitude, double latitude)
    {
        return ToMGRS(longitude, latitude);
    }

    /// <summary>
    /// Format coordinates based on the specified format type
    /// </summary>
    public static string Format(double longitude, double latitude, CoordinateFormat format, int precision = 6)
    {
        return format switch
        {
            CoordinateFormat.DecimalDegrees => ToDecimalDegrees(longitude, latitude, precision),
            CoordinateFormat.DegreesDecimalMinutes => ToDegreesDecimalMinutes(longitude, latitude, Math.Min(precision, 3)),
            CoordinateFormat.DegreesMinutesSeconds => ToDegreesMinutesSeconds(longitude, latitude, Math.Min(precision, 2)),
            CoordinateFormat.UTM => ToUTM(longitude, latitude),
            CoordinateFormat.MGRS => ToMGRS(longitude, latitude),
            CoordinateFormat.USNG => ToUSNG(longitude, latitude),
            _ => ToDecimalDegrees(longitude, latitude, precision)
        };
    }

    /// <summary>
    /// Get the short format name for display
    /// </summary>
    public static string GetFormatName(CoordinateFormat format)
    {
        return format switch
        {
            CoordinateFormat.DecimalDegrees => "DD",
            CoordinateFormat.DegreesDecimalMinutes => "DDM",
            CoordinateFormat.DegreesMinutesSeconds => "DMS",
            CoordinateFormat.UTM => "UTM",
            CoordinateFormat.MGRS => "MGRS",
            CoordinateFormat.USNG => "USNG",
            _ => "DD"
        };
    }

    /// <summary>
    /// Get the full format description
    /// </summary>
    public static string GetFormatDescription(CoordinateFormat format)
    {
        return format switch
        {
            CoordinateFormat.DecimalDegrees => "Decimal Degrees",
            CoordinateFormat.DegreesDecimalMinutes => "Degrees Decimal Minutes",
            CoordinateFormat.DegreesMinutesSeconds => "Degrees Minutes Seconds",
            CoordinateFormat.UTM => "Universal Transverse Mercator",
            CoordinateFormat.MGRS => "Military Grid Reference System",
            CoordinateFormat.USNG => "United States National Grid",
            _ => "Decimal Degrees"
        };
    }

    /// <summary>
    /// Convert scale to ratio string (e.g., 1:25,000)
    /// </summary>
    public static string FormatScale(double scale)
    {
        return $"1:{scale:N0}";
    }

    /// <summary>
    /// Format elevation with appropriate unit
    /// </summary>
    public static string FormatElevation(double elevation, MeasurementUnitSystem unit)
    {
        return unit switch
        {
            MeasurementUnitSystem.Imperial => $"{MetersToFeet(elevation):F0} ft",
            MeasurementUnitSystem.Nautical => $"{elevation:F0} m", // Nautical uses metric for elevation
            _ => $"{elevation:F0} m"
        };
    }

    /// <summary>
    /// Format bearing/heading in degrees
    /// </summary>
    public static string FormatBearing(double bearing)
    {
        var normalized = ((bearing % 360) + 360) % 360; // Normalize to 0-360
        return $"{normalized:F1}°";
    }

    #region Private Helper Methods

    private static (int degrees, double minutes) ToDegreeMinutes(double decimalDegrees)
    {
        var degrees = (int)Math.Floor(decimalDegrees);
        var minutes = (decimalDegrees - degrees) * 60;
        return (degrees, minutes);
    }

    private static (int degrees, int minutes, double seconds) ToDegreeMinuteSeconds(double decimalDegrees)
    {
        var degrees = (int)Math.Floor(decimalDegrees);
        var minutesDecimal = (decimalDegrees - degrees) * 60;
        var minutes = (int)Math.Floor(minutesDecimal);
        var seconds = (minutesDecimal - minutes) * 60;
        return (degrees, minutes, seconds);
    }

    private static (int zone, string hemisphere, double easting, double northing) ToUTMComponents(double longitude, double latitude)
    {
        // Calculate UTM zone
        var zone = (int)Math.Floor((longitude + 180) / 6) + 1;

        // Determine hemisphere
        var hemisphere = latitude >= 0 ? "N" : "S";

        // WGS84 parameters
        const double a = 6378137.0; // Semi-major axis
        const double e2 = 0.00669438; // First eccentricity squared
        const double k0 = 0.9996; // Scale factor

        // Convert to radians
        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;

        // Central meridian for the zone
        var lonOrigin = ((zone - 1) * 6 - 180 + 3) * Math.PI / 180.0;

        // Calculate easting and northing
        var N = a / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(latRad), 2));
        var T = Math.Pow(Math.Tan(latRad), 2);
        var C = e2 * Math.Pow(Math.Cos(latRad), 2) / (1 - e2);
        var A = Math.Cos(latRad) * (lonRad - lonOrigin);

        var M = a * ((1 - e2 / 4 - 3 * Math.Pow(e2, 2) / 64 - 5 * Math.Pow(e2, 3) / 256) * latRad
            - (3 * e2 / 8 + 3 * Math.Pow(e2, 2) / 32 + 45 * Math.Pow(e2, 3) / 1024) * Math.Sin(2 * latRad)
            + (15 * Math.Pow(e2, 2) / 256 + 45 * Math.Pow(e2, 3) / 1024) * Math.Sin(4 * latRad)
            - (35 * Math.Pow(e2, 3) / 3072) * Math.Sin(6 * latRad));

        var easting = k0 * N * (A + (1 - T + C) * Math.Pow(A, 3) / 6
            + (5 - 18 * T + Math.Pow(T, 2) + 72 * C - 58 * e2 / (1 - e2)) * Math.Pow(A, 5) / 120) + 500000.0;

        var northing = k0 * (M + N * Math.Tan(latRad) * (Math.Pow(A, 2) / 2
            + (5 - T + 9 * C + 4 * Math.Pow(C, 2)) * Math.Pow(A, 4) / 24
            + (61 - 58 * T + Math.Pow(T, 2) + 600 * C - 330 * e2 / (1 - e2)) * Math.Pow(A, 6) / 720));

        // Add false northing for southern hemisphere
        if (hemisphere == "S")
        {
            northing += 10000000.0;
        }

        return (zone, hemisphere, easting, northing);
    }

    private static char GetMGRSLatitudeBand(double latitude)
    {
        // MGRS latitude bands from south to north (excluding I and O)
        const string bands = "CDEFGHJKLMNPQRSTUVWXX";
        var index = (int)Math.Floor((latitude + 80) / 8);
        index = Math.Max(0, Math.Min(bands.Length - 1, index));
        return bands[index];
    }

    private static (char column, char row) GetMGRSGridSquare(int zone, double easting, double northing, string hemisphere)
    {
        // 100km grid square identification
        const string colLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // No I or O
        const string rowLetters = "ABCDEFGHJKLMNPQRSTUV"; // No I or O

        // Calculate column letter (based on easting)
        var colIndex = ((int)(easting / 100000) - 1) % 8;
        colIndex = ((zone - 1) % 3) * 8 + colIndex;
        colIndex = colIndex % 24;

        // Calculate row letter (based on northing)
        var rowIndex = ((int)(northing / 100000)) % 20;
        if (hemisphere == "S")
        {
            rowIndex = (rowIndex + 5) % 20; // Offset for southern hemisphere
        }

        return (colLetters[colIndex], rowLetters[rowIndex]);
    }

    private static double MetersToFeet(double meters)
    {
        return meters * 3.28084;
    }

    #endregion
}
