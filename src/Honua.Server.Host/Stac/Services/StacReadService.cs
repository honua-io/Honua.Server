// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Configuration.V2;
using System.Linq;
using Honua.Server.Core.Configuration.V2;
using System.Threading;
using Honua.Server.Core.Configuration.V2;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Configuration.V2;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Configuration.V2;

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Service for STAC read operations (GET collections, GET items).
/// </summary>
public sealed class StacReadService
{
    private readonly IStacCatalogStore _store;
    private readonly HonuaConfig? _honuaConfig;
    private readonly StacMetrics _metrics;
    private readonly ILogger<StacReadService> _logger;

    public StacReadService(
        IStacCatalogStore store,
        StacMetrics metrics,
        ILogger<StacReadService> logger,
        HonuaConfig? honuaConfig = null)
    {
        _store = Guard.NotNull(store);
        _honuaConfig = honuaConfig;
        _metrics = Guard.NotNull(metrics);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Checks if STAC is enabled in the configuration.
    /// </summary>
    public bool IsStacEnabled() => StacRequestHelpers.IsStacEnabled(_honuaConfig);

    /// <summary>
    /// Gets all collections from the catalog.
    /// </summary>
    public async Task<StacCollectionsResponse> GetCollectionsAsync(string baseUri, CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<StacCollectionsResponse>("STAC ListCollections")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("operation", "list_collections")
            .WithTag("resource", "collection")
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
                var collections = await _store.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
                var response = StacApiMapper.BuildCollections(collections, new Uri(baseUri));

                activity?.SetTag("collection_count", response.Collections.Count);
                return response;
            });
    }

    /// <summary>
    /// Gets collections from the catalog with pagination.
    /// </summary>
    public async Task<StacCollectionsResponse> GetCollectionsAsync(string baseUri, int limit, string? token, CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<StacCollectionsResponse>("STAC ListCollections")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("operation", "list_collections")
            .WithTag("resource", "collection")
            .WithTag("has_pagination", true)
            .WithTag("limit", NormalizeLimit(limit))
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
                var pageSize = NormalizeLimit(limit);
                var result = await _store.ListCollectionsAsync(pageSize, token, cancellationToken).ConfigureAwait(false);
                var response = StacApiMapper.BuildCollections(result.Collections, new Uri(baseUri), result.TotalCount, result.NextToken, pageSize);

                activity?.SetTag("collection_count", response.Collections.Count);
                activity?.SetTag("limit", pageSize);
                activity?.SetTag("total_count", result.TotalCount);
                activity?.SetTag("has_more", result.NextToken is not null);
                return response;
            });
    }

    /// <summary>
    /// Gets a specific collection by ID.
    /// </summary>
    public async Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<StacCollectionRecord?>("STAC GetCollection")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("operation", "get_collection")
            .WithTag("resource", "collection")
            .WithTag("collection_id", collectionId)
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
                var collection = await _store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);

                activity?.SetTag("found", collection is not null);
                return collection;
            });
    }

    /// <summary>
    /// Gets items from a specific collection with pagination.
    /// </summary>
    public async Task<StacItemCollectionResponse> GetCollectionItemsAsync(
        string collectionId,
        int? limit,
        string? pageToken,
        string baseUri,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<StacItemCollectionResponse>("STAC ListCollectionItems")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("operation", "list_items")
            .WithTag("resource", "item")
            .WithTag("collection_id", collectionId)
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

                var collection = await _store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
                if (collection is null)
                {
                    throw new InvalidOperationException($"Collection '{collectionId}' not found.");
                }

                var pageSize = NormalizeLimit(limit);
                var batchSize = checked(pageSize + 1);
                var items = await _store.ListItemsAsync(collectionId, batchSize, pageToken, cancellationToken).ConfigureAwait(false);
                var itemsList = items.ToList();

                string? nextToken = null;
                if (itemsList.Count > pageSize)
                {
                    itemsList.RemoveAt(itemsList.Count - 1);
                    nextToken = itemsList[^1].Id;
                }

                var response = StacApiMapper.BuildItemCollection(itemsList, collection, new Uri(baseUri), matched: null, nextToken, pageSize);

                activity?.SetTag("item_count", itemsList.Count);
                activity?.SetTag("has_more", nextToken is not null);
                activity?.SetTag("page_size", pageSize);
                return response;
            });
    }

    /// <summary>
    /// Gets a specific item by ID.
    /// </summary>
    public async Task<StacItemRecord?> GetCollectionItemAsync(
        string collectionId,
        string itemId,
        CancellationToken cancellationToken)
    {
        return await OperationInstrumentation.Create<StacItemRecord?>("STAC GetCollectionItem")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("operation", "get_item")
            .WithTag("resource", "item")
            .WithTag("collection_id", collectionId)
            .WithTag("item_id", itemId)
            .ExecuteAsync(async activity =>
            {
                await _store.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
                var item = await _store.GetItemAsync(collectionId, itemId, cancellationToken).ConfigureAwait(false);

                activity?.SetTag("found", item is not null);
                return item;
            });
    }

    private static int NormalizeLimit(int? limit)
    {
        const int defaultLimit = 10;
        const int maxLimit = 1000;
        if (!limit.HasValue || limit.Value <= 0)
        {
            return defaultLimit;
        }

        return Math.Min(limit.Value, maxLimit);
    }
}
