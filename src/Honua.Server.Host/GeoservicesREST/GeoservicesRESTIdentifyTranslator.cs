// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTIdentifyTranslator
{
    public static bool TryParse(
        HttpRequest request,
        CatalogServiceView serviceView,
        out GeoservicesRESTIdentifyContext context,
        out IActionResult? error)
    {
        Guard.NotNull(request);
        Guard.NotNull(serviceView);

        try
        {
            var query = request.Query;

            var layers = ResolveLayerSelection(query, serviceView, out var layerError);
            if (layerError is not null)
            {
                context = default!;
                error = layerError;
                return false;
            }

            var tolerancePixels = ResolveInt(query, "tolerance", defaultValue: 3, allowZero: true);
            var toleranceUnits = ResolveToleranceInUnits(query, tolerancePixels);

            var (bbox, bboxError) = ResolveGeometry(query, toleranceUnits);
            if (bboxError is not null)
            {
                context = default!;
                error = bboxError;
                return false;
            }

            var returnGeometry = ResolveBoolean(query, "returnGeometry", defaultValue: true);

            var outputWkid = ResolveOutputWkid(query);
            var mapScale = ResolveMapScale(query);

            var (whereFilter, filterError) = ResolveWhereClause(query);
            if (filterError is not null)
            {
                context = default!;
                error = filterError;
                return false;
            }

            context = new GeoservicesRESTIdentifyContext(
                bbox,
                layers,
                tolerancePixels,
                returnGeometry,
                whereFilter,
                outputWkid,
                mapScale);
            error = null;
            return true;
        }
        catch (GeoservicesRESTQueryException ex)
        {
            context = default!;
            error = GeoservicesRESTErrorHelper.BadRequest(ex.Message);
            return false;
        }
    }

    private static (BoundingBox Bbox, IActionResult? Error) ResolveGeometry(IQueryCollection query, double tolerance)
    {
        if (!query.TryGetValue("geometry", out var geometryValues) || geometryValues.Count == 0)
        {
            return (default!, GeoservicesRESTErrorHelper.BadRequest("Parameter 'geometry' is required."));
        }

        var geometryJson = geometryValues[^1];
        if (geometryJson.IsNullOrWhiteSpace())
        {
            return (default!, GeoservicesRESTErrorHelper.BadRequest("Parameter 'geometry' is required."));
        }
        if (!query.TryGetValue("geometryType", out var geometryTypeValues) || geometryTypeValues.Count == 0)
        {
            return (default!, GeoservicesRESTErrorHelper.BadRequest("Parameter 'geometryType' is required."));
        }

        var geometryType = geometryTypeValues[^1];
        if (geometryType.IsNullOrWhiteSpace())
        {
            return (default!, GeoservicesRESTErrorHelper.BadRequest("Parameter 'geometryType' is required."));
        }
        try
        {
            var node = JsonNode.Parse(geometryJson);
            if (node is null)
            {
                throw new GeoservicesRESTQueryException("Geometry payload is invalid.");
            }

            return geometryType switch
            {
                "esriGeometryPoint" => (CreatePointEnvelope(node, tolerance), null),
                "esriGeometryEnvelope" => (CreateEnvelope(node), null),
                _ => (default!, GeoservicesRESTErrorHelper.BadRequest($"geometryType '{geometryType}' is not supported."))
            };
        }
        catch (JsonException)
        {
            return (default!, GeoservicesRESTErrorHelper.BadRequest("Geometry payload is invalid JSON."));
        }
    }

    private static BoundingBox CreatePointEnvelope(JsonNode node, double tolerance)
    {
        if (node is not JsonObject obj)
        {
            throw new GeoservicesRESTQueryException("Point geometry must be an object.");
        }

        var x = obj["x"]?.GetValue<double?>();
        var y = obj["y"]?.GetValue<double?>();
        if (!x.HasValue || !y.HasValue)
        {
            throw new GeoservicesRESTQueryException("Point geometry must include x and y coordinates.");
        }

        var epsilon = tolerance > 0 ? tolerance : 0.0005;
        return new BoundingBox(x.Value - epsilon, y.Value - epsilon, x.Value + epsilon, y.Value + epsilon);
    }

    private static BoundingBox CreateEnvelope(JsonNode node)
    {
        if (node is not JsonObject obj)
        {
            throw new GeoservicesRESTQueryException("Envelope geometry must be an object.");
        }

        var xmin = obj["xmin"]?.GetValue<double?>();
        var ymin = obj["ymin"]?.GetValue<double?>();
        var xmax = obj["xmax"]?.GetValue<double?>();
        var ymax = obj["ymax"]?.GetValue<double?>();
        if (!xmin.HasValue || !ymin.HasValue || !xmax.HasValue || !ymax.HasValue)
        {
            throw new GeoservicesRESTQueryException("Envelope geometry must include xmin, ymin, xmax, ymax.");
        }

        return new BoundingBox(xmin.Value, ymin.Value, xmax.Value, ymax.Value);
    }

    private static IReadOnlyList<int> ResolveLayerSelection(IQueryCollection query, CatalogServiceView serviceView, out IActionResult? error)
    {
        var layerIds = new List<int>();

        if (!query.TryGetValue("layers", out var layerValues) || layerValues.Count == 0)
        {
            for (var i = 0; i < serviceView.Layers.Count; i++)
            {
                layerIds.Add(i);
            }

            error = null;
            return layerIds;
        }

        var raw = layerValues[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            for (var i = 0; i < serviceView.Layers.Count; i++)
            {
                layerIds.Add(i);
            }

            error = null;
            return layerIds;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(raw, "all"))
        {
            for (var i = 0; i < serviceView.Layers.Count; i++)
            {
                layerIds.Add(i);
            }

            error = null;
            return layerIds;
        }

        var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var idSegment = parts.Length == 2 ? parts[1] : parts[0];
        var segments = QueryParsingHelpers.ParseCsv(idSegment);

        foreach (var segment in segments)
        {
            if (!segment.TryParseInt(out var parsed) || parsed < 0 || parsed >= serviceView.Layers.Count)
            {
                error = GeoservicesRESTErrorHelper.BadRequest($"Layer id '{segment}' is not valid.");
                return Array.Empty<int>();
            }

            if (!layerIds.Contains(parsed))
            {
                layerIds.Add(parsed);
            }
        }

        error = null;
        return layerIds;
    }

    private static (QueryFilter? Filter, IActionResult? Error) ResolveWhereClause(IQueryCollection query)
    {
        if (!query.TryGetValue("where", out var values) || values.Count == 0)
        {
            return (null, null);
        }

        var whereClause = values[^1];
        if (whereClause.IsNullOrWhiteSpace() || whereClause.Trim().EqualsIgnoreCase("1=1"))
        {
            return (null, null);
        }

        // Basic equality support: field=value
        var segments = whereClause.Split('=', 2);
        if (segments.Length != 2)
        {
            return (null, GeoservicesRESTErrorHelper.BadRequest("Only simple equality filters are supported in identify."));
        }

        var fieldName = segments[0].Trim();
        var rawValue = segments[1].Trim().Trim('\'', '"');
        if (fieldName.IsNullOrWhiteSpace())
        {
            return (null, GeoservicesRESTErrorHelper.BadRequest("where clause field cannot be empty."));
        }

        var expression = new QueryBinaryExpression(
            new QueryFieldReference(fieldName),
            QueryBinaryOperator.Equal,
            new QueryConstant(rawValue));

        return (new QueryFilter(expression), null);
    }

    private static bool ResolveBoolean(IQueryCollection query, string key, bool defaultValue)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        var text = values[^1];
        return text switch
        {
            "1" or "true" or "True" => true,
            "0" or "false" or "False" => false,
            _ => defaultValue
        };
    }

    private static int ResolveInt(IQueryCollection query, string key, int defaultValue, bool allowZero)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        if (!values[^1].TryParseInt(out var parsed) || parsed < 0 || (!allowZero && parsed == 0))
        {
            throw new GeoservicesRESTQueryException($"Parameter '{key}' must be a {(allowZero ? "non-negative" : "positive")} integer.");
        }

        return parsed;
    }

    private static double ResolveToleranceInUnits(IQueryCollection query, int tolerancePixels)
    {
        if (tolerancePixels <= 0)
        {
            return 0.0005;
        }

        if (!query.TryGetValue("imageDisplay", out var displayValues) || displayValues.Count == 0 ||
            !query.TryGetValue("mapExtent", out var extentValues) || extentValues.Count == 0)
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var displayRaw = displayValues[^1];
        if (displayRaw.IsNullOrWhiteSpace())
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var displayParts = QueryParsingHelpers.ParseCsv(displayRaw);
        if (displayParts.Count < 2 ||
            !displayParts[0].TryParseDoubleStrict(out var width) ||
            !displayParts[1].TryParseDoubleStrict(out var height) ||
            width <= 0 || height <= 0)
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var extentRaw = extentValues[^1];
        if (extentRaw.IsNullOrWhiteSpace())
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var extentParts = QueryParsingHelpers.ParseCsv(extentRaw);
        if (extentParts.Count < 4 ||
            !extentParts[0].TryParseDoubleStrict(out var xmin) ||
            !extentParts[1].TryParseDoubleStrict(out var ymin) ||
            !extentParts[2].TryParseDoubleStrict(out var xmax) ||
            !extentParts[3].TryParseDoubleStrict(out var ymax))
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var widthUnits = Math.Abs(xmax - xmin);
        var heightUnits = Math.Abs(ymax - ymin);
        if (widthUnits <= 0 || heightUnits <= 0)
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        var resolutionX = widthUnits / width;
        var resolutionY = heightUnits / height;
        var resolution = Math.Max(resolutionX, resolutionY);
        if (resolution <= 0)
        {
            return ConvertToleranceToUnits(tolerancePixels);
        }

        return Math.Max(resolution * tolerancePixels, ConvertToleranceToUnits(1));
    }

    private static double ConvertToleranceToUnits(int tolerancePixels)
    {
        if (tolerancePixels <= 0)
        {
            return 0.0005;
        }

        // Rough approximation: treat tolerance as screen pixels and convert to degrees
        return Math.Max(tolerancePixels * 0.0001, 0.0005);
    }

    private static int? ResolveOutputWkid(IQueryCollection query)
    {
        if (TryParseSpatialReference(query, "outSR", out var wkid))
        {
            return wkid;
        }

        if (TryParseSpatialReference(query, "sr", out wkid))
        {
            return wkid;
        }

        return null;
    }

    private static double? ResolveMapScale(IQueryCollection query)
    {
        if (!query.TryGetValue("mapExtent", out var extentValues) || extentValues.Count == 0)
        {
            return null;
        }

        if (!query.TryGetValue("imageDisplay", out var displayValues) || displayValues.Count == 0)
        {
            return null;
        }

        var extentRaw = extentValues[^1];
        if (extentRaw.IsNullOrWhiteSpace())
        {
            return null;
        }

        var extentParts = QueryParsingHelpers.ParseCsv(extentRaw);
        if (extentParts.Count < 4 ||
            !extentParts[0].TryParseDoubleStrict(out var xmin) ||
            !extentParts[1].TryParseDoubleStrict(out var ymin) ||
            !extentParts[2].TryParseDoubleStrict(out var xmax) ||
            !extentParts[3].TryParseDoubleStrict(out var ymax))
        {
            return null;
        }

        var displayRaw = displayValues[^1];
        if (string.IsNullOrWhiteSpace(displayRaw))
        {
            return null;
        }

        var displayParts = QueryParsingHelpers.ParseCsv(displayRaw);
        if (displayParts.Count < 2 ||
            !displayParts[0].TryParseDoubleStrict(out var widthPixels) ||
            !displayParts[1].TryParseDoubleStrict(out var heightPixels) ||
            widthPixels <= 0 || heightPixels <= 0)
        {
            return null;
        }

        var dpi = 96d;
        if (displayParts.Count >= 3 && displayParts[2].TryParseDoubleStrict(out var parsedDpi) && parsedDpi > 0)
        {
            dpi = parsedDpi;
        }

        var widthUnits = Math.Abs(xmax - xmin);
        var heightUnits = Math.Abs(ymax - ymin);
        if (widthUnits <= 0 || heightUnits <= 0)
        {
            return null;
        }

        var resolution = Math.Max(widthUnits / widthPixels, heightUnits / heightPixels);
        if (resolution <= 0)
        {
            return null;
        }

        const double InchesPerMeter = 39.37d;
        var scale = resolution * dpi * InchesPerMeter;
        return scale > 0 ? scale : null;
    }

    private static bool TryParseSpatialReference(IQueryCollection query, string key, out int wkid)
    {
        wkid = 0;
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return false;
        }

        var raw = values[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out wkid))
        {
            return true;
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(raw, "{"))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                if (document.RootElement.TryGetProperty("wkid", out var wkidElement) &&
                    wkidElement.TryGetInt32(out wkid))
                {
                    return true;
                }

                if (document.RootElement.TryGetProperty("latestWkid", out var latestElement) &&
                    latestElement.TryGetInt32(out wkid))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        var normalized = raw.Trim();
        var separator = normalized.LastIndexOf(':');
        if (separator >= 0 && separator < normalized.Length - 1)
        {
            normalized = normalized[(separator + 1)..];
        }

        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out wkid);
    }
}

internal sealed record GeoservicesRESTIdentifyContext(
    BoundingBox Geometry,
    IReadOnlyList<int> LayerIds,
    int Tolerance,
    bool ReturnGeometry,
    QueryFilter? Filter,
    int? OutputWkid,
    double? MapScale);
