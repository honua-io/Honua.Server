// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Helper methods for service resolution, attribute conversion, JSON formatting, and related record handling.
/// </summary>
public sealed partial class GeoservicesRESTFeatureServerController
{
    private static void AttachGeometry(JsonElement editElement, string geometryField, IDictionary<string, object?> attributes)
    {
        if (editElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!editElement.TryGetProperty("geometry", out var geometryElement))
        {
            return;
        }

        if (geometryElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            attributes[geometryField] = null;
            return;
        }

        if (geometryElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            attributes[geometryField] = JsonNode.Parse(geometryElement.GetRawText());
        }
    }

    private static string? ResolveFeatureIdForUpdate(JsonElement editElement, Dictionary<string, object?> attributes, string idField, out string? requestedGlobalId)
    {
        requestedGlobalId = TryExtractGlobalId(attributes, remove: true);

        var attributeId = TryExtractId(attributes, idField, remove: true);
        if (attributeId.HasValue())
        {
            return attributeId;
        }

        if (editElement.ValueKind == JsonValueKind.Object)
        {
            if (editElement.TryGetProperty("objectId", out var objectIdElement))
            {
                var candidate = ConvertJsonElementToString(objectIdElement);
                if (candidate.HasValue())
                {
                    return candidate;
                }
            }

            if (editElement.TryGetProperty("globalId", out var globalIdElement))
            {
                var candidate = GeoservicesGlobalIdHelper.NormalizeGlobalId(ConvertJsonElementToString(globalIdElement));
                if (candidate.HasValue())
                {
                    requestedGlobalId ??= candidate;
                }
            }
        }

        return null;
    }

    private static string? TryExtractId(IDictionary<string, object?> attributes, string idField, bool remove)
    {
        if (attributes.TryGetValue(idField, out var value) && value is not null)
        {
            if (remove)
            {
                attributes.Remove(idField);
            }

            return ConvertToInvariantString(value);
        }

        return null;
    }

    private static string? TryExtractGlobalId(IDictionary<string, object?> attributes, bool remove)
    {
        if (attributes.TryGetValue(GlobalIdFieldName, out var value) && value is not null)
        {
            if (remove)
            {
                attributes.Remove(GlobalIdFieldName);
            }

            return GeoservicesGlobalIdHelper.NormalizeGlobalId(ConvertToInvariantString(value));
        }

        return null;
    }

    private static string? ConvertJsonElementToString(JsonElement element)
    {
        return JsonElementConverter.ToString(element);
    }

