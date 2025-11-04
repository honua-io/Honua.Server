// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Stac.Services;

/// <summary>
/// Service for parsing and validating STAC JSON documents.
/// </summary>
public sealed class StacParsingService
{
    private static readonly HashSet<string> ReservedCollectionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "stac_version",
        "id",
        "title",
        "description",
        "keywords",
        "license",
        "extent",
        "links",
        "stac_extensions",
        "version",
        "conformsTo"
    };

    private static readonly HashSet<string> LinkReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "rel",
        "href",
        "type",
        "title",
        "hreflang"
    };

    private static readonly HashSet<string> ExtentReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "spatial",
        "temporal"
    };

    private static readonly HashSet<string> AssetReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "href",
        "title",
        "description",
        "type",
        "roles"
    };

    /// <summary>
    /// Parses a STAC collection from JSON.
    /// </summary>
    public StacCollectionRecord ParseCollectionFromJson(JsonObject json)
    {
        Guard.NotNull(json);

        if (!TryGetStringValue(json, "id", out var id))
        {
            throw new InvalidOperationException("Collection 'id' is required and must be a string.");
        }

        var sanitizedId = StacTextSanitizer.Sanitize(id, allowEmpty: false)!;

        TryGetStringValue(json, "title", out var titleRaw);
        var title = titleRaw is null ? null : StacTextSanitizer.Sanitize(titleRaw);

        TryGetStringValue(json, "description", out var descriptionRaw);
        var description = descriptionRaw is null ? null : StacTextSanitizer.Sanitize(descriptionRaw);

        TryGetStringValue(json, "license", out var licenseRaw);
        var license = licenseRaw is null ? null : StacTextSanitizer.Sanitize(licenseRaw);

        TryGetStringValue(json, "version", out var versionRaw);
        var version = versionRaw is null ? null : StacTextSanitizer.Sanitize(versionRaw);

        TryGetStringValue(json, "conformsTo", out var conformsToRaw);
        var conformsTo = conformsToRaw is null ? null : StacTextSanitizer.Sanitize(conformsToRaw);

        var keywords = new List<string>();
        if (json["keywords"] is JsonArray keywordsArray)
        {
            foreach (var keyword in keywordsArray)
            {
                if (keyword is null)
                {
                    continue;
                }

                if (!TryGetStringFromNode(keyword, out var keywordStr))
                {
                    throw new InvalidOperationException("Collection 'keywords' entries must be strings.");
                }

                if (keywordStr.IsNullOrWhiteSpace())
                {
                    continue;
                }

                keywords.Add(StacTextSanitizer.Sanitize(keywordStr, allowEmpty: false)!);
            }
        }

        // Parse extent if provided
        var extent = json["extent"] is JsonObject extentObj
            ? ParseExtent(extentObj)
            : StacExtent.Empty;

        var links = ParseLinks(json["links"]);
        var extensions = ParseExtensions(json["stac_extensions"]);
        var additionalProperties = ExtractAdditionalProperties(json);

        return new StacCollectionRecord
        {
            Id = sanitizedId,
            Title = title,
            Description = description,
            License = license,
            Version = version,
            Keywords = keywords.Count == 0 ? Array.Empty<string>() : keywords.ToArray(),
            Extent = extent,
            Links = links,
            Extensions = extensions,
            Properties = additionalProperties,
            ConformsTo = conformsTo,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static IReadOnlyList<StacLink> ParseLinks(JsonNode? node)
    {
        if (node is null)
        {
            return Array.Empty<StacLink>();
        }

        if (node is not JsonArray linksArray)
        {
            throw new InvalidOperationException("Collection 'links' must be an array when provided.");
        }

        var links = new List<StacLink>();
        foreach (var element in linksArray)
        {
            if (element is null)
            {
                continue;
            }

            if (element is not JsonObject linkObj)
            {
                throw new InvalidOperationException("Each link entry must be a JSON object.");
            }

            if (!TryGetStringValue(linkObj, "rel", out var relRaw) || relRaw.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Link 'rel' is required and must be a string.");
            }

            if (!TryGetStringValue(linkObj, "href", out var hrefRaw) || hrefRaw.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Link 'href' is required and must be a string.");
            }

            var rel = StacTextSanitizer.Sanitize(relRaw, allowEmpty: false)!;
            var href = StacTextSanitizer.SanitizeUrl(hrefRaw);

            TryGetStringValue(linkObj, "type", out var typeRaw);
            var type = typeRaw is null ? null : StacTextSanitizer.Sanitize(typeRaw);

            TryGetStringValue(linkObj, "title", out var titleRaw);
            var title = titleRaw is null ? null : StacTextSanitizer.Sanitize(titleRaw);

            TryGetStringValue(linkObj, "hreflang", out var hreflangRaw);
            var hreflang = hreflangRaw is null ? null : StacTextSanitizer.Sanitize(hreflangRaw);

            var properties = ExtractAdditionalProperties(linkObj, LinkReservedKeys);

            links.Add(new StacLink
            {
                Rel = rel,
                Href = href,
                Type = type,
                Title = title,
                Hreflang = hreflang,
                Properties = properties
            });
        }

        return links.Count == 0 ? Array.Empty<StacLink>() : links;
    }

    private static IReadOnlyList<string> ParseExtensions(JsonNode? node)
    {
        if (node is null)
        {
            return Array.Empty<string>();
        }

        if (node is not JsonArray array)
        {
            throw new InvalidOperationException("Collection 'stac_extensions' must be an array when provided.");
        }

        var extensions = new List<string>();
        foreach (var element in array)
        {
            if (element is null)
            {
                continue;
            }

            if (!TryGetStringFromNode(element, out var extValue))
            {
                throw new InvalidOperationException("STAC extension values must be strings.");
            }

            if (extValue.IsNullOrWhiteSpace())
            {
                continue;
            }

            extensions.Add(StacTextSanitizer.SanitizeUrl(extValue));
        }

        return extensions.Count == 0
            ? Array.Empty<string>()
            : extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyDictionary<string, StacAsset> ParseAssets(JsonNode? node)
    {
        if (node is null)
        {
            return new Dictionary<string, StacAsset>();
        }

        if (node is not JsonObject assetsObj)
        {
            throw new InvalidOperationException("Item 'assets' must be an object when provided.");
        }

        var assets = new Dictionary<string, StacAsset>(StringComparer.Ordinal);
        foreach (var (key, value) in assetsObj)
        {
            if (value is not JsonObject assetObj)
            {
                throw new InvalidOperationException("Each asset entry must be a JSON object.");
            }

            if (!TryGetStringValue(assetObj, "href", out var hrefRaw) || hrefRaw.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException($"Asset '{key}' must include a non-empty 'href'.");
            }

            var href = StacTextSanitizer.SanitizeUrl(hrefRaw);

            TryGetStringValue(assetObj, "title", out var assetTitleRaw);
            var assetTitle = assetTitleRaw is null ? null : StacTextSanitizer.Sanitize(assetTitleRaw);

            TryGetStringValue(assetObj, "description", out var assetDescRaw);
            var assetDescription = assetDescRaw is null ? null : StacTextSanitizer.Sanitize(assetDescRaw);

            TryGetStringValue(assetObj, "type", out var assetTypeRaw);
            var assetType = assetTypeRaw is null ? null : StacTextSanitizer.Sanitize(assetTypeRaw);

            var roles = ParseRoles(assetObj);
            var additional = ExtractAdditionalProperties(assetObj, AssetReservedKeys);

            assets[key] = new StacAsset
            {
                Href = href,
                Title = assetTitle,
                Description = assetDescription,
                Type = assetType,
                Roles = roles,
                Properties = additional
            };
        }

        return assets;
    }

    private static IReadOnlyList<string> ParseRoles(JsonObject assetObj)
    {
        if (!assetObj.TryGetPropertyValue("roles", out var rolesNode) || rolesNode is null)
        {
            return Array.Empty<string>();
        }

        if (rolesNode is not JsonArray rolesArray)
        {
            throw new InvalidOperationException("Asset 'roles' must be an array when provided.");
        }

        var roles = new List<string>();
        foreach (var roleNode in rolesArray)
        {
            if (roleNode is null)
            {
                continue;
            }

            if (!TryGetStringFromNode(roleNode, out var roleValue))
            {
                throw new InvalidOperationException("Asset 'roles' entries must be strings.");
            }

            if (roleValue.IsNullOrWhiteSpace())
            {
                continue;
            }

            roles.Add(StacTextSanitizer.Sanitize(roleValue, allowEmpty: false)!);
        }

        return roles.Count == 0 ? Array.Empty<string>() : roles;
    }

    private static StacExtent ParseExtent(JsonObject extentObj)
    {
        var spatial = new List<double[]>();
        var temporal = new List<StacTemporalInterval>();

        if (extentObj.TryGetPropertyValue("spatial", out var spatialNode))
        {
            if (spatialNode is not JsonObject spatialObj)
            {
                throw new InvalidOperationException("Extent 'spatial' must be an object.");
            }

            if (spatialObj.TryGetPropertyValue("bbox", out var bboxNode))
            {
                if (bboxNode is not JsonArray bboxArray)
                {
                    throw new InvalidOperationException("Extent 'spatial.bbox' must be an array.");
                }

                foreach (var bboxEntry in bboxArray)
                {
                    if (bboxEntry is not JsonArray coordsArray)
                    {
                        throw new InvalidOperationException("Extent 'spatial.bbox' entries must be arrays of numbers.");
                    }

                    if (coordsArray.Count == 0)
                    {
                        continue;
                    }

                    var coords = new double[coordsArray.Count];
                    for (var i = 0; i < coordsArray.Count; i++)
                    {
                        if (!TryGetDoubleFromNode(coordsArray[i], out var value))
                        {
                            throw new InvalidOperationException("Extent 'spatial.bbox' values must be numeric.");
                        }

                        coords[i] = value;
                    }

                    spatial.Add(coords);
                }
            }
        }

        if (extentObj.TryGetPropertyValue("temporal", out var temporalNode))
        {
            if (temporalNode is not JsonObject temporalObj)
            {
                throw new InvalidOperationException("Extent 'temporal' must be an object.");
            }

            if (temporalObj.TryGetPropertyValue("interval", out var intervalNode))
            {
                if (intervalNode is not JsonArray intervalArray)
                {
                    throw new InvalidOperationException("Extent 'temporal.interval' must be an array.");
                }

                foreach (var intervalEntry in intervalArray)
                {
                    if (intervalEntry is not JsonArray pairArray)
                    {
                        throw new InvalidOperationException("Extent 'temporal.interval' entries must be arrays.");
                    }

                    if (pairArray.Count != 2)
                    {
                        throw new InvalidOperationException("Extent 'temporal.interval' entries must contain exactly two values.");
                    }

                    var start = ParseOptionalDateTime(pairArray[0]);
                    var end = ParseOptionalDateTime(pairArray[1]);

                    if (start.HasValue && end.HasValue && end < start)
                    {
                        throw new InvalidOperationException("Extent temporal intervals must have end >= start.");
                    }

                    temporal.Add(new StacTemporalInterval
                    {
                        Start = start,
                        End = end
                    });
                }
            }
        }

        var additional = ExtractAdditionalProperties(extentObj, ExtentReservedKeys);

        if (spatial.Count == 0 && temporal.Count == 0 && additional is null)
        {
            return StacExtent.Empty;
        }

        return new StacExtent
        {
            Spatial = spatial.Count == 0 ? Array.Empty<double[]>() : spatial.ToArray(),
            Temporal = temporal.Count == 0 ? Array.Empty<StacTemporalInterval>() : temporal.ToArray(),
            AdditionalProperties = additional
        };
    }

    private static DateTimeOffset? ParseOptionalDateTime(JsonNode? node)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        if (!TryGetStringFromNode(node, out var value) || value.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Temporal interval values must be ISO-8601 strings or null.");
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidOperationException($"Invalid datetime value '{value}'. Expected ISO-8601 format.");
        }

        return parsed;
    }

    private static JsonObject? ExtractAdditionalProperties(JsonObject source)
    {
        return ExtractAdditionalProperties(source, ReservedCollectionKeys);
    }

    private static JsonObject? ExtractAdditionalProperties(JsonObject? source, ISet<string> reservedKeys)
    {
        if (source is null)
        {
            return null;
        }

        JsonObject? result = null;

        foreach (var property in source)
        {
            if (reservedKeys.Contains(property.Key))
            {
                continue;
            }

            result ??= new JsonObject();
            var sanitizedValue = property.Value is null
                ? JsonValue.Create((string?)null)
                : SanitizeNode(property.Value);
            result[property.Key] = sanitizedValue;
        }

        return result;
    }

    private static JsonNode? SanitizeNode(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonObject obj)
        {
            var sanitized = new JsonObject();
            foreach (var property in obj)
            {
                sanitized[property.Key] = SanitizeNode(property.Value);
            }
            return sanitized;
        }

        if (node is JsonArray array)
        {
            var sanitizedArray = new JsonArray();
            foreach (var element in array)
            {
                sanitizedArray.Add(SanitizeNode(element));
            }
            return sanitizedArray;
        }

        var kind = node.GetValueKind();
        if (kind == JsonValueKind.String)
        {
            if (!TryGetStringFromNode(node, out var value))
            {
                return JsonValue.Create((string?)null);
            }

            var sanitized = StacTextSanitizer.Sanitize(value);
            return JsonValue.Create(sanitized);
        }

        if (kind == JsonValueKind.Null)
        {
            return JsonValue.Create((string?)null);
        }

        return node.DeepClone();
    }

    /// <summary>
    /// Parses a STAC item from JSON.
    /// </summary>
    public StacItemRecord ParseItemFromJson(JsonObject json, string collectionId)
    {
        if (!TryGetStringValue(json, "id", out var id))
        {
            throw new InvalidOperationException("Item 'id' is required and must be a string.");
        }

        var sanitizedId = StacTextSanitizer.Sanitize(id, allowEmpty: false)!;

        TryGetStringValue(json, "title", out var titleRaw);
        var title = titleRaw is null ? null : StacTextSanitizer.Sanitize(titleRaw);

        TryGetStringValue(json, "description", out var descriptionRaw);
        var description = descriptionRaw is null ? null : StacTextSanitizer.Sanitize(descriptionRaw);

        var links = ParseLinks(json["links"]);
        var extensions = ParseExtensions(json["stac_extensions"]);
        var assets = ParseAssets(json["assets"]);

        // Validate properties is a JsonObject if present
        JsonObject? properties = null;
        if (json["properties"] is not null)
        {
            if (json["properties"] is not JsonObject propsObj)
            {
                throw new InvalidOperationException("Item 'properties' must be a JSON object.");
            }
            properties = propsObj;
        }

        var geometry = json["geometry"]?.ToJsonString();

        // Parse bbox if provided
        double[]? bbox = null;
        if (json["bbox"] is JsonArray bboxArray)
        {
            bbox = new double[bboxArray.Count];
            for (int i = 0; i < bboxArray.Count; i++)
            {
                if (TryGetDoubleFromNode(bboxArray[i], out var value))
                {
                    bbox[i] = value;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid bbox value at index {i}. Expected a number.");
                }
            }
        }

        // Parse datetime
        DateTimeOffset? datetime = null;
        DateTimeOffset? startDatetime = null;
        DateTimeOffset? endDatetime = null;

        if (properties?["datetime"] is not null)
        {
            if (TryGetStringValue(properties, "datetime", out var datetimeStr) &&
                datetimeStr.HasValue() &&
                DateTimeOffset.TryParse(datetimeStr, out var dt))
            {
                datetime = dt;
            }
        }

        if (properties?["start_datetime"] is not null)
        {
            if (TryGetStringValue(properties, "start_datetime", out var startStr) &&
                startStr.HasValue() &&
                DateTimeOffset.TryParse(startStr, out var start))
            {
                startDatetime = start;
            }
        }

        if (properties?["end_datetime"] is not null)
        {
            if (TryGetStringValue(properties, "end_datetime", out var endStr) &&
                endStr.HasValue() &&
                DateTimeOffset.TryParse(endStr, out var end))
            {
                endDatetime = end;
            }
        }

        if (!datetime.HasValue && startDatetime.HasValue && endDatetime.HasValue && startDatetime == endDatetime)
        {
            datetime = startDatetime;
        }

        return new StacItemRecord
        {
            Id = sanitizedId,
            CollectionId = collectionId,
            Title = title,
            Description = description,
            Properties = properties,
            Assets = assets,
            Links = links,
            Extensions = extensions,
            Geometry = geometry,
            Bbox = bbox,
            Datetime = datetime,
            StartDatetime = startDatetime,
            EndDatetime = endDatetime,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Merges a patch into an existing collection record.
    /// </summary>
    public StacCollectionRecord MergeCollectionPatch(StacCollectionRecord existing, JsonObject patch)
    {
        Guard.NotNull(existing);
        Guard.NotNull(patch);

        var title = existing.Title;
        if (TryGetStringValue(patch, "title", out var titleVal))
        {
            title = titleVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(titleVal);
        }

        var description = existing.Description;
        if (TryGetStringValue(patch, "description", out var descVal))
        {
            description = descVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(descVal);
        }

        var license = existing.License;
        if (TryGetStringValue(patch, "license", out var licenseVal))
        {
            license = licenseVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(licenseVal);
        }

        var version = existing.Version;
        if (TryGetStringValue(patch, "version", out var versionVal))
        {
            version = versionVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(versionVal);
        }

        var keywords = existing.Keywords;
        if (patch["keywords"] is JsonArray keywordsArray)
        {
            var newKeywords = new List<string>();
            foreach (var keyword in keywordsArray)
            {
                if (keyword is null)
                {
                    continue;
                }

                if (!TryGetStringFromNode(keyword, out var keywordStr))
                {
                    throw new InvalidOperationException("Collection 'keywords' entries must be strings.");
                }

                if (keywordStr.IsNullOrWhiteSpace())
                {
                    continue;
                }

                newKeywords.Add(StacTextSanitizer.Sanitize(keywordStr, allowEmpty: false)!);
            }
            keywords = newKeywords.Count == 0 ? Array.Empty<string>() : newKeywords.ToArray();
        }

        var extent = existing.Extent;
        if (patch.TryGetPropertyValue("extent", out var extentNode))
        {
            extent = extentNode switch
            {
                null => StacExtent.Empty,
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => StacExtent.Empty,
                JsonObject extentObj => ParseExtent(extentObj),
                _ => throw new InvalidOperationException("Collection 'extent' must be an object when provided.")
            };
        }

        var links = existing.Links;
        if (patch.TryGetPropertyValue("links", out var linksNode))
        {
            links = linksNode switch
            {
                null => Array.Empty<StacLink>(),
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => Array.Empty<StacLink>(),
                JsonArray array => ParseLinks(array),
                _ => throw new InvalidOperationException("Collection 'links' must be an array when provided.")
            };
        }

        var extensions = existing.Extensions;
        if (patch.TryGetPropertyValue("stac_extensions", out var extensionsNode))
        {
            extensions = extensionsNode switch
            {
                null => Array.Empty<string>(),
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => Array.Empty<string>(),
                JsonArray array => ParseExtensions(array),
                _ => throw new InvalidOperationException("Collection 'stac_extensions' must be an array when provided.")
            };
        }

        var additionalPatch = ExtractAdditionalProperties(patch);
        var properties = existing.Properties;
        if (additionalPatch is not null)
        {
            properties = CloneObject(existing.Properties);
            foreach (var property in additionalPatch)
            {
                properties[property.Key] = property.Value?.DeepClone();
            }
        }

        return existing with
        {
            Title = title,
            Description = description,
            License = license,
            Version = version,
            Keywords = keywords,
            Extent = extent,
            Links = links,
            Extensions = extensions,
            Properties = properties,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Merges a patch into an existing item record.
    /// </summary>
    public StacItemRecord MergeItemPatch(StacItemRecord existing, JsonObject patch)
    {
        Guard.NotNull(existing);
        Guard.NotNull(patch);

        var title = existing.Title;
        if (TryGetStringValue(patch, "title", out var titleVal))
        {
            title = titleVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(titleVal);
        }

        var description = existing.Description;
        if (TryGetStringValue(patch, "description", out var descriptionVal))
        {
            description = descriptionVal.IsNullOrWhiteSpace() ? null : StacTextSanitizer.Sanitize(descriptionVal);
        }

        var properties = existing.Properties;
        if (patch["properties"] is not null)
        {
            if (patch["properties"] is not JsonObject patchProperties)
            {
                throw new InvalidOperationException("Patch 'properties' must be a JSON object.");
            }
            properties = patchProperties;
        }

        var geometry = existing.Geometry;
        if (patch.TryGetPropertyValue("geometry", out var geometryNode))
        {
            geometry = geometryNode?.ToJsonString();
        }

        double[]? bbox = existing.Bbox;
        if (patch["bbox"] is JsonArray bboxArray)
        {
            bbox = new double[bboxArray.Count];
            for (int i = 0; i < bboxArray.Count; i++)
            {
                if (TryGetDoubleFromNode(bboxArray[i], out var value))
                {
                    bbox[i] = value;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid bbox value at index {i}. Expected a number.");
                }
            }
        }

        var links = existing.Links;
        if (patch.TryGetPropertyValue("links", out var linksNode))
        {
            links = linksNode switch
            {
                null => Array.Empty<StacLink>(),
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => Array.Empty<StacLink>(),
                JsonArray array => ParseLinks(array),
                _ => throw new InvalidOperationException("Item 'links' must be an array when provided.")
            };
        }

        var extensions = existing.Extensions;
        if (patch.TryGetPropertyValue("stac_extensions", out var extensionsNode))
        {
            extensions = extensionsNode switch
            {
                null => Array.Empty<string>(),
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => Array.Empty<string>(),
                JsonArray array => ParseExtensions(array),
                _ => throw new InvalidOperationException("Item 'stac_extensions' must be an array when provided.")
            };
        }

        var assets = existing.Assets;
        if (patch.TryGetPropertyValue("assets", out var assetsNode))
        {
            assets = assetsNode switch
            {
                null => new Dictionary<string, StacAsset>(),
                JsonValue value when value.GetValueKind() == JsonValueKind.Null => new Dictionary<string, StacAsset>(),
                JsonObject obj => ParseAssets(obj),
                _ => throw new InvalidOperationException("Item 'assets' must be an object when provided.")
            };
        }

        DateTimeOffset? datetime = existing.Datetime;
        DateTimeOffset? startDatetime = existing.StartDatetime;
        DateTimeOffset? endDatetime = existing.EndDatetime;

        if (properties is not null)
        {
            if (properties.TryGetPropertyValue("datetime", out var datetimeNode))
            {
                datetime = ParseOptionalDateTime(datetimeNode);
            }

            if (properties.TryGetPropertyValue("start_datetime", out var startNode))
            {
                startDatetime = ParseOptionalDateTime(startNode);
            }

            if (properties.TryGetPropertyValue("end_datetime", out var endNode))
            {
                endDatetime = ParseOptionalDateTime(endNode);
            }

            if (!datetime.HasValue && startDatetime.HasValue && endDatetime.HasValue && startDatetime == endDatetime)
            {
                datetime = startDatetime;
            }
        }

        return existing with
        {
            Title = title,
            Description = description,
            Properties = properties,
            Geometry = geometry,
            Bbox = bbox,
            Links = links,
            Extensions = extensions,
            Assets = assets,
            Datetime = datetime,
            StartDatetime = startDatetime,
            EndDatetime = endDatetime,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
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

    /// <summary>
    /// Safely attempts to get a string value from a JsonObject property.
    /// </summary>
    private static bool TryGetStringValue(JsonObject json, string propertyName, out string? value)
    {
        value = null;
        try
        {
            var node = json[propertyName];
            if (node is null)
            {
                return false;
            }

            if (node.GetValueKind() != JsonValueKind.String)
            {
                return false;
            }

            value = node.GetValue<string>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely attempts to get a string value from a JsonNode.
    /// </summary>
    private static bool TryGetStringFromNode(JsonNode node, out string value)
    {
        value = string.Empty;
        try
        {
            if (node.GetValueKind() != JsonValueKind.String)
            {
                return false;
            }

            value = node.GetValue<string>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely attempts to get a double value from a JsonNode.
    /// </summary>
    private static bool TryGetDoubleFromNode(JsonNode? node, out double value)
    {
        value = 0;
        if (node is null)
        {
            return false;
        }

        try
        {
            var kind = node.GetValueKind();
            if (kind == JsonValueKind.Number)
            {
                value = node.GetValue<double>();
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
