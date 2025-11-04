// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Raster;

public static class RasterFormatHelper
{
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
            _ => "png"
        };
    }

    public static string GetContentType(string normalizedFormat)
    {
        return normalizedFormat.ToLowerInvariant() switch
        {
            "jpeg" => "image/jpeg",
            "mvt" => "application/vnd.mapbox-vector-tile",
            "pmtiles" => "application/vnd.pmtiles",
            _ => "image/png"
        };
    }

    public static bool IsVectorFormat(string normalizedFormat)
    {
        return string.Equals(normalizedFormat, "mvt", StringComparison.OrdinalIgnoreCase);
    }
}
