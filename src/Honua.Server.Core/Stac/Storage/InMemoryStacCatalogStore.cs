// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Stac.Storage;

public sealed class InMemoryStacCatalogStore : IStacCatalogStore
{
    private readonly CollectionStore _collections = new();
    private readonly ItemStore _items = new();

    // Nested stores inheriting from InMemoryStoreBase
    private sealed class CollectionStore : InMemoryStoreBase<StacCollectionRecord>
    {
        public CollectionStore() : base(StringComparer.OrdinalIgnoreCase) { }
        protected override string GetKey(StacCollectionRecord entity) => entity.Id;
    }

    private sealed class ItemStore : InMemoryStoreBase<StacItemRecord, ItemKey>
    {
        protected override ItemKey GetKey(StacItemRecord entity) => new(entity.CollectionId, entity.Id);

        // Expose bulk upsert capability without locking overhead
        public void BulkPut(IEnumerable<StacItemRecord> items)
        {
            foreach (var item in items)
            {
                var key = GetKey(item);
                Storage[key] = item;
            }
        }
    }

    // Composite key for items
    private readonly record struct ItemKey(string CollectionId, string ItemId)
    {
        public override string ToString() => $"{CollectionId}:{ItemId}";
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task UpsertCollectionAsync(StacCollectionRecord collection, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(collection);
        Guard.NotNullOrWhiteSpace(collection.Id);

        // Check ETag if provided
        if (expectedETag != null)
        {
            var existing = await _collections.GetAsync(collection.Id, cancellationToken).ConfigureAwait(false);
            if (existing != null && existing.ETag != expectedETag)
            {
                throw new DBConcurrencyException(
                    $"Collection '{collection.Id}' was modified by another user. Expected ETag: {expectedETag}");
            }
        }

        // Generate new ETag for this update
        var newETag = Guid.NewGuid().ToString("N");
        await _collections.PutAsync(collection with { ETag = newETag }, cancellationToken).ConfigureAwait(false);
    }

    public Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        return _collections.GetAsync(collectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<StacCollectionRecord>> GetCollectionsAsync(IReadOnlyList<string> collectionIds, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(collectionIds);

        if (collectionIds.Count == 0)
        {
            return Array.Empty<StacCollectionRecord>();
        }

        var results = new List<StacCollectionRecord>();
        foreach (var collectionId in collectionIds)
        {
            var collection = await _collections.GetAsync(collectionId, cancellationToken).ConfigureAwait(false);
            if (collection != null)
            {
                results.Add(collection);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<StacCollectionRecord>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _collections.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return collections.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    public async Task<StacCollectionListResult> ListCollectionsAsync(int limit, string? token = null, CancellationToken cancellationToken = default)
    {
        // Normalize limit to be between 1 and 1000
        var normalizedLimit = Math.Max(1, Math.Min(1000, limit));

        // Get all collections ordered by ID
        var allCollections = await _collections.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var orderedCollections = allCollections.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

        // Apply token filter if provided
        var filteredCollections = token.IsNullOrEmpty()
            ? orderedCollections
            : orderedCollections.Where(c => string.Compare(c.Id, token, StringComparison.Ordinal) > 0).ToList();

        // Take limit + 1 to detect if there are more results
        var collections = filteredCollections.Take(normalizedLimit + 1).ToList();

        // Determine next token
        string? nextToken = null;
        if (collections.Count > normalizedLimit)
        {
            nextToken = collections[^1].Id;
            collections.RemoveAt(collections.Count - 1);
        }

        return new StacCollectionListResult
        {
            Collections = collections,
            TotalCount = orderedCollections.Count,
            NextToken = nextToken
        };
    }

    public async Task<bool> DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);

        var removed = await _collections.DeleteAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (removed)
        {
            // Delete all items in the collection
            var keys = await _items.GetKeysAsync(cancellationToken).ConfigureAwait(false);
            var itemKeysToDelete = keys.Where(key => string.Equals(key.CollectionId, collectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in itemKeysToDelete)
            {
                await _items.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }

        return removed;
    }

    public async Task UpsertItemAsync(StacItemRecord item, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(item);
        Guard.NotNullOrWhiteSpace(item.Id);
        Guard.NotNullOrWhiteSpace(item.CollectionId);

        var key = new ItemKey(item.CollectionId, item.Id);

        // Check ETag if provided
        if (expectedETag != null)
        {
            var existing = await _items.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (existing != null && existing.ETag != expectedETag)
            {
                throw new DBConcurrencyException(
                    $"Item '{item.Id}' in collection '{item.CollectionId}' was modified by another user. Expected ETag: {expectedETag}");
            }
        }

        // Generate new ETag for this update
        var newETag = Guid.NewGuid().ToString("N");
        await _items.PutAsync(item with { ETag = newETag }, cancellationToken).ConfigureAwait(false);
    }

    public Task<BulkUpsertResult> BulkUpsertItemsAsync(IReadOnlyList<StacItemRecord> items, BulkUpsertOptions? options = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(items);
        options ??= new BulkUpsertOptions();

        var stopwatch = Stopwatch.StartNew();
        using var activity = HonuaTelemetry.Stac.StartActivity("STAC BulkUpsert (InMemory)");
        activity?.SetTag("stac.operation", "BulkUpsert");
        activity?.SetTag("stac.provider", "InMemory");
        activity?.SetTag("stac.item_count", items.Count);

        var failures = new List<BulkUpsertItemFailure>();
        var validItems = new List<StacItemRecord>();

        foreach (var item in items)
        {
            try
            {
                if (item.Id.IsNullOrWhiteSpace() || item.CollectionId.IsNullOrWhiteSpace())
                {
                    failures.Add(new BulkUpsertItemFailure
                    {
                        ItemId = item.Id ?? string.Empty,
                        CollectionId = item.CollectionId ?? string.Empty,
                        ErrorMessage = "Item ID and Collection ID are required"
                    });
                    continue;
                }

                var newETag = Guid.NewGuid().ToString("N");
                validItems.Add(item with { ETag = newETag });
            }
            catch (Exception ex)
            {
                failures.Add(new BulkUpsertItemFailure
                {
                    ItemId = item.Id ?? string.Empty,
                    CollectionId = item.CollectionId ?? string.Empty,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });

                if (!options.ContinueOnError)
                {
                    throw;
                }
            }
        }

        // Bulk insert all valid items
        _items.BulkPut(validItems);

        stopwatch.Stop();

        var successCount = validItems.Count;
        activity?.SetTag("stac.success_count", successCount);
        activity?.SetTag("stac.failure_count", failures.Count);

        // Record metrics
        StacMetrics.BulkUpsertCount.Add(1);
        StacMetrics.BulkUpsertItemsCount.Add(items.Count);
        StacMetrics.BulkUpsertDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        if (failures.Count > 0)
        {
            StacMetrics.BulkUpsertFailures.Add(failures.Count);
        }

        // Calculate throughput
        var throughput = items.Count / stopwatch.Elapsed.TotalSeconds;
        StacMetrics.BulkUpsertThroughput.Record(throughput);

        return Task.FromResult(new BulkUpsertResult
        {
            SuccessCount = successCount,
            FailureCount = failures.Count,
            Failures = failures,
            Duration = stopwatch.Elapsed
        });
    }

    public Task<StacItemRecord?> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);

        return _items.GetAsync(new ItemKey(collectionId, itemId), cancellationToken);
    }

    public async Task<IReadOnlyList<StacItemRecord>> ListItemsAsync(string collectionId, int limit, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);

        var items = await _items.QueryAsync(
            item => string.Equals(item.CollectionId, collectionId, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        IEnumerable<StacItemRecord> query = items.OrderBy(item => item.Id, StringComparer.Ordinal);

        if (pageToken.HasValue())
        {
            query = query.SkipWhile(item => string.CompareOrdinal(item.Id, pageToken) <= 0);
        }

        if (limit > 0)
        {
            query = query.Take(limit);
        }

        return query.ToList();
    }

    public Task<bool> DeleteItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);

        return _items.DeleteAsync(new ItemKey(collectionId, itemId), cancellationToken);
    }

    public Task<bool> SoftDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        // In-memory store doesn't support soft delete - use regular delete instead
        return DeleteCollectionAsync(collectionId, cancellationToken);
    }

    public Task<bool> RestoreCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        // In-memory store doesn't support restore
        return Task.FromResult(false);
    }

