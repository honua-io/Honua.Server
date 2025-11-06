// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// OGC API link object.
/// </summary>
public sealed record OgcLink(
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("title")] string? Title);

/// <summary>
/// OGC API response formats.
/// </summary>
public enum OgcResponseFormat
{
    Json,
    GeoJson,
    Html,
    Mvt,
    FlatGeobuf,
    GeoArrow,
    PmTiles,
    Gml,
    Kml,
    Kmz,
    TopoJson,
    GeoPackage,
    Shapefile,
    JsonLd,
    GeoJsonT,
    Csv,
    Wkt,
    Wkb
}

/// <summary>
/// Result wrapper that adds an HTTP header to the response.
/// </summary>
internal sealed class HeaderResult : IResult
{
    private readonly IResult _inner;
    private readonly string _headerName;
    private readonly string _headerValue;

    public HeaderResult(IResult inner, string headerName, string headerValue)
    {
        _inner = Guard.NotNull(inner);
        _headerName = Guard.NotNull(headerName);
        _headerValue = Guard.NotNull(headerValue);
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        Guard.NotNull(httpContext);

        if (_headerValue.HasValue())
        {
            httpContext.Response.Headers[_headerName] = _headerValue;
        }

        return _inner.ExecuteAsync(httpContext);
    }
}

/// <summary>
/// Summary information for an OGC collection.
/// </summary>
internal sealed record CollectionSummary(
    string Id,
    string? Title,
    string? Description,
    string? ItemType,
    IReadOnlyList<string> Crs,
    string? StorageCrs);
