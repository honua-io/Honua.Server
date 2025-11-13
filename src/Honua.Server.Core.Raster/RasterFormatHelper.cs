// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Raster;

/// <summary>
/// Helper methods for normalizing and working with raster format strings.
/// </summary>
public static class RasterFormatHelper
{
    /// <summary>
    /// Normalizes a format string to a standard format identifier.
    /// </summary>
    /// <param name="format">The format string to normalize.</param>
    /// <returns>The normalized format identifier.</returns>
    public static string Normalize(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "png";
        }

        var value = format.Trim().ToLowerInvariant();

        return value switch
        {
            "png" or "png8" or "png24" or "png32" or "image/png" => "png",
            "jpg" or "jpeg" or "image/jpg" or "image/jpeg" => "jpeg",
            "mvt" or "pbf" or "application/vnd.mapbox-vector-tile" or "application/x-protobuf" => "mvt",
            "pmtiles" or "application/vnd.pmtiles" => "pmtiles",
            _ => "png",
        };
    }

    /// <summary>
    /// Gets the MIME content type for a normalized format identifier.
    /// </summary>
    /// <param name="normalizedFormat">The normalized format identifier.</param>
    /// <returns>The MIME content type.</returns>
    public static string GetContentType(string normalizedFormat)
    {
        return normalizedFormat.ToLowerInvariant() switch
        {
            "jpeg" => "image/jpeg",
            "mvt" => "application/vnd.mapbox-vector-tile",
            "pmtiles" => "application/vnd.pmtiles",
            _ => "image/png",
        };
    }

    /// <summary>
    /// Determines whether a format is a vector format.
    /// </summary>
    /// <param name="normalizedFormat">The normalized format identifier.</param>
    /// <returns>True if the format is a vector format; otherwise false.</returns>
    public static bool IsVectorFormat(string normalizedFormat)
    {
        return string.Equals(normalizedFormat, "mvt", StringComparison.OrdinalIgnoreCase);
    }
}
