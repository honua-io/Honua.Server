// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public sealed class UserResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isLockedOut")]
    public bool IsLockedOut { get; set; }

    [JsonPropertyName("lockedUntil")]
    public DateTimeOffset? LockedUntil { get; set; }

    [JsonPropertyName("lastLogin")]
    public DateTimeOffset? LastLogin { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("passwordExpiresAt")]
    public DateTimeOffset? PasswordExpiresAt { get; set; }

    [JsonPropertyName("failedLoginAttempts")]
    public int FailedLoginAttempts { get; set; }
}

/// <summary>
/// Request to create a new user.
/// </summary>
public sealed class CreateUserRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Request to update an existing user.
/// </summary>
public sealed class UpdateUserRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Request to change a user's password.
/// </summary>
public sealed class ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    public string? CurrentPassword { get; set; }

    [JsonPropertyName("newPassword")]
    public required string NewPassword { get; set; }
}

/// <summary>
/// Request to assign roles to a user.
/// </summary>
public sealed class AssignRolesRequest
{
    [JsonPropertyName("roles")]
    public required List<string> Roles { get; set; }
}

/// <summary>
/// Paginated list of users.
/// </summary>
public sealed class UserListResponse
{
    [JsonPropertyName("users")]
    public List<UserResponse> Users { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

/// <summary>
/// Represents a role in the system.
/// </summary>
public sealed class RoleInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// User statistics.
/// </summary>
public sealed class UserStatistics
{
    [JsonPropertyName("totalUsers")]
    public int TotalUsers { get; set; }

    [JsonPropertyName("activeUsers")]
    public int ActiveUsers { get; set; }

    [JsonPropertyName("lockedUsers")]
    public int LockedUsers { get; set; }

    [JsonPropertyName("disabledUsers")]
    public int DisabledUsers { get; set; }

    [JsonPropertyName("usersByRole")]
    public Dictionary<string, int> UsersByRole { get; set; } = new();
}

/// <summary>
/// Predefined role options.
/// </summary>
public static class RoleOptions
{
    public const string Administrator = "administrator";
    public const string DataPublisher = "datapublisher";
    public const string Viewer = "viewer";

    public static readonly List<RoleInfo> AvailableRoles = new()
    {
        new RoleInfo
        {
            Name = Administrator,
            DisplayName = "Administrator",
            Description = "Full system access including user management, configuration, and all data operations",
            Permissions = new List<string> { "all" }
        },
        new RoleInfo
        {
            Name = DataPublisher,
            DisplayName = "Data Publisher",
            Description = "Can create, update, and delete services, layers, and import data",
            Permissions = new List<string> { "read", "write", "import", "export" }
        },
        new RoleInfo
        {
            Name = Viewer,
            DisplayName = "Viewer",
            Description = "Read-only access to view services, layers, and metadata",
            Permissions = new List<string> { "read" }
        }
    };

    public static RoleInfo? GetRoleInfo(string roleName)
    {
        return AvailableRoles.FirstOrDefault(r =>
            string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
    }
}
