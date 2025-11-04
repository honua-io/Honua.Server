// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Humanizer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// SQL Server schema discovery and migration service.
/// </summary>
public sealed class SqlServerSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly ILogger<SqlServerSchemaDiscoveryService> _logger;

    public SqlServerSchemaDiscoveryService(ILogger<SqlServerSchemaDiscoveryService> logger)
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

        await using var connection = new SqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Get columns
        var columns = await GetTableColumnsAsync(connection, schema, table, cancellationToken);

        // Get primary key
        var primaryKey = await GetPrimaryKeyAsync(connection, schema, table, cancellationToken);

        // Get geometry information (SQL Server Spatial)
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
            1 => ("dbo", parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw new ArgumentException($"Invalid table name format: {tableName}", nameof(tableName))
        };
    }

    private static async Task<List<DiscoveredColumn>> GetTableColumnsAsync(
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    AND tc.TABLE_SCHEMA = @schema
                    AND tc.TABLE_NAME = @table
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        var columns = new List<DiscoveredColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var isPrimaryKey = reader.GetInt32(3) == 1;

            var (suggestedDataType, suggestedStorageType) = MapDatabaseTypeToMetadata(dataType);

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
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1 ku.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                AND tc.TABLE_NAME = ku.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                AND tc.TABLE_SCHEMA = @schema
                AND tc.TABLE_NAME = @table";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async Task<(string? ColumnName, string? GeometryType, int? Srid)> GetGeometryInfoAsync(
        SqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        // SQL Server spatial types are stored as geometry or geography
        const string sql = @"
            SELECT TOP 1
                c.COLUMN_NAME,
                c.DATA_TYPE,
                NULL as SRID
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_SCHEMA = @schema
                AND c.TABLE_NAME = @table
                AND c.DATA_TYPE IN ('geometry', 'geography')";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.IsDBNull(0) ? null : reader.GetString(0);
                var geometryType = reader.IsDBNull(1) ? null : reader.GetString(1);

                // For SQL Server, we'd need to query the actual geometry column to get SRID
                // Since that requires actual data, we'll return null and let the layer metadata define it
                return (columnName, geometryType, null);
            }
        }
        catch
        {
            // Spatial columns might not exist or spatial types might not be installed
        }

        return (null, null, null);
    }

    private static (string DataType, string StorageType) MapDatabaseTypeToMetadata(string dataType)
    {
        var lowerDataType = dataType.ToLowerInvariant();

        // Geometry/Geography
        if (lowerDataType is "geometry" or "geography")
        {
            return (lowerDataType, lowerDataType);
        }

        // String types
        if (lowerDataType is "nvarchar" or "varchar")
        {
            return ("string", "text");
        }

        if (lowerDataType is "nchar" or "char" or "text" or "ntext")
        {
            return ("string", lowerDataType);
        }

        // Integer types
        if (lowerDataType is "tinyint")
        {
            return ("byte", "tinyint");
        }

        if (lowerDataType is "smallint")
        {
            return ("int16", "smallint");
        }

        if (lowerDataType is "int")
        {
            return ("int32", "int");
        }

        if (lowerDataType is "bigint")
        {
            return ("int64", "bigint");
        }

        // Floating point
        if (lowerDataType is "real")
        {
            return ("float32", "real");
        }

        if (lowerDataType is "float")
        {
            return ("float64", "float");
        }

        // Decimal
        if (lowerDataType is "decimal" or "numeric")
        {
            return ("decimal", lowerDataType);
        }

        if (lowerDataType is "money" or "smallmoney")
        {
            return ("decimal", lowerDataType);
        }

        // Boolean
        if (lowerDataType is "bit")
        {
            return ("boolean", "bit");
        }

        // Date/Time
        if (lowerDataType == "date")
        {
            return ("date", "date");
        }

        if (lowerDataType is "datetime" or "datetime2" or "smalldatetime")
        {
            return ("datetime", lowerDataType);
        }

        if (lowerDataType is "datetimeoffset")
        {
            return ("datetimeoffset", "datetimeoffset");
        }

        if (lowerDataType is "time")
        {
            return ("time", "time");
        }

        // UUID/GUID
        if (lowerDataType is "uniqueidentifier")
        {
            return ("guid", "uniqueidentifier");
        }

        // Binary
        if (lowerDataType is "binary" or "varbinary" or "image")
        {
            return ("binary", lowerDataType);
        }

        // XML
        if (lowerDataType is "xml")
        {
            return ("xml", "xml");
        }

        // SQL_VARIANT
        if (lowerDataType is "sql_variant")
        {
            return ("string", "sql_variant");
        }

        // Rowversion/Timestamp
        if (lowerDataType is "rowversion" or "timestamp")
        {
            return ("binary", lowerDataType);
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
