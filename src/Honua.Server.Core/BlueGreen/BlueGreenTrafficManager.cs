// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.BlueGreen;

/// <summary>
/// Manages blue-green deployment traffic switching using YARP reverse proxy.
/// Allows gradual traffic migration from blue (current) to green (new) deployment.
/// </summary>
public sealed class BlueGreenTrafficManager
{
    private readonly ILogger<BlueGreenTrafficManager> _logger;
    private readonly InMemoryConfigProvider? _configProvider;
    private IReadOnlyList<RouteConfig> _routes = new List<RouteConfig>();
    private IReadOnlyList<ClusterConfig> _clusters = new List<ClusterConfig>();

    public BlueGreenTrafficManager(
        ILogger<BlueGreenTrafficManager> logger,
        IProxyConfigProvider? proxyConfigProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configProvider = proxyConfigProvider as InMemoryConfigProvider;

        if (_configProvider == null && proxyConfigProvider != null)
        {
            _logger.LogWarning("ProxyConfigProvider is not InMemoryConfigProvider. Traffic switching will not be applied to running proxy.");
        }
    }

    /// <summary>
    /// Switches traffic from blue to green environment with specified percentage.
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="blueEndpoint">Blue (current) deployment endpoint</param>
    /// <param name="greenEndpoint">Green (new) deployment endpoint</param>
    /// <param name="greenTrafficPercentage">Percentage of traffic to route to green (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task<TrafficSwitchResult> SwitchTrafficAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        int greenTrafficPercentage,
        CancellationToken cancellationToken)
    {
        try
        {
            if (greenTrafficPercentage < 0 || greenTrafficPercentage > 100)
            {
                throw new ArgumentException("Green traffic percentage must be between 0 and 100", nameof(greenTrafficPercentage));
            }

            _logger.LogInformation(
                "Switching traffic for {Service}: Blue={Blue}% Green={Green}%",
                serviceName,
                100 - greenTrafficPercentage,
                greenTrafficPercentage);

            // Create weighted destinations
            var destinations = new Dictionary<string, DestinationConfig>();

            if (greenTrafficPercentage < 100)
            {
                destinations["blue"] = new DestinationConfig
                {
                    Address = blueEndpoint,
                    Health = blueEndpoint + "/health",
                    Metadata = new Dictionary<string, string>
                    {
                        ["weight"] = (100 - greenTrafficPercentage).ToString()
                    }
                };
            }

            if (greenTrafficPercentage > 0)
            {
                destinations["green"] = new DestinationConfig
                {
                    Address = greenEndpoint,
                    Health = greenEndpoint + "/health",
                    Metadata = new Dictionary<string, string>
                    {
                        ["weight"] = greenTrafficPercentage.ToString()
                    }
                };
            }

            // Create cluster configuration with weighted routing
            var cluster = new ClusterConfig
            {
                ClusterId = serviceName,
                Destinations = destinations,
                LoadBalancingPolicy = "WeightedRoundRobin",
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(10),
                        Timeout = TimeSpan.FromSeconds(5),
                        Policy = "ConsecutiveFailures",
                        Path = "/health"
                    }
                }
            };

            // Create route configuration
            var route = new RouteConfig
            {
                RouteId = $"{serviceName}-route",
                ClusterId = serviceName,
                Match = new RouteMatch
                {
                    Path = "/{**catch-all}"
                }
            };

            // Apply the configuration to the proxy
            if (_configProvider != null)
            {
                // Update internal state
                var existingClusters = _clusters.Where(c => c.ClusterId != serviceName).ToList();
                existingClusters.Add(cluster);
                _clusters = existingClusters;

                var existingRoutes = _routes.Where(r => r.RouteId != route.RouteId).ToList();
                existingRoutes.Add(route);
                _routes = existingRoutes;

                // Update the proxy configuration
                _configProvider.Update(_routes, _clusters);

                _logger.LogInformation("Traffic split applied to proxy: {BlueWeight}% blue, {GreenWeight}% green",
                    100 - greenTrafficPercentage, greenTrafficPercentage);
            }
            else
            {
                _logger.LogWarning("No InMemoryConfigProvider available. Configuration built but not applied.");
            }

            return Task.FromResult(new TrafficSwitchResult
            {
                Success = true,
                BlueTrafficPercentage = 100 - greenTrafficPercentage,
                GreenTrafficPercentage = greenTrafficPercentage,
                Message = $"Traffic switched: {100 - greenTrafficPercentage}% blue, {greenTrafficPercentage}% green"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch traffic for {Service}", serviceName);
            return Task.FromResult(new TrafficSwitchResult
            {
                Success = false,
                Message = $"Failed to switch traffic: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Performs gradual traffic migration from blue to green.
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="blueEndpoint">Blue deployment endpoint</param>
    /// <param name="greenEndpoint">Green deployment endpoint</param>
    /// <param name="strategy">Canary deployment strategy</param>
    /// <param name="healthCheckFunc">Function to check green deployment health</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CanaryDeploymentResult> PerformCanaryDeploymentAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CanaryStrategy strategy,
        Func<CancellationToken, Task<bool>> healthCheckFunc,
        CancellationToken cancellationToken)
    {
        var stages = new List<CanaryStage>();

        try
        {
            _logger.LogInformation("Starting canary deployment for {Service}", serviceName);

            // Execute canary stages
            foreach (var percentage in strategy.TrafficSteps)
            {
                _logger.LogInformation("Canary stage: routing {Percentage}% to green", percentage);

                // Switch traffic
                var switchResult = await SwitchTrafficAsync(
                    serviceName,
                    blueEndpoint,
                    greenEndpoint,
                    percentage,
                    cancellationToken);

                if (!switchResult.Success)
                {
                    throw new InvalidOperationException($"Traffic switch failed: {switchResult.Message}");
                }

                // Wait for soak period
                _logger.LogInformation("Soaking for {Duration} seconds", strategy.SoakDurationSeconds);
                await Task.Delay(TimeSpan.FromSeconds(strategy.SoakDurationSeconds), cancellationToken);

                // Health check
                var isHealthy = await healthCheckFunc(cancellationToken);

                stages.Add(new CanaryStage
                {
                    GreenTrafficPercentage = percentage,
                    IsHealthy = isHealthy,
                    Timestamp = DateTime.UtcNow
                });

                if (!isHealthy)
                {
                    _logger.LogWarning("Health check failed at {Percentage}% green traffic, rolling back", percentage);

                    // Rollback to 100% blue
                    await SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 0, cancellationToken);

                    return new CanaryDeploymentResult
                    {
                        Success = false,
                        RolledBack = true,
                        Stages = stages,
                        Message = $"Deployment failed health check at {percentage}% and was rolled back"
                    };
                }

                _logger.LogInformation("Stage {Percentage}% completed successfully", percentage);
            }

            _logger.LogInformation("Canary deployment completed successfully for {Service}", serviceName);

            return new CanaryDeploymentResult
            {
                Success = true,
                RolledBack = false,
                Stages = stages,
                Message = "Canary deployment completed successfully, 100% traffic on green"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Canary deployment failed for {Service}", serviceName);

            // Attempt rollback
            try
            {
                await SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 0, cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback failed");
            }

            return new CanaryDeploymentResult
            {
                Success = false,
                RolledBack = true,
                Stages = stages,
                Message = $"Deployment failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Performs instant cutover from blue to green (no gradual migration).
    /// </summary>
    public async Task<TrafficSwitchResult> PerformInstantCutoverAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing instant cutover for {Service}", serviceName);
        return await SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 100, cancellationToken);
    }

    /// <summary>
    /// Rolls back from green to blue (100% traffic to blue).
    /// </summary>
    public async Task<TrafficSwitchResult> RollbackToBlueAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Rolling back to blue for {Service}", serviceName);
        return await SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 0, cancellationToken);
    }
}

/// <summary>
/// Canary deployment strategy configuration.
/// </summary>
public sealed class CanaryStrategy
{
    /// <summary>
    /// Traffic percentage steps (e.g., [10, 25, 50, 100])
    /// </summary>
    public List<int> TrafficSteps { get; set; } = new() { 10, 25, 50, 100 };

    /// <summary>
    /// How long to wait at each stage before proceeding (seconds)
    /// </summary>
    public int SoakDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Automatically rollback on health check failure
    /// </summary>
    public bool AutoRollback { get; set; } = true;
}

/// <summary>
/// Result of traffic switch operation.
/// </summary>
public sealed class TrafficSwitchResult
{
    public bool Success { get; set; }
    public int BlueTrafficPercentage { get; set; }
    public int GreenTrafficPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of canary deployment.
/// </summary>
public sealed class CanaryDeploymentResult
{
    public bool Success { get; set; }
    public bool RolledBack { get; set; }
    public List<CanaryStage> Stages { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents one stage of canary deployment.
/// </summary>
public sealed class CanaryStage
{
    public int GreenTrafficPercentage { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
}
