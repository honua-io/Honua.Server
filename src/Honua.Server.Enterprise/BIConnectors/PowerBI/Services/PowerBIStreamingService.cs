// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Honua.Server.Enterprise.BIConnectors.PowerBI.Configuration;
using Honua.Server.Enterprise.Sensors.Models;

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Services;

/// <summary>
/// Implementation of Power BI streaming service with rate limiting and batching
/// </summary>
public class PowerBIStreamingService : IPowerBIStreamingService
{
    private readonly PowerBIOptions _options;
    private readonly IPowerBIDatasetService _datasetService;
    private readonly ILogger<PowerBIStreamingService> _logger;
    private readonly ConcurrentQueue<object> _streamingQueue = new();
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private CancellationTokenSource? _autoStreamingCts;
    private Task? _autoStreamingTask;

    public PowerBIStreamingService(
        PowerBIOptions options,
        IPowerBIDatasetService datasetService,
        ILogger<PowerBIStreamingService> logger)
    {
        _options = options;
        _datasetService = datasetService;
        _logger = logger;

        // Rate limiter: Power BI allows 10,000 rows per hour
        // We'll use a semaphore to limit concurrent pushes
        _rateLimitSemaphore = new SemaphoreSlim(15, 15); // 15 concurrent requests max
    }

    public async Task StreamObservationAsync(
        Observation observation,
        Datastream datastream,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnablePushDatasets)
        {
            return;
        }

        try
        {
            // Find the streaming dataset configuration for this datastream
            var streamingConfig = _options.StreamingDatasets
                .FirstOrDefault(c => c.SourceType == "Observations" &&
                                   c.DatastreamIds.Contains(datastream.Id));

            if (streamingConfig == null || string.IsNullOrEmpty(streamingConfig.DatasetId))
            {
                _logger.LogDebug("No streaming dataset configured for datastream {DatastreamId}", datastream.Id);
                return;
            }

            // Convert observation to Power BI row
            var row = ConvertObservationToRow(observation, datastream);

            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                await _datasetService.PushRowsAsync(
                    streamingConfig.DatasetId,
                    "Observations",
                    new[] { row },
                    cancellationToken);

                _logger.LogDebug("Streamed observation {ObservationId} to Power BI dataset {DatasetId}",
                    observation.Id, streamingConfig.DatasetId);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming observation {ObservationId} to Power BI", observation.Id);
            // Don't throw - we don't want to break the observation creation flow
        }
    }

    public async Task StreamObservationsAsync(
        IEnumerable<(Observation Observation, Datastream Datastream)> observations,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnablePushDatasets)
        {
            return;
        }

        try
        {
            var observationsList = observations.ToList();
            if (observationsList.Count == 0)
            {
                return;
            }

            // Group by streaming dataset
            var groups = observationsList
                .Select(o => new
                {
                    Observation = o.Observation,
                    Datastream = o.Datastream,
                    Config = _options.StreamingDatasets.FirstOrDefault(c =>
                        c.SourceType == "Observations" &&
                        c.DatastreamIds.Contains(o.Datastream.Id))
                })
                .Where(x => x.Config != null && !string.IsNullOrEmpty(x.Config.DatasetId))
                .GroupBy(x => x.Config!.DatasetId);

            foreach (var group in groups)
            {
                var rows = group.Select(x => ConvertObservationToRow(x.Observation, x.Datastream)).ToList();

                await _rateLimitSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await _datasetService.PushRowsAsync(
                        group.Key!,
                        "Observations",
                        rows,
                        cancellationToken);

                    _logger.LogDebug("Streamed {Count} observations to Power BI dataset {DatasetId}",
                        rows.Count, group.Key);
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming observation batch to Power BI");
        }
    }

    public async Task StreamAnomalyAlertAsync(
        string datastreamId,
        double observedValue,
        double expectedValue,
        double threshold,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnablePushDatasets)
        {
            return;
        }

        try
        {
            var alertConfig = _options.StreamingDatasets
                .FirstOrDefault(c => c.SourceType == "Alerts");

            if (alertConfig == null || string.IsNullOrEmpty(alertConfig.DatasetId))
            {
                _logger.LogDebug("No streaming dataset configured for anomaly alerts");
                return;
            }

            var row = new
            {
                DatastreamId = datastreamId,
                ObservedValue = observedValue,
                ExpectedValue = expectedValue,
                Threshold = threshold,
                Deviation = Math.Abs(observedValue - expectedValue),
                Severity = CalculateSeverity(observedValue, expectedValue, threshold),
                Timestamp = timestamp.UtcDateTime
            };

            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                await _datasetService.PushRowsAsync(
                    alertConfig.DatasetId,
                    "Alerts",
                    new[] { row },
                    cancellationToken);

                _logger.LogInformation("Streamed anomaly alert for datastream {DatastreamId} to Power BI", datastreamId);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming anomaly alert to Power BI");
        }
    }

    public Task StartAutoStreamingAsync(CancellationToken cancellationToken = default)
    {
        if (_autoStreamingTask != null)
        {
            _logger.LogWarning("Auto-streaming is already running");
            return Task.CompletedTask;
        }

        _autoStreamingCts = new CancellationTokenSource();
        _autoStreamingTask = Task.Run(() => AutoStreamingLoopAsync(_autoStreamingCts.Token), cancellationToken);

        _logger.LogInformation("Started Power BI auto-streaming");
        return Task.CompletedTask;
    }

    public async Task StopAutoStreamingAsync()
    {
        if (_autoStreamingCts == null || _autoStreamingTask == null)
        {
            return;
        }

        _autoStreamingCts.Cancel();
        await _autoStreamingTask;
        _autoStreamingCts.Dispose();
        _autoStreamingCts = null;
        _autoStreamingTask = null;

        _logger.LogInformation("Stopped Power BI auto-streaming");
    }

    private async Task AutoStreamingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Process queued items
                var batch = new List<object>();
                while (batch.Count < _options.StreamingBatchSize && _streamingQueue.TryDequeue(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count > 0)
                {
                    // Push batch to Power BI (implementation would go here)
                    _logger.LogDebug("Processed {Count} items from streaming queue", batch.Count);
                }

                // Wait before next iteration
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-streaming loop");
                await Task.Delay(5000, cancellationToken); // Back off on error
            }
        }
    }

    private object ConvertObservationToRow(Observation observation, Datastream datastream)
    {
        // Extract result value
        var resultValue = observation.Result switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetDouble(),
            double d => d,
            int i => (double)i,
            long l => (double)l,
            string s when double.TryParse(s, out var d) => d,
            _ => 0.0
        };

        return new
        {
            ObservationId = observation.Id,
            DatastreamId = datastream.Id,
            DatastreamName = datastream.Name,
            Result = resultValue,
            ResultTime = observation.ResultTime?.UtcDateTime ?? DateTime.UtcNow,
            PhenomenonTime = observation.PhenomenonTime?.UtcDateTime,
            UnitOfMeasurement = datastream.UnitOfMeasurement?.Symbol ?? string.Empty,
            ObservedPropertyName = datastream.ObservedPropertyId ?? string.Empty
        };
    }

    private string CalculateSeverity(double observed, double expected, double threshold)
    {
        var deviation = Math.Abs(observed - expected);
        var deviationPercent = expected != 0 ? (deviation / Math.Abs(expected)) * 100 : 0;

        return deviationPercent switch
        {
            >= 50 => "Critical",
            >= 25 => "High",
            >= 10 => "Medium",
            _ => "Low"
        };
    }
}
