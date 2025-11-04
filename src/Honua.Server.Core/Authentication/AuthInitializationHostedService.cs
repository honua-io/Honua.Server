// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

internal sealed class AuthInitializationHostedService : IHostedService
{
    private readonly IAuthRepository _repository;
    private readonly ILogger<AuthInitializationHostedService> _logger;

    public AuthInitializationHostedService(IAuthRepository repository, ILogger<AuthInitializationHostedService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting {ServiceName} initialization...", nameof(AuthInitializationHostedService));

            await _repository.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("{ServiceName} initialization completed successfully", nameof(AuthInitializationHostedService));
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
                nameof(AuthInitializationHostedService));

            // For production: Fail fast with clear error
            throw new InvalidOperationException(
                $"{nameof(AuthInitializationHostedService)} initialization failed. " +
                $"Check database connectivity and configuration. Error: {ex.Message}",
                ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
