// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Honua.Server.Observability.Tracing;

/// <summary>
/// Configuration options for OpenTelemetry distributed tracing.
/// </summary>
public class TracingConfiguration
{
    /// <summary>
    /// Enables or disables distributed tracing. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The trace exporter to use. Options: "none", "console", "otlp", "jaeger", "multiple".
    /// Default is "console" for development.
    /// </summary>
    public string Exporter { get; set; } = "console";

    /// <summary>
    /// The OTLP exporter endpoint. Required when Exporter is "otlp" or "multiple".
    /// Example: "http://localhost:4317" for gRPC or "http://localhost:4318" for HTTP.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// The OTLP protocol to use. Options: "grpc", "http/protobuf".
    /// Default is "grpc".
    /// </summary>
    public string OtlpProtocol { get; set; } = "grpc";

    /// <summary>
    /// The Jaeger exporter agent host. Required when Exporter is "jaeger" or "multiple".
    /// Default is "localhost".
    /// </summary>
    public string JaegerAgentHost { get; set; } = "localhost";

    /// <summary>
    /// The Jaeger exporter agent port. Default is 6831.
    /// </summary>
    public int JaegerAgentPort { get; set; } = 6831;

    /// <summary>
    /// The sampling strategy. Options: "always_on", "always_off", "trace_id_ratio", "parent_based".
    /// Default is "parent_based" (respects parent sampling decision).
    /// </summary>
    public string SamplingStrategy { get; set; } = "parent_based";

    /// <summary>
    /// The sampling ratio when using "trace_id_ratio". Value between 0.0 and 1.0.
    /// Example: 0.1 means sample 10% of traces. Default is 1.0 (100%).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Maximum attributes per span. Default is 128.
    /// </summary>
    public int MaxAttributesPerSpan { get; set; } = 128;

    /// <summary>
    /// Maximum events per span. Default is 128.
    /// </summary>
    public int MaxEventsPerSpan { get; set; } = 128;

    /// <summary>
    /// Maximum links per span. Default is 128.
    /// </summary>
    public int MaxLinksPerSpan { get; set; } = 128;

    /// <summary>
    /// Whether to record exception details in spans. Default is true.
    /// Set to false in production if exception messages contain sensitive data.
    /// </summary>
    public bool RecordExceptionDetails { get; set; } = true;

    /// <summary>
    /// List of endpoints to exclude from tracing (e.g., health checks).
    /// Default excludes: /health, /metrics, /ready, /live.
    /// </summary>
    public List<string> ExcludedEndpoints { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/ready",
        "/live",
    };

    /// <summary>
    /// Whether to enrich spans with HTTP request/response details. Default is true.
    /// </summary>
    public bool EnrichWithHttpDetails { get; set; } = true;

    /// <summary>
    /// Whether to enrich spans with database statement details. Default is true.
    /// Set to false if SQL queries contain sensitive data.
    /// </summary>
    public bool EnrichWithDbStatements { get; set; } = true;

    /// <summary>
    /// Whether to trace Redis commands. Default is false for security.
    /// </summary>
    public bool TraceRediCommands { get; set; } = false;

    /// <summary>
    /// Custom trace baggage to add to all spans.
    /// Baggage is propagated across service boundaries.
    /// </summary>
    public Dictionary<string, string> Baggage { get; set; } = new();
}

/// <summary>
/// Extension methods for configuring OpenTelemetry tracing with multiple exporters.
/// </summary>
public static class TracingConfigurationExtensions
{
    /// <summary>
    /// Configures OpenTelemetry tracing exporters based on configuration.
    /// Supports Console, OTLP, Jaeger, and multiple exporters simultaneously.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The tracer provider builder for chaining.</returns>
    public static TracerProviderBuilder ConfigureHonuaExporters(
        this TracerProviderBuilder builder,
        IConfiguration configuration)
    {
        var tracingConfig = configuration.GetSection("Observability:Tracing")
            .Get<TracingConfiguration>() ?? new TracingConfiguration();

        return builder.ConfigureHonuaExporters(tracingConfig);
    }

