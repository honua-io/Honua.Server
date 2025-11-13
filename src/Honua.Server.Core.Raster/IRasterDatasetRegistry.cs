// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Raster;

/// <summary>
/// Provides a registry for managing and querying raster datasets.
/// </summary>
public interface IRasterDatasetRegistry
{
    /// <summary>
    /// Gets all raster datasets.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All raster datasets.</returns>
    ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all raster datasets for a specific service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All datasets for the service.</returns>
    ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a raster dataset by identifier.
    /// </summary>
    /// <param name="datasetId">The dataset identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dataset if found; otherwise null.</returns>
    ValueTask<RasterDatasetDefinition?> FindAsync(string datasetId, CancellationToken cancellationToken = default);
}
