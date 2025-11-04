// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Validates SRID (Spatial Reference Identifier) values against a whitelist of supported coordinate reference systems.
/// SECURITY: Prevents injection of invalid SRID values that could cause geometry processing errors or security issues.
/// </summary>
/// <remarks>
/// This validator ensures that only well-known and supported EPSG coordinate reference systems are used.
/// Unsupported or invalid SRIDs are rejected to prevent potential exploits or processing errors.
/// The whitelist includes commonly used geographic and projected coordinate systems.
/// </remarks>
public sealed class SridValidator
{
    /// <summary>
    /// Common geographic coordinate reference systems (latitude/longitude).
    /// </summary>
    private static readonly HashSet<int> CommonGeographicSrids =
    [
        4326,  // WGS 84 (GPS, web mapping standard)
        4269,  // NAD83 (North American Datum 1983)
        4267,  // NAD27 (North American Datum 1927)
        4258,  // ETRS89 (European Terrestrial Reference System 1989)
        4230,  // ED50 (European Datum 1950)
        4674,  // SIRGAS 2000 (South America)
        4019,  // GDA94 (Geocentric Datum of Australia 1994)
        4283,  // GDA94 (alternative code)
        4152,  // NAD83(HARN)
        4617,  // NAD83(NSRS2007)
        4759,  // NAD83(2011)
        3857   // WGS 84 / Pseudo-Mercator (web mapping, technically projected but commonly used)
    ];

    /// <summary>
    /// Common projected coordinate reference systems.
    /// </summary>
    private static readonly HashSet<int> CommonProjectedSrids =
    [
        // UTM WGS84 Northern Hemisphere zones (32601-32660)
        32601, 32602, 32603, 32604, 32605, 32606, 32607, 32608, 32609, 32610,
        32611, 32612, 32613, 32614, 32615, 32616, 32617, 32618, 32619, 32620,
        32621, 32622, 32623, 32624, 32625, 32626, 32627, 32628, 32629, 32630,
        32631, 32632, 32633, 32634, 32635, 32636, 32637, 32638, 32639, 32640,
        32641, 32642, 32643, 32644, 32645, 32646, 32647, 32648, 32649, 32650,
        32651, 32652, 32653, 32654, 32655, 32656, 32657, 32658, 32659, 32660,

        // UTM WGS84 Southern Hemisphere zones (32701-32760)
        32701, 32702, 32703, 32704, 32705, 32706, 32707, 32708, 32709, 32710,
        32711, 32712, 32713, 32714, 32715, 32716, 32717, 32718, 32719, 32720,
        32721, 32722, 32723, 32724, 32725, 32726, 32727, 32728, 32729, 32730,
        32731, 32732, 32733, 32734, 32735, 32736, 32737, 32738, 32739, 32740,
        32741, 32742, 32743, 32744, 32745, 32746, 32747, 32748, 32749, 32750,
        32751, 32752, 32753, 32754, 32755, 32756, 32757, 32758, 32759, 32760,

        // US State Plane NAD83 (common zones - representative sample)
        2225,  // California zone 2
        2226,  // California zone 3
        2227,  // California zone 4
        2228,  // California zone 5
        2229,  // California zone 6
        2230,  // California zone 1
        2231,  // California zone 2 (meters)
        2232,  // California zone 3 (meters)
        2233,  // California zone 4 (meters)
        2234,  // California zone 5 (meters)
        2235,  // California zone 6 (meters)
        2276,  // Texas North Central
        2277,  // Texas Central
        2278,  // Texas South Central
        2279,  // Texas North
        2805,  // NAD83(HARN) / Texas Centric Mapping System / Albers
        3081,  // NAD83(HARN) / Texas Centric Lambert Conformal
        3082,  // NAD83(HARN) / Texas Centric Albers Equal Area

        // International and regional projections
        2154,  // RGF93 / Lambert-93 (France)
        2056,  // CH1903+ / LV95 (Switzerland)
        3035,  // ETRS89 / LAEA Europe
        3034,  // ETRS89 / LCC Europe
        27700, // OSGB 1936 / British National Grid (UK)
        28992, // Amersfoort / RD New (Netherlands)
        31370, // BD72 / Belgian Lambert 72 (Belgium)
        25832, // ETRS89 / UTM zone 32N (Central Europe)
        25833, // ETRS89 / UTM zone 33N (Eastern Europe)

        // Polar regions
        3031,  // WGS 84 / Antarctic Polar Stereographic
        3995,  // WGS 84 / Arctic Polar Stereographic
        3413,  // WGS 84 / NSIDC Sea Ice Polar Stereographic North
        3976   // WGS 84 / NSIDC Sea Ice Polar Stereographic South
    ];

