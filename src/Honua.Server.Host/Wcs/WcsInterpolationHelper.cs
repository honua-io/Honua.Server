// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// Helper for WCS 2.0 Interpolation Extension support.
/// Maps OGC interpolation URIs to GDAL resampling algorithms.
/// Specification: OGC 12-049 - WCS 2.0 Interpolation Extension
/// </summary>
internal static class WcsInterpolationHelper
{
    /// <summary>
    /// Supported interpolation methods with their OGC URIs and GDAL equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> InterpolationMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // OGC standard interpolation URIs
        ["http://www.opengis.net/def/interpolation/OGC/1/nearest-neighbor"] = "near",
        ["http://www.opengis.net/def/interpolation/OGC/1/linear"] = "bilinear",
        ["http://www.opengis.net/def/interpolation/OGC/1/cubic"] = "cubic",
        ["http://www.opengis.net/def/interpolation/OGC/1/cubic-spline"] = "cubicspline",
        ["http://www.opengis.net/def/interpolation/OGC/1/average"] = "average",

        // Short form URIs
        ["nearest-neighbor"] = "near",
        ["linear"] = "bilinear",
        ["cubic-spline"] = "cubicspline",

        // GDAL names directly
        ["near"] = "near",
        ["nearest"] = "near",
        ["bilinear"] = "bilinear",
        ["cubicspline"] = "cubicspline",
        ["lanczos"] = "lanczos",
        ["average"] = "average",
        ["mode"] = "mode",
        ["max"] = "max",
        ["min"] = "min",
        ["med"] = "med",
        ["q1"] = "q1",
        ["q3"] = "q3",
    };

    /// <summary>
    /// Gets the list of supported interpolation method URIs for capabilities.
    /// </summary>
    public static IEnumerable<string> GetSupportedInterpolationUris()
    {
        yield return "http://www.opengis.net/def/interpolation/OGC/1/nearest-neighbor";
        yield return "http://www.opengis.net/def/interpolation/OGC/1/linear";
        yield return "http://www.opengis.net/def/interpolation/OGC/1/cubic";
        yield return "http://www.opengis.net/def/interpolation/OGC/1/cubic-spline";
        yield return "http://www.opengis.net/def/interpolation/OGC/1/average";
    }

    /// <summary>
    /// Parses an interpolation parameter and returns the GDAL resampling algorithm name.
    /// </summary>
    /// <param name="interpolation">The interpolation parameter value (OGC URI or short name).</param>
    /// <param name="gdalMethod">The GDAL resampling method name.</param>
    /// <param name="error">Error message if parsing fails.</param>
    /// <returns>True if successfully parsed, false otherwise.</returns>
    public static bool TryParseInterpolation(string? interpolation, out string gdalMethod, out string? error)
    {
        error = null;
        gdalMethod = "near"; // Default to nearest neighbor

        if (interpolation.IsNullOrWhiteSpace())
        {
            return true; // No interpolation specified, use default
        }

        if (InterpolationMapping.TryGetValue(interpolation, out var method))
        {
            gdalMethod = method;
            return true;
        }

        error = $"Interpolation method '{interpolation}' is not supported. " +
                "Supported methods: nearest-neighbor, linear, cubic, cubic-spline, average.";
        return false;
    }

    /// <summary>
    /// Validates that an interpolation method is supported.
    /// </summary>
    /// <param name="interpolation">The interpolation parameter value.</param>
    /// <returns>True if supported, false otherwise.</returns>
    public static bool IsSupported(string? interpolation)
    {
        if (interpolation.IsNullOrWhiteSpace())
        {
            return true; // No interpolation = use default = supported
        }

        return InterpolationMapping.ContainsKey(interpolation);
    }

    /// <summary>
    /// Gets the default interpolation method (nearest neighbor).
    /// </summary>
    public static string GetDefaultGdalMethod()
    {
        return "near";
    }

    /// <summary>
    /// Gets a user-friendly description of an interpolation method.
    /// </summary>
    public static string GetMethodDescription(string gdalMethod)
    {
        return gdalMethod switch
        {
            "near" => "Nearest neighbor (fast, preserves values, may appear blocky)",
            "bilinear" => "Bilinear (balanced speed and quality, smooth results)",
            "cubic" => "Cubic convolution (high quality, slower, very smooth)",
            "cubicspline" => "Cubic spline (highest quality, slowest, maximum smoothness)",
            "average" => "Average (good for downsampling, reduces aliasing)",
            "lanczos" => "Lanczos (excellent quality, balanced performance)",
            "mode" => "Mode (most common value, good for categorical data)",
            "max" => "Maximum (highest value in window)",
            "min" => "Minimum (lowest value in window)",
            "med" => "Median (middle value, good for noise reduction)",
            "q1" => "First quartile (25th percentile)",
            "q3" => "Third quartile (75th percentile)",
            _ => "Unknown interpolation method"
        };
    }
}
