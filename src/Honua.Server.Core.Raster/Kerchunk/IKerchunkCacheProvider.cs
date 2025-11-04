// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Cache provider for storing kerchunk reference JSON.
/// Implementations: S3, Azure Blob, filesystem, Redis, etc.
/// </summary>
public interface IKerchunkCacheProvider
{
    /// <summary>
    /// Gets cached kerchunk references by key.
    /// </summary>
    /// <param name="key">Cache key (typically SHA256 hash of source URI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached references or null if not found</returns>
    Task<KerchunkReferences?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores kerchunk references in cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="references">References to cache</param>
    /// <param name="ttl">Time-to-live (null = never expire)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync(
        string key,
        KerchunkReferences references,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a cache entry exists.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if entry exists</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cache entry.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
