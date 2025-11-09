// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin REST API endpoints for metadata administration (services, layers, folders).
/// This is the main partial class file that contains the routing setup and dashboard endpoints.
/// </summary>
public static partial class MetadataAdministrationEndpoints
{
    /// <summary>
    /// Maps all admin metadata endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAdminMetadataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/metadata")
            .WithTags("Admin - Metadata")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // Dashboard
        group.MapGet("/stats", GetDashboardStats)
            .WithName("GetDashboardStats")
            .WithSummary("Get dashboard statistics");

        // Services
        group.MapGet("/services", GetServices)
            .WithName("GetServices")
            .WithSummary("List all services");

        group.MapGet("/services/{id}", GetServiceById)
            .WithName("GetServiceById")
            .WithSummary("Get service by ID");

        group.MapPost("/services", CreateService)
            .WithName("CreateService")
            .WithSummary("Create a new service");

        group.MapPut("/services/{id}", UpdateService)
            .WithName("UpdateService")
            .WithSummary("Update an existing service");

        group.MapDelete("/services/{id}", DeleteService)
            .WithName("DeleteService")
            .WithSummary("Delete a service");

        group.MapPost("/services/{id}/enable", EnableService)
            .WithName("EnableService")
            .WithSummary("Enable a service");

        group.MapPost("/services/{id}/disable", DisableService)
            .WithName("DisableService")
            .WithSummary("Disable a service");

        group.MapGet("/services/{id}/connection/{type}", GetServiceConnectionFile)
            .WithName("GetServiceConnectionFile")
            .WithSummary("Download connection file for GIS applications");

        // Layers
        group.MapGet("/layers", GetLayers)
            .WithName("GetLayers")
            .WithSummary("List all layers");

        group.MapGet("/layers/{id}", GetLayerById)
            .WithName("GetLayerById")
            .WithSummary("Get layer by ID");

        group.MapPost("/layers", CreateLayer)
            .WithName("CreateLayer")
            .WithSummary("Create a new layer");

        group.MapPut("/layers/{id}", UpdateLayer)
            .WithName("UpdateLayer")
            .WithSummary("Update an existing layer");

        group.MapDelete("/layers/{id}", DeleteLayer)
            .WithName("DeleteLayer")
            .WithSummary("Delete a layer");

        // SQL Views for Layers
        group.MapGet("/layers/{layerId}/sqlview", GetLayerSqlView)
            .WithName("GetLayerSqlView")
            .WithSummary("Get SQL View configuration for a layer");

        group.MapPut("/layers/{layerId}/sqlview", UpdateLayerSqlView)
            .WithName("UpdateLayerSqlView")
            .WithSummary("Update SQL View configuration for a layer");

        group.MapPost("/layers/{layerId}/sqlview/test", TestSqlQuery)
            .WithName("TestSqlQuery")
            .WithSummary("Test a SQL query with sample parameters");

        group.MapPost("/layers/{layerId}/sqlview/detect-schema", DetectSchemaFromSql)
            .WithName("DetectSchemaFromSql")
            .WithSummary("Detect schema from a SQL query");

        // Folders
        group.MapGet("/folders", GetFolders)
            .WithName("GetFolders")
            .WithSummary("List all folders");

        group.MapPost("/folders", CreateFolder)
            .WithName("CreateFolder")
            .WithSummary("Create a new folder");

        group.MapPut("/folders/{id}", UpdateFolder)
            .WithName("UpdateFolder")
            .WithSummary("Update an existing folder");

        group.MapDelete("/folders/{id}", DeleteFolder)
            .WithName("DeleteFolder")
            .WithSummary("Delete a folder");

        // Data Sources
        group.MapGet("/datasources", GetDataSources)
            .WithName("GetDataSources")
            .WithSummary("List all data sources");

        group.MapGet("/datasources/{id}", GetDataSourceById)
            .WithName("GetDataSourceById")
            .WithSummary("Get data source by ID");

        group.MapPost("/datasources", CreateDataSource)
            .WithName("CreateDataSource")
            .WithSummary("Create a new data source");

        group.MapPut("/datasources/{id}", UpdateDataSource)
            .WithName("UpdateDataSource")
            .WithSummary("Update an existing data source");

        group.MapDelete("/datasources/{id}", DeleteDataSource)
            .WithName("DeleteDataSource")
            .WithSummary("Delete a data source");

        group.MapPost("/datasources/{id}/test", TestDataSourceConnection)
            .WithName("TestDataSourceConnection")
            .WithSummary("Test connection to a data source");

        group.MapGet("/datasources/{id}/tables", GetDataSourceTables)
            .WithName("GetDataSourceTables")
            .WithSummary("Discover tables in a data source");

        return group;
    }

    #region Dashboard

    private static async Task<IResult> GetDashboardStats(
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var stats = new DashboardStatsResponse
        {
            ServiceCount = snapshot.Services.Count,
            LayerCount = snapshot.Layers.Count,
            FolderCount = snapshot.Folders.Count,
            DataSourceCount = snapshot.DataSources.Count,
            SupportsVersioning = metadataProvider.SupportsVersioning,
            SupportsRealTimeUpdates = metadataProvider.SupportsChangeNotifications
        };

        return Results.Ok(stats);
    }

    #endregion
}
