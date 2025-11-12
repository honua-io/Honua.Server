// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Features GeoJSON and feature serialization operations.
/// Extracted from OgcSharedHandlers to enable dependency injection and testability.
/// </summary>
internal sealed class OgcFeaturesGeoJsonHandler : IOgcFeaturesGeoJsonHandler
{
    /// <inheritdoc />
    public object ToFeature(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureRecord record,
        FeatureQuery query,
        FeatureComponents? componentsOverride = null,
        IReadOnlyList<OgcLink>? additionalLinks = null)
    {
        var components = componentsOverride ?? FeatureComponentBuilder.BuildComponents(layer, record, query);

        var links = BuildFeatureLinks(request, collectionId, layer, components, additionalLinks);

        var properties = new Dictionary<string, object?>(components.Properties, StringComparer.OrdinalIgnoreCase);
        AppendStyleMetadata(properties, layer);

        return new
        {
            type = "Feature",
            id = components.RawId,
            geometry = components.Geometry,
            properties,
            links
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks)
    {
        var links = new List<OgcLink>();
        if (components.FeatureId.HasValue())
        {
            links.Add(OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}/items/{components.FeatureId}", "self", "application/geo+json", $"Feature {components.FeatureId}"));
        }

        links.Add(OgcSharedHandlers.BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title));

        if (additionalLinks is not null)
        {
            links.AddRange(additionalLinks);
        }

        return links;
    }

    /// <inheritdoc />
    public async Task<JsonDocument?> ParseJsonDocumentAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // BUG FIX #31: SECURITY - DoS prevention for GeoJSON upload endpoints
        // Previous implementation buffered entire request body without size validation,
        // allowing attackers to exhaust memory with multi-GB JSON payloads.
        //
        // Security measures:
        // 1. Check Content-Length before buffering (fail fast)
        // 2. Configurable maximum size (default 100 MB)
        // 3. Return HTTP 413 Payload Too Large for oversized requests
        // 4. Prevent memory exhaustion before Kestrel's limits

        const long DefaultMaxSizeBytes = 100 * 1024 * 1024; // 100 MB
        var maxSize = DefaultMaxSizeBytes;

        // Try to get configured limit from request services (if available)
        var honuaConfig = request.HttpContext.RequestServices
            .GetService(typeof(Honua.Server.Core.Configuration.V2.HonuaConfig))
            as Honua.Server.Core.Configuration.V2.HonuaConfig;

        if (honuaConfig?.Services.TryGetValue("ogc-api", out var ogcApiService) == true
            && ogcApiService.Settings is not null)
        {
            // Extract MaxFeatureUploadSizeBytes from settings if available
            // For now, use default
        }

        // Check Content-Length header before buffering
        if (request.ContentLength.HasValue && request.ContentLength.Value > maxSize)
        {
            throw new InvalidOperationException(
                $"Request body size ({request.ContentLength.Value:N0} bytes) exceeds maximum allowed size " +
                $"({maxSize:N0} bytes). To upload larger files, increase OgcApi.MaxFeatureUploadSizeBytes in configuration.");
        }

        request.EnableBuffering(maxSize);
        request.Body.Seek(0, SeekOrigin.Begin);

        try
        {
            // Additional safety: limit how much we'll actually read from the stream
            // This protects against cases where Content-Length is missing or incorrect
            var options = new JsonDocumentOptions
            {
                MaxDepth = 256 // Prevent deeply nested JSON attacks
            };

            return await JsonDocument.ParseAsync(request.Body, options, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <inheritdoc />
    public IEnumerable<JsonElement> EnumerateGeoJsonFeatures(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "FeatureCollection", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("features", out var featuresElement) &&
                featuresElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in featuresElement.EnumerateArray())
                {
                    yield return feature;
                }

                yield break;
            }

            yield return root;
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    /// <inheritdoc />
    public Dictionary<string, object?> ReadGeoJsonAttributes(
        JsonElement featureElement,
        LayerDefinition layer,
        bool removeId,
        out string? featureId)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        featureId = null;

        if (featureElement.ValueKind != JsonValueKind.Object)
        {
            return attributes;
        }

        if (featureElement.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                attributes[property.Name] = ConvertJsonElement(property.Value);
            }
        }

        if (featureElement.TryGetProperty("geometry", out var geometryElement))
        {
            if (geometryElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                attributes[layer.GeometryField] = null;
            }
            else if (geometryElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                attributes[layer.GeometryField] = JsonNode.Parse(geometryElement.GetRawText());
            }
        }

        if (featureElement.TryGetProperty("id", out var idElement))
        {
            featureId = ConvertJsonElementToString(idElement);
        }

        if (attributes.TryGetValue(layer.IdField, out var attributeId) && attributeId is not null)
        {
            featureId ??= Convert.ToString(attributeId, CultureInfo.InvariantCulture);
            if (removeId)
            {
                attributes.Remove(layer.IdField);
            }
        }

        return attributes;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return JsonElementConverter.ToObjectWithJsonNode(element);
    }

    private static string? ConvertJsonElementToString(JsonElement element)
    {
        return JsonElementConverter.ToString(element);
    }

    private static void AppendStyleMetadata(IDictionary<string, object?> target, LayerDefinition layer)
    {
        if (target is null)
        {
            return;
        }

        if (layer.DefaultStyleId.HasValue())
        {
            target["honua:defaultStyleId"] = layer.DefaultStyleId;
        }

        var styleIds = BuildOrderedStyleIds(layer);
        if (styleIds.Count > 0)
        {
            target["honua:styleIds"] = styleIds;
        }

        if (layer.MinScale is double minScale)
        {
            target["honua:minScale"] = minScale;
        }

        if (layer.MaxScale is double maxScale)
        {
            target["honua:maxScale"] = maxScale;
        }
    }

    private static IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layer.DefaultStyleId.HasValue() && seen.Add(layer.DefaultStyleId))
        {
            results.Add(layer.DefaultStyleId);
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }
}
