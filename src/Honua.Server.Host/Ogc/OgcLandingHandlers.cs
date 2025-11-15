// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// OGC API landing page handlers implemented with the async minimal API pattern to keep request threads unblocked.
/// </summary>
internal static class OgcLandingHandlers
{
    /// <summary>
    /// OGC API landing page handler.
    /// </summary>
    public static Task<IResult> GetLanding(
        HttpRequest request,
        [FromServices] Services.IOgcLandingPageService landingPageService,
        CancellationToken cancellationToken = default)
    {
        return landingPageService.GetLandingPageAsync(request, cancellationToken);
    }

    public static Task<IResult> GetApiDefinition(
        [FromServices] Services.IOgcLandingPageService landingPageService,
        CancellationToken cancellationToken = default)
    {
        return landingPageService.GetApiDefinitionAsync(cancellationToken);
    }

    /// <summary>
    /// OGC API conformance handler.
    /// </summary>
    public static Task<IResult> GetConformance(
        [FromServices] Services.IOgcConformanceService conformanceService,
        CancellationToken cancellationToken = default)
    {
        return conformanceService.GetConformanceAsync(cancellationToken);
    }

    /// <summary>
    /// OGC API collections handler with response caching.
    /// </summary>
    /// <remarks>
    /// Implements caching strategy from PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md:
    /// - Cache key: ogc:collections:{service_id}:{format}:{accept_language}
    /// - TTL: 10 minutes (configurable)
    /// - Invalidated on metadata updates
    /// - Separate caching for JSON and HTML formats
    /// - Language-aware for i18n support
    /// </remarks>
    public static Task<IResult> GetCollections(
        HttpRequest request,
        [FromServices] Services.IOgcCollectionService collectionService,
        CancellationToken cancellationToken = default)
    {
        return collectionService.GetCollectionsAsync(request, cancellationToken);
    }

    public static Task<IResult> GetCollection(
        string collectionId,
        HttpRequest request,
        [FromServices] Services.IOgcCollectionService collectionService,
        CancellationToken cancellationToken)
    {
        return collectionService.GetCollectionAsync(collectionId, request, cancellationToken);
    }
}
