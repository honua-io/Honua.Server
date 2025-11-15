// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Services;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Services for tile caching and performance optimization.
/// </summary>
public sealed record TileCachingServices
{
    /// <summary>
    /// Provides access to cached tiles and cache management.
    /// </summary>
    public required IRasterTileCacheProvider CacheProvider { get; init; }

    /// <summary>
    /// Records cache hit/miss metrics for performance monitoring.
    /// </summary>
    public required IRasterTileCacheMetrics CacheMetrics { get; init; }

    /// <summary>
    /// Generates cache control headers and ETags.
    /// </summary>
    public required OgcCacheHeaderService CacheHeaders { get; init; }
}
