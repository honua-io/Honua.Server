// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public sealed class MetadataInitializationHostedService : IHostedService
{
    private readonly IMetadataRegistry _registry;
    private readonly ILogger<MetadataInitializationHostedService> _logger;

    public MetadataInitializationHostedService(IMetadataRegistry registry, ILogger<MetadataInitializationHostedService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_registry.IsInitialized)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Starting {ServiceName} initialization...", nameof(MetadataInitializationHostedService));

            await _registry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("{ServiceName} initialization completed successfully", nameof(MetadataInitializationHostedService));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "FATAL: {ServiceName} initialization failed. " +
                "This is typically caused by: " +
                "1. Database/Redis unreachable, " +
                "2. Invalid connection string, " +
                "3. Network connectivity issues, " +
                "4. Missing required configuration. " +
                "Application cannot start without this service.",
                nameof(MetadataInitializationHostedService));

            // For production: Fail fast with clear error
            throw new InvalidOperationException(
                $"{nameof(MetadataInitializationHostedService)} initialization failed. " +
                $"Check database connectivity and configuration. Error: {ex.Message}",
                ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
