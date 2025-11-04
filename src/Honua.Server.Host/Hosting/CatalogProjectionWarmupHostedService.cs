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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
