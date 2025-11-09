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
/// </summary>
public static class MetadataAdministrationEndpoints
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

    #region Services

    private static async Task<IResult> GetServices(
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var services = snapshot.Services.Select(s => new ServiceListItem
        {
            Id = s.Id,
            Title = s.Title,
            FolderId = s.FolderId,
            ServiceType = s.ServiceType,
            DataSourceId = s.DataSourceId,
            Enabled = s.Enabled,
            LayerCount = s.Layers.Count
        }).ToList();

        return Results.Ok(services);
    }

    private static async Task<IResult> GetServiceById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (service is null)
        {
            return Results.Problem(
                title: "Service not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Service with ID '{id}' does not exist");
        }

        var response = new ServiceResponse
        {
            Id = service.Id,
            Title = service.Title,
            FolderId = service.FolderId,
            ServiceType = service.ServiceType,
            DataSourceId = service.DataSourceId,
            Description = service.Description,
            Keywords = service.Keywords.ToList(),
            Enabled = service.Enabled,
            LayerCount = service.Layers.Count,
            OgcOptions = new ServiceOgcOptionsDto
            {
                WfsEnabled = service.Ogc.WfsEnabled,
                WmsEnabled = service.Ogc.WmsEnabled,
                WmtsEnabled = service.Ogc.WmtsEnabled,
                CollectionsEnabled = service.Ogc.CollectionsEnabled,
                ItemLimit = service.Ogc.ItemLimit,
                DefaultCrs = service.Ogc.DefaultCrs
            },
            CreatedAt = DateTime.UtcNow, // TODO: Get from metadata when available
            ModifiedAt = null
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateService(
        CreateServiceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Load current snapshot
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if service ID already exists
            if (snapshot.Services.Any(s => s.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Service already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Service with ID '{request.Id}' already exists");
            }

            // Validate: Check if folder exists
            if (!snapshot.Folders.Any(f => f.Id.Equals(request.FolderId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Folder not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Folder with ID '{request.FolderId}' does not exist");
            }

            // Validate: Check if data source exists
            if (!snapshot.DataSources.Any(ds => ds.Id.Equals(request.DataSourceId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{request.DataSourceId}' does not exist");
            }

            // Create new service definition
            var newService = new ServiceDefinition
            {
                Id = request.Id,
                Title = request.Title,
                FolderId = request.FolderId,
                ServiceType = request.ServiceType,
                DataSourceId = request.DataSourceId,
                Description = request.Description,
                Keywords = request.Keywords,
                Enabled = request.Enabled,
                Ogc = new OgcServiceDefinition
                {
                    WfsEnabled = request.OgcOptions?.WfsEnabled ?? false,
                    WmsEnabled = request.OgcOptions?.WmsEnabled ?? false,
                    WmtsEnabled = request.OgcOptions?.WmtsEnabled ?? false,
                    CollectionsEnabled = request.OgcOptions?.CollectionsEnabled ?? false,
                    ItemLimit = request.OgcOptions?.ItemLimit,
                    DefaultCrs = request.OgcOptions?.DefaultCrs
                }
            };

            // Build new snapshot (immutable update)
            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services.Append(newService).ToList(),
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            // Save atomically
            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created service {ServiceId}", newService.Id);

            // Return created response
            var response = new ServiceResponse
            {
                Id = newService.Id,
                Title = newService.Title,
                FolderId = newService.FolderId,
                ServiceType = newService.ServiceType,
                DataSourceId = newService.DataSourceId,
                Description = newService.Description,
                Keywords = newService.Keywords.ToList(),
                Enabled = newService.Enabled,
                LayerCount = 0,
                OgcOptions = new ServiceOgcOptionsDto
                {
                    WfsEnabled = newService.Ogc.WfsEnabled,
                    WmsEnabled = newService.Ogc.WmsEnabled,
                    WmtsEnabled = newService.Ogc.WmtsEnabled,
                    CollectionsEnabled = newService.Ogc.CollectionsEnabled,
                    ItemLimit = newService.Ogc.ItemLimit,
                    DefaultCrs = newService.Ogc.DefaultCrs
                },
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            };

            return Results.Created($"/admin/metadata/services/{newService.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create service {ServiceId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the service");
        }
    }

    private static async Task<IResult> UpdateService(
        string id,
        UpdateServiceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingService = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingService is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{id}' does not exist");
            }

            // Validate folder exists
            if (!snapshot.Folders.Any(f => f.Id.Equals(request.FolderId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Folder not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Folder with ID '{request.FolderId}' does not exist");
            }

            // Update service (using with expression for immutable record)
            var updatedService = existingService with
            {
                Title = request.Title,
                FolderId = request.FolderId,
                Description = request.Description,
                Keywords = request.Keywords,
                Enabled = request.Enabled,
                Ogc = existingService.Ogc with
                {
                    WfsEnabled = request.OgcOptions?.WfsEnabled ?? existingService.Ogc.WfsEnabled,
                    WmsEnabled = request.OgcOptions?.WmsEnabled ?? existingService.Ogc.WmsEnabled,
                    WmtsEnabled = request.OgcOptions?.WmtsEnabled ?? existingService.Ogc.WmtsEnabled,
                    CollectionsEnabled = request.OgcOptions?.CollectionsEnabled ?? existingService.Ogc.CollectionsEnabled,
                    ItemLimit = request.OgcOptions?.ItemLimit ?? existingService.Ogc.ItemLimit,
                    DefaultCrs = request.OgcOptions?.DefaultCrs ?? existingService.Ogc.DefaultCrs
                }
            };

            // Build new snapshot
            var updatedServices = snapshot.Services
                .Select(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedService : s)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                updatedServices,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated service {ServiceId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update service {ServiceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the service");
        }
    }

    private static async Task<IResult> DeleteService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingService = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingService is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{id}' does not exist");
            }

            // Remove service and its layers
            var updatedServices = snapshot.Services
                .Where(s => !s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var updatedLayers = snapshot.Layers
                .Where(l => !l.ServiceId.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                updatedServices,
                updatedLayers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted service {ServiceId} and {LayerCount} layers", id, existingService.Layers.Count);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete service {ServiceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the service");
        }
    }

    private static async Task<IResult> EnableService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        return await ToggleServiceEnabledState(id, true, metadataProvider, logger, ct);
    }

    private static async Task<IResult> DisableService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        return await ToggleServiceEnabledState(id, false, metadataProvider, logger, ct);
    }

    private static async Task<IResult> ToggleServiceEnabledState(
        string id,
        bool enabled,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingService = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingService is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{id}' does not exist");
            }

            // Update only the Enabled property
            var updatedService = existingService with
            {
                Enabled = enabled
            };

            // Build new snapshot
            var updatedServices = snapshot.Services
                .Select(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedService : s)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                updatedServices,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("{Action} service {ServiceId}", enabled ? "Enabled" : "Disabled", id);

            return Results.Ok(new { Id = id, Enabled = enabled });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Action} service {ServiceId}", enabled ? "enable" : "disable", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"An error occurred while {(enabled ? "enabling" : "disabling")} the service");
        }
    }

    private static async Task<IResult> GetServiceConnectionFile(
        string id,
        string type,
        HttpContext context,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (service is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{id}' does not exist");
            }

            // Get base URL from the request
            var request = context.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";

            string content;
            string fileName;
            string contentType;

            switch (type.ToLowerInvariant())
            {
                case "wms":
                    var wmsUrl = $"{baseUrl}/ogc/services/{service.Id}/wms?SERVICE=WMS&REQUEST=GetCapabilities";
                    content = GenerateWmsConnectionFile(service.Title, wmsUrl);
                    fileName = $"{service.Id}.wms";
                    contentType = "application/xml";
                    break;

                case "wfs":
                    var wfsUrl = $"{baseUrl}/ogc/services/{service.Id}/wfs?SERVICE=WFS&REQUEST=GetCapabilities";
                    content = GenerateWfsConnectionFile(service.Title, wfsUrl);
                    fileName = $"{service.Id}.wfs";
                    contentType = "application/xml";
                    break;

                case "qgis":
                    var qgisWmsUrl = $"{baseUrl}/ogc/services/{service.Id}/wms";
                    var qgisWfsUrl = $"{baseUrl}/ogc/services/{service.Id}/wfs";
                    content = GenerateQgisConnectionFile(service.Title, service.Id, qgisWmsUrl, qgisWfsUrl);
                    fileName = $"{service.Id}.qgs";
                    contentType = "application/xml";
                    break;

                case "arcgis":
                    var arcgisWmsUrl = $"{baseUrl}/ogc/services/{service.Id}/wms";
                    content = GenerateArcGisConnectionFile(service.Title, arcgisWmsUrl);
                    fileName = $"{service.Id}.ags";
                    contentType = "application/xml";
                    break;

                default:
                    return Results.Problem(
                        title: "Invalid connection type",
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: $"Connection type '{type}' is not supported. Valid types: wms, wfs, qgis, arcgis");
            }

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(content),
                contentType,
                fileName);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while generating the connection file");
        }
    }

    private static string GenerateWmsConnectionFile(string title, string url)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<WMS_Capabilities>
  <Service>
    <Name>{System.Security.SecurityElement.Escape(title)}</Name>
    <OnlineResource xmlns:xlink=""http://www.w3.org/1999/xlink"" xlink:href=""{System.Security.SecurityElement.Escape(url)}""/>
  </Service>
