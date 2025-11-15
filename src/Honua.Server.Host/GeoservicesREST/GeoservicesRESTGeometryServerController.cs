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
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Geoservices.GeometryService;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Filters;
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
[Route("v1/rest/services/Geometry/GeometryServer")]
[ServiceFilter(typeof(GeometryOperationExceptionFilter))]
public sealed class GeoservicesRESTGeometryServerController : ControllerBase
{
    private const int OperationTimeoutSeconds = 30;

    private readonly HonuaConfig? honuaConfig;
    private readonly IGeometrySerializer serializer;
    private readonly IGeometryOperationExecutor executor;
    private readonly ILogger<GeoservicesRESTGeometryServerController> logger;

    public GeoservicesRESTGeometryServerController(
        IGeometrySerializer serializer,
        IGeometryOperationExecutor executor,
        ILogger<GeoservicesRESTGeometryServerController> logger,
        HonuaConfig? honuaConfig = null)
    {
        this.honuaConfig = honuaConfig;
        this.serializer = Guard.NotNull(serializer);
        this.executor = Guard.NotNull(executor);
        this.logger = Guard.NotNull(logger);
    }

    [HttpGet]
    public IActionResult GetService()
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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

        return this.Ok(response);
    }

    [HttpPost("project")]
    public async Task<IActionResult> Project([FromBody] GeometryProjectRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        // For now, assume GDAL operations are enabled by default in Configuration V2
        var enableGdalOperations = true;
        if (!enableGdalOperations)
        {
            return this.StatusCode(StatusCodes.Status501NotImplemented, new { error = "Projection operation is disabled in this environment." });
        }

        var format = ResolveFormat(request.Format);
        if (!format.EqualsIgnoreCase("json"))
        {
            return this.BadRequest(new { error = $"Format '{format}' is not supported." });
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

                    var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, inputSrid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    var result = this.executor.Project(new GeometryProjectOperation(geometryType, inputSrid, outputSrid, geometries), cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, outputSrid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for project operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("Project operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "project operation");
            return this.BadRequest(new { error = ex.Message });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "project operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Project operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    private string ResolveFormat(string? format)
    {
        if (format.HasValue())
        {
            return format;
        }

        if (this.Request.Query.TryGetValue("f", out var values) && values.Count > 0)
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
        if (TryResolveSpatialReference(request.InSpatialReference, request.InSr, this.Request.Query["inSR"], request.Geometries, out var srid))
        {
            return srid;
        }

        throw new GeometrySerializationException("Input spatial reference (inSR) must be specified.");
    }

    private int ResolveOutputSpatialReference(GeometryProjectRequest request)
    {
        if (TryResolveSpatialReference(request.OutSpatialReference, request.OutSr, this.Request.Query["outSR"], request.Geometries, out var srid))
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

        if (this.Request.Query.TryGetValue("geometries", out var values) && values.Count > 0)
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
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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
                    var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

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

                    var result = this.executor.Buffer(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for buffer operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("Buffer operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "buffer operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "buffer operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Buffer operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("simplify")]
    public IActionResult Simplify([FromBody] GeometrySimplifyRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySimplifyOperation(geometryType, srid, geometries);
            var result = this.executor.Simplify(operation, cts.Token);
            var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return this.Ok(response);
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Simplify operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("densify")]
    public async Task<IActionResult> Densify([FromBody] GeometryDensifyRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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
                    var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    if (!request.MaxSegmentLength.HasValue || request.MaxSegmentLength.Value <= 0)
                    {
                        return this.BadRequest(new { error = "maxSegmentLength must be a positive value." });
                    }

                    activity.AddTag("arcgis.maxSegmentLength", request.MaxSegmentLength.Value);

                    var operation = new GeometryDensifyOperation(geometryType, srid, geometries, request.MaxSegmentLength.Value);
                    var result = this.executor.Densify(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for densify operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("Densify operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "densify operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "densify operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Densify operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("generalize")]
    public async Task<IActionResult> Generalize([FromBody] GeometryGeneralizeRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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
                    var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

                    // Validate geometry complexity to prevent DoS attacks
                    GeometryComplexityValidator.ValidateCollection(geometries);

                    if (!request.MaxDeviation.HasValue || request.MaxDeviation.Value <= 0)
                    {
                        return this.BadRequest(new { error = "maxDeviation must be a positive value." });
                    }

                    activity.AddTag("arcgis.maxDeviation", request.MaxDeviation.Value);

                    var operation = new GeometryGeneralizeOperation(geometryType, srid, geometries, request.MaxDeviation.Value);
                    var result = this.executor.Generalize(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for generalize operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("Generalize operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "generalize operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "generalize operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Generalize operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("union")]
    public IActionResult Union([FromBody] GeometrySetRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySetOperation(geometryType, srid, geometries);
            var result = this.executor.Union(operation, cts.Token);

            if (result is null)
            {
                return this.Ok(new { geometry = (object?)null });
            }

            var response = this.serializer.SerializeGeometry(result, geometryType, srid);
            return this.Ok(new { geometry = response });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Union operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("intersect")]
    public IActionResult Intersect([FromBody] GeometryPairwiseRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries1);
            var geometries1Payload = request.Geometries1 ?? throw new GeometrySerializationException("geometries1 payload is required.");
            var geometries2Payload = request.Geometries2 ?? throw new GeometrySerializationException("geometries2 payload is required.");

            var geometries1 = this.serializer.DeserializeGeometries(geometries1Payload, geometryType, srid, cts.Token);
            var geometries2 = this.serializer.DeserializeGeometries(geometries2Payload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries1);
            GeometryComplexityValidator.ValidateCollection(geometries2);

            var operation = new GeometryPairwiseOperation(geometryType, srid, geometries1, geometries2);
            var result = this.executor.Intersect(operation, cts.Token);
            var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return this.Ok(response);
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Intersect operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("difference")]
    public IActionResult Difference([FromBody] GeometryPairwiseRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries1);
            var geometries1Payload = request.Geometries1 ?? throw new GeometrySerializationException("geometries1 payload is required.");
            var geometries2Payload = request.Geometries2 ?? throw new GeometrySerializationException("geometries2 payload is required.");

            var geometries1 = this.serializer.DeserializeGeometries(geometries1Payload, geometryType, srid, cts.Token);
            var geometries2 = this.serializer.DeserializeGeometries(geometries2Payload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries1);
            GeometryComplexityValidator.ValidateCollection(geometries2);

            var operation = new GeometryPairwiseOperation(geometryType, srid, geometries1, geometries2);
            var result = this.executor.Difference(operation, cts.Token);
            var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return this.Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for difference operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("Difference operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "difference operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "difference operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Difference operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("convexHull")]
    public IActionResult ConvexHull([FromBody] GeometrySetRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometries);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometries);
            var geometriesPayload = request.Geometries ?? throw new GeometrySerializationException("geometries payload is required.");
            var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(geometries);

            var operation = new GeometrySetOperation(geometryType, srid, geometries);
            var result = this.executor.ConvexHull(operation, cts.Token);
            var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
            return this.Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for convexHull operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogOperationTimeout("ConvexHull operation", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry serialization", "convexHull operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogOperationFailure(ex, "Geometry service operation", "convexHull operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("ConvexHull operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("distance")]
    public IActionResult Distance([FromBody] GeometryDistanceRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = ResolveGeometryType(request.GeometryType, request.Geometry1);
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Geometry1);
            var geometry1Payload = request.Geometry1 ?? throw new GeometrySerializationException("geometry1 payload is required.");
            var geometry2Payload = request.Geometry2 ?? throw new GeometrySerializationException("geometry2 payload is required.");

            var geometries1 = this.serializer.DeserializeGeometries(geometry1Payload, geometryType, srid, cts.Token);
            var geometries2 = this.serializer.DeserializeGeometries(geometry2Payload, geometryType, srid, cts.Token);

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

            var result = this.executor.Distance(operation, cts.Token);
            return this.Ok(new { distances = result });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for distance operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Distance operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for distance operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for distance operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Distance operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("areasAndLengths")]
    public IActionResult AreasAndLengths([FromBody] GeometryMeasurementRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Polygons);
            var polygonsPayload = request.Polygons ?? throw new GeometrySerializationException("polygons payload is required.");
            var geometries = this.serializer.DeserializeGeometries(polygonsPayload, "esriGeometryPolygon", srid, cts.Token);
            var polygons = geometries.Cast<Polygon>().ToList();

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(polygons);

            var operation = new GeometryMeasurementOperation("esriGeometryPolygon", srid, polygons, request.AreaUnit, request.LengthUnit);
            var areas = this.executor.Areas(operation, cts.Token);
            var lengths = this.executor.Lengths(operation, cts.Token);

            return this.Ok(new { areas, lengths });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for areasAndLengths operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("AreasAndLengths operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for areasAndLengths operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for areasAndLengths operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("AreasAndLengths operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("offset")]
    public async Task<IActionResult> Offset([FromBody] GeometryOffsetRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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
                    var geometries = this.serializer.DeserializeGeometries(geometriesPayload, geometryType, srid, cts.Token);

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

                    var result = this.executor.Offset(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for offset operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Offset operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for offset operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for offset operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Offset operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("trimExtend")]
    public async Task<IActionResult> TrimExtend([FromBody] GeometryTrimExtendRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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
                    var polylines = this.serializer.DeserializeGeometries(polylinesPayload, geometryType, srid, cts.Token);

                    var trimExtendToPayload = request.TrimExtendTo ?? throw new GeometrySerializationException("trimExtendTo payload is required.");
                    var trimExtendToGeometries = this.serializer.DeserializeGeometries(trimExtendToPayload, geometryType, srid, cts.Token);

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

                    var result = this.executor.TrimExtend(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for trimExtend operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("TrimExtend operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for trimExtend operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for trimExtend operation");
            return this.BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("TrimExtend operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("labelPoints")]
    [Authorize(Policy = "RequireViewer")]
    public IActionResult LabelPoints([FromBody] GeometryLabelPointsRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(OperationTimeoutSeconds));
        var startTime = DateTime.UtcNow;

        try
        {
            var geometryType = "esriGeometryPolygon"; // Input type must be polygon
            var srid = ResolveSpatialReference(request.SpatialReference, request.Sr, request.Polygons);
            var polygonsPayload = request.Polygons ?? throw new GeometrySerializationException("polygons payload is required.");
            var polygons = this.serializer.DeserializeGeometries(polygonsPayload, geometryType, srid, cts.Token);

            // Validate geometry complexity to prevent DoS attacks
            GeometryComplexityValidator.ValidateCollection(polygons);

            var operation = new GeometryLabelPointsOperation(geometryType, srid, polygons);
            var result = this.executor.LabelPoints(operation, cts.Token);

            // Serialize as points (output type is different from input type)
            var response = this.serializer.SerializeGeometries(result, "esriGeometryPoint", srid, cts.Token);
            return this.Ok(response);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for labelPoints operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("LabelPoints operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout,
                new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for labelPoints operation");
            return this.BadRequest(new { error = "Geometry serialization failed. Check server logs for details." });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for labelPoints operation");
            return this.BadRequest(new { error = "Geometry operation failed. Check server logs for details." });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("LabelPoints operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("cut")]
    public async Task<IActionResult> Cut([FromBody] GeometryCutRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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

                    var targets = this.serializer.DeserializeGeometries(targetPayload, geometryType, srid, cts.Token);
                    var cutters = this.serializer.DeserializeGeometries(cutterPayload, "esriGeometryPolyline", srid, cts.Token);

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
                    var result = this.executor.Cut(operation, cts.Token);
                    var response = this.serializer.SerializeGeometries(result, geometryType, srid, cts.Token);
                    return await Task.FromResult(Ok(response));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for cut operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Cut operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for cut operation");
            return this.BadRequest(new { error = ex.Message });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for cut operation");
            return this.BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Cut operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("reshape")]
    public async Task<IActionResult> Reshape([FromBody] GeometryReshapeRequest request)
    {
        var geometryEnabled = this.honuaConfig?.Services.TryGetValue("geometry", out var geometryService) == true
            ? geometryService.Enabled
            : true;
        if (!geometryEnabled)
        {
            return this.NotFound();
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

                    var targets = this.serializer.DeserializeGeometries(targetPayload, geometryType, srid, cts.Token);
                    var reshapers = this.serializer.DeserializeGeometries(reshaperPayload, "esriGeometryPolyline", srid, cts.Token);

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
                    var result = this.executor.Reshape(operation, cts.Token);
                    var response = this.serializer.SerializeGeometry(result, geometryType, srid);
                    return await Task.FromResult(Ok(new { geometry = response }));
                });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("vertices") || ex.Message.Contains("coordinates") || ex.Message.Contains("nesting"))
        {
            this.logger.LogWarning(ex, "Geometry complexity validation failed for reshape operation");
            return this.BadRequest(new { error = $"Geometry complexity limit exceeded: {ex.Message}" });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogWarning("Reshape operation timed out after {Timeout} seconds", OperationTimeoutSeconds);
            return this.StatusCode(StatusCodes.Status408RequestTimeout, new { error = $"Operation timed out after {OperationTimeoutSeconds} seconds." });
        }
        catch (GeometrySerializationException ex)
        {
            this.logger.LogError(ex, "Geometry serialization failed for reshape operation");
            return this.BadRequest(new { error = ex.Message });
        }
        catch (GeometryServiceException ex)
        {
            this.logger.LogError(ex, "Geometry service operation failed for reshape operation");
            return this.BadRequest(new { error = ex.Message });
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startTime;
            this.logger.LogInformation("Reshape operation completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
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
