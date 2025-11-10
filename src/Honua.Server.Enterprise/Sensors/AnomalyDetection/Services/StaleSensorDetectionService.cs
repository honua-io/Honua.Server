// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;

/// <summary>
/// Service for detecting stale sensors (sensors with no recent data)
/// </summary>
public sealed class StaleSensorDetectionService : IStaleSensorDetectionService
{
    private readonly IAnomalyDetectionRepository _repository;
    private readonly IOptionsMonitor<AnomalyDetectionOptions> _options;
    private readonly ILogger<StaleSensorDetectionService> _logger;

    public StaleSensorDetectionService(
        IAnomalyDetectionRepository repository,
        IOptionsMonitor<AnomalyDetectionOptions> options,
        ILogger<StaleSensorDetectionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SensorAnomaly>> DetectStaleDatastreamsAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue.StaleSensorDetection;

        if (!opts.Enabled)
        {
            _logger.LogDebug("Stale sensor detection is disabled");
            return Array.Empty<SensorAnomaly>();
        }

        _logger.LogInformation("Starting stale sensor detection for tenant {TenantId}", tenantId ?? "all");

        var staleDatastreams = await _repository.GetStaleDatastreamsAsync(
            opts.StaleThreshold,
            tenantId,
            ct);

        if (staleDatastreams.Count == 0)
        {
            _logger.LogInformation("No stale sensors detected");
            return Array.Empty<SensorAnomaly>();
        }

        _logger.LogInformation("Found {Count} stale sensors", staleDatastreams.Count);

        var anomalies = new List<SensorAnomaly>();

        foreach (var staleDs in staleDatastreams)
        {
            // Check for threshold override based on observed property
            var threshold = opts.ThresholdOverrides.TryGetValue(
                staleDs.ObservedPropertyName,
                out var overrideThreshold)
                    ? overrideThreshold
                    : opts.StaleThreshold;

            // Skip if this datastream has a different threshold and hasn't exceeded it
            if (staleDs.TimeSinceLastObservation < threshold)
            {
                continue;
            }

            var severity = DetermineSeverity(staleDs.TimeSinceLastObservation, threshold);

            var anomaly = new SensorAnomaly
            {
                Type = staleDs.LastObservationTime == null
                    ? AnomalyType.SensorOffline
                    : AnomalyType.StaleSensor,
                Severity = severity,
                DatastreamId = staleDs.DatastreamId,
                DatastreamName = staleDs.DatastreamName,
                ThingId = staleDs.ThingId,
                ThingName = staleDs.ThingName,
                SensorId = staleDs.SensorId,
                SensorName = staleDs.SensorName,
                ObservedPropertyName = staleDs.ObservedPropertyName,
                LastObservationTime = staleDs.LastObservationTime,
                TimeSinceLastObservation = staleDs.TimeSinceLastObservation,
                Description = staleDs.LastObservationTime == null
                    ? $"Sensor '{staleDs.SensorName}' has never reported data for datastream '{staleDs.DatastreamName}'"
                    : $"Sensor '{staleDs.SensorName}' has not reported data for {FormatTimeSpan(staleDs.TimeSinceLastObservation)} " +
                      $"(last observation at {staleDs.LastObservationTime:yyyy-MM-dd HH:mm:ss} UTC)",
                TenantId = staleDs.TenantId,
                Metadata = new Dictionary<string, object>
                {
                    ["threshold"] = threshold.ToString(),
                    ["expected_interval"] = threshold.TotalMinutes,
                    ["actual_interval"] = staleDs.TimeSinceLastObservation.TotalMinutes
                }
            };

            anomalies.Add(anomaly);

            _logger.LogWarning(
                "Stale sensor detected: {SensorName} ({DatastreamName}) - last observation {TimeSince} ago",
                staleDs.SensorName,
                staleDs.DatastreamName,
                FormatTimeSpan(staleDs.TimeSinceLastObservation));
        }

        _logger.LogInformation("Detected {Count} stale sensor anomalies", anomalies.Count);

        return anomalies;
    }

    private static AnomalySeverity DetermineSeverity(TimeSpan timeSinceLastObservation, TimeSpan threshold)
    {
        // Critical if more than 2x the threshold
        if (timeSinceLastObservation > threshold * 2)
        {
            return AnomalySeverity.Critical;
        }

        // Warning if more than 1.5x the threshold
        if (timeSinceLastObservation > threshold * 1.5)
        {
            return AnomalySeverity.Warning;
        }

        // Info if just past threshold
        return AnomalySeverity.Info;
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.MaxValue)
        {
            return "never";
        }

        if (timeSpan.TotalDays >= 1)
        {
            return $"{timeSpan.TotalDays:F1} days";
        }

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.TotalHours:F1} hours";
        }

        return $"{timeSpan.TotalMinutes:F0} minutes";
    }
}

/// <summary>
/// Interface for stale sensor detection service
/// </summary>
public interface IStaleSensorDetectionService
{
    /// <summary>
    /// Detects datastreams with no recent observations
    /// </summary>
    Task<IReadOnlyList<SensorAnomaly>> DetectStaleDatastreamsAsync(
        string? tenantId = null,
        CancellationToken ct = default);
}
