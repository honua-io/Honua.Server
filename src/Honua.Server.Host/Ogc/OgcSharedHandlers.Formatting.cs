// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains format resolution and conversion methods.

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
    private static (OgcResponseFormat Format, IResult? Error) ParseFormat(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (OgcResponseFormat.GeoJson, null);
        }

        return raw.ToLowerInvariant() switch
        {
            "json" => (OgcResponseFormat.GeoJson, null),
            "geojson" => (OgcResponseFormat.GeoJson, null),
            "html" => (OgcResponseFormat.Html, null),
            "text/html" => (OgcResponseFormat.Html, null),
            "kml" => (OgcResponseFormat.Kml, null),
            "application/vnd.google-earth.kml+xml" => (OgcResponseFormat.Kml, null),
            "kmz" => (OgcResponseFormat.Kmz, null),
            "application/vnd.google-earth.kmz" => (OgcResponseFormat.Kmz, null),
            "topojson" => (OgcResponseFormat.TopoJson, null),
            "application/topo+json" => (OgcResponseFormat.TopoJson, null),
            "flatgeobuf" => (OgcResponseFormat.FlatGeobuf, null),
            "fgb" => (OgcResponseFormat.FlatGeobuf, null),
            "application/vnd.flatgeobuf" => (OgcResponseFormat.FlatGeobuf, null),
            "geoarrow" => (OgcResponseFormat.GeoArrow, null),
            "arrow" => (OgcResponseFormat.GeoArrow, null),
            "application/vnd.apache.arrow.stream" => (OgcResponseFormat.GeoArrow, null),
            "application/vnd.apache.arrow.file" => (OgcResponseFormat.GeoArrow, null),
            "geopkg" => (OgcResponseFormat.GeoPackage, null),
            "geopackage" => (OgcResponseFormat.GeoPackage, null),
            "application/geopackage+sqlite3" => (OgcResponseFormat.GeoPackage, null),
            "shapefile" => (OgcResponseFormat.Shapefile, null),
            "shp" => (OgcResponseFormat.Shapefile, null),
            "application/x-esri-shapefile" => (OgcResponseFormat.Shapefile, null),
            "jsonld" => (OgcResponseFormat.JsonLd, null),
            "json-ld" => (OgcResponseFormat.JsonLd, null),
            "application/ld+json" => (OgcResponseFormat.JsonLd, null),
            "geojson-t" => (OgcResponseFormat.GeoJsonT, null),
            "geojsont" => (OgcResponseFormat.GeoJsonT, null),
            "application/geo+json-t" => (OgcResponseFormat.GeoJsonT, null),
            "csv" => (OgcResponseFormat.Csv, null),
            "text/csv" => (OgcResponseFormat.Csv, null),
            "wkt" => (OgcResponseFormat.Wkt, null),
            "text/wkt" => (OgcResponseFormat.Wkt, null),
            "application/wkt" => (OgcResponseFormat.Wkt, null),
            "wkb" => (OgcResponseFormat.Wkb, null),
            "application/wkb" => (OgcResponseFormat.Wkb, null),
            "application/vnd.ogc.wkb" => (OgcResponseFormat.Wkb, null),
            _ => (default, CreateValidationProblem($"Unsupported format '{raw}'.", "f"))
        };
    }

    internal static (OgcResponseFormat Format, string ContentType, IResult? Error) ResolveResponseFormat(HttpRequest request, IQueryCollection? queryOverrides = null)
    {
        var formatParameter = queryOverrides?["f"].ToString();
        if (formatParameter.IsNullOrWhiteSpace())
        {
            formatParameter = request.Query["f"].ToString();
        }
        if (formatParameter.HasValue())
        {
            var (format, error) = ParseFormat(formatParameter);
            if (error is not null)
            {
                return (default, string.Empty, error);
            }

            return (format, GetMimeType(format), null);
        }

        if (request.Headers.TryGetValue(HeaderNames.Accept, out var acceptValues) && acceptValues.Count > 0)
        {
            if (MediaTypeHeaderValue.TryParseList(acceptValues, out var parsedAccepts))
            {
                // Use lazy evaluation - no need to materialize with ToList() when only iterating
                foreach (var media in parsedAccepts
                    .OrderByDescending(value => value.Quality ?? 1.0))
                {
                    var mediaType = media.MediaType.ToString();
                    if (mediaType.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    if (TryMapMediaType(mediaType!, out var mappedFormat))
                    {
                        return (mappedFormat, GetMimeType(mappedFormat), null);
                    }

                    if (string.Equals(mediaType, "*/*", StringComparison.Ordinal))
                    {
                        return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
                    }
                }

                // If no Accept header media types matched, fall back to default format (GeoJSON)
                // This is more lenient than returning 406 and aligns with OGC best practices
            }
        }

        return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
    }

    private static bool TryMapMediaType(string mediaType, out OgcResponseFormat format)
    {
        format = mediaType.ToLowerInvariant() switch
        {
            "application/geo+json" or "application/json" or "application/vnd.geo+json" => OgcResponseFormat.GeoJson,
            "text/html" or "application/xhtml+xml" => OgcResponseFormat.Html,
            "application/vnd.google-earth.kml+xml" => OgcResponseFormat.Kml,
            "application/vnd.google-earth.kmz" => OgcResponseFormat.Kmz,
            "application/topo+json" => OgcResponseFormat.TopoJson,
            "application/vnd.flatgeobuf" => OgcResponseFormat.FlatGeobuf,
            "application/vnd.apache.arrow.stream" or "application/vnd.apache.arrow.file" => OgcResponseFormat.GeoArrow,
            "application/geopackage+sqlite3" or "application/vnd.sqlite3" => OgcResponseFormat.GeoPackage,
            "application/ld+json" => OgcResponseFormat.JsonLd,
            "application/geo+json-t" => OgcResponseFormat.GeoJsonT,
            "application/zip" or "application/x-esri-shapefile" => OgcResponseFormat.Shapefile,
            "text/csv" => OgcResponseFormat.Csv,
            _ => (OgcResponseFormat)0
        };

        return format != 0;
    }
    internal static string GetMimeType(OgcResponseFormat format)
        => format switch
        {
            OgcResponseFormat.Html => HtmlMediaType,
            OgcResponseFormat.Kml => "application/vnd.google-earth.kml+xml",
            OgcResponseFormat.Kmz => "application/vnd.google-earth.kmz",
            OgcResponseFormat.TopoJson => "application/topo+json",
            OgcResponseFormat.FlatGeobuf => "application/vnd.flatgeobuf",
            OgcResponseFormat.GeoArrow => "application/vnd.apache.arrow.stream",
            OgcResponseFormat.GeoPackage => "application/geopackage+sqlite3",
            OgcResponseFormat.Shapefile => "application/zip",
            OgcResponseFormat.Csv => "text/csv",
            OgcResponseFormat.JsonLd => "application/ld+json",
            OgcResponseFormat.GeoJsonT => "application/geo+json-t",
            OgcResponseFormat.Wkt => "text/wkt; charset=utf-8",
            OgcResponseFormat.Wkb => "application/wkb",
            _ => "application/geo+json"
        };
    private static string BuildDownloadFileName(string collectionId, string? featureId, OgcResponseFormat format)
    {
        var baseName = FileNameHelper.SanitizeSegment(collectionId);
        if (featureId.HasValue())
        {
            baseName = $"{baseName}-{FileNameHelper.SanitizeSegment(featureId)}";
        }

        var extension = format == OgcResponseFormat.Kmz ? "kmz" : "kml";
        return $"{baseName}.{extension}";
    }

    internal static double[] ResolveBounds(LayerDefinition layer, RasterDatasetDefinition? dataset)
    {
        if (dataset?.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = dataset.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        if (layer.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = layer.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        return new[] { -180d, -90d, 180d, 90d };
    }

    private static string BuildArchiveEntryName(string collectionId, string? featureId)
    {
        var baseName = FileNameHelper.SanitizeSegment(collectionId);
        if (featureId.HasValue())
        {
            baseName = $"{baseName}-{FileNameHelper.SanitizeSegment(featureId)}";
        }

        return $"{baseName}.kml";
    }
}
