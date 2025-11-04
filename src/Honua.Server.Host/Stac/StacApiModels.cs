// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Honua.Server.Host.Stac;

public sealed record StacRootResponse
{
    [JsonPropertyName("stac_version")]
    public string StacVersion { get; init; } = "1.0.0";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "Catalog";

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string> ConformsTo { get; init; } = StacConstants.DefaultConformance;

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLinkDto> Links { get; init; } = StacConstants.EmptyLinks;
}

public sealed record StacConformanceResponse
{
    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string> ConformsTo { get; init; } = StacConstants.DefaultConformance;
}

public sealed record StacCollectionResponse
{
    [JsonPropertyName("stac_version")]
    public string StacVersion { get; init; } = "1.0.0";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "Collection";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("license")]
    public required string License { get; init; }

    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get; init; } = new List<string>();

    [JsonPropertyName("extent")]
    public JsonObject Extent { get; init; } = new();

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLinkDto> Links { get; init; } = StacConstants.EmptyLinks;

    [JsonPropertyName("summaries")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Summaries { get; init; }

    [JsonPropertyName("assets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Assets { get; init; }

    [JsonPropertyName("item_assets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? ItemAssets { get; init; }

    [JsonPropertyName("providers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonArray? Providers { get; init; }

    [JsonPropertyName("stac_extensions")]
    public IReadOnlyList<string> StacExtensions { get; init; } = new List<string>();

    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalFields { get; init; } = new Dictionary<string, JsonElement>();
}

public sealed record StacCollectionsResponse
{
    [JsonPropertyName("collections")]
    public IReadOnlyList<StacCollectionResponse> Collections { get; init; } = new List<StacCollectionResponse>();

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLinkDto> Links { get; init; } = StacConstants.EmptyLinks;

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? Context { get; init; }
}

public sealed record StacItemResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    [JsonPropertyName("stac_version")]
    public string StacVersion { get; init; } = "1.0.0";

    [JsonPropertyName("stac_extensions")]
    public IReadOnlyList<string> StacExtensions { get; init; } = new List<string>();

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("collection")]
    public string CollectionId { get; init; } = string.Empty;

    [JsonPropertyName("bbox")]
    public double[]? Bbox { get; init; }

    [JsonPropertyName("geometry")]
    public JsonNode? Geometry { get; init; }

    [JsonPropertyName("properties")]
    public JsonObject Properties { get; init; } = new();

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLinkDto> Links { get; init; } = StacConstants.EmptyLinks;

    [JsonPropertyName("assets")]
    public JsonObject Assets { get; init; } = new();
}

public sealed record StacItemCollectionResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    [JsonPropertyName("stac_version")]
    public string StacVersion { get; init; } = "1.0.0";

    [JsonPropertyName("features")]
    public IReadOnlyList<StacItemResponse> Features { get; init; } = new List<StacItemResponse>();

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLinkDto> Links { get; init; } = StacConstants.EmptyLinks;

    [JsonPropertyName("context")]
    public JsonObject? Context { get; init; }
}

public sealed record StacLinkDto
{
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("hreflang")]
    public string? Hreflang { get; init; }
}

internal static class StacConstants
{
    /// <summary>
    /// Conformance classes advertised by the STAC API implementation.
    ///
    /// Note: The "ogcapi-features" conformance class was removed because Honua implements
    /// STAC API - Item Search (which provides /search endpoint with POST and GET),
    /// NOT OGC API - Features (which would require /collections/{collectionId}/items with
    /// CQL2 filtering, multiple coordinate systems, and full OGC Features compliance).
    ///
    /// Honua's STAC implementation conforms to:
    /// - Core: Landing page, conformance, and basic API structure
    /// - Collections: Collection enumeration and metadata
    /// - Item Search: Spatial/temporal search via /search endpoint
    /// - Fields: Field filtering for reducing response payload size
    /// - Sort: Sort Extension for ordering search results
    /// - Filter: CQL2-JSON filtering for advanced attribute queries
    /// - CQL2-JSON: JSON format for CQL2 filter expressions (subset)
    ///
    /// CQL2 Implementation Details:
    /// Implemented operators: AND, OR, NOT, =, &lt;&gt;, &lt;, &lt;=, &gt;, &gt;=, IS NULL, LIKE, BETWEEN, IN, s_intersects, t_intersects, anyinteracts
    /// Not implemented: Arithmetic operators, full spatial operator set, array operations, functions
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultConformance = new[]
    {
        "https://api.stacspec.org/v1.0.0/core",
        "https://api.stacspec.org/v1.0.0/collections",
        "https://api.stacspec.org/v1.0.0/item-search",
        "https://api.stacspec.org/v1.0.0/item-search#fields",
        "https://api.stacspec.org/v1.0.0/item-search#sort",
        "https://api.stacspec.org/v1.0.0/item-search#filter",
        "http://www.opengis.net/spec/cql2/1.0/conf/cql2-json"
        // Note: NOT conforming to "basic-cql2" as we don't implement all required operators
        // (e.g., arithmetic operators, casei function, full spatial operator set)
    };

    public static readonly IReadOnlyList<StacLinkDto> EmptyLinks = new List<StacLinkDto>();

    /// <summary>
    /// Maximum number of collections that can be requested in a single search operation.
    /// Used by StacSearchController GET and POST operations.
    /// </summary>
    public const int MaxCollectionsCount = 100;

    /// <summary>
    /// Maximum number of IDs that can be requested in a single query.
    /// Used by StacSearchController for item ID filtering.
    /// </summary>
    public const int MaxIdsCount = 100;

    /// <summary>
    /// Default limit for search results when not specified by the client.
    /// </summary>
    public const int DefaultSearchLimit = 10;

    /// <summary>
    /// Maximum limit for search results to prevent excessive response sizes.
    /// </summary>
    public const int MaxSearchLimit = 1000;

    /// <summary>
    /// Pagination constants
    /// </summary>
    public static class Pagination
    {
        /// <summary>
        /// Default number of items to return when limit is not specified.
        /// </summary>
        public const int DefaultLimit = 10;

        /// <summary>
        /// Maximum number of items that can be returned in a single request.
        /// </summary>
        public const int MaxLimit = 1000;
    }

    /// <summary>
    /// Activity and telemetry names for consistent instrumentation.
    /// </summary>
    public static class Activities
    {
        public const string PostCollection = "STAC PostCollection";
        public const string PutCollection = "STAC PutCollection";
        public const string DeleteCollection = "STAC DeleteCollection";
        public const string PostItem = "STAC PostItem";
        public const string PutItem = "STAC PutItem";
        public const string DeleteItem = "STAC DeleteItem";
        public const string Search = "STAC Search";
        public const string GetItems = "STAC GetItems";
    }

    /// <summary>
    /// Metric operation types for consistent recording.
    /// </summary>
    public static class Operations
    {
        public const string Post = "post";
        public const string Put = "put";
        public const string Patch = "patch";
        public const string Delete = "delete";
        public const string Get = "get";
        public const string Search = "search";
    }

    /// <summary>
    /// Resource types for metrics and logging.
    /// </summary>
    public static class Resources
    {
        public const string Collection = "collection";
        public const string Item = "item";
    }

    /// <summary>
    /// Common error codes used across STAC operations.
    /// </summary>
    public static class ErrorCodes
    {
        public const string ValidationError = "validation_error";
        public const string MissingId = "missing_id";
        public const string Conflict = "conflict";
        public const string ParseError = "parse_error";
        public const string NotFound = "not_found";
    }
}
