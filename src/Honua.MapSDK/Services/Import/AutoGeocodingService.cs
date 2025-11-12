// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Honua.MapSDK.Models.BatchGeocoding;
using Honua.MapSDK.Models.Import;
using Honua.MapSDK.Services.BatchGeocoding;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Service for automatic geocoding on data upload.
/// Orchestrates address detection, geocoding, and result application.
/// </summary>
public class AutoGeocodingService
{
    private readonly AddressDetectionService _addressDetectionService;
    private readonly BatchGeocodingService _batchGeocodingService;
    private readonly CsvGeocodingService _csvGeocodingService;
    private readonly ILogger<AutoGeocodingService> _logger;

    // In-memory storage for geocoding sessions (in production, use distributed cache or database)
    private readonly Dictionary<string, AutoGeocodingSession> _sessions = new();
    private readonly object _sessionsLock = new();

    public AutoGeocodingService(
        AddressDetectionService addressDetectionService,
        BatchGeocodingService batchGeocodingService,
        CsvGeocodingService csvGeocodingService,
        ILogger<AutoGeocodingService> logger)
    {
        _addressDetectionService = addressDetectionService ?? throw new ArgumentNullException(nameof(addressDetectionService));
        _batchGeocodingService = batchGeocodingService ?? throw new ArgumentNullException(nameof(batchGeocodingService));
        _csvGeocodingService = csvGeocodingService ?? throw new ArgumentNullException(nameof(csvGeocodingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes uploaded data and detects address columns.
    /// Returns candidates for user confirmation.
    /// </summary>
    public async Task<AddressDetectionResponse> DetectAddressColumnsAsync(
        string datasetId,
        ParsedData parsedData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Detecting address columns for dataset {DatasetId}", datasetId);

        // Detect address columns
        var candidates = _addressDetectionService.DetectAddressColumns(parsedData);
        var suggestedConfig = _addressDetectionService.SuggestAddressConfiguration(parsedData);

        // Count rows with existing geometry
        var rowsWithGeometry = parsedData.Features.Count(f => f.Geometry != null);
        var rowsToGeocode = parsedData.Features.Count - rowsWithGeometry;

        // Generate sample addresses
        var sampleAddresses = new List<string>();
        if (suggestedConfig != null)
        {
            sampleAddresses = await ExtractSampleAddressesAsync(
                parsedData,
                suggestedConfig,
                5);
        }

        var response = new AddressDetectionResponse
        {
            DatasetId = datasetId,
            Candidates = candidates,
            SuggestedConfiguration = suggestedConfig,
            TotalRows = parsedData.Features.Count,
            RowsWithGeometry = rowsWithGeometry,
            RowsToGeocode = rowsToGeocode,
            SampleAddresses = sampleAddresses
        };

        _logger.LogInformation(
            "Detected {CandidateCount} address candidates for dataset {DatasetId}. Suggested: {Suggested}",
            candidates.Count,
            datasetId,
            suggestedConfig != null ? "Yes" : "No");

        return response;
    }

    /// <summary>
    /// Starts automatic geocoding operation with the given configuration.
    /// </summary>
    public async Task<AutoGeocodingResult> StartGeocodingAsync(
        AutoGeocodingRequest request,
        IProgress<AutoGeocodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting auto-geocoding session {SessionId} for dataset {DatasetId} with provider {Provider}",
            sessionId,
            request.DatasetId,
            request.Provider);

        try
        {
            // Create session
            var session = new AutoGeocodingSession
            {
                SessionId = sessionId,
                DatasetId = request.DatasetId,
                Status = AutoGeocodingStatus.InProgress,
                StartTime = startTime
            };

            lock (_sessionsLock)
            {
                _sessions[sessionId] = session;
            }

            // Extract addresses from parsed data
            var addresses = await ExtractAddressesAsync(
                request.ParsedData,
                request.AddressConfiguration);

            // Filter features to geocode
            var featuresToGeocode = request.ParsedData.Features
                .Where(f => !request.SkipExistingGeometry || f.Geometry == null)
                .ToList();

            if (featuresToGeocode.Count == 0)
            {
                _logger.LogInformation("No features to geocode - all already have geometry");
                return CreateEmptyResult(request, sessionId, startTime);
            }

            // Prepare batch geocoding request
            var batchRequest = new BatchGeocodingRequest
            {
                Addresses = addresses,
                Provider = request.Provider,
                ServerUrl = "http://localhost:5000", // TODO: Get from configuration
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                MaxRetries = 3,
                TimeoutMs = 10000
            };

            // Execute batch geocoding with progress reporting
            var batchProgress = new Progress<BatchGeocodingProgress>(batchProg =>
            {
                var autoProgress = MapToAutoGeocodingProgress(
                    sessionId,
                    request.DatasetId,
                    batchProg,
                    featuresToGeocode.Count,
                    request.ParsedData.Features.Count - featuresToGeocode.Count);

                progress?.Report(autoProgress);
            });

            var batchResult = await _batchGeocodingService.ProcessBatchAsync(
                batchRequest,
                batchProgress,
                cancellationToken);

            // Apply geocoding results to features
            var geocodedFeatures = await ApplyGeocodingResultsAsync(
                featuresToGeocode,
                batchResult,
                request.MinConfidenceThreshold);

            // Calculate statistics
            var statistics = CalculateStatistics(
                geocodedFeatures,
                request.ParsedData.Features.Count,
                request.ParsedData.Features.Count - featuresToGeocode.Count,
                batchResult);

            // Update session
            lock (_sessionsLock)
            {
                if (_sessions.TryGetValue(sessionId, out var s))
                {
                    s.Status = statistics.FailedCount > 0
                        ? AutoGeocodingStatus.CompletedWithErrors
                        : AutoGeocodingStatus.Completed;
                    s.EndTime = DateTime.UtcNow;
                }
            }

            var result = new AutoGeocodingResult
            {
                DatasetId = request.DatasetId,
                SessionId = sessionId,
                Status = statistics.FailedCount > 0
                    ? AutoGeocodingStatus.CompletedWithErrors
                    : AutoGeocodingStatus.Completed,
                Statistics = statistics,
                GeocodedFeatures = geocodedFeatures,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Provider = request.Provider,
                AddressConfiguration = request.AddressConfiguration
            };

            _logger.LogInformation(
                "Auto-geocoding session {SessionId} completed: {Success}/{Total} successful",
                sessionId,
                statistics.SuccessCount,
                statistics.ProcessedRows);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-geocoding session {SessionId} failed", sessionId);

            lock (_sessionsLock)
            {
                if (_sessions.TryGetValue(sessionId, out var s))
                {
                    s.Status = AutoGeocodingStatus.Failed;
                    s.EndTime = DateTime.UtcNow;
                }
            }

            return new AutoGeocodingResult
            {
                DatasetId = request.DatasetId,
                SessionId = sessionId,
                Status = AutoGeocodingStatus.Failed,
                Statistics = new AutoGeocodingStatistics
                {
                    TotalRows = request.ParsedData.Features.Count
                },
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Provider = request.Provider,
                AddressConfiguration = request.AddressConfiguration,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Retries failed geocoding for specific rows.
    /// </summary>
    public async Task<AutoGeocodingResult> RetryFailedGeocodingAsync(
        RetryGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrying failed geocoding for session {SessionId}, dataset {DatasetId}",
            request.SessionId,
            request.DatasetId);

        // In a real implementation, you would:
        // 1. Load the original session data
        // 2. Filter to failed rows
        // 3. Re-run geocoding with potentially different provider or config
        // 4. Merge results with original session

        throw new NotImplementedException("Retry functionality will be implemented based on session storage mechanism");
    }

    /// <summary>
    /// Gets the status of a geocoding session.
    /// </summary>
    public AutoGeocodingSession? GetSession(string sessionId)
    {
        lock (_sessionsLock)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }

    /// <summary>
    /// Extracts addresses from parsed data using the configured address extraction.
    /// </summary>
    private async Task<List<string>> ExtractAddressesAsync(
        ParsedData parsedData,
        AddressConfiguration config)
    {
        var addresses = new List<string>();

        foreach (var feature in parsedData.Features)
        {
            var address = ExtractAddressFromFeature(feature, config);
            addresses.Add(address ?? string.Empty);
        }

        await Task.CompletedTask;
        return addresses;
    }

    /// <summary>
    /// Extracts sample addresses for preview.
    /// </summary>
    private async Task<List<string>> ExtractSampleAddressesAsync(
        ParsedData parsedData,
        AddressConfiguration config,
        int count)
    {
        var samples = new List<string>();
        var features = parsedData.Features.Take(count);

        foreach (var feature in features)
        {
            var address = ExtractAddressFromFeature(feature, config);
            if (!string.IsNullOrWhiteSpace(address))
            {
                samples.Add(address);
            }
        }

        await Task.CompletedTask;
        return samples;
    }

    /// <summary>
    /// Extracts address string from a feature using the configuration.
    /// </summary>
    private string? ExtractAddressFromFeature(ParsedFeature feature, AddressConfiguration config)
    {
        if (config.Type == AddressConfigurationType.SingleColumn)
        {
            if (string.IsNullOrEmpty(config.SingleColumnName))
                return null;

            return feature.Properties.TryGetValue(config.SingleColumnName, out var value)
                ? value?.ToString()
                : null;
        }
        else if (config.Type == AddressConfigurationType.MultiColumn)
        {
            if (config.MultiColumnNames == null || !config.MultiColumnNames.Any())
                return null;

            var parts = new List<string>();
            foreach (var columnName in config.MultiColumnNames)
            {
                if (feature.Properties.TryGetValue(columnName, out var value) &&
                    !string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    parts.Add(value.ToString()!);
                }
            }

            return parts.Any() ? string.Join(config.Separator, parts) : null;
        }

        return null;
    }

    /// <summary>
    /// Applies geocoding results to features and creates geocoded feature list.
    /// </summary>
    private async Task<List<GeocodedFeature>> ApplyGeocodingResultsAsync(
        List<ParsedFeature> features,
        BatchGeocodingResult batchResult,
        double minConfidenceThreshold)
    {
        var geocodedFeatures = new List<GeocodedFeature>();

        for (int i = 0; i < features.Count && i < batchResult.Matches.Count; i++)
        {
            var feature = features[i];
            var match = batchResult.Matches[i];

            var status = DetermineFeatureStatus(match, minConfidenceThreshold);

            GeocodedFeature geocodedFeature;

            if (status == GeocodingFeatureStatus.Success && match.Latitude.HasValue && match.Longitude.HasValue)
            {
                // Add geometry to feature
                feature.Geometry = new Dictionary<string, object>
                {
                    ["type"] = "Point",
                    ["coordinates"] = new[] { match.Longitude.Value, match.Latitude.Value }
                };

                geocodedFeature = new GeocodedFeature
                {
                    FeatureId = feature.Id ?? i.ToString(),
                    RowNumber = feature.RowNumber,
                    OriginalAddress = match.OriginalAddress,
                    MatchedAddress = match.MatchedAddress,
                    Geometry = feature.Geometry,
                    Latitude = match.Latitude.Value,
                    Longitude = match.Longitude.Value,
                    Confidence = match.Confidence,
                    Status = status,
                    Properties = feature.Properties
                };
            }
            else
            {
                geocodedFeature = new GeocodedFeature
                {
                    FeatureId = feature.Id ?? i.ToString(),
                    RowNumber = feature.RowNumber,
                    OriginalAddress = match.OriginalAddress,
                    MatchedAddress = match.MatchedAddress,
                    Confidence = match.Confidence,
                    Status = status,
                    ErrorMessage = match.ErrorMessage,
                    Properties = feature.Properties
                };
            }

            geocodedFeatures.Add(geocodedFeature);
        }

        await Task.CompletedTask;
        return geocodedFeatures;
    }

    /// <summary>
    /// Determines feature status based on geocoding match.
    /// </summary>
    private GeocodingFeatureStatus DetermineFeatureStatus(GeocodingMatch match, double minConfidenceThreshold)
    {
        if (match.Status == GeocodingStatus.Success || match.Status == GeocodingStatus.Ambiguous)
        {
            if (match.Confidence.HasValue && match.Confidence.Value < minConfidenceThreshold)
            {
                return GeocodingFeatureStatus.LowConfidence;
            }

            if (match.Status == GeocodingStatus.Ambiguous)
            {
                return GeocodingFeatureStatus.Ambiguous;
            }

            return GeocodingFeatureStatus.Success;
        }

        return GeocodingFeatureStatus.Failed;
    }

    /// <summary>
    /// Calculates statistics for auto-geocoding operation.
    /// </summary>
    private AutoGeocodingStatistics CalculateStatistics(
        List<GeocodedFeature> features,
        int totalRows,
        int skippedCount,
        BatchGeocodingResult batchResult)
    {
        var successCount = features.Count(f => f.Status == GeocodingFeatureStatus.Success);
        var failedCount = features.Count(f => f.Status == GeocodingFeatureStatus.Failed);
        var ambiguousCount = features.Count(f => f.Status == GeocodingFeatureStatus.Ambiguous);

        var avgConfidence = features
            .Where(f => f.Confidence.HasValue)
            .Select(f => f.Confidence!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return new AutoGeocodingStatistics
        {
            TotalRows = totalRows,
            ProcessedRows = features.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            SkippedCount = skippedCount,
            AmbiguousCount = ambiguousCount,
            AverageConfidence = avgConfidence,
            AverageTimeMs = batchResult.Statistics.AverageTimeMs
        };
    }

    /// <summary>
    /// Maps batch geocoding progress to auto-geocoding progress.
    /// </summary>
    private AutoGeocodingProgress MapToAutoGeocodingProgress(
        string sessionId,
        string datasetId,
        BatchGeocodingProgress batchProgress,
        int totalToGeocode,
        int skippedCount)
    {
        var stats = new AutoGeocodingStatistics
        {
            TotalRows = totalToGeocode + skippedCount,
            ProcessedRows = batchProgress.Statistics.ProcessedCount,
            SuccessCount = batchProgress.Statistics.SuccessCount,
            FailedCount = batchProgress.Statistics.FailedCount,
            SkippedCount = skippedCount,
            AmbiguousCount = batchProgress.Statistics.AmbiguousCount,
            AverageConfidence = 0, // Will be calculated at the end
            AverageTimeMs = batchProgress.Statistics.AverageTimeMs
        };

        return new AutoGeocodingProgress
        {
            DatasetId = datasetId,
            SessionId = sessionId,
            Statistics = stats,
            CurrentAddress = batchProgress.CurrentAddress,
            RecentErrors = batchProgress.RecentErrors ?? new List<string>(),
            EstimatedTimeRemaining = batchProgress.Statistics.EstimatedTimeRemaining,
            Status = AutoGeocodingStatus.InProgress
        };
    }

    /// <summary>
    /// Creates an empty result when no features need geocoding.
    /// </summary>
    private AutoGeocodingResult CreateEmptyResult(
        AutoGeocodingRequest request,
        string sessionId,
        DateTime startTime)
    {
        return new AutoGeocodingResult
        {
            DatasetId = request.DatasetId,
            SessionId = sessionId,
            Status = AutoGeocodingStatus.Completed,
            Statistics = new AutoGeocodingStatistics
            {
                TotalRows = request.ParsedData.Features.Count,
                ProcessedRows = 0,
                SuccessCount = 0,
                FailedCount = 0,
                SkippedCount = request.ParsedData.Features.Count,
                AmbiguousCount = 0,
                AverageConfidence = 0,
                AverageTimeMs = 0
            },
            GeocodedFeatures = new List<GeocodedFeature>(),
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            Provider = request.Provider,
            AddressConfiguration = request.AddressConfiguration
        };
    }
}

/// <summary>
/// Internal session tracking for auto-geocoding operations.
/// </summary>
public class AutoGeocodingSession
{
    public required string SessionId { get; set; }
    public required string DatasetId { get; set; }
    public AutoGeocodingStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
