// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Provider for loading raster data from various sources (file system, S3, Azure Blob, HTTP/COG).
/// </summary>
public interface IRasterSourceProvider
{
    /// <summary>
    /// Gets the provider key (e.g., "file", "s3", "azureblob", "http").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Determines if this provider can handle the specified URI.
    /// </summary>
    bool CanHandle(string uri);

    /// <summary>
    /// Opens a read-only stream to the raster data source.
    /// </summary>
    Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream with support for range requests (for COG optimization).
    /// </summary>
    /// <param name="uri">The resource URI</param>
    /// <param name="offset">Byte offset to start reading from</param>
    /// <param name="length">Number of bytes to read, or null to read to end</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default);
}
