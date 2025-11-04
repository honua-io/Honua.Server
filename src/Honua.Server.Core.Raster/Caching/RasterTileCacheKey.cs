// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public readonly record struct RasterTileCacheKey
{
    public RasterTileCacheKey(
        string datasetId,
        string tileMatrixSetId,
        int zoom,
        int row,
        int column,
        string? styleId,
        string? format,
        bool transparent,
        int tileSize,
        string? time = null)
    {
        if (string.IsNullOrWhiteSpace(datasetId))
        {
            throw new ArgumentNullException(nameof(datasetId));
        }

        if (string.IsNullOrWhiteSpace(tileMatrixSetId))
        {
            throw new ArgumentNullException(nameof(tileMatrixSetId));
        }

        if (zoom < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        DatasetId = datasetId;
        TileMatrixSetId = tileMatrixSetId;
        Zoom = zoom;
        Row = row;
        Column = column;
        StyleId = string.IsNullOrWhiteSpace(styleId) ? "default" : styleId;
        Format = string.IsNullOrWhiteSpace(format) ? "image/png" : format;
        Transparent = transparent;
        TileSize = tileSize;
        Time = time;
    }

    public string DatasetId { get; }

    public string TileMatrixSetId { get; }

    public int Zoom { get; }

    public int Row { get; }

    public int Column { get; }

    public string StyleId { get; }

    public string Format { get; }

    public bool Transparent { get; }

    public int TileSize { get; }

    public string? Time { get; }

    public override string ToString()
    {
        return Cache.CacheKeyGenerator.GenerateVectorTileKey(
            DatasetId,
            TileMatrixSetId,
            Zoom,
            Column,
            Row,
            StyleId,
            Format,
            Transparent,
            TileSize,
            Time);
    }
}
