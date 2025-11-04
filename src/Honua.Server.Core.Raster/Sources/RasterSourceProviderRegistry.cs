// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

public sealed class RasterSourceProviderRegistry : IRasterSourceProviderRegistry
{
    private readonly IReadOnlyList<IRasterSourceProvider> _providers;

    public RasterSourceProviderRegistry(IEnumerable<IRasterSourceProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    public IRasterSourceProvider? GetProvider(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        return _providers.FirstOrDefault(p => p.CanHandle(uri));
    }

    public Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(uri);
        if (provider is null)
        {
            throw new InvalidOperationException($"No raster source provider found for URI: {uri}");
        }

        return provider.OpenReadAsync(uri, cancellationToken);
    }

    public Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(uri);
        if (provider is null)
        {
            throw new InvalidOperationException($"No raster source provider found for URI: {uri}");
        }

        return provider.OpenReadRangeAsync(uri, offset, length, cancellationToken);
    }
}
