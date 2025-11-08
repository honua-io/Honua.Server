// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Stac;

internal static class StacApiMapper
{
    public static StacRootResponse BuildRoot(CatalogDefinition? catalog, Uri baseUri)
    {
        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, "/stac"), StacMediaTypes.Json, catalog?.Title ?? "STAC Root"),
            BuildLink("data", Combine(baseUri, "/stac/collections"), StacMediaTypes.Json, "Collections"),
            BuildLink("conformance", Combine(baseUri, "/stac/conformance"), StacMediaTypes.Json, "Conformance")
        };

        return new StacRootResponse
        {
            Id = catalog?.Id,
            Title = catalog?.Title,
            Description = catalog?.Description,
            Links = links
        };
    }

    public static StacConformanceResponse BuildConformance()
    {
        return new StacConformanceResponse();
    }

    public static StacCollectionsResponse BuildCollections(IEnumerable<StacCollectionRecord> collections, Uri baseUri)
    {
        var list = collections.Select(collection => BuildCollection(collection, baseUri)).ToList();

        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, "/stac/collections"), StacMediaTypes.Json, "Collections"),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root")
        };

        return new StacCollectionsResponse
        {
            Collections = list,
            Links = links
        };
    }

    public static StacCollectionsResponse BuildCollections(IEnumerable<StacCollectionRecord> collections, Uri baseUri, int totalCount, string? nextToken, int limit)
    {
        var list = collections.Select(collection => BuildCollection(collection, baseUri)).ToList();

        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, $"/stac/collections?limit={limit}"), StacMediaTypes.Json, "Collections"),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root")
        };

        if (!nextToken.IsNullOrEmpty())
        {
            links.Add(BuildLink("next", Combine(baseUri, $"/stac/collections?limit={limit}&token={Uri.EscapeDataString(nextToken)}"), StacMediaTypes.Json, "Next page"));
        }

        var context = PaginationHelper.BuildStacContext(list.Count, totalCount, limit);

        return new StacCollectionsResponse
        {
            Collections = list,
            Links = links,
            Context = context
        };
    }

    public static StacCollectionResponse BuildCollection(StacCollectionRecord record, Uri baseUri)
    {
        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(record.Id)}"), StacMediaTypes.Json, record.Title ?? record.Id),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root"),
            BuildLink("parent", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Parent Catalog"),
            BuildLink("items", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(record.Id)}/items"), StacMediaTypes.GeoJson, $"Items for {record.Id}")
        };

        if (record.Links.Count > 0)
        {
            links.AddRange(record.Links.Select(MapLink));
        }

        var additional = CloneObject(record.Properties);
        var summaries = ExtractObject(additional, "summaries", remove: true);
        var assets = ExtractObject(additional, "assets", remove: true);
        var itemAssets = ExtractObject(additional, "item_assets", remove: true);
        var providers = ExtractArray(additional, "providers", remove: true);

        return new StacCollectionResponse
        {
            Id = record.Id,
            Title = record.Title,
            Description = record.Description,
            License = record.License,
            Version = record.Version,
            Keywords = record.Keywords,
            Extent = BuildExtent(record.Extent),
            Links = links,
            StacExtensions = record.Extensions,
            Summaries = summaries,
            Assets = assets,
            ItemAssets = itemAssets,
            Providers = providers,
            AdditionalFields = CreateExtensionDictionary(additional)
        };
    }

    public static StacItemCollectionResponse BuildItemCollection(
        IEnumerable<StacItemRecord> items,
        StacCollectionRecord collection,
        Uri baseUri,
        int? matched,
        string? nextToken,
        int? limit = null,
        FieldsSpecification? fields = null)
    {
        var features = items.Select(item => BuildItem(item, baseUri, fields)).ToList();

        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(collection.Id)}/items"), StacMediaTypes.GeoJson, $"Items for {collection.Id}"),
            BuildLink("collection", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(collection.Id)}"), StacMediaTypes.Json, collection.Title ?? collection.Id),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root")
        };

        if (nextToken.HasValue())
        {
            var query = $"token={Uri.EscapeDataString(nextToken)}";
            if (limit.HasValue && limit.Value > 0)
            {
                query += $"&limit={limit.Value}";
            }

            var href = Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(collection.Id)}/items?{query}");
            links.Add(BuildLink("next", href, StacMediaTypes.GeoJson, "Next page"));
        }

        var context = PaginationHelper.BuildStacContext(features.Count, matched ?? -1, limit ?? 0);

        return new StacItemCollectionResponse
        {
            Features = features,
            Links = links,
            Context = context
        };
    }

    public static StacItemCollectionResponse BuildSearchCollection(
        IEnumerable<StacItemRecord> items,
        Uri baseUri,
        int matched,
        string? nextToken,
        int? limit = null,
        FieldsSpecification? fields = null)
    {
        var features = items.Select(item => BuildItem(item, baseUri, fields)).ToList();

        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, "/stac/search"), StacMediaTypes.GeoJson, "Search"),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root")
        };

        if (nextToken.HasValue())
        {
            var query = $"token={Uri.EscapeDataString(nextToken)}";
            if (limit.HasValue && limit.Value > 0)
            {
                query += $"&limit={limit.Value}";
            }

            var href = Combine(baseUri, $"/stac/search?{query}");
            links.Add(BuildLink("next", href, StacMediaTypes.GeoJson, "Next page"));
        }

        var context = PaginationHelper.BuildStacContext(features.Count, matched, limit ?? 0);

        return new StacItemCollectionResponse
        {
            Features = features,
            Links = links,
            Context = context
        };
    }

    public static StacItemResponse BuildItem(StacItemRecord record, Uri baseUri, FieldsSpecification? fields = null)
    {
        var links = new List<StacLinkDto>
        {
            BuildLink("self", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(record.CollectionId)}/items/{Uri.EscapeDataString(record.Id)}"), StacMediaTypes.GeoJson, record.Title ?? record.Id),
            BuildLink("collection", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(record.CollectionId)}"), StacMediaTypes.Json, record.CollectionId),
            BuildLink("parent", Combine(baseUri, $"/stac/collections/{Uri.EscapeDataString(record.CollectionId)}"), StacMediaTypes.Json, record.CollectionId),
            BuildLink("root", Combine(baseUri, "/stac"), StacMediaTypes.Json, "Root"),
        };

        if (record.Links.Count > 0)
        {
            links.AddRange(record.Links.Select(MapLink));
        }

        var properties = MergeProperties(record);
        var assetsNode = BuildAssetsNode(record.Assets);

        var response = new StacItemResponse
        {
            Id = record.Id,
            CollectionId = record.CollectionId,
            Bbox = record.Bbox,
            Geometry = ParseGeometry(record.Geometry),
            Properties = properties,
            Links = links,
            Assets = assetsNode,
            StacExtensions = record.Extensions
        };

        // Apply field filtering if specified
        if (fields is not null && !fields.IsEmpty)
        {
            return ApplyFieldFiltering(response, fields);
        }

        return response;
    }

    private static JsonNode? ParseGeometry(string? geometry)
    {
        if (geometry.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(geometry);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject MergeProperties(StacItemRecord record)
    {
        var properties = CloneObject(record.Properties);

        // Per STAC spec, items must have either:
        // 1. A datetime field with a valid datetime value, OR
        // 2. Both start_datetime and end_datetime fields with datetime explicitly set to null

        // Always remove any existing datetime fields from properties to avoid conflicts
        // These will be set from the record fields below
        properties.Remove("datetime");
        properties.Remove("start_datetime");
        properties.Remove("end_datetime");

        if (record.Datetime.HasValue)
        {
            // Case 1: Single datetime value
            properties["datetime"] = record.Datetime.Value.ToString("O");
        }
        else if (record.StartDatetime.HasValue && record.EndDatetime.HasValue)
        {
            // Case 2: Date range with start and end - datetime must be explicitly null per STAC spec
            properties["datetime"] = null;
            properties["start_datetime"] = record.StartDatetime.Value.ToString("O");
            properties["end_datetime"] = record.EndDatetime.Value.ToString("O");
        }
        else if (record.StartDatetime.HasValue || record.EndDatetime.HasValue)
        {
            // Case 3: Only one of start/end is present - use it as datetime
            // This handles edge cases where data might have incomplete temporal info
            var fallbackDatetime = record.StartDatetime ?? record.EndDatetime;
            properties["datetime"] = fallbackDatetime!.Value.ToString("O");
        }
        else
        {
            // Case 4: No temporal information available in record fields
            // Check if datetime was already in the properties JSON blob
            // If not, use Unix epoch as a fallback to indicate unknown acquisition time
            properties["datetime"] = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).ToString("O");
        }

        return properties;
    }

    private static JsonObject BuildAssetsNode(IReadOnlyDictionary<string, StacAsset> assets)
    {
        var node = new JsonObject();
        foreach (var (key, asset) in assets)
        {
            var assetNode = new JsonObject
            {
                ["href"] = asset.Href
            };

            if (asset.Title.HasValue())
            {
                assetNode["title"] = asset.Title;
            }

            if (asset.Description.HasValue())
            {
                assetNode["description"] = asset.Description;
            }

            if (asset.Type.HasValue())
            {
                assetNode["type"] = asset.Type;
            }

            if (asset.Roles.Count > 0)
            {
                var rolesArray = new JsonArray();
                foreach (var role in asset.Roles)
                {
                    rolesArray.Add(JsonValue.Create(role));
                }

                assetNode["roles"] = rolesArray;
            }

            if (asset.Properties is not null)
            {
                assetNode["extra_fields"] = asset.Properties.DeepClone();
            }

            node[key] = assetNode;
        }

        return node;
    }

    private static JsonObject BuildExtent(StacExtent extent)
    {
        var bboxArray = new JsonArray();
        foreach (var bbox in extent.Spatial)
        {
            var coords = new JsonArray();
            foreach (var value in bbox)
            {
                coords.Add(JsonValue.Create(value));
            }

            bboxArray.Add(coords);
        }

        var spatial = new JsonObject
        {
            ["bbox"] = bboxArray
        };

        var temporalArray = new JsonArray();
        foreach (var interval in extent.Temporal)
        {
            temporalArray.Add(new JsonArray
            {
                interval.Start?.ToString("O"),
                interval.End?.ToString("O")
            });
        }

        var temporal = new JsonObject
        {
            ["interval"] = temporalArray
        };

        var extentNode = new JsonObject
        {
            ["spatial"] = spatial,
            ["temporal"] = temporal
        };

        if (extent.AdditionalProperties is not null)
        {
            foreach (var property in extent.AdditionalProperties)
            {
                extentNode[property.Key] = property.Value?.DeepClone();
            }
        }

        return extentNode;
    }

    private static JsonObject CloneObject(JsonObject? source)
    {
        var result = new JsonObject();
        if (source is null)
        {
            return result;
        }

        foreach (var property in source)
        {
            result[property.Key] = property.Value?.DeepClone();
        }

        return result;
    }

    private static JsonObject? ExtractObject(JsonObject? source, string propertyName, bool remove = false)
    {
        if (source is null)
        {
            return null;
        }

        if (source.TryGetPropertyValue(propertyName, out var node) && node is JsonObject obj)
        {
            if (remove)
            {
                source.Remove(propertyName);
            }

            return CloneObject(obj);
        }

        return null;
    }

    private static JsonArray? ExtractArray(JsonObject? source, string propertyName, bool remove = false)
    {
        if (source is null)
        {
            return null;
        }

        if (source.TryGetPropertyValue(propertyName, out var node) && node is JsonArray array)
        {
            if (remove)
            {
                source.Remove(propertyName);
            }

            var clone = new JsonArray();
            foreach (var element in array)
            {
                clone.Add(element?.DeepClone());
            }

            return clone;
        }

        return null;
    }

    private static IDictionary<string, JsonElement> CreateExtensionDictionary(JsonObject additional)
    {
        if (additional.Count == 0)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var dictionary = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in additional)
        {
            var element = JsonSerializer.SerializeToElement<JsonNode?>(property.Value);
            dictionary[property.Key] = element;
        }

        return dictionary;
    }

    private static StacLinkDto MapLink(StacLink link)
    {
        return new StacLinkDto
        {
            Rel = link.Rel,
            Href = link.Href,
            Type = link.Type,
            Title = link.Title,
            Hreflang = link.Hreflang
        };
    }

    private static StacLinkDto BuildLink(string rel, string href, string? type, string? title)
    {
        return new StacLinkDto
        {
            Rel = rel,
            Href = href,
            Type = type,
            Title = title
        };
    }

    private static string Combine(Uri baseUri, string path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return baseUri.ToString();
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var builder = new UriBuilder(baseUri);

        var pathPart = path;
        string? fragment = null;
        string? query = null;

        var fragmentIndex = pathPart.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            fragment = pathPart[(fragmentIndex + 1)..];
            pathPart = pathPart[..fragmentIndex];
        }

        var queryIndex = pathPart.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = pathPart[(queryIndex + 1)..];
            pathPart = pathPart[..queryIndex];
        }

        builder.Path = CombinePaths(builder.Path, pathPart);
        builder.Query = query ?? string.Empty;
        builder.Fragment = fragment ?? string.Empty;

        return builder.Uri.ToString();
    }

    private static string CombinePaths(string basePath, string relativePath)
    {
        var normalizedBase = basePath.IsNullOrEmpty() ? "/" : basePath;
        if (!normalizedBase.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedBase += "/";
        }

        var trimmedRelative = relativePath.IsNullOrWhiteSpace()
            ? string.Empty
            : relativePath.TrimStart('/');

        if (trimmedRelative.Length == 0)
        {
            var trimmedBase = normalizedBase.TrimEnd('/');
            return trimmedBase.IsNullOrEmpty() ? "/" : trimmedBase;
        }

        var combined = $"{normalizedBase}{trimmedRelative}".Replace("//", "/");
        return combined.StartsWith("/", StringComparison.Ordinal) ? combined : $"/{combined}";
    }

    /// <summary>
    /// Applies field filtering to a STAC Item response.
    /// Converts the response to JSON, applies filtering, and converts back.
    /// </summary>
    private static StacItemResponse ApplyFieldFiltering(StacItemResponse response, FieldsSpecification fields)
    {
        // Serialize response to JsonObject for filtering
        var jsonElement = JsonSerializer.SerializeToElement(response);
        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            return response;
        }

        var jsonObject = JsonNode.Parse(jsonElement.GetRawText())?.AsObject();
        if (jsonObject is null)
        {
            return response;
        }

        // Apply field filtering
        var filtered = FieldsFilter.ApplyFieldsFilter(jsonObject, fields);

        // Deserialize back to StacItemResponse
        var filteredResponse = JsonSerializer.Deserialize<StacItemResponse>(filtered);
        return filteredResponse ?? response;
    }
}

internal static class StacMediaTypes
{
    public const string Json = "application/json";
    public const string GeoJson = "application/geo+json";
}
