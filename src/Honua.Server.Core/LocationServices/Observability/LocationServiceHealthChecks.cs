// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Observability;

/// <summary>
/// Health check for geocoding service providers.
/// Tests connectivity to the geocoding service and verifies it can handle requests.
/// </summary>
public class GeocodingProviderHealthCheck : IHealthCheck
{
    private readonly IGeocodingProvider _provider;
    private readonly ILogger<GeocodingProviderHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public GeocodingProviderHealthCheck(
        IGeocodingProvider provider,
        ILogger<GeocodingProviderHealthCheck> logger,
        TimeSpan? timeout = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var isHealthy = await _provider.TestConnectivityAsync(cts.Token);

            var data = new Dictionary<string, object>
            {
                ["provider_key"] = _provider.ProviderKey,
                ["provider_name"] = _provider.ProviderName,
                ["provider_type"] = "geocoding"
            };

            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    $"Geocoding provider '{_provider.ProviderName}' is responding normally",
                    data);
            }
            else
            {
                return HealthCheckResult.Degraded(
                    $"Geocoding provider '{_provider.ProviderName}' connectivity test failed",
                    data: data);
            }
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                $"Geocoding provider '{_provider.ProviderName}' health check timed out after {_timeout.TotalSeconds}s",
                data: new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["timeout_seconds"] = _timeout.TotalSeconds
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Health check failed for geocoding provider {Provider}",
                _provider.ProviderKey);

            return HealthCheckResult.Unhealthy(
                $"Geocoding provider '{_provider.ProviderName}' health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}

/// <summary>
/// Health check for routing service providers.
/// Tests connectivity to the routing service and verifies it can handle requests.
/// </summary>
public class RoutingProviderHealthCheck : IHealthCheck
{
    private readonly IRoutingProvider _provider;
    private readonly ILogger<RoutingProviderHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public RoutingProviderHealthCheck(
        IRoutingProvider provider,
        ILogger<RoutingProviderHealthCheck> logger,
        TimeSpan? timeout = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var isHealthy = await _provider.TestConnectivityAsync(cts.Token);

            var data = new Dictionary<string, object>
            {
                ["provider_key"] = _provider.ProviderKey,
                ["provider_name"] = _provider.ProviderName,
                ["provider_type"] = "routing"
            };

            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    $"Routing provider '{_provider.ProviderName}' is responding normally",
                    data);
            }
            else
            {
                return HealthCheckResult.Degraded(
                    $"Routing provider '{_provider.ProviderName}' connectivity test failed",
                    data: data);
            }
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                $"Routing provider '{_provider.ProviderName}' health check timed out after {_timeout.TotalSeconds}s",
                data: new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["timeout_seconds"] = _timeout.TotalSeconds
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Health check failed for routing provider {Provider}",
                _provider.ProviderKey);

            return HealthCheckResult.Unhealthy(
                $"Routing provider '{_provider.ProviderName}' health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}

/// <summary>
/// Health check for basemap tile service providers.
/// Tests connectivity to the tile service and verifies it can serve tiles.
/// </summary>
public class BasemapTileProviderHealthCheck : IHealthCheck
{
    private readonly IBasemapTileProvider _provider;
    private readonly ILogger<BasemapTileProviderHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public BasemapTileProviderHealthCheck(
        IBasemapTileProvider provider,
        ILogger<BasemapTileProviderHealthCheck> logger,
        TimeSpan? timeout = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var isHealthy = await _provider.TestConnectivityAsync(cts.Token);

            var data = new Dictionary<string, object>
            {
                ["provider_key"] = _provider.ProviderKey,
                ["provider_name"] = _provider.ProviderName,
                ["provider_type"] = "basemap"
            };

            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    $"Basemap provider '{_provider.ProviderName}' is responding normally",
                    data);
            }
            else
            {
                return HealthCheckResult.Degraded(
                    $"Basemap provider '{_provider.ProviderName}' connectivity test failed",
                    data: data);
            }
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                $"Basemap provider '{_provider.ProviderName}' health check timed out after {_timeout.TotalSeconds}s",
                data: new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["timeout_seconds"] = _timeout.TotalSeconds
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Health check failed for basemap provider {Provider}",
                _provider.ProviderKey);

            return HealthCheckResult.Unhealthy(
                $"Basemap provider '{_provider.ProviderName}' health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["provider_key"] = _provider.ProviderKey,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}

/// <summary>
/// Aggregate health check for all location services.
/// Checks geocoding, routing, and basemap providers and aggregates their health status.
/// </summary>
public class LocationServicesHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IGeocodingProvider> _geocodingProviders;
    private readonly IEnumerable<IRoutingProvider> _routingProviders;
    private readonly IEnumerable<IBasemapTileProvider> _basemapProviders;
    private readonly ILogger<LocationServicesHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public LocationServicesHealthCheck(
        IEnumerable<IGeocodingProvider> geocodingProviders,
        IEnumerable<IRoutingProvider> routingProviders,
        IEnumerable<IBasemapTileProvider> basemapProviders,
        ILogger<LocationServicesHealthCheck> logger,
        TimeSpan? timeout = null)
    {
        _geocodingProviders = geocodingProviders ?? Enumerable.Empty<IGeocodingProvider>();
        _routingProviders = routingProviders ?? Enumerable.Empty<IRoutingProvider>();
        _basemapProviders = basemapProviders ?? Enumerable.Empty<IBasemapTileProvider>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var healthyCount = 0;
        var degradedCount = 0;
        var unhealthyCount = 0;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            // Check geocoding providers
            var geocodingTasks = _geocodingProviders
                .Select(async p =>
                {
                    try
                    {
                        var healthy = await p.TestConnectivityAsync(cts.Token);
                        return (Provider: p.ProviderKey, Type: "geocoding", Healthy: healthy);
                    }
                    catch
                    {
                        return (Provider: p.ProviderKey, Type: "geocoding", Healthy: false);
                    }
                });

            // Check routing providers
            var routingTasks = _routingProviders
                .Select(async p =>
                {
                    try
                    {
                        var healthy = await p.TestConnectivityAsync(cts.Token);
                        return (Provider: p.ProviderKey, Type: "routing", Healthy: healthy);
                    }
                    catch
                    {
                        return (Provider: p.ProviderKey, Type: "routing", Healthy: false);
                    }
                });

            // Check basemap providers
            var basemapTasks = _basemapProviders
                .Select(async p =>
                {
                    try
                    {
                        var healthy = await p.TestConnectivityAsync(cts.Token);
                        return (Provider: p.ProviderKey, Type: "basemap", Healthy: healthy);
                    }
                    catch
                    {
                        return (Provider: p.ProviderKey, Type: "basemap", Healthy: false);
                    }
                });

            var allTasks = geocodingTasks.Concat(routingTasks).Concat(basemapTasks);
            var results = await Task.WhenAll(allTasks);

            foreach (var result in results)
            {
                data[$"{result.Type}_{result.Provider}"] = result.Healthy ? "healthy" : "unhealthy";
                if (result.Healthy)
                    healthyCount++;
                else
                    unhealthyCount++;
            }

            data["total_providers"] = results.Length;
            data["healthy_count"] = healthyCount;
            data["degraded_count"] = degradedCount;
            data["unhealthy_count"] = unhealthyCount;

            if (unhealthyCount == 0)
            {
                return HealthCheckResult.Healthy(
                    $"All {healthyCount} location service providers are healthy",
                    data);
            }
            else if (healthyCount > 0)
            {
                return HealthCheckResult.Degraded(
                    $"{healthyCount} of {results.Length} location service providers are healthy",
                    data: data);
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    $"All {unhealthyCount} location service providers are unhealthy",
                    data: data);
            }
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                $"Location services health check timed out after {_timeout.TotalSeconds}s",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregate location services health check failed");

            return HealthCheckResult.Unhealthy(
                $"Location services health check failed: {ex.Message}",
                ex,
                data);
        }
    }
}
