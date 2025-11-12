// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// SQLite database schema reader with SpatiaLite support.
/// </summary>
public sealed class SqliteSchemaReader : ISchemaReader
{
    public string ProviderName => "sqlite";

    public async Task<IntrospectionResult> IntrospectAsync(
        string connectionString,
        IntrospectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= IntrospectionOptions.Default;
        var result = new IntrospectionResult { Success = true, Errors = new List<string>(), Warnings = new List<string>() };

        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var schema = new DatabaseSchema
            {
                Provider = ProviderName,
                DatabaseName = connection.DataSource,
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
                var pkColumns = workingTable.Columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToList();
                workingTable.PrimaryKeyColumns.AddRange(pkColumns);

                // Get geometry column (SpatiaLite)
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
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<TableSchema>> GetTablesAsync(
        SqliteConnection connection,
        IntrospectionOptions options,
        CancellationToken cancellationToken)
    {
        var tables = new List<TableSchema>();

        var sql = @"
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
                AND name NOT LIKE 'sqlite_%'
            ";

        if (!options.IncludeSystemTables)
        {
            sql += " AND name NOT IN ('geometry_columns', 'spatial_ref_sys', 'spatialite_history')";
        }

        if (!string.IsNullOrWhiteSpace(options.TableNamePattern))
        {
            sql += $" AND name LIKE '{options.TableNamePattern}'";
        }

        sql += " ORDER BY name";

        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TableSchema
            {
                SchemaName = "main",
                TableName = reader.GetString(0)
            });
        }

        return tables;
    }

    private async Task<List<ColumnSchema>> GetColumnsAsync(
        SqliteConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        var sql = $"PRAGMA table_info('{table.TableName}')";

        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            // PRAGMA table_info columns:
            // 0: cid
            // 1: name
            // 2: type
            // 3: notnull
            // 4: dflt_value
            // 5: pk

            columns.Add(new ColumnSchema
            {
                ColumnName = reader.GetString(1),
                DataType = reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                IsPrimaryKey = reader.GetInt32(5) > 0,
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetValue(4).ToString()
            });
        }

        return columns;
    }

    private async Task<GeometryColumnInfo?> GetGeometryColumnAsync(
        SqliteConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        // Try SpatiaLite geometry_columns table
        var sql = @"
            SELECT
                f_geometry_column,
                geometry_type,
                srid,
                coord_dimension
            FROM geometry_columns
            WHERE f_table_name = @table
            LIMIT 1
            ";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@table", table.TableName);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                return new GeometryColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    GeometryType = NormalizeGeometryType(reader.GetInt32(1)),
                    Srid = reader.GetInt32(2),
                    CoordinateDimension = reader.GetInt32(3)
                };
            }
        }
        catch
        {
            // SpatiaLite not enabled or geometry_columns table not available
        }

        return null;
    }

    private async Task<long?> GetRowCountAsync(
        SqliteConnection connection,
        TableSchema table,
        CancellationToken cancellationToken)
    {
        try
        {
            var sql = $"SELECT COUNT(*) FROM \"{table.TableName}\"";
            await using var cmd = new SqliteCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        catch
        {
            return null;
        }
    }

    private string NormalizeGeometryType(int geometryTypeCode)
    {
        // SpatiaLite geometry type codes (simplified)
        return geometryTypeCode switch
        {
            1 or 1001 => "Point",
            2 or 1002 => "LineString",
            3 or 1003 => "Polygon",
            4 or 1004 => "MultiPoint",
            5 or 1005 => "MultiLineString",
            6 or 1006 => "MultiPolygon",
            7 or 1007 => "GeometryCollection",
            _ => "Geometry"
        };
    }
}
