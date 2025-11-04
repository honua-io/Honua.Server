// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Ensures the alert history store is reachable and schema is created at startup.
/// </summary>
public sealed class AlertHistoryStartupInitializer : IHostedService
{
    private readonly IAlertHistoryStore _historyStore;
    private readonly ILogger<AlertHistoryStartupInitializer> _logger;

    public AlertHistoryStartupInitializer(
        IAlertHistoryStore historyStore,
        ILogger<AlertHistoryStartupInitializer> logger)
    {
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating alert history store connectivity...");
        await _historyStore.CheckConnectivityAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Alert history store connectivity verified.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
