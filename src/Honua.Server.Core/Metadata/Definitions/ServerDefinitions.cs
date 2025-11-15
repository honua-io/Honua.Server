// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

public sealed record CorsDefinition
{
    public static CorsDefinition Disabled => new();

    public bool Enabled { get; init; }
    public bool AllowAnyOrigin { get; init; }
    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedMethods { get; init; } = Array.Empty<string>();
    public bool AllowAnyMethod { get; init; }
    public IReadOnlyList<string> AllowedHeaders { get; init; } = Array.Empty<string>();
    public bool AllowAnyHeader { get; init; }
    public IReadOnlyList<string> ExposedHeaders { get; init; } = Array.Empty<string>();
    public bool AllowCredentials { get; init; }
    public int? MaxAge { get; init; }
}

public sealed record ServerDefinition
{
    public static ServerDefinition Default => new();

    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
    public CorsDefinition Cors { get; init; } = CorsDefinition.Disabled;
    public ServerSecurityDefinition Security { get; init; } = ServerSecurityDefinition.Default;
    public RbacDefinition Rbac { get; init; } = RbacDefinition.Default;
}

public sealed record ServerSecurityDefinition
{
    public static ServerSecurityDefinition Default => new();

    /// <summary>
    /// Allowed base directories for raster data access.
    /// All raster file paths must resolve to within one of these directories.
    /// If empty, path validation is disabled (not recommended for production).
    /// </summary>
    public IReadOnlyList<string> AllowedRasterDirectories { get; init; } = Array.Empty<string>();
}

/// <summary>
/// RBAC (Role-Based Access Control) configuration.
/// </summary>
public sealed record RbacDefinition
{
    public static RbacDefinition Default => new()
    {
        Roles = new List<RoleDefinition>
        {
            new RoleDefinition
            {
                Id = "administrator",
                Name = "administrator",
                DisplayName = "Administrator",
                Description = "Full system access including user management, configuration, and all data operations",
                Permissions = new List<string> { "all" },
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RoleDefinition
            {
                Id = "datapublisher",
                Name = "datapublisher",
                DisplayName = "Data Publisher",
                Description = "Can create, update, and delete services, layers, and import data",
                Permissions = new List<string> { "read", "write", "import", "export" },
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RoleDefinition
            {
                Id = "viewer",
                Name = "viewer",
                DisplayName = "Viewer",
                Description = "Read-only access to view services, layers, and metadata",
                Permissions = new List<string> { "read" },
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        },
        Permissions = new List<PermissionDefinition>
        {
            // System permissions
            new PermissionDefinition { Name = "all", DisplayName = "All Permissions", Description = "Complete system access", Category = "System", IsSystem = true },

            // Data access permissions
            new PermissionDefinition { Name = "read", DisplayName = "Read", Description = "View data and metadata", Category = "Data", IsSystem = true },
            new PermissionDefinition { Name = "write", DisplayName = "Write", Description = "Create and update data", Category = "Data", IsSystem = true },
            new PermissionDefinition { Name = "delete", DisplayName = "Delete", Description = "Delete data and resources", Category = "Data", IsSystem = true },

            // Import/Export permissions
            new PermissionDefinition { Name = "import", DisplayName = "Import", Description = "Import data from external sources", Category = "DataTransfer", IsSystem = true },
            new PermissionDefinition { Name = "export", DisplayName = "Export", Description = "Export data to various formats", Category = "DataTransfer", IsSystem = true },

            // Collection permissions
            new PermissionDefinition { Name = "collections.read", DisplayName = "Read Collections", Description = "View collections and their metadata", Category = "Collections", IsSystem = true },
            new PermissionDefinition { Name = "collections.write", DisplayName = "Write Collections", Description = "Create and update collections", Category = "Collections", IsSystem = true },
            new PermissionDefinition { Name = "collections.delete", DisplayName = "Delete Collections", Description = "Delete collections", Category = "Collections", IsSystem = true },

            // Layer permissions
            new PermissionDefinition { Name = "layers.read", DisplayName = "Read Layers", Description = "View layers and their metadata", Category = "Layers", IsSystem = true },
            new PermissionDefinition { Name = "layers.write", DisplayName = "Write Layers", Description = "Create and update layers", Category = "Layers", IsSystem = true },
            new PermissionDefinition { Name = "layers.delete", DisplayName = "Delete Layers", Description = "Delete layers", Category = "Layers", IsSystem = true },
            new PermissionDefinition { Name = "layers.manage-styles", DisplayName = "Manage Layer Styles", Description = "Create, update, and delete layer styles", Category = "Layers", IsSystem = true },

            // User management permissions
            new PermissionDefinition { Name = "users.read", DisplayName = "Read Users", Description = "View user accounts", Category = "Administration", IsSystem = true },
            new PermissionDefinition { Name = "users.write", DisplayName = "Write Users", Description = "Create and update user accounts", Category = "Administration", IsSystem = true },
            new PermissionDefinition { Name = "users.delete", DisplayName = "Delete Users", Description = "Delete user accounts", Category = "Administration", IsSystem = true },

            // Role management permissions
            new PermissionDefinition { Name = "roles.read", DisplayName = "Read Roles", Description = "View roles and permissions", Category = "Administration", IsSystem = true },
            new PermissionDefinition { Name = "roles.write", DisplayName = "Write Roles", Description = "Create and update roles", Category = "Administration", IsSystem = true },
            new PermissionDefinition { Name = "roles.delete", DisplayName = "Delete Roles", Description = "Delete custom roles", Category = "Administration", IsSystem = true },

            // Configuration permissions
            new PermissionDefinition { Name = "config.read", DisplayName = "Read Configuration", Description = "View system configuration", Category = "Administration", IsSystem = true },
            new PermissionDefinition { Name = "config.write", DisplayName = "Write Configuration", Description = "Update system configuration", Category = "Administration", IsSystem = true },

            // Metadata permissions
            new PermissionDefinition { Name = "metadata.manage", DisplayName = "Manage Metadata", Description = "Create, update, and delete metadata", Category = "Metadata", IsSystem = true }
        }
    };

    public IReadOnlyList<RoleDefinition> Roles { get; init; } = Array.Empty<RoleDefinition>();
    public IReadOnlyList<PermissionDefinition> Permissions { get; init; } = Array.Empty<PermissionDefinition>();
}

/// <summary>
/// Represents a role with assigned permissions.
/// </summary>
public sealed record RoleDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public bool IsSystem { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Represents a permission that can be assigned to roles.
/// </summary>
public sealed record PermissionDefinition
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string Category { get; init; }
    public bool IsSystem { get; init; }
}
