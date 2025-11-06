// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            .WithOpenApi();

        // TODO: Add authorization after auth integration
        // .RequireAuthorization("RequireAdministrator")
        // .RequireRateLimiting("admin-operations");

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
}
