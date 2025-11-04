// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Data.SqlServer;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Migration.GeoservicesRest;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace Honua.Server.Core.Migration;

public sealed class LayerSchemaCreator
{
    public async Task EnsureLayerSchemaAsync(FeatureContext context, LayerSchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schema);

        var providerKey = context.DataSource.Provider?.Trim().ToLowerInvariant();
        switch (providerKey)
        {
            case SqliteDataStoreProvider.ProviderKey:
                await EnsureSqliteSchemaAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case PostgresDataStoreProvider.ProviderKey:
                await EnsurePostgresSchemaAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case SqlServerDataStoreProvider.ProviderKey:
                await EnsureSqlServerSchemaAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case MySqlDataStoreProvider.ProviderKey:
                await EnsureMySqlSchemaAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"Schema creation for data source provider '{context.DataSource.Provider}' is not supported.");
        }
    }

    private static async Task EnsureSqliteSchemaAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var quotedTable = QuoteSqliteIdentifier(schema.TableName);

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"drop table if exists {quotedTable};";
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var columnDefinitions = BuildSqliteColumns(schema);
        var ddl = $"create table {quotedTable} ({string.Join(", ", columnDefinitions)});";

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = ddl;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsurePostgresSchemaAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, "public");
        var quotedSchema = QuotePostgresIdentifier(schemaName);
        var quotedTable = QuotePostgresIdentifier(tableName);
        var qualified = $"{quotedSchema}.{quotedTable}";

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"drop table if exists {qualified};";
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var columnDefinitions = BuildPostgresColumns(schema);
        var ddl = $"create table {qualified} ({string.Join(", ", columnDefinitions)});";

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = ddl;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureSqlServerSchemaAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, "dbo");
        var quotedSchema = QuoteSqlServerIdentifier(schemaName);
        var quotedTable = QuoteSqlServerIdentifier(tableName);
        var qualified = $"{quotedSchema}.{quotedTable}";

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"if object_id('{schemaName}.{tableName}', 'U') is not null drop table {qualified};";
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var columnDefinitions = BuildSqlServerColumns(schema);
        var ddl = $"create table {qualified} ({string.Join(", ", columnDefinitions)});";

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = ddl;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureMySqlSchemaAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, null);
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            await connection.ChangeDatabaseAsync(schemaName!, cancellationToken).ConfigureAwait(false);
        }

        var quotedTable = QuoteMySqlIdentifier(tableName);

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"drop table if exists {quotedTable};";
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var columnDefinitions = BuildMySqlColumns(schema);
        var ddl = $"create table {quotedTable} ({string.Join(", ", columnDefinitions)}) engine=InnoDB;";

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = ddl;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> BuildSqliteColumns(LayerSchemaDefinition schema)
    {
        var columns = new List<string>();
        foreach (var field in schema.Fields)
        {
            var columnType = MapSqliteType(field);
            var builder = new StringBuilder();
            builder.Append(QuoteSqliteIdentifier(field.Name));
            builder.Append(' ');
            builder.Append(columnType);
            if (!field.Nullable)
            {
                builder.Append(" not null");
            }

            if (string.Equals(field.Name, schema.PrimaryKey, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" primary key");
            }

            columns.Add(builder.ToString());
        }

        return columns;
    }

    private static IEnumerable<string> BuildPostgresColumns(LayerSchemaDefinition schema)
    {
        var columns = new List<string>();
        foreach (var field in schema.Fields)
        {
            var builder = new StringBuilder();
            builder.Append(QuotePostgresIdentifier(field.Name));
            builder.Append(' ');
            builder.Append(MapPostgresType(field, schema));
            if (!field.Nullable)
            {
                builder.Append(" NOT NULL");
            }

            columns.Add(builder.ToString());
        }

        columns.Add($"PRIMARY KEY ({QuotePostgresIdentifier(schema.PrimaryKey)})");
        return columns;
    }

    private static IEnumerable<string> BuildSqlServerColumns(LayerSchemaDefinition schema)
    {
        var columns = new List<string>();
        foreach (var field in schema.Fields)
        {
            var builder = new StringBuilder();
            builder.Append(QuoteSqlServerIdentifier(field.Name));
            builder.Append(' ');
            builder.Append(MapSqlServerType(field, schema));
            if (!field.Nullable)
            {
                builder.Append(" NOT NULL");
            }

            columns.Add(builder.ToString());
        }

        columns.Add($"PRIMARY KEY ({QuoteSqlServerIdentifier(schema.PrimaryKey)})");
        return columns;
    }

    private static IEnumerable<string> BuildMySqlColumns(LayerSchemaDefinition schema)
    {
        var columns = new List<string>();
        foreach (var field in schema.Fields)
        {
            var builder = new StringBuilder();
            builder.Append(QuoteMySqlIdentifier(field.Name));
            builder.Append(' ');
            builder.Append(MapMySqlType(field, schema));
            if (!field.Nullable)
            {
                builder.Append(" NOT NULL");
            }

            columns.Add(builder.ToString());
        }

        columns.Add($"PRIMARY KEY ({QuoteMySqlIdentifier(schema.PrimaryKey)})");
        return columns;
    }

    private static string MapSqliteType(LayerFieldSchema field)
    {
        var type = field.StorageType.ToLowerInvariant();
        return type switch
        {
            "integer" => "INTEGER",
            "smallint" => "INTEGER",
            "float" or "double" => "REAL",
            "datetime" => "TEXT",
            "uuid" => "TEXT",
            "blob" => "BLOB",
            "geometry" => "TEXT",
            _ => "TEXT"
        };
    }

    private static string MapPostgresType(LayerFieldSchema field, LayerSchemaDefinition schema)
    {
        var type = field.StorageType.ToLowerInvariant();
        return type switch
        {
            "integer" => "bigint",
            "smallint" => "smallint",
            "float" => "real",
            "double" => "double precision",
            "datetime" => "timestamp with time zone",
            "uuid" => "uuid",
            "blob" => "bytea",
            "geometry" => BuildPostgresGeometryType(schema),
            _ => field.MaxLength.HasValue && field.MaxLength.Value > 0 ? $"varchar({field.MaxLength.Value})" : "text"
        };
    }

    private static string MapSqlServerType(LayerFieldSchema field, LayerSchemaDefinition schema)
    {
        var type = field.StorageType.ToLowerInvariant();
        return type switch
        {
            "integer" => "bigint",
            "smallint" => "smallint",
            "float" => "real",
            "double" => "float",
            "datetime" => "datetimeoffset(7)",
            "uuid" => "uniqueidentifier",
            "blob" => "varbinary(max)",
            "geometry" => "geometry",
            _ => field.MaxLength.HasValue && field.MaxLength.Value > 0 && field.MaxLength.Value <= 4000
                ? $"nvarchar({field.MaxLength.Value})"
                : "nvarchar(max)"
        };
    }

    private static string MapMySqlType(LayerFieldSchema field, LayerSchemaDefinition schema)
    {
        var type = field.StorageType.ToLowerInvariant();
        return type switch
        {
            "integer" => "bigint",
            "smallint" => "smallint",
            "float" => "float",
            "double" => "double",
            "datetime" => "datetime(6)",
            "uuid" => "char(36)",
            "blob" => "longblob",
            "geometry" => "geometry",
            _ => field.MaxLength.HasValue && field.MaxLength.Value > 0 && field.MaxLength.Value <= 65535
                ? $"varchar({field.MaxLength.Value})"
                : "longtext"
        };
    }

    private static string BuildPostgresGeometryType(LayerSchemaDefinition schema)
    {
        if (string.IsNullOrWhiteSpace(schema.GeometryType))
        {
            return "geometry";
        }

        var geometryType = schema.GeometryType.ToUpperInvariant();
        if (schema.Srid.HasValue)
        {
            return $"geometry({geometryType},{schema.Srid.Value})";
        }

        return $"geometry({geometryType})";
    }

    private static (string Schema, string Table) SplitTableName(string tableName, string? defaultSchema)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must be provided.", nameof(tableName));
        }

        var parts = tableName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (defaultSchema ?? string.Empty, parts[0]);
    }

    private static string QuoteSqliteIdentifier(string name)
    {
        return $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string QuotePostgresIdentifier(string name)
    {
        return $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string QuoteSqlServerIdentifier(string name)
    {
        return $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string QuoteMySqlIdentifier(string name)
    {
        return $"`{name.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
