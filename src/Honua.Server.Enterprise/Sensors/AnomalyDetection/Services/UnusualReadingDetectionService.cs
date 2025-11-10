// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;

/// <summary>
/// Service for detecting unusual readings using statistical methods
/// Identifies observations that are statistical outliers (e.g., 3 standard deviations from mean)
/// </summary>
public sealed class UnusualReadingDetectionService : IUnusualReadingDetectionService
{
    private readonly IAnomalyDetectionRepository _repository;
    private readonly IOptionsMonitor<AnomalyDetectionOptions> _options;
    private readonly ILogger<UnusualReadingDetectionService> _logger;

    public UnusualReadingDetectionService(
        IAnomalyDetectionRepository repository,
        IOptionsMonitor<AnomalyDetectionOptions> options,
        ILogger<UnusualReadingDetectionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SensorAnomaly>> DetectUnusualReadingsAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue.UnusualReadingDetection;

        if (!opts.Enabled)
        {
            _logger.LogDebug("Unusual reading detection is disabled");
            return Array.Empty<SensorAnomaly>();
        }

        _logger.LogInformation("Starting unusual reading detection for tenant {TenantId}", tenantId ?? "all");

        // Get all active datastreams
        var datastreams = await _repository.GetActiveDatastreamsAsync(tenantId, ct);

        if (datastreams.Count == 0)
        {
            _logger.LogInformation("No active datastreams found");
            return Array.Empty<SensorAnomaly>();
        }

        _logger.LogInformation("Checking {Count} datastreams for unusual readings", datastreams.Count);

        var anomalies = new List<SensorAnomaly>();

        foreach (var datastream in datastreams)
        {
            try
            {
                var datastreamAnomalies = await DetectDatastreamAnomaliesAsync(
                    datastream,
                    opts,
                    ct);

                anomalies.AddRange(datastreamAnomalies);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error detecting anomalies for datastream {DatastreamId} ({DatastreamName})",
                    datastream.DatastreamId,
                    datastream.DatastreamName);
            }
        }

        _logger.LogInformation("Detected {Count} unusual reading anomalies", anomalies.Count);

        return anomalies;
    }

    private async Task<List<SensorAnomaly>> DetectDatastreamAnomaliesAsync(
        DatastreamInfo datastream,
        UnusualReadingDetectionOptions opts,
        CancellationToken ct)
    {
        var anomalies = new List<SensorAnomaly>();

        // Get minimum observation count (with possible override)
        var minCount = opts.MinimumCountOverrides.TryGetValue(
            datastream.ObservedPropertyName,
            out var overrideCount)
                ? overrideCount
                : opts.MinimumObservationCount;

        // Skip if not enough observations for statistical analysis
        if (datastream.TotalObservations < minCount)
        {
            _logger.LogDebug(
                "Skipping datastream {DatastreamName} - insufficient observations ({Count}/{MinCount})",
                datastream.DatastreamName,
                datastream.TotalObservations,
                minCount);
            return anomalies;
        }

        // Get statistical summary
        var statistics = await _repository.GetDatastreamStatisticsAsync(
            datastream.DatastreamId,
            opts.StatisticalWindow,
            ct);

        if (statistics == null || statistics.ObservationCount < minCount)
        {
            _logger.LogDebug(
                "Skipping datastream {DatastreamName} - insufficient data for statistics",
                datastream.DatastreamName);
            return anomalies;
        }

        // If standard deviation is zero, all values are identical - no outliers possible
        if (statistics.StandardDeviation == 0)
        {
            _logger.LogDebug(
                "Skipping datastream {DatastreamName} - zero variance in data",
                datastream.DatastreamName);
            return anomalies;
        }

        // Get threshold (with possible override)
        var threshold = opts.ThresholdOverrides.TryGetValue(
            datastream.ObservedPropertyName,
            out var overrideThreshold)
                ? overrideThreshold
                : opts.StandardDeviationThreshold;

        // Get recent observations to check for outliers
        var recentObservations = await _repository.GetRecentObservationsAsync(
            datastream.DatastreamId,
            DateTime.UtcNow - TimeSpan.FromMinutes(10), // Last 10 minutes
            100,
            ct);

        foreach (var observation in recentObservations)
        {
            if (observation.NumericResult == null)
            {
                continue;
            }

            var value = observation.NumericResult.Value;
            var deviations = Math.Abs((value - statistics.Mean) / statistics.StandardDeviation);

            if (deviations >= threshold)
            {
                var severity = DetermineSeverity(deviations, threshold);

                var anomaly = new SensorAnomaly
                {
                    Type = AnomalyType.UnusualReading,
                    Severity = severity,
                    DatastreamId = datastream.DatastreamId,
                    DatastreamName = datastream.DatastreamName,
                    ThingId = datastream.ThingId,
                    ThingName = datastream.ThingName,
                    SensorId = datastream.SensorId,
                    SensorName = datastream.SensorName,
                    ObservedPropertyName = datastream.ObservedPropertyName,
                    AnomalousValue = value,
                    ExpectedValue = $"{statistics.Mean:F2} ± {statistics.StandardDeviation:F2}",
                    StandardDeviations = deviations,
                    Mean = statistics.Mean,
                    StdDev = statistics.StandardDeviation,
                    ObservationCount = statistics.ObservationCount,
                    Description = $"Unusual reading detected for '{datastream.DatastreamName}': " +
                                  $"value {value:F2} is {deviations:F1} standard deviations from mean " +
                                  $"(mean: {statistics.Mean:F2}, std dev: {statistics.StandardDeviation:F2})",
                    TenantId = datastream.TenantId,
                    Metadata = new Dictionary<string, object>
                    {
                        ["threshold"] = threshold,
                        ["observation_id"] = observation.Id,
                        ["phenomenon_time"] = observation.PhenomenonTime,
                        ["statistical_window"] = opts.StatisticalWindow.ToString(),
                        ["min"] = statistics.Min,
                        ["max"] = statistics.Max
                    }
                };

                anomalies.Add(anomaly);

                _logger.LogWarning(
                    "Unusual reading detected: {DatastreamName} = {Value} ({Deviations:F1} σ from mean {Mean:F2})",
                    datastream.DatastreamName,
                    value,
                    deviations,
                    statistics.Mean);
            }
        }

        return anomalies;
    }

    private static AnomalySeverity DetermineSeverity(double deviations, double threshold)
    {
        // Critical if more than 2x the threshold
        if (deviations >= threshold * 2)
        {
            return AnomalySeverity.Critical;
        }

        // Warning if more than 1.5x the threshold
        if (deviations >= threshold * 1.5)
        {
            return AnomalySeverity.Warning;
        }

        // Info if just past threshold
        return AnomalySeverity.Info;
    }
}

/// <summary>
/// Interface for unusual reading detection service
/// </summary>
public interface IUnusualReadingDetectionService
{
    /// <summary>
    /// Detects unusual readings that are statistical outliers
    /// </summary>
    Task<IReadOnlyList<SensorAnomaly>> DetectUnusualReadingsAsync(
        string? tenantId = null,
        CancellationToken ct = default);
}
