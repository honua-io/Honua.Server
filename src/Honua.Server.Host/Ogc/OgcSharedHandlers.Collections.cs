// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains collection resolution methods.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static async Task<Result<FeatureContext>> ResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        if (!TryParseCollectionId(collectionId, out var serviceId, out var layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        // Security: Validate inputs to prevent path traversal and injection attacks
        if (ContainsDangerousCharacters(serviceId) || ContainsDangerousCharacters(layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        FeatureContext context;
        try
        {
            context = await resolver.ResolveAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            var message = ex.Message.IsNullOrWhiteSpace()
                ? $"Collection '{collectionId}' was not found."
                : ex.Message;
            return Result<FeatureContext>.Failure(Error.NotFound(message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }
        catch (Exception)
        {
            // Catch all other exceptions and return NotFound to avoid exposing internal errors
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (context == null)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (!context.Service.Enabled || !context.Service.Ogc.CollectionsEnabled)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' is not available."));
        }

        return Result<FeatureContext>.Success(context);
    }

    private static bool ContainsDangerousCharacters(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return false;
        }

        // Check for path traversal attempts
        if (value.Contains("..") || value.Contains("/") || value.Contains("\\"))
        {
            return true;
        }

        // Check for SQL injection attempts
        if (value.Contains("'") || value.Contains("--") || value.Contains(";"))
        {
            return true;
        }

        // Check for XML/HTML injection
        if (value.Contains("<") || value.Contains(">"))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseCollectionId(string collectionId, out string serviceId, out string layerId)
    {
        serviceId = string.Empty;
        layerId = string.Empty;

        if (collectionId.IsNullOrWhiteSpace())
        {
            return false;
        }

        var parts = collectionId.Split(CollectionIdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        serviceId = parts[0];
        layerId = parts[1];
        return true;
    }

    internal static IResult MapCollectionResolutionError(Error error, string collectionId)
    {
        return error.Code switch
        {
            "not_found" => CreateNotFoundProblem(error.Message ?? $"Collection '{collectionId}' was not found."),
            "invalid" => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed"),
            _ => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed")
        };
    }

    /// <summary>
    /// Resolves a collection and returns either the context or an error result.
    /// This consolidates the common pattern of calling ResolveCollectionAsync and mapping errors.
    /// </summary>
    /// <returns>
    /// A tuple containing either (FeatureContext, null) on success or (null, IResult) on failure.
    /// </returns>
    internal static async Task<(FeatureContext? Context, IResult? Error)> TryResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return (null, MapCollectionResolutionError(resolution.Error!, collectionId));
        }

        return (resolution.Value, null);
    }

    internal static string BuildCollectionId(ServiceDefinition service, LayerDefinition layer)
        => $"{service.Id}{CollectionIdSeparator}{layer.Id}";

    internal static string BuildLayerGroupCollectionId(ServiceDefinition service, LayerGroupDefinition layerGroup)
        => $"{service.Id}{CollectionIdSeparator}{layerGroup.Id}";

    internal static List<OgcLink> BuildLayerGroupCollectionLinks(HttpRequest request, ServiceDefinition service, LayerGroupDefinition layerGroup, string collectionId)
    {
        var links = new List<OgcLink>(layerGroup.Links.Select(ToLink));
        links.AddRange(new[]
        {
            BuildLink(request, $"/ogc/collections/{collectionId}", "self", "application/json", "This collection"),
            BuildLink(request, $"/ogc/collections/{collectionId}", "alternate", "text/html", "This collection as HTML"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "items", "application/geo+json", "Features in this layer group"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "http://www.opengis.net/def/rel/ogc/1.0/items", "application/geo+json", "Features")
        });

        if (layerGroup.StyleIds.Count > 0)
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles", "styles", "application/json", "Styles for this layer group"));
        }

        return links;
    }

    internal static IReadOnlyList<string> BuildOrderedStyleIds(LayerGroupDefinition layerGroup)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layerGroup.DefaultStyleId.HasValue() && seen.Add(layerGroup.DefaultStyleId))
        {
            results.Add(layerGroup.DefaultStyleId);
        }

        foreach (var styleId in layerGroup.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results;
    }

    internal static IResult CreateValidationProblem(string detail, string parameter)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request parameter",
            Detail = detail,
            Extensions = { ["parameter"] = parameter }
        };

        return Results.Problem(problemDetails.Detail, statusCode: problemDetails.Status, title: problemDetails.Title, extensions: problemDetails.Extensions);
    }

    internal static IResult CreateNotFoundProblem(string detail)
    {
        return Results.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
    }
}
