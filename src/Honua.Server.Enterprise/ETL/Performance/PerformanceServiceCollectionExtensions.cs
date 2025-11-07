// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Enterprise.ETL.Caching;
using Honua.Server.Enterprise.ETL.Database;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Honua.Server.Enterprise.ETL.Performance;

/// <summary>
/// Service collection extensions for registering performance optimizations
/// </summary>
public static class PerformanceServiceCollectionExtensions
{
    /// <summary>
    /// Adds GeoETL performance optimizations to the service collection
    /// </summary>
    public static IServiceCollection AddGeoEtlPerformanceOptimizations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register cache options
        services.Configure<CacheOptions>(configuration.GetSection("GeoETL:Performance:Cache"));

        // Register database optimization options
        services.Configure<DatabaseOptimizationOptions>(configuration.GetSection("GeoETL:Performance:Database"));

        // Register cache provider
        var cacheProvider = configuration.GetValue<string>("GeoETL:Performance:Cache:Provider") ?? "Memory";

        if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            // Register Redis
            var redisConnectionString = configuration.GetValue<string>("GeoETL:Performance:Cache:RedisConnectionString");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(redisConnectionString));

                services.AddSingleton<IWorkflowCache, RedisWorkflowCache>();
            }
            else
            {
                // Fallback to memory cache
                services.AddMemoryCache();
                services.AddSingleton<IWorkflowCache, MemoryWorkflowCache>();
            }
        }
        else
        {
            // Use memory cache
            services.AddMemoryCache();
            services.AddSingleton<IWorkflowCache, MemoryWorkflowCache>();
        }

        // Register performance metrics
        services.AddSingleton<IPerformanceMetrics, PerformanceMetrics>();

        // Register batch database operations
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BatchDatabaseOperations>>();
            var metrics = sp.GetService<IPerformanceMetrics>();
            var batchSize = configuration.GetValue<int>("GeoETL:Performance:Database:DefaultBatchSize", 1000);
            return new BatchDatabaseOperations(logger, metrics, batchSize);
        });

        // Register memory pooling
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MemoryPressureManager>>();
            var maxMemoryMB = configuration.GetValue<long>("GeoETL:Performance:Memory:MaxMemoryMB", 2048);
            return new MemoryPressureManager(logger, maxMemoryMB);
        });

        // Register workflow engine based on configuration
        var engineType = configuration.GetValue<string>("GeoETL:Performance:EngineType") ?? "Sequential";

        if (engineType.Equals("Parallel", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ParallelWorkflowEngineOptions>(sp =>
            {
                var maxParallelNodes = configuration.GetValue<int>("GeoETL:Performance:MaxParallelNodes",
                    Environment.ProcessorCount);
                var maxMemoryPerWorkflow = configuration.GetValue<long>("GeoETL:Performance:MaxMemoryPerWorkflowMB", 512);

                return new ParallelWorkflowEngineOptions
                {
                    MaxParallelNodes = maxParallelNodes,
                    MaxMemoryPerWorkflowMB = maxMemoryPerWorkflow,
                    EnableResourceAwareScheduling = true
                };
            });

            // Use parallel engine
            services.AddScoped<IWorkflowEngine, ParallelWorkflowEngine>();
        }
        else
        {
            // Use sequential engine (default)
            services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        }

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation for GeoETL
    /// </summary>
    public static IServiceCollection AddGeoEtlOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var enableMetrics = configuration.GetValue<bool>("GeoETL:Performance:Monitoring:EnableMetrics", true);

        if (enableMetrics)
        {
            services.AddOpenTelemetry()
                .WithMetrics(builder =>
                {
                    builder
                        .AddMeter("Honua.GeoETL")
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation();

                    // Add Prometheus exporter if enabled
                    var prometheusEnabled = configuration.GetValue<bool>("GeoETL:Performance:Monitoring:Prometheus:Enabled", false);
                    if (prometheusEnabled)
                    {
                        builder.AddPrometheusExporter();
                    }

                    // Add OTLP exporter if configured
                    var otlpEndpoint = configuration.GetValue<string>("GeoETL:Performance:Monitoring:OtlpEndpoint");
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        builder.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                });
        }

        return services;
    }

    /// <summary>
    /// Adds Application Insights for GeoETL
    /// </summary>
    public static IServiceCollection AddGeoEtlApplicationInsights(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appInsightsEnabled = configuration.GetValue<bool>("GeoETL:Performance:Monitoring:ApplicationInsights:Enabled", false);

        if (appInsightsEnabled)
        {
            var connectionString = configuration.GetValue<string>("GeoETL:Performance:Monitoring:ApplicationInsights:ConnectionString");

            if (!string.IsNullOrEmpty(connectionString))
            {
                services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = connectionString;
                    options.EnableAdaptiveSampling = true;
                    options.EnableQuickPulseMetricStream = true;
                });
            }
        }

        return services;
    }
}
