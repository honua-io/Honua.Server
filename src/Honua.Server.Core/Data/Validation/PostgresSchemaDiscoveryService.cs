// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Humanizer;
using Microsoft.Extensions.Logging;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// PostgreSQL/PostGIS schema discovery and migration service.
/// </summary>
public sealed class PostgresSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly ILogger<PostgresSchemaDiscoveryService> _logger;

    public PostgresSchemaDiscoveryService(ILogger<PostgresSchemaDiscoveryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TableSchemaInfo> DiscoverTableSchemaAsync(
        DataSourceDefinition dataSource,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNullOrWhiteSpace(tableName);

        var (schema, table) = ParseTableName(tableName);

        await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Get columns
        var columns = await GetTableColumnsAsync(connection, schema, table, cancellationToken);

        // Get primary key
        var primaryKey = await GetPrimaryKeyAsync(connection, schema, table, cancellationToken);

        // Get geometry information
        var geometryInfo = await GetGeometryInfoAsync(connection, schema, table, cancellationToken);

        return new TableSchemaInfo
        {
            Schema = schema,
            Table = table,
            Columns = columns,
            PrimaryKey = primaryKey,
            GeometryColumn = geometryInfo.ColumnName,
            GeometryType = geometryInfo.GeometryType,
            Srid = geometryInfo.Srid
        };
    }

    public async Task<SchemaSyncResult> SyncLayerFieldsAsync(
        LayerDefinition layer,
        DataSourceDefinition dataSource,
        SchemaSyncOptions options,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(dataSource);
        Guard.NotNull(options);

        var tableName = layer.Storage?.Table ?? layer.Id;
        var schemaInfo = await DiscoverTableSchemaAsync(dataSource, tableName, cancellationToken);

        var updatedLayer = layer;
        var addedFields = new List<string>();
        var removedFields = new List<string>();
        var updatedFields = new List<string>();
        var warnings = new List<string>();

        // Build lookup of existing fields
        var existingFields = layer.Fields.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
        var newFields = new List<FieldDefinition>();

        // Process each discovered column
        foreach (var column in schemaInfo.Columns)
        {
            // Skip geometry column (handled separately)
            if (string.Equals(column.Name, schemaInfo.GeometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip primary key if it's not in the field list
            if (string.Equals(column.Name, schemaInfo.PrimaryKey, StringComparison.OrdinalIgnoreCase) &&
                !existingFields.ContainsKey(column.Name))
            {
                // Primary key can be included or excluded - if it's not already in metadata, skip it
                continue;
            }

            if (existingFields.TryGetValue(column.Name, out var existing))
            {
                // Field exists - check if we need to update it
                var updated = false;
                var field = existing;

                if (options.UpdateFieldTypes)
                {
                    var suggestedDataType = column.SuggestedDataType ?? existing.DataType;
                    var suggestedStorageType = column.SuggestedStorageType ?? existing.StorageType;

                    if (!string.Equals(existing.DataType, suggestedDataType, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(existing.StorageType, suggestedStorageType, StringComparison.OrdinalIgnoreCase))
                    {
                        field = existing with
                        {
                            DataType = suggestedDataType,
                            StorageType = suggestedStorageType
                        };
                        updated = true;
                    }
                }

                if (options.UpdateNullability && existing.Nullable != column.IsNullable)
                {
                    field = field with { Nullable = column.IsNullable };
                    updated = true;
                }

                if (updated)
                {
                    updatedFields.Add(column.Name);
                    _logger.LogInformation("Updated field {FieldName} in layer {LayerId}", column.Name, layer.Id);
                }

                newFields.Add(field);
                existingFields.Remove(column.Name);
            }
            else if (options.AddMissingFields)
            {
                // Add new field with friendly alias
                var newField = new FieldDefinition
                {
                    Name = column.Name,
                    Alias = GenerateFriendlyAlias(column.Name),
                    DataType = column.SuggestedDataType,
                    StorageType = column.SuggestedStorageType,
                    Nullable = column.IsNullable
                };

                newFields.Add(newField);
                addedFields.Add(column.Name);
                _logger.LogInformation("Added field {FieldName} to layer {LayerId}", column.Name, layer.Id);
            }
        }

        // Handle fields in metadata but not in database
        if (options.RemoveOrphanedFields)
        {
            foreach (var orphan in existingFields.Keys)
            {
                removedFields.Add(orphan);
                warnings.Add($"Removed field '{orphan}' that no longer exists in database table");
                _logger.LogWarning("Removed orphaned field {FieldName} from layer {LayerId}", orphan, layer.Id);
            }
        }
        else if (options.PreserveCustomMetadata)
        {
            // Keep orphaned fields if preserving custom metadata
            foreach (var field in existingFields.Values)
            {
                newFields.Add(field);
                warnings.Add($"Field '{field.Name}' not found in database but preserved in metadata");
            }
        }

        // Create updated layer
        updatedLayer = layer with
        {
            Fields = newFields.ToArray()
        };

        // Update storage information if we discovered it
        if (schemaInfo.PrimaryKey != null && string.IsNullOrWhiteSpace(layer.Storage?.PrimaryKey))
        {
            var storage = layer.Storage ?? new LayerStorageDefinition { Table = tableName };
            updatedLayer = updatedLayer with
            {
                Storage = storage with { PrimaryKey = schemaInfo.PrimaryKey }
            };
        }

        if (schemaInfo.GeometryColumn != null && string.IsNullOrWhiteSpace(layer.Storage?.GeometryColumn))
        {
            var storage = layer.Storage ?? new LayerStorageDefinition { Table = tableName };
            updatedLayer = updatedLayer with
            {
                Storage = storage with
                {
                    GeometryColumn = schemaInfo.GeometryColumn,
                    Srid = schemaInfo.Srid ?? storage.Srid
                }
            };
        }

        return new SchemaSyncResult
        {
            UpdatedLayer = updatedLayer,
            AddedFields = addedFields,
            RemovedFields = removedFields,
            UpdatedFields = updatedFields,
            Warnings = warnings
        };
    }

    private static (string Schema, string Table) ParseTableName(string tableName)
    {
        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 => ("public", parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName))
        };
    }

    private static async Task<List<DiscoveredColumn>> GetTableColumnsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.column_name,
                c.data_type,
                c.udt_name,
                c.is_nullable,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                    AND tc.table_schema = ku.table_schema
                    AND tc.table_name = ku.table_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_schema = @schema
                    AND tc.table_name = @table
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        var columns = new List<DiscoveredColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var udtName = reader.GetString(2);
            var isNullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var isPrimaryKey = reader.GetBoolean(4);

            var (suggestedDataType, suggestedStorageType) = MapDatabaseTypeToMetadata(dataType, udtName);

            columns.Add(new DiscoveredColumn
            {
                Name = columnName,
                DbType = dataType,
                IsNullable = isNullable,
                IsPrimaryKey = isPrimaryKey,
                SuggestedDataType = suggestedDataType,
                SuggestedStorageType = suggestedStorageType
            });
        }

        return columns;
    }

    private static async Task<string?> GetPrimaryKeyAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT ku.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage ku
                ON tc.constraint_name = ku.constraint_name
                AND tc.table_schema = ku.table_schema
                AND tc.table_name = ku.table_name
            WHERE tc.constraint_type = 'PRIMARY KEY'
                AND tc.table_schema = @schema
                AND tc.table_name = @table
            LIMIT 1";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async Task<(string? ColumnName, string? GeometryType, int? Srid)> GetGeometryInfoAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                f_geometry_column,
                type,
                srid
            FROM geometry_columns
            WHERE f_table_schema = @schema AND f_table_name = @table
            LIMIT 1";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken))
            {
                return (
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2)
                );
            }
        }
        catch
        {
            // geometry_columns view might not exist if PostGIS is not installed
        }

        return (null, null, null);
    }

    private static (string DataType, string StorageType) MapDatabaseTypeToMetadata(string dataType, string udtName)
    {
        var lowerDataType = dataType.ToLowerInvariant();
        var lowerUdtName = udtName.ToLowerInvariant();
        var isUserDefined = string.Equals(dataType, "USER-DEFINED", StringComparison.OrdinalIgnoreCase);

        // Geometry/Geography
        if (isUserDefined && (lowerUdtName == "geometry" || lowerUdtName == "geography"))
        {
            return (lowerUdtName, lowerUdtName);
        }

        // String types
        if (lowerDataType is "character varying" or "varchar")
        {
            return ("string", "text");
        }

        if (lowerDataType is "text" or "character" or "char")
        {
            return ("string", lowerDataType);
        }

        // Integer types
        if (lowerDataType is "smallint" or "int2")
        {
            return ("int16", "smallint");
        }

        if (lowerDataType is "integer" or "int" or "int4")
        {
            return ("int32", "integer");
        }

        if (lowerDataType is "bigint" or "int8")
        {
            return ("int64", "bigint");
        }

        // Floating point
        if (lowerDataType is "real" or "float4")
        {
            return ("float32", "real");
        }

        if (lowerDataType is "double precision" or "float8")
        {
            return ("float64", "double precision");
        }

        // Decimal
        if (lowerDataType is "numeric" or "decimal")
        {
            return ("decimal", lowerDataType);
        }

        // Boolean
        if (lowerDataType is "boolean" or "bool")
        {
            return ("boolean", "boolean");
        }

        // Date/Time
        if (lowerDataType == "date")
        {
            return ("date", "date");
        }

        if (lowerDataType is "timestamp without time zone" or "timestamp")
        {
            return ("datetime", "timestamp");
        }

        if (lowerDataType is "timestamp with time zone" or "timestamptz")
        {
            return ("datetimeoffset", "timestamptz");
        }

        if (lowerDataType is "time without time zone" or "time with time zone" or "time")
        {
            return ("time", lowerDataType);
        }

        // UUID
        if (lowerDataType == "uuid" || lowerUdtName == "uuid")
        {
            return ("guid", "uuid");
        }

        // JSON
        if (lowerDataType is "json" or "jsonb")
        {
            return ("json", lowerDataType);
        }

        // Binary
        if (lowerDataType == "bytea")
        {
            return ("binary", "bytea");
        }

        // Default: use database type as-is
        return (lowerDataType, lowerUdtName);
    }

    /// <summary>
    /// Generates a friendly, human-readable alias from a column name using Humanizer.
    /// Examples:
    /// - "ImmedSupervisor" => "Immed supervisor"
    /// - "employee_id" => "Employee"
    /// - "CREATED_AT" => "Created at"
    /// - "FKDepartmentID" => "Fk department id"
    /// </summary>
    private static string GenerateFriendlyAlias(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return columnName;
        }

        // Use Humanizer to convert column names to friendly titles
        // This handles PascalCase, camelCase, snake_case, etc.
        return columnName.Humanize(LetterCasing.Title);
    }
}
