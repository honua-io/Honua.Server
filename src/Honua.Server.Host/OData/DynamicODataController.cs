// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Host.OData.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries.Prepared;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OData;

/// <summary>
/// Dynamic OData controller for exposing feature data through the OData protocol.
/// Delegates to specialized services for query building, entity operations, and conversions.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = "RequireViewer")]
[ODataRouteComponent("odata")]
public sealed class DynamicODataController : ODataController
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IFeatureRepository _repository;
    private readonly IFeatureEditOrchestrator _editOrchestrator;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ODataMetadataResolver _metadataResolver;
    private readonly ODataQueryService _queryService;
    private readonly ODataEntityService _entityService;
    private readonly ODataGeometryService _geometryService;
    private readonly ODataConverterService _converterService;
    private readonly ILogger<DynamicODataController> _logger;

    private static readonly string[] ConcurrencyFieldCandidates =
    {
        "version", "Version", "VERSION",
        "_version",
        "rowversion", "RowVersion",
        "timestamp", "Timestamp", "TIMESTAMP",
        "lastModified", "LastModified",
        "modifiedDate", "ModifiedDate",
        "updated_at", "UpdatedAt"
    };

    public DynamicODataController(
        IMetadataRegistry metadataRegistry,
        IFeatureRepository repository,
        IFeatureEditOrchestrator editOrchestrator,
        IHonuaConfigurationService configurationService,
        ODataMetadataResolver metadataResolver,
        ODataQueryService queryService,
        ODataEntityService entityService,
        ODataGeometryService geometryService,
        ODataConverterService converterService,
        ILogger<DynamicODataController> logger)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _repository = Guard.NotNull(repository);
        _editOrchestrator = Guard.NotNull(editOrchestrator);
        _configurationService = Guard.NotNull(configurationService);
        _metadataResolver = Guard.NotNull(metadataResolver);
        _queryService = Guard.NotNull(queryService);
        _entityService = Guard.NotNull(entityService);
        _geometryService = Guard.NotNull(geometryService);
        _converterService = Guard.NotNull(converterService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData Get",
            [("odata.operation", "Get"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var totalStopwatch = Stopwatch.StartNew();
                var queryConstructionStopwatch = Stopwatch.StartNew();

                _logger.LogInformation("Entering DynamicODataController.Get for path {Path}", Request.Path);
                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);
                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));
                var (collectionType, entityType) = _metadataResolver.ResolveCollectionTypes(metadata);

                try
                {
                    var supportsGeoIntersects = SupportsGeoIntersects(metadata);
                    var pushDownGeoIntersects = await _queryService.ShouldPushDownGeoIntersectsAsync(metadata, _metadataRegistry, cancellationToken).ConfigureAwait(false);
                    Request.HttpContext.Items[ODataGeoFilterContext.GeoIntersectsPushdownKey] = pushDownGeoIntersects;

                    if (!supportsGeoIntersects && _queryService.HasGeoIntersectsFilter(Request))
                    {
                        return BadRequest("geo.intersects is not supported by this layer.");
                    }

                    var (queryOptions, featureQuery) = await _queryService.BuildFeatureQueryAsync(Request, metadata, entityType, feature.Path, cancellationToken).ConfigureAwait(false);
                    queryConstructionStopwatch.Stop();
                    activity.AddTag("odata.query_construction_ms", queryConstructionStopwatch.ElapsedMilliseconds);

                    var geoIntersectsInfo = Request.HttpContext.Items.TryGetValue(ODataGeoFilterContext.GeoIntersectsInfoKey, out var geoInfoObject) && geoInfoObject is GeoIntersectsFilterInfo info
                        ? info
                        : null;

                    var hasGeoFilter = geoIntersectsInfo is not null;
                    activity.AddTags(
                        ("odata.has_geo_filter", hasGeoFilter),
                        ("odata.supports_geo_intersects", supportsGeoIntersects),
                        ("odata.geo_pushdown_enabled", pushDownGeoIntersects));
                    var shouldApplyManualFilter = !(Request.HttpContext.Items.TryGetValue(
                            ODataGeoFilterContext.GeoIntersectsPushdownAppliedKey,
                            out var pushdownApplied) &&
                        pushdownApplied is true);
                    if (!shouldApplyManualFilter)
                    {
                        Request.HttpContext.Items.Remove(ODataGeoFilterContext.GeoIntersectsPushdownAppliedKey);
                    }

                    int? comparisonSrid = null;
                    if (geoIntersectsInfo is not null)
                    {
                        comparisonSrid = geoIntersectsInfo.Geometry.Srid ?? (featureQuery.Crs.HasValue()
                            ? CrsHelper.ParseCrs(featureQuery.Crs)
                            : (int?)CrsHelper.Wgs84);
                        if (comparisonSrid.HasValue)
                        {
                            geoIntersectsInfo.TargetSrid = comparisonSrid;
                        }
                    }

                    IPreparedGeometry? preparedFilter = null;
                    var collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(collectionType));

                    async Task PopulateAsync(FeatureQuery activeQuery, bool applyManualFilter)
                    {
                        if (applyManualFilter && geoIntersectsInfo is not null && preparedFilter is null)
                        {
                            var targetSrid = comparisonSrid ?? geoIntersectsInfo.StorageSrid ?? CrsHelper.Wgs84;
                            geoIntersectsInfo.TargetSrid = targetSrid;
                            preparedFilter = _geometryService.PrepareFilterGeometry(geoIntersectsInfo, targetSrid);
                            if (preparedFilter is null)
                            {
                                throw new ODataException("Unable to parse geo.intersects geometry literal.");
                            }
                        }

                        await foreach (var record in _repository.QueryAsync(
                                           metadata.Service.Id,
                                           metadata.Layer.Id,
                                           activeQuery,
                                           cancellationToken).ConfigureAwait(false))
                        {
                            if (applyManualFilter && geoIntersectsInfo is not null &&
                                !_geometryService.RecordIntersectsFilter(record, geoIntersectsInfo, preparedFilter!))
                            {
                                continue;
                            }

                            var entityObj = _entityService.CreateEntity(metadata, entityType, record, queryOptions);
                            collection.Add(entityObj);
                        }
                    }

                    if (queryOptions.SelectExpand?.SelectExpandClause is not null)
                    {
                        feature.SelectExpandClause = queryOptions.SelectExpand.SelectExpandClause;
                    }

                    if (_metadataResolver.IsCountRequest(feature.Path))
                    {
                        var countStopwatch = Stopwatch.StartNew();
                        var countQuery = featureQuery with { ResultType = FeatureResultType.Hits, Limit = null, Offset = null };
                        var count = await _repository.CountAsync(
                            metadata.Service.Id,
                            metadata.Layer.Id,
                            countQuery,
                            cancellationToken).ConfigureAwait(false);
                        countStopwatch.Stop();

                        activity.AddTags(
                            ("odata.is_count_request", true),
                            ("odata.count_query_ms", countStopwatch.ElapsedMilliseconds),
                            ("odata.total_count", count));

                        totalStopwatch.Stop();
                        activity.AddTag("odata.total_duration_ms", totalStopwatch.ElapsedMilliseconds);

                        return Ok(count);
                    }

                    var queryExecutionStopwatch = Stopwatch.StartNew();
                    var filterPushedDown = !shouldApplyManualFilter;

                    try
                    {
                        await PopulateAsync(featureQuery, shouldApplyManualFilter).ConfigureAwait(false);
                        queryExecutionStopwatch.Stop();
                    }
                    catch (NotSupportedException ex) when (geoIntersectsInfo is not null &&
                                                           ex.Message.Contains("geo.intersects", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(ex, "Geo.intersects pushdown not available; retrying with manual filtering.");
                        activity.AddTag("odata.geo_pushdown_fallback", true);

                        shouldApplyManualFilter = true;
                        filterPushedDown = false;
                        preparedFilter = null;
                        featureQuery = featureQuery with { Filter = null };
                        collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(collectionType));

                        queryExecutionStopwatch.Restart();
                        await PopulateAsync(featureQuery, shouldApplyManualFilter).ConfigureAwait(false);
                        queryExecutionStopwatch.Stop();
                    }

                    var resultCount = collection.Count;
                    activity.AddTags(
                        ("odata.query_execution_ms", queryExecutionStopwatch.ElapsedMilliseconds),
                        ("odata.result_count", resultCount),
                        ("odata.filter_pushdown", filterPushedDown));

                    if (queryOptions.Count?.Value == true)
                    {
                        var countQuery = featureQuery with { ResultType = FeatureResultType.Hits, Limit = null, Offset = null };
                        var total = await _repository.CountAsync(
                            metadata.Service.Id,
                            metadata.Layer.Id,
                            countQuery,
                            cancellationToken).ConfigureAwait(false);
                        feature.TotalCount = total;
                        activity.AddTag("odata.total_count", total);
                    }

                    totalStopwatch.Stop();
                    activity.AddTag("odata.total_duration_ms", totalStopwatch.ElapsedMilliseconds);

                    // Log slow queries
                    if (totalStopwatch.ElapsedMilliseconds > 1000)
                    {
                        _logger.LogWarning(
                            "Slow OData query: {Duration}ms for {ResultCount} results on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                            totalStopwatch.ElapsedMilliseconds,
                            resultCount,
                            metadata.EntitySetName,
                            metadata.Service.Id,
                            metadata.Layer.Id);
                    }

                    return Ok(collection);
                }
                catch (ODataException ex)
                {
                    _logger.LogWarning(ex, "Invalid OData query for entity set {EntitySet}.", metadata.EntitySet.Name);
                    return CreateODataError("InvalidQuery", ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning(ex, "Unsupported OData query for entity set {EntitySet}.", metadata.EntitySet.Name);
                    return CreateODataError("NotSupported", ex.Message);
                }
            });

    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData GetById",
            [("odata.operation", "GetById"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var stopwatch = Stopwatch.StartNew();

                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);

                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));

                var resolvedKey = key.HasValue()
                    ? key
                    : _metadataResolver.ResolveKeyFromRoute(Request, metadata, feature.Path);

                if (resolvedKey.IsNullOrWhiteSpace())
                {
                    return BadRequest("A feature key is required.");
                }

                activity.AddTag("odata.key", resolvedKey);

                try
                {
                    var (queryOptions, featureQuery) = await _queryService.BuildFeatureQueryAsync(Request, metadata, metadata.EntityType, feature.Path, cancellationToken).ConfigureAwait(false);

                    var queryStopwatch = Stopwatch.StartNew();
                    var record = await _repository.GetAsync(
                        metadata.Service.Id,
                        metadata.Layer.Id,
                        resolvedKey,
                        featureQuery,
                        cancellationToken).ConfigureAwait(false);
                    queryStopwatch.Stop();

                    activity.AddTag("odata.query_execution_ms", queryStopwatch.ElapsedMilliseconds);

                    if (record is null)
                    {
                        stopwatch.Stop();
                        activity.AddTags(
                            ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                            ("odata.found", false));
                        return NotFound();
                    }

                    stopwatch.Stop();
                    activity.AddTags(
                        ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                        ("odata.found", true));

                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        _logger.LogWarning(
                            "Slow OData GetById query: {Duration}ms for key {Key} on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                            stopwatch.ElapsedMilliseconds,
                            resolvedKey,
                            metadata.EntitySetName,
                            metadata.Service.Id,
                            metadata.Layer.Id);
                    }

                    var entity = _entityService.CreateEntity(metadata, metadata.EntityType, record, queryOptions);
                    var etag = ComputeStableEtag(record, metadata);
                    SetResponseEtag(etag);
                    return Ok(entity);
                }
                catch (ODataException ex)
                {
                    _logger.LogWarning(ex, "Invalid OData query for entity set {EntitySet}.", metadata.EntitySet.Name);
                    return CreateODataError("InvalidQuery", ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning(ex, "Unsupported OData query for entity set {EntitySet}.", metadata.EntitySet.Name);
                    return CreateODataError("NotSupported", ex.Message);
                }
            });

    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> Post([FromBody] EdmEntityObject entity, CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData Post",
            [("odata.operation", "Post"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var stopwatch = Stopwatch.StartNew();

                if (entity is null)
                {
                    return BadRequest("Payload is required.");
                }

                if (!GetODataConfiguration().AllowWrites)
                {
                    return WritesNotAllowed();
                }

                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);

                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));

                // BUG FIX #12: Layer editing configuration ignored by OData
                // Validate per-layer editing capabilities before allowing writes
                if (!metadata.Layer.Editing.Capabilities.AllowAdd)
                {
                    _logger.LogWarning(
                        "OData POST rejected: layer {LayerId} in service {ServiceId} does not allow adding features",
                        metadata.Layer.Id,
                        metadata.Service.Id);
                    return StatusCode(StatusCodes.Status403Forbidden, "This layer does not permit adding features.");
                }

                var entityType = metadata.EntityType;
                var record = _entityService.CreateRecord(metadata, entityType, entity, changedProperties: null, includeKey: true);

                // BUG FIX #13: Global-ID-required layers accept anonymous OData edits
                // Validate globalId requirements before inserting features
                if (metadata.Layer.Attachments.RequireGlobalIds)
                {
                    var hasGlobalId = record.Attributes.TryGetValue("globalId", out var gid1) && gid1 is not null ||
                                      record.Attributes.TryGetValue("GlobalId", out var gid2) && gid2 is not null ||
                                      record.Attributes.TryGetValue("GLOBALID", out var gid3) && gid3 is not null;

                    if (!hasGlobalId)
                    {
                        _logger.LogWarning(
                            "OData POST rejected: layer {LayerId} requires globalId but none provided",
                            metadata.Layer.Id);
                        return BadRequest("This layer requires a globalId field for all features.");
                    }
                }

                // BUG FIX #9: OData writes bypass feature edit orchestrator
                // Delegate to edit orchestrator for validation, auditing, and attachment orchestration
                var userRoles = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var command = new AddFeatureCommand(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    record.Attributes);

                var batch = new FeatureEditBatch(
                    new[] { command },
                    rollbackOnFailure: false,
                    clientReference: null,
                    isAuthenticated: User.Identity?.IsAuthenticated ?? false,
                    userRoles: userRoles);

                var createStopwatch = Stopwatch.StartNew();
                var result = await _editOrchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                createStopwatch.Stop();

                var commandResult = result.Results.FirstOrDefault();
                if (commandResult is null || !commandResult.Success)
                {
                    var error = commandResult?.Error ?? new FeatureEditError("unknown", "Edit operation failed");
                    _logger.LogWarning(
                        "OData POST failed: {ErrorCode} - {ErrorMessage}",
                        error.Code,
                        error.Message);
                    return BadRequest(new { error = error.Message });
                }

                // Retrieve the created feature to return
                var createdFeatureId = commandResult.FeatureId;
                if (createdFeatureId is null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Feature created but ID unavailable.");
                }

                var created = await _repository.GetAsync(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    createdFeatureId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (created is null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Feature created but cannot be retrieved.");
                }

                stopwatch.Stop();
                activity.AddTags(
                    ("odata.create_ms", createStopwatch.ElapsedMilliseconds),
                    ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds));

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning(
                        "Slow OData Post operation: {Duration}ms on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                        stopwatch.ElapsedMilliseconds,
                        metadata.EntitySetName,
                        metadata.Service.Id,
                        metadata.Layer.Id);
                }

                var createdEntity = _entityService.CreateEntity(metadata, entityType, created);
                var etag = ComputeStableEtag(created, metadata);
                SetResponseEtag(etag);
                return Created(createdEntity);
            });

    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> Put(string key, [FromBody] EdmEntityObject entity, CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData Put",
            [("odata.operation", "Put"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var stopwatch = Stopwatch.StartNew();

                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);

                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));

                var resolvedKey = key.HasValue()
                    ? key
                    : _metadataResolver.ResolveKeyFromRoute(Request, metadata, feature.Path);

                if (resolvedKey.IsNullOrWhiteSpace())
                {
                    return BadRequest("A feature key is required.");
                }

                activity.AddTag("odata.key", resolvedKey);

                if (entity is null)
                {
                    return BadRequest("Payload is required.");
                }

                if (!GetODataConfiguration().AllowWrites)
                {
                    return WritesNotAllowed();
                }

                // BUG FIX #12: Layer editing configuration ignored by OData
                // Validate per-layer editing capabilities before allowing writes
                if (!metadata.Layer.Editing.Capabilities.AllowUpdate)
                {
                    _logger.LogWarning(
                        "OData PUT rejected: layer {LayerId} in service {ServiceId} does not allow updating features",
                        metadata.Layer.Id,
                        metadata.Service.Id);
                    return StatusCode(StatusCodes.Status403Forbidden, "This layer does not permit updating features.");
                }

                // BUG FIX #11: No concurrency/ETag enforcement on OData updates
                // Check If-Match header for concurrency control
                string? rawIfMatch = null;
                if (Request.Headers.TryGetValue("If-Match", out var ifMatchValues) && ifMatchValues.Count > 0)
                {
                    rawIfMatch = ifMatchValues.FirstOrDefault();
                }

                var clientETag = NormalizeClientEtag(rawIfMatch);
                activity.AddTag("odata.etag_provided", clientETag is not null);

                if (clientETag is not null)
                {
                    var current = await _repository.GetAsync(
                        metadata.Service.Id,
                        metadata.Layer.Id,
                        resolvedKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (current is null)
                    {
                        return NotFound();
                    }

                    var currentETag = ComputeStableEtag(current, metadata);
                    if (!string.Equals(currentETag, clientETag, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "OData PUT rejected: ETag mismatch for feature {Key} in layer {LayerId}",
                            resolvedKey,
                            metadata.Layer.Id);
                        return StatusCode(StatusCodes.Status412PreconditionFailed, "The resource has been modified. Please refresh and retry.");
                    }
                }

                var entityType = metadata.EntityType;
                var record = _entityService.CreateRecord(metadata, entityType, entity, changedProperties: null, includeKey: true);

                // BUG FIX #13: Global-ID-required layers accept anonymous OData edits
                // Validate globalId requirements before updating features
                if (metadata.Layer.Attachments.RequireGlobalIds)
                {
                    var hasGlobalId = record.Attributes.TryGetValue("globalId", out var gid1) && gid1 is not null ||
                                      record.Attributes.TryGetValue("GlobalId", out var gid2) && gid2 is not null ||
                                      record.Attributes.TryGetValue("GLOBALID", out var gid3) && gid3 is not null;

                    if (!hasGlobalId)
                    {
                        _logger.LogWarning(
                            "OData PUT rejected: layer {LayerId} requires globalId but none provided",
                            metadata.Layer.Id);
                        return BadRequest("This layer requires a globalId field for all features.");
                    }
                }

                // BUG FIX #9: OData writes bypass feature edit orchestrator
                // Delegate to edit orchestrator for validation, auditing, and attachment orchestration
                var userRoles = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var command = new UpdateFeatureCommand(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    resolvedKey,
                    record.Attributes,
                    ETag: clientETag);

                var batch = new FeatureEditBatch(
                    new[] { command },
                    rollbackOnFailure: false,
                    clientReference: null,
                    isAuthenticated: User.Identity?.IsAuthenticated ?? false,
                    userRoles: userRoles);

                var updateStopwatch = Stopwatch.StartNew();
                var result = await _editOrchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                updateStopwatch.Stop();

                activity.AddTag("odata.update_ms", updateStopwatch.ElapsedMilliseconds);

                var commandResult = result.Results.FirstOrDefault();
                if (commandResult is null || !commandResult.Success)
                {
                    var error = commandResult?.Error ?? new FeatureEditError("unknown", "Edit operation failed");
                    _logger.LogWarning(
                        "OData PUT failed: {ErrorCode} - {ErrorMessage}",
                        error.Code,
                        error.Message);

                    if (error.Code == "not_found")
                    {
                        stopwatch.Stop();
                        activity.AddTags(
                            ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                            ("odata.found", false));
                        return NotFound();
                    }

                    return BadRequest(new { error = error.Message });
                }

                // Retrieve the updated feature to return
                var updated = await _repository.GetAsync(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    resolvedKey,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (updated is null)
                {
                    stopwatch.Stop();
                    activity.AddTags(
                        ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                        ("odata.found", false));
                    return NotFound();
                }

                stopwatch.Stop();
                activity.AddTags(
                    ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                    ("odata.found", true));

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning(
                        "Slow OData Put operation: {Duration}ms for key {Key} on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                        stopwatch.ElapsedMilliseconds,
                        resolvedKey,
                        metadata.EntitySetName,
                        metadata.Service.Id,
                        metadata.Layer.Id);
                }

                var updatedEntity = _entityService.CreateEntity(metadata, entityType, updated);
                var updatedEtag = ComputeStableEtag(updated, metadata);
                SetResponseEtag(updatedEtag);
                return Ok(updatedEntity);
            });

    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> Patch(string key, [FromBody] EdmEntityObject entity, CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData Patch",
            [("odata.operation", "Patch"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var stopwatch = Stopwatch.StartNew();

                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);

                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));

                var resolvedKey = key.HasValue()
                    ? key
                    : _metadataResolver.ResolveKeyFromRoute(Request, metadata, feature.Path);

                if (resolvedKey.IsNullOrWhiteSpace())
                {
                    return BadRequest("A feature key is required.");
                }

                activity.AddTag("odata.key", resolvedKey);

                if (entity is null)
                {
                    return BadRequest("Payload is required.");
                }

                if (!GetODataConfiguration().AllowWrites)
                {
                    return WritesNotAllowed();
                }

                // BUG FIX #12: Layer editing configuration ignored by OData
                // Validate per-layer editing capabilities before allowing writes
                if (!metadata.Layer.Editing.Capabilities.AllowUpdate)
                {
                    _logger.LogWarning(
                        "OData PATCH rejected: layer {LayerId} in service {ServiceId} does not allow updating features",
                        metadata.Layer.Id,
                        metadata.Service.Id);
                    return StatusCode(StatusCodes.Status403Forbidden, "This layer does not permit updating features.");
                }

                // BUG FIX #11: No concurrency/ETag enforcement on OData updates
                // Check If-Match header for concurrency control
                string? rawIfMatch = null;
                if (Request.Headers.TryGetValue("If-Match", out var ifMatchValues) && ifMatchValues.Count > 0)
                {
                    rawIfMatch = ifMatchValues.FirstOrDefault();
                }

                var clientETag = NormalizeClientEtag(rawIfMatch);
                activity.AddTag("odata.etag_provided", clientETag is not null);

                if (clientETag is not null)
                {
                    var current = await _repository.GetAsync(
                        metadata.Service.Id,
                        metadata.Layer.Id,
                        resolvedKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (current is null)
                    {
                        return NotFound();
                    }

                    var currentETag = ComputeStableEtag(current, metadata);
                    if (!string.Equals(currentETag, clientETag, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "OData PATCH rejected: ETag mismatch for feature {Key} in layer {LayerId}",
                            resolvedKey,
                            metadata.Layer.Id);
                        return StatusCode(StatusCodes.Status412PreconditionFailed, "The resource has been modified. Please refresh and retry.");
                    }
                }

                var entityType = metadata.EntityType;
                var changedProperties = _converterService.GetChangedPropertyNames(entity);
                activity.AddTag("odata.changed_properties_count", changedProperties?.Count ?? 0);
                var record = _entityService.CreateRecord(metadata, entityType, entity, changedProperties, includeKey: false);

                // BUG FIX #13: Global-ID-required layers accept anonymous OData edits
                // For PATCH, only validate globalId if it's being changed
                if (metadata.Layer.Attachments.RequireGlobalIds && changedProperties?.Any(p => p.Equals("globalId", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    var hasGlobalId = record.Attributes.TryGetValue("globalId", out var gid1) && gid1 is not null ||
                                      record.Attributes.TryGetValue("GlobalId", out var gid2) && gid2 is not null ||
                                      record.Attributes.TryGetValue("GLOBALID", out var gid3) && gid3 is not null;

                    if (!hasGlobalId)
                    {
                        _logger.LogWarning(
                            "OData PATCH rejected: layer {LayerId} requires globalId but provided value is null",
                            metadata.Layer.Id);
                        return BadRequest("This layer requires a globalId field for all features.");
                    }
                }

                // BUG FIX #9: OData writes bypass feature edit orchestrator
                // Delegate to edit orchestrator for validation, auditing, and attachment orchestration
                var userRoles = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var command = new UpdateFeatureCommand(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    resolvedKey,
                    record.Attributes,
                    ETag: clientETag);

                var batch = new FeatureEditBatch(
                    new[] { command },
                    rollbackOnFailure: false,
                    clientReference: null,
                    isAuthenticated: User.Identity?.IsAuthenticated ?? false,
                    userRoles: userRoles);

                var updateStopwatch = Stopwatch.StartNew();
                var result = await _editOrchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                updateStopwatch.Stop();

                activity.AddTag("odata.update_ms", updateStopwatch.ElapsedMilliseconds);

                var commandResult = result.Results.FirstOrDefault();
                if (commandResult is null || !commandResult.Success)
                {
                    var error = commandResult?.Error ?? new FeatureEditError("unknown", "Edit operation failed");
                    _logger.LogWarning(
                        "OData PATCH failed: {ErrorCode} - {ErrorMessage}",
                        error.Code,
                        error.Message);

                    if (error.Code == "not_found")
                    {
                        stopwatch.Stop();
                        activity.AddTags(
                            ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                            ("odata.found", false));
                        return NotFound();
                    }

                    return BadRequest(new { error = error.Message });
                }

                // Retrieve the updated feature to return
                var updated = await _repository.GetAsync(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    resolvedKey,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (updated is null)
                {
                    stopwatch.Stop();
                    activity.AddTags(
                        ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                        ("odata.found", false));
                    return NotFound();
                }

                stopwatch.Stop();
                activity.AddTags(
                    ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                    ("odata.found", true));

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning(
                        "Slow OData Patch operation: {Duration}ms for key {Key} on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                        stopwatch.ElapsedMilliseconds,
                        resolvedKey,
                        metadata.EntitySetName,
                        metadata.Service.Id,
                        metadata.Layer.Id);
                }

                var updatedEntity = _entityService.CreateEntity(metadata, entityType, updated);
                var updatedEtag = ComputeStableEtag(updated, metadata);
                SetResponseEtag(updatedEtag);
                return Ok(updatedEntity);
            });

    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OData,
            "OData Delete",
            [("odata.operation", "Delete"), ("odata.path", Request.Path.Value)],
            async activity =>
            {
                var stopwatch = Stopwatch.StartNew();

                var feature = Request.ODataFeature();
                var metadata = await _metadataResolver
                    .ResolveMetadataAsync(HttpContext, feature.Path, cancellationToken)
                    .ConfigureAwait(false);

                activity.AddTags(
                    ("odata.entity_set", metadata.EntitySetName),
                    ("odata.service_id", metadata.Service.Id),
                    ("odata.layer_id", metadata.Layer.Id));

                var resolvedKey = key.HasValue()
                    ? key
                    : _metadataResolver.ResolveKeyFromRoute(Request, metadata, feature.Path);

                if (resolvedKey.IsNullOrWhiteSpace())
                {
                    return BadRequest("A feature key is required.");
                }

                activity.AddTag("odata.key", resolvedKey);

                if (!GetODataConfiguration().AllowWrites)
                {
                    return WritesNotAllowed();
                }

                // BUG FIX #12: Layer editing configuration ignored by OData
                // Validate per-layer editing capabilities before allowing writes
                if (!metadata.Layer.Editing.Capabilities.AllowDelete)
                {
                    _logger.LogWarning(
                        "OData DELETE rejected: layer {LayerId} in service {ServiceId} does not allow deleting features",
                        metadata.Layer.Id,
                        metadata.Service.Id);
                    return StatusCode(StatusCodes.Status403Forbidden, "This layer does not permit deleting features.");
                }

                // BUG FIX #11: No concurrency/ETag enforcement on OData updates
                // Check If-Match header for concurrency control
                string? rawIfMatch = null;
                if (Request.Headers.TryGetValue("If-Match", out var ifMatchValues) && ifMatchValues.Count > 0)
                {
                    rawIfMatch = ifMatchValues.FirstOrDefault();
                }

                var clientETag = NormalizeClientEtag(rawIfMatch);
                activity.AddTag("odata.etag_provided", clientETag is not null);

                if (clientETag is not null)
                {
                    var current = await _repository.GetAsync(
                        metadata.Service.Id,
                        metadata.Layer.Id,
                        resolvedKey,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (current is null)
                    {
                        return NotFound();
                    }

                    var currentETag = ComputeStableEtag(current, metadata);
                    if (!string.Equals(currentETag, clientETag, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "OData DELETE rejected: ETag mismatch for feature {Key} in layer {LayerId}",
                            resolvedKey,
                            metadata.Layer.Id);
                        return StatusCode(StatusCodes.Status412PreconditionFailed, "The resource has been modified. Please refresh and retry.");
                    }
                }

                // BUG FIX #9: OData writes bypass feature edit orchestrator
                // Delegate to edit orchestrator for validation, auditing, and attachment orchestration
                var userRoles = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var command = new DeleteFeatureCommand(
                    metadata.Service.Id,
                    metadata.Layer.Id,
                    resolvedKey,
                    ETag: clientETag);

                var batch = new FeatureEditBatch(
                    new[] { command },
                    rollbackOnFailure: false,
                    clientReference: null,
                    isAuthenticated: User.Identity?.IsAuthenticated ?? false,
                    userRoles: userRoles);

                var deleteStopwatch = Stopwatch.StartNew();
                var result = await _editOrchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
                deleteStopwatch.Stop();

                activity.AddTag("odata.delete_ms", deleteStopwatch.ElapsedMilliseconds);

                var commandResult = result.Results.FirstOrDefault();
                if (commandResult is null || !commandResult.Success)
                {
                    var error = commandResult?.Error ?? new FeatureEditError("unknown", "Edit operation failed");
                    _logger.LogWarning(
                        "OData DELETE failed: {ErrorCode} - {ErrorMessage}",
                        error.Code,
                        error.Message);

                    if (error.Code == "not_found")
                    {
                        stopwatch.Stop();
                        activity.AddTags(
                            ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                            ("odata.found", false));
                        return NotFound();
                    }

                    return BadRequest(new { error = error.Message });
                }

                stopwatch.Stop();
                activity.AddTags(
                    ("odata.total_duration_ms", stopwatch.ElapsedMilliseconds),
                    ("odata.found", true));

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning(
                        "Slow OData Delete operation: {Duration}ms for key {Key} on {EntitySet} (service: {ServiceId}, layer: {LayerId})",
                        stopwatch.ElapsedMilliseconds,
                        resolvedKey,
                        metadata.EntitySetName,
                        metadata.Service.Id,
                        metadata.Layer.Id);
                }

                return NoContent();
            });

    private IActionResult WritesNotAllowed() =>
        StatusCode(StatusCodes.Status403Forbidden, "OData writes are disabled.");

    private bool SupportsGeoIntersects(ODataEntityMetadata metadata)
    {
        return metadata.Layer.GeometryField.HasValue();
    }

    private ODataConfiguration GetODataConfiguration() => _configurationService.Current.Services.OData;

    private IActionResult CreateODataError(string code, string message, string? target = null)
    {
        var error = new
        {
            error = new
            {
                code,
                message,
                target
            }
        };

        return BadRequest(error);
    }

    private static string? NormalizeClientEtag(string? candidate)
    {
        if (candidate.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (EntityTagHeaderValue.TryParse(candidate, out var parsed))
        {
            var tagValue = parsed.Tag.ToString();
            return tagValue.Trim('\"');
        }

        return candidate.Trim().Trim('\"');
    }

    private static string ComputeStableEtag(FeatureRecord record, ODataEntityMetadata metadata)
    {
        var versionToken = TryExtractVersionToken(record);
        if (versionToken.HasValue())
        {
            return EncodeEtagPayload(versionToken!);
        }

        var builder = new StringBuilder();
        AppendAttribute(builder, metadata.Layer.IdField, record);

        var orderedKeys = record.Attributes.Keys
            .Where(key => !string.Equals(key, metadata.Layer.IdField, StringComparison.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

        foreach (var key in orderedKeys)
        {
            AppendAttribute(builder, key, record);
        }

        return EncodeEtagPayload(builder.ToString());
    }

    private static void AppendAttribute(StringBuilder builder, string key, FeatureRecord record)
    {
        builder.Append(key);
        builder.Append('=');
        if (record.Attributes.TryGetValue(key, out var value) && value is not null)
        {
            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        builder.Append(';');
    }

    private static string? TryExtractVersionToken(FeatureRecord record)
    {
        foreach (var candidate in ConcurrencyFieldCandidates)
        {
            if (record.Attributes.TryGetValue(candidate, out var value) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static string EncodeEtagPayload(string payload)
    {
        var seed = payload ?? string.Empty;
        var bytes = Encoding.UTF8.GetBytes(seed);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private void SetResponseEtag(string? etag)
    {
        var headers = Response.GetTypedHeaders();
        if (etag.IsNullOrWhiteSpace())
        {
            headers.ETag = null;
            return;
        }

        headers.ETag = EntityTagHeaderValue.Parse($"\"{etag}\"");
    }
}
