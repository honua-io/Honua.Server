// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using BenchmarkState = Honua.Cli.AI.Services.Processes.State.BenchmarkState;

namespace Honua.Cli.AI.Services.Processes.Steps.Benchmark;

/// <summary>
/// Sets up benchmark environment and test data.
/// </summary>
public class SetupBenchmarkStep : KernelProcessStep<BenchmarkState>, IProcessStepTimeout
{
    private readonly ILogger<SetupBenchmarkStep> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private BenchmarkState _state = new();

    /// <summary>
    /// Setup includes test data preparation, endpoint validation, and load generator configuration.
    /// Default timeout: 5 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);

    public SetupBenchmarkStep(ILogger<SetupBenchmarkStep> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<BenchmarkState> state)
    {
        _state = state.State ?? new BenchmarkState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("SetupBenchmark")]
    public async Task SetupBenchmarkAsync(
        KernelProcessStepContext context,
        BenchmarkRequest request)
    {
        _logger.LogInformation("Setting up benchmark: {BenchmarkName}", request.BenchmarkName);

        _state.BenchmarkId = Guid.NewGuid().ToString();
        _state.BenchmarkName = request.BenchmarkName;
        _state.TargetEndpoint = request.TargetEndpoint;
        _state.Concurrency = request.Concurrency;
        _state.Duration = request.Duration;
        _state.StartTime = DateTime.UtcNow;
        _state.Status = "SettingUp";

        try
        {
            // Prepare test data
            await PrepareTestData();

            // Validate endpoint
            await ValidateEndpoint();

            // Configure load generator
            await ConfigureLoadGenerator();

            _logger.LogInformation("Benchmark setup completed: {BenchmarkName}", _state.BenchmarkName);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SetupCompleted",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup benchmark: {BenchmarkName}", request.BenchmarkName);
            _state.Status = "SetupFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SetupFailed",
                Data = new { request.BenchmarkName, Error = ex.Message }
            });
        }
    }

    private async Task PrepareTestData()
    {
        _logger.LogInformation("Preparing test data");

        // Initialize test data structures based on benchmark type
        if (string.IsNullOrEmpty(_state.TargetEndpoint))
        {
            throw new InvalidOperationException("Target endpoint not specified");
        }

        // For load testing, we prepare a set of test requests
        // This could include different query parameters, spatial extents, etc.
        var testScenarios = new List<string>();

        if (_state.TargetEndpoint.Contains("/collections"))
        {
            testScenarios.Add("GET /collections - List all collections");
            testScenarios.Add("GET /collections/{id} - Get collection details");
            testScenarios.Add("GET /collections/{id}/items - Query collection items");
        }
        else if (_state.TargetEndpoint.Contains("/coverages"))
        {
            testScenarios.Add("GET /coverages - List coverages");
            testScenarios.Add("GET /coverages/{id} - Get coverage metadata");
        }
        else
        {
            testScenarios.Add("GET endpoint - Basic request");
        }

        _logger.LogInformation("Prepared {Count} test scenarios for benchmark", testScenarios.Count);

        // Warm up cache if needed
        if (!_state.CacheWarmed)
        {
            _logger.LogInformation("Warming up cache with initial requests");
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var response = await httpClient.GetAsync(_state.TargetEndpoint);
                if (response.IsSuccessStatusCode)
                {
                    _state.CacheWarmed = true;
                    _logger.LogInformation("Cache warmed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm cache, continuing anyway");
            }
        }

        await Task.CompletedTask;
    }

    private async Task ValidateEndpoint()
    {
        _logger.LogInformation("Validating target endpoint: {Endpoint}", _state.TargetEndpoint);

        if (string.IsNullOrEmpty(_state.TargetEndpoint))
        {
            throw new InvalidOperationException("Target endpoint not specified");
        }

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Try to determine if there's a health endpoint
        var baseUrl = GetBaseUrl(_state.TargetEndpoint);
        var healthUrl = $"{baseUrl}/health";

        try
        {
            // First, try health endpoint
            _logger.LogInformation("Checking health endpoint: {HealthUrl}", healthUrl);
            var healthResponse = await httpClient.GetAsync(healthUrl);

            if (healthResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Health check passed: {StatusCode}", healthResponse.StatusCode);
            }
            else
            {
                _logger.LogWarning("Health endpoint returned {StatusCode}, trying target endpoint directly",
                    healthResponse.StatusCode);

                // Fall back to target endpoint
                var targetResponse = await httpClient.GetAsync(_state.TargetEndpoint);
                targetResponse.EnsureSuccessStatusCode();
                _logger.LogInformation("Target endpoint validated: {StatusCode}", targetResponse.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to validate endpoint: {Endpoint}", _state.TargetEndpoint);
            throw new InvalidOperationException($"Endpoint validation failed for {_state.TargetEndpoint}: {ex.Message}", ex);
        }
    }

    private string GetBaseUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Authority}";
        }
        catch
        {
            // If URL parsing fails, return as-is
            return url;
        }
    }

    private async Task ConfigureLoadGenerator()
    {
        _logger.LogInformation("Configuring load generator with {Concurrency} concurrent users",
            _state.Concurrency);

        // Validate load generator configuration
        if (_state.Concurrency <= 0)
        {
            throw new InvalidOperationException("Concurrency must be greater than 0");
        }

        if (_state.Duration <= 0)
        {
            throw new InvalidOperationException("Duration must be greater than 0");
        }

        // Log configuration details
        _logger.LogInformation("Load generator configuration:");
        _logger.LogInformation("  - Target: {Endpoint}", _state.TargetEndpoint);
        _logger.LogInformation("  - Concurrency: {Concurrency} concurrent users", _state.Concurrency);
        _logger.LogInformation("  - Duration: {Duration} seconds", _state.Duration);
        _logger.LogInformation("  - Benchmark Type: {Type}", _state.BenchmarkType);

        // Perform a quick baseline test to ensure the endpoint responds
        _logger.LogInformation("Performing baseline test with single request");
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var startTime = DateTime.UtcNow;
        var response = await httpClient.GetAsync(_state.TargetEndpoint);
        var endTime = DateTime.UtcNow;
        var latency = (endTime - startTime).TotalMilliseconds;

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Baseline test completed: {StatusCode}, Latency: {Latency}ms",
            response.StatusCode, latency);

        // Store baseline latency
        _state.BaselineLatencyP95 = (decimal)latency;

        _logger.LogInformation("Load generator configured successfully");
    }
}

/// <summary>
/// Request object for benchmark.
/// </summary>
public record BenchmarkRequest(
    string BenchmarkName,
    string TargetEndpoint,
    int Concurrency,
    int Duration);
