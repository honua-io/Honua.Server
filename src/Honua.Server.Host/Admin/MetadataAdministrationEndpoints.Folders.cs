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
/// Partial class containing folder-related admin endpoints.
/// Handles CRUD operations for organizing services into folders.
/// </summary>
public static partial class MetadataAdministrationEndpoints
{
    #region Folders

    private static async Task<IResult> GetFolders(
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
                    action: "GetFolders",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

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

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "Folder",
                resourceId: "list",
                operation: "Read");

            return Results.Ok(folders);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetFolders",
                resourceType: "Folder",
                details: "Failed to retrieve folders",
                exception: ex);

            throw;
        }
    }

    private static async Task<IResult> CreateFolder(
        CreateFolderRequest request,
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
                    action: "CreateFolder",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Id, nameof(request.Id));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Title, nameof(request.Title));

            if (!InputValidationHelpers.IsValidLength(request.Id, minLength: 1, maxLength: 100))
            {
                return Results.BadRequest("Folder ID must be between 1 and 100 characters");
            }

            if (!InputValidationHelpers.IsValidLength(request.Title, minLength: 1, maxLength: 200))
            {
                return Results.BadRequest("Folder title must be between 1 and 200 characters");
            }

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} created folder {FolderId}", identity.UserId, newFolder.Id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "CreateFolder",
                resourceType: "Folder",
                resourceId: newFolder.Id,
                details: $"Created folder: {newFolder.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["folderId"] = newFolder.Id,
                    ["folderTitle"] = newFolder.Title,
                    ["order"] = newFolder.Order
                });

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "CreateFolder",
                resourceType: "Folder",
                resourceId: request.Id,
                details: "Failed to create folder",
                exception: ex);

            logger.LogError(ex, "Failed to create folder {FolderId}", request.Id);
            throw;
        }
    }

    private static async Task<IResult> UpdateFolder(
        string id,
        UpdateFolderRequest request,
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
                    action: "UpdateFolder",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid folder ID format");
            }

            if (request.Title != null)
            {
                InputValidationHelpers.ThrowIfUnsafeInput(request.Title, nameof(request.Title));
                if (!InputValidationHelpers.IsValidLength(request.Title, minLength: 1, maxLength: 200))
                {
                    return Results.BadRequest("Folder title must be between 1 and 200 characters");
                }
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingFolder = snapshot.Folders.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingFolder is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "UpdateFolder",
                    resourceType: "Folder",
                    resourceId: id,
                    details: "Folder not found");

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} updated folder {FolderId}", identity.UserId, id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateFolder",
                resourceType: "Folder",
                resourceId: id,
                details: $"Updated folder: {updatedFolder.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["folderId"] = id,
                    ["folderTitle"] = updatedFolder.Title,
                    ["order"] = updatedFolder.Order
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateFolder",
                resourceType: "Folder",
                resourceId: id,
                details: "Failed to update folder",
                exception: ex);

            logger.LogError(ex, "Failed to update folder {FolderId}", id);
            throw;
        }
    }

    private static async Task<IResult> DeleteFolder(
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
                    action: "DeleteFolder",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid folder ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingFolder = snapshot.Folders.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingFolder is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteFolder",
                    resourceType: "Folder",
                    resourceId: id,
                    details: "Folder not found");

                return Results.Problem(
                    title: "Folder not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Folder with ID '{id}' does not exist");
            }

            // Check if folder contains services
            var serviceCount = snapshot.Services.Count(s => s.FolderId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (serviceCount > 0)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteFolder",
                    resourceType: "Folder",
                    resourceId: id,
                    details: $"Folder contains {serviceCount} service(s)");

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
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("User {UserId} deleted folder {FolderId}", identity.UserId, id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "DeleteFolder",
                resourceType: "Folder",
                resourceId: id,
                details: $"Deleted folder: {existingFolder.Title}",
                additionalData: new Dictionary<string, object>
                {
                    ["folderId"] = id,
                    ["folderTitle"] = existingFolder.Title
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteFolder",
                resourceType: "Folder",
                resourceId: id,
                details: "Failed to delete folder",
                exception: ex);

            logger.LogError(ex, "Failed to delete folder {FolderId}", id);
            throw;
        }
    }

    #endregion
}
