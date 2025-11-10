// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains feature and GeoJSON handling methods.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static object? ConvertExtent(LayerExtentDefinition? extent)
    {
        if (extent is null)
        {
            return null;
        }

        object? spatial = null;
        if (extent.Bbox.Count > 0 || extent.Crs.HasValue())
        {
            spatial = new
            {
                bbox = extent.Bbox,
                crs = extent.Crs.IsNullOrWhiteSpace()
                    ? CrsHelper.DefaultCrsIdentifier
                    : CrsHelper.NormalizeIdentifier(extent.Crs)
            };
        }

        var hasIntervals = extent.Temporal.Count > 0;
        // Use IEnumerable to avoid materializing array unless needed
        var intervals = hasIntervals
            ? extent.Temporal
                .Select(t => new[] { t.Start?.ToString("O"), t.End?.ToString("O") })
            : Array.Empty<string?[]>();

        object? temporal = null;
        if (hasIntervals || extent.TemporalReferenceSystem.HasValue())
        {
            temporal = new
            {
                interval = intervals,
                trs = extent.TemporalReferenceSystem.IsNullOrWhiteSpace()
                    ? DefaultTemporalReferenceSystem
                    : extent.TemporalReferenceSystem
            };
        }

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new
        {
            spatial,
            temporal
        };
    }

    internal static object ToFeature(
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

    internal static IReadOnlyList<OgcLink> BuildFeatureLinks(
        HttpRequest request,
        string collectionId,
        LayerDefinition layer,
        FeatureComponents components,
        IReadOnlyList<OgcLink>? additionalLinks)
    {
        var links = new List<OgcLink>();
        if (components.FeatureId.HasValue())
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/items/{components.FeatureId}", "self", "application/geo+json", $"Feature {components.FeatureId}"));
        }

        links.Add(BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", layer.Title));

        if (additionalLinks is not null)
        {
            links.AddRange(additionalLinks);
        }

        return links;
    }

    internal static IEnumerable<JsonElement> EnumerateGeoJsonFeatures(JsonElement root)
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
    internal static Dictionary<string, object?> ReadGeoJsonAttributes(JsonElement featureElement, LayerDefinition layer, bool removeId, out string? featureId)
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

    internal static object? ConvertJsonElement(JsonElement element)
    {
        return JsonElementConverter.ToObjectWithJsonNode(element);
    }

    internal static string? ConvertJsonElementToString(JsonElement element)
    {
        return JsonElementConverter.ToString(element);
    }
}
