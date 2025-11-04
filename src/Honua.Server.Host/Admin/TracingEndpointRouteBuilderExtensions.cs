// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Provides API endpoints for runtime tracing configuration.
/// Allows administrators to configure OpenTelemetry tracing exporters and sampling at runtime.
/// </summary>
internal static class TracingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all runtime tracing configuration endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The route group builder for additional configuration.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Viewing current tracing configuration and statistics
    /// - Listing available OpenTelemetry activity sources
    /// - Updating the tracing exporter type (none, console, otlp)
    /// - Configuring OTLP endpoint for distributed tracing backends
    /// - Adjusting sampling ratio for trace collection
    /// - Creating test traces for verification
    /// - Getting platform-specific configuration guidance
    /// </remarks>
    /// <example>
    /// Example request to configure OTLP exporter for Jaeger:
    /// <code>
    /// PATCH /admin/observability/tracing/exporter
    /// { "exporter": "otlp" }
    ///
    /// PATCH /admin/observability/tracing/endpoint
    /// { "endpoint": "http://jaeger:4317" }
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapTracingConfiguration(this WebApplication app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/observability/tracing");
        return MapTracingConfigurationCore(group);
    }

    public static RouteGroupBuilder MapTracingConfiguration(this RouteGroupBuilder group)
    {
        Guard.NotNull(group);

        return MapTracingConfigurationCore(group.MapGroup("/admin/observability/tracing"));
    }

    private static RouteGroupBuilder MapTracingConfigurationCore(RouteGroupBuilder group)
    {
        group.WithTags("Runtime Tracing")
            .RequireAuthorization("RequireAdministrator");

        // GET /admin/observability/tracing - Get current tracing configuration
        group.MapGet("/", ([FromServices] RuntimeTracingConfigurationService tracingConfig) =>
        {
            var stats = tracingConfig.GetStatistics();
            var current = tracingConfig.Current;

            return Results.Ok(new
            {
                configuration = new
                {
                    exporter = current.Exporter,
                    otlpEndpoint = current.OtlpEndpoint,
                    samplingRatio = current.SamplingRatio,
                    samplingPercentage = $"{current.SamplingRatio * 100:F1}%"
                },
                status = new
                {
                    enabled = stats.IsEnabled,
                    activitySources = stats.ActivitySourcesConfigured,
                    note = stats.Note
                },
                exporterOptions = new[]
                {
                    new { value = "none", description = "Disable tracing (no traces exported)" },
                    new { value = "console", description = "Console exporter (logs traces to stdout - for development)" },
                    new { value = "otlp", description = "OTLP exporter (sends traces to Jaeger, Tempo, etc.)" }
                },
                usage = new
                {
                    updateExporter = "PATCH /admin/observability/tracing/exporter with { \"exporter\": \"otlp\" }",
                    updateEndpoint = "PATCH /admin/observability/tracing/endpoint with { \"endpoint\": \"http://jaeger:4317\" }",
                    updateSampling = "PATCH /admin/observability/tracing/sampling with { \"ratio\": 0.1 }",
                    testTracing = "POST /admin/observability/tracing/test"
                }
            });
        });

        // GET /admin/observability/tracing/activity-sources - List available activity sources
        group.MapGet("/activity-sources", () =>
        {
            return Results.Ok(new
            {
                activitySources = new[]
                {
                    new
                    {
                        name = "OGC Protocols",
                        description = "WMS, WFS, WMTS, WCS, CSW operations"
                    },
                    new
                    {
                        name = "OData",
                        description = "OData query operations"
                    },
                    new
                    {
                        name = "STAC",
                        description = "STAC catalog operations"
                    },
                    new
                    {
                        name = "Database",
                        description = "Database query operations"
                    },
                    new
                    {
                        name = "Raster Tiles",
                        description = "Raster tile rendering and caching"
                    },
                    new
                    {
                        name = "Metadata",
                        description = "Metadata operations"
                    },
                    new
                    {
                        name = "Authentication",
                        description = "Authentication and authorization"
                    },
                    new
                    {
                        name = "Export",
                        description = "Data export operations"
                    },
                    new
                    {
                        name = "Import",
                        description = "Data import and migration"
                    }
                },
                note = "All activity sources are automatically configured in OpenTelemetry. Traces will include spans from these sources when tracing is enabled."
            });
        });

        // PATCH /admin/observability/tracing/exporter - Update exporter type
        group.MapPatch("/exporter", (
            UpdateExporterRequest request,
            [FromServices] RuntimeTracingConfigurationService tracingConfig) =>
        {
            try
            {
                tracingConfig.UpdateExporter(request.Exporter);

                return Results.Ok(new
                {
                    status = "updated",
                    exporter = request.Exporter,
                    message = $"Tracing exporter set to '{request.Exporter}'",
                    warning = request.Exporter != "none"
                        ? "⚠️ Exporter changes require application restart to take effect. The new configuration will be used on next startup."
                        : null,
                    nextSteps = request.Exporter == "otlp"
                        ? new[] {
                            "Set OTLP endpoint: PATCH /admin/observability/tracing/endpoint",
                            "Restart application for changes to take effect",
                            "Verify traces in your OTLP backend (Jaeger, Tempo, etc.)"
                        }
                        : request.Exporter == "console"
                            ? new[] {
                                "Restart application for changes to take effect",
                                "Traces will be logged to stdout (check application logs)"
                            }
                            : new[] {
                                "Tracing is now disabled",
                                "Restart application for changes to take effect"
                            }
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message,
                    validExporters = new[] { "none", "console", "otlp" }
                });
            }
        })
        .RequireRateLimiting("admin-operations");

        // PATCH /admin/observability/tracing/endpoint - Update OTLP endpoint
        group.MapPatch("/endpoint", (
            UpdateEndpointRequest request,
            [FromServices] RuntimeTracingConfigurationService tracingConfig) =>
        {
            try
            {
                tracingConfig.UpdateOtlpEndpoint(request.Endpoint);

                return Results.Ok(new
                {
                    status = "updated",
                    endpoint = request.Endpoint,
                    message = $"OTLP endpoint set to '{request.Endpoint}'",
                    warning = "⚠️ Endpoint changes require application restart to take effect. The new configuration will be used on next startup.",
                    note = "Ensure the exporter is set to 'otlp' for this endpoint to be used"
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireRateLimiting("admin-operations");

        // PATCH /admin/observability/tracing/sampling - Update sampling ratio
        group.MapPatch("/sampling", (
            UpdateSamplingRequest request,
            [FromServices] RuntimeTracingConfigurationService tracingConfig) =>
        {
            try
            {
                tracingConfig.UpdateSamplingRatio(request.Ratio);

                return Results.Ok(new
                {
                    status = "updated",
                    samplingRatio = request.Ratio,
                    samplingPercentage = $"{request.Ratio * 100:F1}%",
                    message = $"Sampling ratio set to {request.Ratio * 100:F1}%",
                    note = "Sampling changes take effect immediately (no restart required)",
                    interpretation = request.Ratio switch
                    {
                        1.0 => "100% sampling - all traces will be captured (high overhead, use for development/debugging)",
                        >= 0.1 => $"{request.Ratio * 100:F1}% sampling - moderate overhead, good for staging/testing",
                        > 0.0 => $"{request.Ratio * 100:F1}% sampling - low overhead, recommended for high-traffic production",
                        _ => "0% sampling - tracing effectively disabled"
                    },
                    recommendations = new
                    {
                        development = "1.0 (100%) - capture all traces",
                        staging = "0.1 (10%) - balance between coverage and overhead",
                        production = "0.01-0.05 (1-5%) - minimal overhead for high-volume APIs"
                    }
                });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message,
                    validRange = "0.0 to 1.0 (0% to 100%)"
                });
            }
        })
        .RequireRateLimiting("admin-operations");

        // POST /admin/observability/tracing/test - Create a test trace
        group.MapPost("/test", (
            TestTraceRequest? request,
            [FromServices] RuntimeTracingConfigurationService tracingConfig) =>
        {
            var activityName = request?.ActivityName ?? "Honua.Test.Trace";
            var tags = request?.Tags ?? new Dictionary<string, object?>
            {
                ["test.type"] = "manual",
                ["test.user"] = "administrator"
            };

            // Create a test activity using one of the configured activity sources
            using var activity = HonuaTelemetry.OgcProtocols.StartActivity(activityName);

            if (activity == null)
            {
                return Results.Ok(new
                {
                    status = "warning",
                    message = "No ActivityListener is configured. Tracing may be disabled or exporter is set to 'none'.",
                    recommendation = "Set exporter to 'console' or 'otlp' and restart the application",
                    currentConfig = tracingConfig.Current
                });
            }

            // Add test tags
            foreach (var (key, value) in tags)
            {
                activity.SetTag(key, value);
            }
            activity.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString("o"));
            activity.SetTag("test.source", "admin-api");

            // Simulate nested spans
            using var childActivity = HonuaTelemetry.Database.StartActivity("Test.Database.Query");
            childActivity?.SetTag("db.operation", "SELECT");
            childActivity?.SetTag("db.table", "test");
            childActivity?.SetTag("db.duration_ms", 42);

            return Results.Ok(new
            {
                status = "success",
                message = "Test trace created successfully",
                trace = new
                {
                    activityId = activity.Id,
                    traceId = activity.TraceId.ToString(),
                    spanId = activity.SpanId.ToString(),
                    parentSpanId = activity.ParentSpanId.ToString(),
                    activityName = activity.DisplayName,
                    tags = tags
                },
                currentConfig = tracingConfig.Current,
                note = tracingConfig.Current.Exporter switch
                {
                    "console" => "Check application logs for trace output",
                    "otlp" => $"Check your OTLP backend (e.g., Jaeger at {tracingConfig.Current.OtlpEndpoint}) for this trace",
                    _ => "Tracing is disabled (exporter is 'none'). Set exporter to 'console' or 'otlp' to see traces."
                }
            });
        })
        .RequireRateLimiting("admin-operations");

        // GET /admin/observability/tracing/platforms - Get platform-specific configuration guidance
        group.MapGet("/platforms", () =>
        {
            return Results.Ok(new
            {
                platforms = new[]
                {
                    new
                    {
                        platform = "Jaeger (Self-Hosted)",
                        exporter = "otlp",
                        endpoint = "http://jaeger:4317",
                        setup = new[]
                        {
                            "Deploy Jaeger: docker run -d --name jaeger -e COLLECTOR_OTLP_ENABLED=true -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one:latest",
                            "Set exporter: PATCH /admin/observability/tracing/exporter { \"exporter\": \"otlp\" }",
                            "Set endpoint: PATCH /admin/observability/tracing/endpoint { \"endpoint\": \"http://jaeger:4317\" }",
                            "Restart Honua",
                            "View traces: http://localhost:16686"
                        },
                        documentation = "https://www.jaegertracing.io/docs/latest/getting-started/"
                    },
                    new
                    {
                        platform = "Grafana Tempo",
                        exporter = "otlp",
                        endpoint = "http://tempo:4317",
                        setup = new[]
                        {
                            "Deploy Tempo with OTLP receiver enabled",
                            "Set exporter: PATCH /admin/observability/tracing/exporter { \"exporter\": \"otlp\" }",
                            "Set endpoint: PATCH /admin/observability/tracing/endpoint { \"endpoint\": \"http://tempo:4317\" }",
                            "Restart Honua",
                            "Query traces in Grafana with Tempo datasource"
                        },
                        documentation = "https://grafana.com/docs/tempo/latest/"
                    },
                    new
                    {
                        platform = "Azure Application Insights",
                        exporter = "otlp",
                        endpoint = "https://dc.services.visualstudio.com/v2/track",
                        setup = new[]
                        {
                            "Create Application Insights resource in Azure",
                            "Get connection string from Azure portal",
                            "Use Azure Monitor OpenTelemetry exporter (requires code changes)",
                            "Alternative: Use OTLP exporter with Application Insights ingestion endpoint"
                        },
                        documentation = "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore"
                    },
                    new
                    {
                        platform = "AWS X-Ray",
                        exporter = "otlp",
                        endpoint = "http://xray-daemon:2000",
                        setup = new[]
                        {
                            "Deploy AWS Distro for OpenTelemetry (ADOT) Collector",
                            "Configure ADOT to export to X-Ray",
                            "Set exporter: PATCH /admin/observability/tracing/exporter { \"exporter\": \"otlp\" }",
                            "Set endpoint to ADOT collector: { \"endpoint\": \"http://adot-collector:4317\" }",
                            "Restart Honua",
                            "View traces in AWS X-Ray console"
                        },
                        documentation = "https://aws-otel.github.io/docs/getting-started/dotnet-sdk/trace-manual-instr"
                    },
                    new
                    {
                        platform = "Google Cloud Trace",
                        exporter = "otlp",
                        endpoint = "https://cloudtrace.googleapis.com/v1/projects/PROJECT_ID/traces",
                        setup = new[]
                        {
                            "Enable Cloud Trace API in Google Cloud Console",
                            "Set up authentication (service account or workload identity)",
                            "Use Google Cloud OpenTelemetry exporter (requires code changes)",
                            "Alternative: Use OTLP with Cloud Trace OTLP endpoint"
                        },
                        documentation = "https://cloud.google.com/trace/docs/setup/dotnet-ot"
                    },
                    new
                    {
                        platform = "Console (Development)",
                        exporter = "console",
                        endpoint = "N/A (logs to stdout)",
                        setup = new[]
                        {
                            "Set exporter: PATCH /admin/observability/tracing/exporter { \"exporter\": \"console\" }",
                            "Restart Honua",
                            "Traces will be logged to application stdout/stderr",
                            "Check logs: docker logs honua-web -f"
                        },
                        documentation = "Built-in OpenTelemetry console exporter"
                    }
                },
                note = "After configuring exporter and endpoint, restart the application for changes to take effect. Sampling ratio can be adjusted without restart."
            });
        });

        return group;
    }

    /// <summary>
    /// Request model for updating the tracing exporter type.
    /// </summary>
    /// <param name="Exporter">The exporter type (none, console, or otlp).</param>
    private sealed record UpdateExporterRequest(string Exporter);

    /// <summary>
    /// Request model for updating the OTLP endpoint.
    /// </summary>
    /// <param name="Endpoint">The OTLP endpoint URL (e.g., http://jaeger:4317).</param>
    private sealed record UpdateEndpointRequest(string Endpoint);

    /// <summary>
    /// Request model for updating the trace sampling ratio.
    /// </summary>
    /// <param name="Ratio">The sampling ratio between 0.0 (0%) and 1.0 (100%).</param>
    private sealed record UpdateSamplingRequest(double Ratio);

    /// <summary>
    /// Request model for creating a test trace.
    /// </summary>
    /// <param name="ActivityName">The name of the test activity (optional).</param>
    /// <param name="Tags">Custom tags to add to the test trace (optional).</param>
    private sealed record TestTraceRequest(string? ActivityName, Dictionary<string, object?>? Tags);
}
