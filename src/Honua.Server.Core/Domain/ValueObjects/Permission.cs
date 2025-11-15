// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission with behavior.
/// Permissions define what actions can be performed on resources.
/// </summary>
public sealed record Permission
{
    /// <summary>
    /// Gets the permission action (e.g., "read", "write", "delete").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Gets the resource type this permission applies to (e.g., "map", "layer", "user").
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Gets the optional scope or constraint for this permission.
    /// For example, "own" for only own resources, or a specific resource ID.
    /// </summary>
    public string? Scope { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Permission"/> record.
    /// </summary>
    /// <param name="action">The permission action.</param>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="scope">The optional scope or constraint.</param>
    /// <exception cref="DomainException">Thrown when action or resourceType is invalid.</exception>
    public Permission(string action, string resourceType, string? scope = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException(
                "Permission action cannot be empty.",
                "PERMISSION_ACTION_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new DomainException(
                "Permission resource type cannot be empty.",
                "PERMISSION_RESOURCE_TYPE_EMPTY");
        }

        Action = action.Trim().ToLowerInvariant();
        ResourceType = resourceType.Trim().ToLowerInvariant();
        Scope = scope?.Trim();
    }

    /// <summary>
    /// Creates a permission from a string in the format "action:resourceType" or "action:resourceType:scope".
    /// </summary>
    /// <param name="permissionString">The permission string to parse.</param>
    /// <returns>A new <see cref="Permission"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the permission string format is invalid.</exception>
    public static Permission Parse(string permissionString)
    {
        if (string.IsNullOrWhiteSpace(permissionString))
        {
            throw new DomainException(
                "Permission string cannot be empty.",
                "PERMISSION_STRING_EMPTY");
        }

        var parts = permissionString.Split(':', StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            2 => new Permission(parts[0], parts[1]),
            3 => new Permission(parts[0], parts[1], parts[2]),
            _ => throw new DomainException(
                $"Invalid permission format: {permissionString}. Expected format: 'action:resourceType' or 'action:resourceType:scope'",
                "PERMISSION_INVALID_FORMAT")
        };
    }

    /// <summary>
    /// Attempts to parse a permission from a string.
    /// </summary>
    /// <param name="permissionString">The permission string to parse.</param>
    /// <param name="permission">When this method returns, contains the permission if parsing succeeded.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string permissionString, out Permission? permission)
    {
        try
        {
            permission = Parse(permissionString);
            return true;
        }
        catch (DomainException)
        {
            permission = null;
            return false;
        }
    }

    /// <summary>
    /// Checks if this permission matches another permission.
    /// A permission matches if action and resourceType are the same, and scope is compatible.
    /// </summary>
    /// <param name="other">The permission to check against.</param>
    /// <returns>true if the permissions match; otherwise, false.</returns>
    public bool Matches(Permission other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Action != other.Action || ResourceType != other.ResourceType)
            return false;

        // If this permission has no scope, it matches any scope
        if (string.IsNullOrEmpty(Scope))
            return true;

        // If this permission has a scope, it must match exactly
        return Scope == other.Scope;
    }

    /// <summary>
    /// Checks if this permission implies another permission.
    /// A permission implies another if it grants equal or broader access.
    /// </summary>
    /// <param name="required">The required permission to check.</param>
    /// <returns>true if this permission implies the required permission; otherwise, false.</returns>
    public bool Implies(Permission required)
    {
        ArgumentNullException.ThrowIfNull(required);

        // Wildcard action implies any action
        if (Action == "*" && ResourceType == required.ResourceType)
            return true;

        // Wildcard resource type implies any resource type
        if (Action == required.Action && ResourceType == "*")
            return true;

        // Full wildcard implies everything
        if (Action == "*" && ResourceType == "*")
            return true;

        // Otherwise, use standard matching
        return Matches(required);
    }

    /// <summary>
    /// Returns the string representation of this permission.
    /// </summary>
    /// <returns>The permission in format "action:resourceType" or "action:resourceType:scope".</returns>
    public override string ToString()
    {
        return string.IsNullOrEmpty(Scope)
            ? $"{Action}:{ResourceType}"
            : $"{Action}:{ResourceType}:{Scope}";
    }

    /// <summary>
    /// Implicitly converts a <see cref="Permission"/> to a string.
    /// </summary>
    /// <param name="permission">The permission to convert.</param>
    public static implicit operator string(Permission permission) => permission.ToString();

    // Common permission factory methods for convenience

    /// <summary>
    /// Creates a read permission for the specified resource type.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A new read permission.</returns>
    public static Permission Read(string resourceType, string? scope = null)
        => new("read", resourceType, scope);

    /// <summary>
    /// Creates a write permission for the specified resource type.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A new write permission.</returns>
    public static Permission Write(string resourceType, string? scope = null)
        => new("write", resourceType, scope);

    /// <summary>
    /// Creates a delete permission for the specified resource type.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A new delete permission.</returns>
    public static Permission Delete(string resourceType, string? scope = null)
        => new("delete", resourceType, scope);

    /// <summary>
    /// Creates a full access permission (wildcard) for the specified resource type.
    /// </summary>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="scope">The optional scope.</param>
    /// <returns>A new full access permission.</returns>
    public static Permission FullAccess(string resourceType, string? scope = null)
        => new("*", resourceType, scope);

    /// <summary>
    /// Creates an admin permission with full access to all resources.
    /// </summary>
    /// <returns>A new admin permission.</returns>
    public static Permission Admin() => new("*", "*");
}
