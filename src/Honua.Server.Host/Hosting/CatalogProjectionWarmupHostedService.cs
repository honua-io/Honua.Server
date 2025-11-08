// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Background service that warms up the catalog projection snapshot during application startup.
/// Ensures the catalog is pre-loaded and ready before the application begins serving requests.
/// </summary>
public sealed class CatalogProjectionWarmupHostedService : IHostedService
{
    private readonly ICatalogProjectionService _catalog;
    private readonly ILogger<CatalogProjectionWarmupHostedService> _logger;

    public CatalogProjectionWarmupHostedService(
        ICatalogProjectionService catalog,
        ILogger<CatalogProjectionWarmupHostedService> logger)
    {
        _catalog = Guard.NotNull(catalog);
        _logger = Guard.NotNull(logger);
    }

    /// <summary>
    /// Triggered when the application host is starting. Performs catalog warmup asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the warmup operation</param>
    /// <returns>A task representing the asynchronous startup operation</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Warming catalog projection snapshot.");
            await _catalog.WarmupAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Catalog projection warm-up completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Catalog projection warm-up canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog projection warm-up failed; using last known snapshot until refresh completes.");
        }
    }

    /// <summary>
    /// Triggered when the application host is stopping. No cleanup needed for this service.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the stop operation</param>
    /// <returns>A completed task</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
