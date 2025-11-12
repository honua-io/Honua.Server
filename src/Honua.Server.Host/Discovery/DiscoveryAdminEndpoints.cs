// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Discovery;

/// <summary>
/// Admin endpoints for managing auto-discovery.
/// </summary>
public static class DiscoveryAdminEndpoints
{
    /// <summary>
    /// Maps discovery admin endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapDiscoveryAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/discovery")
            .WithTags("Admin - Discovery")
            .RequireAuthorization("RequireAdministrator"); // Require admin authorization

        // GET /admin/discovery/status - Get discovery status
        group.MapGet("/status", GetDiscoveryStatus)
            .WithName("GetDiscoveryStatus")
            .WithSummary("Get auto-discovery status and statistics")
            .WithDescription("Returns information about the auto-discovery feature including enabled status, cache status, and discovered table counts.")
            .Produces<DiscoveryStatus>(StatusCodes.Status200OK);

        // GET /admin/discovery/tables - List discovered tables
        group.MapGet("/tables", GetDiscoveredTables)
            .WithName("GetDiscoveredTables")
            .WithSummary("List all discovered tables")
            .WithDescription("Returns a list of all tables discovered from the configured data source.")
            .Produces<DiscoveredTablesResponse>(StatusCodes.Status200OK);

        // GET /admin/discovery/tables/{schema}/{table} - Get specific table
        group.MapGet("/tables/{schema}/{table}", GetDiscoveredTable)
            .WithName("GetDiscoveredTable")
            .WithSummary("Get details for a specific discovered table")
            .Produces<DiscoveredTable>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // POST /admin/discovery/refresh - Force refresh of discovery cache
        group.MapPost("/refresh", RefreshDiscoveryCache)
            .WithName("RefreshDiscoveryCache")
            .WithSummary("Force refresh of the discovery cache")
            .WithDescription("Clears the discovery cache and triggers a new discovery scan.")
            .Produces<RefreshResult>(StatusCodes.Status200OK);

        // POST /admin/discovery/clear-cache - Clear discovery cache
        group.MapPost("/clear-cache", ClearDiscoveryCache)
            .WithName("ClearDiscoveryCache")
            .WithSummary("Clear the discovery cache")
            .Produces<ClearCacheResult>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetDiscoveryStatus(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AutoDiscoveryOptions>>().Value;
        var metadataRegistry = context.RequestServices.GetRequiredService<IMetadataRegistry>();

        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);

        // Find PostGIS data sources
        var postGisDataSources = snapshot.DataSources
            .Where(ds => string.Equals(ds.Provider, "postgis", StringComparison.OrdinalIgnoreCase))
            .Select(ds => ds.Id)
            .ToList();

        var status = new DiscoveryStatus
        {
            Enabled = options.Enabled,
            ODataDiscoveryEnabled = options.DiscoverPostGISTablesAsODataCollections,
            OgcDiscoveryEnabled = options.DiscoverPostGISTablesAsOgcCollections,
            CacheDuration = options.CacheDuration,
            MaxTables = options.MaxTables,
            RequireSpatialIndex = options.RequireSpatialIndex,
            PostGisDataSourceCount = postGisDataSources.Count,
            ConfiguredDataSourceId = options.DataSourceId,
            BackgroundRefreshEnabled = options.BackgroundRefresh,
            BackgroundRefreshInterval = options.BackgroundRefreshInterval ?? options.CacheDuration
        };

