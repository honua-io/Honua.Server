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

    internal static void AppendStyleMetadata(IDictionary<string, object?> target, LayerDefinition layer)
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

    internal static IResult WithResponseHeader(IResult result, string headerName, string headerValue)
        => new HeaderResult(result, headerName, headerValue);

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

    internal static List<OgcLink> BuildLayerGroupCollectionLinks(HttpRequest request, ServiceDefinition service, LayerGroupDefinition layerGroup, string collectionId)
    {
        var links = new List<OgcLink>(layerGroup.Links.Select(ToLink));
        links.AddRange(new[]
        {
            BuildLink(request, $"/ogc/collections/{collectionId}", "self", "application/json", "This collection"),
            BuildLink(request, $"/ogc/collections/{collectionId}", "alternate", "text/html", "This collection as HTML"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "items", "application/geo+json", "Features in this layer group"),
            BuildLink(request, $"/ogc/collections/{collectionId}/items", "http://www.opengis.net/def/rel/ogc/1.0/items", "application/geo+json", "Features")
        });

        if (layerGroup.StyleIds.Count > 0)
        {
            links.Add(BuildLink(request, $"/ogc/collections/{collectionId}/styles", "styles", "application/json", "Styles for this layer group"));
        }

        return links;
    }

    internal static IReadOnlyList<object> BuildTileMatrixSetLinks(HttpRequest request, string collectionId, string tilesetId)
    {
        return new object[]
        {
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldCrs84QuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldCrs84QuadUri,
                crs = OgcTileMatrixHelper.WorldCrs84QuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldCrs84QuadId}", null, null)
            },
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldWebMercatorQuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldWebMercatorQuadUri,
                crs = OgcTileMatrixHelper.WorldWebMercatorQuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldWebMercatorQuadId}", null, null)
            }
        };
    }

    internal static object BuildTileMatrixSetSummary(HttpRequest request, string id, string uri, string crs)
    {
        return new
        {
            id,
            title = id,
            tileMatrixSetUri = uri,
            crs,
            links = new[]
            {
                BuildLink(request, $"/ogc/tileMatrixSets/{id}", "self", "application/json", $"{id} definition")
            }
        };
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

    internal static string FormatContentCrs(string? value)
        => value.IsNullOrWhiteSpace() ? string.Empty : $"<{value}>";

    /// <summary>
    /// Adds a Content-Crs header to the result with proper formatting.
    /// This consolidates the common pattern of calling WithResponseHeader + FormatContentCrs.
    /// </summary>
    internal static IResult WithContentCrsHeader(IResult result, string? contentCrs)
        => WithResponseHeader(result, "Content-Crs", FormatContentCrs(contentCrs));
}
