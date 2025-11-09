// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
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
/// Admin REST API endpoints for layer group administration.
/// </summary>
public static class LayerGroupAdministrationEndpoints
{
    /// <summary>
    /// Maps all admin layer group endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAdminLayerGroupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/metadata/layergroups")
            .WithTags("Admin - Layer Groups")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        group.MapGet("", GetLayerGroups)
            .WithName("GetLayerGroups")
            .WithSummary("List all layer groups");

        group.MapGet("{id}", GetLayerGroupById)
            .WithName("GetLayerGroupById")
            .WithSummary("Get layer group by ID");

        group.MapPost("", CreateLayerGroup)
            .WithName("CreateLayerGroup")
            .WithSummary("Create a new layer group");

        group.MapPut("{id}", UpdateLayerGroup)
            .WithName("UpdateLayerGroup")
            .WithSummary("Update an existing layer group");

        group.MapDelete("{id}", DeleteLayerGroup)
            .WithName("DeleteLayerGroup")
            .WithSummary("Delete a layer group");

        return group;
    }

    private static async Task<IResult> GetLayerGroups(
        [FromServices] IMutableMetadataProvider metadataProvider,
        string? serviceId,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var layerGroups = snapshot.LayerGroups
            .Where(lg => string.IsNullOrEmpty(serviceId) || lg.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase))
            .Select(lg => new LayerGroupListItem
            {
                Id = lg.Id,
                Title = lg.Title,
                ServiceId = lg.ServiceId,
                RenderMode = lg.RenderMode.ToString(),
                MemberCount = lg.Members.Count,
                Enabled = lg.Enabled
            })
            .ToList();

        return Results.Ok(layerGroups);
    }

    private static async Task<IResult> GetLayerGroupById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var layerGroup = snapshot.LayerGroups.FirstOrDefault(lg => lg.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (layerGroup is null)
        {
            return Results.Problem(
                title: "Layer group not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Layer group with ID '{id}' does not exist");
        }

        var response = new LayerGroupResponse
        {
            Id = layerGroup.Id,
            Title = layerGroup.Title,
            ServiceId = layerGroup.ServiceId,
            Description = layerGroup.Description,
            RenderMode = layerGroup.RenderMode.ToString(),
            Members = layerGroup.Members.Select(m => new LayerGroupMemberDto
            {
                Type = m.Type.ToString(),
                LayerId = m.LayerId,
                GroupId = m.GroupId,
                Order = m.Order,
                Opacity = m.Opacity,
                StyleId = m.StyleId,
                Enabled = m.Enabled
            }).ToList(),
            DefaultStyleId = layerGroup.DefaultStyleId,
            StyleIds = layerGroup.StyleIds.ToList(),
            Keywords = layerGroup.Keywords.ToList(),
            MinScale = layerGroup.MinScale,
            MaxScale = layerGroup.MaxScale,
            Enabled = layerGroup.Enabled,
            Queryable = layerGroup.Queryable,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = null
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateLayerGroup(
        CreateLayerGroupRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if layer group ID already exists
            if (snapshot.LayerGroups.Any(lg => lg.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Layer group already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Layer group with ID '{request.Id}' already exists");
            }

            // Validate: Check if service exists
            if (!snapshot.Services.Any(s => s.Id.Equals(request.ServiceId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{request.ServiceId}' does not exist");
            }

            // Validate members reference valid layers/groups
            foreach (var member in request.Members)
            {
                if (member.Type == "Layer" && !string.IsNullOrEmpty(member.LayerId))
                {
                    if (!snapshot.Layers.Any(l => l.Id.Equals(member.LayerId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Results.Problem(
                            title: "Layer not found",
                            statusCode: StatusCodes.Status404NotFound,
                            detail: $"Layer with ID '{member.LayerId}' does not exist");
                    }
                }
                else if (member.Type == "Group" && !string.IsNullOrEmpty(member.GroupId))
                {
                    if (!snapshot.LayerGroups.Any(lg => lg.Id.Equals(member.GroupId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Results.Problem(
                            title: "Nested group not found",
                            statusCode: StatusCodes.Status404NotFound,
                            detail: $"Layer group with ID '{member.GroupId}' does not exist");
                    }
                }
            }

            // Parse render mode
            if (!Enum.TryParse<RenderMode>(request.RenderMode, ignoreCase: true, out var renderMode))
            {
                return Results.Problem(
                    title: "Invalid render mode",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Render mode '{request.RenderMode}' is not valid. Valid values: Single, Opaque, Transparent");
            }

            // Create new layer group definition
            var newLayerGroup = new LayerGroupDefinition
            {
                Id = request.Id,
                Title = request.Title,
                ServiceId = request.ServiceId,
                Description = request.Description,
                RenderMode = renderMode,
                Members = request.Members.Select(m => new LayerGroupMember
                {
                    Type = Enum.Parse<LayerGroupMemberType>(m.Type, ignoreCase: true),
                    LayerId = m.LayerId,
                    GroupId = m.GroupId,
                    Order = m.Order,
                    Opacity = m.Opacity,
                    StyleId = m.StyleId,
                    Enabled = m.Enabled
                }).ToList(),
                DefaultStyleId = request.DefaultStyleId,
                StyleIds = request.StyleIds,
                Keywords = request.Keywords,
                MinScale = request.MinScale,
                MaxScale = request.MaxScale,
                Enabled = request.Enabled,
                Queryable = request.Queryable
            };

            // Build new snapshot
            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups.Append(newLayerGroup).ToList(),
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created layer group {LayerGroupId} for service {ServiceId}", newLayerGroup.Id, newLayerGroup.ServiceId);

            var response = new LayerGroupResponse
            {
                Id = newLayerGroup.Id,
                Title = newLayerGroup.Title,
                ServiceId = newLayerGroup.ServiceId,
                Description = newLayerGroup.Description,
                RenderMode = newLayerGroup.RenderMode.ToString(),
                Members = newLayerGroup.Members.Select(m => new LayerGroupMemberDto
                {
                    Type = m.Type.ToString(),
                    LayerId = m.LayerId,
                    GroupId = m.GroupId,
                    Order = m.Order,
                    Opacity = m.Opacity,
                    StyleId = m.StyleId,
                    Enabled = m.Enabled
                }).ToList(),
                DefaultStyleId = newLayerGroup.DefaultStyleId,
                StyleIds = newLayerGroup.StyleIds.ToList(),
                Keywords = newLayerGroup.Keywords.ToList(),
                MinScale = newLayerGroup.MinScale,
                MaxScale = newLayerGroup.MaxScale,
                Enabled = newLayerGroup.Enabled,
                Queryable = newLayerGroup.Queryable,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            };

            return Results.Created($"/admin/metadata/layergroups/{newLayerGroup.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create layer group {LayerGroupId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the layer group");
        }
    }

    private static async Task<IResult> UpdateLayerGroup(
        string id,
        UpdateLayerGroupRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayerGroup = snapshot.LayerGroups.FirstOrDefault(lg => lg.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayerGroup is null)
            {
                return Results.Problem(
                    title: "Layer group not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer group with ID '{id}' does not exist");
            }

            // Validate members reference valid layers/groups
            foreach (var member in request.Members)
            {
                if (member.Type == "Layer" && !string.IsNullOrEmpty(member.LayerId))
                {
                    if (!snapshot.Layers.Any(l => l.Id.Equals(member.LayerId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Results.Problem(
                            title: "Layer not found",
                            statusCode: StatusCodes.Status404NotFound,
                            detail: $"Layer with ID '{member.LayerId}' does not exist");
                    }
                }
                else if (member.Type == "Group" && !string.IsNullOrEmpty(member.GroupId))
                {
                    if (!snapshot.LayerGroups.Any(lg => lg.Id.Equals(member.GroupId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Results.Problem(
                            title: "Nested group not found",
                            statusCode: StatusCodes.Status404NotFound,
                            detail: $"Layer group with ID '{member.GroupId}' does not exist");
                    }
                }
            }

            // Parse render mode
            if (!Enum.TryParse<RenderMode>(request.RenderMode, ignoreCase: true, out var renderMode))
            {
                return Results.Problem(
                    title: "Invalid render mode",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Render mode '{request.RenderMode}' is not valid. Valid values: Single, Opaque, Transparent");
            }

            // Update layer group
            var updatedLayerGroup = existingLayerGroup with
            {
                Title = request.Title,
                Description = request.Description,
                RenderMode = renderMode,
                Members = request.Members.Select(m => new LayerGroupMember
                {
                    Type = Enum.Parse<LayerGroupMemberType>(m.Type, ignoreCase: true),
                    LayerId = m.LayerId,
                    GroupId = m.GroupId,
                    Order = m.Order,
                    Opacity = m.Opacity,
                    StyleId = m.StyleId,
                    Enabled = m.Enabled
                }).ToList(),
                DefaultStyleId = request.DefaultStyleId,
                StyleIds = request.StyleIds,
                Keywords = request.Keywords,
                MinScale = request.MinScale,
                MaxScale = request.MaxScale,
                Enabled = request.Enabled,
                Queryable = request.Queryable
            };

            // Build new snapshot
            var updatedLayerGroups = snapshot.LayerGroups
                .Select(lg => lg.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedLayerGroup : lg)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                updatedLayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated layer group {LayerGroupId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update layer group {LayerGroupId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the layer group");
        }
    }

    private static async Task<IResult> DeleteLayerGroup(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayerGroup = snapshot.LayerGroups.FirstOrDefault(lg => lg.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayerGroup is null)
            {
                return Results.Problem(
                    title: "Layer group not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer group with ID '{id}' does not exist");
            }

            // Check if this group is referenced by other groups (nested)
            var referencingGroups = snapshot.LayerGroups
                .Where(lg => lg.Members.Any(m => m.Type == LayerGroupMemberType.Group &&
                    m.GroupId != null &&
                    m.GroupId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (referencingGroups.Any())
            {
                return Results.Problem(
                    title: "Layer group in use",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Cannot delete layer group '{id}' because it is referenced by {referencingGroups.Count} other group(s): {string.Join(", ", referencingGroups.Select(g => g.Id))}");
            }

            // Remove layer group
            var updatedLayerGroups = snapshot.LayerGroups
                .Where(lg => !lg.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                updatedLayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted layer group {LayerGroupId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete layer group {LayerGroupId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the layer group");
        }
    }
}
