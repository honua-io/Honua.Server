// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace Honua.Server.Core.Raster;

/// <summary>
/// Provides caching for raster metadata to improve performance.
/// </summary>
public sealed class RasterMetadataCache : DisposableBase
{
    private readonly IMemoryCache cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RasterMetadataCache"/> class.
    /// </summary>
    /// <param name="cache">Optional memory cache instance. If null, a default cache is created.</param>
    public RasterMetadataCache(IMemoryCache? cache = null)
    {
        // Create a default memory cache with size limits if none provided
        this.cache = cache ?? new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000, // Limit to 1000 raster metadata entries
        });
    }

    /// <summary>
    /// Sets metadata for a raster URI in the cache.
    /// </summary>
    /// <param name="uri">The raster URI.</param>
    /// <param name="metadata">The metadata to cache.</param>
    public void SetMetadata(string uri, GeoRasterMetadata metadata)
    {
        this.ThrowIfDisposed();

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

        this.cache.Set(Normalize(uri), metadata, cacheOptions);
    }

    /// <summary>
    /// Tries to get metadata for a raster URI from the cache.
    /// </summary>
    /// <param name="uri">The raster URI.</param>
    /// <param name="metadata">The cached metadata if found.</param>
    /// <returns>True if metadata was found in cache; otherwise false.</returns>
    public bool TryGetMetadata(string? uri, out GeoRasterMetadata? metadata)
    {
        this.ThrowIfDisposed();

        metadata = null;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return this.cache.TryGetValue(Normalize(uri), out metadata);
    }

    private static string Normalize(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return parsed.AbsoluteUri;
        }

        return uri;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        // Dispose the cache if it was created internally
        if (this.cache is MemoryCache mc)
        {
            mc.Dispose();
        }
    }
}

/// <summary>
/// Represents metadata for a georeferenced raster.
/// </summary>
public sealed record GeoRasterMetadata
{
    /// <summary>
    /// Gets the raster width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the raster height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the pixel size in the X direction.
    /// </summary>
    public double? PixelSizeX { get; init; }

    /// <summary>
    /// Gets the pixel size in the Y direction.
    /// </summary>
    public double? PixelSizeY { get; init; }

    /// <summary>
    /// Gets the origin X coordinate.
    /// </summary>
    public double? OriginX { get; init; }

    /// <summary>
    /// Gets the origin Y coordinate.
    /// </summary>
    public double? OriginY { get; init; }

    /// <summary>
    /// Gets the spatial extent as [minX, minY, maxX, maxY].
    /// </summary>
    public double[]? Extent { get; init; }

    /// <summary>
    /// Gets the no-data value for the raster.
    /// </summary>
    public double? NoDataValue { get; init; }

    /// <summary>
    /// Gets the spatial reference system identifier.
    /// </summary>
    public string? SpatialReference { get; init; }

    /// <summary>
    /// Gets the band names.
    /// </summary>
    public IReadOnlyList<string> BandNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the band descriptions.
    /// </summary>
    public IReadOnlyList<string?> BandDescriptions { get; init; } = Array.Empty<string?>();

    /// <summary>
    /// Gets the no-data values for each band.
    /// </summary>
    public IReadOnlyList<double?> BandNoData { get; init; } = Array.Empty<double?>();
}
