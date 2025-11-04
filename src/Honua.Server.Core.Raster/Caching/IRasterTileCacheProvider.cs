// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public interface IRasterTileCacheProvider
{
    ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default);

    Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default);

    Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default);

    Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default);
}
