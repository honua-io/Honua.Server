// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.OutputCaching;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Service for invalidating output cache entries based on tags.
/// Provides centralized cache invalidation for POST/PUT/PATCH/DELETE operations.
/// </summary>
public interface IOutputCacheInvalidationService
{
    /// <summary>
    /// Invalidates all STAC-related cache entries.
    /// Called when any STAC collection or item is created, updated, or deleted.
    /// </summary>
    Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache for a specific STAC collection.
    /// Called when a collection is created, updated, or deleted.
    /// </summary>
    Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache for STAC items in a collection.
    /// Called when items are created, updated, or deleted.
    /// </summary>
    Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all catalog-related cache entries.
    /// Called when catalog data is modified.
    /// </summary>
    Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cache entries across the application.
    /// Use sparingly - only for major configuration changes.
    /// </summary>
    Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of output cache invalidation service using IOutputCacheStore.
/// </summary>
public sealed class OutputCacheInvalidationService : IOutputCacheInvalidationService
{
    private readonly IOutputCacheStore cacheStore;

    public OutputCacheInvalidationService(IOutputCacheStore cacheStore)
    {
        this.cacheStore = Guard.NotNull(cacheStore);
    }

    public async Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default)
    {
        // Invalidate all STAC-tagged cache entries
        await this.cacheStore.EvictByTagAsync("stac", cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        // Invalidate collection-specific cache
        await this.cacheStore.EvictByTagAsync("stac-collections", cancellationToken).ConfigureAwait(false);
        await this.cacheStore.EvictByTagAsync("stac-collection-metadata", cancellationToken).ConfigureAwait(false);

        // SECURITY FIX (Issue 37): Also invalidate search cache when collections change
        // Search results depend on collection metadata and availability
        await this.cacheStore.EvictByTagAsync("stac-search", cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        // Invalidate items and item metadata cache for this collection
        await this.cacheStore.EvictByTagAsync("stac-items", cancellationToken).ConfigureAwait(false);
        await this.cacheStore.EvictByTagAsync("stac-item-metadata", cancellationToken).ConfigureAwait(false);

        // SECURITY FIX (Issue 37): Also invalidate search cache when items change
        // Search results include items, so item mutations should invalidate search cache
        await this.cacheStore.EvictByTagAsync("stac-search", cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default)
    {
        // Invalidate all catalog-tagged cache entries
        await this.cacheStore.EvictByTagAsync("catalog", cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default)
    {
        // Invalidate everything by evicting the base api-cache tag
        await this.cacheStore.EvictByTagAsync("api-cache", cancellationToken).ConfigureAwait(false);
        await this.cacheStore.EvictByTagAsync("stac", cancellationToken).ConfigureAwait(false);
        await this.cacheStore.EvictByTagAsync("catalog", cancellationToken).ConfigureAwait(false);
        await this.cacheStore.EvictByTagAsync("ogc", cancellationToken).ConfigureAwait(false);
    }
}
