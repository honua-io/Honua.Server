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
/// Decorator that adds comprehensive monitoring and metrics to routing providers.
/// Tracks request count, duration, route distance/duration distributions, and waypoint counts.
/// </summary>
public class MonitoredRoutingProvider : IRoutingProvider
{
    private readonly IRoutingProvider _inner;
    private readonly LocationServiceMetrics _metrics;
    private readonly ILogger<MonitoredRoutingProvider> _logger;
    private readonly IMemoryCache? _cache;
    private readonly TimeSpan _cacheDuration;

    private const string ProviderType = "routing";

    public string ProviderKey => _inner.ProviderKey;
    public string ProviderName => _inner.ProviderName;

    /// <summary>
    /// Initializes a new monitored routing provider decorator.
    /// </summary>
    /// <param name="inner">Inner routing provider to wrap.</param>
    /// <param name="metrics">Metrics collector for location services.</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <param name="cache">Optional memory cache for routing results.</param>
    /// <param name="cacheDuration">Cache duration (default: 15 minutes).</param>
    public MonitoredRoutingProvider(
        IRoutingProvider inner,
        LocationServiceMetrics metrics,
        ILogger<MonitoredRoutingProvider> logger,
        IMemoryCache? cache = null,
        TimeSpan? cacheDuration = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(15);
    }

    /// <inheritdoc/>
    public async Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = "calculate_route";
        var cacheKey = GenerateCacheKey(request);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ProviderType"] = ProviderType,
            ["ProviderKey"] = ProviderKey,
            ["Operation"] = operation,
            ["WaypointCount"] = request.Waypoints.Count,
            ["TravelMode"] = request.TravelMode
        });

        try
        {
            // Check cache first
            if (_cache != null && _cache.TryGetValue(cacheKey, out RoutingResponse? cachedResponse))
            {
                stopwatch.Stop();

                _metrics.RecordCacheOperation(ProviderType, operation, "hit");
                _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
                _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogDebug("Cache hit for routing request with {WaypointCount} waypoints",
                    request.Waypoints.Count);

                return cachedResponse!;
            }

            if (_cache != null)
            {
                _metrics.RecordCacheOperation(ProviderType, operation, "miss");
            }

            // Execute the actual routing request
            _logger.LogInformation(
                "Executing routing request with {WaypointCount} waypoints, travel mode: {TravelMode}",
                request.Waypoints.Count,
                request.TravelMode);

            var response = await _inner.CalculateRouteAsync(request, cancellationToken);
            stopwatch.Stop();

            // Record basic request metrics
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: true);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);

            // Record routing-specific metrics
            _metrics.RecordWaypointCount(ProviderKey, request.Waypoints.Count);

            // Record metrics for each route in the response
            foreach (var route in response.Routes)
            {
                _metrics.RecordRouteDistance(ProviderKey, route.DistanceMeters, request.TravelMode);
                _metrics.RecordRouteDuration(ProviderKey, route.DurationSeconds, request.TravelMode,
                    withTraffic: route.DurationWithTrafficSeconds.HasValue);

                _logger.LogInformation(
                    "Route calculated: Distance={DistanceKm:F2}km, Duration={DurationMin:F1}min, " +
                    "TrafficDuration={TrafficDurationMin:F1}min, Mode={TravelMode}",
                    route.DistanceMeters / 1000.0,
                    route.DurationSeconds / 60.0,
                    route.DurationWithTrafficSeconds.HasValue ? route.DurationWithTrafficSeconds.Value / 60.0 : 0,
                    request.TravelMode);
            }

            _logger.LogInformation(
                "Routing request completed successfully in {DurationMs}ms. Routes: {RouteCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                response.Routes.Count);

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
            _logger.LogWarning("Routing request was cancelled after {DurationMs}ms",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "http_error");

            _logger.LogError(ex,
                "HTTP error during routing request with {WaypointCount} waypoints: {ErrorMessage}",
                request.Waypoints.Count,
                ex.Message);
            throw;
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, "validation_error");

            _logger.LogError(ex,
                "Validation error during routing request: {ErrorMessage}",
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());

            _logger.LogError(ex,
                "Unexpected error during routing request with {WaypointCount} waypoints: {ErrorMessage}",
                request.Waypoints.Count,
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
            _logger.LogDebug("Testing connectivity to routing provider: {Provider}", ProviderKey);

            var isHealthy = await _inner.TestConnectivityAsync(cancellationToken);
            stopwatch.Stop();

            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: isHealthy);
            _metrics.RecordRequestDuration(ProviderType, ProviderKey, operation, stopwatch.Elapsed.TotalMilliseconds);
            _metrics.UpdateRoutingProviderHealth(isHealthy);

            if (isHealthy)
            {
                _logger.LogInformation(
                    "Routing provider {Provider} is healthy (responded in {DurationMs}ms)",
                    ProviderKey,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Routing provider {Provider} connectivity test failed",
                    ProviderKey);
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.RecordRequest(ProviderType, ProviderKey, operation, success: false);
            _metrics.RecordError(ProviderType, ProviderKey, operation, ex.GetType().Name.ToLowerInvariant());
            _metrics.UpdateRoutingProviderHealth(false);

            _logger.LogError(ex,
                "Error testing connectivity to routing provider {Provider}: {ErrorMessage}",
                ProviderKey,
                ex.Message);

            return false;
        }
    }

    private static string GenerateCacheKey(RoutingRequest request)
    {
        var waypoints = string.Join("|", request.Waypoints.Select(w => $"{w[0]},{w[1]}"));
        var key = $"routing:{request.TravelMode}:{waypoints}";

        if (request.AvoidTolls) key += ":no-tolls";
        if (request.AvoidHighways) key += ":no-highways";
        if (request.AvoidFerries) key += ":no-ferries";
        if (request.UseTraffic) key += ":traffic";
        if (request.DepartureTime.HasValue)
            key += $":depart-{request.DepartureTime.Value.ToUnixTimeSeconds()}";

        if (request.Vehicle != null)
        {
            key += $":vehicle-{request.Vehicle.WeightKg}-{request.Vehicle.HeightMeters}-" +
                   $"{request.Vehicle.WidthMeters}-{request.Vehicle.LengthMeters}";
        }

        return key;
    }
}
