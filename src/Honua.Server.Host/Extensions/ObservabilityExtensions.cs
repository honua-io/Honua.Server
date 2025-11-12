// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Raster;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring observability features including logging, metrics, and tracing.
/// </summary>
internal static class ObservabilityExtensions
{
    /// <summary>
    /// Configures logging with JSON console output and runtime configuration support.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The web application builder for method chaining.</returns>
    public static WebApplicationBuilder AddHonuaLogging(
        this WebApplicationBuilder builder,
        IConfiguration configuration)
    {
        // Register ObservabilityOptions for validation
        builder.Services.Configure<ObservabilityOptions>(configuration.GetSection("observability"));
        builder.Services.AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>();

        var observability = configuration.GetSection("observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        var logging = observability.Logging;

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(configuration.GetSection("Logging"));

        var includeScopes = logging?.IncludeScopes ?? false;
        var useJson = logging?.JsonConsole is null || logging.JsonConsole.Value;

        if (useJson)
        {
            builder.Logging.AddJsonConsole(options => options.IncludeScopes = includeScopes);
        }
        else
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = includeScopes;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
                options.UseUtcTimestamp = true;
            });
        }

        builder.Logging.AddDebug();

        // Add runtime logging configuration filter via options so overrides are resolved once
        builder.Services.AddOptions<LoggerFilterOptions>()
            .Configure<RuntimeLoggingConfigurationService>((options, runtime) =>
            {
                options.AddFilter((provider, category, level) =>
                {
                    var decision = runtime.IsEnabled(category ?? string.Empty, level);
                    return decision ?? true;
                });
            });

        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry observability with metrics and tracing support.
    /// Configures Prometheus metrics export and OTLP/console trace export.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ObservabilityOptions>(configuration.GetSection("observability"));
        var observability = configuration.GetSection("observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        services.AddSingleton<RuntimeLoggingConfigurationService>();

        // Register all metrics services
        services.AddSingleton<IApiMetrics, ApiMetrics>();
        services.AddSingleton<IDatabaseMetrics, DatabaseMetrics>();
        services.AddSingleton<ICacheMetrics, CacheMetrics>();
        services.AddSingleton<IQueryMetrics, QueryMetrics>();
        services.AddSingleton<IVectorTileMetrics, VectorTileMetrics>();
        services.AddSingleton<ISecurityMetrics, SecurityMetrics>();
        services.AddSingleton<IBusinessMetrics, BusinessMetrics>();
        services.AddSingleton<IInfrastructureMetrics, InfrastructureMetrics>();
        services.AddSingleton<IRasterTileCacheMetrics, RasterTileCacheMetrics>();

        var otelBuilder = services.AddOpenTelemetry();

        if (observability.Metrics is { Enabled: true } metrics)
        {
            otelBuilder.WithMetrics(metricBuilder =>
            {
                // ASP.NET Core and runtime instrumentation
                metricBuilder.AddAspNetCoreInstrumentation();
                metricBuilder.AddRuntimeInstrumentation();

                // Add all Honua meters
                metricBuilder.AddMeter("Honua.Server.Api");
                metricBuilder.AddMeter("Honua.Server.Database");
                metricBuilder.AddMeter("Honua.Server.Cache");
                metricBuilder.AddMeter("Honua.Server.Query");
                metricBuilder.AddMeter("Honua.Server.VectorTiles");
                metricBuilder.AddMeter("Honua.Server.Security");
                metricBuilder.AddMeter("Honua.Server.Business");
                metricBuilder.AddMeter("Honua.Server.Infrastructure");
                metricBuilder.AddMeter("Honua.Server.RasterCache");

                if (metrics.UsePrometheus)
                {
                    metricBuilder.AddPrometheusExporter();
                }
            });
        }

        otelBuilder.WithTracing(tracingBuilder =>
        {
            tracingBuilder.AddAspNetCoreInstrumentation();
            tracingBuilder.AddSource("Honua.Server.OgcProtocols");
            tracingBuilder.AddSource("Honua.Server.OData");
            tracingBuilder.AddSource("Honua.Server.Stac");
            tracingBuilder.AddSource("Honua.Server.Database");
            tracingBuilder.AddSource("Honua.Server.RasterTiles");
            tracingBuilder.AddSource("Honua.Server.Metadata");
            tracingBuilder.AddSource("Honua.Server.Authentication");
            tracingBuilder.AddSource("Honua.Server.Export");
            tracingBuilder.AddSource("Honua.Server.Import");

            var exporterType = observability.Tracing?.Exporter?.ToLowerInvariant() ?? "none";

            switch (exporterType)
            {
                case "otlp":
                    tracingBuilder.AddOtlpExporter(otlpOptions =>
                    {
                        var endpoint = observability.Tracing?.OtlpEndpoint ?? configuration.GetSection("observability:tracing").GetValue<string>("otlpEndpoint");
                        if (!endpoint.IsNullOrEmpty())
                        {
                            otlpOptions.Endpoint = new Uri(endpoint);
                        }
                    });
                    break;

                case "console":
                    tracingBuilder.AddConsoleExporter();
                    break;

                case "none":
                default:
                    break;
            }
        });

        return services;
    }

    /// <summary>
    /// Configures the Prometheus metrics endpoint if enabled.
    /// Requires authentication in non-QuickStart mode.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaMetricsEndpoint(this WebApplication app)
    {
        var observability = app.Services.GetService<IOptions<ObservabilityOptions>>()?.Value;
        if (observability?.Metrics.Enabled == true && observability.Metrics.UsePrometheus)
        {
            var endpoint = ResolveMetricsEndpoint(observability.Metrics.Endpoint);
            var builderEndpoint = app.MapPrometheusScrapingEndpoint(endpoint);

            var authOptions = app.Services.GetRequiredService<IOptions<HonuaAuthenticationOptions>>().Value;
            if (authOptions.Mode != HonuaAuthenticationOptions.AuthenticationMode.QuickStart)
            {
                builderEndpoint.RequireAuthorization("RequireViewer");
            }
        }

        return app;
    }

    private static string ResolveMetricsEndpoint(string? endpoint)
    {
        if (endpoint.IsNullOrWhiteSpace())
        {
            return "/metrics";
        }

        return endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : $"/{endpoint}";
    }
}
