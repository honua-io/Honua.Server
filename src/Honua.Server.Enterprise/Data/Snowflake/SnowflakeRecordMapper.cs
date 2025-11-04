// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Snowflake.Data.Client;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Snowflake;

/// <summary>
/// Handles mapping between Snowflake database records and feature records.
/// Provides utilities for creating feature records from data readers.
/// Migrated to use consolidated FeatureRecordReader and GeometryReader utilities.
/// </summary>
internal static class SnowflakeRecordMapper
{
    /// <summary>
    /// Creates a FeatureRecord from a Snowflake data reader.
    /// Handles GeoJSON geometry conversion from Snowflake GEOGRAPHY/GEOMETRY types.
    /// Uses consolidated FeatureRecordReader.ReadFeatureRecordWithCustomGeometry for standard record reading.
    /// </summary>
    /// <param name="reader">Snowflake data reader positioned at a valid row</param>
    /// <param name="layer">Layer definition for mapping</param>
    /// <returns>Populated FeatureRecord</returns>
    public static FeatureRecord CreateFeatureRecord(IDataReader reader, LayerDefinition layer)
    {
        var geometryField = layer.GeometryField;
        var skipColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "_geojson" };

        // Use consolidated FeatureRecordReader with custom geometry extraction
        return FeatureRecordReader.ReadFeatureRecordWithCustomGeometry(
            reader,
            layer,
            geometryField,
            skipColumns,
            geometryExtractor: () =>
            {
                // Find _geojson column and parse it
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (string.Equals(reader.GetName(i), "_geojson", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.IsDBNull(i))
                        {
                            return null;
                        }

                        var geoJsonText = reader.GetString(i);
                        return GeometryReader.ReadGeoJsonGeometry(geoJsonText);
                    }
                }

                return null;
            });
    }

    /// <summary>
    /// Creates a Snowflake command from SQL and parameters.
    /// Uses consolidated SqlParameterHelper for parameter handling.
    /// </summary>
    /// <param name="connection">Active Snowflake connection</param>
    /// <param name="sql">SQL query text</param>
    /// <param name="parameters">Named parameters</param>
    /// <returns>Configured Snowflake command</returns>
    public static SnowflakeDbCommand CreateCommand(
        SnowflakeDbConnection connection,
        string sql,
        Dictionary<string, object?> parameters)
    {
        var command = (SnowflakeDbCommand)connection.CreateCommand();
        command.CommandText = sql;

        // Use consolidated SqlParameterHelper for parameter addition
        SqlParameterHelper.AddParameters(command, parameters);

        return command;
    }

    /// <summary>
    /// Quotes a Snowflake identifier using double quotes, with SQL injection protection.
    /// Validates the identifier for length, valid characters, and potential injection attacks before quoting.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote (table, column, or schema name)</param>
    /// <returns>The safely quoted identifier for use in Snowflake SQL</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid or potentially malicious</exception>
    public static string QuoteIdentifier(string identifier)
    {
        SqlIdentifierValidator.ValidateIdentifier(identifier, nameof(identifier));

        // Split on dots to handle qualified names (schema.table, database.schema.table)
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Quote each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = parts[i].Trim('"'); // Remove existing quotes if any
            parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
        }

        return string.Join('.', parts);
    }

    /// <summary>
    /// Resolves the fully qualified table name for a layer.
    /// Uses consolidated LayerMetadataHelper for metadata extraction.
    /// </summary>
    /// <param name="layer">Layer definition</param>
    /// <returns>Quoted table name</returns>
    public static string ResolveTableName(LayerDefinition layer)
    {
        // Use consolidated LayerMetadataHelper for table name extraction
        return LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);
    }

    /// <summary>
    /// Resolves the primary key column name for a layer.
    /// Uses consolidated LayerMetadataHelper for metadata extraction.
    /// </summary>
    /// <param name="layer">Layer definition</param>
    /// <returns>Primary key column name (unquoted)</returns>
    public static string ResolvePrimaryKey(LayerDefinition layer)
    {
        // Use consolidated LayerMetadataHelper for primary key extraction
        return LayerMetadataHelper.GetPrimaryKeyColumn(layer);
    }

    /// <summary>
    /// Parses a GeoJSON string into a JsonNode.
    /// Delegates to consolidated GeometryReader for geometry parsing.
    /// </summary>
    /// <param name="text">GeoJSON string</param>
    /// <returns>Parsed JsonNode or null if parsing fails</returns>
    public static JsonNode? ParseGeometry(string? text)
    {
        // Use consolidated GeometryReader for GeoJSON parsing
        return GeometryReader.ReadGeoJsonGeometry(text);
    }
}
