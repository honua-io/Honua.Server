// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Provides common helper methods for extracting metadata from layer definitions across all database providers.
/// Extracted from duplicate implementations in PostgreSQL, MySQL, SQL Server, SQLite, and Oracle query builders.
/// </summary>
/// <remarks>
/// This utility consolidates 100% identical layer metadata extraction logic that was duplicated
/// across all query builder implementations, including table name resolution, primary key extraction,
/// geometry column resolution, and feature ID type normalization.
/// </remarks>
public static class LayerMetadataHelper
{
    /// <summary>
    /// Gets the primary key column name from a layer definition.
    /// Falls back to IdField if Storage.PrimaryKey is not specified.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The primary key column name</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static string GetPrimaryKeyColumn(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        return layer.Storage?.PrimaryKey ?? layer.IdField;
    }

    /// <summary>
    /// Gets the geometry column name from a layer definition.
    /// Falls back to GeometryField if Storage.GeometryColumn is not specified.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The geometry column name</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static string GetGeometryColumn(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        return layer.Storage?.GeometryColumn ?? layer.GeometryField;
    }

    /// <summary>
    /// Gets the table name from a layer definition.
    /// Falls back to layer ID if Storage.Table is not specified.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The table name (may include schema/database prefix)</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static string GetTableName(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        var table = layer.Storage?.Table;
        if (table.IsNullOrWhiteSpace())
        {
            table = layer.Id;
        }

        return table;
    }

    /// <summary>
    /// Builds a quoted table expression from a layer definition, handling schema-qualified names.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <param name="quoteIdentifier">Function to quote identifiers for the specific database provider</param>
    /// <returns>A fully-qualified, quoted table expression (e.g., "schema"."table")</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer or quoteIdentifier is null</exception>
    public static string GetTableExpression(LayerDefinition layer, Func<string, string> quoteIdentifier)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        if (quoteIdentifier is null)
        {
            throw new ArgumentNullException(nameof(quoteIdentifier));
        }

        var table = GetTableName(layer);
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var quoted = parts.Select(quoteIdentifier);
        return string.Join('.', quoted);
    }

    /// <summary>
    /// Normalizes a feature ID string to the appropriate .NET type based on the layer's ID field data type.
    /// Supports conversion to int, long, double, decimal, and Guid.
    /// </summary>
    /// <param name="featureId">The feature ID as a string</param>
    /// <param name="layer">The layer definition containing field metadata</param>
    /// <returns>
    /// The feature ID converted to the appropriate type, or the original string if conversion fails
    /// or no type hint is available
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when featureId or layer is null</exception>
    /// <remarks>
    /// This method uses the DataType or StorageType from the layer's ID field to determine
    /// the target type for conversion. If parsing fails, it returns the original string value
    /// rather than throwing an exception.
    /// </remarks>
    public static object NormalizeKeyValue(string featureId, LayerDefinition layer)
    {
        if (featureId is null)
        {
            throw new ArgumentNullException(nameof(featureId));
        }

        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        var field = layer.Fields.FirstOrDefault(f =>
            string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase));
        var hint = field?.DataType ?? field?.StorageType;
        if (hint.IsNullOrWhiteSpace())
        {
            return featureId;
        }

        switch (hint.Trim().ToLowerInvariant())
        {
            case "int":
            case "int32":
            case "integer":
            case "smallint":
            case "int16":
                return int.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                    ? i
                    : featureId;

            case "long":
            case "int64":
            case "bigint":
                return long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
                    ? l
                    : featureId;

            case "double":
            case "float":
            case "real":
                return double.TryParse(featureId, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d)
                    ? d
                    : featureId;

            case "decimal":
            case "numeric":
                return decimal.TryParse(featureId, NumberStyles.Number, CultureInfo.InvariantCulture, out var m)
                    ? m
                    : featureId;

            case "guid":
            case "uuid":
            case "uniqueidentifier":
                return Guid.TryParse(featureId, out var g)
                    ? g
                    : featureId;

            default:
                return featureId;
        }
    }

    /// <summary>
    /// Gets the schema name from a layer definition's table name.
    /// Returns null if the table name does not include a schema qualifier.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The schema name, or null if not schema-qualified</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static string? GetSchemaName(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        var table = GetTableName(layer);
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // If table has schema.table format, return schema
        return parts.Length > 1 ? parts[0] : null;
    }

    /// <summary>
    /// Gets all field definitions from a layer definition.
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <returns>The collection of field definitions</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer is null</exception>
    public static IReadOnlyList<FieldDefinition> GetFields(LayerDefinition layer)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        return layer.Fields;
    }

    /// <summary>
    /// Checks if a layer definition contains a field with the specified name (case-insensitive).
    /// </summary>
    /// <param name="layer">The layer definition</param>
    /// <param name="fieldName">The field name to check</param>
    /// <returns>True if the field exists, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when layer or fieldName is null</exception>
    public static bool HasField(LayerDefinition layer, string fieldName)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        if (fieldName is null)
        {
            throw new ArgumentNullException(nameof(fieldName));
        }

        return layer.Fields.Any(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
    }
}
