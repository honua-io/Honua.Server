// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Text.Json.Nodes;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides common utilities for reading feature records from database result sets.
/// Consolidates record reading, attribute extraction, and field value conversion logic.
/// </summary>
public static class FeatureRecordReader
{
    /// <summary>
    /// Reads a complete feature record from a data reader, including attributes and geometry.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="layer">The layer definition containing field metadata</param>
    /// <param name="geometryFieldName">Optional override for the geometry field name (defaults to layer.GeometryField)</param>
    /// <param name="geometryReader">Delegate to read geometry from the result set (provider-specific)</param>
    /// <returns>A feature record with all attributes and geometry</returns>
    /// <exception cref="ArgumentNullException">Thrown if reader or layer is null</exception>
    public static FeatureRecord ReadFeatureRecord(
        IDataReader reader,
        LayerDefinition layer,
        string? geometryFieldName = null,
        Func<IDataReader, string, JsonNode?>? geometryReader = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(layer);

        var effectiveGeometryField = geometryFieldName ?? layer.GeometryField;
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        JsonNode? geometryNode = null;

        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);

            // Use custom geometry reader if provided, otherwise skip geometry column
            if (string.Equals(columnName, effectiveGeometryField, StringComparison.OrdinalIgnoreCase))
            {
                if (geometryReader != null)
                {
                    geometryNode = geometryReader(reader, columnName);
                }
                continue;
            }

            // Read non-geometry attribute
            attributes[columnName] = reader.IsDBNull(index) ? null : reader.GetValue(index);
        }

        // Add geometry to attributes if it was read
        if (geometryNode is not null)
        {
            attributes[effectiveGeometryField] = geometryNode;
        }
        else if (!attributes.ContainsKey(effectiveGeometryField))
        {
            attributes[effectiveGeometryField] = null;
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    /// <summary>
    /// Reads a complete feature record with vendor-specific geometry handling.
    /// This overload allows providers to specify exactly which columns contain geometry data
    /// and how to process them.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="layer">The layer definition containing field metadata</param>
    /// <param name="geometryFieldName">The name of the geometry field in the layer</param>
    /// <param name="skipColumns">Set of column names to skip (e.g., geometry aliases, SRID columns)</param>
    /// <param name="geometryExtractor">Delegate that extracts the final geometry node from all columns</param>
    /// <returns>A feature record with all attributes and geometry</returns>
    public static FeatureRecord ReadFeatureRecordWithCustomGeometry(
        IDataReader reader,
        LayerDefinition layer,
        string geometryFieldName,
        ISet<string> skipColumns,
        Func<JsonNode?> geometryExtractor)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(skipColumns);
        ArgumentNullException.ThrowIfNull(geometryExtractor);

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);

            // Skip geometry-related columns
            if (skipColumns.Contains(columnName))
            {
                continue;
            }

            attributes[columnName] = reader.IsDBNull(index) ? null : reader.GetValue(index);
        }

        // Extract and add geometry
        var geometryNode = geometryExtractor();
        if (geometryNode is not null)
        {
            attributes[geometryFieldName] = geometryNode;
        }
        else if (!attributes.ContainsKey(geometryFieldName))
        {
            attributes[geometryFieldName] = null;
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    /// <summary>
    /// Reads multiple attributes from a data reader into a dictionary.
    /// Useful for reading statistics results or grouped data.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="fieldNames">The names of fields to read</param>
    /// <returns>Dictionary of field names to values</returns>
    public static Dictionary<string, object?> ReadAttributes(
        IDataReader reader,
        IEnumerable<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(fieldNames);

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var ordinal = 0;

        foreach (var fieldName in fieldNames)
        {
            if (ordinal >= reader.FieldCount)
            {
                break;
            }

            attributes[fieldName] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            ordinal++;
        }

        return attributes;
    }

    /// <summary>
    /// Reads a single field value from a data reader with type-safe conversion.
    /// </summary>
    /// <typeparam name="T">The expected type of the field value</typeparam>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="ordinal">The zero-based column ordinal</param>
    /// <param name="defaultValue">Default value to return if the field is null</param>
    /// <returns>The field value converted to type T, or the default value if null</returns>
    public static T? GetFieldValue<T>(
        IDataReader reader,
        int ordinal,
        T? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.IsDBNull(ordinal))
        {
            return defaultValue;
        }

        var value = reader.GetValue(ordinal);
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Attempt conversion
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Reads a field value from a data reader without conversion.
    /// Handles null/DBNull conversion.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="ordinal">The zero-based column ordinal</param>
    /// <returns>The field value or null if DBNull</returns>
    public static object? GetFieldValue(IDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
    }

    /// <summary>
    /// Gets a string field value from a data reader.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="ordinal">The zero-based column ordinal</param>
    /// <returns>The field value as a string, or null if DBNull</returns>
    public static string? GetString(IDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Gets an integer field value from a data reader.
    /// </summary>
    /// <param name="reader">The data reader positioned at the current row</param>
    /// <param name="ordinal">The zero-based column ordinal</param>
    /// <param name="defaultValue">Default value to return if the field is null</param>
    /// <returns>The field value as an integer, or the default value if null</returns>
    public static int? GetInt32(IDataReader reader, int ordinal, int? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal);
    }

    /// <summary>
    /// Attempts to find the ordinal (column index) of a geometry field in the reader.
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <param name="geometryFieldName">The name of the geometry field to find</param>
    /// <returns>The ordinal of the geometry field, or -1 if not found</returns>
    public static int TryGetGeometryOrdinal(IDataReader reader, string? geometryFieldName)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (string.IsNullOrWhiteSpace(geometryFieldName))
        {
            return -1;
        }

        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), geometryFieldName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks if a column with the specified name exists in the reader.
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <param name="columnName">The column name to check</param>
    /// <returns>True if the column exists, false otherwise</returns>
    public static bool HasColumn(IDataReader reader, string columnName)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(columnName);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads all column names from the data reader.
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <returns>List of column names</returns>
    public static List<string> GetColumnNames(IDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var columns = new List<string>(reader.FieldCount);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        return columns;
    }
}