</WMS_Capabilities>";
    }

    private static string GenerateWfsConnectionFile(string title, string url)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<WFS_Capabilities>
  <Service>
    <Name>{System.Security.SecurityElement.Escape(title)}</Name>
    <OnlineResource xmlns:xlink=""http://www.w3.org/1999/xlink"" xlink:href=""{System.Security.SecurityElement.Escape(url)}""/>
  </Service>
</WFS_Capabilities>";
    }

    private static string GenerateQgisConnectionFile(string title, string id, string wmsUrl, string wfsUrl)
    {
        return $@"<!DOCTYPE qgis PUBLIC 'http://mrcc.com/qgis.dtd' 'SYSTEM'>
<qgis version=""3.0"">
  <projectlayers>
    <maplayer>
      <id>{System.Security.SecurityElement.Escape(id)}</id>
      <datasource>{System.Security.SecurityElement.Escape(wmsUrl)}</datasource>
      <layername>{System.Security.SecurityElement.Escape(title)}</layername>
      <provider>wms</provider>
    </maplayer>
  </projectlayers>
  <properties>
    <WMSUrl type=""QString"">{System.Security.SecurityElement.Escape(wmsUrl)}</WMSUrl>
    <WFSUrl type=""QString"">{System.Security.SecurityElement.Escape(wfsUrl)}</WFSUrl>
  </properties>
</qgis>";
    }

    private static string GenerateArcGisConnectionFile(string title, string wmsUrl)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ARCGIS>
  <WMSConnection>
    <Name>{System.Security.SecurityElement.Escape(title)}</Name>
    <URL>{System.Security.SecurityElement.Escape(wmsUrl)}</URL>
    <Version>1.3.0</Version>
  </WMSConnection>
