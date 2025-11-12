// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models.Sky;

namespace Honua.MapSDK.Services.Sky;

/// <summary>
/// Service for calculating sun position using Solar Position Algorithm (SPA)
/// Based on NREL Solar Position Algorithm (Reda & Andreas, 2004)
/// Accurate to within 0.0003° between years 2000-6000
/// </summary>
public class SunPositionService
{
    private const double DEG_TO_RAD = Math.PI / 180.0;
    private const double RAD_TO_DEG = 180.0 / Math.PI;

    /// <summary>
    /// Calculate sun position (azimuth and altitude) for a specific time and location
    /// </summary>
    /// <param name="dateTime">Date and time (UTC)</param>
    /// <param name="latitude">Latitude in degrees (-90 to 90)</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180)</param>
    /// <param name="elevation">Elevation in meters (optional, default 0)</param>
    /// <returns>Sun position with azimuth and altitude</returns>
    public SunPosition Calculate(DateTime dateTime, double latitude, double longitude, double elevation = 0)
    {
        // Validate inputs
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees");
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees");

        // Calculate Julian Day
        double jd = CalculateJulianDay(dateTime);

        // Calculate Julian Century
        double jc = (jd - 2451545.0) / 36525.0;

        // Calculate sun's geometric mean longitude (degrees)
        double sunMeanLongitude = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360.0;

        // Calculate sun's geometric mean anomaly (degrees)
        double sunMeanAnomaly = 357.52911 + jc * (35999.05029 - 0.0001537 * jc);

        // Calculate Earth's orbit eccentricity
        double eccentricity = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);

        // Calculate sun's equation of center
        double sunCenter = Math.Sin(sunMeanAnomaly * DEG_TO_RAD) * (1.914602 - jc * (0.004817 + 0.000014 * jc))
            + Math.Sin(2 * sunMeanAnomaly * DEG_TO_RAD) * (0.019993 - 0.000101 * jc)
            + Math.Sin(3 * sunMeanAnomaly * DEG_TO_RAD) * 0.000289;

        // Calculate sun's true longitude
        double sunTrueLongitude = sunMeanLongitude + sunCenter;

        // Calculate sun's apparent longitude
        double omega = 125.04 - 1934.136 * jc;
        double sunApparentLongitude = sunTrueLongitude - 0.00569 - 0.00478 * Math.Sin(omega * DEG_TO_RAD);

