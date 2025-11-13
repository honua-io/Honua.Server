// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Metadata endpoints for service info, layer details, and renderer generation.
/// Endpoints:
/// - GET / (GetService)
/// - GET /{layerIndex} (GetLayer)
/// - GET/POST /{layerIndex}/generateRenderer (GenerateRenderer)
/// - GET/POST /returnUpdates (ReturnUpdates)
/// </summary>
public sealed partial class GeoservicesRESTFeatureServerController
{
    /// <summary>
    /// Get Feature Service metadata.
    /// Route: GET /rest/services/{serviceId}/FeatureServer
    /// </summary>
    [HttpGet]
    public ActionResult<GeoservicesRESTFeatureServiceSummary> GetService(string folderId, string serviceId)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var summary = GeoservicesRESTMetadataMapper.CreateFeatureServiceSummary(serviceView, GeoServicesVersion);
        return this.Ok(summary);
    }

    /// <summary>
    /// Get layer metadata.
    /// Route: GET /rest/services/{serviceId}/FeatureServer/{layerIndex}
    /// </summary>
    [HttpGet("{layerIndex:int}")]
    public async Task<ActionResult<GeoservicesRESTLayerDetailResponse>> GetLayer(string folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var resolution = GeoservicesRESTServiceResolutionHelper.ResolveServiceAndLayer(this, this.catalog, folderId, serviceId, layerIndex);
        if (resolution.Error is not null)
        {
            return (ActionResult)resolution.Error;
        }

        var serviceView = resolution.ServiceView!;
        var layerView = resolution.LayerView!;

        var style = await ResolveDefaultStyleAsync(layerView.Layer, cancellationToken).ConfigureAwait(false);
        var detail = GeoservicesRESTMetadataMapper.CreateLayerDetailResponse(serviceView, layerView, layerIndex, GeoServicesVersion, style);
        return this.Ok(detail);
    }

    /// <summary>
    /// Generate renderer for layer.
    /// Route: GET /rest/services/{serviceId}/FeatureServer/{layerIndex}/generateRenderer
    /// </summary>
    [HttpGet("{layerIndex:int}/generateRenderer")]
    public Task<IActionResult> GenerateRendererGetAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return GenerateRendererInternalAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    /// <summary>
    /// Generate renderer for layer (POST).
    /// Route: POST /rest/services/{serviceId}/FeatureServer/{layerIndex}/generateRenderer
    /// </summary>
    [HttpPost("{layerIndex:int}/generateRenderer")]
    public Task<IActionResult> GenerateRendererPostAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return GenerateRendererInternalAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    /// <summary>
    /// Return updates for sync operations.
    /// Route: GET /rest/services/{serviceId}/FeatureServer/returnUpdates
    /// </summary>
    [HttpGet("returnUpdates")]
    public Task<IActionResult> ReturnUpdatesGetAsync(string? folderId, string serviceId, CancellationToken cancellationToken)
    {
        return ReturnUpdatesAsync(folderId, serviceId, cancellationToken);
    }

    /// <summary>
    /// Return updates for sync operations (POST).
    /// Route: POST /rest/services/{serviceId}/FeatureServer/returnUpdates
    /// </summary>
    [HttpPost("returnUpdates")]
    public Task<IActionResult> ReturnUpdatesPostAsync(string? folderId, string serviceId, CancellationToken cancellationToken)
    {
        return ReturnUpdatesAsync(folderId, serviceId, cancellationToken);
    }

    private async Task<IActionResult> GenerateRendererInternalAsync(
        string? folderId,
        string serviceId,
        int layerIndex,
        CancellationToken cancellationToken)
    {
        var resolution = GeoservicesRESTServiceResolutionHelper.ResolveServiceAndLayer(this, this.catalog, folderId, serviceId, layerIndex);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var layerView = resolution.LayerView!;
        var style = await ResolveDefaultStyleAsync(layerView.Layer, cancellationToken).ConfigureAwait(false);

        var drawingInfo = style is not null
            ? StyleFormatConverter.CreateEsriDrawingInfo(style, layerView.Layer.GeometryType)
            : CreateDefaultDrawingInfo(layerView.Layer);

        var rendererNode = drawingInfo?.TryGetPropertyValue("renderer", out var renderer) == true && renderer is JsonObject rendererObject
            ? (JsonObject)rendererObject.DeepClone()
            : new JsonObject
            {
                ["type"] = "simple"
            };

        var response = new JsonObject
        {
            ["renderer"] = rendererNode,
            ["transparency"] = 0,
            ["labelingInfo"] = new JsonArray(),
            ["authoringInfo"] = new JsonObject()
        };

        return this.Ok(response);
    }

    private Task<IActionResult> ReturnUpdatesAsync(
        string? folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var (serviceView, error) = GeoservicesRESTServiceResolutionHelper.ResolveServiceOnly(this, this.catalog, folderId, serviceId);
        if (error is not null)
        {
            return Task.FromResult<IActionResult>(error);
        }

        var layersRaw = this.Request.Query.TryGetValue("layers", out var layerValues) ? layerValues.ToString() : null;
        var layerIndexes = ResolveRequestedLayerIndexes(layersRaw, serviceView!, out var parseError);
        if (parseError is not null)
        {
            return Task.FromResult<IActionResult>(BadRequest(new { error = parseError }));
        }

        var layerSnapshots = new List<object>(layerIndexes.Count);
        foreach (var index in layerIndexes)
        {
            var layerView = GeoservicesRESTServiceResolutionHelper.ResolveLayer(serviceView!, index);
            if (layerView is null)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = $"Layer index '{index}' is not defined for service '{serviceId}'." }));
            }

            layerSnapshots.Add(new
            {
                id = index,
                name = layerView.Layer.Title ?? layerView.Layer.Id,
                adds = Array.Empty<object>(),
                updates = Array.Empty<object>(),
                deletes = Array.Empty<object>(),
                deleteIds = Array.Empty<object>(),
                attachments = new
                {
                    adds = Array.Empty<object>(),
                    updates = Array.Empty<object>(),
                    deletes = Array.Empty<object>()
                },
                exceededTransferLimit = false
            });
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var response = new
        {
            layers = layerSnapshots,
            tables = Array.Empty<object>(),
            hasVersionedData = false,
            globalIdDeletes = Array.Empty<object>(),
            latestTimestamp = timestamp,
            revisionInfo = new
            {
                lastChange = timestamp
            }
        };

        return Task.FromResult<IActionResult>(Ok(response));
    }

    private static JsonObject CreateDefaultDrawingInfo(LayerDefinition layer)
    {
        var defaultStyle = new StyleDefinition
        {
            Id = $"{layer.Id}_default_renderer",
            Title = $"{layer.Title ?? layer.Id} Default Renderer",
            GeometryType = layer.GeometryType,
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#4E9AF1",
                StrokeColor = "#1F78B4",
                StrokeWidth = 1.5,
                Opacity = 0.65
            }
        };

        return StyleFormatConverter.CreateEsriDrawingInfo(defaultStyle, layer.GeometryType);
    }

    private static IReadOnlyList<int> ResolveRequestedLayerIndexes(string? raw, CatalogServiceView serviceView, out string? error)
    {
        error = null;

        if (raw.IsNullOrWhiteSpace())
        {
            return Enumerable.Range(0, serviceView.Layers.Count).ToArray();
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Enumerable.Range(0, serviceView.Layers.Count).ToArray();
        }

        var indexes = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                error = $"Unable to parse layer index '{part}'. Expected comma-separated integers.";
                return Array.Empty<int>();
            }

            if (parsed < 0 || parsed >= serviceView.Layers.Count)
            {
                error = $"Layer index '{parsed}' is out of range. Service defines {serviceView.Layers.Count} layers.";
                return Array.Empty<int>();
            }

            if (!indexes.Contains(parsed))
            {
                indexes.Add(parsed);
            }
        }

        return indexes;
    }
}
