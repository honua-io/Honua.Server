// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Parses OGC API query parameters and request headers.
/// </summary>
internal static class OgcQueryParser
{
    public static (OgcResponseFormat Format, IResult? Error) ParseFormat(string? raw)
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
            "geopkg" => (OgcResponseFormat.GeoPackage, null),
            "geopackage" => (OgcResponseFormat.GeoPackage, null),
            "application/geopackage+sqlite3" => (OgcResponseFormat.GeoPackage, null),
            "shapefile" => (OgcResponseFormat.Shapefile, null),
            "shp" => (OgcResponseFormat.Shapefile, null),
            "application/x-esri-shapefile" => (OgcResponseFormat.Shapefile, null),
            "wkt" => (OgcResponseFormat.Wkt, null),
            "text/wkt" => (OgcResponseFormat.Wkt, null),
            "application/wkt" => (OgcResponseFormat.Wkt, null),
            "wkb" => (OgcResponseFormat.Wkb, null),
            "application/wkb" => (OgcResponseFormat.Wkb, null),
            "application/vnd.ogc.wkb" => (OgcResponseFormat.Wkb, null),
            _ => (default, OgcProblemDetails.CreateValidationProblem($"Unsupported format '{raw}'.", "f"))
        };
    }

    public static (OgcResponseFormat Format, string ContentType, IResult? Error) ResolveResponseFormat(
        HttpRequest request,
        IQueryCollection? queryOverrides = null)
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
                var ordered = parsedAccepts
                    .OrderByDescending(value => value.Quality ?? 1.0)
                    .ToList();

                foreach (var media in ordered)
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

                return (default, string.Empty, OgcProblemDetails.CreateNotAcceptableProblem("None of the requested media types are supported."));
            }
        }

        return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
    }

    public static bool TryMapMediaType(string mediaType, out OgcResponseFormat format)
    {
        format = mediaType.ToLowerInvariant() switch
        {
            "application/geo+json" or "application/json" or "application/vnd.geo+json" => OgcResponseFormat.GeoJson,
            "text/html" or "application/xhtml+xml" => OgcResponseFormat.Html,
            "application/vnd.google-earth.kml+xml" => OgcResponseFormat.Kml,
            "application/vnd.google-earth.kmz" => OgcResponseFormat.Kmz,
            "application/topo+json" => OgcResponseFormat.TopoJson,
            "application/geopackage+sqlite3" or "application/vnd.sqlite3" => OgcResponseFormat.GeoPackage,
            "application/zip" or "application/x-esri-shapefile" => OgcResponseFormat.Shapefile,
            "text/wkt" or "application/wkt" => OgcResponseFormat.Wkt,
            "application/wkb" or "application/vnd.ogc.wkb" => OgcResponseFormat.Wkb,
            _ => (OgcResponseFormat)0
        };

        return format != 0;
    }

    public static (string? Value, IResult? Error) ResolveAcceptCrs(HttpRequest request, IReadOnlyCollection<string> supported)
    {
        if (!request.Headers.TryGetValue("Accept-Crs", out var headerValues) || headerValues.Count == 0)
        {
            return (null, null);
        }

        var candidates = new List<(string Crs, double Quality)>();
        foreach (var header in headerValues)
        {
            if (header.IsNullOrWhiteSpace())
            {
                continue;
            }

            foreach (var token in QueryParsingHelpers.ParseCsv(header))
            {
                var semicolonIndex = token.IndexOf(';');
                var crsToken = semicolonIndex >= 0 ? token[..semicolonIndex] : token;
                var quality = 1.0;

                if (semicolonIndex >= 0)
                {
                    var parameters = token[(semicolonIndex + 1)..]
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var parameter in parameters)
                    {
                        var parts = parameter.Split('=', 2, StringSplitOptions.TrimEntries);
                        if (parts.Length == 2 && string.Equals(parts[0], "q", StringComparison.OrdinalIgnoreCase) &&
                            parts[1].TryParseDoubleStrict(out var parsedQ))
                        {
                            quality = parsedQ;
                        }
                    }
                }

                candidates.Add((CrsHelper.NormalizeIdentifier(crsToken), quality));
            }
        }

        if (candidates.Count == 0)
        {
            return (null, null);
        }

        foreach (var candidate in candidates.OrderByDescending(c => c.Quality))
        {
            if (supported.Contains(candidate.Crs, StringComparer.OrdinalIgnoreCase))
            {
                return (candidate.Crs, null);
            }
        }

        var supportedList = string.Join(", ", supported);
        return (null, OgcProblemDetails.CreateValidationProblem(
            $"None of the requested CRS values are supported. Supported CRS: {supportedList}",
            "Accept-Crs"));
    }

    public static (string Value, IResult? Error) ResolveContentCrs(
        string? requestedCrs,
        ServiceDefinition service,
        LayerDefinition layer)
    {
        var supported = OgcSharedHandlers.ResolveSupportedCrs(service, layer);

        if (requestedCrs.IsNullOrWhiteSpace())
        {
            var defaultCrs = OgcSharedHandlers.DetermineDefaultCrs(service, supported);
            return (defaultCrs, null);
        }

        var normalized = CrsHelper.NormalizeIdentifier(requestedCrs);
        if (supported.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return (normalized, null);
        }

        var supportedList = string.Join(", ", supported);
        return (string.Empty, OgcProblemDetails.CreateValidationProblem(
            $"CRS '{requestedCrs}' is not supported. Supported CRS: {supportedList}",
            "crs"));
    }


    private static string GetMimeType(OgcResponseFormat format)
    {
        return format switch
        {
            OgcResponseFormat.GeoJson => "application/geo+json",
            OgcResponseFormat.Json => "application/json",
            OgcResponseFormat.Html => "text/html; charset=utf-8",
            OgcResponseFormat.Mvt => "application/vnd.mapbox-vector-tile",
            OgcResponseFormat.Gml => "application/gml+xml",
            OgcResponseFormat.Kml => "application/vnd.google-earth.kml+xml",
            OgcResponseFormat.Kmz => "application/vnd.google-earth.kmz",
            OgcResponseFormat.TopoJson => "application/topo+json",
            OgcResponseFormat.GeoPackage => "application/geopackage+sqlite3",
            OgcResponseFormat.Shapefile => "application/x-esri-shapefile",
            OgcResponseFormat.Wkt => "text/wkt; charset=utf-8",
            OgcResponseFormat.Wkb => "application/wkb",
            _ => "application/geo+json"
        };
    }
}
