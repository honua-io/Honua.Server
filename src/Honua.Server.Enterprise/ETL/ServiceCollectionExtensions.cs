// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Stores;
using Honua.Server.Enterprise.Geoprocessing;
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

            // Register data source nodes
            registry.RegisterNode("data_source.postgis",
                new PostGisDataSourceNode(connectionString, loggerFactory.CreateLogger<PostGisDataSourceNode>()));
            registry.RegisterNode("data_source.file",
                new FileDataSourceNode(loggerFactory.CreateLogger<FileDataSourceNode>()));

            // Register data sink nodes
            registry.RegisterNode("data_sink.postgis",
                new PostGisDataSinkNode(connectionString, loggerFactory.CreateLogger<PostGisDataSinkNode>()));
            registry.RegisterNode("data_sink.geojson",
                new GeoJsonExportNode(loggerFactory.CreateLogger<GeoJsonExportNode>()));
            registry.RegisterNode("data_sink.output",
                new OutputNode(loggerFactory.CreateLogger<OutputNode>()));

            return registry;
        });

        // Register workflow engine
        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();

        return services;
    }

    /// <summary>
    /// Registers GeoETL services with in-memory store (for testing)
    /// </summary>
    public static IServiceCollection AddGeoEtlInMemory(this IServiceCollection services, string connectionString)
    {
        return AddGeoEtl(services, connectionString, usePostgresStore: false);
    }
}