    public Task<bool> HardDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        // In-memory store: hard delete is the same as regular delete
        return DeleteCollectionAsync(collectionId, cancellationToken);
    }

    public Task<bool> SoftDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        // In-memory store doesn't support soft delete - use regular delete instead
        return DeleteItemAsync(collectionId, itemId, cancellationToken);
    }

    public Task<bool> RestoreItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        // In-memory store doesn't support restore
        return Task.FromResult(false);
    }

    public Task<bool> HardDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default)
    {
        // In-memory store: hard delete is the same as regular delete
        return DeleteItemAsync(collectionId, itemId, cancellationToken);
    }

    public async Task<StacSearchResult> SearchAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(parameters);

        var stopwatch = Stopwatch.StartNew();
        using var activity = HonuaTelemetry.Stac.StartActivity("STAC Search (InMemory)");
        activity?.SetTag("stac.operation", "Search");
        activity?.SetTag("stac.provider", "InMemory");

        var limit = parameters.Limit > 0 ? parameters.Limit : 10;
        var collectionsFilter = parameters.Collections is { Count: > 0 }
            ? new HashSet<string>(parameters.Collections, StringComparer.OrdinalIgnoreCase)
            : null;
        var idsFilter = parameters.Ids is { Count: > 0 }
            ? new HashSet<string>(parameters.Ids, StringComparer.OrdinalIgnoreCase)
            : null;

        activity?.SetTag("stac.limit", limit);
        activity?.SetTag("stac.has_bbox", parameters.Bbox != null);
        activity?.SetTag("stac.has_datetime", parameters.Start != null || parameters.End != null);
        if (collectionsFilter != null)
        {
            activity?.SetTag("stac.collections", string.Join(",", collectionsFilter));
        }

        var (tokenCollection, tokenItem) = ParseContinuationToken(parameters.Token);

        var allItems = await _items.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var query = allItems
            .Where(item => collectionsFilter is null || collectionsFilter.Contains(item.CollectionId))
            .Where(item => idsFilter is null || idsFilter.Contains(item.Id))
            .Where(item => MatchesDatetime(item, parameters.Start, parameters.End))
            .Where(item => MatchesBbox(item, parameters.Bbox))
            .OrderBy(item => item.CollectionId, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        // Calculate matched count BEFORE applying pagination token
        var matched = query.Count;

        if (tokenCollection is not null && tokenItem is not null)
        {
            query = query
                .SkipWhile(item => string.CompareOrdinal(item.CollectionId, tokenCollection) < 0
                                   || (string.Equals(item.CollectionId, tokenCollection, StringComparison.Ordinal)
                                       && string.CompareOrdinal(item.Id, tokenItem) <= 0))
                .ToList();
        }

        string? nextToken = null;

        if (query.Count > limit)
        {
            var last = query[limit - 1];
            nextToken = $"{last.CollectionId}:{last.Id}";
            query = query.Take(limit).ToList();
        }

        var items = query.ToList();

        stopwatch.Stop();

        activity?.SetTag("stac.matched_count", matched);
        activity?.SetTag("stac.result_count", items.Count);

        // Record search metrics
        StacMetrics.SearchCount.Add(1);
        StacMetrics.SearchDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        return new StacSearchResult
        {
            Items = items,
            Matched = matched,
            NextToken = nextToken
        };
    }

    private static (string? CollectionId, string? ItemId) ParseContinuationToken(string? token)
    {
        if (token.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var parts = token.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (null, null);
        }

        return (parts[0], parts[1]);
    }

    private static bool MatchesDatetime(StacItemRecord item, DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null && end is null)
        {
            return true;
        }

        var itemStart = item.StartDatetime ?? item.Datetime;
        var itemEnd = item.EndDatetime ?? item.Datetime;

        if (start.HasValue && itemEnd.HasValue && itemEnd.Value < start.Value)
        {
            return false;
        }

        if (end.HasValue && itemStart.HasValue && itemStart.Value > end.Value)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesBbox(StacItemRecord item, double[]? bbox)
    {
        if (bbox is null || bbox.Length < 4)
        {
            return true;
        }

        if (item.Bbox is null || item.Bbox.Length < 4)
        {
            return false;
        }

        var candidate = item.Bbox;
        return candidate[0] <= bbox[2] && candidate[2] >= bbox[0] && candidate[1] <= bbox[3] && candidate[3] >= bbox[1];
    }

    /// <summary>
    /// Searches for STAC items with streaming support to handle large result sets efficiently.
    /// For in-memory store, this simply wraps SearchAsync and streams the results.
    /// </summary>
    public async IAsyncEnumerable<StacItemRecord> SearchStreamAsync(
        StacSearchParameters parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(parameters);

        // For in-memory store, we use a simple pagination approach
        // This doesn't save memory since everything is already in memory,
        // but it maintains the same interface as the relational store
        var pageSize = 100; // Use a reasonable page size
        var maxItems = 100_000; // Default limit
        var itemsReturned = 0;
        string? currentToken = parameters.Token;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if we've reached the maximum item limit
            if (maxItems > 0 && itemsReturned >= maxItems)
            {
                yield break;
            }

            // Fetch a page of results
            var pageParameters = new StacSearchParameters
            {
                Collections = parameters.Collections,
                Ids = parameters.Ids,
                Bbox = parameters.Bbox,
                Intersects = parameters.Intersects,
                Start = parameters.Start,
                End = parameters.End,
                Limit = pageSize,
                Token = currentToken,
                SortBy = parameters.SortBy,
                Filter = parameters.Filter,
                FilterLang = parameters.FilterLang
            };

            var result = await SearchAsync(pageParameters, cancellationToken).ConfigureAwait(false);

            // If no items were returned, we're done
            if (result.Items.Count == 0)
            {
                yield break;
            }

            // Yield items one at a time
            foreach (var item in result.Items)
            {
                if (maxItems > 0 && itemsReturned >= maxItems)
                {
                    yield break;
                }

                yield return item;
                itemsReturned++;
            }

            // If there's no next token, we're done
            if (result.NextToken == null)
            {
                yield break;
            }

            // Update the continuation token for the next page
            currentToken = result.NextToken;
        }
    }
}