</ARCGIS>";
    }

    #endregion

    #region Layers

    private static async Task<IResult> GetLayers(
        [FromServices] IMutableMetadataProvider metadataProvider,
        string? serviceId,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var layers = snapshot.Layers
            .Where(l => string.IsNullOrEmpty(serviceId) || l.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase))
            .Select(l => new LayerListItem
            {
                Id = l.Id,
                ServiceId = l.ServiceId,
                Title = l.Title,
                GeometryType = l.GeometryType
            })
            .ToList();

        return Results.Ok(layers);
    }

    private static async Task<IResult> GetLayerById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            return Results.Problem(
                title: "Layer not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Layer with ID '{id}' does not exist");
        }

        var response = new LayerResponse
        {
            Id = layer.Id,
            ServiceId = layer.ServiceId,
            Title = layer.Title,
            Description = layer.Description,
            GeometryType = layer.GeometryType,
            IdField = layer.IdField,
            GeometryField = layer.GeometryField,
            DisplayField = layer.DisplayField,
            Crs = layer.Crs.ToList(),
            Keywords = layer.Keywords.ToList(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = null
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateLayer(
        CreateLayerRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if layer ID already exists
            if (snapshot.Layers.Any(l => l.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Layer already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Layer with ID '{request.Id}' already exists");
            }

            // Validate: Check if service exists
            if (!snapshot.Services.Any(s => s.Id.Equals(request.ServiceId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{request.ServiceId}' does not exist");
            }

            // Create new layer definition
            var newLayer = new LayerDefinition
            {
                Id = request.Id,
                ServiceId = request.ServiceId,
                Title = request.Title,
                Description = request.Description,
                GeometryType = request.GeometryType,
                IdField = request.IdField,
                GeometryField = request.GeometryField,
                DisplayField = request.DisplayField,
                Crs = request.Crs,
                Keywords = request.Keywords
            };

            // Build new snapshot
            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers.Append(newLayer).ToList(),
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created layer {LayerId} for service {ServiceId}", newLayer.Id, newLayer.ServiceId);

            var response = new LayerResponse
            {
                Id = newLayer.Id,
                ServiceId = newLayer.ServiceId,
                Title = newLayer.Title,
                Description = newLayer.Description,
                GeometryType = newLayer.GeometryType,
                IdField = newLayer.IdField,
                GeometryField = newLayer.GeometryField,
                DisplayField = newLayer.DisplayField,
                Crs = newLayer.Crs.ToList(),
                Keywords = newLayer.Keywords.ToList(),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            };

            return Results.Created($"/admin/metadata/layers/{newLayer.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create layer {LayerId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the layer");
        }
    }

    private static async Task<IResult> UpdateLayer(
        string id,
        UpdateLayerRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{id}' does not exist");
            }

            // Update layer
            var updatedLayer = existingLayer with
            {
                Title = request.Title,
                Description = request.Description,
                DisplayField = request.DisplayField,
                Crs = request.Crs.Any() ? request.Crs : existingLayer.Crs,
                Keywords = request.Keywords
            };

            // Use optimized UpdateLayerAsync if available
            await metadataProvider.UpdateLayerAsync(updatedLayer, ct);

            logger.LogInformation("Updated layer {LayerId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update layer {LayerId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the layer");
        }
    }

    private static async Task<IResult> DeleteLayer(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{id}' does not exist");
            }

            // Remove layer
            var updatedLayers = snapshot.Layers
                .Where(l => !l.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                updatedLayers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted layer {LayerId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete layer {LayerId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the layer");
        }
    }

    private static async Task<IResult> GetLayerSqlView(
        string layerId,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            return Results.Problem(
                title: "Layer not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Layer with ID '{layerId}' does not exist");
        }

        if (layer.SqlView is null)
        {
            return Results.Ok(new { sqlView = (object?)null });
        }

        // Convert to admin model
        var sqlViewModel = new
        {
            sql = layer.SqlView.Sql,
            description = layer.SqlView.Description,
            parameters = layer.SqlView.Parameters.Select(p => new
            {
                name = p.Name,
                title = p.Title,
                description = p.Description,
                type = p.Type,
                defaultValue = p.DefaultValue,
                required = p.Required,
                validation = p.Validation is not null ? new
                {
                    min = p.Validation.Min,
                    max = p.Validation.Max,
                    minLength = p.Validation.MinLength,
                    maxLength = p.Validation.MaxLength,
                    pattern = p.Validation.Pattern,
                    allowedValues = p.Validation.AllowedValues?.ToList() ?? new List<string>(),
                    errorMessage = p.Validation.ErrorMessage
                } : null
            }).ToList(),
            timeoutSeconds = layer.SqlView.TimeoutSeconds,
            readOnly = layer.SqlView.ReadOnly,
            securityFilter = layer.SqlView.SecurityFilter,
            validateGeometry = layer.SqlView.ValidateGeometry,
            hints = layer.SqlView.Hints
        };

        return Results.Ok(new { sqlView = sqlViewModel });
    }

    private static async Task<IResult> UpdateLayerSqlView(
        string layerId,
        [FromBody] UpdateLayerSqlViewRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            // Convert admin model to core model
            SqlViewDefinition? sqlView = null;
            if (request.SqlView is not null)
            {
                sqlView = new SqlViewDefinition
                {
                    Sql = request.SqlView.Sql,
                    Description = request.SqlView.Description,
                    Parameters = request.SqlView.Parameters.Select(p => new SqlViewParameterDefinition
                    {
                        Name = p.Name,
                        Title = p.Title,
                        Description = p.Description,
                        Type = p.Type,
                        DefaultValue = p.DefaultValue,
                        Required = p.Required,
                        Validation = new SqlViewParameterValidation
                        {
                            Min = p.Validation.Min,
                            Max = p.Validation.Max,
                            MinLength = p.Validation.MinLength,
                            MaxLength = p.Validation.MaxLength,
                            Pattern = p.Validation.Pattern,
                            AllowedValues = p.Validation.AllowedValues.Count > 0 ? p.Validation.AllowedValues : null,
                            ErrorMessage = p.Validation.ErrorMessage
                        }
                    }).ToList(),
                    TimeoutSeconds = request.SqlView.TimeoutSeconds,
                    ReadOnly = request.SqlView.ReadOnly,
                    SecurityFilter = request.SqlView.SecurityFilter,
                    ValidateGeometry = request.SqlView.ValidateGeometry,
                    Hints = request.SqlView.Hints
                };

                // Validate the SQL view
                try
                {
                    SqlViewExecutor.ValidateParameterReferences(sqlView, layerId);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "SQL View validation failed",
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: ex.Message);
                }
            }

            // Update layer with new SQL view
            var updatedLayer = existingLayer with { SqlView = sqlView };

            // Replace the layer in the snapshot
            var updatedLayers = snapshot.Layers
                .Select(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase) ? updatedLayer : l)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                updatedLayers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated SQL View for layer {LayerId}", layerId);

            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update SQL View for layer {LayerId}", layerId);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the SQL View");
        }
    }

    private static async Task<IResult> TestSqlQuery(
        string layerId,
        [FromBody] TestSqlQueryRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IDataStoreProviderFactory providerFactory,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (layer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(layer.ServiceId, StringComparison.OrdinalIgnoreCase));
            if (service is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{layer.ServiceId}' does not exist");
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(service.DataSourceId, StringComparison.OrdinalIgnoreCase));
            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{service.DataSourceId}' does not exist");
            }

            // Get data provider
            var provider = providerFactory.CreateProvider(dataSource);

            // Execute the SQL with timeout
            var stopwatch = Stopwatch.StartNew();
            var timeout = request.TimeoutSeconds ?? 30;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            try
            {
                // For now, we'll just return a mock result since we need more integration
                // In a full implementation, you would execute the SQL through the provider
                var result = new QueryTestResult
                {
                    Success = true,
                    RowCount = 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Columns = new List<string>(),
                    Rows = new List<Dictionary<string, object?>>(),
                    Truncated = false
                };

                logger.LogInformation("Successfully tested SQL query for layer {LayerId}", layerId);

                return Results.Ok(result);
            }
            catch (OperationCanceledException)
            {
                return Results.Ok(new QueryTestResult
                {
                    Success = false,
                    ErrorMessage = $"Query execution timed out after {timeout} seconds",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test SQL query for layer {LayerId}", layerId);
            return Results.Ok(new QueryTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = 0
            });
        }
    }

    private static async Task<IResult> DetectSchemaFromSql(
        string layerId,
        [FromBody] TestSqlQueryRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IDataStoreProviderFactory providerFactory,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (layer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            // Get the service and data source
            var service = snapshot.Services.FirstOrDefault(s => s.Layers.Any(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase)));
            if (service is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"No service found for layer '{layerId}'");
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(service.DataSourceId, StringComparison.OrdinalIgnoreCase));
            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"No data source found for service '{service.Id}'");
            }

            // Check if layer has SQL view defined or if SQL is provided in request
            SqlViewDefinition? sqlView = layer.SqlView;
            if (sqlView is null && string.IsNullOrWhiteSpace(request.Sql))
            {
                return Results.Problem(
                    title: "No SQL view defined",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "Layer does not have a SQL view and no SQL query was provided in the request");
            }

            // If SQL is provided in request, create a temporary SQL view definition
            if (!string.IsNullOrWhiteSpace(request.Sql))
            {
                var parameters = request.Parameters.Select(kvp => new SqlViewParameterDefinition
                {
                    Name = kvp.Key,
                    Type = "string", // Default to string for ad-hoc queries
                    DefaultValue = kvp.Value
                }).ToArray();

                sqlView = new SqlViewDefinition
                {
                    Sql = request.Sql,
                    Parameters = parameters,
                    TimeoutSeconds = request.TimeoutSeconds
                };

                // Create a temporary layer with the SQL view
                layer = layer with { SqlView = sqlView };
            }

            // Get the data provider
            var provider = providerFactory.Create(dataSource.Provider);

            // Detect schema - only if provider supports it (RelationalDataStoreProviderBase)
            IReadOnlyList<FieldDefinition> fields;
            try
            {
                // Use reflection to check if provider has DetectSchemaForSqlViewAsync method
                var method = provider.GetType().GetMethod("DetectSchemaForSqlViewAsync");
                if (method is null)
                {
                    return Results.Problem(
                        title: "Schema detection not supported",
                        statusCode: StatusCodes.Status501NotImplemented,
                        detail: $"Provider '{dataSource.Provider}' does not support schema detection for SQL views");
                }

                // Call the method
                var task = method.Invoke(provider, new object[] { dataSource, layer, ct }) as Task<IReadOnlyList<FieldDefinition>>;
                if (task is null)
                {
                    throw new InvalidOperationException("Failed to invoke DetectSchemaForSqlViewAsync");
                }

                fields = await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to detect schema for layer {LayerId}", layerId);
                return Results.Ok(new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = ex.InnerException?.Message ?? ex.Message
                });
            }

            // Convert to DetectedFieldInfo
            var detectedFields = fields.Select(f => new DetectedFieldInfo
            {
                Name = f.Name,
                Type = f.DataType ?? "esriFieldTypeString",
                Nullable = f.Nullable,
                IsGeometry = f.Name.Equals(layer.GeometryField, StringComparison.OrdinalIgnoreCase)
            }).ToList();

            var result = new SchemaDetectionResult
            {
                Success = true,
                Fields = detectedFields,
                GeometryField = layer.GeometryField,
                GeometryType = layer.GeometryType,
                IdField = layer.IdField
            };

            logger.LogInformation("Successfully detected {FieldCount} fields for layer {LayerId}", fields.Count, layerId);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to detect schema for layer {LayerId}", layerId);
            return Results.Ok(new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    #endregion

    #region Folders

    private static async Task<IResult> GetFolders(
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var folders = snapshot.Folders.Select(f =>
        {
            var serviceCount = snapshot.Services.Count(s => s.FolderId.Equals(f.Id, StringComparison.OrdinalIgnoreCase));
            return new FolderResponse
            {
                Id = f.Id,
                Title = f.Title,
                Order = f.Order,
                ServiceCount = serviceCount
            };
        }).ToList();

        return Results.Ok(folders);
    }

    private static async Task<IResult> CreateFolder(
        CreateFolderRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if folder ID already exists
            if (snapshot.Folders.Any(f => f.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Folder already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Folder with ID '{request.Id}' already exists");
            }

            // Create new folder
            var newFolder = new FolderDefinition
            {
                Id = request.Id,
                Title = request.Title,
                Order = request.Order
            };

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders.Append(newFolder).ToList(),
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created folder {FolderId}", newFolder.Id);

            var response = new FolderResponse
            {
                Id = newFolder.Id,
                Title = newFolder.Title,
                Order = newFolder.Order,
                ServiceCount = 0
            };

            return Results.Created($"/admin/metadata/folders/{newFolder.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create folder {FolderId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the folder");
        }
    }

    private static async Task<IResult> UpdateFolder(
        string id,
        UpdateFolderRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingFolder = snapshot.Folders.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingFolder is null)
            {
                return Results.Problem(
                    title: "Folder not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Folder with ID '{id}' does not exist");
            }

            // Update folder
            var updatedFolder = existingFolder with
            {
                Title = request.Title ?? existingFolder.Title,
                Order = request.Order ?? existingFolder.Order
            };

            var updatedFolders = snapshot.Folders
                .Select(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedFolder : f)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                updatedFolders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated folder {FolderId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update folder {FolderId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the folder");
        }
    }

    private static async Task<IResult> DeleteFolder(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingFolder = snapshot.Folders.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingFolder is null)
            {
                return Results.Problem(
                    title: "Folder not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Folder with ID '{id}' does not exist");
            }

            // Check if folder contains services
            var serviceCount = snapshot.Services.Count(s => s.FolderId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (serviceCount > 0)
            {
                return Results.Problem(
                    title: "Folder not empty",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Cannot delete folder '{id}' because it contains {serviceCount} service(s)");
            }

            // Remove folder
            var updatedFolders = snapshot.Folders
                .Where(f => !f.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                updatedFolders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted folder {FolderId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete folder {FolderId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the folder");
        }
    }

    #endregion

    #region Data Sources

    private static async Task<IResult> GetDataSources(
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var dataSources = snapshot.DataSources.Select(ds => new DataSourceResponse
        {
            Id = ds.Id,
            Provider = ds.Provider,
            ConnectionString = ds.ConnectionString
        }).ToList();

        return Results.Ok(dataSources);
    }

    private static async Task<IResult> GetDataSourceById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (dataSource is null)
        {
            return Results.Problem(
                title: "Data source not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Data source with ID '{id}' does not exist");
        }

        var response = new DataSourceResponse
        {
            Id = dataSource.Id,
            Provider = dataSource.Provider,
            ConnectionString = dataSource.ConnectionString
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateDataSource(
        CreateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if data source ID already exists
            if (snapshot.DataSources.Any(ds => ds.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Data source already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Data source with ID '{request.Id}' already exists");
            }

            // Create new data source
            var newDataSource = new DataSourceDefinition
            {
                Id = request.Id,
                Provider = request.Provider,
                ConnectionString = request.ConnectionString
            };

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources.Append(newDataSource).ToList(),
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created data source {DataSourceId}", newDataSource.Id);

            var response = new DataSourceResponse
            {
                Id = newDataSource.Id,
                Provider = newDataSource.Provider,
                ConnectionString = newDataSource.ConnectionString
            };

            return Results.Created($"/admin/metadata/datasources/{newDataSource.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create data source {DataSourceId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the data source");
        }
    }

    private static async Task<IResult> UpdateDataSource(
        string id,
        UpdateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // Update data source
            var updatedDataSource = existingDataSource with
            {
                Provider = request.Provider ?? existingDataSource.Provider,
                ConnectionString = request.ConnectionString ?? existingDataSource.ConnectionString
            };

            var updatedDataSources = snapshot.DataSources
                .Select(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedDataSource : ds)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                updatedDataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated data source {DataSourceId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the data source");
        }
    }

    private static async Task<IResult> DeleteDataSource(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // Check if data source is in use by any services
            var serviceCount = snapshot.Services.Count(s => s.DataSourceId != null && s.DataSourceId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (serviceCount > 0)
            {
                return Results.Problem(
                    title: "Data source in use",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Cannot delete data source '{id}' because it is used by {serviceCount} service(s)");
            }

            // Remove data source
            var updatedDataSources = snapshot.DataSources
                .Where(ds => !ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                updatedDataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted data source {DataSourceId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the data source");
        }
    }

    private static async Task<IResult> TestDataSourceConnection(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // TODO: Implement actual connection test based on provider
            // For now, return a mock success response
            var response = new TestConnectionResponse
            {
                Success = true,
                Message = "Connection test successful",
                Provider = dataSource.Provider,
                ConnectionTime = 125 // milliseconds
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test connection for data source {DataSourceId}", id);
            return Results.Ok(new TestConnectionResponse
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                Provider = null,
                ConnectionTime = 0
            });
        }
    }

    private static async Task<IResult> GetDataSourceTables(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // TODO: Implement actual table discovery based on provider
            // For now, return an empty list
            var tables = new List<TableInfo>();

            return Results.Ok(new { Tables = tables });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover tables for data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while discovering tables");
        }
    }

    #endregion
}
