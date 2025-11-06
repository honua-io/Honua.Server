// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Observability;

/// <summary>
/// Decorator that adds comprehensive monitoring and metrics to geocoding providers.
/// Tracks request count, duration, success/error rates, cache hit/miss rates, and result quality.
/// </summary>
public class MonitoredGeocodingProvider : IGeocodingProvider
{
    private readonly IGeocodingProvider _inner;
    private readonly LocationServiceMetrics _metrics;
    private readonly ILogger<MonitoredGeocodingProvider> _logger;
    private readonly IMemoryCache? _cache;
    private readonly TimeSpan _cacheDuration;

    private const string ProviderType = "geocoding";

    public string ProviderKey => _inner.ProviderKey;
    public string ProviderName => _inner.ProviderName;

    /// <summary>
    /// Initializes a new monitored geocoding provider decorator.
    /// </summary>
    /// <param name="inner">Inner geocoding provider to wrap.</param>
    /// <param name="metrics">Metrics collector for location services.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="cache">Optional memory cache for geocoding results.</param>
    /// <param name="cacheDuration">Cache duration (default: 1 hour).</param>
    public MonitoredGeocodingProvider(
        IGeocodingProvider inner,
        LocationServiceMetrics metrics,
        ILogger<MonitoredGeocodingProvider> logger,
        IMemoryCache? cache = null,
        TimeSpan? cacheDuration = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
        _cacheDuration = cacheDuration ?? TimeSpan.FromHours(1);
    }

    /// <inheritdoc/>
    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "geocode";
        var cacheKey = GenerateCacheKey("forward", request.Query, request.BoundingBox, request.CountryCodes);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation,
            ["Query"] = request.Query
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out GeocodingResponse? cachedResponse))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for geocoding request: {Query}", request.Query);

                return cachedResponse!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual geocoding request
            _logger.LogInformation("Executing geocoding request for query: {Query}", request.Query);

            var response = await _inner.GeocodeAsync(request, cancellationToken);
            stopwatch.Stop();

            // Record metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.RecordGeocodingResults(ProviderKey, response.Results.Count, "forward");

            // Record confidence scores
            foreach (var result in response.Results.Where(r => r.Confidence.HasValue))
            {
                _metrics.RecordGeocodingConfidence(ProviderKey, result.Confidence.Value);
            }

            _logger.LogInformation(
                "Geocoding request completed successfully in {DurationMs}ms. Results: {ResultCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                response.Results.Count);

            // Cache the successful response
            if (_cache != null)
            {
                _cache.Set(cacheKey, response, _cacheDuration);
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _metrics.RecordError(ProviderType, ProviderKey, operation, "cancelled");
            _logger.LogWarning("Geocoding request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "http_error");

            _logger.LogError(ex,
                "HTTP error during geocoding request for query {Query}: {ErrorMessage}",
                request.Query,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Unexpected error during geocoding request for query {Query}: {ErrorMessage}",
                request.Query,
                ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "reverse_geocode";
        var cacheKey = GenerateCacheKey("reverse", $"{request.Longitude},{request.Latitude}",
            request.Language, request.ResultTypes);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation,
            ["Longitude"] = request.Longitude,
            ["Latitude"] = request.Latitude
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out GeocodingResponse? cachedResponse))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for reverse geocoding request: ({Lon},{Lat})",
                    request.Longitude, request.Latitude);

                return cachedResponse!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual reverse geocoding request
            _logger.LogInformation("Executing reverse geocoding request for coordinates: ({Lon},{Lat})",
                request.Longitude, request.Latitude);

            var response = await _inner.ReverseGeocodeAsync(request, cancellationToken);
            stopwatch.Stop();

            // Record metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.RecordGeocodingResults(ProviderKey, response.Results.Count, "reverse");

            // Record confidence scores
            foreach (var result in response.Results.Where(r => r.Confidence.HasValue))
            {
                _metrics.RecordGeocodingConfidence(ProviderKey, result.Confidence.Value);
            }

            _logger.LogInformation(
                "Reverse geocoding request completed successfully in {DurationMs}ms. Results: {ResultCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                response.Results.Count);

            // Cache the successful response
            if (_cache != null)
            {
                _cache.Set(cacheKey, response, _cacheDuration);
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _metrics.RecordError(ProviderType, ProviderKey, operation, "cancelled");
            _logger.LogWarning("Reverse geocoding request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "http_error");

            _logger.LogError(ex,
                "HTTP error during reverse geocoding request for coordinates ({Lon},{Lat}): {ErrorMessage}",
                request.Longitude,
                request.Latitude,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Unexpected error during reverse geocoding request for coordinates ({Lon},{Lat}): {ErrorMessage}",
                request.Longitude,
                request.Latitude,
                ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "test_connectivity";

        try
        {
            _logger.LogDebug("Testing connectivity to geocoding provider: {Provider}", ProviderKey);

            var isHealthy = await _inner.TestConnectivityAsync(cancellationToken);
            stopwatch.Stop();

            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: isHealthy);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.UpdateGeocodingProviderHealth(isHealthy);

            if (isHealthy)
            {
                _logger.LogInformation(
                    "Geocoding provider {Provider} is healthy (responded in {DurationMs}ms)",
                    ProviderKey,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Geocoding provider {Provider} connectivity test failed",
                    ProviderKey);
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());
            _metrics.UpdateGeocodingProviderHealth(false);

            _logger.LogError(ex,
                "Error testing connectivity to geocoding provider {Provider}: {ErrorMessage}",
                ProviderKey,
                ex.Message);

            return false;
        }
    }

    private static string GenerateCacheKey(string operation, params object?[] parameters)
    {
        var key = $"geocoding:{operation}";
        foreach (var param in parameters)
        {
            if (param != null)
            {
                var paramStr = param switch
                {
                    Array arr => string.Join(",", arr.Cast<object>()),
                    _ => param.ToString()
                };
                key += $":{paramStr}";
            }
        }
        return key;
    }
}
