// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Raster;

public sealed class RasterDatasetRegistry : IRasterDatasetRegistry
{
    private static readonly IReadOnlyList<RasterDatasetDefinition> EmptyDatasets = Array.Empty<RasterDatasetDefinition>();

    private readonly IMetadataRegistry _metadataRegistry;
    private readonly object _sync = new();

    private MetadataSnapshot? _snapshot;
    private IReadOnlyList<RasterDatasetDefinition> _datasets = EmptyDatasets;
    private IReadOnlyDictionary<string, RasterDatasetDefinition> _datasetIndex = new Dictionary<string, RasterDatasetDefinition>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IReadOnlyList<RasterDatasetDefinition>> _serviceIndex = new Dictionary<string, IReadOnlyList<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);

    public RasterDatasetRegistry(IMetadataRegistry metadataRegistry)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
    }

    public async ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return EnsureIndexes(snapshot).Datasets;
    }

    public async ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (_, _, serviceIndex) = EnsureIndexes(snapshot);

        return serviceIndex.TryGetValue(serviceId, out var datasets) ? datasets : EmptyDatasets;
    }

    public async ValueTask<RasterDatasetDefinition?> FindAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetId);

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (index, _, _) = EnsureIndexes(snapshot);

        return index.TryGetValue(datasetId, out var dataset) ? dataset : null;
    }

    private (IReadOnlyDictionary<string, RasterDatasetDefinition> Index, IReadOnlyList<RasterDatasetDefinition> Datasets, IReadOnlyDictionary<string, IReadOnlyList<RasterDatasetDefinition>> Services) EnsureIndexes(MetadataSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot, Volatile.Read(ref _snapshot)))
        {
            lock (_sync)
            {
                if (!ReferenceEquals(snapshot, _snapshot))
                {
                    BuildIndexes(snapshot);
                    _snapshot = snapshot;
                }
            }
        }

        return (_datasetIndex, _datasets, _serviceIndex);
    }

    private void BuildIndexes(MetadataSnapshot snapshot)
    {
        var datasets = snapshot.RasterDatasets;
        if (datasets.Count == 0)
        {
            _datasets = EmptyDatasets;
            _datasetIndex = new Dictionary<string, RasterDatasetDefinition>(StringComparer.OrdinalIgnoreCase);
            _serviceIndex = new Dictionary<string, IReadOnlyList<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var datasetArray = datasets.Where(d => d is not null).ToArray();
        _datasets = new ReadOnlyCollection<RasterDatasetDefinition>(datasetArray);

        var datasetIndex = new Dictionary<string, RasterDatasetDefinition>(StringComparer.OrdinalIgnoreCase);
        var serviceBuckets = new Dictionary<string, List<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataset in datasetArray)
        {
            datasetIndex[dataset.Id] = dataset;

            if (!string.IsNullOrWhiteSpace(dataset.ServiceId))
            {
                if (!serviceBuckets.TryGetValue(dataset.ServiceId, out var list))
                {
                    list = new List<RasterDatasetDefinition>();
                    serviceBuckets[dataset.ServiceId] = list;
                }

                list.Add(dataset);
            }
        }

        var serviceIndex = new Dictionary<string, IReadOnlyList<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (serviceId, list) in serviceBuckets)
        {
            serviceIndex[serviceId] = new ReadOnlyCollection<RasterDatasetDefinition>(list);
        }

        _datasetIndex = datasetIndex;
        _serviceIndex = serviceIndex;
    }
}
