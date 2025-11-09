// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

public static class CrsHelper
{
    public const int Wgs84 = 4326;
    public const int WebMercator = 3857;
    public const string DefaultCrsIdentifier = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";

    public static int ParseCrs(string? crs)
    {
        var normalized = NormalizeIdentifier(crs);
        if (string.Equals(normalized, DefaultCrsIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            return Wgs84;
        }

        var token = GetLastToken(normalized);
        if (token.Equals("CRS84H", StringComparison.OrdinalIgnoreCase))
        {
            // CRS84h is WGS84 with ellipsoidal height (3D support)
            // Return WGS84 SRID but preserve 3D coordinates during transformation
            return Wgs84;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var srid))
        {
            if (srid == 84)
            {
                return Wgs84;
            }

            if (srid == 4326)
            {
                return Wgs84;
            }

            return srid;
        }

        return Wgs84;
    }

    public static string NormalizeIdentifier(string? crs)
    {
        if (crs.IsNullOrWhiteSpace())
        {
            return DefaultCrsIdentifier;
        }

        var trimmed = crs.Trim();
        var token = GetLastToken(trimmed);
        var upperToken = token.ToUpperInvariant();

        if (upperToken == "CRS84" || upperToken == "EPSG:4326" || upperToken == "OGC:CRS84" || upperToken == "4326")
        {
            return DefaultCrsIdentifier;
        }

        if (upperToken == "CRS84H")
        {
            return "http://www.opengis.net/def/crs/OGC/0/CRS84h";
        }

        if (upperToken.StartsWith("EPSG:"))
        {
            upperToken = upperToken.Substring("EPSG:".Length);
        }

        if (int.TryParse(upperToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var srid))
        {
            if (srid == Wgs84)
            {
                return DefaultCrsIdentifier;
            }

            return $"http://www.opengis.net/def/crs/EPSG/0/{srid}";
        }

        return trimmed;
    }

    private static string GetLastToken(string input)
    {
        var trimmed = (input ?? string.Empty).Trim();
        if (trimmed.IsNullOrEmpty())
        {
            return string.Empty;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (uri.Segments.Length > 0)
            {
                var segment = uri.Segments[^1].Trim('/')
                    .Trim();
                if (segment.HasValue())
                {
                    return segment;
                }
            }
        }

        var tokens = trimmed.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length == 0 ? trimmed : tokens[^1];
    }
}
