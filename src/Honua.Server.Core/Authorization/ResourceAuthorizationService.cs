// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Orchestrates resource-based authorization across multiple handlers.
/// </summary>
public interface IResourceAuthorizationService
{
    /// <summary>
    /// Checks if the user is authorized to perform the operation on the resource.
    /// </summary>
    Task<ResourceAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        string resourceType,
        string resourceId,
        string operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached authorization decisions for a resource.
    /// </summary>
    void InvalidateResource(string resourceType, string resourceId);
}

/// <summary>
/// Default implementation of the resource authorization service.
/// </summary>
public sealed class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly IEnumerable<IResourceAuthorizationHandler> _handlers;
    private readonly IResourceAuthorizationCache _cache;
    private readonly ILogger<ResourceAuthorizationService> _logger;

    public ResourceAuthorizationService(
        IEnumerable<IResourceAuthorizationHandler> handlers,
        IResourceAuthorizationCache cache,
        ILogger<ResourceAuthorizationService> logger)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResourceAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        string resourceType,
        string resourceId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Operation cannot be null or empty", nameof(operation));
        }

        // Find the appropriate handler
        var handler = _handlers.FirstOrDefault(h =>
            string.Equals(h.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));

        if (handler == null)
        {
            _logger.LogWarning(
                "No authorization handler found for resource type '{ResourceType}'. Available handlers: {Handlers}",
                resourceType,
                string.Join(", ", _handlers.Select(h => h.ResourceType)));

            return ResourceAuthorizationResult.Fail($"No authorization handler found for resource type '{resourceType}'");
        }

        // Delegate to handler
        return await handler.AuthorizeAsync(user, resourceType, resourceId, operation, cancellationToken);
    }

    public void InvalidateResource(string resourceType, string resourceId)
    {
        _cache.InvalidateResource(resourceType, resourceId);
    }
}
