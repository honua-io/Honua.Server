// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using OSGeo.OSR;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// Helper for WCS 2.0/2.1 CRS extension support.
/// Provides CRS parsing, validation, and transformation utilities.
/// </summary>
internal static class WcsCrsHelper
{
    /// <summary>
    /// Supported output CRS list with common EPSG codes.
    /// </summary>
    private static readonly HashSet<int> SupportedOutputCrs = new()
    {
        4326,  // WGS 84
        3857,  // Web Mercator
        32601, 32602, 32603, 32604, 32605, 32606, 32607, 32608, 32609, 32610, // UTM North zones 1-10
        32611, 32612, 32613, 32614, 32615, 32616, 32617, 32618, 32619, 32620, // UTM North zones 11-20
        32621, 32622, 32623, 32624, 32625, 32626, 32627, 32628, 32629, 32630, // UTM North zones 21-30
        32631, 32632, 32633, 32634, 32635, 32636, 32637, 32638, 32639, 32640, // UTM North zones 31-40
        32641, 32642, 32643, 32644, 32645, 32646, 32647, 32648, 32649, 32650, // UTM North zones 41-50
        32651, 32652, 32653, 32654, 32655, 32656, 32657, 32658, 32659, 32660, // UTM North zones 51-60
        32701, 32702, 32703, 32704, 32705, 32706, 32707, 32708, 32709, 32710, // UTM South zones 1-10
        32711, 32712, 32713, 32714, 32715, 32716, 32717, 32718, 32719, 32720, // UTM South zones 11-20
        32721, 32722, 32723, 32724, 32725, 32726, 32727, 32728, 32729, 32730, // UTM South zones 21-30
        32731, 32732, 32733, 32734, 32735, 32736, 32737, 32738, 32739, 32740, // UTM South zones 31-40
        32741, 32742, 32743, 32744, 32745, 32746, 32747, 32748, 32749, 32750, // UTM South zones 41-50
        32751, 32752, 32753, 32754, 32755, 32756, 32757, 32758, 32759, 32760, // UTM South zones 51-60
        2154,  // RGF93 / Lambert-93 (France)
        3035,  // ETRS89 / LAEA Europe
        3395,  // WGS 84 / World Mercator
        3410,  // NSIDC EASE-Grid North
        3411,  // NSIDC EASE-Grid South
        3412,  // NSIDC EASE-Grid Global
        3413,  // WGS 84 / NSIDC Sea Ice Polar Stereographic North
        3031,  // WGS 84 / Antarctic Polar Stereographic
        3575,  // WGS 84 / North Pole LAEA
        3576,  // WGS 84 / South Pole LAEA
        5041,  // WGS 84 / UPS North (E,N)
        5042,  // WGS 84 / UPS South (E,N)
        6933,  // WGS 84 / NSIDC EASE-Grid 2.0 Global
        6931,  // WGS 84 / NSIDC EASE-Grid 2.0 North
        6932,  // WGS 84 / NSIDC EASE-Grid 2.0 South
        27700, // OSGB 1936 / British National Grid
        2056,  // CH1903+ / LV95 (Switzerland)
        28992, // Amersfoort / RD New (Netherlands)
        2193,  // NZGD2000 / New Zealand Transverse Mercator
        3112,  // GDA94 / Geoscience Australia Lambert
        5070,  // NAD83 / Conus Albers
        5071,  // NAD83(HARN) / Conus Albers
        6350,  // NAD83(2011) / Conus Albers
        102001, // Canada Albers Equal Area Conic
        102003, // USA Contiguous Albers Equal Area Conic
        102008, // North America Albers Equal Area Conic
    };

    /// <summary>
    /// Gets the list of supported output CRS URIs for capabilities.
    /// </summary>
    public static IEnumerable<string> GetSupportedCrsUris()
    {
        return SupportedOutputCrs.Select(epsg => $"http://www.opengis.net/def/crs/EPSG/0/{epsg}");
    }

    /// <summary>
    /// Parses a CRS URI to extract the EPSG code.
    /// Supports formats:
    /// - http://www.opengis.net/def/crs/EPSG/0/4326
    /// - urn:ogc:def:crs:EPSG::4326
    /// - EPSG:4326
    /// </summary>
    /// <param name="crsUri">The CRS URI to parse.</param>
    /// <param name="epsgCode">The extracted EPSG code.</param>
    /// <returns>True if successfully parsed, false otherwise.</returns>
    public static bool TryParseCrsUri(string? crsUri, out int epsgCode)
    {
        epsgCode = 0;

        if (crsUri.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Format: http://www.opengis.net/def/crs/EPSG/0/4326
        if (crsUri.StartsWith("http://www.opengis.net/def/crs/EPSG/0/", StringComparison.OrdinalIgnoreCase))
        {
            var code = crsUri.Substring("http://www.opengis.net/def/crs/EPSG/0/".Length);
            return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsgCode);
        }

        // Format: urn:ogc:def:crs:EPSG::4326
        if (crsUri.StartsWith("urn:ogc:def:crs:EPSG::", StringComparison.OrdinalIgnoreCase))
        {
            var code = crsUri.Substring("urn:ogc:def:crs:EPSG::".Length);
            return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsgCode);
        }

