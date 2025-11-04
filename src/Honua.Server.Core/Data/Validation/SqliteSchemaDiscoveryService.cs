// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Humanizer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// SQLite/SpatiaLite schema discovery and migration service.
/// </summary>
public sealed class SqliteSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly ILogger<SqliteSchemaDiscoveryService> _logger;

    public SqliteSchemaDiscoveryService(ILogger<SqliteSchemaDiscoveryService> logger)
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

        await using var connection = new SqliteConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Get columns
        var columns = await GetTableColumnsAsync(connection, tableName, cancellationToken);

        // Get primary key
        var primaryKey = await GetPrimaryKeyAsync(connection, tableName, cancellationToken);

        // Get geometry information (SpatiaLite)
        var geometryInfo = await GetGeometryInfoAsync(connection, tableName, cancellationToken);

        return new TableSchemaInfo
        {
            Schema = string.Empty, // SQLite doesn't have schemas
            Table = tableName,
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

    private static async Task<List<DiscoveredColumn>> GetTableColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        // SQLite uses PRAGMA table_info to get column information
        var sql = $"PRAGMA table_info({tableName})";

        await using var command = new SqliteCommand(sql, connection);

        var columns = new List<DiscoveredColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken))
        {
            // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
            var columnName = reader.GetString(1);
            var dataType = reader.GetString(2);
            var notNull = reader.GetInt32(3);
            var isPrimaryKey = reader.GetInt32(5) > 0;

            var (suggestedDataType, suggestedStorageType) = MapDatabaseTypeToMetadata(dataType);

            columns.Add(new DiscoveredColumn
            {
                Name = columnName,
                DbType = dataType,
                IsNullable = notNull == 0,
                IsPrimaryKey = isPrimaryKey,
                SuggestedDataType = suggestedDataType,
                SuggestedStorageType = suggestedStorageType
            });
        }

        return columns;
    }

    private static async Task<string?> GetPrimaryKeyAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        // Get primary key from PRAGMA table_info
        var sql = $"PRAGMA table_info({tableName})";

        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1);
            var isPrimaryKey = reader.GetInt32(5) > 0;

            if (isPrimaryKey)
            {
                return columnName;
            }
        }

        return null;
    }

    private static async Task<(string? ColumnName, string? GeometryType, int? Srid)> GetGeometryInfoAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        // SpatiaLite stores geometry information in geometry_columns view
        const string sql = @"
            SELECT
                f_geometry_column,
                geometry_type,
                srid
            FROM geometry_columns
            WHERE f_table_name = @tableName
            LIMIT 1";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);

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
            // geometry_columns view might not exist if SpatiaLite is not installed
        }

        return (null, null, null);
    }

    private static (string DataType, string StorageType) MapDatabaseTypeToMetadata(string dataType)
    {
        var lowerDataType = dataType.ToLowerInvariant();

        // SQLite has dynamic typing with 5 storage classes: NULL, INTEGER, REAL, TEXT, BLOB
        // Handle empty type (SQLite allows this)
        if (string.IsNullOrWhiteSpace(lowerDataType))
        {
            return ("string", "text");
        }

        // Integer types
        if (lowerDataType is "integer" or "int" or "tinyint" or "smallint" or "mediumint" or "bigint" or "unsigned big int" or "int2" or "int8")
        {
            return ("int32", "integer");
        }

        // Floating point and decimal
        if (lowerDataType is "real" or "double" or "double precision" or "float" or "numeric" or "decimal")
        {
            return ("float64", "real");
        }

        // Boolean (stored as INTEGER 0/1 in SQLite)
        if (lowerDataType is "boolean" or "bool")
        {
            return ("boolean", "boolean");
        }

        // Date/Time (SQLite stores as TEXT, REAL, or INTEGER)
        if (lowerDataType is "datetime" or "date" or "timestamp")
        {
            return ("datetime", lowerDataType);
        }

        // Geometry (SpatiaLite extension)
        if (lowerDataType is "geometry" or "point" or "linestring" or "polygon" or "multipoint" or "multilinestring" or "multipolygon")
        {
            return (lowerDataType, lowerDataType);
        }

        // Binary
        if (lowerDataType is "blob" or "binary")
        {
            return ("binary", "blob");
        }

        // Text/String types (default for SQLite)
        if (lowerDataType is "text" or "varchar" or "char" or "character" or "nvarchar" or "nchar" or "clob")
        {
            return ("string", "text");
        }

        // JSON (stored as TEXT)
        if (lowerDataType is "json")
        {
            return ("json", "json");
        }

        // Default: treat as string/text
        return ("string", lowerDataType.Length > 0 ? lowerDataType : "text");
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
