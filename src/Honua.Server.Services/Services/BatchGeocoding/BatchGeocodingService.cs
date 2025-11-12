// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Services.Models.BatchGeocoding;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Services.BatchGeocoding;

/// <summary>
/// Service for batch geocoding operations with rate limiting and retry logic.
/// </summary>
public class BatchGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BatchGeocodingService> _logger;
    private readonly Dictionary<string, RateLimiter> _rateLimiters;
    private readonly SemaphoreSlim _rateLimiterLock;

    public BatchGeocodingService(
        HttpClient httpClient,
        ILogger<BatchGeocodingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiters = new Dictionary<string, RateLimiter>();
        _rateLimiterLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Processes a batch of addresses for geocoding.
    /// </summary>
    /// <param name="request">Batch geocoding request.</param>
    /// <param name="progress">Progress reporter for real-time updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch geocoding result with all matches.</returns>
    public async Task<BatchGeocodingResult> ProcessBatchAsync(
        BatchGeocodingRequest request,
        IProgress<BatchGeocodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var matches = new List<GeocodingMatch>();
        var recentErrors = new List<string>();
        var stopwatch = Stopwatch.StartNew();

        // Get or create rate limiter for provider
        var rateLimiter = await GetOrCreateRateLimiterAsync(request.Provider);

        // Initialize statistics
        var stats = new BatchGeocodingStatistics
        {
            TotalAddresses = request.Addresses.Count,
            ProcessedCount = 0,
            SuccessCount = 0,
            FailedCount = 0,
            AmbiguousCount = 0,
            AverageTimeMs = 0,
            EstimatedTimeRemaining = null
        };

        _logger.LogInformation(
            "Starting batch geocoding: {Count} addresses with provider {Provider}",
            request.Addresses.Count,
            request.Provider);

        // Process each address
        for (int i = 0; i < request.Addresses.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var address = request.Addresses[i];
            var addressStopwatch = Stopwatch.StartNew();

            // Report progress - current address
            ReportProgress(progress, stats, address, null, recentErrors);

            // Geocode address with retry logic
            var match = await GeocodeWithRetryAsync(
                address,
                i,
                request,
                rateLimiter,
                cancellationToken);

            addressStopwatch.Stop();
            match = match with { Duration = addressStopwatch.Elapsed };

            matches.Add(match);

            // Update statistics
            stats = UpdateStatistics(stats, match, stopwatch.Elapsed);

            // Track errors
            if (match.Status == GeocodingStatus.Error || match.Status == GeocodingStatus.Timeout)
            {
                var errorMsg = $"Row {i + 1}: {match.ErrorMessage ?? "Unknown error"}";
                recentErrors.Insert(0, errorMsg);
                if (recentErrors.Count > 10)
                {
                    recentErrors.RemoveAt(10);
                }
            }

            // Report progress - completed match
            ReportProgress(progress, stats, null, match, recentErrors);

            // Log progress every 10 addresses
            if ((i + 1) % 10 == 0)
            {
                _logger.LogInformation(
                    "Batch geocoding progress: {Processed}/{Total} addresses ({Percentage:F1}%)",
                    stats.ProcessedCount,
                    stats.TotalAddresses,
                    stats.ProgressPercentage);
            }
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Batch geocoding completed: {Success}/{Total} successful in {Duration:F2}s (avg {AvgMs:F0}ms per address)",
            stats.SuccessCount,
            stats.TotalAddresses,
            stopwatch.Elapsed.TotalSeconds,
            stats.AverageTimeMs);

        return new BatchGeocodingResult
        {
            Matches = matches,
            Statistics = stats,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            Provider = request.Provider
        };
    }

    /// <summary>
    /// Retries geocoding for a specific address.
    /// </summary>
    public async Task<GeocodingMatch> RetryGeocodeAsync(
        GeocodingMatch failedMatch,
        string provider,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var rateLimiter = await GetOrCreateRateLimiterAsync(provider);

        var request = new BatchGeocodingRequest
        {
            Addresses = new List<string> { failedMatch.OriginalAddress },
            Provider = provider,
            ServerUrl = serverUrl,
            MaxConcurrentRequests = 1,
            MaxRetries = 3
        };

        return await GeocodeWithRetryAsync(
            failedMatch.OriginalAddress,
            failedMatch.RowIndex,
            request,
            rateLimiter,
            cancellationToken);
    }

    private async Task<GeocodingMatch> GeocodeWithRetryAsync(
        string address,
        int rowIndex,
        BatchGeocodingRequest request,
        RateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= request.MaxRetries)
        {
            try
            {
                // Wait for rate limiter
                await rateLimiter.WaitAsync(cancellationToken);

                // Geocode address
                var match = await GeocodeAddressAsync(
                    address,
                    rowIndex,
                    request,
                    cancellationToken);

                return match with { RetryCount = retryCount };
            }
            catch (TaskCanceledException)
            {
                return new GeocodingMatch
                {
                    RowIndex = rowIndex,
                    OriginalAddress = address,
                    Status = GeocodingStatus.Timeout,
                    Quality = MatchQuality.Failed,
                    ErrorMessage = "Request timed out",
                    RetryCount = retryCount
                };
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                if (retryCount <= request.MaxRetries)
                {
                    // Exponential backoff
                    var delayMs = Math.Pow(2, retryCount) * 1000;
                    await Task.Delay((int)delayMs, cancellationToken);

                    _logger.LogWarning(
                        "Geocoding retry {Retry}/{MaxRetries} for address: {Address}",
                        retryCount,
                        request.MaxRetries,
                        address);
                }
            }
        }

        // All retries exhausted
        return new GeocodingMatch
        {
            RowIndex = rowIndex,
            OriginalAddress = address,
            Status = GeocodingStatus.Error,
            Quality = MatchQuality.Failed,
            ErrorMessage = lastException?.Message ?? "Geocoding failed after retries",
            RetryCount = retryCount - 1
        };
    }

    private async Task<GeocodingMatch> GeocodeAddressAsync(
        string address,
        int rowIndex,
        BatchGeocodingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return new GeocodingMatch
            {
                RowIndex = rowIndex,
                OriginalAddress = address,
                Status = GeocodingStatus.NoResults,
                Quality = MatchQuality.Failed,
                ErrorMessage = "Empty address"
            };
        }

        var url = $"{request.ServerUrl.TrimEnd('/')}/api/location/geocode?query={Uri.EscapeDataString(address)}&provider={request.Provider}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(request.TimeoutMs);

        var response = await _httpClient.GetAsync(url, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"HTTP {response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<GeocodingApiResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result?.Results == null || result.Results.Count == 0)
        {
            return new GeocodingMatch
            {
                RowIndex = rowIndex,
                OriginalAddress = address,
                Status = GeocodingStatus.NoResults,
                Quality = MatchQuality.Failed,
                ErrorMessage = "No results found"
            };
        }

        // Use first result
        var firstResult = result.Results[0];
        var confidence = firstResult.Confidence ?? 1.0;
        var quality = DetermineQuality(confidence, result.Results.Count);
        var status = result.Results.Count > 1
            ? GeocodingStatus.Ambiguous
            : GeocodingStatus.Success;

        return new GeocodingMatch
        {
            RowIndex = rowIndex,
            OriginalAddress = address,
            MatchedAddress = firstResult.FormattedAddress,
            Latitude = firstResult.Latitude,
            Longitude = firstResult.Longitude,
            Confidence = confidence,
            Quality = quality,
            Status = status,
            ResultType = firstResult.Type,
            BoundingBox = firstResult.BoundingBox,
            AlternativesCount = result.Results.Count - 1
        };
    }

    private MatchQuality DetermineQuality(double confidence, int resultCount)
    {
        if (resultCount == 1 && confidence > 0.9)
            return MatchQuality.Exact;
        if (resultCount == 1 && confidence >= 0.7)
            return MatchQuality.High;
        if (confidence >= 0.5 || resultCount > 1)
            return MatchQuality.Medium;
        if (confidence > 0)
            return MatchQuality.Low;

        return MatchQuality.Failed;
    }

    private BatchGeocodingStatistics UpdateStatistics(
        BatchGeocodingStatistics stats,
        GeocodingMatch match,
        TimeSpan elapsed)
    {
        var processedCount = stats.ProcessedCount + 1;
        var successCount = match.Status == GeocodingStatus.Success || match.Status == GeocodingStatus.Ambiguous
            ? stats.SuccessCount + 1
            : stats.SuccessCount;
        var failedCount = match.Status == GeocodingStatus.Error ||
                         match.Status == GeocodingStatus.NoResults ||
                         match.Status == GeocodingStatus.Timeout
            ? stats.FailedCount + 1
            : stats.FailedCount;
        var ambiguousCount = match.Status == GeocodingStatus.Ambiguous
            ? stats.AmbiguousCount + 1
            : stats.AmbiguousCount;

        var avgTimeMs = elapsed.TotalMilliseconds / processedCount;
        var remaining = stats.TotalAddresses - processedCount;
        var estimatedRemaining = remaining > 0
            ? TimeSpan.FromMilliseconds(avgTimeMs * remaining)
            : TimeSpan.Zero;

        return new BatchGeocodingStatistics
        {
            TotalAddresses = stats.TotalAddresses,
            ProcessedCount = processedCount,
            SuccessCount = successCount,
            FailedCount = failedCount,
            AmbiguousCount = ambiguousCount,
            AverageTimeMs = avgTimeMs,
            EstimatedTimeRemaining = estimatedRemaining
        };
    }

    private void ReportProgress(
        IProgress<BatchGeocodingProgress>? progress,
        BatchGeocodingStatistics stats,
        string? currentAddress,
        GeocodingMatch? lastMatch,
        List<string> recentErrors)
    {
        if (progress == null) return;

        progress.Report(new BatchGeocodingProgress
        {
            Statistics = stats,
            CurrentAddress = currentAddress,
            LastMatch = lastMatch,
            RecentErrors = recentErrors.ToList(),
            IsPaused = false,
            IsCancelled = false
        });
    }

    private async Task<RateLimiter> GetOrCreateRateLimiterAsync(string provider)
    {
        await _rateLimiterLock.WaitAsync();
        try
        {
            if (!_rateLimiters.TryGetValue(provider, out var rateLimiter))
            {
                var config = RateLimiterConfiguration.GetDefault(provider);
                rateLimiter = new RateLimiter(config.MaxRequests, config.TimeWindow);
                _rateLimiters[provider] = rateLimiter;
            }

            return rateLimiter;
        }
        finally
        {
            _rateLimiterLock.Release();
        }
    }

    /// <summary>
    /// API response structure matching server endpoint.
    /// </summary>
    private class GeocodingApiResponse
    {
        public string? Query { get; set; }
        public string? Provider { get; set; }
        public List<GeocodingApiResult>? Results { get; set; }
        public string? Attribution { get; set; }
    }

    private class GeocodingApiResult
    {
        public required string FormattedAddress { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double[]? BoundingBox { get; set; }
        public string? Type { get; set; }
        public double? Confidence { get; set; }
    }
}

/// <summary>
/// Rate limiter for controlling request frequency.
/// </summary>
public class RateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;
    private readonly Queue<DateTime> _requestTimes;
    private readonly SemaphoreSlim _lock;

    public RateLimiter(int maxRequests, TimeSpan timeWindow)
    {
        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
        _requestTimes = new Queue<DateTime>();
        _lock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Waits until a request can be made within rate limits.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;

            // Remove old requests outside time window
            while (_requestTimes.Count > 0 && (now - _requestTimes.Peek()) > _timeWindow)
            {
                _requestTimes.Dequeue();
            }

            // If at limit, wait until oldest request expires
            if (_requestTimes.Count >= _maxRequests)
            {
                var oldestRequest = _requestTimes.Peek();
                var waitTime = _timeWindow - (now - oldestRequest);
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }

                // Remove expired request
                _requestTimes.Dequeue();
            }

            // Record this request
            _requestTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _lock.Release();
        }
    }
}
