// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features attachment operations.
/// Provides attachment link generation and validation for features.
/// Extracted from OgcSharedHandlers to enable dependency injection and testability.
/// </summary>
internal sealed class OgcFeaturesAttachmentHandler : IOgcFeaturesAttachmentHandler
{
    /// <inheritdoc />
    public bool ShouldExposeAttachmentLinks(ServiceDefinition service, LayerDefinition layer)
    {
        // Allow attachment links for both root-level and folder-based services
        // The FolderId check was preventing root collections from exposing attachment links
        return layer.Attachments.Enabled
            && layer.Attachments.ExposeOgcLinks;
    }

    /// <inheritdoc />
    public int ResolveLayerIndex(ServiceDefinition service, LayerDefinition layer)
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

    /// <inheritdoc />
    public Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
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

    /// <inheritdoc />
    public Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
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

    private async Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksCoreAsync(
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

    private string? BuildAttachmentHref(
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
}