        // Calculate mean obliquity of ecliptic (degrees)
        double meanObliquity = 23.0 + (26.0 + ((21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813)))) / 60.0) / 60.0;

        // Calculate obliquity correction
        double obliquityCorrection = meanObliquity + 0.00256 * Math.Cos(omega * DEG_TO_RAD);

        // Calculate sun's declination (degrees)
        double declination = Math.Asin(Math.Sin(obliquityCorrection * DEG_TO_RAD)
            * Math.Sin(sunApparentLongitude * DEG_TO_RAD)) * RAD_TO_DEG;

        // Calculate equation of time (minutes)
        double y = Math.Tan((obliquityCorrection / 2.0) * DEG_TO_RAD);
        y *= y;
        double equationOfTime = 4.0 * RAD_TO_DEG * (y * Math.Sin(2.0 * sunMeanLongitude * DEG_TO_RAD)
            - 2.0 * eccentricity * Math.Sin(sunMeanAnomaly * DEG_TO_RAD)
            + 4.0 * eccentricity * y * Math.Sin(sunMeanAnomaly * DEG_TO_RAD) * Math.Cos(2.0 * sunMeanLongitude * DEG_TO_RAD)
            - 0.5 * y * y * Math.Sin(4.0 * sunMeanLongitude * DEG_TO_RAD)
            - 1.25 * eccentricity * eccentricity * Math.Sin(2.0 * sunMeanAnomaly * DEG_TO_RAD));

        // Calculate true solar time
        double timeOffset = equationOfTime + 4.0 * longitude;
        double trueSolarTime = dateTime.Hour * 60.0 + dateTime.Minute + dateTime.Second / 60.0 + timeOffset;

        // Calculate hour angle (degrees)
        double hourAngle = (trueSolarTime / 4.0) - 180.0;
        if (hourAngle < -180) hourAngle += 360.0;
        if (hourAngle > 180) hourAngle -= 360.0;

        // Calculate solar zenith angle
        double latRad = latitude * DEG_TO_RAD;
        double decRad = declination * DEG_TO_RAD;
        double haRad = hourAngle * DEG_TO_RAD;

        double zenithAngle = Math.Acos(
            Math.Sin(latRad) * Math.Sin(decRad) +
            Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(haRad)
        ) * RAD_TO_DEG;

        // Calculate atmospheric refraction correction
        double altitude = 90.0 - zenithAngle;
        double refractionCorrection = CalculateRefraction(altitude, elevation);
        altitude += refractionCorrection;

        // Calculate azimuth angle
        double azimuth = (Math.Acos(
            ((Math.Sin(latRad) * Math.Cos(zenithAngle * DEG_TO_RAD)) - Math.Sin(decRad)) /
            (Math.Cos(latRad) * Math.Sin(zenithAngle * DEG_TO_RAD))
        ) * RAD_TO_DEG);

        // Adjust azimuth based on hour angle
        if (hourAngle > 0)
        {
            azimuth = 360.0 - azimuth;
        }

        // Normalize azimuth to 0-360
        azimuth = azimuth % 360.0;
        if (azimuth < 0) azimuth += 360.0;

        return new SunPosition(azimuth, altitude);
    }

    /// <summary>
    /// Calculate sun times (sunrise, sunset, solar noon) for a specific date and location
    /// </summary>
    /// <param name="date">Date (any time component will be ignored)</param>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="elevation">Elevation in meters (optional)</param>
    /// <returns>Sun times for the date</returns>
    public SunTimes CalculateSunTimes(DateTime date, double latitude, double longitude, double elevation = 0)
    {
        var sunTimes = new SunTimes();

        // Calculate solar noon first
        double jd = CalculateJulianDay(new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc));
        double jc = (jd - 2451545.0) / 36525.0;

        // Calculate equation of time for solar noon
        double sunMeanLongitude = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360.0;
        double sunMeanAnomaly = 357.52911 + jc * (35999.05029 - 0.0001537 * jc);
        double eccentricity = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);
        double omega = 125.04 - 1934.136 * jc;
        double meanObliquity = 23.0 + (26.0 + ((21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813)))) / 60.0) / 60.0;
        double obliquityCorrection = meanObliquity + 0.00256 * Math.Cos(omega * DEG_TO_RAD);

        double y = Math.Tan((obliquityCorrection / 2.0) * DEG_TO_RAD);
        y *= y;
        double equationOfTime = 4.0 * RAD_TO_DEG * (y * Math.Sin(2.0 * sunMeanLongitude * DEG_TO_RAD)
            - 2.0 * eccentricity * Math.Sin(sunMeanAnomaly * DEG_TO_RAD)
            + 4.0 * eccentricity * y * Math.Sin(sunMeanAnomaly * DEG_TO_RAD) * Math.Cos(2.0 * sunMeanLongitude * DEG_TO_RAD)
            - 0.5 * y * y * Math.Sin(4.0 * sunMeanLongitude * DEG_TO_RAD)
            - 1.25 * eccentricity * eccentricity * Math.Sin(2.0 * sunMeanAnomaly * DEG_TO_RAD));

        // Solar noon in minutes from midnight
        double solarNoonMinutes = 720.0 - 4.0 * longitude - equationOfTime;
        sunTimes.SolarNoon = date.Date.AddMinutes(solarNoonMinutes);

        // Calculate sun declination for the day
        double sunCenter = Math.Sin(sunMeanAnomaly * DEG_TO_RAD) * (1.914602 - jc * (0.004817 + 0.000014 * jc))
            + Math.Sin(2 * sunMeanAnomaly * DEG_TO_RAD) * (0.019993 - 0.000101 * jc)
            + Math.Sin(3 * sunMeanAnomaly * DEG_TO_RAD) * 0.000289;
        double sunTrueLongitude = sunMeanLongitude + sunCenter;
        double sunApparentLongitude = sunTrueLongitude - 0.00569 - 0.00478 * Math.Sin(omega * DEG_TO_RAD);
        double declination = Math.Asin(Math.Sin(obliquityCorrection * DEG_TO_RAD)
            * Math.Sin(sunApparentLongitude * DEG_TO_RAD)) * RAD_TO_DEG;

        // Calculate hour angle for sunrise/sunset (sun at horizon, 0.833° below for refraction and sun's radius)
        double sunsetAngle = -0.833;
        double latRad = latitude * DEG_TO_RAD;
        double decRad = declination * DEG_TO_RAD;

        double cosHourAngle = (Math.Sin(sunsetAngle * DEG_TO_RAD) - Math.Sin(latRad) * Math.Sin(decRad)) /
                              (Math.Cos(latRad) * Math.Cos(decRad));

        // Check for polar day/night
        if (cosHourAngle > 1)
        {
            // Polar night - sun never rises
            return sunTimes;
        }
        else if (cosHourAngle < -1)
        {
            // Polar day - sun never sets
            sunTimes.Sunrise = date.Date;
            return sunTimes;
        }

        double hourAngle = Math.Acos(cosHourAngle) * RAD_TO_DEG;

        // Calculate sunrise and sunset
        double sunriseMinutes = solarNoonMinutes - 4.0 * hourAngle;
        double sunsetMinutes = solarNoonMinutes + 4.0 * hourAngle;

        sunTimes.Sunrise = date.Date.AddMinutes(sunriseMinutes);
        sunTimes.Sunset = date.Date.AddMinutes(sunsetMinutes);

        // Calculate civil twilight (sun at -6°)
        double twilightAngle = -6.0;
        double cosTwilightAngle = (Math.Sin(twilightAngle * DEG_TO_RAD) - Math.Sin(latRad) * Math.Sin(decRad)) /
                                   (Math.Cos(latRad) * Math.Cos(decRad));

        if (cosTwilightAngle >= -1 && cosTwilightAngle <= 1)
        {
            double twilightHourAngle = Math.Acos(cosTwilightAngle) * RAD_TO_DEG;
            sunTimes.CivilTwilightBegin = date.Date.AddMinutes(solarNoonMinutes - 4.0 * twilightHourAngle);
            sunTimes.CivilTwilightEnd = date.Date.AddMinutes(solarNoonMinutes + 4.0 * twilightHourAngle);
        }

        return sunTimes;
    }

    /// <summary>
    /// Calculate Julian Day number from DateTime
    /// </summary>
    private double CalculateJulianDay(DateTime dateTime)
    {
        int year = dateTime.Year;
        int month = dateTime.Month;
        int day = dateTime.Day;
        double hour = dateTime.Hour + dateTime.Minute / 60.0 + dateTime.Second / 3600.0;

        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + (a / 4);

        double jd = Math.Floor(365.25 * (year + 4716))
                  + Math.Floor(30.6001 * (month + 1))
                  + day + b - 1524.5
                  + hour / 24.0;

        return jd;
    }

    /// <summary>
    /// Calculate atmospheric refraction correction
    /// </summary>
    private double CalculateRefraction(double altitude, double elevation)
    {
        // Standard atmospheric pressure at sea level (millibars)
        double pressure = 1013.25 * Math.Pow((1.0 - 0.0000225577 * elevation), 5.25588);

        // Standard temperature at sea level (Kelvin)
        double temperature = 288.15 - 0.0065 * elevation;

        if (altitude > 85.0)
        {
            // No refraction above 85°
            return 0.0;
        }
        else if (altitude > 5.0)
        {
            // Bennett's formula for refraction
            return (pressure / 1010.0) * (283.0 / temperature) *
                   (1.02 / Math.Tan((altitude + 10.3 / (altitude + 5.11)) * DEG_TO_RAD)) / 60.0;
        }
        else if (altitude > -0.575)
        {
            // More complex formula for low altitudes
            return (pressure / 1010.0) * (283.0 / temperature) *
                   (1.506 + 0.0845 * Math.Tan((altitude + 7.31 / (altitude + 4.4)) * DEG_TO_RAD)) / 60.0;
        }
        else
        {
            // Sun below horizon
            return -20.774 / Math.Tan(altitude * DEG_TO_RAD) / 3600.0;
        }
    }

    /// <summary>
    /// Get time of day category for a given sun altitude
    /// </summary>
    /// <param name="altitude">Sun altitude in degrees</param>
    /// <returns>Time of day category</returns>
    public TimeOfDay GetTimeOfDay(double altitude)
    {
        if (altitude < -18) return TimeOfDay.Night;
        if (altitude < -12) return TimeOfDay.AstronomicalTwilight;
        if (altitude < -6) return TimeOfDay.NauticalTwilight;
        if (altitude < 0) return TimeOfDay.CivilTwilight;
        if (altitude < 6) return TimeOfDay.Sunrise;
        if (altitude > 84) return TimeOfDay.HighNoon;
        return TimeOfDay.Day;
    }

    /// <summary>
    /// Get sky color based on sun altitude (for automatic sky coloring)
    /// </summary>
    /// <param name="altitude">Sun altitude in degrees</param>
    /// <returns>CSS color string</returns>
    public string GetSkyColorFromAltitude(double altitude)
    {
        var timeOfDay = GetTimeOfDay(altitude);
        return timeOfDay switch
        {
            TimeOfDay.Night => "#0B1026",
            TimeOfDay.AstronomicalTwilight => "#1B2A49",
            TimeOfDay.NauticalTwilight => "#2B3A69",
            TimeOfDay.CivilTwilight => "#4B5A89",
            TimeOfDay.Sunrise => "#FF8C00",
            TimeOfDay.Day => "#87CEEB",
            TimeOfDay.HighNoon => "#87CEEB",
            _ => "#87CEEB"
        };
    }

    /// <summary>
    /// Interpolate sky color based on sun altitude
    /// </summary>
    /// <param name="altitude">Sun altitude in degrees</param>
    /// <returns>Interpolated CSS color string</returns>
    public string InterpolateSkyColor(double altitude)
    {
        // Define color stops based on sun altitude
        if (altitude >= 6)
        {
            // Day time (bright blue)
            return "#87CEEB";
        }
        else if (altitude >= 0)
        {
            // Sunrise/sunset transition (blue to orange)
            double t = altitude / 6.0;
            return InterpolateColor("#FF8C00", "#87CEEB", t);
        }
        else if (altitude >= -6)
        {
            // Civil twilight (orange to purple)
            double t = (altitude + 6.0) / 6.0;
            return InterpolateColor("#4B0082", "#FF8C00", t);
        }
        else if (altitude >= -12)
        {
            // Nautical twilight (purple to dark blue)
            double t = (altitude + 12.0) / 6.0;
            return InterpolateColor("#1B2A49", "#4B0082", t);
        }
        else
        {
            // Night (dark blue to black)
            double t = Math.Max(0, (altitude + 18.0) / 6.0);
            return InterpolateColor("#0B1026", "#1B2A49", t);
        }
    }

    /// <summary>
    /// Interpolate between two hex colors
    /// </summary>
    private string InterpolateColor(string color1, string color2, double t)
    {
        t = Math.Clamp(t, 0, 1);

        // Parse hex colors
        var rgb1 = ParseHexColor(color1);
        var rgb2 = ParseHexColor(color2);

        // Interpolate
        int r = (int)(rgb1.r + (rgb2.r - rgb1.r) * t);
        int g = (int)(rgb1.g + (rgb2.g - rgb1.g) * t);
        int b = (int)(rgb1.b + (rgb2.b - rgb1.b) * t);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Parse hex color to RGB components
    /// </summary>
    private (int r, int g, int b) ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16)
        );
    }
}