        return Results.Ok(status);
    }

    private static async Task<IResult> GetDiscoveredTables(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var discoveryService = context.RequestServices.GetRequiredService<ITableDiscoveryService>();
        var options = context.RequestServices.GetRequiredService<IOptions<AutoDiscoveryOptions>>().Value;
        var metadataRegistry = context.RequestServices.GetRequiredService<IMetadataRegistry>();

        if (!options.Enabled)
        {
            return Results.Ok(new DiscoveredTablesResponse
            {
                Tables = Array.Empty<DiscoveredTable>(),
                TotalCount = 0,
                Message = "Auto-discovery is disabled"
            });
        }

        // Determine which data source to use
        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);

        var dataSourceId = options.DataSourceId;
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            // Use first PostGIS data source
            var postGisDs = snapshot.DataSources.FirstOrDefault(ds =>
                string.Equals(ds.Provider, "postgis", StringComparison.OrdinalIgnoreCase));

            if (postGisDs == null)
            {
                return Results.Ok(new DiscoveredTablesResponse
                {
                    Tables = Array.Empty<DiscoveredTable>(),
                    TotalCount = 0,
                    Message = "No PostGIS data sources configured"
                });
            }

            dataSourceId = postGisDs.Id;
        }

        var tables = await discoveryService.DiscoverTablesAsync(dataSourceId, cancellationToken);

        var tableList = tables.ToList();

        return Results.Ok(new DiscoveredTablesResponse
        {
            Tables = tableList,
            TotalCount = tableList.Count,
            DataSourceId = dataSourceId
        });
    }

    private static async Task<IResult> GetDiscoveredTable(
        string schema,
        string table,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var discoveryService = context.RequestServices.GetRequiredService<ITableDiscoveryService>();
        var options = context.RequestServices.GetRequiredService<IOptions<AutoDiscoveryOptions>>().Value;
        var metadataRegistry = context.RequestServices.GetRequiredService<IMetadataRegistry>();

        if (!options.Enabled)
        {
            return Results.NotFound(new { message = "Auto-discovery is disabled" });
        }

        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);

        var dataSourceId = options.DataSourceId;
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            var postGisDs = snapshot.DataSources.FirstOrDefault(ds =>
                string.Equals(ds.Provider, "postgis", StringComparison.OrdinalIgnoreCase));

            if (postGisDs == null)
            {
                return Results.NotFound(new { message = "No PostGIS data sources configured" });
            }

            dataSourceId = postGisDs.Id;
        }

        var qualifiedName = $"{schema}.{table}";
        var discoveredTable = await discoveryService.DiscoverTableAsync(dataSourceId, qualifiedName, cancellationToken);

        if (discoveredTable == null)
        {
            return Results.NotFound(new { message = $"Table {qualifiedName} not found or not discoverable" });
        }

        return Results.Ok(discoveredTable);
    }

    private static async Task<IResult> RefreshDiscoveryCache(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var discoveryService = context.RequestServices.GetService<ITableDiscoveryService>();
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Honua.Server.Host.Discovery.DiscoveryAdminEndpoints");

        if (discoveryService is CachedTableDiscoveryService cachedService)
        {
            var options = context.RequestServices.GetRequiredService<IOptions<AutoDiscoveryOptions>>().Value;
            var metadataRegistry = context.RequestServices.GetRequiredService<IMetadataRegistry>();

            var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);

            var dataSourceId = options.DataSourceId;
            if (string.IsNullOrWhiteSpace(dataSourceId))
            {
                var postGisDs = snapshot.DataSources.FirstOrDefault(ds =>
                    string.Equals(ds.Provider, "postgis", StringComparison.OrdinalIgnoreCase));

                if (postGisDs != null)
                {
                    dataSourceId = postGisDs.Id;
                }
            }

            if (!string.IsNullOrWhiteSpace(dataSourceId))
            {
                // Clear cache
                cachedService.ClearCache(dataSourceId);

                // Trigger new discovery
                var tables = await discoveryService.DiscoverTablesAsync(dataSourceId, cancellationToken);

                logger.LogInformation("Discovery cache refreshed for data source {DataSourceId}", dataSourceId);

                return Results.Ok(new RefreshResult
                {
                    Success = true,
                    Message = "Cache refreshed successfully",
                    TablesDiscovered = tables.Count(),
                    DataSourceId = dataSourceId
                });
            }

            return Results.Ok(new RefreshResult
            {
                Success = false,
                Message = "No PostGIS data source found to refresh"
            });
        }

        return Results.Ok(new RefreshResult
        {
            Success = false,
            Message = "Caching is not enabled for discovery service"
        });
    }

    private static IResult ClearDiscoveryCache(HttpContext context)
    {
        var discoveryService = context.RequestServices.GetService<ITableDiscoveryService>();
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Honua.Server.Host.Discovery.DiscoveryAdminEndpoints");

        if (discoveryService is CachedTableDiscoveryService cachedService)
        {
            cachedService.ClearAllCaches();

            logger.LogInformation("All discovery caches cleared");

            return Results.Ok(new ClearCacheResult
            {
                Success = true,
                Message = "All discovery caches cleared"
            });
        }

        return Results.Ok(new ClearCacheResult
        {
            Success = false,
            Message = "Caching is not enabled for discovery service"
        });
    }
}

// Response DTOs

public sealed class DiscoveryStatus
{
    public bool Enabled { get; init; }
    public bool ODataDiscoveryEnabled { get; init; }
    public bool OgcDiscoveryEnabled { get; init; }
    public TimeSpan CacheDuration { get; init; }
    public int MaxTables { get; init; }
    public bool RequireSpatialIndex { get; init; }
    public int PostGisDataSourceCount { get; init; }
    public string? ConfiguredDataSourceId { get; init; }
    public bool BackgroundRefreshEnabled { get; init; }
    public TimeSpan BackgroundRefreshInterval { get; init; }
}

public sealed class DiscoveredTablesResponse
{
    public required IReadOnlyList<DiscoveredTable> Tables { get; init; }
    public int TotalCount { get; init; }
    public string? DataSourceId { get; init; }
    public string? Message { get; init; }
}

public sealed class RefreshResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public int TablesDiscovered { get; init; }
    public string? DataSourceId { get; init; }
}

public sealed class ClearCacheResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
}
