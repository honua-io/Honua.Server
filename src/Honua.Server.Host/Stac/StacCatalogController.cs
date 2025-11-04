// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using Honua.Server.Host.Utilities;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Stac.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Stac;

/// <summary>
/// API controller for STAC (SpatioTemporal Asset Catalog) catalog operations.
/// </summary>
/// <remarks>
/// Provides endpoints for accessing the STAC catalog root and conformance information.
/// Implements the STAC API specification for geospatial asset discovery and metadata.
/// </remarks>
[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("stac")]
public sealed class StacCatalogController : ControllerBase
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly StacControllerHelper _helper;
    private readonly ILogger<StacCatalogController> _logger;
    private readonly StacMetrics _metrics;

    public StacCatalogController(IMetadataRegistry metadataRegistry, StacControllerHelper helper, ILogger<StacCatalogController> logger, StacMetrics metrics)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _helper = Guard.NotNull(helper);
        _logger = Guard.NotNull(logger);
        _metrics = Guard.NotNull(metrics);
    }

    /// <summary>
    /// Gets the STAC catalog root landing page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The STAC catalog root response with links to child catalogs and collections.</returns>
    /// <response code="200">Returns the STAC catalog root</response>
    /// <response code="404">STAC is not enabled in the configuration</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacRootResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<StacRootResponse>> GetRoot(CancellationToken cancellationToken)
    {
        _logger.LogInformation("STAC catalog root requested");

        if (!_helper.IsStacEnabled())
        {
            _logger.LogWarning("STAC catalog root request rejected: STAC is not enabled");
            return NotFound();
        }

        return await OperationInstrumentation.Create<ActionResult<StacRootResponse>>("STAC GetRoot")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("stac.operation", "GetRoot")
            .WithTag("operation", "get_root")
            .WithTag("resource", "catalog")
            .ExecuteAsync(async activity =>
            {
                var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var baseUri = _helper.BuildBaseUri(Request);

                var response = StacApiMapper.BuildRoot(snapshot.Catalog, baseUri);
                activity?.SetTag("catalog.id", response.Id);
                activity?.SetTag("catalog.title", response.Title);
                return Ok(response);
            });
    }

    /// <summary>
    /// Gets the STAC API conformance declaration.
    /// </summary>
    /// <returns>A list of OGC conformance class URIs that this API implements.</returns>
    /// <response code="200">Returns the conformance declaration</response>
    /// <response code="404">STAC is not enabled in the configuration</response>
    [HttpGet("conformance")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StacConformanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<StacConformanceResponse>> GetConformance()
    {
        _logger.LogInformation("STAC conformance declaration requested");

        if (!_helper.IsStacEnabled())
        {
            _logger.LogWarning("STAC conformance request rejected: STAC is not enabled");
            return NotFound();
        }

        return await OperationInstrumentation.Create<ActionResult<StacConformanceResponse>>("STAC GetConformance")
            .WithActivitySource(HonuaTelemetry.Stac)
            .WithLogger(_logger)
            .WithMetrics(_metrics.ReadOperationsCounter, _metrics.ReadOperationsCounter, _metrics.ReadOperationDuration)
            .WithTag("stac.operation", "GetConformance")
            .WithTag("operation", "get_conformance")
            .WithTag("resource", "conformance")
            .ExecuteAsync(async activity =>
            {
                var response = StacApiMapper.BuildConformance();
                activity?.SetTag("conformance.count", response.ConformsTo.Count);
                return await Task.FromResult(Ok(response));
            });
    }
}
