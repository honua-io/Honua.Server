// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Stores and retrieves kerchunk reference JSON for virtual Zarr datasets.
/// Supports caching with multiple backends (S3, Redis, filesystem).
/// </summary>
public interface IKerchunkReferenceStore
{
    /// <summary>
    /// Gets kerchunk references, generating them if not cached (lazy/on-demand).
    /// Thread-safe with distributed locking to prevent duplicate generation.
    /// </summary>
    /// <param name="sourceUri">Source file URI</param>
    /// <param name="options">Generation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Kerchunk references</returns>
    Task<KerchunkReferences> GetOrGenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly generates kerchunk references (used by API/CLI).
    /// Optionally forces regeneration even if cached.
    /// </summary>
    /// <param name="sourceUri">Source file URI</param>
    /// <param name="options">Generation options</param>
    /// <param name="force">Force regeneration even if cached</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Kerchunk references</returns>
    Task<KerchunkReferences> GenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if kerchunk references exist in cache.
    /// </summary>
    /// <param name="sourceUri">Source file URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if references are cached</returns>
    Task<bool> ExistsAsync(string sourceUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes cached kerchunk references.
    /// </summary>
    /// <param name="sourceUri">Source file URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string sourceUri, CancellationToken cancellationToken = default);
}
