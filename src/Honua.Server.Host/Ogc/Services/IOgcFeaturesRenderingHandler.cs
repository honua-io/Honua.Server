// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for rendering OGC API responses as HTML and formatting feature data.
/// </summary>
internal interface IOgcFeaturesRenderingHandler
{
    /// <summary>
    /// Checks if the request prefers HTML response.
    /// </summary>
    bool WantsHtml(HttpRequest request);

    /// <summary>
    /// Renders the OGC API landing page as HTML.
    /// </summary>
    string RenderLandingHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcLink> links);

    /// <summary>
    /// Renders the collections list as HTML.
    /// </summary>
    string RenderCollectionsHtml(HttpRequest request, MetadataSnapshot snapshot, IReadOnlyList<OgcSharedHandlers.CollectionSummary> collections);

    /// <summary>
    /// Renders a single collection page as HTML.
    /// </summary>
    string RenderCollectionHtml(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId,
        IReadOnlyList<string> crs,
        IReadOnlyList<OgcLink> links);

    /// <summary>
    /// Renders a feature collection (multiple features) as HTML.
    /// </summary>
    string RenderFeatureCollectionHtml(
        string title,
        string? subtitle,
        IReadOnlyList<OgcSharedHandlers.HtmlFeatureEntry> features,
        long? numberMatched,
        long numberReturned,
        string? contentCrs,
        IReadOnlyList<OgcLink> links,
        bool hitsOnly);

    /// <summary>
    /// Renders a single feature as HTML.
    /// </summary>
    string RenderFeatureHtml(
        string title,
        string? description,
        OgcSharedHandlers.HtmlFeatureEntry entry,
        string? contentCrs,
        IReadOnlyList<OgcLink> links);

    /// <summary>
    /// Formats a property value for display.
    /// </summary>
    string FormatPropertyValue(object? value);

    /// <summary>
    /// Formats a geometry value for display.
    /// </summary>
    string? FormatGeometryValue(object? geometry);
}
