// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Middleware;

namespace Honua.Server.Host.Stac;

/// <summary>
/// Helper methods for STAC collection operations.
/// Consolidates repeated patterns like collection existence validation and cache invalidation.
/// </summary>
public static class StacCollectionHelper
{
    /// <summary>
    /// Ensures a collection exists, returning it or throwing a not-found result.
    /// Consolidates the repeated pattern of GetCollectionAsync + null check.
    /// </summary>
    /// <param name="store">The STAC store.</param>
    /// <param name="collectionId">The collection ID to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection record if found.</returns>
    /// <exception cref="StacCollectionNotFoundException">Thrown if collection doesn't exist.</exception>
    public static async Task<StacCollectionRecord> EnsureCollectionExistsAsync(
        this IStacCatalogStore store,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        var collection = await store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collection is null)
        {
            throw new StacCollectionNotFoundException(collectionId);
        }
        return collection;
    }

    /// <summary>
    /// Attempts to get a collection, returning a result indicating success or failure.
    /// Use this when you want to handle missing collections without exceptions.
    /// </summary>
    public static async Task<CollectionResult> TryGetCollectionAsync(
        this IStacCatalogStore store,
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        var collection = await store.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collection is null)
        {
            return CollectionResult.NotFound(collectionId);
        }
        return CollectionResult.Success(collection);
    }

    /// <summary>
    /// Invalidates cache for a collection and optionally its items.
    /// </summary>
    public static async Task InvalidateCacheForCollectionAsync(
        this IOutputCacheInvalidationService cache,
        string collectionId,
        bool includeItems = false,
        CancellationToken cancellationToken = default)
    {
        await cache.InvalidateStacCollectionCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);
        
        if (includeItems)
        {
            await cache.InvalidateStacItemsCacheAsync(collectionId, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Result of attempting to retrieve a collection.
/// </summary>
public sealed class CollectionResult
{
    public bool IsSuccess { get; init; }
    public StacCollectionRecord? Collection { get; init; }
    public string? ErrorMessage { get; init; }

    public static CollectionResult Success(StacCollectionRecord collection)
        => new() { IsSuccess = true, Collection = collection };

    public static CollectionResult NotFound(string collectionId)
        => new() { IsSuccess = false, ErrorMessage = $"Collection '{collectionId}' not found." };
}

/// <summary>
/// Exception thrown when a STAC collection is not found.
/// </summary>
public sealed class StacCollectionNotFoundException : Exception
{
    public string CollectionId { get; }

    public StacCollectionNotFoundException(string collectionId)
        : base($"Collection '{collectionId}' not found.")
    {
        CollectionId = collectionId;
    }
}
