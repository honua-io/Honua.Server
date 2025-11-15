// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
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
    private const string CollectionIdSeparator = "::";
    internal const string ApiDefinitionFileName = "ogc-openapi.json";
    private const string DefaultTemporalReferenceSystem = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian";
    private const string HtmlMediaType = "text/html";
    internal const string HtmlContentType = HtmlMediaType + "; charset=utf-8";
    internal static readonly JsonSerializerOptions GeoJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    internal static readonly string[] DefaultConformanceClasses =
    {
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/search",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql2-json",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/features-filter",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/spatial-operators",
        "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/temporal-operators",
        // OGC API - Tiles conformance classes (spec version 1.0)
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/tileset",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/tilesets-list",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/collections",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/geodata-tilesets",
        "http://www.opengis.net/spec/ogcapi-tiles-1/1.0/conf/oas30"
    };
    private const int OverlayFetchBatchSize = 500;
    private const int OverlayFetchMaxFeatures = 10_000;

    internal sealed record CollectionSummary(
        string Id,
        string? Title,
        string? Description,
        string? ItemType,
        IReadOnlyList<string> Crs,
        string? StorageCrs);

    internal sealed record HtmlFeatureEntry(
        string CollectionId,
        string? CollectionTitle,
        FeatureComponents Components);

    private sealed record SearchIterationResult(long? NumberMatched, long NumberReturned);

    private sealed record SearchCollectionContext(string CollectionId, FeatureContext FeatureContext);

    private sealed class HeaderResult : IResult
    {
        private readonly IResult inner;
        private readonly string headerName;
        private readonly string headerValue;

        public HeaderResult(IResult inner, string headerName, string headerValue)
        {
            this.inner = Guard.NotNull(inner);
            this.headerName = Guard.NotNull(headerName);
            this.headerValue = Guard.NotNull(headerValue);
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            Guard.NotNull(httpContext);

            if (this.headerValue.HasValue())
            {
                httpContext.Response.Headers[this.headerName] = this.headerValue;
            }

            return this.inner.ExecuteAsync(httpContext);
        }
    }

    private static readonly JsonSerializerOptions HtmlJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions RuntimeSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