    /// <summary>
    /// Validates an SRID value against the whitelist of supported coordinate reference systems.
    /// </summary>
    /// <param name="srid">The SRID value to validate.</param>
    /// <returns>True if the SRID is valid and supported; otherwise, false.</returns>
    /// <remarks>
    /// An SRID is considered valid if:
    /// - It is zero (indicates no specific CRS or unknown)
    /// - It is in the common geographic SRID whitelist
    /// - It is in the common projected SRID whitelist
    /// </remarks>
    public static bool IsValid(int srid)
    {
        // SRID 0 means no SRID or unknown/undefined - allow it
        if (srid == 0)
        {
            return true;
        }

        // Check if SRID is negative (invalid)
        if (srid < 0)
        {
            return false;
        }

        // Check against whitelisted SRIDs
        return CommonGeographicSrids.Contains(srid) || CommonProjectedSrids.Contains(srid);
    }

    /// <summary>
    /// Validates an SRID value and throws an exception if invalid.
    /// </summary>
    /// <param name="srid">The SRID value to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the SRID is not supported.</exception>
    /// <remarks>
    /// Error messages are intentionally vague to avoid leaking information about supported SRIDs.
    /// </remarks>
    public static void Validate(int srid, string parameterName = "srid")
    {
        if (!IsValid(srid))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                srid,
                $"The SRID value {srid} is not supported. Please use a standard EPSG coordinate reference system.");
        }
    }

    /// <summary>
    /// Tries to validate an SRID value.
    /// </summary>
    /// <param name="srid">The SRID value to validate.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>True if valid; otherwise, false with an error message.</returns>
    public static bool TryValidate(int srid, out string? errorMessage)
    {
        if (IsValid(srid))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = $"The SRID value {srid} is not supported. Please use a standard EPSG coordinate reference system.";
        return false;
    }

    /// <summary>
    /// Gets whether an SRID represents a geographic coordinate system (lat/lon).
    /// </summary>
    /// <param name="srid">The SRID to check.</param>
    /// <returns>True if the SRID is a known geographic CRS; false if projected or unknown.</returns>
    public static bool IsGeographic(int srid)
    {
        return CommonGeographicSrids.Contains(srid);
    }

    /// <summary>
    /// Gets whether an SRID represents a projected coordinate system.
    /// </summary>
    /// <param name="srid">The SRID to check.</param>
    /// <returns>True if the SRID is a known projected CRS; false if geographic or unknown.</returns>
    public static bool IsProjected(int srid)
    {
        return CommonProjectedSrids.Contains(srid);
    }

    /// <summary>
    /// Validates coordinate values against the expected range for the SRID.
    /// </summary>
    /// <param name="srid">The SRID defining the coordinate system.</param>
    /// <param name="x">The X coordinate (longitude for geographic, easting for projected).</param>
    /// <param name="y">The Y coordinate (latitude for geographic, northing for projected).</param>
    /// <returns>True if coordinates are within valid range; otherwise, false.</returns>
    /// <remarks>
    /// For geographic CRSs, validates that longitude is in [-180, 180] and latitude is in [-90, 90].
    /// For projected CRSs, performs basic sanity checks (within reasonable Earth surface bounds).
    /// </remarks>
    public static bool AreCoordinatesValid(int srid, double x, double y)
    {
        // Check for NaN or infinity
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
        {
            return false;
        }

        // SRID 0 or unknown - no specific validation
        if (srid == 0)
        {
            return true;
        }

        // Geographic coordinate systems (lat/lon)
        if (IsGeographic(srid))
        {
            // Longitude range: -180 to 180
            // Latitude range: -90 to 90
            return x >= -180.0 && x <= 180.0 && y >= -90.0 && y <= 90.0;
        }

        // Projected coordinate systems
        if (IsProjected(srid))
        {
            // Very generous bounds check - Earth's circumference is ~40,000 km
            // Projected coordinates should be within +/- 20,000 km from origin in most systems
            const double MaxProjectedExtent = 20_000_000.0; // 20,000 km in meters
            return Math.Abs(x) <= MaxProjectedExtent && Math.Abs(y) <= MaxProjectedExtent;
        }

        // Unknown SRID that wasn't validated - should have been caught earlier
        return false;
    }

    /// <summary>
    /// Gets a friendly description of the SRID.
    /// </summary>
    /// <param name="srid">The SRID to describe.</param>
    /// <returns>A human-readable description of the coordinate reference system.</returns>
    public static string GetDescription(int srid)
    {
        return srid switch
        {
            0 => "Unspecified / Unknown CRS",
            4326 => "WGS 84 (GPS coordinates)",
            3857 => "WGS 84 / Pseudo-Mercator (Web Mercator)",
            4269 => "NAD83 (North American Datum 1983)",
            4267 => "NAD27 (North American Datum 1927)",
            4258 => "ETRS89 (European Terrestrial Reference System 1989)",
            27700 => "OSGB 1936 / British National Grid",
            2154 => "RGF93 / Lambert-93 (France)",
            3031 => "WGS 84 / Antarctic Polar Stereographic",
            3995 => "WGS 84 / Arctic Polar Stereographic",
            _ when srid >= 32601 && srid <= 32660 => $"WGS 84 / UTM zone {srid - 32600}N",
            _ when srid >= 32701 && srid <= 32760 => $"WGS 84 / UTM zone {srid - 32700}S",
            _ when IsGeographic(srid) => "Geographic Coordinate System",
            _ when IsProjected(srid) => "Projected Coordinate System",
            _ => "Unknown or Unsupported CRS"
        };
    }
}
