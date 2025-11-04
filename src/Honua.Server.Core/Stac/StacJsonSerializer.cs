// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Stac;

internal static class StacJsonSerializer
{
    // Apply JSON security limits to prevent DoS attacks via deeply nested STAC metadata
    // MaxDepth of 64 is sufficient for legitimate STAC structures while preventing stack overflow
    // Use centralized JsonHelper for consistent security-hardened options
    private static readonly JsonSerializerOptions SerializerOptions = Utilities.JsonHelper.SecureOptions;

    public static string SerializeKeywords(IReadOnlyList<string> keywords)
    {
        return JsonSerializer.Serialize(keywords ?? Array.Empty<string>(), SerializerOptions);
    }

    public static IReadOnlyList<string> DeserializeKeywords(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var result = JsonSerializer.Deserialize<List<string>>(json, SerializerOptions);
        return result is null || result.Count == 0
            ? Array.Empty<string>()
            : result.AsReadOnly();
    }

    public static string SerializeExtent(StacExtent extent)
    {
        return JsonSerializer.Serialize(extent ?? StacExtent.Empty, SerializerOptions);
    }

    public static StacExtent DeserializeExtent(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return StacExtent.Empty;
        }

        return JsonSerializer.Deserialize<StacExtent>(json, SerializerOptions) ?? StacExtent.Empty;
    }

    public static string SerializeLinks(IReadOnlyList<StacLink> links)
    {
        return JsonSerializer.Serialize(links ?? Array.Empty<StacLink>(), SerializerOptions);
    }

    public static IReadOnlyList<StacLink> DeserializeLinks(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return Array.Empty<StacLink>();
        }

        var result = JsonSerializer.Deserialize<List<StacLink>>(json, SerializerOptions);
        return result is null || result.Count == 0
            ? Array.Empty<StacLink>()
            : result.AsReadOnly();
    }

    public static string? SerializeNode(JsonObject? node)
    {
        return node is null ? null : node.ToJsonString(SerializerOptions);
    }

    public static JsonObject? DeserializeNode(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        var parsed = JsonNode.Parse(json) as JsonObject;
        return parsed;
    }

    public static string SerializeAssets(IReadOnlyDictionary<string, StacAsset> assets)
    {
        return JsonSerializer.Serialize(assets ?? new Dictionary<string, StacAsset>(), SerializerOptions);
    }

    public static IReadOnlyDictionary<string, StacAsset> DeserializeAssets(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return new Dictionary<string, StacAsset>();
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, StacAsset>>(json, SerializerOptions);
        return result ?? new Dictionary<string, StacAsset>();
    }

    public static string SerializeBbox(double[]? bbox)
    {
        return JsonSerializer.Serialize(bbox ?? Array.Empty<double>(), SerializerOptions);
    }

    public static string SerializeExtensions(IReadOnlyList<string> extensions)
    {
        return JsonSerializer.Serialize(extensions ?? Array.Empty<string>(), SerializerOptions);
    }

    public static IReadOnlyList<string> DeserializeExtensions(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var result = JsonSerializer.Deserialize<List<string>>(json, SerializerOptions);
        return result is null || result.Count == 0 ? Array.Empty<string>() : result.AsReadOnly();
    }

    public static double[]? DeserializeBbox(string? json)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        var data = JsonSerializer.Deserialize<double[]>(json, SerializerOptions);
        return data is { Length: > 0 } ? data : null;
    }

    public static JsonSerializerOptions Options => SerializerOptions;
}
