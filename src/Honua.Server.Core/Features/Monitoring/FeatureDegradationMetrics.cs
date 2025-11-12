// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Monitoring;

/// <summary>
/// Metrics collector for feature degradation monitoring.
/// </summary>
public sealed class FeatureDegradationMetrics : IHostedService, IDisposable
{
    private readonly IFeatureManagementService _featureManagement;
    private readonly Meter _meter;
    private readonly ILogger<FeatureDegradationMetrics> _logger;
    private PeriodicTimer? _periodicTimer;
    private Task? _updateTask;
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly ObservableGauge<int> _degradedFeaturesGauge;
    private readonly ObservableGauge<int> _unavailableFeaturesGauge;
    private readonly ObservableGauge<int> _healthyFeaturesGauge;
    private readonly Counter<long> _degradationEventsCounter;
    private readonly Counter<long> _recoveryEventsCounter;
    private readonly Histogram<double> _healthScoreHistogram;

    private int _lastDegradedCount;
    private int _lastUnavailableCount;
    private int _lastHealthyCount;

    public FeatureDegradationMetrics(
        IFeatureManagementService featureManagement,
        ILogger<FeatureDegradationMetrics> logger)
    {
        _featureManagement = featureManagement ?? throw new ArgumentNullException(nameof(featureManagement));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _meter = new Meter("Honua.Features", "1.0.0");

        // Gauges for current state
        _degradedFeaturesGauge = _meter.CreateObservableGauge(
            "honua_degraded_features_count",
            () => _lastDegradedCount,
            description: "Number of features currently in degraded state");

        _unavailableFeaturesGauge = _meter.CreateObservableGauge(
            "honua_unavailable_features_count",
            () => _lastUnavailableCount,
            description: "Number of features currently unavailable");

        _healthyFeaturesGauge = _meter.CreateObservableGauge(
            "honua_healthy_features_count",
            () => _lastHealthyCount,
            description: "Number of features currently healthy");

        // Counters for events
        _degradationEventsCounter = _meter.CreateCounter<long>(
            "honua_feature_degradation_events_total",
            description: "Total number of feature degradation events");

        _recoveryEventsCounter = _meter.CreateCounter<long>(
            "honua_feature_recovery_events_total",
            description: "Total number of feature recovery events");

        // Histogram for health scores
        _healthScoreHistogram = _meter.CreateHistogram<double>(
            "honua_feature_health_score",
            unit: "score",
            description: "Feature health scores (0-100)");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Feature degradation metrics collector starting");

        // Update metrics every 30 seconds
        _cancellationTokenSource = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _updateTask = UpdateMetricsLoopAsync(_cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    private async Task UpdateMetricsLoopAsync(CancellationToken cancellationToken)
    {
        // Perform initial update immediately
        await UpdateMetricsAsync(cancellationToken);

        // Continue periodic updates
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_periodicTimer == null || !await _periodicTimer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }

                await UpdateMetricsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics update loop");
            }
        }
    }

    private async Task UpdateMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var statuses = await _featureManagement.GetAllFeatureStatusesAsync(cancellationToken).ConfigureAwait(false);

            var degradedCount = statuses.Count(kvp => kvp.Value.IsDegraded);
            var unavailableCount = statuses.Count(kvp => !kvp.Value.IsAvailable);
            var healthyCount = statuses.Count(kvp =>
                kvp.Value.State == FeatureDegradationState.Healthy);

            // Detect state changes
            if (degradedCount > _lastDegradedCount)
            {
                var newlyDegraded = degradedCount - _lastDegradedCount;
                _degradationEventsCounter.Add(newlyDegraded);

                _logger.LogWarning(
                    "Feature degradation detected: {Count} features newly degraded",
                    newlyDegraded);
            }

            if (degradedCount < _lastDegradedCount)
            {
                var recovered = _lastDegradedCount - degradedCount;
                _recoveryEventsCounter.Add(recovered);

                _logger.LogInformation(
                    "Feature recovery detected: {Count} features recovered",
                    recovered);
            }

            // Update gauge values
            _lastDegradedCount = degradedCount;
            _lastUnavailableCount = unavailableCount;
            _lastHealthyCount = healthyCount;

            // Record health scores
            foreach (var status in statuses.Values)
            {
                _healthScoreHistogram.Record(
                    status.HealthScore,
                    new KeyValuePair<string, object?>("feature", status.Name),
                    new KeyValuePair<string, object?>("state", status.State.ToString()));
            }

            _logger.LogDebug(
                "Feature metrics updated: Healthy={Healthy}, Degraded={Degraded}, Unavailable={Unavailable}",
                healthyCount,
                degradedCount,
                unavailableCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature degradation metrics");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Feature degradation metrics collector stopping");

        // Cancel the update loop
        _cancellationTokenSource?.Cancel();

        // Wait for the update task to complete (with a timeout)
        if (_updateTask != null)
        {
            try
            {
                await Task.WhenAny(_updateTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _periodicTimer?.Dispose();
        _meter.Dispose();
    }
}

/// <summary>
/// Alert conditions for feature degradation.
/// </summary>
public sealed class FeatureDegradationAlerts
{
    /// <summary>
    /// Checks if alerts should be triggered based on degradation state.
    /// </summary>
    public static List<DegradationAlert> CheckAlerts(
        Dictionary<string, FeatureStatus> statuses,
        TimeSpan degradationDurationThreshold)
    {
        var alerts = new List<DegradationAlert>();
        var now = DateTimeOffset.UtcNow;

        foreach (var (featureName, status) in statuses)
        {
            // Alert if degraded for too long
            if (status.IsDegraded || !status.IsAvailable)
            {
                var degradationDuration = now - status.StateChangedAt;

                if (degradationDuration > degradationDurationThreshold)
                {
                    alerts.Add(new DegradationAlert
                    {
                        Severity = status.IsAvailable ? AlertSeverity.Warning : AlertSeverity.Critical,
                        Feature = featureName,
                        Message = $"Feature '{featureName}' has been {status.State} for {degradationDuration.TotalMinutes:F1} minutes",
                        Duration = degradationDuration,
                        Reason = status.DegradationReason ?? "Unknown",
                        HealthScore = status.HealthScore
                    });
                }
            }
        }

        return alerts;
    }
}

/// <summary>
/// Degradation alert information.
/// </summary>
public sealed class DegradationAlert
{
    public required AlertSeverity Severity { get; init; }
    public required string Feature { get; init; }
    public required string Message { get; init; }
    public TimeSpan Duration { get; init; }
    public required string Reason { get; init; }
    public int HealthScore { get; init; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
