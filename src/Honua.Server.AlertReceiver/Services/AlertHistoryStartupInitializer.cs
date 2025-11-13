// <copyright file="AlertHistoryStartupInitializer.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Ensures the alert history store is reachable and schema is created at startup.
/// </summary>
public sealed class AlertHistoryStartupInitializer : IHostedService
{
    private readonly IAlertHistoryStore historyStore;
    private readonly ILogger<AlertHistoryStartupInitializer> logger;

    public AlertHistoryStartupInitializer(
        IAlertHistoryStore historyStore,
        ILogger<AlertHistoryStartupInitializer> logger)
    {
        this.historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Validating alert history store connectivity...");
        await this.historyStore.CheckConnectivityAsync(cancellationToken).ConfigureAwait(false);
        this.logger.LogInformation("Alert history store connectivity verified.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
