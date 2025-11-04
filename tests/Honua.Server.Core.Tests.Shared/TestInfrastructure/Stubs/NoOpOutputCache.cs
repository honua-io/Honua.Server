using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.OutputCaching;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// No-op implementation of IOutputCacheInvalidationService for testing.
/// All cache invalidation operations complete immediately without side effects.
/// </summary>
/// <remarks>
/// Use this stub in tests where you need to satisfy IOutputCacheInvalidationService
/// dependencies but don't need actual cache invalidation behavior.
/// </remarks>
public sealed class NoOpOutputCacheInvalidationService : IOutputCacheInvalidationService
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NoOpOutputCacheInvalidationService Instance = new();

    private NoOpOutputCacheInvalidationService()
    {
    }

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// No-op implementation of IOutputCacheStore for testing.
/// All cache operations complete immediately without storing or retrieving data.
/// </summary>
/// <remarks>
/// Use this stub in tests where you need to satisfy IOutputCacheStore dependencies
/// but don't need actual cache storage behavior.
/// </remarks>
public sealed class NoOpOutputCacheStore : IOutputCacheStore
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NoOpOutputCacheStore Instance = new();

    private NoOpOutputCacheStore()
    {
    }

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public ValueTask EvictByTagAsync(string[] tags, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan duration, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Always returns an empty byte array.
    /// </summary>
    public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Array.Empty<byte>());

    /// <summary>
    /// No-op. Returns completed task.
    /// </summary>
    public ValueTask EvictAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