        // Format: EPSG:4326
        if (crsUri.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
        {
            var code = crsUri.Substring("EPSG:".Length);
            return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsgCode);
        }

        // Try direct integer parse as last resort
        return int.TryParse(crsUri, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsgCode);
    }

    /// <summary>
    /// Validates that a CRS is supported for output.
    /// </summary>
    /// <param name="epsgCode">The EPSG code to validate.</param>
    /// <returns>True if supported, false otherwise.</returns>
    public static bool IsSupportedOutputCrs(int epsgCode)
    {
        return SupportedOutputCrs.Contains(epsgCode);
    }

    /// <summary>
    /// Formats an EPSG code as a WCS CRS URI.
    /// </summary>
    /// <param name="epsgCode">The EPSG code.</param>
    /// <returns>The formatted CRS URI.</returns>
    public static string FormatCrsUri(int epsgCode)
    {
        return $"http://www.opengis.net/def/crs/EPSG/0/{epsgCode}";
    }

    /// <summary>
    /// Extracts the EPSG code from a GDAL projection string.
    /// </summary>
    /// <param name="projectionWkt">The GDAL projection WKT string.</param>
    /// <param name="epsgCode">The extracted EPSG code.</param>
    /// <returns>True if successfully extracted, false otherwise.</returns>
    public static bool TryExtractEpsgFromProjection(string? projectionWkt, out int epsgCode)
    {
        epsgCode = 0;

        if (projectionWkt.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            using var srs = new SpatialReference(projectionWkt);
            var authorityName = srs.GetAuthorityName(null);
            var authorityCode = srs.GetAuthorityCode(null);

            if (authorityName?.EqualsIgnoreCase("EPSG") == true && !authorityCode.IsNullOrWhiteSpace())
            {
                return int.TryParse(authorityCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out epsgCode);
            }
        }
        catch
        {
            // Ignore errors, return false
        }

        return false;
    }

    /// <summary>
    /// Gets the native CRS URI for a dataset based on its projection.
    /// Falls back to EPSG:4326 if the projection cannot be determined.
    /// </summary>
    /// <param name="projectionWkt">The GDAL projection WKT string.</param>
    /// <returns>The CRS URI.</returns>
    public static string GetNativeCrsUri(string? projectionWkt)
    {
        if (TryExtractEpsgFromProjection(projectionWkt, out var epsgCode) && epsgCode > 0)
        {
            return FormatCrsUri(epsgCode);
        }

        // Default to WGS 84
        return FormatCrsUri(4326);
    }

    /// <summary>
    /// Validates CRS transformation parameters.
    /// </summary>
    /// <param name="subsettingCrs">The subsetting CRS (for bbox coordinates).</param>
    /// <param name="outputCrs">The output CRS (for reprojection).</param>
    /// <param name="nativeCrs">The native CRS of the coverage.</param>
    /// <param name="error">Error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateCrsParameters(
        string? subsettingCrs,
        string? outputCrs,
        string nativeCrs,
        out string? error)
    {
        error = null;

        // Validate subsettingCrs if provided
        if (!subsettingCrs.IsNullOrWhiteSpace())
        {
            if (!TryParseCrsUri(subsettingCrs, out var subsettingEpsg))
            {
                error = $"Invalid subsettingCrs format: '{subsettingCrs}'.";
                return false;
            }

            if (subsettingEpsg <= 0)
            {
                error = $"Invalid subsettingCrs EPSG code: {subsettingEpsg}.";
                return false;
            }
        }

        // Validate outputCrs if provided
        if (!outputCrs.IsNullOrWhiteSpace())
        {
            if (!TryParseCrsUri(outputCrs, out var outputEpsg))
            {
                error = $"Invalid outputCrs format: '{outputCrs}'.";
                return false;
            }

            if (!IsSupportedOutputCrs(outputEpsg))
            {
                error = $"CRS 'EPSG:{outputEpsg}' is not supported. Use GetCapabilities to see supported CRS list.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines if CRS transformation is needed based on parameters.
    /// </summary>
    /// <param name="subsettingCrs">The subsetting CRS.</param>
    /// <param name="outputCrs">The output CRS.</param>
    /// <param name="nativeEpsg">The native EPSG code of the coverage.</param>
    /// <returns>True if transformation is needed, false otherwise.</returns>
    public static bool NeedsTransformation(string? subsettingCrs, string? outputCrs, int nativeEpsg)
    {
        if (!outputCrs.IsNullOrWhiteSpace())
        {
            if (TryParseCrsUri(outputCrs, out var outputEpsg) && outputEpsg != nativeEpsg)
            {
                return true;
            }
        }

        if (!subsettingCrs.IsNullOrWhiteSpace())
        {
            if (TryParseCrsUri(subsettingCrs, out var subsettingEpsg) && subsettingEpsg != nativeEpsg)
            {
                return true;
            }
        }

        return false;
    }
}
