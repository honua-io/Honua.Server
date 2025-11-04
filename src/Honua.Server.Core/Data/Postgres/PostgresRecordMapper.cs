// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Handles mapping between database records and feature records.
/// Provides utilities for record normalization and parameter conversion.
/// </summary>
internal static class PostgresRecordMapper
{
    public static FeatureRecord CreateFeatureRecord(NpgsqlDataReader reader, LayerDefinition layer)
    {
        var attributes = new ReadOnlyDictionary<string, object?>(ReadAttributes(reader, layer, out var version));
        return new FeatureRecord(attributes, version);
    }

    public static IDictionary<string, object?> ReadAttributes(NpgsqlDataReader reader, LayerDefinition layer)
    {
        return ReadAttributes(reader, layer, out _);
    }

    public static IDictionary<string, object?> ReadAttributes(NpgsqlDataReader reader, LayerDefinition layer, out object? version)
    {
        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        JsonNode? geometryNode = null;
        var geometryField = layer.GeometryField;
        version = null;

        for (var index = 0; index < reader.FieldCount; index++)
        {
            var columnName = reader.GetName(index);

            if (string.Equals(columnName, PostgresFeatureQueryBuilder.GeoJsonColumnAlias, StringComparison.OrdinalIgnoreCase))
            {
                geometryNode = reader.IsDBNull(index) ? null : FeatureRecordNormalizer.ParseGeometry(reader.GetString(index));
                continue;
            }

            if (string.Equals(columnName, geometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extract row_version for optimistic concurrency control
            if (string.Equals(columnName, "row_version", StringComparison.OrdinalIgnoreCase))
            {
                version = reader.IsDBNull(index) ? null : reader.GetValue(index);
                // Continue to also include it in attributes for backwards compatibility
            }

            record[columnName] = reader.IsDBNull(index) ? null : reader.GetValue(index);
        }

        if (geometryNode is not null)
        {
            record[geometryField] = geometryNode;
        }
        else if (!record.ContainsKey(geometryField))
        {
            record[geometryField] = null;
        }

        return record;
    }

    public static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        PostgresQueryDefinition definition,
        TimeSpan? commandTimeout = null,
        int? defaultTimeoutSeconds = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;

        // Use per-query timeout override if provided, otherwise use configured default
        // This allows slow analytical queries to specify longer timeouts
        command.CommandTimeout = commandTimeout.HasValue
            ? (int)commandTimeout.Value.TotalSeconds
            : (defaultTimeoutSeconds ?? 30); // Fallback to 30 seconds if no default provided

        AddParameters(command, definition.Parameters);
        return command;
    }

    public static string ResolveTableName(LayerDefinition layer) =>
        LayerMetadataHelper.GetTableExpression(layer, QuoteIdentifier);

    public static string ResolvePrimaryKey(LayerDefinition layer) =>
        LayerMetadataHelper.GetPrimaryKeyColumn(layer);

    public static NormalizedRecord NormalizeRecord(LayerDefinition layer, IReadOnlyDictionary<string, object?> attributes, bool includeKey)
    {
        var srid = layer.Storage?.Srid ?? CrsHelper.Wgs84;
        var keyColumn = layer.Storage?.PrimaryKey ?? layer.IdField;
        var geometryColumn = layer.Storage?.GeometryColumn ?? layer.GeometryField;

        var columns = new List<NormalizedColumn>();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var index = 0;

        foreach (var pair in attributes)
        {
            var columnName = pair.Key;
            if (!includeKey && string.Equals(columnName, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isGeometry = string.Equals(columnName, geometryColumn, StringComparison.OrdinalIgnoreCase);
            var value = NormalizeValue(pair.Value, isGeometry);
            if (isGeometry && value is null)
            {
                continue;
            }

            var parameterName = $"@p{index++}";
            columns.Add(new NormalizedColumn(QuoteIdentifier(columnName), parameterName, value, isGeometry));
            parameters[parameterName] = value ?? DBNull.Value;
        }

        return new NormalizedRecord(columns, parameters, srid);
    }

    public static object? NormalizeValue(object? value, bool isGeometry)
    {
        return FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
    }

    public static string BuildValueExpression(NormalizedColumn column, int srid)
    {
        if (column.IsGeometry)
        {
            if (column.IsNull)
            {
                return "NULL";
            }

            var text = column.Value as string;
            var function = LooksLikeGeoJson(text)
                ? "ST_GeomFromGeoJSON"
                : "ST_GeomFromText";

            return $"ST_SetSRID({function}({column.ParameterName}), {srid})";
        }

        return column.ParameterName;
    }

    public static bool LooksLikeGeoJson(string? value)
    {
        return FeatureRecordNormalizer.LooksLikeJson(value);
    }

    public static object NormalizeKeyParameter(LayerDefinition layer, string featureId)
    {
        return SqlParameterHelper.NormalizeKeyParameter(layer, featureId);
    }

    public static void AddParameters(NpgsqlCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        SqlParameterHelper.AddParameters(command, parameters);
    }

    public static string QuoteIdentifier(string identifier)
    {
        // Validate identifier for SQL injection protection before quoting
        return SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);
    }
}

internal sealed record NormalizedColumn(string ColumnName, string ParameterName, object? Value, bool IsGeometry)
{
    public bool IsNull => Value is null || Value is DBNull;
}

internal sealed record NormalizedRecord(IReadOnlyList<NormalizedColumn> Columns, IReadOnlyDictionary<string, object?> Parameters, int Srid);
