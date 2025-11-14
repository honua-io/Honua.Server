// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Partial class containing service-related admin endpoints.
/// Handles CRUD operations for services and connection file generation for GIS applications.
/// </summary>
public static partial class MetadataAdministrationEndpoints
{
    #region Services

    private static async Task<IResult> GetServices(
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetServices",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

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

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "Service",
                resourceId: "list",
                operation: "Read");

            return Results.Ok(services);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetServices",
                resourceType: "Service",
                details: "Failed to retrieve services",
                exception: ex);

            throw;
        }
    }

    private static async Task<IResult> GetServiceById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetServiceById",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid service ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (service is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "GetServiceById",
                    resourceType: "Service",
                    resourceId: id,
                    details: "Service not found");

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
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            };

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "Service",
                resourceId: id,
                operation: "Read");

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetServiceById",
                resourceType: "Service",
                resourceId: id,
                details: "Failed to retrieve service",
                exception: ex);

            throw;
        }
    }

    private static async Task<IResult> CreateService(
        CreateServiceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "CreateService",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Id, nameof(request.Id));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Title, nameof(request.Title));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Id, minLength: 1, maxLength: 100))
            {
                return Results.BadRequest("Service ID must be between 1 and 100 characters");
            }

            if (!InputValidationHelpers.IsValidLength(request.Title, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Service title must be between 1 and 200 characters");
            }

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            // Save atomically
            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} created service {ServiceId}", identity.UserId, newService.Id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "CreateService",
                resourceType: "Service",
                resourceId: newService.Id,
                details: $"Created service: {newService.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceId"] = newService.Id,
                    ["serviceTitle"] = newService.Title,
                    ["serviceType"] = newService.ServiceType,
                    ["folderId"] = newService.FolderId,
                    ["dataSourceId"] = newService.DataSourceId,
                    ["enabled"] = newService.Enabled
                });

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "CreateService",
                resourceType: "Service",
                resourceId: request.Id,
                details: "Failed to create service",
                exception: ex);

            logger.LogError(ex, "Failed to create service {ServiceId}", request.Id);
            throw;
        }
    }

    private static async Task<IResult> UpdateService(
        string id,
        UpdateServiceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateService",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid service ID format");
            }

            InputValidationHelpers.ThrowIfUnsafeInput(request.Title, nameof(request.Title));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

            if (!InputValidationHelpers.IsValidLength(request.Title, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Service title must be between 1 and 200 characters");
            }

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} updated service {ServiceId}", identity.UserId, id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateService",
                resourceType: "Service",
                resourceId: id,
                details: $"Updated service: {updatedService.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceId"] = id,
                    ["serviceTitle"] = updatedService.Title,
                    ["folderId"] = updatedService.FolderId,
                    ["enabled"] = updatedService.Enabled
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateService",
                resourceType: "Service",
                resourceId: id,
                details: "Failed to update service",
                exception: ex);

            logger.LogError(ex, "Failed to update service {ServiceId}", id);
            throw;
        }
    }

    private static async Task<IResult> DeleteService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "DeleteService",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid service ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingService = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingService is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteService",
                    resourceType: "Service",
                    resourceId: id,
                    details: "Service not found");

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} deleted service {ServiceId} and {LayerCount} layers",
                identity.UserId, id, existingService.Layers.Count);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "DeleteService",
                resourceType: "Service",
                resourceId: id,
                details: $"Deleted service: {existingService.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceId"] = id,
                    ["serviceTitle"] = existingService.Title,
                    ["layersDeleted"] = existingService.Layers.Count
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteService",
                resourceType: "Service",
                resourceId: id,
                details: "Failed to delete service",
                exception: ex);

            logger.LogError(ex, "Failed to delete service {ServiceId}", id);
            throw;
        }
    }

    private static async Task<IResult> EnableService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        return await ToggleServiceEnabledState(id, true, metadataProvider, userIdentityService, auditLoggingService, logger, ct);
    }

    private static async Task<IResult> DisableService(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        return await ToggleServiceEnabledState(id, false, metadataProvider, userIdentityService, auditLoggingService, logger, ct);
    }

    private static async Task<IResult> ToggleServiceEnabledState(
        string id,
        bool enabled,
        [FromServices] IMutableMetadataProvider metadataProvider,
        IUserIdentityService userIdentityService,
        IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: enabled ? "EnableService" : "DisableService",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid service ID format");
            }

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} {Action} service {ServiceId}",
                identity.UserId, enabled ? "enabled" : "disabled", id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: enabled ? "EnableService" : "DisableService",
                resourceType: "Service",
                resourceId: id,
                details: $"{(enabled ? "Enabled" : "Disabled")} service: {existingService.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceId"] = id,
                    ["enabled"] = enabled
                });

            return Results.Ok(new { Id = id, Enabled = enabled });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: enabled ? "EnableService" : "DisableService",
                resourceType: "Service",
                resourceId: id,
                details: $"Failed to {(enabled ? "enable" : "disable")} service",
                exception: ex);

            logger.LogError(ex, "Failed to {Action} service {ServiceId}", enabled ? "enable" : "disable", id);
            throw;
        }
    }

    private static async Task<IResult> GetServiceConnectionFile(
        string id,
        string type,
        HttpContext context,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetServiceConnectionFile",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid service ID format");
            }

            InputValidationHelpers.ThrowIfUnsafeInput(type, nameof(type));

            var snapshot = await metadataProvider.LoadAsync(ct);
            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (service is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "GetServiceConnectionFile",
                    resourceType: "Service",
                    resourceId: id,
                    details: "Service not found");

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

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "Service",
                resourceId: id,
                operation: "DownloadConnectionFile",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceId"] = id,
                    ["connectionType"] = type
                });

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(content),
                contentType,
                fileName);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetServiceConnectionFile",
                resourceType: "Service",
                resourceId: id,
                details: "Failed to generate connection file",
                exception: ex);

            throw;
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
}
