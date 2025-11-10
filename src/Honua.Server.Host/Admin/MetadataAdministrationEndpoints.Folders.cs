// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
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
