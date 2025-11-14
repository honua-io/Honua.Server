// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using Honua.Server.Core.Configuration.V2;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Host.Extensions;
using Honua.Server.Core.Configuration.V2;
using Microsoft.Extensions.Hosting;
using Honua.Server.Core.Configuration.V2;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Configuration.V2;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Configuration.V2;

namespace Honua.Server.Host.Stac;

internal sealed class StacCatalogSynchronizationHostedService : IHostedService, IDisposable
{
    private readonly HonuaConfig? honuaConfig;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IRasterStacCatalogSynchronizer rasterSynchronizer;
    private readonly IVectorStacCatalogSynchronizer? vectorSynchronizer;
    private readonly ILogger<StacCatalogSynchronizationHostedService> logger;

    private IDisposable? _changeTokenRegistration;

    // Prevent unbounded task creation with debouncing and cancellation
    private Task? _synchronizationTask;
    private CancellationTokenSource? _synchronizationCts;
    private readonly object _syncLock = new();

    public StacCatalogSynchronizationHostedService(
        IMetadataRegistry metadataRegistry,
        IRasterStacCatalogSynchronizer rasterSynchronizer,
        ILogger<StacCatalogSynchronizationHostedService> logger,
        HonuaConfig? honuaConfig = null,
        IVectorStacCatalogSynchronizer? vectorSynchronizer = null)
    {
        this.honuaConfig = honuaConfig;
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.rasterSynchronizer = Guard.NotNull(rasterSynchronizer);
        this.vectorSynchronizer = vectorSynchronizer;
        this.logger = Guard.NotNull(logger);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("HONUA_SKIP_STAC_SYNCHRONIZATION").EqualsIgnoreCase("1"))
        {
            this.logger.LogInformation("STAC synchronization skipped due to HONUA_SKIP_STAC_SYNCHRONIZATION=1 environment variable.");
            return;
        }

        if (!StacRequestHelpers.IsStacEnabled(this.honuaConfig))
        {
            return;
        }

        try
        {
            await this.metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            // Synchronize both raster and vector data
            await this.rasterSynchronizer.SynchronizeAllAsync(cancellationToken).ConfigureAwait(false);

            if (this.vectorSynchronizer is not null)
            {
                await this.vectorSynchronizer.SynchronizeAllVectorLayersAsync(cancellationToken).ConfigureAwait(false);
                this.logger.LogInformation("STAC catalog initialized with raster and vector data.");
            }
            else
            {
                this.logger.LogInformation("STAC catalog initialized with raster data only.");
            }

            RegisterForMetadataChanges();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to initialize STAC catalog store.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _changeTokenRegistration?.Dispose();
        this._changeTokenRegistration = null;

        // Cancel any in-progress synchronization
        _synchronizationCts?.Cancel();
        _synchronizationCts?.Dispose();
        this._synchronizationCts = null;

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
            changeToken = this.metadataRegistry.GetChangeToken();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        this._changeTokenRegistration = changeToken.RegisterChangeCallback(_ => OnMetadataChanged(), state: null);
    }

    private void OnMetadataChanged()
    {
        // Implement debouncing and cancellation to prevent unbounded task creation
        lock (_syncLock)
        {
            // Cancel any existing synchronization task
            _synchronizationCts?.Cancel();
            _synchronizationCts?.Dispose();
            this._synchronizationCts = new CancellationTokenSource();

            // Only start a new task if the previous one is complete
            if (_synchronizationTask?.IsCompleted != false)
            {
                this._synchronizationTask = SynchronizeWithDebounceAsync(this._synchronizationCts.Token);
            }
        }
    }

    private async Task SynchronizeWithDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Debounce: wait 500ms to batch multiple rapid metadata changes
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug("Metadata changed, starting STAC catalog synchronization");

            // Synchronize both raster and vector data
            await this.rasterSynchronizer.SynchronizeAllAsync(cancellationToken).ConfigureAwait(false);

            if (this.vectorSynchronizer is not null)
            {
                await this.vectorSynchronizer.SynchronizeAllVectorLayersAsync(cancellationToken).ConfigureAwait(false);
            }

            this.logger.LogInformation("STAC catalog synchronized successfully after metadata change");
        }
        catch (OperationCanceledException)
        {
            // Expected when a new change arrives - previous sync was cancelled
            this.logger.LogDebug("STAC catalog synchronization cancelled due to new metadata change");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to synchronize STAC catalog after metadata change");
        }
        finally
        {
            // Re-register for the next change
            RegisterForMetadataChanges();
        }
    }
}
