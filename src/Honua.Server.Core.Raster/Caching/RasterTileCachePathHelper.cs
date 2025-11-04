// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;

using Honua.Server.Core.Caching;
namespace Honua.Server.Core.Raster.Caching;

public static class RasterTileCachePathHelper
{
    public static string GetRelativePath(RasterTileCacheKey key, char separator)
    {
        var dataset = CacheKeyNormalizer.SanitizeForFilesystem(key.DatasetId);
        var matrix = CacheKeyNormalizer.SanitizeForFilesystem(key.TileMatrixSetId);
        var style = CacheKeyNormalizer.SanitizeForFilesystem(key.StyleId);
        var variant = BuildVariantSegment(key);
        var zoom = key.Zoom.ToString(CultureInfo.InvariantCulture);
        var column = key.Column.ToString(CultureInfo.InvariantCulture);
        var rowFileName = string.Create(CultureInfo.InvariantCulture, $"{key.Row}.{ResolveExtension(key.Format)}");

        // Build path segments - include time dimension if present
        var segments = string.IsNullOrWhiteSpace(key.Time)
            ? new[] { dataset, matrix, style, variant, zoom, column, rowFileName }
            : new[] { dataset, matrix, style, variant, CacheKeyNormalizer.SanitizeForFilesystem(key.Time), zoom, column, rowFileName };

        return string.Join(separator, segments);
    }

    public static string GetDatasetPrefix(string datasetId, char separator)
        => CacheKeyNormalizer.SanitizeForFilesystem(datasetId) + separator;

    public static string ResolveExtension(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "bin";
        }

        if (string.Equals(format, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return "png";
        }

        if (string.Equals(format, "image/jpeg", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return "jpg";
        }

        if (string.Equals(format, "image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return "webp";
        }

        var sanitized = format.Replace('/', '-').Replace('+', '-');
        return CacheKeyNormalizer.SanitizeForFilesystem(sanitized);
    }

    public static string ResolveFormatToken(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "png";
        }

        if (string.Equals(format, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return "png";
        }

        if (string.Equals(format, "image/jpeg", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return "jpeg";
        }

        if (string.Equals(format, "image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return "webp";
        }

        var sanitized = format.Replace('/', '-').Replace('+', '-');
        return CacheKeyNormalizer.SanitizeForFilesystem(sanitized);
    }

    private static string BuildVariantSegment(RasterTileCacheKey key)
    {
        var token = ResolveFormatToken(key.Format);
        var transparency = key.Transparent ? "alpha" : "opaque";
        return string.Create(CultureInfo.InvariantCulture, $"{token}-{key.TileSize}-{transparency}");
    }
}
