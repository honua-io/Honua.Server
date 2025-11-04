// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Gets all styles available in the system.
    /// OGC API - Styles endpoint.
    /// </summary>
    public static async Task<IResult> GetStyles(
        HttpRequest request,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var items = snapshot.Styles
            .OrderBy(style => style.Id, StringComparer.OrdinalIgnoreCase)
            .Select(style =>
            {
                var links = new List<OgcLink>
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{style.Id}", "self", "application/json", style.Title ?? style.Id, null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["f"] = null }),
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{style.Id}", "stylesheet", "application/vnd.ogc.sld+xml", style.Title ?? style.Id, null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["f"] = "sld" })
                };

                return new
                {
                    id = style.Id,
                    title = style.Title ?? style.Id,
                    geometryType = style.GeometryType,
                    renderer = style.Renderer,
                    links
                };
            })
            .ToArray();

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, "/ogc/styles", "self", "application/json", "Styles", null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["f"] = null }),
            OgcSharedHandlers.BuildLink(request, "/ogc", "alternate", "application/json", "Landing")
        };

        return Results.Ok(new { styles = items, links });
    }

    /// <summary>
    /// Gets a specific style by ID.
    /// OGC API - Styles endpoint.
    /// </summary>
    public static async Task<IResult> GetStyle(
        string styleId,
        HttpRequest request,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!snapshot.TryGetStyle(styleId, out var style))
        {
            return OgcProblemDetails.CreateNotFoundProblem($"Style '{styleId}' was not found.");
        }

        var format = request.Query.TryGetValue("f", out var formatValues)
            ? formatValues.ToString()
            : null;

        if (string.Equals(format, "sld", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = $"{FileNameHelper.SanitizeSegment(style.Id)}.sld";
            return OgcStyleResponseBuilder.CreateSldFileResponse(style, style.Title ?? style.Id, style.GeometryType, fileName);
        }

        var links = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{style.Id}", "self", "application/json", style.Title ?? style.Id, null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["f"] = null }),
            OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{style.Id}", "stylesheet", "application/vnd.ogc.sld+xml", style.Title ?? style.Id, null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["f"] = "sld" })
        };

        return Results.Ok(new
        {
            id = style.Id,
            title = style.Title ?? style.Id,
            geometryType = style.GeometryType,
            renderer = style.Renderer,
            rules = style.Rules,
            simple = style.Simple,
            uniqueValue = style.UniqueValue,
            links
        });
    }

    /// <summary>
    /// Gets all styles available for a specific collection.
    /// OGC API - Features collection styles endpoint.
    /// </summary>
    public static async Task<IResult> GetCollectionStyles(
        string collectionId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(metadataRegistry);

        var (context, error) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        var layer = context!.Layer;

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var orderedIds = new List<string>();
        if (layer.DefaultStyleId.HasValue())
        {
            orderedIds.Add(layer.DefaultStyleId);
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (styleId.HasValue())
            {
                orderedIds.Add(styleId);
            }
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var seen = new HashSet<string>(comparer);
        var entries = new List<object>();

        foreach (var id in orderedIds)
        {
            if (!seen.Add(id))
            {
                continue;
            }

            var isDefault = layer.DefaultStyleId.HasValue() && comparer.Equals(layer.DefaultStyleId, id);
            StyleDefinition? style = null;
            if (snapshot.TryGetStyle(id, out var resolved))
            {
                style = resolved;
            }

            var links = new List<OgcLink>
            {
                OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/styles/{id}", "stylesheet", "application/vnd.ogc.sld+xml", style?.Title ?? id)
            };

            entries.Add(new
            {
                id = style?.Id ?? id,
                title = style?.Title ?? id,
                isDefault,
                geometryType = style?.GeometryType ?? layer.GeometryType,
                renderer = style?.Renderer ?? "simple",
                links
            });
        }

        var responseLinks = new List<OgcLink>
        {
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/styles", "self", "application/json", $"Styles for {layer.Title ?? collectionId}"),
            OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title ?? collectionId)
        };

        return Results.Ok(new
        {
            collectionId,
            defaultStyle = layer.DefaultStyleId,
            styles = entries,
            links = responseLinks
        });
    }

    /// <summary>
    /// Gets a specific style for a collection, typically as SLD XML.
    /// OGC API - Features collection style endpoint.
    /// </summary>
    public static async Task<IResult> GetCollectionStyle(
        string collectionId,
        string styleId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(registry);

        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var layer = context.Layer;
        var style = await OgcSharedHandlers.ResolveStyleDefinitionAsync(styleId, layer, registry, cancellationToken).ConfigureAwait(false);
        if (style is null)
        {
            return OgcProblemDetails.CreateNotFoundProblem($"Style '{styleId}' is not defined for collection '{collectionId}'.");
        }

        var geometry = layer.GeometryType.IsNullOrWhiteSpace() ? style.GeometryType : layer.GeometryType;
        var fileName = FileNameHelper.BuildDownloadFileName(collectionId, style.Id, "sld");
        return OgcStyleResponseBuilder.CreateSldFileResponse(style, layer.Title ?? layer.Id, geometry, fileName);
    }
}
