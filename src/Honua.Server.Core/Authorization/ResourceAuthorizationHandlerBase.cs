// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Base class for ASP.NET Core authorization handlers that integrate with resource-based authorization.
/// </summary>
/// <typeparam name="TRequirement">The type of authorization requirement.</typeparam>
/// <typeparam name="TResource">The type of resource being authorized.</typeparam>
public abstract class ResourceAuthorizationHandlerBase<TRequirement, TResource> : AuthorizationHandler<TRequirement, TResource>
    where TRequirement : ResourceAuthorizationRequirement
{
    private readonly IResourceAuthorizationService _authorizationService;

    protected ResourceAuthorizationHandlerBase(IResourceAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TRequirement requirement,
        TResource resource)
    {
        var resourceInfo = ExtractResourceInfo(resource);

        if (!resourceInfo.HasValue)
        {
            context.Fail();
            return;
        }

        var result = await _authorizationService.AuthorizeAsync(
            context.User,
            resourceInfo.Value.ResourceType,
            resourceInfo.Value.ResourceId,
            requirement.Operation,
            CancellationToken.None);

        if (result.Succeeded)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }

    /// <summary>
    /// Extracts resource type and ID from the resource object.
    /// </summary>
    protected abstract (string ResourceType, string ResourceId)? ExtractResourceInfo(TResource resource);
}

/// <summary>
/// Resource information tuple.
/// </summary>
public record ResourceInfo(string ResourceType, string ResourceId);

/// <summary>
/// Authorization handler for layer read operations.
/// </summary>
public sealed class ReadLayerAuthorizationHandler : ResourceAuthorizationHandlerBase<ReadLayerRequirement, string>
{
    public ReadLayerAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("layer", resource);
    }
}

/// <summary>
/// Authorization handler for layer write operations.
/// </summary>
public sealed class WriteLayerAuthorizationHandler : ResourceAuthorizationHandlerBase<WriteLayerRequirement, string>
{
    public WriteLayerAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("layer", resource);
    }
}

/// <summary>
/// Authorization handler for layer delete operations.
/// </summary>
public sealed class DeleteLayerAuthorizationHandler : ResourceAuthorizationHandlerBase<DeleteLayerRequirement, string>
{
    public DeleteLayerAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("layer", resource);
    }
}

/// <summary>
/// Authorization handler for collection read operations.
/// </summary>
public sealed class ReadCollectionAuthorizationHandler : ResourceAuthorizationHandlerBase<ReadCollectionRequirement, string>
{
    public ReadCollectionAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("collection", resource);
    }
}

/// <summary>
/// Authorization handler for collection write operations.
/// </summary>
public sealed class WriteCollectionAuthorizationHandler : ResourceAuthorizationHandlerBase<WriteCollectionRequirement, string>
{
    public WriteCollectionAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("collection", resource);
    }
}

/// <summary>
/// Authorization handler for collection delete operations.
/// </summary>
public sealed class DeleteCollectionAuthorizationHandler : ResourceAuthorizationHandlerBase<DeleteCollectionRequirement, string>
{
    public DeleteCollectionAuthorizationHandler(IResourceAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override (string ResourceType, string ResourceId)? ExtractResourceInfo(string resource)
    {
        return string.IsNullOrWhiteSpace(resource) ? null : ("collection", resource);
    }
}
