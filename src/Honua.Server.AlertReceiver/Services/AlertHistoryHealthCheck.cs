// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Verifies the alert history database is reachable and schema creation succeeded.
/// </summary>
public sealed class AlertHistoryHealthCheck : IHealthCheck
{
    private readonly IAlertHistoryStore _historyStore;
    private readonly ILogger<AlertHistoryHealthCheck> _logger;

    public AlertHistoryHealthCheck(IAlertHistoryStore historyStore, ILogger<AlertHistoryHealthCheck> logger)
    {
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyStore.CheckConnectivityAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Alert history store reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alert history connectivity check failed.");
            return HealthCheckResult.Unhealthy("Alert history store unreachable.", ex);
        }
    }
}
