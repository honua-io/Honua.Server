// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// API endpoints for RBAC (Role-Based Access Control) management.
/// </summary>
public static class RbacEndpoints
{
    public static RouteGroupBuilder MapAdminRbacEndpoints(this RouteGroupBuilder group)
    {
        var rbacGroup = group.MapGroup("/rbac")
            .WithTags("RBAC Administration")
            .RequireAuthorization(new AuthorizeAttribute { Roles = "administrator" });

        // Role management endpoints
        rbacGroup.MapGet("/roles", GetRoles)
            .WithName("GetRoles")
            .WithDescription("List all roles");

        rbacGroup.MapGet("/roles/{id}", GetRole)
            .WithName("GetRole")
            .WithDescription("Get a specific role by ID");

        rbacGroup.MapPost("/roles", CreateRole)
            .WithName("CreateRole")
            .WithDescription("Create a new custom role");

        rbacGroup.MapPut("/roles/{id}", UpdateRole)
            .WithName("UpdateRole")
            .WithDescription("Update an existing role");

        rbacGroup.MapDelete("/roles/{id}", DeleteRole)
            .WithName("DeleteRole")
            .WithDescription("Delete a custom role");

        // Permission management endpoints
        rbacGroup.MapGet("/permissions", GetPermissions)
            .WithName("GetPermissions")
            .WithDescription("List all available permissions");

        rbacGroup.MapPost("/permissions", CreatePermission)
            .WithName("CreatePermission")
            .WithDescription("Create a custom permission");

        return group;
    }

    private static async Task<IResult> GetRoles(
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var roles = snapshot.Server.Rbac.Roles
            .Select(r => new RoleDefinitionDto
            {
                Id = r.Id,
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                Permissions = r.Permissions.ToList(),
                IsSystem = r.IsSystem,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToList();

        return Results.Ok(new RoleListResponse
        {
            Roles = roles,
            TotalCount = roles.Count
        });
    }

    private static async Task<IResult> GetRole(
        string id,
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var role = snapshot.Server.Rbac.Roles
            .FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (role == null)
        {
            return Results.NotFound(new { error = $"Role '{id}' not found" });
        }

        return Results.Ok(new RoleDefinitionDto
        {
            Id = role.Id,
            Name = role.Name,
            DisplayName = role.DisplayName,
            Description = role.Description,
            Permissions = role.Permissions.ToList(),
            IsSystem = role.IsSystem,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        });
    }

    private static async Task<IResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        IMetadataRegistry registry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);

        // Validate role name is unique
        if (snapshot.Server.Rbac.Roles.Any(r => r.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.BadRequest(new { error = $"Role with name '{request.Name}' already exists" });
        }

        // Validate permissions exist
        var availablePermissions = snapshot.Server.Rbac.Permissions.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidPermissions = request.Permissions.Where(p => !availablePermissions.Contains(p)).ToList();
        if (invalidPermissions.Any())
        {
            return Results.BadRequest(new { error = $"Invalid permissions: {string.Join(", ", invalidPermissions)}" });
        }

        // Create new role
        var roleId = request.Name.ToLowerInvariant().Replace(" ", "-");
        var newRole = new RoleDefinition
        {
            Id = roleId,
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Permissions = request.Permissions.ToList(),
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Update snapshot
        var updatedRoles = snapshot.Server.Rbac.Roles.Append(newRole).ToList();
        var updatedRbac = snapshot.Server.Rbac with { Roles = updatedRoles };
        var updatedServer = snapshot.Server with { Rbac = updatedRbac };
        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            snapshot.Layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.LayerGroups,
            updatedServer);

        await registry.UpdateAsync(updatedSnapshot, cancellationToken);

        logger.LogInformation("Created role '{RoleName}' with {PermissionCount} permissions", request.Name, request.Permissions.Count);

        return Results.Ok(new RoleDefinitionDto
        {
            Id = newRole.Id,
            Name = newRole.Name,
            DisplayName = newRole.DisplayName,
            Description = newRole.Description,
            Permissions = newRole.Permissions.ToList(),
            IsSystem = newRole.IsSystem,
            CreatedAt = newRole.CreatedAt
        });
    }

    private static async Task<IResult> UpdateRole(
        string id,
        [FromBody] UpdateRoleRequest request,
        IMetadataRegistry registry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var existingRole = snapshot.Server.Rbac.Roles
            .FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingRole == null)
        {
            return Results.NotFound(new { error = $"Role '{id}' not found" });
        }

        if (existingRole.IsSystem)
        {
            return Results.BadRequest(new { error = "Cannot modify system roles" });
        }

        // Validate permissions if provided
        if (request.Permissions != null)
        {
            var availablePermissions = snapshot.Server.Rbac.Permissions.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidPermissions = request.Permissions.Where(p => !availablePermissions.Contains(p)).ToList();
            if (invalidPermissions.Any())
            {
                return Results.BadRequest(new { error = $"Invalid permissions: {string.Join(", ", invalidPermissions)}" });
            }
        }

        // Update role
        var updatedRole = existingRole with
        {
            DisplayName = request.DisplayName ?? existingRole.DisplayName,
            Description = request.Description ?? existingRole.Description,
            Permissions = request.Permissions?.ToList() ?? existingRole.Permissions,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Update snapshot
        var updatedRoles = snapshot.Server.Rbac.Roles
            .Select(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedRole : r)
            .ToList();
        var updatedRbac = snapshot.Server.Rbac with { Roles = updatedRoles };
        var updatedServer = snapshot.Server with { Rbac = updatedRbac };
        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            snapshot.Layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.LayerGroups,
            updatedServer);

        await registry.UpdateAsync(updatedSnapshot, cancellationToken);

        logger.LogInformation("Updated role '{RoleId}'", id);

        return Results.Ok(new RoleDefinitionDto
        {
            Id = updatedRole.Id,
            Name = updatedRole.Name,
            DisplayName = updatedRole.DisplayName,
            Description = updatedRole.Description,
            Permissions = updatedRole.Permissions.ToList(),
            IsSystem = updatedRole.IsSystem,
            CreatedAt = updatedRole.CreatedAt,
            UpdatedAt = updatedRole.UpdatedAt
        });
    }

