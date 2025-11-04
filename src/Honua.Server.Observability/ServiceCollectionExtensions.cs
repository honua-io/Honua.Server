// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Observability.HealthChecks;
using Honua.Server.Observability.Metrics;
using Honua.Server.Observability.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Honua.Server.Observability;

/// <summary>
/// Extension methods for configuring Honua observability services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds comprehensive observability services including metrics, logging, tracing, and health checks.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="serviceName">Service name for telemetry</param>
    /// <param name="serviceVersion">Service version for telemetry</param>
    /// <param name="connectionString">Database connection string for health checks</param>
    /// <param name="configureTracing">Optional action to configure tracing</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHonuaObservability(
        this IServiceCollection services,
        string serviceName = "Honua.Server",
        string serviceVersion = "1.0.0",
        string? connectionString = null,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        // Add OpenTelemetry with comprehensive instrumentation
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment",
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"),
                    new KeyValuePair<string, object>("host.name",
                        Environment.MachineName),
                    new KeyValuePair<string, object>("service.namespace", "Honua"),
                    new KeyValuePair<string, object>("service.instance.id",
                        Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName)
                }))
            .WithMetrics(builder =>
            {
                builder
                    // Add custom meters
                    .AddMeter("Honua.BuildQueue")
                    .AddMeter("Honua.Cache")
                    .AddMeter("Honua.License")
                    .AddMeter("Honua.Registry")
                    .AddMeter("Honua.Intake")
                    .AddMeter("Honua.Http")

                    // Add runtime instrumentation
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()

                    // Add Prometheus exporter
                    .AddPrometheusExporter(options =>
                    {
                        options.ScrapeEndpointPath = "/metrics";
                        options.ScrapeResponseCacheDurationMilliseconds = 0;
                    });
            })
            .WithTracing(builder =>
            {
                // Configure comprehensive distributed tracing
                builder
                    // ASP.NET Core instrumentation - trace incoming HTTP requests
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Don't trace health check endpoints (too noisy)
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
                        };
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent.ToString());
                            activity.SetTag("http.request.header.accept", request.Headers.Accept.ToString());
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.content_length", response.ContentLength);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.escaped", true);
                        };
                    })

                    // HTTP Client instrumentation - trace outgoing HTTP requests
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Don't trace internal health checks or metrics
                            var uri = request.RequestUri?.ToString() ?? string.Empty;
                            return !uri.Contains("/health") && !uri.Contains("/metrics");
                        };
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.client.method", request.Method.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.client.status_code", (int)response.StatusCode);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.http_client", true);
                        };
                    })

                    // SQL Client instrumentation - trace database operations
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.SetDbStatementForText = true;
                        // Note: Enrich callback signature changed in newer versions
                        // We'll rely on automatic instrumentation for now
                    })

                    // StackExchange.Redis instrumentation - trace cache operations
                    .AddRedisInstrumentation(options =>
                    {
                        options.SetVerboseDatabaseStatements = false; // Don't log commands (security)
                        options.Enrich = (activity, command) =>
                        {
                            activity.SetTag("db.system", "redis");
                        };
                    })

                    // Add all Honua activity sources
                    .AddSource("Honua.Server.OgcProtocols")
                    .AddSource("Honua.Server.OData")
                    .AddSource("Honua.Server.Stac")
                    .AddSource("Honua.Server.Database")
                    .AddSource("Honua.Server.RasterTiles")
                    .AddSource("Honua.Server.Metadata")
                    .AddSource("Honua.Server.Authentication")
                    .AddSource("Honua.Server.Export")
                    .AddSource("Honua.Server.Import")
                    .AddSource("Honua.Server.Notifications")

                    // Configure sampling - default to always on for development
                    .SetSampler(new AlwaysOnSampler());

                // Allow custom tracing configuration
                configureTracing?.Invoke(builder);
            });

        // Register metric services
        services.AddSingleton<BuildQueueMetrics>();
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<LicenseMetrics>();
        services.AddSingleton<RegistryMetrics>();
        services.AddSingleton<IntakeMetrics>();

        // Add health checks
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHealthChecks()
                .AddCheck("database",
                    new DatabaseHealthCheck(connectionString),
                    HealthStatus.Unhealthy,
                    tags: new[] { "database", "postgres" })
                .AddCheck("license",
                    new LicenseHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "license" })
                .AddCheck("queue",
                    new QueueHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "queue", "build" })
                .AddCheck("registry",
                    new RegistryHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "registry", "docker" });
        }
        else
        {
            services.AddHealthChecks();
        }

        return services;
    }

    /// <summary>
    /// Configures Serilog structured logging for Honua services.
    /// </summary>
    public static ILoggingBuilder AddHonuaSerilog(
        this ILoggingBuilder loggingBuilder,
        string serviceName = "Honua.Server",
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.File(
                new CompactJsonFormatter(),
                path: $"logs/{serviceName}-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog(dispose: true);

        return loggingBuilder;
    }

    /// <summary>
    /// Adds the correlation ID and metrics middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseHonuaMetrics(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<MetricsMiddleware>();
        return app;
    }

    /// <summary>
    /// Maps health check endpoints.
    /// </summary>
    public static IApplicationBuilder UseHonuaHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health");
        app.UseHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false // Liveness - always healthy if app is running
        });
        app.UseHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("queue")
        });

        return app;
    }

    /// <summary>
    /// Maps Prometheus metrics endpoint.
    /// </summary>
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }
}
