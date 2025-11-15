// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Honua.Server.Gateway.Clients;

/// <summary>
/// Strongly-typed HTTP client for the YARP traffic management API.
/// Includes automatic retry logic with Polly for resilience.
/// </summary>
public class TrafficManagementClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TrafficManagementClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public TrafficManagementClient(
        string baseUrl,
        string apiToken,
        ILogger<TrafficManagementClient>? logger = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Honua-TrafficManagementClient/1.0");

        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TrafficManagementClient>.Instance;

        // Configure retry policy with exponential backoff
        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Traffic management API call failed. Retry {RetryCount} after {Delay}s. Error: {Error}",
                        retryCount,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });
    }

    /// <summary>
    /// Switch traffic between blue and green environments.
    /// </summary>
    public async Task<TrafficSwitchResult> SwitchTrafficAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        int greenPercentage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Switching traffic for {Service}: {Green}% green, {Blue}% blue",
            serviceName,
            greenPercentage,
            100 - greenPercentage);

        var request = new TrafficSwitchRequest
        {
            ServiceName = serviceName,
            BlueEndpoint = blueEndpoint,
            GreenEndpoint = greenEndpoint,
            GreenPercentage = greenPercentage
        };

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsJsonAsync("/admin/traffic/switch", request, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrafficSwitchResult>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize traffic switch response");
        }

        _logger.LogInformation(
            "Traffic switched successfully for {Service}. Blue: {Blue}%, Green: {Green}%",
            serviceName,
            result.BlueTrafficPercentage,
            result.GreenTrafficPercentage);

        return result;
    }

    /// <summary>
    /// Perform automated canary deployment with health checks and automatic rollback.
    /// </summary>
    public async Task<CanaryDeploymentResult> PerformCanaryDeploymentAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CanaryStrategy strategy,
        string? healthCheckUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting canary deployment for {Service} with steps: {Steps}",
            serviceName,
            string.Join(", ", strategy.TrafficSteps));

        var request = new CanaryDeploymentRequest
        {
            ServiceName = serviceName,
            BlueEndpoint = blueEndpoint,
            GreenEndpoint = greenEndpoint,
            Strategy = strategy,
            HealthCheckUrl = healthCheckUrl ?? $"{greenEndpoint}/health"
        };

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsJsonAsync("/admin/traffic/canary", request, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CanaryDeploymentResult>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize canary deployment response");
        }

        if (result.Success)
        {
            _logger.LogInformation(
                "Canary deployment completed successfully for {Service}. {StageCount} stages completed.",
                serviceName,
                result.Stages.Count);
        }
        else
        {
            _logger.LogWarning(
                "Canary deployment failed for {Service}. Rolled back: {RolledBack}. Message: {Message}",
                serviceName,
                result.RolledBack,
                result.Message);
        }

        return result;
    }

    /// <summary>
    /// Immediately rollback to 100% blue environment.
    /// </summary>
    public async Task<TrafficSwitchResult> RollbackToBlueAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rolling back to blue for {Service}", serviceName);

        var request = new TrafficSwitchRequest
        {
            ServiceName = serviceName,
            BlueEndpoint = blueEndpoint,
            GreenEndpoint = greenEndpoint,
            GreenPercentage = 0
        };

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsJsonAsync("/admin/traffic/rollback", request, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrafficSwitchResult>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize rollback response");
        }

        _logger.LogInformation("Rollback completed for {Service}", serviceName);

        return result;
    }

    /// <summary>
    /// Immediately switch 100% traffic to green (instant cutover).
    /// </summary>
    public async Task<TrafficSwitchResult> PerformInstantCutoverAsync(
        string serviceName,
        string blueEndpoint,
        string greenEndpoint,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing instant cutover to green for {Service}", serviceName);

        var request = new TrafficSwitchRequest
        {
            ServiceName = serviceName,
            BlueEndpoint = blueEndpoint,
            GreenEndpoint = greenEndpoint,
            GreenPercentage = 100
        };

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.PostAsJsonAsync("/admin/traffic/instant-cutover", request, cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrafficSwitchResult>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize instant cutover response");
        }

        _logger.LogInformation("Instant cutover completed for {Service}", serviceName);

        return result;
    }

    /// <summary>
    /// Get current traffic distribution for a service.
    /// </summary>
    public async Task<TrafficStatusResult> GetTrafficStatusAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting traffic status for {Service}", serviceName);

        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.GetAsync($"/admin/traffic/status?serviceName={Uri.EscapeDataString(serviceName)}", cancellationToken));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrafficStatusResult>(cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize traffic status response");
        }

        return result;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Request for traffic switch operation.
/// </summary>
public class TrafficSwitchRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string BlueEndpoint { get; set; } = string.Empty;
    public string GreenEndpoint { get; set; } = string.Empty;
    public int GreenPercentage { get; set; }
}

/// <summary>
/// Request for canary deployment operation.
/// </summary>
public class CanaryDeploymentRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string BlueEndpoint { get; set; } = string.Empty;
    public string GreenEndpoint { get; set; } = string.Empty;
    public CanaryStrategy Strategy { get; set; } = new();
    public string HealthCheckUrl { get; set; } = string.Empty;
}

/// <summary>
/// Canary deployment strategy configuration.
/// </summary>
public class CanaryStrategy
{
    public List<int> TrafficSteps { get; set; } = new() { 10, 25, 50, 100 };
    public int SoakDurationSeconds { get; set; } = 60;
    public bool AutoRollback { get; set; } = true;
}

/// <summary>
/// Result of traffic switch operation.
/// </summary>
public class TrafficSwitchResult
{
    public bool Success { get; set; }
    public int BlueTrafficPercentage { get; set; }
    public int GreenTrafficPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of canary deployment operation.
/// </summary>
public class CanaryDeploymentResult
{
    public bool Success { get; set; }
    public bool RolledBack { get; set; }
    public List<CanaryStage> Stages { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents one stage of canary deployment.
/// </summary>
public class CanaryStage
{
    public int GreenTrafficPercentage { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Current traffic status for a service.
/// </summary>
public class TrafficStatusResult
{
    public string ServiceName { get; set; } = string.Empty;
    public int BlueTrafficPercentage { get; set; }
    public int GreenTrafficPercentage { get; set; }
    public Dictionary<string, DestinationStatus> Destinations { get; set; } = new();
}

/// <summary>
/// Status of a destination endpoint.
/// </summary>
public class DestinationStatus
{
    public string Address { get; set; } = string.Empty;
    public int Weight { get; set; }
    public bool Healthy { get; set; }
    public DateTime? LastHealthCheck { get; set; }
}
