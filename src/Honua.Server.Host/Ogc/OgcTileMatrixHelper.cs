// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Honua.Server.Host.Ogc;

public static class OgcTileMatrixHelper
{
    public const string WorldCrs84QuadId = "WorldCRS84Quad";
    public const string WorldCrs84QuadUri = "http://www.opengis.net/def/tms/OGC/1.0/WorldCRS84Quad";
    public const string WorldCrs84QuadCrs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";
    public const string WorldWebMercatorQuadId = "WorldWebMercatorQuad";
    public const string WorldWebMercatorQuadUri = "http://www.opengis.net/def/tms/OGC/1.0/WorldWebMercatorQuad";
    public const string WorldWebMercatorQuadCrs = "http://www.opengis.net/def/crs/EPSG/0/3857";

    private const double MinLongitude = -180d;
    private const double MaxLongitude = 180d;
    private const double MinLatitude = -90d;
    private const double MaxLatitude = 90d;
    private const double WebMercatorMin = -20037508.3427892d;
    private const double WebMercatorMax = 20037508.3427892d;
    private const int DefaultMinZoom = 0;
    private const int DefaultMaxZoom = 14;
    private const int TileSize = 256;

    public static (int MinZoom, int MaxZoom) ResolveZoomRange(IReadOnlyList<int> zoomLevels)
    {
        if (zoomLevels is { Count: > 0 })
        {
            var min = int.MaxValue;
            var max = int.MinValue;
            foreach (var level in zoomLevels)
            {
                if (level < 0)
                {
                    continue;
                }

                if (level < min)
                {
                    min = level;
                }

                if (level > max)
                {
                    max = level;
                }
            }

            if (min != int.MaxValue && max != int.MinValue)
            {
                return (min, max);
            }
        }

        return (DefaultMinZoom, DefaultMaxZoom);
    }

    public static bool IsSupportedMatrixSet(string? identifier)
        => IsWorldCrs84Quad(identifier) || IsWorldWebMercatorQuad(identifier);

    public static bool IsWorldCrs84Quad(string? identifier)
        => string.Equals(identifier, WorldCrs84QuadId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, WorldCrs84QuadUri, StringComparison.OrdinalIgnoreCase);

    public static bool IsWorldWebMercatorQuad(string? identifier)
        => string.Equals(identifier, WorldWebMercatorQuadId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, WorldWebMercatorQuadUri, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseZoom(string value, out int zoom)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out zoom) && zoom >= 0;

    public static bool IsValidTileCoordinate(int zoom, int row, int column)
    {
        if (zoom < 0)
        {
            return false;
        }

        var dimension = 1 << zoom;
        return row >= 0 && row < dimension && column >= 0 && column < dimension;
    }

    public static double[] GetBoundingBox(string tileMatrixSetId, int zoom, int row, int column)
    {
        if (IsWorldWebMercatorQuad(tileMatrixSetId))
        {
            return GetWebMercatorBoundingBox(zoom, row, column);
        }

        var tilesPerAxis = 1 << zoom;
        var tileWidth = (MaxLongitude - MinLongitude) / tilesPerAxis;
        var tileHeight = (MaxLatitude - MinLatitude) / tilesPerAxis;

        var minX = MinLongitude + column * tileWidth;
        var maxX = minX + tileWidth;

        var maxY = MaxLatitude - row * tileHeight;
        var minY = maxY - tileHeight;

        return new[] { minX, minY, maxX, maxY };
    }

    /// <summary>
    /// Detects if a bounding box crosses the antimeridian (180/-180 longitude line).
    /// A bbox crosses the antimeridian when minX > maxX in the [-180, 180] range.
    /// </summary>
    public static bool CrossesAntimeridian(double minX, double maxX)
    {
        return minX > maxX;
    }

    /// <summary>
    /// Splits an antimeridian-crossing bbox into two separate bboxes.
    /// Returns an array of one or two bboxes depending on whether the input crosses the antimeridian.
    /// </summary>
    public static double[][] SplitAntimeridianBbox(double minX, double minY, double maxX, double maxY)
    {
        if (!CrossesAntimeridian(minX, maxX))
        {
            // Not crossing - return single bbox
            return new[] { new[] { minX, minY, maxX, maxY } };
        }

        // Crossing - split into western and eastern hemispheres
        // Western bbox: [minX, minY, 180, maxY]
        // Eastern bbox: [-180, minY, maxX, maxY]
        return new[]
        {
            new[] { minX, minY, MaxLongitude, maxY },
            new[] { MinLongitude, minY, maxX, maxY }
        };
    }

