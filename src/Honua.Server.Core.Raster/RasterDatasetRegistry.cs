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

/// <summary>
/// Provides a registry for raster dataset definitions with efficient lookup by ID or service.
/// </summary>
public sealed class RasterDatasetRegistry : IRasterDatasetRegistry
{
    private static readonly IReadOnlyList<RasterDatasetDefinition> EmptyDatasets = Array.Empty<RasterDatasetDefinition>();

    private readonly IMetadataRegistry metadataRegistry;
    private readonly object sync = new();

    private MetadataSnapshot? snapshot;
    private IReadOnlyList<RasterDatasetDefinition> datasets = EmptyDatasets;
    private IReadOnlyDictionary<string, RasterDatasetDefinition> datasetIndex = new Dictionary<string, RasterDatasetDefinition>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IReadOnlyList<RasterDatasetDefinition>> serviceIndex = new Dictionary<string, IReadOnlyList<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="RasterDatasetRegistry"/> class.
    /// </summary>
    /// <param name="metadataRegistry">The metadata registry.</param>
    public RasterDatasetRegistry(IMetadataRegistry metadataRegistry)
    {
        this.metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
    }

    /// <summary>
    /// Gets all raster datasets.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All raster datasets.</returns>
    public async ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return this.EnsureIndexes(snapshot).Datasets;
    }

    /// <summary>
    /// Gets all raster datasets for a specific service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All datasets for the service.</returns>
    public async ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);

        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (_, _, serviceIndex) = this.EnsureIndexes(snapshot);

        return serviceIndex.TryGetValue(serviceId, out var datasets) ? datasets : EmptyDatasets;
    }

    /// <summary>
    /// Finds a raster dataset by identifier.
    /// </summary>
    /// <param name="datasetId">The dataset identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dataset if found; otherwise null.</returns>
    public async ValueTask<RasterDatasetDefinition?> FindAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetId);

        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var (index, _, _) = this.EnsureIndexes(snapshot);

        return index.TryGetValue(datasetId, out var dataset) ? dataset : null;
    }

    private (IReadOnlyDictionary<string, RasterDatasetDefinition> Index, IReadOnlyList<RasterDatasetDefinition> Datasets, IReadOnlyDictionary<string, IReadOnlyList<RasterDatasetDefinition>> Services) EnsureIndexes(MetadataSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot, Volatile.Read(ref this.snapshot)))
        {
            lock (this.sync)
            {
                if (!ReferenceEquals(snapshot, this.snapshot))
                {
                    this.BuildIndexes(snapshot);
                    this.snapshot = snapshot;
                }
            }
        }

        return (this.datasetIndex, this.datasets, this.serviceIndex);
    }

    private void BuildIndexes(MetadataSnapshot snapshot)
    {
        var datasets = snapshot.RasterDatasets;
        if (datasets.Count == 0)
        {
            this.datasets = EmptyDatasets;
            this.datasetIndex = new Dictionary<string, RasterDatasetDefinition>(StringComparer.OrdinalIgnoreCase);
            this.serviceIndex = new Dictionary<string, IReadOnlyList<RasterDatasetDefinition>>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var datasetArray = datasets.Where(d => d is not null).ToArray();
        this.datasets = new ReadOnlyCollection<RasterDatasetDefinition>(datasetArray);

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

        this.datasetIndex = datasetIndex;
        this.serviceIndex = serviceIndex;
    }
}
