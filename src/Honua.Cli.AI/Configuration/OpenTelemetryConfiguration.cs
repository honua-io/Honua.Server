// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Configuration;

/// <summary>
/// Configuration for OpenTelemetry tracing, metrics, and logs for Azure AI Foundry integration.
/// Provides observability for Semantic Kernel multi-agent orchestration.
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Adds comprehensive OpenTelemetry telemetry for Honua AI agents.
    /// Exports to Azure Monitor (Application Insights) and optionally OTLP.
    /// </summary>
    public static IServiceCollection AddHonuaAITelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        var enableSensitiveDiagnostics = configuration.GetValue<bool>("ApplicationInsights:EnableSensitiveDiagnostics");
        var samplingPercentage = configuration.GetValue<double>("ApplicationInsights:SamplingPercentage", 100.0);

        // Create resource attributes for telemetry identification
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: "Honua.AI",
                serviceVersion: typeof(OpenTelemetryConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                serviceInstanceId: Environment.MachineName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                ["honua.component"] = "multi-agent-orchestration"
            });

        // Register custom ActivitySource for Honua agents
        services.AddSingleton(new ActivitySource("Honua.AI.Agents", "1.0.0"));

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddDetector(new ResourceDetector()))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    // Semantic Kernel traces (automatic instrumentation)
                    .AddSource("Microsoft.SemanticKernel*")
                    .AddSource("Microsoft.SemanticKernel.Agents*")
                    .AddSource("Microsoft.SemanticKernel.Process*")
                    // Honua custom traces
                    .AddSource("Honua.AI*")
                    // HTTP client instrumentation (Azure OpenAI calls)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        // Filter sensitive headers
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.method", request.Method.ToString());
                            activity.SetTag("http.url", request.RequestUri?.GetLeftPart(UriPartial.Path));
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.status_code", (int)response.StatusCode);
                        };
                    });

                // Export to Azure Monitor
                if (!connectionString.IsNullOrEmpty())
                {
                    tracing.AddAzureMonitorTraceExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                        // Enable diagnostics for SK traces (includes prompts/completions)
                        // ⚠️ WARNING: Only enable in dev/staging! Contains sensitive data
                        if (enableSensitiveDiagnostics)
                        {
                            options.Diagnostics.IsDistributedTracingEnabled = true;
                            options.Diagnostics.IsLoggingEnabled = true;
                        }
                    });
                }

                // Optionally export to OTLP (for local Jaeger/Tempo)
                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }

                // Sampling configuration
                if (samplingPercentage < 100.0)
                {
                    tracing.SetSampler(new TraceIdRatioBasedSampler(samplingPercentage / 100.0));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    // Semantic Kernel metrics
                    .AddMeter("Microsoft.SemanticKernel*")
                    // Honua custom metrics
                    .AddMeter("Honua.AI*")
                    // Runtime metrics (CPU, memory, GC)
                    .AddRuntimeInstrumentation()
                    // HTTP client metrics
                    .AddHttpClientInstrumentation();

                // Export to Azure Monitor
                if (!connectionString.IsNullOrEmpty())
                {
                    metrics.AddAzureMonitorMetricExporter(options =>
                    {
                        options.ConnectionString = connectionString;
                    });
                }

                // Optionally export to OTLP
                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            });

        return services;
    }

    /// <summary>
    /// Custom resource detector for additional environment information.
    /// </summary>
    private class ResourceDetector : IResourceDetector
    {
        public Resource Detect()
        {
            var attributes = new List<KeyValuePair<string, object>>
            {
                new("host.name", Environment.MachineName),
                new("os.type", Environment.OSVersion.Platform.ToString()),
                new("process.runtime.name", ".NET"),
                new("process.runtime.version", Environment.Version.ToString())
            };

            // Add cloud provider info if available
            var cloudProvider = Environment.GetEnvironmentVariable("CLOUD_PROVIDER");
            if (!cloudProvider.IsNullOrEmpty())
            {
                attributes.Add(new("cloud.provider", cloudProvider));
            }

            var region = Environment.GetEnvironmentVariable("REGION");
            if (!region.IsNullOrEmpty())
            {
                attributes.Add(new("cloud.region", region));
            }

            return new Resource(attributes);
        }
    }
}

/// <summary>
/// Custom ActivitySource for Honua agent operations.
/// Provides detailed tracing for multi-agent orchestration, guard validation, and agent decisions.
/// </summary>
public sealed class AgentActivitySource
{
    public readonly ActivitySource activitySource;

    public AgentActivitySource()
    {
        activitySource = new ActivitySource("Honua.AI.Agents", "1.0.0");
    }

    /// <summary>
    /// Traces a complete agent orchestration operation.
    /// </summary>
    public Activity? StartOrchestration(string userRequest, int agentCount)
    {
        var activity = activitySource.StartActivity("Agent.Orchestration", ActivityKind.Internal);
        activity?.SetTag("agent.orchestration.type", "magentic");
        activity?.SetTag("agent.count", agentCount);
        activity?.SetTag("request.length", userRequest.Length);
        activity?.SetTag("request.preview", userRequest.Substring(0, Math.Min(100, userRequest.Length)));
        return activity;
    }

    /// <summary>
    /// Traces an individual agent invocation.
    /// </summary>
    public Activity? StartAgentInvocation(string agentName, string intent)
    {
        var activity = activitySource.StartActivity($"Agent.{agentName}", ActivityKind.Internal);
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.intent", intent);
        return activity;
    }

    /// <summary>
    /// Traces lifecycle of a long-running agent process/workflow.
    /// </summary>
    public Activity? StartProcess(string processId, string workflowType)
    {
        var activity = activitySource.StartActivity("Agent.Process", ActivityKind.Internal);
        activity?.SetTag("agent.process.id", processId);
        activity?.SetTag("agent.process.workflow_type", workflowType);
        return activity;
    }

    /// <summary>
    /// Traces guard validation (input/output guards).
    /// </summary>
    public Activity? StartGuardValidation(string guardType, string content)
    {
        var activity = activitySource.StartActivity($"Guard.{guardType}", ActivityKind.Internal);
        activity?.SetTag("guard.type", guardType);
        activity?.SetTag("content.length", content.Length);
        return activity;
    }

    /// <summary>
    /// Records guard validation result.
    /// </summary>
    public void RecordGuardResult(Activity? activity, bool blocked, string? reason = null, double? confidence = null)
    {
        if (activity == null) return;

        activity.SetTag("guard.blocked", blocked);
        if (reason != null)
        {
            activity.SetTag("guard.reason", reason);
        }
        if (confidence.HasValue)
        {
            activity.SetTag("guard.confidence", confidence.Value);
        }
    }

    /// <summary>
    /// Records agent response metadata.
    /// </summary>
    public void RecordAgentResponse(Activity? activity, int responseLength, int tokenCount = 0)
    {
        if (activity == null) return;

        activity.SetTag("response.length", responseLength);
        if (tokenCount > 0)
        {
            activity.SetTag("response.tokens", tokenCount);
        }
    }

    /// <summary>
    /// Records orchestration result.
    /// </summary>
    public void RecordOrchestrationResult(
        Activity? activity,
        bool success,
        int agentsInvoked,
        int totalSteps,
        TimeSpan duration)
    {
        if (activity == null) return;

        activity.SetTag("orchestration.success", success);
        activity.SetTag("orchestration.agents_invoked", agentsInvoked);
        activity.SetTag("orchestration.total_steps", totalSteps);
        activity.SetTag("orchestration.duration_ms", duration.TotalMilliseconds);
    }
}
