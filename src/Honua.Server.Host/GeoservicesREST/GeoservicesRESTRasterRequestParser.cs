// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTRasterRequestParser
{
    public static bool TryParseBoundingBox(string? value, out double[] bbox)
    {
        var (parsed, error) = QueryParsingHelpers.ParseBoundingBox(value);
        if (parsed is null || error is not null)
        {
            bbox = Array.Empty<double>();
            return false;
        }

        bbox = parsed;
        return true;
    }

    public static bool TryParseSize(string? value, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = QueryParsingHelpers.ParseCsv(value);
        if (parts.Count != 2)
        {
            return false;
        }

        return parts[0].TryParseInt(out width)
            && parts[1].TryParseInt(out height)
            && width > 0
            && height > 0;
    }

    public static string ResolveRasterFormat(HttpRequest request)
    {
        Guard.NotNull(request);

        string? raw = null;
        if (request.Query.TryGetValue("format", out var formatValues))
        {
            raw = formatValues.ToString();
        }
        else if (request.Query.TryGetValue("f", out var alternateValues))
        {
            raw = alternateValues.ToString();
        }

        return RasterFormatHelper.Normalize(raw);
    }

    public static bool TryResolveStyle(RasterDatasetDefinition dataset, string? requestedStyleId, out string styleId, out string? unresolvedStyle)
    {
        var (success, resolvedStyleId, error) = StyleResolutionHelper.TryResolveRasterStyleId(dataset, requestedStyleId);
        styleId = resolvedStyleId ?? string.Empty;
        unresolvedStyle = success ? null : requestedStyleId;
        return success;
    }
}
