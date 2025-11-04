// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Stac;

internal sealed class StacCatalogSynchronizationHostedService : IHostedService, IDisposable
{
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IRasterStacCatalogSynchronizer _rasterSynchronizer;
    private readonly IVectorStacCatalogSynchronizer? _vectorSynchronizer;
    private readonly ILogger<StacCatalogSynchronizationHostedService> _logger;

    private IDisposable? _changeTokenRegistration;

    // Prevent unbounded task creation with debouncing and cancellation
    private Task? _synchronizationTask;
    private CancellationTokenSource? _synchronizationCts;
    private readonly object _syncLock = new();

    public StacCatalogSynchronizationHostedService(
        IHonuaConfigurationService configurationService,
        IMetadataRegistry metadataRegistry,
        IRasterStacCatalogSynchronizer rasterSynchronizer,
        ILogger<StacCatalogSynchronizationHostedService> logger,
        IVectorStacCatalogSynchronizer? vectorSynchronizer = null)
    {
        _configurationService = Guard.NotNull(configurationService);
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _rasterSynchronizer = Guard.NotNull(rasterSynchronizer);
        _vectorSynchronizer = vectorSynchronizer;
        _logger = Guard.NotNull(logger);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("HONUA_SKIP_STAC_SYNCHRONIZATION").EqualsIgnoreCase("1"))
        {
            _logger.LogInformation("STAC synchronization skipped due to HONUA_SKIP_STAC_SYNCHRONIZATION=1 environment variable.");
            return;
        }

        if (!StacRequestHelpers.IsStacEnabled(_configurationService))
        {
            return;
        }

        try
        {
            await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            // Synchronize both raster and vector data
            await _rasterSynchronizer.SynchronizeAllAsync(cancellationToken).ConfigureAwait(false);

            if (_vectorSynchronizer is not null)
            {
                await _vectorSynchronizer.SynchronizeAllVectorLayersAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("STAC catalog initialized with raster and vector data.");
            }
            else
            {
                _logger.LogInformation("STAC catalog initialized with raster data only.");
            }

            RegisterForMetadataChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize STAC catalog store.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _changeTokenRegistration?.Dispose();
        _changeTokenRegistration = null;

        // Cancel any in-progress synchronization
        _synchronizationCts?.Cancel();
        _synchronizationCts?.Dispose();
        _synchronizationCts = null;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _changeTokenRegistration?.Dispose();
        _synchronizationCts?.Dispose();
    }

    private void RegisterForMetadataChanges()
    {
        _changeTokenRegistration?.Dispose();
        IChangeToken changeToken;
        try
        {
            changeToken = _metadataRegistry.GetChangeToken();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _changeTokenRegistration = changeToken.RegisterChangeCallback(_ => OnMetadataChanged(), state: null);
    }

    private void OnMetadataChanged()
    {
        // Implement debouncing and cancellation to prevent unbounded task creation
        lock (_syncLock)
        {
            // Cancel any existing synchronization task
            _synchronizationCts?.Cancel();
            _synchronizationCts?.Dispose();
            _synchronizationCts = new CancellationTokenSource();

            // Only start a new task if the previous one is complete
            if (_synchronizationTask?.IsCompleted != false)
            {
                _synchronizationTask = SynchronizeWithDebounceAsync(_synchronizationCts.Token);
            }
        }
    }

    private async Task SynchronizeWithDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Debounce: wait 500ms to batch multiple rapid metadata changes
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Metadata changed, starting STAC catalog synchronization");

            // Synchronize both raster and vector data
            await _rasterSynchronizer.SynchronizeAllAsync(cancellationToken).ConfigureAwait(false);

            if (_vectorSynchronizer is not null)
            {
                await _vectorSynchronizer.SynchronizeAllVectorLayersAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("STAC catalog synchronized successfully after metadata change");
        }
        catch (OperationCanceledException)
        {
            // Expected when a new change arrives - previous sync was cancelled
            _logger.LogDebug("STAC catalog synchronization cancelled due to new metadata change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize STAC catalog after metadata change");
        }
        finally
        {
            // Re-register for the next change
            RegisterForMetadataChanges();
        }
    }
}
