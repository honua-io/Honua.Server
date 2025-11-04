// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Configuration options for resource-based authorization.
/// </summary>
public sealed class ResourceAuthorizationOptions
{
    public const string SectionName = "honua:authorization";

    /// <summary>
    /// Gets or sets whether resource-based authorization is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache duration for authorization decisions in seconds.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Gets or sets the maximum number of cached authorization decisions.
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the resource policies.
    /// </summary>
    public List<ResourcePolicy> Policies { get; set; } = new();

    /// <summary>
    /// Gets or sets the default action when no policy matches.
    /// </summary>
    public DefaultAction DefaultAction { get; set; } = DefaultAction.Deny;
}

/// <summary>
/// Defines a resource-based authorization policy.
/// </summary>
public sealed class ResourcePolicy
{
    /// <summary>
    /// Gets or sets the policy identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resource type (e.g., "layer", "collection", "style").
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resource pattern (supports wildcards like "weather:*" or "*").
    /// </summary>
    public string ResourcePattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed operations (e.g., "read", "write", "delete").
    /// </summary>
    public List<string> AllowedOperations { get; set; } = new();

    /// <summary>
    /// Gets or sets the roles that this policy applies to.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the users (by claim) that this policy applies to.
    /// </summary>
    public List<string> Users { get; set; } = new();

    /// <summary>
    /// Gets or sets the priority of this policy (higher values take precedence).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether this policy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Determines if the specified resource ID matches this policy's pattern.
    /// </summary>
    public bool MatchesResource(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(ResourcePattern))
        {
            return false;
        }

        // Handle exact match
        if (ResourcePattern == resourceId)
        {
            return true;
        }

        // Handle wildcard patterns
        if (ResourcePattern.Contains('*'))
        {
            // Convert glob pattern to regex
            var regexPattern = "^" + Regex.Escape(ResourcePattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(resourceId, regexPattern, RegexOptions.IgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Determines if the specified operation is allowed by this policy.
    /// </summary>
    public bool AllowsOperation(string operation)
    {
        return AllowedOperations.Contains(operation, StringComparer.OrdinalIgnoreCase) ||
               AllowedOperations.Contains("*", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the policy applies to the specified roles.
    /// </summary>
    public bool AppliesTo(IEnumerable<string> userRoles)
    {
        if (Roles.Count == 0 && Users.Count == 0)
        {
            // If no roles or users specified, policy applies to everyone
            return true;
        }

        if (Roles.Count > 0 && userRoles.Any(role => Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the policy applies to the specified user.
    /// </summary>
    public bool AppliesToUser(string userId)
    {
        if (Users.Count == 0)
        {
            return false;
        }

        return Users.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Default action when no policy matches.
/// </summary>
public enum DefaultAction
{
    /// <summary>
    /// Deny access by default.
    /// </summary>
    Deny,

    /// <summary>
    /// Allow access by default.
    /// </summary>
    Allow
}
