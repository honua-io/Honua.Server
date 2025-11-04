// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace Honua.Server.Core.Raster;

public sealed class RasterMetadataCache : DisposableBase
{
    private readonly IMemoryCache _cache;

    public RasterMetadataCache(IMemoryCache? cache = null)
    {
        // Create a default memory cache with size limits if none provided
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000 // Limit to 1000 raster metadata entries
        });
    }

    public void SetMetadata(string uri, GeoRasterMetadata metadata)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var cacheOptions = CacheOptionsBuilder.ForMetadata()
            .WithSize(1)
            .BuildMemory();

        _cache.Set(Normalize(uri), metadata, cacheOptions);
    }

    public bool TryGetMetadata(string? uri, out GeoRasterMetadata? metadata)
    {
        ThrowIfDisposed();

        metadata = null;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return _cache.TryGetValue(Normalize(uri), out metadata);
    }

    private static string Normalize(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return parsed.AbsoluteUri;
        }

        return uri;
    }

    protected override void DisposeCore()
    {
        // Dispose the cache if it was created internally
        if (_cache is MemoryCache mc)
        {
            mc.Dispose();
        }
    }
}

public sealed record GeoRasterMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double? PixelSizeX { get; init; }
    public double? PixelSizeY { get; init; }
    public double? OriginX { get; init; }
    public double? OriginY { get; init; }
    public double[]? Extent { get; init; }
    public double? NoDataValue { get; init; }
    public string? SpatialReference { get; init; }
    public IReadOnlyList<string> BandNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string?> BandDescriptions { get; init; } = Array.Empty<string?>();
    public IReadOnlyList<double?> BandNoData { get; init; } = Array.Empty<double?>();
}
