// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;

/// <summary>
/// Background service that orchestrates sensor anomaly detection
/// Periodically runs stale sensor and unusual reading detection,
/// then sends alerts via webhooks
/// </summary>
public sealed class AnomalyDetectionBackgroundService : BackgroundService
{
    private readonly IStaleSensorDetectionService _staleSensorDetection;
    private readonly IUnusualReadingDetectionService _unusualReadingDetection;
    private readonly IAnomalyAlertService _alertService;
    private readonly IOptionsMonitor<AnomalyDetectionOptions> _options;
    private readonly ILogger<AnomalyDetectionBackgroundService> _logger;

    // Health tracking
    private DateTime _lastCheckTime = DateTime.MinValue;
    private DateTime _lastSuccessfulCheckTime = DateTime.MinValue;
    private int _consecutiveFailures = 0;
    private bool _isHealthy = true;
    private string? _lastError = null;

    public AnomalyDetectionBackgroundService(
        IStaleSensorDetectionService staleSensorDetection,
        IUnusualReadingDetectionService unusualReadingDetection,
        IAnomalyAlertService alertService,
        IOptionsMonitor<AnomalyDetectionOptions> options,
        ILogger<AnomalyDetectionBackgroundService> logger)
    {
        _staleSensorDetection = staleSensorDetection ?? throw new ArgumentNullException(nameof(staleSensorDetection));
        _unusualReadingDetection = unusualReadingDetection ?? throw new ArgumentNullException(nameof(unusualReadingDetection));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Health status for health checks
    /// </summary>
    public AnomalyDetectionHealthStatus GetHealthStatus()
    {
        return new AnomalyDetectionHealthStatus
        {
            IsHealthy = _isHealthy,
            LastCheckTime = _lastCheckTime,
            LastSuccessfulCheckTime = _lastSuccessfulCheckTime,
            ConsecutiveFailures = _consecutiveFailures,
            LastError = _lastError
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;

        if (!opts.Enabled)
        {
            _logger.LogInformation("Anomaly detection is disabled - service will not run");
            return;
        }

        _logger.LogInformation("Anomaly detection background service started (check interval: {Interval})", opts.CheckInterval);

        // Initial delay to allow services to start up
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformAnomalyDetectionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in anomaly detection cycle");
                _consecutiveFailures++;
                _lastError = ex.Message;

                // Mark unhealthy after 3 consecutive failures
                if (_consecutiveFailures >= 3)
                {
                    _isHealthy = false;
                }
            }

            // Wait for the configured interval before next check
            var interval = _options.CurrentValue.CheckInterval;
            _logger.LogDebug("Next anomaly detection check in {Interval}", interval);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("Anomaly detection background service stopped");
    }

    private async Task PerformAnomalyDetectionAsync(CancellationToken ct)
    {
        _lastCheckTime = DateTime.UtcNow;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting anomaly detection cycle");

        try
        {
            var allAnomalies = new List<SensorAnomaly>();

            // 1. Detect stale sensors
            var opts = _options.CurrentValue;
            if (opts.StaleSensorDetection.Enabled)
            {
                _logger.LogDebug("Running stale sensor detection");
                var staleAnomalies = await _staleSensorDetection.DetectStaleDatastreamsAsync(
                    tenantId: null, // Check all tenants
                    ct: ct);

                allAnomalies.AddRange(staleAnomalies);
                _logger.LogInformation("Found {Count} stale sensor anomalies", staleAnomalies.Count);
            }

            // 2. Detect unusual readings
            if (opts.UnusualReadingDetection.Enabled)
            {
                _logger.LogDebug("Running unusual reading detection");
                var unusualReadings = await _unusualReadingDetection.DetectUnusualReadingsAsync(
                    tenantId: null, // Check all tenants
                    ct: ct);

                allAnomalies.AddRange(unusualReadings);
                _logger.LogInformation("Found {Count} unusual reading anomalies", unusualReadings.Count);
            }

            // 3. Send alerts
            if (allAnomalies.Count > 0)
            {
                _logger.LogInformation("Sending {Count} anomaly alerts", allAnomalies.Count);

                var alertResult = await _alertService.SendAnomalyAlertsAsync(allAnomalies, ct);

                _logger.LogInformation(
                    "Alert delivery completed: {Sent} sent, {Skipped} skipped, {Failed} failed",
                    alertResult.AlertsSent,
                    alertResult.AlertsSkipped,
                    alertResult.AlertsFailed);

                if (alertResult.Errors?.Count > 0)
                {
                    foreach (var error in alertResult.Errors)
                    {
                        _logger.LogWarning("Alert error: {Error}", error);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No anomalies detected");
            }

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Anomaly detection cycle completed in {Duration:F2}s", duration);

            // Mark as healthy and reset failure count
            _consecutiveFailures = 0;
            _isHealthy = true;
            _lastError = null;
            _lastSuccessfulCheckTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing anomaly detection");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Anomaly detection background service is stopping");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Health status for the anomaly detection service
/// </summary>
public sealed record AnomalyDetectionHealthStatus
{
    public bool IsHealthy { get; init; }
    public DateTime LastCheckTime { get; init; }
    public DateTime LastSuccessfulCheckTime { get; init; }
    public int ConsecutiveFailures { get; init; }
    public string? LastError { get; init; }
}
