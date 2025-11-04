// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.OData.Edm;
using NetTopologySuite.IO;
using SpatialGeometry = Microsoft.Spatial.Geometry;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OData.Services;

/// <summary>
/// Service responsible for type conversions between OData and internal formats.
/// Handles conversions for primitives, geometry, dates, JSON, and other complex types.
/// </summary>
public sealed class ODataConverterService
{
    private static readonly GeoJsonWriter GeoJsonWriter = new();

    private readonly ODataGeometryService _geometryService;

    public ODataConverterService(ODataGeometryService geometryService)
    {
        _geometryService = Guard.NotNull(geometryService);
    }

    public object? ConvertIncomingValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            EdmEnumObject enumObject => enumObject.Value,
            EdmUntypedObject untyped => ConvertUntyped(untyped),
            JsonElement element => JsonElementConverter.ToObject(element),
            DateTimeOffset dto => dto.ToUniversalTime(),
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
            _ => value
        };
    }

    public object? ConvertOutgoingScalar(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            JsonNode node when node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) => text,
            JsonNode node => node.ToJsonString(),
            JsonElement element => ConvertOutgoingJsonElement(element),
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
            DateTimeOffset dto => dto.ToUniversalTime(),
            NetTopologySuite.Geometries.Geometry geometry => GeoJsonWriter.Write(geometry),
            SpatialGeometry spatial => ConvertSpatialToGeoJson(spatial) ?? spatial.ToString(),
            _ => value
        };
    }

    public object? ConvertPropertyValue(IEdmProperty property, object? value)
    {
        if (value is null)
        {
            return null;
        }

        var scalar = ConvertOutgoingScalar(value);
        if (scalar is null)
        {
            return null;
        }

        var type = property.Type;
        if (IsGeometryType(type))
        {
            return NormalizeIncomingGeometry(value);
        }

        if (type.IsInt32())
        {
            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        if (type.IsInt64())
        {
            return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }

        if (type.IsDecimal())
        {
            return Convert.ToDecimal(scalar, CultureInfo.InvariantCulture);
        }

        if (type.IsDouble() || type.IsSingle())
        {
            return Convert.ToDouble(scalar, CultureInfo.InvariantCulture);
        }

        if (type.IsBoolean())
        {
            return Convert.ToBoolean(scalar, CultureInfo.InvariantCulture);
        }

        if (type.IsDateTimeOffset())
        {
            return scalar switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => dt.Kind == DateTimeKind.Utc ? new DateTimeOffset(dt) : new DateTimeOffset(dt.ToUniversalTime()),
                string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
                _ => scalar
            };
        }

        if (type.IsGuid())
        {
            return scalar switch
            {
                Guid guid => guid,
                string s when Guid.TryParse(s, out var parsed) => parsed,
                _ => scalar
            };
        }

        if (type.IsString())
        {
            return Convert.ToString(scalar, CultureInfo.InvariantCulture);
        }

        return scalar;
    }

    public object? NormalizeIncomingGeometry(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node,
            JsonElement element => NormalizeGeometryText(element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText()),
            string text => NormalizeGeometryText(text),
            SpatialGeometry spatial => NormalizeGeometryText(ConvertSpatialToGeoJson(spatial)),
            _ => value.ToString()
        };
    }

    public IReadOnlyCollection<string>? GetChangedPropertyNames(IEdmEntityObject entity)
    {
        if (entity is IDelta delta)
        {
            return delta.GetChangedPropertyNames()?.ToList();
        }

        return null;
    }

    private static object? ConvertUntyped(EdmUntypedObject untyped)
    {
        if (untyped.TryGetPropertyValue("value", out var value))
        {
            return value;
        }

        return untyped.ToString();
    }

    private static object? ConvertOutgoingJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.String when element.TryGetDateTimeOffset(out var dto) => dto.ToUniversalTime(),
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }


    private object? NormalizeGeometryText(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (LooksLikeJson(text))
        {
            try
            {
                return JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                return text;
            }
        }

        return text;
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool IsGeometryType(IEdmTypeReference type)
    {
        if (type is not IEdmPrimitiveTypeReference primitive)
        {
            return false;
        }

        return primitive.PrimitiveKind() switch
        {
            EdmPrimitiveTypeKind.Geometry or
            EdmPrimitiveTypeKind.GeometryPoint or
            EdmPrimitiveTypeKind.GeometryLineString or
            EdmPrimitiveTypeKind.GeometryPolygon or
            EdmPrimitiveTypeKind.GeometryMultiPoint or
            EdmPrimitiveTypeKind.GeometryMultiLineString or
            EdmPrimitiveTypeKind.GeometryMultiPolygon or
            EdmPrimitiveTypeKind.GeometryCollection => true,
            _ => false
        };
    }

    private string? ConvertSpatialToGeoJson(SpatialGeometry spatial)
    {
        if (spatial is null)
        {
            return null;
        }

        var wkt = WriteSpatialAsWkt(spatial);
        var geometry = _geometryService.ComputeGeometry(wkt);
        if (geometry is null)
        {
            return wkt;
        }

        return GeoJsonWriter.Write(geometry);
    }

    private static string WriteSpatialAsWkt(SpatialGeometry spatial)
    {
        var formatter = Microsoft.Spatial.WellKnownTextSqlFormatter.Create();
        using var writer = new System.IO.StringWriter();
        formatter.Write(spatial, writer);
        return writer.ToString();
    }
}
