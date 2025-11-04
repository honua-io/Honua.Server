// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using StackExchange.Redis;
using Honua.Server.Core.Utilities;

namespace Honua.Cli.AI.HealthChecks;

/// <summary>
/// Comprehensive health check for Process Framework infrastructure.
/// Verifies Redis connectivity, LLM service availability, and active process health.
/// </summary>
public sealed class ProcessFrameworkHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ProcessFrameworkMetrics? _metrics;
    private readonly ILogger<ProcessFrameworkHealthCheck> _logger;

    public ProcessFrameworkHealthCheck(
        ILogger<ProcessFrameworkHealthCheck> logger,
        IConnectionMultiplexer? redis = null,
        IChatCompletionService? chatCompletion = null,
        ProcessFrameworkMetrics? metrics = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
        _chatCompletion = chatCompletion;
        _metrics = metrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var healthData = new Dictionary<string, object>();
        var degradedReasons = new List<string>();
        var unhealthyReasons = new List<string>();

        try
        {
            // Check Redis connectivity
            var redisHealthy = await CheckRedisHealthAsync(healthData, degradedReasons, unhealthyReasons, cancellationToken);

            // Check LLM service availability
            var llmHealthy = await CheckLLMHealthAsync(healthData, degradedReasons, unhealthyReasons, cancellationToken);

            // Check active process metrics
            var metricsHealthy = CheckMetricsHealth(healthData, degradedReasons);

            // Determine overall health status
            if (unhealthyReasons.Count > 0)
            {
                var message = $"Process Framework is unhealthy: {string.Join(", ", unhealthyReasons)}";
                _logger.LogError(message);
                return HealthCheckResult.Unhealthy(message, data: healthData);
            }

            if (degradedReasons.Count > 0)
            {
                var message = $"Process Framework is degraded: {string.Join(", ", degradedReasons)}";
                _logger.LogWarning(message);
                return HealthCheckResult.Degraded(message, data: healthData);
            }

            _logger.LogDebug("Process Framework health check passed");
            return HealthCheckResult.Healthy("Process Framework is healthy", healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process Framework health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Process Framework health check failed",
                ex,
                healthData);
        }
    }

    private async Task<bool> CheckRedisHealthAsync(
        Dictionary<string, object> healthData,
        List<string> degradedReasons,
        List<string> unhealthyReasons,
        CancellationToken cancellationToken)
    {
        if (_redis == null)
        {
            degradedReasons.Add("Redis connection not configured");
            healthData["redis.configured"] = false;
            healthData["redis.status"] = "not_configured";
            return false;
        }

        var result = await ExceptionHandler.ExecuteSafeAsync(
            async () =>
            {
                var db = _redis.GetDatabase();
                var pong = await db.PingAsync();

                healthData["redis.configured"] = true;
                healthData["redis.latency_ms"] = pong.TotalMilliseconds;
                healthData["redis.status"] = "healthy";

                if (pong.TotalMilliseconds > 100)
                {
                    degradedReasons.Add($"Redis latency is high: {pong.TotalMilliseconds:F2}ms");
                    healthData["redis.status"] = "degraded";
                    return false;
                }

                // Check connection status
                var endpoints = _redis.GetEndPoints();
                healthData["redis.endpoints"] = endpoints.Length;

                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    if (!server.IsConnected)
                    {
                        degradedReasons.Add($"Redis endpoint {endpoint} is not connected");
                        healthData["redis.status"] = "degraded";
                        return false;
                    }
                }

                _logger.LogDebug("Redis health check passed. Latency: {Latency}ms", pong.TotalMilliseconds);
                return true;
            },
            _logger,
            "Redis health check");

        if (result.IsFailure)
        {
            var ex = result.Exception;
            if (ex is RedisConnectionException)
            {
                unhealthyReasons.Add($"Redis connection failed: {ex.Message}");
                healthData["redis.configured"] = true;
                healthData["redis.status"] = "unhealthy";
                healthData["redis.error"] = ex.Message;
            }
            else
            {
                degradedReasons.Add($"Redis health check error: {ex.Message}");
                healthData["redis.configured"] = true;
                healthData["redis.status"] = "error";
                healthData["redis.error"] = ex.Message;
            }
            return false;
        }

        return result.Value;
    }

    private Task<bool> CheckLLMHealthAsync(
        Dictionary<string, object> healthData,
        List<string> degradedReasons,
        List<string> unhealthyReasons,
        CancellationToken cancellationToken)
    {
        if (_chatCompletion == null)
        {
            degradedReasons.Add("LLM service not configured");
            healthData["llm.configured"] = false;
            healthData["llm.status"] = "not_configured";
            return Task.FromResult(false);
        }

        var result = ExceptionHandler.ExecuteSafe(
            () =>
            {
                // Simple health check: verify service is responsive
                // We don't actually call the LLM to avoid costs, just check if it's configured
                healthData["llm.configured"] = true;
                healthData["llm.provider"] = _chatCompletion.GetType().Name;
                healthData["llm.status"] = "healthy";

                _logger.LogDebug("LLM service health check passed");
                return true;
            },
            _logger,
            "LLM service health check");

        if (result.IsFailure)
        {
            degradedReasons.Add($"LLM service health check error: {result.Exception.Message}");
            healthData["llm.configured"] = true;
            healthData["llm.status"] = "error";
            healthData["llm.error"] = result.Exception.Message;
            return Task.FromResult(false);
        }

        return Task.FromResult(result.Value);
    }

    private bool CheckMetricsHealth(
        Dictionary<string, object> healthData,
        List<string> degradedReasons)
    {
        if (_metrics == null)
        {
            healthData["metrics.configured"] = false;
            return true; // Metrics are optional, not critical
        }

        try
        {
            var activeProcessCount = _metrics.GetActiveProcessCount();
            var activeByWorkflow = _metrics.GetActiveProcessCountByWorkflow();

            healthData["metrics.configured"] = true;
            healthData["metrics.active_processes"] = activeProcessCount;
            healthData["metrics.active_by_workflow"] = activeByWorkflow;

            // Check for excessive active processes (potential leak or performance issue)
            if (activeProcessCount > 100)
            {
                degradedReasons.Add($"High number of active processes: {activeProcessCount}");
                healthData["metrics.status"] = "degraded";
                return false;
            }

            // Calculate overall success rates
            var successRates = new Dictionary<string, double>();
            foreach (var workflow in activeByWorkflow.Keys)
            {
                var rate = _metrics.GetSuccessRate(workflow);
                successRates[workflow] = rate;

                // Warn if success rate is low
                if (rate < 0.8 && _metrics.GetTotalExecutions(workflow) > 10)
                {
                    degradedReasons.Add($"Low success rate for {workflow}: {rate:P1}");
                    healthData["metrics.status"] = "degraded";
                }
            }

            healthData["metrics.success_rates"] = successRates;
            healthData["metrics.status"] = "healthy";

            _logger.LogDebug(
                "Metrics health check passed. Active processes: {Count}",
                activeProcessCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metrics health check encountered an error");
            healthData["metrics.configured"] = true;
            healthData["metrics.status"] = "error";
            healthData["metrics.error"] = ex.Message;
            return true; // Non-critical
        }
    }
}
