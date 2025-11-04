// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for building OGC API links, including collection links, item links, pagination links,
/// and resource hrefs with proper query parameter handling.
/// </summary>
internal sealed class OgcLinkBuilder
{
    /// <summary>
    /// Builds a single OGC link with query parameters.
    /// </summary>
    internal OgcLink BuildLink(
        HttpRequest request,
        string relativePath,
        string rel,
        string type,
        string? title,
        FeatureQuery? query = null,
        IDictionary<string, string?>? overrides = null)
    {
        var href = BuildHref(request, relativePath, query, overrides);
        return new OgcLink(href, rel, type, title);
    }

    /// <summary>
    /// Converts a LinkDefinition to an OgcLink.
    /// </summary>
    internal OgcLink ToLink(LinkDefinition link)
    {
        return new OgcLink(
            link.Href,
            link.Rel.IsNullOrWhiteSpace() ? "related" : link.Rel,
            link.Type,
            link.Title
        );
    }

    /// <summary>
    /// Builds an HREF with query parameters using RequestLinkHelper for consistent URL generation.
    /// Respects proxy headers (X-Forwarded-Proto, X-Forwarded-Host) and handles query parameter merging.
    /// </summary>
    internal string BuildHref(
        HttpRequest request,
        string relativePath,
        FeatureQuery? query,
        IDictionary<string, string?>? overrides)
    {
        var queryParameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (query is not null)
        {
            if (query.Limit.HasValue)
            {
                queryParameters["limit"] = query.Limit.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (query.Offset.HasValue)
            {
                queryParameters["offset"] = query.Offset.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (query.Crs.HasValue())
            {
                queryParameters["crs"] = query.Crs;
            }

            // Carry forward all active filters (bbox, datetime, filter, property selections)
            if (query.Bbox is not null)
            {
                var bbox = query.Bbox;
                var bboxValue = bbox.MinZ.HasValue && bbox.MaxZ.HasValue
                    ? $"{bbox.MinX:G17},{bbox.MinY:G17},{bbox.MinZ:G17},{bbox.MaxX:G17},{bbox.MaxY:G17},{bbox.MaxZ:G17}"
                    : $"{bbox.MinX:G17},{bbox.MinY:G17},{bbox.MaxX:G17},{bbox.MaxY:G17}";
                queryParameters["bbox"] = bboxValue;

                if (bbox.Crs.HasValue())
                {
                    queryParameters["bbox-crs"] = bbox.Crs;
                }
            }

            if (query.Temporal is not null)
            {
                var temporal = query.Temporal;
                var start = temporal.Start?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "..";
                var end = temporal.End?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "..";
                queryParameters["datetime"] = $"{start}/{end}";
            }

            if (query.Filter is not null)
            {
                var filterStr = query.Filter.Expression?.ToString();
                if (!string.IsNullOrWhiteSpace(filterStr))
                {
                    queryParameters["filter"] = filterStr;
                }
            }

            if (query.PropertyNames is not null && query.PropertyNames.Count > 0)
            {
                queryParameters["properties"] = string.Join(',', query.PropertyNames);
            }

            if (query.ResultType == FeatureResultType.Hits)
            {
                queryParameters["resultType"] = "hits";
            }
        }

        // Apply overrides (null value = remove parameter)
        if (overrides is not null)
        {
            foreach (var kvp in overrides)
            {
                if (kvp.Value is null)
                {
                    queryParameters.Remove(kvp.Key);
                }
                else
                {
                    queryParameters[kvp.Key] = kvp.Value;
                }
            }
        }

        // Use RequestLinkHelper for consistent URL generation with proxy header support
        return request.BuildAbsoluteUrl(relativePath, queryParameters);
    }

    /// <summary>
    /// Builds collection-level links including items, queryables, and style links.
    /// </summary>
    internal List<OgcLink> BuildCollectionLinks(
        HttpRequest request,
        ServiceDefinition service,
        LayerDefinition layer,
        string collectionId)
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

    /// <summary>
    /// Builds item/feature links including self, collection, alternate formats, and pagination links.
    /// </summary>
    internal List<OgcLink> BuildItemsLinks(
        HttpRequest request,
        string collectionId,
        FeatureQuery query,
        long? numberMatched,
        OgcResponseFormat format,
        string contentType)
    {
        var basePath = $"/ogc/collections/{collectionId}/items";
        var links = new List<OgcLink>
        {
            BuildLink(request, basePath, "self", contentType, "This page", query),
            BuildLink(request, $"/ogc/collections/{collectionId}", "collection", "application/json", "Collection"),
            BuildLink(request, basePath, "alternate", "application/vnd.google-earth.kml+xml", "KML items", query, new Dictionary<string, string?> { ["f"] = "kml" }),
            BuildLink(request, basePath, "alternate", "application/vnd.google-earth.kmz", "KMZ items", query, new Dictionary<string, string?> { ["f"] = "kmz" }),
            BuildLink(request, basePath, "alternate", "application/topo+json", "TopoJSON items", query, new Dictionary<string, string?> { ["f"] = "topojson" }),
            BuildLink(request, basePath, "alternate", "application/geopackage+sqlite3", "GeoPackage items", query, new Dictionary<string, string?> { ["f"] = "geopackage" }),
            BuildLink(request, basePath, "alternate", "application/zip", "Shapefile items", query, new Dictionary<string, string?> { ["f"] = "shapefile" }),
            BuildLink(request, basePath, "alternate", "application/vnd.flatgeobuf", "FlatGeobuf items", query, new Dictionary<string, string?> { ["f"] = "flatgeobuf" }),
            BuildLink(request, basePath, "alternate", "application/vnd.apache.arrow.stream", "GeoArrow items", query, new Dictionary<string, string?> { ["f"] = "geoarrow" }),
            BuildLink(request, basePath, "alternate", "text/csv", "CSV items", query, new Dictionary<string, string?> { ["f"] = "csv" }),
            BuildLink(request, basePath, "alternate", "application/ld+json", "JSON-LD items", query, new Dictionary<string, string?> { ["f"] = "jsonld" }),
            BuildLink(request, basePath, "alternate", "application/geo+json-t", "GeoJSON-T items", query, new Dictionary<string, string?> { ["f"] = "geojson-t" })
        };

        AddPaginationLinks(links, request, basePath, query, numberMatched, contentType);
        return links;
    }

    /// <summary>
    /// Builds search links for cross-collection search with pagination.
    /// </summary>
    internal List<OgcLink> BuildSearchLinks(
        HttpRequest request,
        IReadOnlyList<string> collections,
        FeatureQuery query,
        long? numberMatched,
        string contentType)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["collections"] = string.Join(',', collections)
        };

        var links = new List<OgcLink>
        {
            BuildLink(request, "/ogc/search", "self", contentType, "This page", query, overrides),
            BuildLink(request, "/ogc/collections", "data", "application/json", "Collections")
        };

        AddPaginationLinks(links, request, "/ogc/search", query, numberMatched, contentType, overrides);
        return links;
    }

