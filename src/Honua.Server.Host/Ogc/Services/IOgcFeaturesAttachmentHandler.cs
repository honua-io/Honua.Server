// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features attachment operations.
/// Provides attachment link generation and validation for features.
/// </summary>
internal interface IOgcFeaturesAttachmentHandler
{
    /// <summary>
    /// Determines whether attachment links should be exposed for a given service and layer.
    /// </summary>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition</param>
    /// <returns>True if attachments should be exposed, false otherwise</returns>
    bool ShouldExposeAttachmentLinks(ServiceDefinition service, LayerDefinition layer);

    /// <summary>
    /// Creates attachment links for a feature by fetching attachment descriptors.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="collectionId">OGC collection ID</param>
    /// <param name="components">Feature components containing feature ID</param>
    /// <param name="attachmentOrchestrator">Attachment orchestrator for fetching descriptors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of attachment links</returns>
    Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates attachment links for a feature using preloaded attachment descriptors.
    /// Avoids additional database queries when descriptors are already available.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition</param>
    /// <param name="collectionId">OGC collection ID</param>
    /// <param name="components">Feature components containing feature ID</param>
    /// <param name="attachmentOrchestrator">Attachment orchestrator (unused but kept for signature compatibility)</param>
    /// <param name="preloadedDescriptors">Preloaded attachment descriptors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of attachment links</returns>
    Task<IReadOnlyList<OgcLink>> CreateAttachmentLinksAsync(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        FeatureComponents components,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IReadOnlyList<AttachmentDescriptor> preloadedDescriptors,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the layer index within a service definition.
    /// Used for building ArcGIS REST API attachment URLs.
    /// </summary>
    /// <param name="service">Service definition</param>
    /// <param name="layer">Layer definition</param>
    /// <returns>Layer index or -1 if not found</returns>
    int ResolveLayerIndex(ServiceDefinition service, LayerDefinition layer);
}
