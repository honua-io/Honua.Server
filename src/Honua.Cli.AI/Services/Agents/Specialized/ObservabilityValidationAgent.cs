// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Validates deployed Honua infrastructure through OpenTelemetry metrics and observability signals.
/// Integrates with Honua's existing IApiMetrics for validation and GitOps reconciliation.
///
/// Validation Strategy:
/// 1. Query Honua's OpenTelemetry metrics endpoint (/metrics)
/// 2. Analyze error rates, response times, feature counts per service/layer
/// 3. Check for anomalies using LLM-powered analysis
/// 4. Recommend rollback or metadata fixes if issues detected
/// </summary>
public sealed class ObservabilityValidationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ObservabilityValidationAgent> _logger;
    private readonly HttpClient _httpClient;

    public ObservabilityValidationAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<ObservabilityValidationAgent> logger,
        IHttpClientFactory httpClientFactory)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = (httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))).CreateClient("ObservabilityValidation");
    }

    /// <summary>
    /// Validates deployment health through observability signals.
    /// Used in validation loop and GitOps reconciliation.
    /// </summary>
    public async Task<ObservabilityValidationResult> ValidateDeploymentHealthAsync(
        DeploymentHealthCheckRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating deployment health for {Deployment}", request.DeploymentName);

        var checks = new List<HealthCheck>();

        // 1. Endpoint availability checks
        if (request.Endpoints?.Any() == true)
        {
            var endpointChecks = await ValidateEndpointsAsync(request.Endpoints, cancellationToken);
            checks.AddRange(endpointChecks);
        }

        // 2. Metrics validation (if metrics endpoint provided)
        if (request.MetricsEndpoint.HasValue())
        {
            var metricsCheck = await ValidateMetricsAsync(
                request.MetricsEndpoint,
                request.ExpectedMetrics,
                cancellationToken);
            checks.Add(metricsCheck);
        }

        // 3. Log analysis (if log endpoint provided)
        if (request.LogsEndpoint.HasValue())
        {
            var logCheck = await AnalyzeLogsAsync(
                request.LogsEndpoint,
                request.LogAnalysisWindow,
                cancellationToken);
            checks.Add(logCheck);
        }

        // 4. Resource health (CPU, memory, disk)
        if (request.ResourceMetricsEndpoint.HasValue())
        {
            var resourceCheck = await ValidateResourceHealthAsync(
                request.ResourceMetricsEndpoint,
                request.ResourceThresholds,
                cancellationToken);
            checks.Add(resourceCheck);
        }

        // 5. Database connectivity (if applicable)
        if (request.DatabaseHealthEndpoint.HasValue())
        {
            var dbCheck = await ValidateDatabaseHealthAsync(
                request.DatabaseHealthEndpoint,
                cancellationToken);
            checks.Add(dbCheck);
        }

        // 6. LLM-powered anomaly detection
        if (request.EnableAnomalyDetection && checks.Any())
        {
            var anomalyAnalysis = await DetectAnomaliesAsync(checks, request, context, cancellationToken);
            if (anomalyAnalysis != null)
            {
                checks.Add(anomalyAnalysis);
            }
        }

        // Determine overall health
        var criticalFailures = checks.Count(c => c.Status == HealthStatus.Critical);
        var warnings = checks.Count(c => c.Status == HealthStatus.Warning);
        var healthy = checks.Count(c => c.Status == HealthStatus.Healthy);

        var overallStatus = criticalFailures > 0 ? HealthStatus.Critical :
                           warnings > 0 ? HealthStatus.Warning :
                           HealthStatus.Healthy;

        var shouldRollback = criticalFailures > 0 ||
                            (warnings >= 3 && request.RollbackOnMultipleWarnings);

        var result = new ObservabilityValidationResult
        {
            DeploymentName = request.DeploymentName,
            OverallStatus = overallStatus,
            Checks = checks,
            CriticalFailures = criticalFailures,
            Warnings = warnings,
            HealthyChecks = healthy,
            ShouldRollback = shouldRollback,
            Timestamp = DateTime.UtcNow,
            Recommendation = GenerateRecommendation(checks, overallStatus, shouldRollback)
        };

        _logger.LogInformation(
            "Deployment health validation complete: {Status} ({Critical} critical, {Warnings} warnings, {Healthy} healthy)",
            overallStatus, criticalFailures, warnings, healthy);

        return result;
    }

    /// <summary>
    /// Validates endpoint availability and response times.
    /// </summary>
    private async Task<List<HealthCheck>> ValidateEndpointsAsync(
        List<EndpointHealthCheck> endpoints,
        CancellationToken cancellationToken)
    {
        var checks = new List<HealthCheck>();

        foreach (var endpoint in endpoints)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(endpoint.Url, cancellationToken);
                sw.Stop();

                // Determine status based on response and timing
                var status = response.IsSuccessStatusCode
                    ? (endpoint.MaxResponseTimeMs > 0 && sw.ElapsedMilliseconds > endpoint.MaxResponseTimeMs
                        ? HealthStatus.Warning
                        : HealthStatus.Healthy)
                    : HealthStatus.Critical;

                var message = response.IsSuccessStatusCode
                    ? (endpoint.MaxResponseTimeMs > 0 && sw.ElapsedMilliseconds > endpoint.MaxResponseTimeMs
                        ? $"Endpoint responding ({sw.ElapsedMilliseconds}ms) - slow response (expected <{endpoint.MaxResponseTimeMs}ms)"
                        : $"Endpoint responding ({sw.ElapsedMilliseconds}ms)")
                    : $"Endpoint returned {response.StatusCode}";

                var check = new HealthCheck
                {
                    Category = "Endpoint",
                    Name = $"Endpoint: {endpoint.Name}",
                    Status = status,
                    Message = message,
                    Details = new Dictionary<string, string>
                    {
                        ["url"] = endpoint.Url,
                        ["status_code"] = ((int)response.StatusCode).ToString(),
                        ["response_time_ms"] = sw.ElapsedMilliseconds.ToString()
                    }
                };

                checks.Add(check);
            }
            catch (Exception ex)
            {
                checks.Add(new HealthCheck
                {
                    Category = "Endpoint",
                    Name = $"Endpoint: {endpoint.Name}",
                    Status = HealthStatus.Critical,
                    Message = $"Endpoint unreachable: {ex.Message}",
                    Details = new Dictionary<string, string>
                    {
                        ["url"] = endpoint.Url,
                        ["error"] = ex.Message
                    }
                });
            }
        }

        return checks;
    }

    /// <summary>
    /// Validates metrics against expected values.
    /// </summary>
    private async Task<HealthCheck> ValidateMetricsAsync(
        string metricsEndpoint,
        Dictionary<string, MetricExpectation>? expectedMetrics,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(metricsEndpoint, cancellationToken);
            var metrics = ParsePrometheusMetrics(response);

            if (expectedMetrics == null || expectedMetrics.Count == 0)
            {
                return new HealthCheck
                {
                    Category = "Metrics",
                    Name = "Metrics Available",
                    Status = HealthStatus.Healthy,
                    Message = $"Metrics endpoint responding with {metrics.Count} metrics",
                    Details = new Dictionary<string, string>
                    {
                        ["endpoint"] = metricsEndpoint,
                        ["metric_count"] = metrics.Count.ToString()
                    }
                };
            }

            var violations = new List<string>();
            foreach (var expectation in expectedMetrics)
            {
                if (metrics.TryGetValue(expectation.Key, out var value))
                {
                    if (!IsMetricWithinExpectation(value, expectation.Value))
                    {
                        violations.Add($"{expectation.Key}: {value} (expected {expectation.Value.Min}-{expectation.Value.Max})");
                    }
                }
                else
                {
                    violations.Add($"{expectation.Key}: missing");
                }
            }

            return new HealthCheck
            {
                Category = "Metrics",
                Name = "Metrics Validation",
                Status = violations.Any() ? HealthStatus.Warning : HealthStatus.Healthy,
                Message = violations.Any()
                    ? $"{violations.Count} metric violations found"
                    : "All metrics within expected ranges",
                Details = new Dictionary<string, string>
                {
                    ["violations"] = string.Join(", ", violations)
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Category = "Metrics",
                Name = "Metrics Validation",
                Status = HealthStatus.Critical,
                Message = $"Failed to validate metrics: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Analyzes recent logs for errors and anomalies.
    /// </summary>
    private async Task<HealthCheck> AnalyzeLogsAsync(
        string logsEndpoint,
        TimeSpan? window,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(logsEndpoint, cancellationToken);
            var logs = JsonDocument.Parse(response);

            var errorCount = 0;
            var warningCount = 0;
            var criticalErrors = new List<string>();

            // Parse log entries (assuming JSON log format)
            if (logs.RootElement.TryGetProperty("entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.TryGetProperty("level", out var level))
                    {
                        var levelStr = level.GetString()?.ToLowerInvariant();
                        if (levelStr == "error" || levelStr == "fatal")
                        {
                            errorCount++;
                            if (entry.TryGetProperty("message", out var msg))
                            {
                                criticalErrors.Add(msg.GetString() ?? "Unknown error");
                            }
                        }
                        else if (levelStr == "warning" || levelStr == "warn")
                        {
                            warningCount++;
                        }
                    }
                }
            }

            var status = errorCount > 10 ? HealthStatus.Critical :
                        errorCount > 0 || warningCount > 20 ? HealthStatus.Warning :
                        HealthStatus.Healthy;

            return new HealthCheck
            {
                Category = "Logs",
                Name = "Log Analysis",
                Status = status,
                Message = $"Found {errorCount} errors, {warningCount} warnings in recent logs",
                Details = new Dictionary<string, string>
                {
                    ["error_count"] = errorCount.ToString(),
                    ["warning_count"] = warningCount.ToString(),
                    ["critical_errors"] = string.Join("; ", criticalErrors.Take(3))
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Category = "Logs",
                Name = "Log Analysis",
                Status = HealthStatus.Warning,
                Message = $"Failed to analyze logs: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Validates resource utilization (CPU, memory, disk).
    /// </summary>
    private async Task<HealthCheck> ValidateResourceHealthAsync(
        string resourceMetricsEndpoint,
        ResourceThresholds? thresholds,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(resourceMetricsEndpoint, cancellationToken);
            var metrics = JsonDocument.Parse(response);

            var cpuUsage = GetMetricValue(metrics, "cpu_usage_percent");
            var memoryUsage = GetMetricValue(metrics, "memory_usage_percent");
            var diskUsage = GetMetricValue(metrics, "disk_usage_percent");

            thresholds ??= new ResourceThresholds();

            var issues = new List<string>();
            var status = HealthStatus.Healthy;

            if (cpuUsage > thresholds.CpuCriticalPercent)
            {
                issues.Add($"CPU usage critical: {cpuUsage}%");
                status = HealthStatus.Critical;
            }
            else if (cpuUsage > thresholds.CpuWarningPercent)
            {
                issues.Add($"CPU usage high: {cpuUsage}%");
                status = HealthStatus.Warning;
            }

            if (memoryUsage > thresholds.MemoryCriticalPercent)
            {
                issues.Add($"Memory usage critical: {memoryUsage}%");
                status = HealthStatus.Critical;
            }
            else if (memoryUsage > thresholds.MemoryWarningPercent)
            {
                issues.Add($"Memory usage high: {memoryUsage}%");
                if (status == HealthStatus.Healthy) status = HealthStatus.Warning;
            }

            if (diskUsage > thresholds.DiskCriticalPercent)
            {
                issues.Add($"Disk usage critical: {diskUsage}%");
                status = HealthStatus.Critical;
            }
            else if (diskUsage > thresholds.DiskWarningPercent)
            {
                issues.Add($"Disk usage high: {diskUsage}%");
                if (status == HealthStatus.Healthy) status = HealthStatus.Warning;
            }

            return new HealthCheck
            {
                Category = "Resources",
                Name = "Resource Utilization",
                Status = status,
                Message = issues.Any() ? string.Join(", ", issues) : "Resource utilization normal",
                Details = new Dictionary<string, string>
                {
                    ["cpu_percent"] = cpuUsage.ToString("F1"),
                    ["memory_percent"] = memoryUsage.ToString("F1"),
                    ["disk_percent"] = diskUsage.ToString("F1")
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Category = "Resources",
                Name = "Resource Utilization",
                Status = HealthStatus.Warning,
                Message = $"Failed to check resource utilization: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Validates database connectivity and health.
    /// </summary>
    private async Task<HealthCheck> ValidateDatabaseHealthAsync(
        string dbHealthEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(dbHealthEndpoint, cancellationToken);
            var health = JsonDocument.Parse(response);

            var status = HealthStatus.Healthy;
            var message = "Database healthy";

            if (health.RootElement.TryGetProperty("status", out var statusProp))
            {
                var statusStr = statusProp.GetString()?.ToLowerInvariant();
                if (statusStr == "unhealthy" || statusStr == "down")
                {
                    status = HealthStatus.Critical;
                    message = "Database unhealthy";
                }
            }

            var connectionCount = GetMetricValue(health, "connections");
            var slowQueries = GetMetricValue(health, "slow_queries");

            var details = new Dictionary<string, string>
            {
                ["connections"] = connectionCount.ToString("F0"),
                ["slow_queries"] = slowQueries.ToString("F0")
            };

            if (slowQueries > 10)
            {
                status = HealthStatus.Warning;
                message += $", {slowQueries} slow queries detected";
            }

            return new HealthCheck
            {
                Category = "Database",
                Name = "Database Health",
                Status = status,
                Message = message,
                Details = details
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Category = "Database",
                Name = "Database Health",
                Status = HealthStatus.Critical,
                Message = $"Database unreachable: {ex.Message}",
                Details = new Dictionary<string, string>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    /// <summary>
    /// Uses LLM to detect anomalies in health check data.
    /// </summary>
    private async Task<HealthCheck?> DetectAnomaliesAsync(
        List<HealthCheck> checks,
        DeploymentHealthCheckRequest request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = @"You are an expert in observability and deployment health monitoring.

Analyze the health check data and identify potential anomalies or concerning patterns that might not be caught by individual checks.

Look for:
1. **Correlation patterns**: Multiple related warnings that together indicate a problem
2. **Resource exhaustion trends**: Early signs of resource exhaustion
3. **Performance degradation**: Slow response times combined with resource pressure
4. **Cascading failures**: One issue likely to cause others
5. **Configuration issues**: Mismatches suggesting misconfiguration

Return JSON:
{
  ""anomalyDetected"": true/false,
  ""severity"": ""critical"" | ""warning"" | ""info"",
  ""description"": ""string (what's wrong)"",
  ""likelyRootCause"": ""string (root cause hypothesis)"",
  ""recommendedAction"": ""string (what to do)""
}

If no significant anomalies detected, return: {""anomalyDetected"": false}";

            var healthSummary = JsonSerializer.Serialize(checks.Select(c => new
            {
                c.Category,
                c.Name,
                Status = c.Status.ToString(),
                c.Message,
                c.Details
            }), CliJsonOptions.Indented);

            var userPrompt = $@"Analyze this deployment health data for anomalies:

**Deployment**: {request.DeploymentName}
**Environment**: {context.WorkspacePath}

**Health Checks**:
```json
{healthSummary}
```

Detect any anomalies or concerning patterns.";

            var llmRequest = new LlmRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                Temperature = 0.3,
                MaxTokens = 500
            };

            var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

            if (!response.Success) return null;

            var json = ExtractJson(response.Content);
            var anomaly = JsonSerializer.Deserialize<AnomalyDetection>(
                json,
                CliJsonOptions.DevTooling);

            if (anomaly?.AnomalyDetected != true) return null;

            var status = anomaly.Severity?.ToLowerInvariant() switch
            {
                "critical" => HealthStatus.Critical,
                "warning" => HealthStatus.Warning,
                _ => HealthStatus.Healthy
            };

            return new HealthCheck
            {
                Category = "Anomaly Detection",
                Name = "AI-Powered Anomaly Analysis",
                Status = status,
                Message = anomaly.Description ?? "Anomaly detected",
                Details = new Dictionary<string, string>
                {
                    ["root_cause"] = anomaly.LikelyRootCause ?? "Unknown",
                    ["recommended_action"] = anomaly.RecommendedAction ?? "Manual investigation needed"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anomaly detection failed");
            return null;
        }
    }

    private string GenerateRecommendation(
        List<HealthCheck> checks,
        HealthStatus overallStatus,
        bool shouldRollback)
    {
        if (shouldRollback)
        {
            var criticalIssues = checks.Where(c => c.Status == HealthStatus.Critical)
                .Select(c => c.Message)
                .ToList();

            return $"ROLLBACK RECOMMENDED: {criticalIssues.Count} critical issues detected. " +
                   $"Issues: {string.Join("; ", criticalIssues.Take(3))}";
        }

        if (overallStatus == HealthStatus.Warning)
        {
            var warnings = checks.Where(c => c.Status == HealthStatus.Warning)
                .Select(c => c.Message)
                .ToList();

            return $"Deployment stable with warnings. Monitor: {string.Join("; ", warnings.Take(3))}";
        }

        return "Deployment healthy. All checks passed.";
    }

    // Helper methods

    private Dictionary<string, double> ParsePrometheusMetrics(string prometheusText)
    {
        var metrics = new Dictionary<string, double>();
        var lines = prometheusText.Split('\n');

        foreach (var line in lines)
        {
            if (line.IsNullOrWhiteSpace() || line.StartsWith("#")) continue;

            var parts = line.Split(' ');
            if (parts.Length >= 2 && double.TryParse(parts[^1], out var value))
            {
                metrics[parts[0]] = value;
            }
        }

        return metrics;
    }

    private bool IsMetricWithinExpectation(double value, MetricExpectation expectation)
    {
        if (expectation.Min.HasValue && value < expectation.Min.Value) return false;
        if (expectation.Max.HasValue && value > expectation.Max.Value) return false;
        return true;
    }

    private double GetMetricValue(JsonDocument doc, string metricName)
    {
        if (doc.RootElement.TryGetProperty(metricName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDouble();
            }
        }
        return 0;
    }

    private string ExtractJson(string text)
    {
        if (text.Contains("```json"))
        {
            var start = text.IndexOf("```json") + 7;
            var end = text.IndexOf("```", start);
            if (end > start) return text.Substring(start, end - start).Trim();
        }
        else if (text.Contains("```"))
        {
            var start = text.IndexOf("```") + 3;
            var end = text.IndexOf("```", start);
            if (end > start) return text.Substring(start, end - start).Trim();
        }
        return text.Trim();
    }
}

// Request/Response models

public sealed class DeploymentHealthCheckRequest
{
    public string DeploymentName { get; init; } = string.Empty;
    public List<EndpointHealthCheck>? Endpoints { get; init; }
    public string? MetricsEndpoint { get; init; }
    public Dictionary<string, MetricExpectation>? ExpectedMetrics { get; init; }
    public string? LogsEndpoint { get; init; }
    public TimeSpan? LogAnalysisWindow { get; init; }
    public string? ResourceMetricsEndpoint { get; init; }
    public ResourceThresholds? ResourceThresholds { get; init; }
    public string? DatabaseHealthEndpoint { get; init; }
    public bool EnableAnomalyDetection { get; init; } = true;
    public bool RollbackOnMultipleWarnings { get; init; } = false;
}

public sealed class EndpointHealthCheck
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public int MaxResponseTimeMs { get; init; } = 5000;
}

public sealed class MetricExpectation
{
    public double? Min { get; init; }
    public double? Max { get; init; }
}

public sealed class ResourceThresholds
{
    public double CpuWarningPercent { get; init; } = 70;
    public double CpuCriticalPercent { get; init; } = 90;
    public double MemoryWarningPercent { get; init; } = 75;
    public double MemoryCriticalPercent { get; init; } = 90;
    public double DiskWarningPercent { get; init; } = 80;
    public double DiskCriticalPercent { get; init; } = 95;
}

public sealed class ObservabilityValidationResult
{
    public string DeploymentName { get; init; } = string.Empty;
    public HealthStatus OverallStatus { get; init; }
    public List<HealthCheck> Checks { get; init; } = new();
    public int CriticalFailures { get; init; }
    public int Warnings { get; init; }
    public int HealthyChecks { get; init; }
    public bool ShouldRollback { get; init; }
    public DateTime Timestamp { get; init; }
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class HealthCheck
{
    public string Category { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public HealthStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string> Details { get; init; } = new();
}

public enum HealthStatus
{
    Healthy,
    Warning,
    Critical
}

internal sealed class AnomalyDetection
{
    public bool AnomalyDetected { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public string? LikelyRootCause { get; init; }
    public string? RecommendedAction { get; init; }
}