    /// <summary>
    /// Normalizes a longitude value to the [-180, 180] range.
    /// </summary>
    public static double NormalizeLongitude(double longitude)
    {
        // Wrap to [-180, 180]
        while (longitude > MaxLongitude)
        {
            longitude -= 360.0;
        }
        while (longitude < MinLongitude)
        {
            longitude += 360.0;
        }
        return longitude;
    }

    private static double[] GetWebMercatorBoundingBox(int zoom, int row, int column)
    {
        var tilesPerAxis = 1 << zoom;
        var tileWidth = (WebMercatorMax - WebMercatorMin) / tilesPerAxis;

        var minX = WebMercatorMin + column * tileWidth;
        var maxX = minX + tileWidth;

        var maxY = WebMercatorMax - row * tileWidth;
        var minY = maxY - tileWidth;

        return new[] { minX, minY, maxX, maxY };
    }

    public static IReadOnlyList<object> BuildTileMatrices(string tileMatrixSetId, int minZoom, int maxZoom)
    {
        if (minZoom < 0)
        {
            minZoom = 0;
        }

        if (maxZoom < minZoom)
        {
            maxZoom = minZoom;
        }

        var matrices = new List<object>(Math.Max(1, maxZoom - minZoom + 1));
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var matrixSize = 1 << zoom;
            var resolution = IsWorldWebMercatorQuad(tileMatrixSetId)
                ? (WebMercatorMax - WebMercatorMin) / (TileSize * matrixSize)
                : (MaxLongitude - MinLongitude) / (TileSize * matrixSize);
            var scaleDenominator = resolution / 0.00028d;

            matrices.Add(new
            {
                id = zoom.ToString(CultureInfo.InvariantCulture),
                scaleDenominator,
                topLeftCorner = IsWorldWebMercatorQuad(tileMatrixSetId)
                    ? new[] { WebMercatorMin, WebMercatorMax }
                    : new[] { MinLongitude, MaxLatitude },
                tileWidth = TileSize,
                tileHeight = TileSize,
                matrixWidth = matrixSize,
                matrixHeight = matrixSize
            });
        }

        return new ReadOnlyCollection<object>(matrices);
    }

    public static (int MinRow, int MaxRow, int MinColumn, int MaxColumn) GetTileRange(string tileMatrixSetId, int zoom, double minX, double minY, double maxX, double maxY)
    {
        if (zoom < 0)
        {
            return (0, -1, 0, -1);
        }

        var maxIndex = (1 << zoom) - 1;
        if (IsWorldWebMercatorQuad(tileMatrixSetId))
        {
            var span = WebMercatorMax - WebMercatorMin;
            var minColumn = ClampIndex((int)Math.Floor((minX - WebMercatorMin) / span * (1 << zoom)), maxIndex);
            var maxColumn = ClampIndex((int)Math.Floor((maxX - WebMercatorMin) / span * (1 << zoom)), maxIndex);
            var minRow = ClampIndex((int)Math.Floor((WebMercatorMax - maxY) / span * (1 << zoom)), maxIndex);
            var maxRow = ClampIndex((int)Math.Floor((WebMercatorMax - minY) / span * (1 << zoom)), maxIndex);
            NormalizeRange(ref minRow, ref maxRow);

            // BUG FIX #3: Don't normalize column range if it crosses the antimeridian
            // When minColumn > maxColumn, the extent wraps around ±180°
            // Return the original indices to indicate wraparound to the caller
            if (minColumn <= maxColumn)
            {
                NormalizeRange(ref minColumn, ref maxColumn);
            }

            return (minRow, maxRow, minColumn, maxColumn);
        }

        var width = MaxLongitude - MinLongitude;
        var height = MaxLatitude - MinLatitude;
        var minCol = ClampIndex((int)Math.Floor((minX - MinLongitude) / width * (1 << zoom)), maxIndex);
        var maxCol = ClampIndex((int)Math.Floor((maxX - MinLongitude) / width * (1 << zoom)), maxIndex);
        var minRowCrs84 = ClampIndex((int)Math.Floor((MaxLatitude - maxY) / height * (1 << zoom)), maxIndex);
        var maxRowCrs84 = ClampIndex((int)Math.Floor((MaxLatitude - minY) / height * (1 << zoom)), maxIndex);
        NormalizeRange(ref minRowCrs84, ref maxRowCrs84);

        // BUG FIX #3: Don't normalize column range if it crosses the antimeridian
        // When minCol > maxCol, the extent wraps around ±180°
        // Return the original indices to indicate wraparound to the caller
        if (minCol <= maxCol)
        {
            NormalizeRange(ref minCol, ref maxCol);
        }

        return (minRowCrs84, maxRowCrs84, minCol, maxCol);
    }

    private static void NormalizeRange(ref int min, ref int max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }
    }

    private static int ClampIndex(int value, int maxIndex)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > maxIndex)
        {
            return maxIndex;
        }

        return value;
    }
}
