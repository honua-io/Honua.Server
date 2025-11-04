// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Authorization;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Helper methods for OGC endpoint authorization.
/// </summary>
internal static class OgcAuthorizationHelpers
{
    /// <summary>
    /// Checks if the current user is authorized to access a layer.
    /// </summary>
    public static async Task<IResult?> CheckLayerAuthorizationAsync(
        HttpContext context,
        IResourceAuthorizationService authorizationService,
        string layerId,
        string operation = "read",
        CancellationToken cancellationToken = default)
    {
        var result = await authorizationService.AuthorizeAsync(
            context.User,
            "layer",
            layerId,
            operation,
            cancellationToken);

        if (!result.Succeeded)
        {
            return OgcProblemDetails.CreateForbiddenProblem(
                result.FailureReason ?? "Access to this layer is forbidden");
        }

        return null;
    }

    /// <summary>
    /// Checks if the current user is authorized to access a collection.
    /// </summary>
    public static async Task<IResult?> CheckCollectionAuthorizationAsync(
        HttpContext context,
        IResourceAuthorizationService authorizationService,
        string collectionId,
        string operation = "read",
        CancellationToken cancellationToken = default)
    {
        var result = await authorizationService.AuthorizeAsync(
            context.User,
            "collection",
            collectionId,
            operation,
            cancellationToken);

        if (!result.Succeeded)
        {
            return OgcProblemDetails.CreateForbiddenProblem(
                result.FailureReason ?? "Access to this collection is forbidden");
        }

        return null;
    }

    /// <summary>
    /// Filters a list of layer IDs to only include those the user is authorized to access.
    /// </summary>
    public static async Task<List<string>> FilterAuthorizedLayersAsync(
        ClaimsPrincipal user,
        IResourceAuthorizationService authorizationService,
        IEnumerable<string> layerIds,
        string operation = "read",
        CancellationToken cancellationToken = default)
    {
        var authorizedLayers = new List<string>();

        foreach (var layerId in layerIds)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                "layer",
                layerId,
                operation,
                cancellationToken);

            if (result.Succeeded)
            {
                authorizedLayers.Add(layerId);
            }
        }

        return authorizedLayers;
    }

    /// <summary>
    /// Filters a list of collection IDs to only include those the user is authorized to access.
    /// </summary>
    public static async Task<List<string>> FilterAuthorizedCollectionsAsync(
        ClaimsPrincipal user,
        IResourceAuthorizationService authorizationService,
        IEnumerable<string> collectionIds,
        string operation = "read",
        CancellationToken cancellationToken = default)
    {
        var authorizedCollections = new List<string>();

        foreach (var collectionId in collectionIds)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                "collection",
                collectionId,
                operation,
                cancellationToken);

            if (result.Succeeded)
            {
                authorizedCollections.Add(collectionId);
            }
        }

        return authorizedCollections;
    }
}