    private static string? ConvertToInvariantString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            JsonNode node => node.ToJsonString(),
            _ => value.ToString()
        };
    }

    private static bool TryGetAttribute(FeatureRecord record, string fieldName, out object? value)
    {
        if (record.Attributes.TryGetValue(fieldName, out value))
        {
            return true;
        }

        foreach (var pair in record.Attributes)
        {
            if (pair.Key.EqualsIgnoreCase(fieldName))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string EscapeKeyComponent(object? value)
    {
        if (value is null)
        {
            return "~null";
        }

        string text = value switch
        {
            DateTimeOffset dto => dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        return text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case decimal dec:
                result = (double)dec;
                return true;
            case long l:
                result = l;
                return true;
            case int i:
                result = i;
                return true;
            case short s:
                result = s;
                return true;
            case byte b:
                result = b;
                return true;
            case ulong ul:
                result = ul;
                return true;
            case uint ui:
                result = ui;
                return true;
            case DateTimeOffset dto:
                result = dto.ToUnixTimeMilliseconds();
                return true;
            case DateTime dt:
                result = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
                return true;
            case string str when str.TryParseDouble(out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is IComparable comparable)
        {
            try
            {
                var converted = ConvertToComparableType(right, left.GetType());
                if (converted is not null)
                {
                    return comparable.CompareTo(converted);
                }
            }
            catch
            {
                // Fall back to string comparison
            }
        }

        var leftText = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        var rightText = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Compare(leftText, rightText, StringComparison.Ordinal);
    }

    private static object? ConvertToComparableType(object value, Type targetType)
    {
        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        if (value is IConvertible)
        {
            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
            }
        }

        return null;
    }

    private static HttpRequest CreateRelatedQueryRequest(HttpRequest source, params string[] excludedKeys)
    {
        var exclusion = new HashSet<string>(excludedKeys, StringComparer.OrdinalIgnoreCase);
        var builder = new QueryBuilder();

        foreach (var pair in source.Query)
        {
            if (exclusion.Contains(pair.Key))
            {
                continue;
            }

            foreach (var value in pair.Value)
            {
                if (!value.IsNullOrEmpty())
                {
                    builder.Add(pair.Key, value);
                }
            }
        }

        var context = new DefaultHttpContext();
        context.Request.Method = source.Method;
        context.Request.QueryString = builder.ToQueryString();
        if (source.Headers.TryGetValue("Accept-Crs", out var acceptCrs))
        {
            context.Request.Headers["Accept-Crs"] = acceptCrs;
        }

        return context.Request;
    }

    private static List<ObjectIdValue> ParseObjectIdValues(string raw, LayerDefinition relatedLayer, string relatedKeyField)
    {
        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<ObjectIdValue>(tokens.Length);

        var relatedField = relatedLayer.Fields.FirstOrDefault(field =>
            field.Name.EqualsIgnoreCase(relatedKeyField));

        foreach (var token in tokens)
        {
            if (token.IsNullOrWhiteSpace())
            {
                continue;
            }

            var typedValue = ConvertObjectIdToken(token, relatedField);
            var key = Convert.ToString(ConvertAttributeValue(typedValue), CultureInfo.InvariantCulture) ?? string.Empty;
            results.Add(new ObjectIdValue(typedValue, key));
        }

        return results;
    }

    private static object ConvertObjectIdToken(string token, FieldDefinition? field)
    {
        var trimmed = token.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Replace("''", "'", StringComparison.Ordinal);
        }

        string typeHint = field?.DataType ?? field?.StorageType ?? string.Empty;
        typeHint = typeHint.Trim().ToLowerInvariant();

        return typeHint switch
        {
            "int" or "integer" or "int32" or "long" or "int64" or "bigint" => long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue) ? longValue : trimmed,
            "short" or "smallint" or "int16" => short.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue) ? shortValue : trimmed,
            "single" or "float" => trimmed.TryParseFloat(out var floatValue) ? floatValue : trimmed,
            "double" or "real" or "decimal" or "numeric" => trimmed.TryParseDouble(out var doubleValue) ? doubleValue : trimmed,
            "date" or "datetime" or "datetimeoffset" => DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto) ? dto : trimmed,
            "guid" or "uuid" or "uniqueidentifier" => Guid.TryParse(trimmed, out var guidValue) ? guidValue : trimmed,
            _ => TryParseFallback(trimmed)
        };

        static object TryParseFallback(string value)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            if (value.TryParseDouble(out var doubleValue))
            {
                return doubleValue;
            }

            return value;
        }
    }

    private static QueryExpression? BuildObjectIdFilterExpression(string fieldName, IReadOnlyList<ObjectIdValue> values)
    {
        QueryExpression? expression = null;

        foreach (var entry in values)
        {
            var comparison = new QueryBinaryExpression(
                new QueryFieldReference(fieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(entry.Value));

            expression = expression is null
                ? comparison
                : new QueryBinaryExpression(expression, QueryBinaryOperator.Or, comparison);
        }

        return expression;
    }

    private static QueryFilter? CombineFilters(QueryFilter? existing, QueryExpression? additional)
    {
        if (additional is null)
        {
            return existing;
        }

        if (existing?.Expression is null)
        {
            return new QueryFilter(additional);
        }

        return new QueryFilter(new QueryBinaryExpression(existing.Expression, QueryBinaryOperator.And, additional));
    }

    private static IReadOnlyList<GeoservicesRESTRelatedRecordGroup> BuildRelatedRecordGroups(
        LayerRelationshipDefinition relationship,
        IReadOnlyList<ObjectIdValue> objectIds,
        IReadOnlyList<(FeatureRecord Record, GeoservicesRESTFeature Feature)> relatedRecords,
        bool includeFeatures)
    {
        var grouped = new Dictionary<string, List<GeoservicesRESTFeature>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (record, feature) in relatedRecords)
        {
            if (!record.Attributes.TryGetValue(relationship.RelatedKeyField, out var rawValue))
            {
                continue;
            }

            var key = Convert.ToString(ConvertAttributeValue(rawValue), CultureInfo.InvariantCulture) ?? string.Empty;
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<GeoservicesRESTFeature>();
                grouped[key] = list;
            }

            list.Add(feature);
        }

        var groups = new List<GeoservicesRESTRelatedRecordGroup>(objectIds.Count);
        foreach (var requested in objectIds)
        {
            if (!grouped.TryGetValue(requested.Key, out var matches))
            {
                matches = new List<GeoservicesRESTFeature>();
            }

            groups.Add(new GeoservicesRESTRelatedRecordGroup
            {
                ObjectId = requested.Value,
                RelatedRecords = includeFeatures
                    ? new System.Collections.ObjectModel.ReadOnlyCollection<GeoservicesRESTFeature>(matches)
                    : Array.Empty<GeoservicesRESTFeature>(),
                Count = matches.Count
            });
        }

        return new System.Collections.ObjectModel.ReadOnlyCollection<GeoservicesRESTRelatedRecordGroup>(groups);
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> FilterFieldsForSelection(
        IReadOnlyList<GeoservicesRESTFieldInfo> fields,
        IReadOnlyDictionary<string, string> selectedFields,
        string idField)
    {
        if (selectedFields.Count == 0)
        {
            return fields;
        }

        var filtered = fields
            .Where(field => selectedFields.ContainsKey(field.Name) || field.Name.EqualsIgnoreCase(idField))
            .ToList();

        if (filtered.Count == fields.Count)
        {
            return fields;
        }

        return new System.Collections.ObjectModel.ReadOnlyCollection<GeoservicesRESTFieldInfo>(filtered);
    }

    private sealed record ObjectIdValue(object Value, string Key);

    private async Task<bool> IsRelationalServiceAsync(ServiceDefinition service, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
            ds.Id.EqualsIgnoreCase(service.DataSourceId));

        return dataSource is not null && IsRelationalProvider(dataSource.Provider);
    }

    private static bool IsRelationalProvider(string? provider)
    {
        if (provider.IsNullOrWhiteSpace())
        {
            return false;
        }

        return provider.EqualsIgnoreCase("sqlite")
            || provider.EqualsIgnoreCase("postgres")
            || provider.EqualsIgnoreCase("postgresql")
            || provider.EqualsIgnoreCase("sqlserver")
            || provider.EqualsIgnoreCase("mssql")
            || provider.EqualsIgnoreCase("mysql");
    }

    private static object? ConvertAttributeValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => JsonElementConverter.ToObjectWithJsonNode(element),
            JsonValue jsonValue => ConvertAttributeValue(jsonValue.GetValue<object?>()),
            JsonObject or JsonArray => value,
            DateTimeOffset dto => dto.ToUnixTimeMilliseconds(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
            bool => value,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private static bool IsKmlFormat(GeoservicesResponseFormat format)
    {
        return format is GeoservicesResponseFormat.Kml or GeoservicesResponseFormat.Kmz;
    }

    private static string CreateCollectionIdentifier(string serviceId, string layerId)
    {
        return string.Concat(serviceId, "::", layerId);
    }

    private IActionResult WriteJson(object payload, GeoservicesRESTQueryContext context)
    {
        if (!context.PrettyPrint)
        {
            return new JsonResult(payload);
        }

        var json = JsonSerializer.Serialize(payload, JsonSerializerOptionsRegistry.WebIndented);
        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    private IActionResult CreateJsonResult(object payload, bool prettyPrint, string contentType)
    {
        if (!prettyPrint)
        {
            return new JsonResult(payload) { ContentType = contentType };
        }

        var json = JsonSerializer.Serialize(payload, JsonSerializerOptionsRegistry.WebIndented);
        return new ContentResult
        {
            Content = json,
            ContentType = contentType,
            StatusCode = 200
        };
    }

    private async Task<StyleDefinition?> ResolveDefaultStyleAsync(LayerDefinition layer, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (layer.DefaultStyleId.HasValue() &&
            snapshot.TryGetStyle(layer.DefaultStyleId, out var defaultStyle))
        {
            return defaultStyle;
        }

        foreach (var candidate in layer.StyleIds)
        {
            if (candidate.HasValue() && snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    private CatalogServiceView? ResolveService(string? folderId, string serviceId)
    {
        if (serviceId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var service = this.catalog.GetService(serviceId);
        if (service is null)
        {
            return null;
        }

        // Skip folder validation if folderId is null, empty, whitespace, or "root"
        var validateFolder = !string.IsNullOrWhiteSpace(folderId) &&
                             !string.Equals(folderId, "root", StringComparison.OrdinalIgnoreCase);

        if (validateFolder && !service.Service.FolderId.EqualsIgnoreCase(folderId))
        {
            return null;
        }

        return SupportsFeatureServer(service.Service) ? service : null;
    }

    private static CatalogLayerView? ResolveLayer(CatalogServiceView serviceView, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= serviceView.Layers.Count)
        {
            return null;
        }

        return serviceView.Layers[layerIndex];
    }

    private static bool SupportsFeatureServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature");
    }
}