    /// <summary>
    /// Builds tile matrix set links for raster tiles.
    /// </summary>
    internal IReadOnlyList<object> BuildTileMatrixSetLinks(HttpRequest request, string collectionId, string tilesetId)
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

    /// <summary>
    /// Adds pagination links (next/prev) to a link collection based on query parameters.
    /// </summary>
    private void AddPaginationLinks(
        List<OgcLink> links,
        HttpRequest request,
        string basePath,
        FeatureQuery query,
        long? numberMatched,
        string contentType,
        IDictionary<string, string?>? baseOverrides = null)
    {
        if (query.ResultType == FeatureResultType.Hits)
        {
            return; // No pagination for hits-only queries
        }

        var limit = query.Limit ?? 0;
        var offset = query.Offset ?? 0;

        if (limit <= 0)
        {
            return; // No pagination without a limit
        }

        // Next link
        if (numberMatched.HasValue)
        {
            var remaining = (int)Math.Max(0, numberMatched.Value - offset);
            var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, Math.Min(remaining, limit));
            if (nextOffset.HasValue && nextOffset.Value < numberMatched.Value)
            {
                var nextParameters = new Dictionary<string, string?>(baseOverrides ?? new Dictionary<string, string?>())
                {
                    ["offset"] = nextOffset.Value.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                };
                links.Add(BuildLink(request, basePath, "next", contentType, "Next page", query, nextParameters));
            }
        }

        // Previous link
        if (PaginationHelper.HasPrevPage(offset))
        {
            var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit)!.Value;
            var prevParameters = new Dictionary<string, string?>(baseOverrides ?? new Dictionary<string, string?>())
            {
                ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture),
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
            };
            links.Add(BuildLink(request, basePath, "prev", contentType, "Previous page", query, prevParameters));
        }
    }
}
