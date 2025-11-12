// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// PostgreSQL database schema reader with PostGIS support.
/// </summary>
public sealed class PostgreSqlSchemaReader : ISchemaReader
{
    public string ProviderName => "postgresql";

    public async Task<IntrospectionResult> IntrospectAsync(
        string connectionString,
        IntrospectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= IntrospectionOptions.Default;
        var result = new IntrospectionResult { Success = true, Errors = new List<string>(), Warnings = new List<string>() };

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var schema = new DatabaseSchema
            {
                Provider = ProviderName,
                DatabaseName = connection.Database,
                Tables = new List<TableSchema>()
            };

            // Get tables
            var tables = await GetTablesAsync(connection, options, cancellationToken);

            foreach (var table in tables)
            {
                var workingTable = table;

                // Get columns
                workingTable.Columns.AddRange(await GetColumnsAsync(connection, workingTable, cancellationToken));

                // Get primary keys
                workingTable.PrimaryKeyColumns.AddRange(await GetPrimaryKeysAsync(connection, workingTable, cancellationToken));

                // Mark PK columns
                foreach (var pkCol in workingTable.PrimaryKeyColumns)
                {
                    var column = workingTable.Columns.FirstOrDefault(c => c.ColumnName == pkCol);
                    if (column != null)
                    {
                        workingTable.Columns.Remove(column);
                        workingTable.Columns.Insert(0, column with { IsPrimaryKey = true });
                    }
                }

                // Get geometry column (PostGIS)
                var geometryColumn = await GetGeometryColumnAsync(connection, workingTable, cancellationToken);
                if (geometryColumn != null)
                {
                    workingTable = workingTable with { GeometryColumn = geometryColumn };
                }

                // Get row count
                if (options.IncludeRowCounts)
                {
                    var rowCount = await GetRowCountAsync(connection, workingTable, cancellationToken);
                    workingTable = workingTable with { RowCount = rowCount };
                }

                schema.Tables.Add(workingTable);

                if (options.MaxTables.HasValue && schema.Tables.Count >= options.MaxTables.Value)
                {
                    break;
                }
            }

            result = result with { Schema = schema };
        }
        catch (Exception ex)
        {
            result = result with
            {
                Success = false,
                Errors = new List<string> { $"Introspection failed: {ex.Message}" }
            };
        }

        return result;
    }

    public async Task<bool> TestConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<TableSchema>> GetTablesAsync(
        NpgsqlConnection connection,
        IntrospectionOptions options,
        CancellationToken cancellationToken)
    {
        var tables = new List<TableSchema>();

        var sql = @"
            SELECT
                table_schema,
                table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
                AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ";

        if (!options.IncludeSystemTables)
        {
            sql += " AND table_schema NOT LIKE 'pg_%'";
        }

        if (!string.IsNullOrWhiteSpace(options.SchemaName))
        {
            sql += $" AND table_schema = '{options.SchemaName}'";
        }

        if (!string.IsNullOrWhiteSpace(options.TableNamePattern))
        {
            sql += $" AND table_name LIKE '{options.TableNamePattern}'";
        }

        sql += " ORDER BY table_schema, table_name";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TableSchema
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1)
            });
        }

        return tables;
    }

    private async Task<List<ColumnSchema>> GetColumnsAsync(
        NpgsqlConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        var sql = @"
            SELECT
                column_name,
                data_type,
                is_nullable,
                character_maximum_length,
                numeric_precision,
                numeric_scale,
                column_default
            FROM information_schema.columns
            WHERE table_schema = @schema
                AND table_name = @table
            ORDER BY ordinal_position
            ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", table.SchemaName);
        cmd.Parameters.AddWithValue("table", table.TableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnSchema
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return columns;
    }

    private async Task<List<string>> GetPrimaryKeysAsync(
        NpgsqlConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        var primaryKeys = new List<string>();

        var sql = @"
            SELECT a.attname
            FROM pg_index i
            JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
            WHERE i.indrelid = (@schema || '.' || @table)::regclass
                AND i.indisprimary
            ORDER BY a.attnum
            ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", table.SchemaName);
        cmd.Parameters.AddWithValue("table", table.TableName);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                primaryKeys.Add(reader.GetString(0));
            }
        }
        catch
        {
            // If PK detection fails, continue without it
        }

        return primaryKeys;
    }

    private async Task<GeometryColumnInfo?> GetGeometryColumnAsync(
        NpgsqlConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        // Try PostGIS geometry_columns view first
        var sql = @"
            SELECT
                f_geometry_column,
                type,
                srid,
                coord_dimension
            FROM geometry_columns
            WHERE f_table_schema = @schema
                AND f_table_name = @table
            LIMIT 1
            ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", table.SchemaName);
        cmd.Parameters.AddWithValue("table", table.TableName);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                return new GeometryColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    GeometryType = reader.GetString(1),
                    Srid = reader.GetInt32(2),
                    CoordinateDimension = reader.GetInt32(3)
                };
            }
        }
        catch
        {
            // PostGIS not installed or geometry_columns view not available
        }

        return null;
    }

    private async Task<long?> GetRowCountAsync(
        NpgsqlConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        try
        {
            var sql = $"SELECT COUNT(*) FROM \"{table.SchemaName}\".\"{table.TableName}\"";
            await using var cmd = new NpgsqlCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        catch
        {
            return null;
        }
    }
}
