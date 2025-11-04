// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Attachments;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Gets a specific attachment for a feature.
    /// OGC API - Features /collections/{collectionId}/items/{featureId}/attachments/{attachmentId} endpoint.
    /// </summary>
    public static async Task<IResult> GetCollectionItemAttachment(
        string collectionId,
        string featureId,
        string attachmentId,
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IAttachmentStoreSelector attachmentStoreSelector,
        OgcCacheHeaderService cacheHeaderService,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(resolver);
        Guard.NotNull(attachmentOrchestrator);
        Guard.NotNull(attachmentStoreSelector);

        var (context, contextError) = await OgcSharedHandlers.TryResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (contextError is not null)
        {
            return contextError;
        }
        var service = context.Service;
        var layer = context.Layer;

        if (!layer.Attachments.Enabled || !layer.Attachments.ExposeOgcLinks)
        {
            return Results.NotFound();
        }

        var descriptor = await attachmentOrchestrator.GetAsync(service.Id, layer.Id, attachmentId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null || !string.Equals(descriptor.FeatureId, featureId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        // Use shared logger - in a minimal API context, we don't have access to ILogger directly,
        // but the helper logs internally. A NullLogger could be passed if needed.
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var downloadResult = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            layer.Attachments.StorageProfileId,
            attachmentStoreSelector,
            logger,
            service.Id,
            layer.Id,
            cancellationToken).ConfigureAwait(false);

        return await AttachmentDownloadHelper.ToResultAsync(downloadResult, cacheHeaderService, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds attachment links from pre-loaded attachment descriptors (avoids N+1 query pattern).
    /// </summary>
    private static IReadOnlyList<OgcLink> BuildAttachmentLinks(
        HttpRequest request,
        string collectionId,
        string featureId,
        IReadOnlyList<AttachmentDescriptor> descriptors)
    {
        var links = new List<OgcLink>(descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            var href = AttachmentUrlBuilder.BuildOgcUrl(request, collectionId, featureId, descriptor.AttachmentId);
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
