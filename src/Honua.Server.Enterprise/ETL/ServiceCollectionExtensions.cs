// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.AI;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Resilience;
using Honua.Server.Enterprise.ETL.Scheduling;
using Honua.Server.Enterprise.ETL.Stores;
using Honua.Server.Enterprise.ETL.Templates;
using Honua.Server.Enterprise.Geoprocessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL;

/// <summary>
/// Extension methods for registering GeoETL services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers GeoETL services with dependency injection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="usePostgresStore">Whether to use PostgreSQL store (default) or in-memory store</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEtl(
        this IServiceCollection services,
        string connectionString,
        bool usePostgresStore = true)
    {
        // Register workflow store
        if (usePostgresStore)
        {
            services.AddSingleton<IWorkflowStore>(sp =>
                new PostgresWorkflowStore(connectionString, sp.GetRequiredService<ILogger<PostgresWorkflowStore>>()));
        }
        else
        {
            services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
        }

        // Register node registry
        services.AddSingleton<IWorkflowNodeRegistry>(sp =>
        {
            var registry = new WorkflowNodeRegistry();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var geoprocessingService = sp.GetRequiredService<IGeoprocessingService>();
            var geoNodeFactory = new GeoprocessingNodeFactory(geoprocessingService, loggerFactory);

            // Register geoprocessing nodes (Phase 1 + 1.5: 7 operations)
            // Phase 1: Buffer, Intersection, Union
            // Phase 1.5: Difference, Simplify, ConvexHull, Dissolve
            foreach (var operation in GeoprocessingNodeFactory.GetAvailableOperations())
            {
                var node = geoNodeFactory.CreateNode(operation);
                registry.RegisterNode(node.NodeType, node);
            }

            // Register data source nodes (10 total: 5 legacy + 4 new GDAL formats)
            registry.RegisterNode("data_source.postgis",
                new PostGisDataSourceNode(connectionString, loggerFactory.CreateLogger<PostGisDataSourceNode>()));
            registry.RegisterNode("data_source.file",
                new FileDataSourceNode(loggerFactory.CreateLogger<FileDataSourceNode>()));
            registry.RegisterNode("data_source.geopackage",
                new GeoPackageDataSourceNode(connectionString, loggerFactory.CreateLogger<GeoPackageDataSourceNode>()));
            registry.RegisterNode("data_source.shapefile",
                new ShapefileDataSourceNode(loggerFactory.CreateLogger<ShapefileDataSourceNode>()));
            registry.RegisterNode("data_source.kml",
                new KmlDataSourceNode(loggerFactory.CreateLogger<KmlDataSourceNode>()));
            registry.RegisterNode("data_source.csv_geometry",
                new CsvGeometryDataSourceNode(loggerFactory.CreateLogger<CsvGeometryDataSourceNode>()));
            registry.RegisterNode("data_source.gpx",
                new GpxDataSourceNode(loggerFactory.CreateLogger<GpxDataSourceNode>()));
            registry.RegisterNode("data_source.gml",
                new GmlDataSourceNode(loggerFactory.CreateLogger<GmlDataSourceNode>()));
            registry.RegisterNode("data_source.wfs",
                new WfsDataSourceNode(loggerFactory.CreateLogger<WfsDataSourceNode>()));

            // Get exporters from DI
            var geoPackageExporter = sp.GetRequiredService<Core.Export.IGeoPackageExporter>();
            var shapefileExporter = sp.GetRequiredService<Core.Export.IShapefileExporter>();

            // Register data sink nodes (8 total: 5 legacy + 3 new GDAL formats)
            registry.RegisterNode("data_sink.postgis",
                new PostGisDataSinkNode(connectionString, loggerFactory.CreateLogger<PostGisDataSinkNode>()));
            registry.RegisterNode("data_sink.geojson",
                new GeoJsonExportNode(loggerFactory.CreateLogger<GeoJsonExportNode>()));
            registry.RegisterNode("data_sink.output",
                new OutputNode(loggerFactory.CreateLogger<OutputNode>()));
            registry.RegisterNode("data_sink.geopackage",
                new GeoPackageSinkNode(geoPackageExporter, loggerFactory.CreateLogger<GeoPackageSinkNode>()));
            registry.RegisterNode("data_sink.shapefile",
                new ShapefileSinkNode(shapefileExporter, loggerFactory.CreateLogger<ShapefileSinkNode>()));
            registry.RegisterNode("data_sink.csv_geometry",
                new CsvGeometrySinkNode(loggerFactory.CreateLogger<CsvGeometrySinkNode>()));
            registry.RegisterNode("data_sink.gpx",
                new GpxSinkNode(loggerFactory.CreateLogger<GpxSinkNode>()));
            registry.RegisterNode("data_sink.gml",
                new GmlSinkNode(loggerFactory.CreateLogger<GmlSinkNode>()));

            return registry;
        });

        // Register workflow engine
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        // Register workflow template repository
        services.AddSingleton<IWorkflowTemplateRepository, JsonWorkflowTemplateRepository>();

        return services;
    }

    /// <summary>
    /// Registers GeoETL workflow scheduling services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="enableScheduleExecutor">Whether to enable the background schedule executor (default: true)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEtlScheduling(
        this IServiceCollection services,
        string connectionString,
        bool enableScheduleExecutor = true)
    {
        // Register schedule store
        services.AddSingleton<IWorkflowScheduleStore>(sp =>
            new PostgresWorkflowScheduleStore(connectionString, sp.GetRequiredService<ILogger<PostgresWorkflowScheduleStore>>()));

        // Register schedule executor as hosted service (optional)
        if (enableScheduleExecutor)
        {
            services.AddHostedService<ScheduleExecutor>();
        }

        return services;
    }

    /// <summary>
    /// Registers GeoETL services with in-memory store (for testing)
    /// </summary>
    public static IServiceCollection AddGeoEtlInMemory(this IServiceCollection services, string connectionString)
    {
        return AddGeoEtl(services, connectionString, usePostgresStore: false);
    }

    /// <summary>
    /// Registers AI-powered workflow generation services (optional)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing OpenAI settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEtlAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read OpenAI configuration from appsettings
        var config = new OpenAiConfiguration();
        configuration.GetSection("OpenAI").Bind(config);

        // Only register if API key is configured
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            services.AddSingleton(config);
            services.AddHttpClient<IGeoEtlAiService, OpenAiGeoEtlService>();

            // Also register as singleton without HttpClient for DI
            services.AddSingleton<IGeoEtlAiService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(OpenAiGeoEtlService));
                var logger = sp.GetRequiredService<ILogger<OpenAiGeoEtlService>>();
                return new OpenAiGeoEtlService(httpClient, config, logger);
            });
        }
        else
        {
            // Register null service when not configured (graceful degradation)
            services.AddSingleton<IGeoEtlAiService?>(sp => null);
        }

        return services;
    }

    /// <summary>
    /// Registers AI services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="config">OpenAI configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEtlAi(
        this IServiceCollection services,
        OpenAiConfiguration config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            services.AddSingleton<IGeoEtlAiService?>(sp => null);
            return services;
        }

        services.AddSingleton(config);
        services.AddHttpClient<IGeoEtlAiService, OpenAiGeoEtlService>();

        services.AddSingleton<IGeoEtlAiService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(OpenAiGeoEtlService));
            var logger = sp.GetRequiredService<ILogger<OpenAiGeoEtlService>>();
            return new OpenAiGeoEtlService(httpClient, config, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers GeoETL resilience and error handling services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing resilience settings</param>
    /// <param name="connectionString">PostgreSQL connection string for dead letter queue</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddGeoEtlResilience(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        // Configure circuit breaker options
        services.Configure<CircuitBreakerOptions>(options =>
        {
            var config = configuration.GetSection("GeoETL:CircuitBreaker");
            options.FailureThreshold = config.GetValue<int>("FailureThreshold", 5);
            options.TimeoutSeconds = config.GetValue<int>("TimeoutSeconds", 60);
        });

        // Register circuit breaker service (if enabled)
        var circuitBreakerEnabled = configuration.GetValue<bool>("GeoETL:CircuitBreaker:Enabled", true);
        if (circuitBreakerEnabled)
        {
            services.AddSingleton<ICircuitBreakerService, InMemoryCircuitBreakerService>();
        }

        // Register dead letter queue service (if enabled)
        var dlqEnabled = configuration.GetValue<bool>("GeoETL:DeadLetterQueue:Enabled", true);
        if (dlqEnabled)
        {
            services.AddSingleton<IDeadLetterQueueService>(sp =>
                new PostgresDeadLetterQueueService(
                    connectionString,
                    sp.GetRequiredService<IWorkflowEngine>(),
                    sp.GetRequiredService<ILogger<PostgresDeadLetterQueueService>>()));
        }

        return services;
    }
}
