// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    private static JsonObject CreateDocument(FeatureRecord record, LayerDefinition layer)
    {
        var json = new JsonObject();

        // Use LayerMetadataHelper to get geometry column for normalization
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var kvp in record.Attributes)
        {
            // Use FeatureRecordNormalizer to normalize values (especially DateTime to UTC)
            var isGeometry = string.Equals(kvp.Key, geometryField, StringComparison.OrdinalIgnoreCase);
            var normalized = FeatureRecordNormalizer.NormalizeValue(kvp.Value, isGeometry);
            json[kvp.Key] = ConvertValueToJsonNode(normalized);
        }

        return json;
    }

    private static JsonNode? ConvertValueToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        switch (value)
        {
            case JsonNode node:
                return node;
            case JsonElement element:
                return JsonNode.Parse(element.GetRawText());
            case JsonDocument document:
                return JsonNode.Parse(document.RootElement.GetRawText());
            case string s:
                return JsonValue.Create(s);
            case bool b:
                return JsonValue.Create(b);
            case int i:
                return JsonValue.Create(i);
            case long l:
                return JsonValue.Create(l);
            case double d:
                return JsonValue.Create(d);
            case float f:
                return JsonValue.Create(f);
            case decimal m:
                return JsonValue.Create(m);
            case DateTime dt:
                // DateTime normalization handled by FeatureRecordNormalizer, but format for JSON
                return JsonValue.Create(dt.ToString("o", CultureInfo.InvariantCulture));
            case DateTimeOffset dto:
                // DateTimeOffset normalization handled by FeatureRecordNormalizer, but format for JSON
                return JsonValue.Create(dto.ToString("o", CultureInfo.InvariantCulture));
            case Guid guid:
                return JsonValue.Create(guid.ToString());
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                {
                    var obj = new JsonObject();
                    foreach (var pair in readOnlyDictionary)
                    {
                        obj[pair.Key] = ConvertValueToJsonNode(pair.Value);
                    }
                    return obj;
                }
            case IDictionary<string, object?> dictionaryGeneric:
                {
                    var obj = new JsonObject();
                    foreach (var pair in dictionaryGeneric)
                    {
                        obj[pair.Key] = ConvertValueToJsonNode(pair.Value);
                    }
                    return obj;
                }
            case System.Collections.IDictionary dictionary:
                {
                    var obj = new JsonObject();
                    foreach (System.Collections.DictionaryEntry entry in dictionary)
                    {
                        if (entry.Key is string key)
                        {
                            obj[key] = ConvertValueToJsonNode(entry.Value);
                        }
                    }
                    return obj;
                }
            case System.Collections.IEnumerable enumerable when value is not string:
                {
                    var array = new JsonArray();
                    foreach (var item in enumerable)
                    {
                        array.Add(ConvertValueToJsonNode(item));
                    }
                    return array;
                }
            default:
                return JsonValue.Create(value.ToString());
        }
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertJsonElementValue(property.Value);
        }

        return dictionary;
    }

    private static object? ConvertJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementValue).ToList(),
            JsonValueKind.String => element.TryGetDateTimeOffset(out var dto)
                ? dto
                : element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static async Task<JsonDocument> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        JsonObject? body,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            var json = body.ToJsonString(SerializerOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            var payload = responseText.IsNullOrWhiteSpace() ? "{}" : responseText;
            return JsonDocument.Parse(payload);
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = responseText.IsNullOrWhiteSpace() ? response.ReasonPhrase : responseText;
            throw new InvalidOperationException($"Elasticsearch request to '{path}' failed with {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {reason}");
        }

        var content = responseText.IsNullOrWhiteSpace() ? "{}" : responseText;
        return JsonDocument.Parse(content);
    }

    private static string ResolveIndexName(LayerDefinition layer, ElasticsearchConnectionInfo connectionInfo)
    {
        // Use LayerMetadataHelper to get table name (index name in Elasticsearch)
        var table = LayerMetadataHelper.GetTableName(layer);

        // If table name is just the layer ID, check for default index from connection string
        if (string.Equals(table, layer.Id, StringComparison.Ordinal) && connectionInfo.DefaultIndex.HasValue())
        {
            return connectionInfo.DefaultIndex!;
        }

        return table;
    }

    private static string ResolveDocumentId(LayerDefinition layer, FeatureRecord record)
    {
        // Use LayerMetadataHelper to get primary key column
        var idField = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        if (!record.Attributes.TryGetValue(idField, out var idValue) || idValue is null)
        {
            throw new InvalidOperationException($"Feature record is missing the identifier field '{idField}'.");
        }

        return Convert.ToString(idValue, CultureInfo.InvariantCulture)
               ?? throw new InvalidOperationException($"Unable to convert identifier for field '{idField}' to string.");
    }

    private static bool TryReadCoordinates(JsonElement element, out double lon, out double lat)
    {
        lon = lat = 0;

        if (!element.TryGetProperty("lon", out var lonElement) ||
            !element.TryGetProperty("lat", out var latElement))
        {
            return false;
        }

        return lonElement.TryGetDouble(out lon) && latElement.TryGetDouble(out lat);
    }

    private static string Encode(string value)
        => Uri.EscapeDataString(value);

    private static JsonNode? ConvertGeometryValue(object? value, bool requirePoint)
    {
        switch (value)
        {
            case null:
                return null;
            case QueryGeometryValue geometryValue:
                return ConvertGeometryFromGeometryValue(geometryValue, requirePoint);
            case JsonNode node:
                return requirePoint ? ConvertPointNode(node) : node;
            case JsonElement element when element.ValueKind is JsonValueKind.Object or JsonValueKind.Array:
                {
                    var parsed = JsonNode.Parse(element.GetRawText());
                    return parsed is null ? null : (requirePoint ? ConvertPointNode(parsed) : parsed);
                }
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                return ConvertGeometryString(element.GetString() ?? string.Empty, requirePoint);
            case JsonElement element:
                return ConvertGeometryString(element.ToString(), requirePoint);
            case JsonDocument document:
                {
                    var parsed = JsonNode.Parse(document.RootElement.GetRawText());
                    return parsed is null ? null : (requirePoint ? ConvertPointNode(parsed) : parsed);
                }
            case string text:
                return ConvertGeometryString(text, requirePoint);
            case IDictionary<string, object?> dictionary:
                {
                    var node = JsonSerializer.SerializeToNode(dictionary, SerializerOptions);
                    return node is null ? null : (requirePoint ? ConvertPointNode(node) : node);
                }
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                {
                    var node = JsonSerializer.SerializeToNode(readOnlyDictionary, SerializerOptions);
                    return node is null ? null : (requirePoint ? ConvertPointNode(node) : node);
                }
            case System.Collections.IDictionary legacyDictionary:
                {
                    var obj = new JsonObject();
                    foreach (DictionaryEntry entry in legacyDictionary)
                    {
                        if (entry.Key is string key)
                        {
                            obj[key] = ConvertValueToJsonNode(entry.Value);
                        }
                    }

                    return requirePoint ? ConvertPointNode(obj) : obj;
                }
            default:
                return null;
        }
    }

    private static JsonNode? ConvertGeometryFromGeometryValue(QueryGeometryValue geometryValue, bool requirePoint)
    {
        if (geometryValue.WellKnownText.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(geometryValue.WellKnownText);
            if (geometry is null || geometry.IsEmpty)
            {
                return null;
            }

            if (geometryValue.Srid.HasValue && geometry.SRID == 0)
            {
                geometry.SRID = geometryValue.Srid.Value;
            }

            if (requirePoint)
            {
                if (geometry is not Point point)
                {
                    throw new InvalidOperationException("geo.distance requires a POINT geometry.");
                }

                return new JsonObject
                {
                    ["lat"] = point.Y,
                    ["lon"] = point.X
                };
            }

            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(geometry);
            return JsonNode.Parse(geoJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse WKT geometry for Elasticsearch query.", ex);
        }
    }

    private static JsonNode? ConvertGeometryString(string text, bool requirePoint)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        var trimmed = text.Trim();

        if (TryParseGeoJson(trimmed, out var jsonNode))
        {
            return requirePoint ? ConvertPointNode(jsonNode!) : jsonNode;
        }

        if (requirePoint && TryParseLonLatString(trimmed, out var pointNode))
        {
            return pointNode;
        }

        var geometryValue = new QueryGeometryValue(trimmed, null);
        return ConvertGeometryFromGeometryValue(geometryValue, requirePoint);
    }

    private static bool TryParseGeoJson(string text, out JsonNode? node)
    {
        node = null;

        if (text.IsNullOrWhiteSpace() || (text[0] != '{' && text[0] != '['))
        {
            return false;
        }

        try
        {
            node = JsonNode.Parse(text);
            return node is not null;
        }
        catch (JsonException)
        {
            node = null;
            return false;
        }
    }

    private static bool TryParseLonLatString(string text, out JsonObject? point)
    {
        point = null;
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
        {
            point = new JsonObject
            {
                ["lat"] = lat,
                ["lon"] = lon
            };
            return true;
        }

        return false;
    }

    private static JsonObject ConvertPointNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("lat", out var latNode) && obj.TryGetPropertyValue("lon", out var lonNode))
            {
                return new JsonObject
                {
                    ["lat"] = ExtractDouble(latNode),
                    ["lon"] = ExtractDouble(lonNode)
                };
            }

            if (obj.TryGetPropertyValue("type", out var typeNode) &&
                typeNode is JsonValue typeValue &&
                typeValue.TryGetValue<string>(out var typeString) &&
                string.Equals(typeString, "Point", StringComparison.OrdinalIgnoreCase) &&
                obj.TryGetPropertyValue("coordinates", out var coordsNode))
            {
                return ConvertCoordinatesNode(coordsNode);
            }
        }

        if (node is JsonArray array)
        {
            return ConvertCoordinatesArray(array);
        }

        if (node is JsonValue valueNode && TryParseLonLatString(valueNode.ToString(), out var parsed))
        {
            return parsed!;
        }

        throw new InvalidOperationException("Unable to interpret geometry value as a point for geo.distance().");
    }

    private static JsonObject ConvertCoordinatesNode(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            return ConvertCoordinatesArray(array);
        }

        throw new InvalidOperationException("GeoJSON point coordinates must be an array.");
    }

    private static JsonObject ConvertCoordinatesArray(JsonArray array)
    {
        if (array.Count < 2)
        {
            throw new InvalidOperationException("Point coordinates must contain longitude and latitude values.");
        }

        var lon = ExtractDouble(array[0]);
        var lat = ExtractDouble(array[1]);

        return new JsonObject
        {
            ["lat"] = lat,
            ["lon"] = lon
        };
    }

    private static double ExtractDouble(JsonNode? node)
    {
        if (node is null)
        {
            throw new InvalidOperationException("Missing numeric coordinate value.");
        }

        if (node is JsonValue value && value.TryGetValue<double>(out var dbl))
        {
            return dbl;
        }

        if (node is JsonValue stringValue && stringValue.TryGetValue<string>(out var text) &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Coordinate value must be numeric.");
    }
}
