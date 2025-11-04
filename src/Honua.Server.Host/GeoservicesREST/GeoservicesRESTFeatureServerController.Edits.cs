// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

public sealed partial class GeoservicesRESTFeatureServerController
{
    [HttpPost("{layerIndex:int}/applyEdits")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)] // DoS prevention for large JSON
    public async Task<IActionResult> ApplyEditsAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS FeatureServer ApplyEdits")
            .WithTag("arcgis.operation", "ApplyEdits")
            .WithTag("arcgis.service", serviceId)
            .WithTag("arcgis.layer_index", layerIndex)
            .ExecuteAsync<IActionResult>(async _ =>
            {
                var serviceView = ResolveService(folderId, serviceId);
                if (serviceView is null)
                {
                    return NotFound();
                }

                var layerView = ResolveLayer(serviceView, layerIndex);
                if (layerView is null)
                {
                    return NotFound();
                }

                using var payload = await ParsePayloadAsync(Request, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    return BadRequest(new { error = "Invalid or empty JSON payload." });
                }

                var execution = await _editingService.ExecuteEditsAsync(
                    serviceView,
                    layerView,
                    payload.RootElement,
                    Request,
                    DefaultAddPropertyNames,
                    DefaultUpdatePropertyNames,
                    DefaultDeletePropertyNames,
                    includeAdds: true,
                    includeUpdates: true,
                    includeDeletes: true,
                    cancellationToken).ConfigureAwait(false);

                if (!execution.HasOperations)
                {
                    return BadRequest(new { error = "No edits were supplied." });
                }

                var response = new Dictionary<string, object?>
                {
                    ["addResults"] = execution.AddResults,
                    ["updateResults"] = execution.UpdateResults,
                    ["deleteResults"] = execution.DeleteResults
                };

                if (execution.ReturnsEditMoment && execution.EditMoment is not null)
                {
                    response["editMoment"] = execution.EditMoment.Value.ToUnixTimeMilliseconds();
                }

                return Ok(response);
            });
    }

    [HttpPost("{layerIndex:int}/addFeatures")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)] // DoS prevention
    public async Task<IActionResult> AddFeaturesAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return NotFound();
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return NotFound();
        }

        using var payload = await ParsePayloadAsync(Request, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return BadRequest(new { error = "Invalid or empty JSON payload." });
        }

        var execution = await _editingService.ExecuteEditsAsync(
            serviceView,
            layerView,
            payload.RootElement,
            Request,
            AddFeaturesPropertyNames,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeAdds: true,
            includeUpdates: false,
            includeDeletes: false,
            cancellationToken).ConfigureAwait(false);

        if (!execution.HasOperations)
        {
            return BadRequest(new { error = "No edits were supplied." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        _auditLogger.LogFeatureAdd(
            serviceId,
            layerView.Layer.Id,
            execution.AddResults.Count,
            User,
            ipAddress);

        var response = new Dictionary<string, object?>
        {
            ["addResults"] = execution.AddResults
        };

        if (execution.ReturnsEditMoment && execution.EditMoment is not null)
        {
            response["editMoment"] = execution.EditMoment.Value.ToUnixTimeMilliseconds();
        }

        return Ok(response);
    }

    [HttpPost("{layerIndex:int}/updateFeatures")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)] // DoS prevention
    public async Task<IActionResult> UpdateFeaturesAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return NotFound();
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return NotFound();
        }

        using var payload = await ParsePayloadAsync(Request, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return BadRequest(new { error = "Invalid or empty JSON payload." });
        }

        var execution = await _editingService.ExecuteEditsAsync(
            serviceView,
            layerView,
            payload.RootElement,
            Request,
            Array.Empty<string>(),
            UpdateFeaturesPropertyNames,
            Array.Empty<string>(),
            includeAdds: false,
            includeUpdates: true,
            includeDeletes: false,
            cancellationToken).ConfigureAwait(false);

        if (!execution.HasOperations)
        {
            return BadRequest(new { error = "No edits were supplied." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var featureIds = ExtractFeatureIds(execution.UpdateResults);
        _auditLogger.LogFeatureUpdate(
            serviceId,
            layerView.Layer.Id,
            featureIds,
            User,
            ipAddress);

        var response = new Dictionary<string, object?>
        {
            ["updateResults"] = execution.UpdateResults
        };

        if (execution.ReturnsEditMoment && execution.EditMoment is not null)
        {
            response["editMoment"] = execution.EditMoment.Value.ToUnixTimeMilliseconds();
        }

        return Ok(response);
    }

    [HttpPost("{layerIndex:int}/deleteFeatures")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)] // DoS prevention
    public async Task<IActionResult> DeleteFeaturesAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return NotFound();
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return NotFound();
        }

        using var payload = await ParsePayloadAsync(Request, cancellationToken).ConfigureAwait(false);
        using var document = payload ?? JsonDocument.Parse("{}");

        var execution = await _editingService.ExecuteEditsAsync(
            serviceView,
            layerView,
            document.RootElement,
            Request,
            Array.Empty<string>(),
            Array.Empty<string>(),
            DeleteFeaturesPropertyNames,
            includeAdds: false,
            includeUpdates: false,
            includeDeletes: true,
            cancellationToken).ConfigureAwait(false);

        if (!execution.HasOperations)
        {
            return BadRequest(new { error = "No edits were supplied." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var featureIds = ExtractFeatureIds(execution.DeleteResults);
        _auditLogger.LogFeatureDelete(
            serviceId,
            layerView.Layer.Id,
            featureIds,
            User,
            ipAddress);

        var response = new Dictionary<string, object?>
        {
            ["deleteResults"] = execution.DeleteResults
        };

        if (execution.ReturnsEditMoment && execution.EditMoment is not null)
        {
            response["editMoment"] = execution.EditMoment.Value.ToUnixTimeMilliseconds();
        }

        return Ok(response);
    }

    private static async Task<JsonDocument?> ParsePayloadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var body = request.Body;
        if (body is null)
        {
            return null;
        }

        const int MaxPayloadBytes = (int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes;

        var reader = PipeReader.Create(body);
        var buffer = new ArrayBufferWriter<byte>();

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var sequence = result.Buffer;

                foreach (var segment in sequence)
                {
                    buffer.Write(segment.Span);
                }

                reader.AdvanceTo(sequence.End);

                if (buffer.WrittenCount > MaxPayloadBytes)
                {
                    throw new GeoservicesRESTQueryException("JSON payload exceeds the 100 MB maximum allowed size.");
                }

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }

        if (buffer.WrittenCount == 0)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(buffer.WrittenMemory, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IEnumerable<object> ExtractFeatureIds(IReadOnlyList<object> results)
    {
        var featureIds = new List<object>();
        foreach (var result in results)
        {
            if (result is not Dictionary<string, object?> dict)
            {
                continue;
            }

            if (dict.TryGetValue("objectId", out var objectIdValue) && objectIdValue is not null)
            {
                featureIds.Add(objectIdValue);
            }
            else if (dict.TryGetValue("id", out var idValue) && idValue is not null)
            {
                featureIds.Add(idValue);
            }
        }

        return featureIds;
    }
}
