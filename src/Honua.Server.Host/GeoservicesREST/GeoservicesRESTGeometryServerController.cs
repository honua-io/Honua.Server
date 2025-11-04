// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Geoservices.GeometryService;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.GeoservicesREST;

[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services/Geometry/GeometryServer")]
public sealed class GeoservicesRESTGeometryServerController : ControllerBase
{
    private const int OperationTimeoutSeconds = 30;

    private readonly IHonuaConfigurationService _configurationService;
    private readonly IGeometrySerializer _serializer;
    private readonly IGeometryOperationExecutor _executor;
    private readonly ILogger<GeoservicesRESTGeometryServerController> _logger;

    public GeoservicesRESTGeometryServerController(
        IHonuaConfigurationService configurationService,
        IGeometrySerializer serializer,
        IGeometryOperationExecutor executor,
        ILogger<GeoservicesRESTGeometryServerController> logger)
    {
        _configurationService = Guard.NotNull(configurationService);
        _serializer = Guard.NotNull(serializer);
        _executor = Guard.NotNull(executor);
        _logger = Guard.NotNull(logger);
    }

    [HttpGet]
    public IActionResult GetService()
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        var response = new
        {
            currentVersion = 10.81,
            serviceDescription = "Geometry Service for spatial operations",
            operations = new[]
            {
                "project",
                "buffer",
                "simplify",
                "densify",
                "generalize",
                "union",
                "intersect",
                "difference",
                "convexHull",
                "distance",
                "areasAndLengths",
                "offset",
                "trimExtend",
                "labelPoints",
                "cut",
                "reshape"
            }
        };

        return Ok(response);
    }

    [HttpPost("project")]
    public async Task<IActionResult> Project([FromBody] GeometryProjectRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        if (!geometrySettings.EnableGdalOperations)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Projection operation is disabled in this environment." });
        }

        var format = ResolveFormat(request.Format);
        if (!format.EqualsIgnoreCase("json"))
        {
            return BadRequest(new { error = $"Format '{format}' is not supported." });
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Project")
                .WithTag("arcgis.operation", "Project")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request);
                    var inputSrid = ResolveInputSpatialReference(request);
                    var outputSrid = ResolveOutputSpatialReference(request);
                    var geometriesPayload = ResolveGeometriesPayload(request);

                    activity.AddTag("arcgis.input_srid", inputSrid);
                    activity.AddTag("arcgis.output_srid", outputSrid);

                    var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, inputSrid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    var result = _executor.Project(new GeometryProjectOperation(geometryType, inputSrid, outputSrid, geometries), cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, outputSrid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for project operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Project operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "project operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "project operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Project operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    private string ResolveFormat(string? format)
    {
        if (format.HasValue())
        {
            return format;
        }

        if (Request.Query.TryGetValue("f", out var values) && values.Count > 0)
        {
            var candidate = values[^1];
            if (candidate.HasValue())
            {
                return candidate;
            }
        }

        return "json";
    }

    private static string ResolveGeometryType(GeometryProjectRequest request)
    {
        if (request.GeometryType.HasValue())
        {
            return request.GeometryType;
        }

        if (request.Geometries is JsonObject obj &&
            obj.TryGetPropertyValue("geometryType", out var typeNode) &&
            typeNode is JsonValue typeValue &&
            typeValue.TryGetValue<string>(out var embeddedType) &&
            embeddedType.HasValue())
        {
            return embeddedType;
        }

        throw new GeometrySerializationException("geometryType must be specified.");
    }

    private int ResolveInputSpatialReference(GeometryProjectRequest request)
    {
        if (TryResolveSpatialReference(request.InSpatialReference, request.InSr, Request.Query["inSR"], request.Geometries, out var srid))
        {
            return srid;
        }

        throw new GeometrySerializationException("Input spatial reference (inSR) must be specified.");
    }

    private int ResolveOutputSpatialReference(GeometryProjectRequest request)
    {
        if (TryResolveSpatialReference(request.OutSpatialReference, request.OutSr, Request.Query["outSR"], request.Geometries, out var srid))
        {
            return srid;
        }

        throw new GeometrySerializationException("Output spatial reference (outSR) must be specified.");
    }

    private static bool TryResolveSpatialReference(
        GeoservicesRESTSpatialReference? explicitSpatialReference,
        JsonElement numericOrString,
        StringValues queryValues,
        JsonNode? payload,
        out int srid)
    {
        if (explicitSpatialReference is not null && explicitSpatialReference.Wkid > 0)
        {
            srid = explicitSpatialReference.Wkid;
            return true;
        }

        if (TryParseSpatialReference(numericOrString, out srid))
        {
            return true;
        }

        if (TryParseSpatialReference(queryValues, out srid))
        {
            return true;
        }

        if (payload is JsonObject obj &&
            obj.TryGetPropertyValue("spatialReference", out var srNode) &&
            TryParseSpatialReference(srNode, out srid))
        {
            return true;
        }

        srid = default;
        return false;
    }

    private static bool TryParseSpatialReference(JsonElement element, out int srid)
    {
        srid = default;

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out srid) => srid > 0,
            JsonValueKind.String when int.TryParse(element.GetString(), out srid) && srid > 0 => true,
            _ => false
        };
    }

    private static bool TryParseSpatialReference(StringValues values, out int srid)
    {
        srid = default;
        if (values.Count == 0)
        {
            return false;
        }

        for (var i = values.Count - 1; i >= 0; i--)
        {
            var value = values[i];
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            if (int.TryParse(value, out srid) && srid > 0)
            {
                return true;
            }
        }

        srid = default;
        return false;
    }

    private static bool TryParseSpatialReference(JsonNode? node, out int srid)
    {
        srid = default;
        if (node is JsonObject obj &&
            obj.TryGetPropertyValue("wkid", out var wkidNode) &&
            wkidNode is JsonValue wkidValue &&
            wkidValue.TryGetValue<int>(out var wkid) && wkid > 0)
        {
            srid = wkid;
            return true;
        }

        return false;
    }

    private JsonNode ResolveGeometriesPayload(GeometryProjectRequest request)
    {
        if (request.Geometries is not null)
        {
            return request.Geometries;
        }

        if (Request.Query.TryGetValue("geometries", out var values) && values.Count > 0)
        {
            var candidate = values[^1];
            if (candidate.HasValue())
            {
                try
                {
                    return JsonNode.Parse(candidate) ?? throw new JsonException();
                }
                catch (JsonException ex)
                {
                    throw new GeometrySerializationException("geometries parameter is not valid JSON.", ex);
                }
            }
        }

        throw new GeometrySerializationException("geometries payload is required.");
    }

    [HttpPost("buffer")]
    public async Task<IActionResult> Buffer([FromBody] GeometryBufferRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Buffer")
                .WithTag("arcgis.operation", "Buffer")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
                    var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
                    var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    activity.AddTag("arcgis.distance", request.Distance ?? 0);
                    activity.AddTag("arcgis.unit", request.Unit ?? "meter");

                    var operation = new GeometryBufferOperation(
                        geometryType,
                        srid,
                        geometries,
                        request.Distance ?? 0,
                        request.Unit ?? "meter",
                        request.UnionResults ?? false);

                    var result = _executor.Buffer(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for buffer operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Buffer operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "buffer operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "buffer operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Buffer operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("simplify")]
    public IActionResult Simplify([FromBody] GeometrySimplifyRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySimplifyOperation(geometryType, srid, geometries);
            var result = _executor.Simplify(operation, cts.Token);
            var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for simplify operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Simplify operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "simplify operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "simplify operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Simplify operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("densify")]
    public async Task<IActionResult> Densify([FromBody] GeometryDensifyRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Densify")
                .WithTag("arcgis.operation", "Densify")
                .ExecuteAsync<IActionResult>(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
                    var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
                    var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    if (!request.MaxSegmentLength.HasValue || request.MaxSegmentLength.Value <= 0)
                    {
                        return BadRequest(new { error = "maxSegmentLength must be a positive value." });
                    }

                    activity.AddTag("arcgis.maxSegmentLength", request.MaxSegmentLength.Value);

                    var operation = new GeometryDensifyOperation(geometryType, srid, geometries, request.MaxSegmentLength.Value);
                    var result = _executor.Densify(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for densify operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Densify operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "densify operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "densify operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Densify operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("generalize")]
    public async Task<IActionResult> Generalize([FromBody] GeometryGeneralizeRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Generalize")
                .WithTag("arcgis.operation", "Generalize")
                .ExecuteAsync<IActionResult>(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
                    var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
                    var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    if (!request.MaxDeviation.HasValue || request.MaxDeviation.Value <= 0)
                    {
                        return BadRequest(new { error = "maxDeviation must be a positive value." });
                    }

                    activity.AddTag("arcgis.maxDeviation", request.MaxDeviation.Value);

                    var operation = new GeometryGeneralizeOperation(geometryType, srid, geometries, request.MaxDeviation.Value);
                    var result = _executor.Generalize(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for generalize operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Generalize operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "generalize operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "generalize operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Generalize operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("union")]
    public IActionResult Union([FromBody] GeometrySetRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySetOperation(geometryType, srid, geometries);
            var result = _executor.Union(operation, cts.Token);

            if (result is null)
            {
                return Ok(new { geometry = (object?)null });
            }

            var response = _serializer.SerializeGeometry(result, geometryType, srid);
            return Ok(new { geometry = response });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for union operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Union operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "union operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "union operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Union operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("intersect")]
    public IActionResult Intersect([FromBody] GeometryPairwiseRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries1);
            var geometries1Payload = request.Geometries1 ?? throw new GeometrySerializationException("geometries1 payload is required.");
            var geometries2Payload = request.Geometries2 ?? throw new GeometrySerializationException("geometries2 payload is required.");

            var geometries1 = _serializer.DeserializeGeometries(geometries1Payload, geometryType, srid, cts.Token);
            var geometries2 = _serializer.DeserializeGeometries(geometries2Payload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries1);
            GeometryComplexityValidator.ValidateCollection(geometries2);

            var operation = new GeometryPairwiseOperation(geometryType, srid, geometries1, geometries2);
            var result = _executor.Intersect(operation, cts.Token);
            var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for intersect operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Intersect operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "intersect operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "intersect operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Intersect operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("difference")]
    public IActionResult Difference([FromBody] GeometryPairwiseRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries1);
            var geometries1Payload = request.Geometries1 ?? throw new GeometrySerializationException("geometries1 payload is required.");
            var geometries2Payload = request.Geometries2 ?? throw new GeometrySerializationException("geometries2 payload is required.");

            var geometries1 = _serializer.DeserializeGeometries(geometries1Payload, geometryType, srid, cts.Token);
            var geometries2 = _serializer.DeserializeGeometries(geometries2Payload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries1);
            GeometryComplexityValidator.ValidateCollection(geometries2);

            var operation = new GeometryPairwiseOperation(geometryType, srid, geometries1, geometries2);
            var result = _executor.Difference(operation, cts.Token);
            var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for difference operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("Difference operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "difference operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "difference operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Difference operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("convexHull")]
    public IActionResult ConvexHull([FromBody] GeometrySetRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySetOperation(geometryType, srid, geometries);
            var result = _executor.ConvexHull(operation, cts.Token);
            var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for convexHull operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogOperationTimeout("ConvexHull operation", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry serialization", "convexHull operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogOperationFailure(ex, "Geometry service operation", "convexHull operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("ConvexHull operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("distance")]
    public IActionResult Distance([FromBody] GeometryDistanceRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometry1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometry1);
            var geometry1Payload = request.Geometry1 ?? throw new GeometrySerializationException("geometry1 payload is required.");
            var geometry2Payload = request.Geometry2 ?? throw new GeometrySerializationException("geometry2 payload is required.");

            var geometries1 = _serializer.DeserializeGeometries(geometry1Payload, geometryType, srid, cts.Token);
            var geometries2 = _serializer.DeserializeGeometries(geometry2Payload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries1);
            GeometryComplexityValidator.ValidateCollection(geometries2);

            var operation = new GeometryDistanceOperation(
                geometryType,
                srid,
                geometries1,
                geometries2,
                request.DistanceUnit,
                request.Geodesic ?? false);

            var result = _executor.Distance(operation, cts.Token);
            return Ok(new { distances = result });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for distance operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Distance operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for distance operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for distance operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Distance operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("areasAndLengths")]
    public IActionResult AreasAndLengths([FromBody] GeometryMeasurementRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Polygons);
            var polygonsPayload = request.Polygons ?? throw new GeometrySerializationException("polygons payload is required.");
            var geometries = _serializer.DeserializeGeometries(polygonsPayload, "esriGeometryPolygon", srid, cts.Token);
            var polygons = geometries.Cast<Polygon>().ToList();

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(polygons);

            var operation = new GeometryMeasurementOperation("esriGeometryPolygon", srid, polygons, request.AreaUnit, request.LengthUnit);
            var areas = _executor.Areas(operation, cts.Token);
            var lengths = _executor.Lengths(operation, cts.Token);

            return Ok(new { areas, lengths });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for areasAndLengths operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AreasAndLengths operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for areasAndLengths operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for areasAndLengths operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("AreasAndLengths operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("offset")]
    public async Task<IActionResult> Offset([FromBody] GeometryOffsetRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Offset")
                .WithTag("arcgis.operation", "Offset")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
                    var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
                    var geometries = _serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    activity.AddTag("arcgis.offset_distance", request.OffsetDistance ?? 0);
                    activity.AddTag("arcgis.offset_how", request.OffsetHow ?? "esriGeometryOffsetRounded");

                    var operation = new GeometryOffsetOperation(
                        geometryType,
                        srid,
                        geometries,
                        request.OffsetDistance ?? 0,
                        request.OffsetHow ?? "esriGeometryOffsetRounded",
                        request.BevelRatio ?? 10.0);

                    var result = _executor.Offset(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for offset operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Offset operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for offset operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for offset operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Offset operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("trimExtend")]
    public async Task<IActionResult> TrimExtend([FromBody] GeometryTrimExtendRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer TrimExtend")
                .WithTag("arcgis.operation", "TrimExtend")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Polylines);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Polylines);
                    var polylinesPayload = request.Polylines ?? throw new GeometrySerializationException("polylines payload is required.");
                    var polylines = _serializer.DeserializeGeometries(polylinesPayload, geometryType, srid, cts.Token);

                    var trimExtendToPayload = request.TrimExtendTo ?? throw new GeometrySerializationException("trimExtendTo payload is required.");
                    var trimExtendToGeometries = _serializer.DeserializeGeometries(trimExtendToPayload, geometryType, srid, cts.Token);

                    if (trimExtendToGeometries.Count == 0)
                    {
                        throw new GeometrySerializationException("trimExtendTo must contain at least one geometry.");
                    }

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(polylines);
                    GeometryComplexityValidator.ValidateCollection(trimExtendToGeometries);

                    var operation = new GeometryTrimExtendOperation(
                        geometryType,
                        srid,
                        polylines,
                        trimExtendToGeometries[0],
                        request.ExtendHow ?? 0);

                    var result = _executor.TrimExtend(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for trimExtend operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TrimExtend operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for trimExtend operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for trimExtend operation");
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("TrimExtend operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("labelPoints")]
    [Authorize(Policy = "RequireViewer")]
    public IActionResult LabelPoints([FromBody] GeometryLabelPointsRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = "esriGeometryPolygon"; // Input type must be polygon
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Polygons);
            var polygonsPayload = request.Polygons ?? throw new GeometrySerializationException("polygons payload is required.");
            var polygons = _serializer.DeserializeGeometries(polygonsPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(polygons);

            var operation = new GeometryLabelPointsOperation(geometryType, srid, polygons);
            var result = _executor.LabelPoints(operation, cts.Token);

            // Serialize as points (output type is different from input type)
            var response = _serializer.SerializeGeometries(result, "esriGeometryPoint", srid, cts.Token);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for labelPoints operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LabelPoints operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout,
                new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for labelPoints operation");
            return BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for labelPoints operation");
            return BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("LabelPoints operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("cut")]
    public async Task<IActionResult> Cut([FromBody] GeometryCutRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Cut")
                .WithTag("arcgis.operation", "Cut")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Target);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Target);
                    var targetPayload = request.Target ?? throw new GeometrySerializationException("target geometry is required.");
                    var cutterPayload = request.Cutter ?? throw new GeometrySerializationException("cutter geometry is required.");

                    var targets = _serializer.DeserializeGeometries(targetPayload, geometryType, srid, cts.Token);
                    var cutters = _serializer.DeserializeGeometries(cutterPayload, "esriGeometryPolyline", srid, cts.Token);

                    if (targets.Count == 0)
                    {
                        throw new GeometrySerializationException("At least one target geometry is required.");
                    }

                    if (cutters.Count == 0)
                    {
                        throw new GeometrySerializationException("At least one cutter geometry is required.");
                    }

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(targets);
                    GeometryComplexityValidator.ValidateCollection(cutters);

                    var target = targets[0];
                    var cutter = cutters[0];

                    var operation = new GeometryCutOperation(geometryType, srid, target, cutter);
                    var result = _executor.Cut(operation, cts.Token);
                    var response = _serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for cut operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cut operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for cut operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for cut operation");
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Cut operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("reshape")]
    public async Task<IActionResult> Reshape([FromBody] GeometryReshapeRequest request)
    {
        var geometrySettings = _configurationService.Current.Services.Geometry;
        if (!geometrySettings.Enabled)
        {
            return NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS GeometryServer Reshape")
                .WithTag("arcgis.operation", "Reshape")
                .ExecuteAsync(async activity =>
                {
                    var geometryType = ResolveGeometryType(request.GeometryType, request.Target);
                    var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Target);
                    var targetPayload = request.Target ?? throw new GeometrySerializationException("target geometry is required.");
                    var reshaperPayload = request.Reshaper ?? throw new GeometrySerializationException("reshaper geometry is required.");

                    var targets = _serializer.DeserializeGeometries(targetPayload, geometryType, srid, cts.Token);
                    var reshapers = _serializer.DeserializeGeometries(reshaperPayload, "esriGeometryPolyline", srid, cts.Token);

                    if (targets.Count == 0)
                    {
                        throw new GeometrySerializationException("At least one target geometry is required.");
                    }

                    if (reshapers.Count == 0)
                    {
                        throw new GeometrySerializationException("At least one reshaper geometry is required.");
                    }

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(targets);
                    GeometryComplexityValidator.ValidateCollection(reshapers);

                    var target = targets[0];
                    var reshaper = reshapers[0];

                    var operation = new GeometryReshapeOperation(geometryType, srid, target, reshaper);
                    var result = _executor.Reshape(operation, cts.Token);
                    var response = _serializer.SerializeGeometry(result, geometryType, srid);
                    return await Task.FromResult(Ok(new { geometry = response }));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            _logger.LogWarning(ex, "Geometry complexity validation failed for reshape operation");
            return BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Reshape operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            _logger.LogError(ex, "Geometry serialization failed for reshape operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (GeometryServiceException ex)
        {
            _logger.LogError(ex, "Geometry service operation failed for reshape operation");
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reshape operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    private static string ResolveGeometryType(string? explicitType, JsonNode? payload)
    {
        if (explicitType.HasValue())
        {
            return explicitType;
        }

        if (payload is JsonObject obj &&
            obj.TryGetPropertyValue("geometryType", out var typeNode) &&
            typeNode is JsonValue typeValue &&
            typeValue.TryGetValue<string>(out var embeddedType) &&
            embeddedType.HasValue())
        {
            return embeddedType;
        }

        throw new GeometrySerializationException("geometryType must be specified.");
    }

    private int ResolveSpatialReference(GeoservicesRESTSpatialReference? explicitSr, JsonElement numericOrString, JsonNode? payload)
    {
        if (TryResolveSpatialReference(explicitSr, numericOrString, default(StringValues), payload, out var srid))
        {
            return srid;
        }

        throw new GeometrySerializationException("Spatial reference (sr) must be specified.");
    }
}

public sealed class GeometryProjectRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("inSR")]
    public JsonElement InSr { get; init; }

    [JsonPropertyName("outSR")]
    public JsonElement OutSr { get; init; }

    [JsonPropertyName("inSpatialReference")]
    public GeoservicesRESTSpatialReference? InSpatialReference { get; init; }

    [JsonPropertyName("outSpatialReference")]
    public GeoservicesRESTSpatialReference? OutSpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryBufferRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("distances")]
    public double? Distance { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonPropertyName("unionResults")]
    public bool? UnionResults { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometrySimplifyRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometrySetRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryPairwiseRequest
{
    [JsonPropertyName("geometries1")]
    public JsonNode? Geometries1 { get; init; }

    [JsonPropertyName("geometries2")]
    public JsonNode? Geometries2 { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryDistanceRequest
{
    [JsonPropertyName("geometry1")]
    public JsonNode? Geometry1 { get; init; }

    [JsonPropertyName("geometry2")]
    public JsonNode? Geometry2 { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("distanceUnit")]
    public string? DistanceUnit { get; init; }

    [JsonPropertyName("geodesic")]
    public bool? Geodesic { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryMeasurementRequest
{
    [JsonPropertyName("polygons")]
    public JsonNode? Polygons { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("areaUnit")]
    public string? AreaUnit { get; init; }

    [JsonPropertyName("lengthUnit")]
    public string? LengthUnit { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryLabelPointsRequest
{
    [JsonPropertyName("polygons")]
    public JsonNode? Polygons { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryOffsetRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("offsetDistance")]
    public double? OffsetDistance { get; init; }

    [JsonPropertyName("offsetHow")]
    public string? OffsetHow { get; init; }

    [JsonPropertyName("bevelRatio")]
    public double? BevelRatio { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryTrimExtendRequest
{
    [JsonPropertyName("polylines")]
    public JsonNode? Polylines { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("trimExtendTo")]
    public JsonNode? TrimExtendTo { get; init; }

    [JsonPropertyName("extendHow")]
    public int? ExtendHow { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryDensifyRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("maxSegmentLength")]
    public double? MaxSegmentLength { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryGeneralizeRequest
{
    [JsonPropertyName("geometries")]
    public JsonNode? Geometries { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("maxDeviation")]
    public double? MaxDeviation { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryCutRequest
{
    [JsonPropertyName("target")]
    public JsonNode? Target { get; init; }

    [JsonPropertyName("cutter")]
    public JsonNode? Cutter { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}

public sealed class GeometryReshapeRequest
{
    [JsonPropertyName("target")]
    public JsonNode? Target { get; init; }

    [JsonPropertyName("reshaper")]
    public JsonNode? Reshaper { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("sr")]
    public JsonElement Sr { get; init; }

    [JsonPropertyName("spatialReference")]
    public GeoservicesRESTSpatialReference? SpatialReference { get; init; }

    [JsonPropertyName("f")]
    public string? Format { get; init; }
}
