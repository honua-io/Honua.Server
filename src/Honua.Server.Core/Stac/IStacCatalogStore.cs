// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Stac;

public interface IStacCatalogStore
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task UpsertCollectionAsync(StacCollectionRecord collection, string? expectedETag = null, CancellationToken cancellationToken = default);
    Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StacCollectionRecord>> GetCollectionsAsync(IReadOnlyList<string> collectionIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StacCollectionRecord>> ListCollectionsAsync(CancellationToken cancellationToken = default);
    Task<StacCollectionListResult> ListCollectionsAsync(int limit, string? token = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a STAC collection by marking it as deleted.
    /// </summary>
    Task<bool> SoftDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted STAC collection.
    /// </summary>
    Task<bool> RestoreCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a STAC collection (hard delete).
    /// </summary>
    Task<bool> HardDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default);

    Task UpsertItemAsync(StacItemRecord item, string? expectedETag = null, CancellationToken cancellationToken = default);
    Task<BulkUpsertResult> BulkUpsertItemsAsync(IReadOnlyList<StacItemRecord> items, BulkUpsertOptions? options = null, CancellationToken cancellationToken = default);
    Task<StacItemRecord?> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StacItemRecord>> ListItemsAsync(string collectionId, int limit, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a STAC item by marking it as deleted.
    /// </summary>
    Task<bool> SoftDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted STAC item.
    /// </summary>
    Task<bool> RestoreItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a STAC item (hard delete).
    /// </summary>
    Task<bool> HardDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default);

    Task<StacSearchResult> SearchAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for STAC items with streaming support to handle large result sets efficiently.
    /// Uses cursor-based pagination to maintain constant memory usage regardless of result set size.
    /// </summary>
    /// <param name="parameters">Search parameters including filters, pagination, and sorting.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable stream of STAC items.</returns>
    IAsyncEnumerable<StacItemRecord> SearchStreamAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default);
}
