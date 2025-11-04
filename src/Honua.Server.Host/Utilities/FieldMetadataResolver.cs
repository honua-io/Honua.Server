// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Extensions;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Consolidated field/property enumeration, filtering, and type resolution utility.
/// Eliminates duplicate field resolution logic across GeoServices, OGC API, OData, and WFS protocols.
/// </summary>
public static class FieldMetadataResolver
{
    #region Field Enumeration

    /// <summary>
    /// Resolves all fields from a layer definition with optional filtering.
    /// </summary>
    /// <param name="layer">The layer definition containing field metadata.</param>
    /// <param name="includeGeometry">Whether to include the geometry field in results.</param>
    /// <param name="includeIdField">Whether to ensure the ID field is included in results.</param>
    /// <returns>Read-only list of field definitions.</returns>
    public static IReadOnlyList<FieldDefinition> ResolveFields(
        LayerDefinition layer,
        bool includeGeometry = false,
        bool includeIdField = true)
    {
        Guard.NotNull(layer);

        if (layer.Fields is null || layer.Fields.Count == 0)
        {
            return Array.Empty<FieldDefinition>();
        }

        var fields = new List<FieldDefinition>();

        foreach (var field in layer.Fields)
        {
            if (!includeGeometry && field.Name.Equals(layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fields.Add(field);
        }

        // Ensure ID field is present if requested
        if (includeIdField && !string.IsNullOrWhiteSpace(layer.IdField))
        {
            var idFieldExists = fields.Any(f => f.Name.Equals(layer.IdField, StringComparison.OrdinalIgnoreCase));
            if (!idFieldExists)
            {
                // Create synthetic ID field definition
                fields.Add(new FieldDefinition
                {
                    Name = layer.IdField,
                    Alias = layer.IdField,
                    DataType = "int",
                    Nullable = false,
                    Editable = false
                });
            }
        }

        return fields;
    }

    #endregion

    #region Field Filtering

    /// <summary>
    /// Filters fields based on a list of requested property names.
    /// </summary>
    /// <param name="fields">The full list of available fields.</param>
    /// <param name="requestedProperties">Optional list of requested property names (case-insensitive). If null or empty, returns all fields.</param>
    /// <returns>Filtered list of fields matching the requested properties.</returns>
    public static IReadOnlyList<FieldDefinition> FilterFields(
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<string>? requestedProperties)
    {
        Guard.NotNull(fields);

        if (requestedProperties is null || requestedProperties.Count == 0)
        {
            return fields;
        }

        var propertySet = new HashSet<string>(requestedProperties, StringComparer.OrdinalIgnoreCase);
        var filtered = new List<FieldDefinition>();

        foreach (var field in fields)
        {
            if (propertySet.Contains(field.Name))
            {
                filtered.Add(field);
            }
        }

        return filtered;
    }

    #endregion

    #region Field Name Resolution

    /// <summary>
    /// Extracts field names from a layer definition.
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <param name="includeGeometry">Whether to include the geometry field name.</param>
    /// <returns>List of field names.</returns>
    public static IReadOnlyList<string> GetFieldNames(
        LayerDefinition layer,
        bool includeGeometry = false)
    {
        Guard.NotNull(layer);

        var fields = ResolveFields(layer, includeGeometry, includeIdField: true);
        return fields.Select(f => f.Name).ToArray();
    }

    #endregion

    #region Field Lookup

    /// <summary>
    /// Finds a field by name (case-insensitive).
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <param name="fieldName">The field name to search for.</param>
    /// <returns>The matching field definition, or null if not found.</returns>
    public static FieldDefinition? FindField(LayerDefinition layer, string fieldName)
    {
        Guard.NotNull(layer);

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        return layer.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if a field name matches the layer's geometry field.
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <param name="fieldName">The field name to check.</param>
    /// <returns>True if the field is the geometry field, otherwise false.</returns>
    public static bool IsGeometryField(LayerDefinition layer, string fieldName)
    {
        Guard.NotNull(layer);

        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(layer.GeometryField))
        {
            return false;
        }

        return fieldName.Equals(layer.GeometryField, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a field name matches the layer's ID field.
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <param name="fieldName">The field name to check.</param>
    /// <returns>True if the field is the ID field, otherwise false.</returns>
    public static bool IsIdField(LayerDefinition layer, string fieldName)
    {
        Guard.NotNull(layer);

        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(layer.IdField))
        {
            return false;
        }

        return fieldName.Equals(layer.IdField, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Type Mapping - GeoServices

    /// <summary>
    /// Maps a field definition to Esri GeoServices REST field type.
    /// </summary>
    /// <param name="field">The field definition.</param>
    /// <param name="idField">Optional ID field name for special ID field type mapping.</param>
    /// <returns>GeoServices field type string.</returns>
    public static string MapToGeoServicesType(FieldDefinition field, string? idField = null)
    {
        Guard.NotNull(field);

        // Check for ID field
        if (!string.IsNullOrWhiteSpace(idField) && field.Name.Equals(idField, StringComparison.OrdinalIgnoreCase))
        {
            return "esriFieldTypeOID";
        }

        // Check for GlobalID field
        if (field.Name.Equals("globalId", StringComparison.OrdinalIgnoreCase))
        {
            return "esriFieldTypeGlobalID";
        }

        var sourceType = field.DataType ?? field.StorageType ?? string.Empty;
        return sourceType.Trim().ToLowerInvariant() switch
        {
            "globalid" or "esriglobalid" => "esriFieldTypeGlobalID",
            "int" or "integer" or "int32" or "long" or "int64" or "bigint" => "esriFieldTypeInteger",
            "short" or "smallint" or "int16" => "esriFieldTypeSmallInteger",
            "single" or "float" => "esriFieldTypeSingle",
            "double" or "real" => "esriFieldTypeDouble",
            "decimal" or "numeric" => "esriFieldTypeDouble",
            "date" or "datetime" or "datetimeoffset" => "esriFieldTypeDate",
            "bool" or "boolean" => "esriFieldTypeSmallInteger",
            "guid" or "uuid" or "uniqueidentifier" => "esriFieldTypeGUID",
            _ => "esriFieldTypeString"
        };
    }

    #endregion

    #region Type Mapping - OGC/WFS

    /// <summary>
    /// Maps a field data type to XML Schema (XSD) type for OGC/WFS protocols.
    /// </summary>
    /// <param name="dataType">The field data type string.</param>
    /// <returns>XSD type string (e.g., "xs:int", "xs:string").</returns>
    public static string MapToOgcType(string? dataType)
    {
        return dataType?.ToLowerInvariant() switch
        {
            "int" or "integer" => "xs:int",
            "long" => "xs:long",
            "double" or "float" or "decimal" => "xs:double",
            "datetime" => "xs:dateTime",
            "bool" or "boolean" => "xs:boolean",
            _ => "xs:string"
        };
    }

    /// <summary>
    /// Maps a field definition to XML Schema (XSD) type for OGC/WFS protocols.
    /// </summary>
    /// <param name="field">The field definition.</param>
    /// <returns>XSD type string.</returns>
    public static string MapToOgcType(FieldDefinition field)
    {
        Guard.NotNull(field);
        return MapToOgcType(field.DataType);
    }

    #endregion

    #region Type Mapping - OData

    /// <summary>
    /// Maps a field data type to OData EDM type string representation.
    /// </summary>
    /// <param name="dataType">The field data type string.</param>
    /// <param name="nullable">Whether the field is nullable.</param>
    /// <returns>OData EDM type string.</returns>
    public static string MapToODataType(string? dataType, bool nullable = true)
    {
        var token = (dataType ?? string.Empty).Trim().ToLowerInvariant();

        var edmType = token switch
        {
            "byte" => "Edm.Byte",
            "sbyte" => "Edm.SByte",
            "int16" or "smallint" => "Edm.Int16",
            "int" or "int32" or "integer" => "Edm.Int32",
            "long" or "int64" or "bigint" => "Edm.Int64",
            "single" or "float" or "real" => "Edm.Single",
            "double" => "Edm.Double",
            "decimal" or "numeric" => "Edm.Decimal",
            "bool" or "boolean" => "Edm.Boolean",
            "guid" or "uniqueidentifier" => "Edm.Guid",
            "datetimeoffset" or "datetime" or "timestamp" => "Edm.DateTimeOffset",
            "date" => "Edm.Date",
            "time" or "timeofday" => "Edm.TimeOfDay",
            _ => "Edm.String"
        };

        return nullable ? $"{edmType} (Nullable)" : edmType;
    }

    /// <summary>
    /// Maps a field definition to OData EDM type string representation.
    /// </summary>
    /// <param name="field">The field definition.</param>
    /// <returns>OData EDM type string.</returns>
    public static string MapToODataType(FieldDefinition field)
    {
        Guard.NotNull(field);
        return MapToODataType(field.DataType, field.Nullable);
    }

    #endregion

    #region Geometry Type Resolution

    /// <summary>
    /// Resolves GML geometry type from layer geometry type.
    /// </summary>
    /// <param name="geometryType">The layer's geometry type string.</param>
    /// <returns>GML geometry property type.</returns>
    public static string ResolveGmlGeometryType(string? geometryType)
    {
        return geometryType?.ToLowerInvariant() switch
        {
            "point" => "gml:PointPropertyType",
            "multipoint" => "gml:MultiPointPropertyType",
            "polygon" => "gml:PolygonPropertyType",
            "multipolygon" => "gml:MultiSurfacePropertyType",
            "linestring" => "gml:CurvePropertyType",
            "multilinestring" => "gml:MultiCurvePropertyType",
            _ => "gml:GeometryPropertyType"
        };
    }

    #endregion
}
