// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains attachment handling methods.

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
    private static string? BuildAttachmentHref(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        AttachmentDescriptor descriptor)
    {
        if (descriptor is null)
        {
            return null;
        }

        var featureId = descriptor.FeatureId.HasValue()
            ? descriptor.FeatureId
            : components.FeatureId;

        if (featureId.HasValue() && descriptor.AttachmentObjectId > 0)
        {
            var layerIndex = ResolveLayerIndex(service, layer);
            if (layerIndex >= 0 &&
                long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                return AttachmentUrlBuilder.BuildGeoServicesUrl(
                    request,
                    service.FolderId,
                    service.Id,
                    layerIndex,
                    objectId,
                    descriptor.AttachmentObjectId,
                    includeRootFolder: true);
            }
        }

        if (featureId.IsNullOrWhiteSpace() || descriptor.AttachmentId.IsNullOrWhiteSpace())
        {
            return null;
        }

        return AttachmentUrlBuilder.BuildOgcUrl(request, collectionId, featureId!, descriptor.AttachmentId);
    }

    internal static object? ConvertExtent(LayerExtentDefinition? extent)
    {
        if (extent is null)
        {
            return null;
        }

        object? spatial = null;
        if (extent.Bbox.Count > 0 || extent.Crs.HasValue())
        {
            spatial = new
            {
                bbox = extent.Bbox,
                crs = extent.Crs.IsNullOrWhiteSpace()
                    ? CrsHelper.DefaultCrsIdentifier
                    : CrsHelper.NormalizeIdentifier(extent.Crs)
            };
        }

        var hasIntervals = extent.Temporal.Count > 0;
        var intervals = hasIntervals
            ? extent.Temporal
                .Select(t => new[] { t.Start?.ToString("O"), t.End?.ToString("O") })
                .ToArray()
            : Array.Empty<string?[]>();

        object? temporal = null;
        if (hasIntervals || extent.TemporalReferenceSystem.HasValue())
        {
            temporal = new
            {
                interval = intervals,
                trs = extent.TemporalReferenceSystem.IsNullOrWhiteSpace()
                    ? DefaultTemporalReferenceSystem
                    : extent.TemporalReferenceSystem
            };
        }

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new
        {
            spatial,
            temporal
        };
    }

    internal static object ToFeature(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null)
    {
        var components = componentsOverride ?? FeatureComponentBuilder.BuildComponents(layer, record, query);

        var links = BuildFeatureLinks(request, collectionId, layer, components, additionalLinks);

        var properties = new Dictionary<string, object?>(components.Properties, StringComparer.OrdinalIgnoreCase);
        AppendStyleMetadata(properties, layer);

        return new
        {
            type = "Feature",
            id = components.RawId,
            geometry = components.Geometry,
            properties,
            links
        };
    }

    internal static IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks)
    {
        var links = new List<OgcLink>();
        if (components.FeatureId.HasValue())
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/items/{components.FeatureId}", "self", "application/geo+json", $"Feature {components.FeatureId}"));
        }

        links.Add(BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title));

        if (additionalLinks is not null)
        {
            links.AddRange(additionalLinks);
        }

        return links;
    }

    internal static bool ShouldExposeAttachmentLinks(ServiceDefinition service, LayerDefinition layer)
    {
        // Allow attachment links for both root-level and folder-based services
        // The FolderId check was preventing root collections from exposing attachment links
        return layer.Attachments.Enabled
            && layer.Attachments.ExposeOgcLinks;
    }

    internal static int ResolveLayerIndex(ServiceDefinition service, LayerDefinition layer)
    {
        if (service.Layers is null)
        {
            return -1;
        }

        for (var index = 0; index < service.Layers.Count; index++)
        {
            if (string.Equals(service.Layers[index].Id, layer.Id, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    internal static Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        CancellationToken cancellationToken)
        => CreateAttachmentLinksCoreAsync(
            request,
            service,
            layer,
            collectionId,
            components,
            attachmentOrchestrator,
            preloadedDescriptors: null,
            cancellationToken);

    internal static Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IReadOnlyList<AttachmentDescriptor> preloadedDescriptors,
        CancellationToken cancellationToken)
        => CreateAttachmentLinksCoreAsync(
            request,
            service,
            layer,
            collectionId,
            components,
            attachmentOrchestrator,
            preloadedDescriptors,
            cancellationToken);

    private static async Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksCoreAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IReadOnlyList<AttachmentDescriptor>? preloadedDescriptors,
        CancellationToken cancellationToken)
    {
        if (components.FeatureId.IsNullOrWhiteSpace())
        {
            return Array.Empty<OgcLink>();
        }

        IReadOnlyList<AttachmentDescriptor>? descriptors = preloadedDescriptors;
        if (descriptors is null)
        {
            descriptors = await attachmentOrchestrator
                .ListAsync(service.Id, layer.Id, components.FeatureId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (descriptors.Count == 0)
        {
            return Array.Empty<OgcLink>();
        }

        var links = new List<OgcLink>(descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            var href = BuildAttachmentHref(request, service, layer, collectionId, components, descriptor);
            if (href is null)
            {
                continue;
            }

            var title = descriptor.Name.IsNullOrWhiteSpace()
                ? $"Attachment {descriptor.AttachmentObjectId}"
                : descriptor.Name;
            var type = descriptor.MimeType.IsNullOrWhiteSpace()
                ? "application/octet-stream"
                : descriptor.MimeType;
            links.Add(new OgcLink(href, "enclosure", type, title));
        }

        return links.Count == 0 ? Array.Empty<OgcLink>() : links;
    }
}
