// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Factory interface for creating raster tile cache provider instances.
/// This separates the complex creation logic from the DI registration.
/// </summary>
public interface IRasterTileCacheProviderFactory
{
    /// <summary>
    /// Creates a raster tile cache provider based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The raster tile cache configuration.</param>
    /// <returns>An instance of IRasterTileCacheProvider.</returns>
    IRasterTileCacheProvider Create(RasterTileCacheConfiguration configuration);
}
