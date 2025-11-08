// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Raster.Export;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features query operations including search, parsing, and queryables.
/// </summary>
internal interface IOgcFeaturesQueryHandler
{
    /// <summary>
    /// Parses query parameters for OGC API Features items endpoint.
    /// </summary>
    (FeatureQuery Query, string ContentCrs, bool IncludeCount, IResult? Error) ParseItemsQuery(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        IQueryCollection? overrideQuery = null);

    /// <summary>
    /// Executes a cross-collection search operation.
    /// </summary>
    Task<IResult> ExecuteSearchAsync(
        HttpRequest request,
        IReadOnlyList<string> collections,
        IQueryCollection queryParameters,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        IOgcFeaturesAttachmentHandler attachmentHandler,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds queryables schema for a layer (JSON Schema describing available fields for filtering).
    /// </summary>
    object BuildQueryablesSchema(LayerDefinition layer);

    /// <summary>
    /// Converts layer extent to OGC API extent format.
    /// </summary>
    object? ConvertExtent(LayerExtentDefinition? extent);

    /// <summary>
    /// Builds ordered list of style IDs with default style first.
    /// </summary>
    IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer);
}
