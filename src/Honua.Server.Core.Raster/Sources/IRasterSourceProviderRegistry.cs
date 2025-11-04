// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Registry for raster source providers that routes requests to the appropriate provider.
/// </summary>
public interface IRasterSourceProviderRegistry
{
    /// <summary>
    /// Gets a provider that can handle the specified URI.
    /// </summary>
    IRasterSourceProvider? GetProvider(string uri);

    /// <summary>
    /// Opens a read-only stream to the raster data source using the appropriate provider.
    /// </summary>
    Task<System.IO.Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read-only stream with range support using the appropriate provider.
    /// </summary>
    Task<System.IO.Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default);
}
