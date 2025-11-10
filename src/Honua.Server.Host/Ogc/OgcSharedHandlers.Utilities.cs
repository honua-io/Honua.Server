// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains utility and schema building methods.

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
    private static QueryFilter? CombineFilters(QueryFilter? first, QueryFilter? second)
    {
        if (first?.Expression is null)
        {
            return second;
        }

        if (second?.Expression is null)
        {
            return first;
        }

        var combined = new QueryBinaryExpression(first.Expression, QueryBinaryOperator.And, second.Expression);
        return new QueryFilter(combined);
    }

    private static (QueryFilter? Filter, IResult? Error) BuildIdsFilter(LayerDefinition layer, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return (null, null);
        }

        // Limit IDs to prevent unbounded OR expressions that hammer the database
        const int MaxIds = 1000;
        if (ids.Count > MaxIds)
        {
            return (null, CreateValidationProblem($"ids parameter exceeds maximum limit of {MaxIds} identifiers.", "ids"));
        }

        QueryExpression? expression = null;
        (string FieldName, string? FieldType) resolved;

        try
        {
            resolved = CqlFilterParserUtils.ResolveField(layer, layer.IdField);
        }
        catch (Exception ex)
        {
            return (null, CreateValidationProblem(ex.Message, "ids"));
        }

        foreach (var rawId in ids)
        {
            if (rawId.IsNullOrWhiteSpace())
            {
                continue;
            }

            var typedValue = CqlFilterParserUtils.ConvertToFieldValue(resolved.FieldType, rawId);
            var comparison = new QueryBinaryExpression(
                new QueryFieldReference(resolved.FieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(typedValue));

            expression = expression is null
                ? comparison
                : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparison);
        }

        if (expression is null)
        {
            return (null, CreateValidationProblem("ids parameter must include at least one non-empty value.", "ids"));
        }

        return (new QueryFilter(expression), null);
    }
    internal static bool LooksLikeJson(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }
    internal static object BuildQueryablesSchema(LayerDefinition layer)
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();

        var fields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        foreach (var field in fields)
        {
            var schema = CreateQueryablesPropertySchema(field);
            if (schema is null)
            {
                continue;
            }

            properties[field.Name] = schema;
            if (!field.Nullable)
            {
                required.Add(field.Name);
            }
        }

        if (!properties.ContainsKey(layer.GeometryField))
        {
            properties[layer.GeometryField] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["title"] = layer.Title.IsNullOrWhiteSpace() ? layer.Id : layer.Title,
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (required.Count > 0)
        {
            result["required"] = required;
        }

        return result;
    }

    private static object? CreateQueryablesPropertySchema(FieldDefinition field)
    {
        var kind = (field.DataType ?? field.StorageType ?? "string").Trim().ToLowerInvariant();

        if (string.Equals(kind, "geometry", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["content"] = new Dictionary<string, object>
                {
                    ["application/geo+json"] = new { }
                }
            };
        }

        var schema = new Dictionary<string, object>();

        switch (kind)
        {
            case "int":
            case "integer":
            case "int16":
            case "int32":
            case "short":
            case "smallint":
                schema["type"] = "integer";
                break;
            case "int64":
            case "long":
            case "bigint":
                schema["type"] = "integer";
                schema["format"] = "int64";
                break;
            case "double":
            case "float":
            case "single":
            case "real":
            case "decimal":
            case "numeric":
                schema["type"] = "number";
                break;
            case "date":
            case "datetime":
            case "datetimeoffset":
            case "time":
                schema["type"] = "string";
                schema["format"] = "date-time";
                break;
            case "bool":
            case "boolean":
                schema["type"] = "boolean";
                break;
            case "uuid":
            case "guid":
            case "uniqueidentifier":
                schema["type"] = "string";
                schema["format"] = "uuid";
                break;
            default:
                schema["type"] = "string";
                break;
        }

        if (field.MaxLength.HasValue && field.MaxLength.Value > 0 && schema.TryGetValue("type", out var typeValue) &&
            string.Equals(typeValue as string, "string", StringComparison.OrdinalIgnoreCase))
        {
            schema["maxLength"] = field.MaxLength.Value;
        }

        return schema;
    }

    internal static List<OgcLink> BuildCollectionLinks(HttpRequest request, ServiceDefinition service, LayerDefinition layer, string collectionId)
    {
        var links = new List<OgcLink>(layer.Links.Select(ToLink));
        links.AddRange(new[]
        {
            BuildLink(request, $"/ogc/collections/{collectionId}", "self", "application/json", layer.Title),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "items", "application/geo+json", $"Items for {layer.Title}"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/vnd.google-earth.kml+xml", $"Items for {layer.Title} (KML)", null, new Dictionary<string, string?> { ["f"] = "kml" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/vnd.google-earth.kmz", $"Items for {layer.Title} (KMZ)", null, new Dictionary<string, string?> { ["f"] = "kmz" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "alternate", "application/geopackage+sqlite3", $"Items for {layer.Title} (GeoPackage)", null, new Dictionary<string, string?> { ["f"] = "geopackage" }),
            BuildLink(request, $"/ogc/collections/{collectionId}/queryables", "queryables", "application/json", $"Queryables for {layer.Title}")
        });

        if (layer.DefaultStyleId.HasValue())
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles/{layer.DefaultStyleId}", "stylesheet", "application/vnd.ogc.sld+xml", $"Default style for {layer.Title}"));
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (!string.Equals(styleId, layer.DefaultStyleId, StringComparison.OrdinalIgnoreCase))
            {
                links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles/{styleId}", "stylesheet", "application/vnd.ogc.sld+xml", $"Style '{styleId}'"));
            }
        }

        return links;
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

    internal static IReadOnlyList<string> BuildOrderedStyleIds(LayerDefinition layer)
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
    internal static IResult WithResponseHeader(IResult result, string headerName, string headerValue)
        => new HeaderResult(result, headerName, headerValue);

    internal static string FormatContentCrs(string? value)
        => value.IsNullOrWhiteSpace() ? string.Empty : $"<{value}>";

    /// <summary>
    /// Adds a Content-Crs header to the result with proper formatting.
    /// This consolidates the common pattern of calling WithResponseHeader + FormatContentCrs.
    /// </summary>
    internal static IResult WithContentCrsHeader(IResult result, string? contentCrs)
        => WithResponseHeader(result, "Content-Crs", FormatContentCrs(contentCrs));
    internal static IReadOnlyList<string> BuildOrderedStyleIds(LayerGroupDefinition layerGroup)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        if (layerGroup.DefaultStyleId.HasValue() && seen.Add(layerGroup.DefaultStyleId))
        {
            results.Add(layerGroup.DefaultStyleId);
        }

        foreach (var styleId in layerGroup.StyleIds)
        {
            if (styleId.HasValue() && seen.Add(styleId))
            {
                results.Add(styleId);
            }
        }

        return results;
    }
    internal static IResult CreateValidationProblem(string detail, string parameter)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request parameter",
            Detail = detail,
            Extensions = { ["parameter"] = parameter }
        };

        return Results.Problem(problemDetails.Detail, statusCode: problemDetails.Status, title: problemDetails.Title, extensions: problemDetails.Extensions);
    }

    internal static IResult CreateNotFoundProblem(string detail)
    {
        return Results.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
    }
}
