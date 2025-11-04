// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Defines the contract for resource-based authorization handlers.
/// Implementations evaluate whether a user has permission to perform specific actions on resources.
/// </summary>
public interface IResourceAuthorizationHandler
{
    /// <summary>
    /// Determines whether the specified user is authorized to perform the requested operation on the resource.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="resourceType">The type of resource (e.g., "layer", "collection").</param>
    /// <param name="resourceId">The identifier of the resource.</param>
    /// <param name="operation">The operation being requested (e.g., "read", "write", "delete").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that resolves to an authorization result.</returns>
    Task<ResourceAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        string resourceType,
        string resourceId,
        string operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resource type this handler supports (e.g., "layer", "collection").
    /// </summary>
    string ResourceType { get; }
}

/// <summary>
/// Represents the result of a resource authorization check.
/// </summary>
public sealed record ResourceAuthorizationResult
{
    /// <summary>
    /// Gets whether the authorization succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the reason for authorization failure, if applicable.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets whether the result was retrieved from cache.
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// Creates a successful authorization result.
    /// </summary>
    public static ResourceAuthorizationResult Success(bool fromCache = false) =>
        new() { Succeeded = true, FromCache = fromCache };

    /// <summary>
    /// Creates a failed authorization result.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="fromCache">Whether the result came from cache.</param>
    public static ResourceAuthorizationResult Fail(string reason, bool fromCache = false) =>
        new() { Succeeded = false, FailureReason = reason, FromCache = fromCache };
}
