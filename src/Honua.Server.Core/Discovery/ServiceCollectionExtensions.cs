// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Data.Validation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Core.Discovery;

/// <summary>
/// Extension methods for registering auto-discovery services.
/// </summary>
public static class DiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// Adds Honua auto-discovery services for zero-configuration table exposure.
    /// Enables automatic discovery of PostGIS tables and exposure via OData and OGC APIs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for discovery options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaAutoDiscovery(
        this IServiceCollection services,
        Action<AutoDiscoveryOptions>? configure = null)
    {
        // Register options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // Register default options
            services.Configure<AutoDiscoveryOptions>(_ => { });
        }

        // Register core discovery service
        services.TryAddSingleton<PostgresSchemaDiscoveryService>();
        services.TryAddSingleton<PostGisTableDiscoveryService>();

        // Add memory cache if not already registered
        services.TryAddSingleton<IMemoryCache>(sp =>
        {
            var options = new MemoryCacheOptions
            {
                SizeLimit = 100 // Limit cache to 100 items
            };
            return new MemoryCache(options);
        });

        // Decorate with caching
        services.Decorate<ITableDiscoveryService, CachedTableDiscoveryService>();

        return services;
    }

    /// <summary>
    /// Adds Honua auto-discovery services with typed configuration.
    /// </summary>
    public static IServiceCollection AddHonuaAutoDiscovery(
        this IServiceCollection services,
        AutoDiscoveryOptions options)
    {
        return services.AddHonuaAutoDiscovery(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.DiscoverPostGISTablesAsODataCollections = options.DiscoverPostGISTablesAsODataCollections;
            opt.DiscoverPostGISTablesAsOgcCollections = options.DiscoverPostGISTablesAsOgcCollections;
            opt.DefaultSRID = options.DefaultSRID;
            opt.ExcludeSchemas = options.ExcludeSchemas;
            opt.ExcludeTablePatterns = options.ExcludeTablePatterns;
            opt.RequireSpatialIndex = options.RequireSpatialIndex;
            opt.MaxTables = options.MaxTables;
            opt.CacheDuration = options.CacheDuration;
            opt.UseFriendlyNames = options.UseFriendlyNames;
            opt.GenerateOpenApiDocs = options.GenerateOpenApiDocs;
            opt.ComputeExtentOnDiscovery = options.ComputeExtentOnDiscovery;
            opt.IncludeNonSpatialTables = options.IncludeNonSpatialTables;
            opt.DefaultFolderId = options.DefaultFolderId;
            opt.DefaultFolderTitle = options.DefaultFolderTitle;
            opt.DataSourceId = options.DataSourceId;
            opt.BackgroundRefresh = options.BackgroundRefresh;
            opt.BackgroundRefreshInterval = options.BackgroundRefreshInterval;
        });
    }
}
