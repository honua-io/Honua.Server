// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Utilities;

internal static class CrsNormalizationHelper
{
    public static string NormalizeForWms(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "EPSG:4326";
        }

        var trimmed = value.Trim();
        var normalized = CrsHelper.NormalizeIdentifier(trimmed);

        if (string.Equals(normalized, CrsHelper.DefaultCrsIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Contains("CRS84", StringComparison.OrdinalIgnoreCase)
                ? "CRS:84"
                : "EPSG:4326";
        }

        var srid = CrsHelper.ParseCrs(normalized);
        return $"EPSG:{srid}".ToUpperInvariant();
    }

    public static string NormalizeIdentifier(string? value)
    {
        return CrsHelper.NormalizeIdentifier(value);
    }
}
