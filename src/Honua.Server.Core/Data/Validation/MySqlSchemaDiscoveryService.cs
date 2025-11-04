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
using MySqlConnector;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// MySQL schema discovery and migration service.
/// </summary>
public sealed class MySqlSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly ILogger<MySqlSchemaDiscoveryService> _logger;

    public MySqlSchemaDiscoveryService(ILogger<MySqlSchemaDiscoveryService> logger)
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

        await using var connection = new MySqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (database, table) = ParseTableName(tableName, connection.Database);

        // Get columns
        var columns = await GetTableColumnsAsync(connection, database, table, cancellationToken);

        // Get primary key
        var primaryKey = await GetPrimaryKeyAsync(connection, database, table, cancellationToken);

        // Get geometry information (MySQL Spatial)
        var geometryInfo = await GetGeometryInfoAsync(connection, database, table, cancellationToken);

        return new TableSchemaInfo
        {
            Schema = database,
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

    private static (string Database, string Table) ParseTableName(string tableName, string defaultDatabase)
    {
        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 => (defaultDatabase, parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName))
        };
    }

    private static async Task<List<DiscoveredColumn>> GetTableColumnsAsync(
        MySqlConnection connection,
        string database,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.column_name,
                c.data_type,
                c.column_type,
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
                    AND tc.table_schema = @database
                    AND tc.table_name = @table
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_schema = @database AND c.table_name = @table
            ORDER BY c.ordinal_position";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@database", database);
        command.Parameters.AddWithValue("@table", table);

        var columns = new List<DiscoveredColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var columnType = reader.GetString(2);
            var isNullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var isPrimaryKey = reader.GetBoolean(4);

            var (suggestedDataType, suggestedStorageType) = MapDatabaseTypeToMetadata(dataType, columnType);

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
        MySqlConnection connection,
        string database,
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
                AND tc.table_schema = @database
                AND tc.table_name = @table
            LIMIT 1";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@database", database);
        command.Parameters.AddWithValue("@table", table);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async Task<(string? ColumnName, string? GeometryType, int? Srid)> GetGeometryInfoAsync(
        MySqlConnection connection,
        string database,
        string table,
        CancellationToken cancellationToken)
    {
        // MySQL Spatial stores geometry information in information_schema.columns
        const string sql = @"
            SELECT
                column_name,
                data_type,
                SRS_ID
            FROM information_schema.columns
            WHERE table_schema = @database
                AND table_name = @table
                AND data_type IN ('geometry', 'point', 'linestring', 'polygon', 'multipoint', 'multilinestring', 'multipolygon', 'geometrycollection')
            LIMIT 1";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@database", database);
        command.Parameters.AddWithValue("@table", table);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.IsDBNull(0) ? null : reader.GetString(0);
                var geometryType = reader.IsDBNull(1) ? null : reader.GetString(1);
                var srid = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);

                return (columnName, geometryType, srid);
            }
        }
        catch
        {
            // Spatial columns might not exist or spatial extension might not be installed
        }

        return (null, null, null);
    }

    private static (string DataType, string StorageType) MapDatabaseTypeToMetadata(string dataType, string columnType)
    {
        var lowerDataType = dataType.ToLowerInvariant();
        var lowerColumnType = columnType.ToLowerInvariant();

        // Geometry types
        if (lowerDataType is "geometry" or "point" or "linestring" or "polygon" or "multipoint" or "multilinestring" or "multipolygon" or "geometrycollection")
        {
            return (lowerDataType, lowerDataType);
        }

        // String types
        if (lowerDataType is "varchar" or "char")
        {
            return ("string", "text");
        }

        if (lowerDataType is "text" or "longtext" or "mediumtext" or "tinytext")
        {
            return ("string", lowerDataType);
        }

        // Integer types
        if (lowerDataType is "tinyint")
        {
            // MySQL uses tinyint(1) for boolean
            if (lowerColumnType.Contains("tinyint(1)"))
            {
                return ("boolean", "boolean");
            }
            return ("int16", "tinyint");
        }

        if (lowerDataType is "smallint")
        {
            return ("int16", "smallint");
        }

        if (lowerDataType is "mediumint" or "int" or "integer")
        {
            return ("int32", lowerDataType);
        }

        if (lowerDataType is "bigint")
        {
            return ("int64", "bigint");
        }

        // Floating point
        if (lowerDataType is "float" or "real")
        {
            return ("float32", lowerDataType);
        }

        if (lowerDataType is "double" or "double precision")
        {
            return ("float64", "double");
        }

        // Decimal
        if (lowerDataType is "decimal" or "numeric")
        {
            return ("decimal", lowerDataType);
        }

        // Date/Time
        if (lowerDataType == "date")
        {
            return ("date", "date");
        }

        if (lowerDataType is "datetime")
        {
            return ("datetime", "datetime");
        }

        if (lowerDataType is "timestamp")
        {
            return ("datetimeoffset", "timestamp");
        }

        if (lowerDataType is "time")
        {
            return ("time", "time");
        }

        if (lowerDataType is "year")
        {
            return ("int16", "year");
        }

        // JSON
        if (lowerDataType is "json")
        {
            return ("json", "json");
        }

        // Binary
        if (lowerDataType is "blob" or "longblob" or "mediumblob" or "tinyblob" or "binary" or "varbinary")
        {
            return ("binary", lowerDataType);
        }

        // Enum and Set
        if (lowerDataType is "enum")
        {
            return ("string", "enum");
        }

        if (lowerDataType is "set")
        {
            return ("string", "set");
        }

        // Default: use database type as-is
        return (lowerDataType, lowerDataType);
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
