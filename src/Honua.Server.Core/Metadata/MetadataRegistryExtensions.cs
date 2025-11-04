// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Extension methods for <see cref="IMetadataRegistry"/>.
/// </summary>
public static class MetadataRegistryExtensions
{
    /// <summary>
    /// Reloads the metadata registry and logs the operation.
    /// </summary>
    /// <param name="metadataRegistry">The metadata registry to reload.</param>
    /// <param name="logger">The logger to use for logging.</param>
    /// <param name="operation">The operation that triggered the reload (e.g., "style creation", "style update").</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadataRegistry"/> or <paramref name="logger"/> is null.</exception>
    public static async Task ReloadWithLoggingAsync(
        this IMetadataRegistry metadataRegistry,
        ILogger logger,
        string operation,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(logger);

        await metadataRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Metadata reloaded after {Operation}", operation);
    }
}
