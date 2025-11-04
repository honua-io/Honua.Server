// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Creates database indexes for improved query performance on feature layers.
/// </summary>
public sealed class LayerIndexCreator
{
    public async Task CreateIndexesAsync(FeatureContext context, LayerSchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schema);

        var providerKey = context.DataSource.Provider?.Trim().ToLowerInvariant();
        switch (providerKey)
        {
            case SqliteDataStoreProvider.ProviderKey:
                await CreateSqliteIndexesAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case PostgresDataStoreProvider.ProviderKey:
                await CreatePostgresIndexesAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case SqlServerDataStoreProvider.ProviderKey:
                await CreateSqlServerIndexesAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            case MySqlDataStoreProvider.ProviderKey:
                await CreateMySqlIndexesAsync(context.DataSource, schema, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"Index creation for data source provider '{context.DataSource.Provider}' is not supported.");
        }
    }

    private static async Task CreateSqliteIndexesAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var quotedTable = QuoteSqliteIdentifier(schema.TableName);

        // Create spatial index on geometry column if available
        var geometryField = schema.Fields.FirstOrDefault(f => f.StorageType.Equals("geometry", StringComparison.OrdinalIgnoreCase));
        if (geometryField != null)
        {
            var indexName = $"idx_{schema.TableName}_{geometryField.Name}";
            var quotedGeomColumn = QuoteSqliteIdentifier(geometryField.Name);
            var sql = $"SELECT CreateSpatialIndex({QuoteString(schema.TableName)}, {QuoteString(geometryField.Name)});";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // SpatiaLite might not be loaded, fallback to regular index
                cmd.CommandText = $"CREATE INDEX IF NOT EXISTS {QuoteSqliteIdentifier(indexName)} ON {quotedTable}({quotedGeomColumn});";
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Create indexes on commonly filtered fields
        await CreateCommonIndexesAsync(connection, schema, QuoteSqliteIdentifier, "CREATE INDEX IF NOT EXISTS", cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreatePostgresIndexesAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, "public");
        var quotedSchema = QuotePostgresIdentifier(schemaName);
        var quotedTable = QuotePostgresIdentifier(tableName);
        var qualified = $"{quotedSchema}.{quotedTable}";

        // Create spatial index on geometry column using GIST
        var geometryField = schema.Fields.FirstOrDefault(f => f.StorageType.Equals("geometry", StringComparison.OrdinalIgnoreCase));
        if (geometryField != null)
        {
            var indexName = $"idx_{tableName}_{geometryField.Name}";
            var quotedGeomColumn = QuotePostgresIdentifier(geometryField.Name);
            var sql = $"CREATE INDEX IF NOT EXISTS {QuotePostgresIdentifier(indexName)} ON {qualified} USING GIST ({quotedGeomColumn});";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create indexes on commonly filtered fields
        await CreateCommonIndexesAsync(connection, schema, QuotePostgresIdentifier, "CREATE INDEX IF NOT EXISTS", cancellationToken, schemaName, tableName).ConfigureAwait(false);
    }

    private static async Task CreateSqlServerIndexesAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, "dbo");
        var quotedSchema = QuoteSqlServerIdentifier(schemaName);
        var quotedTable = QuoteSqlServerIdentifier(tableName);
        var qualified = $"{quotedSchema}.{quotedTable}";

        // Create spatial index on geometry column
        var geometryField = schema.Fields.FirstOrDefault(f => f.StorageType.Equals("geometry", StringComparison.OrdinalIgnoreCase));
        if (geometryField != null)
        {
            var indexName = $"sidx_{tableName}_{geometryField.Name}";
            var quotedGeomColumn = QuoteSqlServerIdentifier(geometryField.Name);

            // Use bounding box for spatial index
            var sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{schemaName}.{tableName}'))
                BEGIN
                    CREATE SPATIAL INDEX {QuoteSqlServerIdentifier(indexName)} ON {qualified}({quotedGeomColumn})
                    WITH (BOUNDING_BOX = (XMIN = -180, YMIN = -90, XMAX = 180, YMAX = 90));
                END";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Create indexes on commonly filtered fields
        await CreateCommonIndexesAsync(connection, schema, QuoteSqlServerIdentifier, null, cancellationToken, schemaName, tableName).ConfigureAwait(false);
    }

    private static async Task CreateMySqlIndexesAsync(DataSourceDefinition dataSource, LayerSchemaDefinition schema, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var (schemaName, tableName) = SplitTableName(schema.TableName, null);
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            await connection.ChangeDatabaseAsync(schemaName!, cancellationToken).ConfigureAwait(false);
        }

        var quotedTable = QuoteMySqlIdentifier(tableName);

        // Create spatial index on geometry column
        var geometryField = schema.Fields.FirstOrDefault(f => f.StorageType.Equals("geometry", StringComparison.OrdinalIgnoreCase));
        if (geometryField != null)
        {
            var indexName = $"sidx_{tableName}_{geometryField.Name}";
            var quotedGeomColumn = QuoteMySqlIdentifier(geometryField.Name);
            var sql = $"CREATE SPATIAL INDEX {QuoteMySqlIdentifier(indexName)} ON {quotedTable}({quotedGeomColumn});";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Spatial index might fail, ignore
            }
        }

        // Create indexes on commonly filtered fields
        await CreateCommonIndexesAsync(connection, schema, QuoteMySqlIdentifier, "CREATE INDEX", cancellationToken, schemaName, tableName).ConfigureAwait(false);
    }

    private static async Task CreateCommonIndexesAsync<TConnection>(
        TConnection connection,
        LayerSchemaDefinition schema,
        Func<string, string> quoteIdentifier,
        string? createIndexPrefix,
        CancellationToken cancellationToken,
        string? schemaName = null,
        string? tableName = null) where TConnection : System.Data.Common.DbConnection
    {
        var tableRef = tableName ?? schema.TableName;
        var quotedTableRef = schemaName != null
            ? $"{quoteIdentifier(schemaName)}.{quoteIdentifier(tableName!)}"
            : quoteIdentifier(schema.TableName);

        var indexableFields = new List<LayerFieldSchema>();

        // Find temporal/date fields
        foreach (var field in schema.Fields)
        {
            var storageType = field.StorageType.ToLowerInvariant();
            if (storageType is "datetime" or "timestamp" or "date")
            {
                indexableFields.Add(field);
            }
            // Add fields that look like status/category fields (small string fields)
            else if (storageType == "string" && field.MaxLength.HasValue && field.MaxLength.Value <= 100)
            {
                indexableFields.Add(field);
            }
        }

        // Create indexes for indexable fields
        foreach (var field in indexableFields)
        {
            if (field.Name.Equals(schema.PrimaryKey, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip primary key, already indexed
            }

            var indexName = $"idx_{tableRef}_{field.Name}";
            var quotedColumn = quoteIdentifier(field.Name);

            string sql;
            if (connection is SqlConnection)
            {
                // SQL Server uses different syntax
                sql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{schemaName}.{tableName}'))
                    BEGIN
                        CREATE INDEX {quoteIdentifier(indexName)} ON {quotedTableRef}({quotedColumn});
                    END";
            }
            else
            {
                sql = $"{createIndexPrefix} {quoteIdentifier(indexName)} ON {quotedTableRef}({quotedColumn});";
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Index creation might fail if it already exists or for other reasons, continue
            }
        }
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

    private static string QuoteString(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}
