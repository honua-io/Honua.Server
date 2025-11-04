// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Synchronizes vector layer metadata to the STAC catalog.
/// Handles automatic STAC collection and item generation from vector layer definitions.
/// </summary>
public interface IVectorStacCatalogSynchronizer
{
    /// <summary>
    /// Synchronizes all STAC-enabled vector layers to the catalog.
    /// </summary>
    Task SynchronizeAllVectorLayersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes specific vector layers from a service.
    /// </summary>
    Task SynchronizeServiceLayersAsync(string serviceId, IEnumerable<string>? layerIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a single vector layer.
    /// </summary>
    Task SynchronizeLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of vector STAC catalog synchronization.
/// Coordinates with RasterStacCatalogSynchronizer to provide unified STAC catalog.
/// </summary>
public sealed class VectorStacCatalogSynchronizer : DisposableBase, IVectorStacCatalogSynchronizer
{
    private const int ExistingScanPageSize = 128;

    private readonly IStacCatalogStore _store;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly VectorStacCatalogBuilder _builder;
    private readonly ILogger<VectorStacCatalogSynchronizer> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public VectorStacCatalogSynchronizer(
        IStacCatalogStore store,
        IHonuaConfigurationService configurationService,
        IMetadataRegistry metadataRegistry,
        VectorStacCatalogBuilder builder,
        ILogger<VectorStacCatalogSynchronizer> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SynchronizeAllVectorLayersAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsEnabled())
        {
            _logger.LogDebug("STAC catalog disabled; skipping vector layer synchronization.");
            return;
        }

        await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var vectorLayers = CollectVectorLayers(snapshot, serviceIdFilter: null, layerIdFilter: null)
            .Where(tuple => _builder.Supports(tuple.Layer))
            .ToArray();

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var baseUri = GetBaseUri();
            var expectedCollectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processedCollectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (layer, service) in vectorLayers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var collectionId = _builder.GetCollectionId(layer);
                expectedCollectionIds.Add(collectionId);

                try
                {
                    await SynchronizeLayerInternalAsync(layer, service, snapshot, baseUri, cancellationToken).ConfigureAwait(false);
                    processedCollectionIds.Add(collectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to synchronize STAC metadata for vector layer {ServiceId}:{LayerId}.",
                        service.Id, layer.Id);
                    throw;
                }
            }

            _logger.LogInformation("Synchronized {Count} vector layers to STAC catalog.", processedCollectionIds.Count);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task SynchronizeServiceLayersAsync(string serviceId, IEnumerable<string>? layerIds = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ThrowIfDisposed();

        if (!IsEnabled())
        {
            _logger.LogDebug("STAC catalog disabled; skipping service layer synchronization.");
            return;
        }

        await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var layerIdSet = layerIds is not null
            ? new HashSet<string>(layerIds, StringComparer.OrdinalIgnoreCase)
            : null;

        var vectorLayers = CollectVectorLayers(snapshot, serviceId, layerIdFilter: null)
            .Where(tuple => _builder.Supports(tuple.Layer))
            .Where(tuple => layerIdSet is null || layerIdSet.Contains(tuple.Layer.Id))
            .ToArray();

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var baseUri = GetBaseUri();
            var processedCount = 0;

            foreach (var (layer, service) in vectorLayers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await SynchronizeLayerInternalAsync(layer, service, snapshot, baseUri, cancellationToken).ConfigureAwait(false);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to synchronize STAC metadata for vector layer {ServiceId}:{LayerId}.",
                        service.Id, layer.Id);
                    throw;
                }
            }

            _logger.LogInformation("Synchronized {Count} vector layers from service {ServiceId} to STAC catalog.",
                processedCount, serviceId);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task SynchronizeLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ThrowIfDisposed();

        if (!IsEnabled())
        {
            _logger.LogDebug("STAC catalog disabled; skipping layer synchronization.");
            return;
        }

        await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!snapshot.TryGetService(serviceId, out var service))
        {
            _logger.LogWarning("Service {ServiceId} not found for layer synchronization.", serviceId);
            return;
        }

        var layer = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            _logger.LogWarning("Layer {LayerId} not found in service {ServiceId}.", layerId, serviceId);
            return;
        }

        if (!_builder.Supports(layer))
        {
            _logger.LogDebug("Layer {ServiceId}:{LayerId} does not have STAC enabled.", serviceId, layerId);
            return;
        }

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var baseUri = GetBaseUri();
            await SynchronizeLayerInternalAsync(layer, service, snapshot, baseUri, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Synchronized vector layer {ServiceId}:{LayerId} to STAC catalog.", serviceId, layerId);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task SynchronizeLayerInternalAsync(
        LayerDefinition layer,
        ServiceDefinition service,
        MetadataSnapshot snapshot,
        string? baseUri,
        CancellationToken cancellationToken)
    {
        var collection = _builder.Build(layer, service, snapshot);
        var items = _builder.BuildItems(layer, service, snapshot, baseUri);
        var collectionId = collection.Id;

        // Upsert collection
        await _store.UpsertCollectionAsync(collection, expectedETag: null, cancellationToken).ConfigureAwait(false);

        // Load existing item IDs for pruning
        var existingItemIds = await LoadExistingItemIdsAsync(collectionId, cancellationToken).ConfigureAwait(false);
        var newItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Upsert items
        foreach (var item in items)
        {
            await _store.UpsertItemAsync(item, expectedETag: null, cancellationToken).ConfigureAwait(false);
            newItemIds.Add(item.Id);
        }

        // Prune items that no longer exist
        foreach (var existingId in existingItemIds)
        {
            if (!newItemIds.Contains(existingId))
            {
                await _store.DeleteItemAsync(collectionId, existingId, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Synchronized vector layer {ServiceId}:{LayerId} -> STAC collection {CollectionId} with {ItemCount} items.",
            service.Id, layer.Id, collectionId, items.Count);
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

    private bool IsEnabled()
    {
        return _configurationService.Current.Services.Stac.Enabled;
    }

    private string? GetBaseUri()
    {
        // In a real implementation, this would come from configuration or request context
        // For now, return null and let the builder handle it
        return null;
    }

    protected override void DisposeCore()
    {
        _syncLock.Dispose();
    }
}
