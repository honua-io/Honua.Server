// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Stac;

public interface IRasterStacCatalogSynchronizer
{
    Task SynchronizeAllAsync(CancellationToken cancellationToken = default);
    Task SynchronizeDatasetsAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default);
    Task SynchronizeServiceLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}

public sealed class RasterStacCatalogSynchronizer : DisposableBase, IRasterStacCatalogSynchronizer
{
    private const int ExistingScanPageSize = 128;

    private readonly IStacCatalogStore _store;
    private readonly IOptionsMonitor<StacCatalogOptions> _stacOptions;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly RasterStacCatalogBuilder _builder;
    private readonly VectorStacCatalogBuilder _vectorBuilder;
    private readonly ILogger<RasterStacCatalogSynchronizer> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public RasterStacCatalogSynchronizer(
        IStacCatalogStore store,
        IOptionsMonitor<StacCatalogOptions> stacOptions,
        IMetadataRegistry metadataRegistry,
        RasterStacCatalogBuilder builder,
        VectorStacCatalogBuilder vectorBuilder,
        ILogger<RasterStacCatalogSynchronizer> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stacOptions = stacOptions ?? throw new ArgumentNullException(nameof(stacOptions));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _vectorBuilder = vectorBuilder ?? throw new ArgumentNullException(nameof(vectorBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SynchronizeAllAsync(CancellationToken cancellationToken = default)
    {
        return SynchronizeInternalAsync(
            snapshot => snapshot.RasterDatasets,
            expectedIds: null,
            pruneUnspecifiedCollections: true,
            serviceIdFilter: null,
            layerIdFilter: null,
            cancellationToken);
    }

    public Task SynchronizeDatasetsAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(datasetIds);

        var explicitIds = new HashSet<string>(datasetIds, StringComparer.OrdinalIgnoreCase);

        return SynchronizeInternalAsync(
            snapshot =>
            {
                if (explicitIds.Count == 0)
                {
                    return Array.Empty<RasterDatasetDefinition>();
                }

                return snapshot.RasterDatasets
                    .Where(dataset => explicitIds.Contains(dataset.Id))
                    .ToList();
            },
            expectedIds: explicitIds,
            pruneUnspecifiedCollections: false,
            serviceIdFilter: null,
            layerIdFilter: null,
            cancellationToken);
    }

    public Task SynchronizeServiceLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        return SynchronizeInternalAsync(
            snapshot => snapshot.RasterDatasets
                .Where(dataset => string.Equals(dataset.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(dataset.LayerId, layerId, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            expectedIds: null,
            pruneUnspecifiedCollections: false,
            serviceIdFilter: serviceId,
            layerIdFilter: layerId,
            cancellationToken);
    }

    private async Task SynchronizeInternalAsync(
        Func<MetadataSnapshot, IReadOnlyList<RasterDatasetDefinition>> selector,
        HashSet<string>? expectedIds,
        bool pruneUnspecifiedCollections,
        string? serviceIdFilter,
        string? layerIdFilter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!IsEnabled())
        {
            _logger.LogDebug("STAC catalog disabled; skipping synchronization request.");
            return;
        }

        await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var datasets = selector(snapshot);
        var expectedSet = expectedIds is null
            ? new HashSet<string>(datasets.Select(dataset => dataset.Id), StringComparer.OrdinalIgnoreCase)
            : expectedIds;

        var vectorLayers = Array.Empty<(LayerDefinition Layer, ServiceDefinition Service)>();
        if (expectedIds is null || !string.IsNullOrWhiteSpace(serviceIdFilter) || !string.IsNullOrWhiteSpace(layerIdFilter))
        {
            vectorLayers = CollectVectorLayers(snapshot, serviceIdFilter, layerIdFilter)
                .Where(tuple => _vectorBuilder.Supports(tuple.Layer))
                .ToArray();

            foreach (var (layer, _) in vectorLayers)
            {
                expectedSet.Add(_vectorBuilder.GetCollectionId(layer));
            }
        }

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dataset in datasets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_builder.Supports(dataset))
                {
                    _logger.LogDebug("Skipping dataset {DatasetId} because the STAC builder does not support source type {SourceType}.", dataset.Id, dataset.Source.Type);
                    continue;
                }

                try
                {
                    await SynchronizeDatasetAsync(dataset, snapshot, cancellationToken).ConfigureAwait(false);
                    processedIds.Add(dataset.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to synchronize STAC metadata for dataset {DatasetId}.", dataset.Id);
                    throw;
                }
            }

            foreach (var (layer, service) in vectorLayers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var collection = _vectorBuilder.Build(layer, service, snapshot);
                var items = _vectorBuilder.BuildItems(layer, service, snapshot);

                await _store.UpsertCollectionAsync(collection, expectedETag: null, cancellationToken).ConfigureAwait(false);

                var existingItemIds = await LoadExistingItemIdsAsync(collection.Id, cancellationToken).ConfigureAwait(false);
                var newItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    await _store.UpsertItemAsync(item, expectedETag: null, cancellationToken).ConfigureAwait(false);
                    newItemIds.Add(item.Id);
                }

                foreach (var existingId in existingItemIds)
                {
                    if (!newItemIds.Contains(existingId))
                    {
                        await _store.DeleteItemAsync(collection.Id, existingId, cancellationToken).ConfigureAwait(false);
                    }
                }

                processedIds.Add(collection.Id);
            }

            if ((expectedSet.Count > 0 || pruneUnspecifiedCollections) && (processedIds.Count > 0 || expectedIds is not null))
            {
                await PruneCollectionsAsync(expectedSet, processedIds, pruneUnspecifiedCollections, cancellationToken).ConfigureAwait(false);
            }
            else if (expectedSet.Count == 0 && pruneUnspecifiedCollections)
            {
                await PruneCollectionsAsync(expectedSet, processedIds, pruneUnspecifiedCollections, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static List<(LayerDefinition Layer, ServiceDefinition Service)> CollectVectorLayers(
        MetadataSnapshot snapshot,
        string? serviceIdFilter,
        string? layerIdFilter)
    {
        var results = new List<(LayerDefinition, ServiceDefinition)>();

        foreach (var service in snapshot.Services)
        {
            if (!string.IsNullOrWhiteSpace(serviceIdFilter) &&
                !string.Equals(service.Id, serviceIdFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var layer in service.Layers)
            {
                if (!string.IsNullOrWhiteSpace(layerIdFilter) &&
                    !string.Equals(layer.Id, layerIdFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add((layer, service));
            }
        }

        return results;
    }

    private async Task SynchronizeDatasetAsync(RasterDatasetDefinition dataset, MetadataSnapshot snapshot, CancellationToken cancellationToken)
    {
        var (collection, items) = _builder.Build(dataset, snapshot);
        var existingIds = await LoadExistingItemIdsAsync(collection.Id, cancellationToken).ConfigureAwait(false);

        await _store.UpsertCollectionAsync(collection, expectedETag: null, cancellationToken).ConfigureAwait(false);

        var newIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            await _store.UpsertItemAsync(item, expectedETag: null, cancellationToken).ConfigureAwait(false);
            newIds.Add(item.Id);
        }

        foreach (var existingId in existingIds)
        {
            if (!newIds.Contains(existingId))
            {
                await _store.DeleteItemAsync(collection.Id, existingId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<HashSet<string>> LoadExistingItemIdsAsync(string collectionId, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? pageToken = null;

        while (true)
        {
            var batch = await _store.ListItemsAsync(collectionId, ExistingScanPageSize, pageToken, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var item in batch)
            {
                ids.Add(item.Id);
            }

            if (batch.Count < ExistingScanPageSize)
            {
                break;
            }

            pageToken = batch[^1].Id;
        }

        return ids;
    }

    private async Task PruneCollectionsAsync(
        HashSet<string> expectedIds,
        HashSet<string> processedIds,
        bool pruneUnspecifiedCollections,
        CancellationToken cancellationToken)
    {
        var collections = await _store.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var collection in collections)
        {
            var isExpected = expectedIds.Contains(collection.Id);
            var wasProcessed = processedIds.Contains(collection.Id);

            if (isExpected)
            {
                if (!wasProcessed)
                {
                    await _store.DeleteCollectionAsync(collection.Id, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (pruneUnspecifiedCollections)
            {
                await _store.DeleteCollectionAsync(collection.Id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool IsEnabled()
    {
        return _stacOptions.CurrentValue.Enabled;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _syncLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