    /// <summary>
    /// Configures OpenTelemetry tracing exporters based on TracingConfiguration.
    /// Supports Console, OTLP, Jaeger, and multiple exporters simultaneously.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <param name="config">The tracing configuration.</param>
    /// <returns>The tracer provider builder for chaining.</returns>
    public static TracerProviderBuilder ConfigureHonuaExporters(
        this TracerProviderBuilder builder,
        TracingConfiguration config)
    {
        if (!config.Enabled)
        {
            return builder;
        }

        // Configure sampling
        builder.SetSampler(CreateSampler(config));

        // Configure span limits (resource already configured in main setup)

        // Configure exporters based on configuration
        var exporterType = config.Exporter.ToLowerInvariant();

        switch (exporterType)
        {
            case "console":
                builder.AddConsoleExporter(options =>
                {
                    options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                });
                break;

            case "otlp":
                builder.AddOtlpExporter(options =>
                {
                    if (!string.IsNullOrEmpty(config.OtlpEndpoint))
                    {
                        options.Endpoint = new Uri(config.OtlpEndpoint);
                    }
                    options.Protocol = config.OtlpProtocol.ToLowerInvariant() == "http/protobuf"
                        ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                        : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
                break;

            case "jaeger":
                // Note: Jaeger exporter is deprecated. Use OTLP exporter with Jaeger OTLP receiver instead.
                // See: https://www.jaegertracing.io/docs/1.55/apis/#opentelemetry-protocol-stable
                // For now, we'll configure OTLP to Jaeger's OTLP endpoint
                builder.AddOtlpExporter(options =>
                {
                    // Jaeger OTLP receiver defaults to port 4317 (gRPC) or 4318 (HTTP)
                    var jaegerEndpoint = $"http://{config.JaegerAgentHost}:4317";
                    options.Endpoint = new Uri(jaegerEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
                break;

            case "multiple":
                // Export to multiple backends simultaneously
                builder.AddConsoleExporter(options =>
                {
                    options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                });

                if (!string.IsNullOrEmpty(config.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(config.OtlpEndpoint);
                        options.Protocol = config.OtlpProtocol.ToLowerInvariant() == "http/protobuf"
                            ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
                            : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }

                if (!string.IsNullOrEmpty(config.JaegerAgentHost))
                {
                    // Use OTLP to Jaeger instead of deprecated Jaeger exporter
                    builder.AddOtlpExporter("jaeger", options =>
                    {
                        var jaegerEndpoint = $"http://{config.JaegerAgentHost}:4317";
                        options.Endpoint = new Uri(jaegerEndpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }
                break;

            case "none":
            default:
                // No exporter configured
                break;
        }

        return builder;
    }

    /// <summary>
    /// Creates a sampler based on the configuration.
    /// </summary>
    /// <param name="config">The tracing configuration.</param>
    /// <returns>The configured sampler.</returns>
    private static Sampler CreateSampler(TracingConfiguration config)
    {
        return config.SamplingStrategy.ToLowerInvariant() switch
        {
            "always_on" => new AlwaysOnSampler(),
            "always_off" => new AlwaysOffSampler(),
            "trace_id_ratio" => new TraceIdRatioBasedSampler(config.SamplingRatio),
            "parent_based" => new ParentBasedSampler(new TraceIdRatioBasedSampler(config.SamplingRatio)),
            _ => new ParentBasedSampler(new AlwaysOnSampler()),
        };
    }

    /// <summary>
    /// Adds custom baggage to the current activity based on configuration.
    /// </summary>
    /// <param name="config">The tracing configuration.</param>
    public static void ApplyBaggage(this TracingConfiguration config)
    {
        var activity = Activity.Current;
        if (activity == null || config.Baggage.Count == 0)
            return;

        foreach (var (key, value) in config.Baggage)
        {
            activity.SetBaggage(key, value);
        }
    }
}
