// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

public sealed class FeatureContextResolver : IFeatureContextResolver
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IDataStoreProviderFactory _providerFactory;

    public FeatureContextResolver(IMetadataRegistry metadataRegistry, IDataStoreProviderFactory providerFactory)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    public async Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(serviceId);
        Guard.NotNullOrWhiteSpace(layerId);

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!snapshot.TryGetService(serviceId, out var service))
        {
            throw new ServiceNotFoundException(serviceId);
        }

        if (!snapshot.TryGetLayer(serviceId, layerId, out var layer))
        {
            throw new LayerNotFoundException(serviceId, layerId);
        }

        var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
            string.Equals(ds.Id, service.DataSourceId, StringComparison.OrdinalIgnoreCase));
        if (dataSource is null)
        {
            throw new DataSourceNotFoundException(service.DataSourceId);
        }

        var provider = _providerFactory.Create(dataSource.Provider);
        return new FeatureContext(snapshot, service, layer, dataSource, provider);
    }
}
