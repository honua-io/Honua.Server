// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains link generation methods for OGC API responses.

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
    internal static List<OgcLink> BuildItemsLinks(HttpRequest request, string collectionId, FeatureQuery query, long? numberMatched, OgcResponseFormat format, string contentType)
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

        var limit = query.Limit ?? 0;
        var offset = query.Offset ?? 0;

        if (query.ResultType != FeatureResultType.Hits)
        {
            // Use PaginationHelper for next/prev offset calculations
            if (limit > 0 && numberMatched.HasValue)
            {
                // BUG FIX #12: Clamp remaining to non-negative to prevent ArgumentOutOfRangeException
                // when offset exceeds numberMatched (out-of-range pagination should return empty page)
                var remaining = (int)Math.Max(0, numberMatched.Value - offset);
                var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, Math.Min(remaining, limit));
                if (nextOffset.HasValue && nextOffset.Value < numberMatched.Value)
                {
                    var nextParameters = new Dictionary<string, string?>
                    {
                        ["offset"] = nextOffset.Value.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                    };
                    links.Add(BuildLink(request, basePath, "next", contentType, "Next page", query, nextParameters));
                }
            }

            if (limit > 0 && PaginationHelper.HasPrevPage(offset))
            {
                var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit)!.Value;
                var prevParameters = new Dictionary<string, string?>
                {
                    ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                };
                links.Add(BuildLink(request, basePath, "prev", contentType, "Previous page", query, prevParameters));
            }
        }

        return links;
    }

internal static List<OgcLink> BuildSearchLinks(HttpRequest request, IReadOnlyList<string> collections, FeatureQuery query, long? numberMatched, string contentType)
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

        if (query.ResultType != FeatureResultType.Hits)
        {
            var limit = query.Limit ?? 0;
            var offset = query.Offset ?? 0;

            // Use PaginationHelper for next/prev offset calculations
            if (limit > 0 && numberMatched.HasValue)
            {
                // BUG FIX #13: Clamp remaining to non-negative to prevent ArgumentOutOfRangeException
                // when offset exceeds numberMatched in /ogc/search (should return empty page, not 500)
                var remaining = (int)Math.Max(0, numberMatched.Value - offset);
                var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, Math.Min(remaining, limit));
                if (nextOffset.HasValue && nextOffset.Value < numberMatched.Value)
                {
                    var nextOverrides = new Dictionary<string, string?>(overrides)
                    {
                        ["offset"] = nextOffset.Value.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                    };
                    links.Add(BuildLink(request, "/ogc/search", "next", contentType, "Next page", query, nextOverrides));
                }
            }

            if (limit > 0 && PaginationHelper.HasPrevPage(offset))
            {
                var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit)!.Value;
                var prevOverrides = new Dictionary<string, string?>(overrides)
                {
                    ["offset"] = prevOffset.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
                };
                links.Add(BuildLink(request, "/ogc/search", "prev", contentType, "Previous page", query, prevOverrides));
            }
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
    internal static OgcLink BuildLink(HttpRequest request, string relativePath, string rel, string type, string? title, FeatureQuery? query = null, IDictionary<string, string?>? overrides = null)
    {
        var href = BuildHref(request, relativePath, query, overrides);
        return new OgcLink(href, rel, type, title);
    }

    internal static OgcLink ToLink(LinkDefinition link)
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
    /// Automatically detects and preserves the API version prefix (/v1, /v2, etc.) from the incoming request path.
    /// </summary>
    internal static string BuildHref(HttpRequest request, string relativePath, FeatureQuery? query, IDictionary<string, string?>? overrides)
    {
        // BUG FIX: Preserve API version prefix in OGC links
        // OGC endpoints are available at both /ogc and /v1/ogc
        // We need to detect which one was used and preserve it in generated links
        var requestPath = request.Path.Value ?? string.Empty;
        var versionedPath = relativePath;

        // Check if the request came through a versioned endpoint (e.g., /v1/ogc)
        if (requestPath.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            var firstSegmentEnd = requestPath.IndexOf('/', 1);
            if (firstSegmentEnd > 0)
            {
                var versionPrefix = requestPath.Substring(0, firstSegmentEnd); // e.g., "/v1"

                // Only add version prefix if relativePath doesn't already have it
                if (!relativePath.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    versionedPath = versionPrefix + relativePath;
                }
            }
        }

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

            // BUG FIX #14: Pagination links drop bbox/temporal filters
            // BUG FIX #15: CQL filter context is lost in OGC links
            // Carry forward all active filters (bbox, datetime, filter, property selections) when constructing navigation links
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

            // BUG FIX #15: Preserve CQL filter context in pagination links
            // Note: We only preserve the filter expression string.  The original filter-lang and filter-crs
            // are not stored in FeatureQuery (only the parsed QueryExpression), so they cannot be reconstructed here.
            // Callers should pass filter-lang and filter-crs via overrides parameter if they need to be preserved.
            if (query.Filter is not null)
            {
                // Serialize the parsed filter expression back to string
                // This won't be identical to the original filter string, but preserves the predicate logic
                var filterStr = query.Filter.Expression?.ToString();
                if (!string.IsNullOrWhiteSpace(filterStr))
                {
                    queryParameters["filter"] = filterStr;
                }
            }

            // BUG FIX #15: Preserve property selections in pagination links
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
        // Use versionedPath to preserve the version prefix from the incoming request
        return request.BuildAbsoluteUrl(versionedPath, queryParameters);
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
}