    private static async Task<IResult> DeleteRole(
        string id,
        IMetadataRegistry registry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var existingRole = snapshot.Server.Rbac.Roles
            .FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existingRole == null)
        {
            return Results.NotFound(new { error = $"Role '{id}' not found" });
        }

        if (existingRole.IsSystem)
        {
            return Results.BadRequest(new { error = "Cannot delete system roles" });
        }

        // Remove role
        var updatedRoles = snapshot.Server.Rbac.Roles
            .Where(r => !r.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var updatedRbac = snapshot.Server.Rbac with { Roles = updatedRoles };
        var updatedServer = snapshot.Server with { Rbac = updatedRbac };
        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            snapshot.Layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.LayerGroups,
            updatedServer);

        await registry.UpdateAsync(updatedSnapshot, cancellationToken);

        logger.LogInformation("Deleted role '{RoleId}'", id);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPermissions(
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var permissions = snapshot.Server.Rbac.Permissions
            .Select(p => new PermissionDefinitionDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Category = p.Category,
                IsSystem = p.IsSystem
            })
            .ToList();

        var categories = permissions
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Results.Ok(new PermissionListResponse
        {
            Permissions = permissions,
            Categories = categories
        });
    }

    private static async Task<IResult> CreatePermission(
        [FromBody] CreatePermissionRequest request,
        IMetadataRegistry registry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);

        // Validate permission name is unique
        if (snapshot.Server.Rbac.Permissions.Any(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.BadRequest(new { error = $"Permission '{request.Name}' already exists" });
        }

        // Create new permission
        var newPermission = new PermissionDefinition
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Category = request.Category,
            IsSystem = false
        };

        // Update snapshot
        var updatedPermissions = snapshot.Server.Rbac.Permissions.Append(newPermission).ToList();
        var updatedRbac = snapshot.Server.Rbac with { Permissions = updatedPermissions };
        var updatedServer = snapshot.Server with { Rbac = updatedRbac };
        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            snapshot.Layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.LayerGroups,
            updatedServer);

        await registry.UpdateAsync(updatedSnapshot, cancellationToken);

        logger.LogInformation("Created permission '{PermissionName}' in category '{Category}'", request.Name, request.Category);

        return Results.Ok(new PermissionDefinitionDto
        {
            Name = newPermission.Name,
            DisplayName = newPermission.DisplayName,
            Description = newPermission.Description,
            Category = newPermission.Category,
            IsSystem = newPermission.IsSystem
        });
    }
}
