// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Metadata;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Core.Discovery;

/// <summary>
/// Discovers PostGIS tables and generates metadata automatically.
/// Enables zero-configuration deployment where you just point at a database
/// and all spatial tables are instantly available via OData and OGC APIs.
/// </summary>
public sealed class PostGisTableDiscoveryService : ITableDiscoveryService
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly PostgresSchemaDiscoveryService _schemaDiscovery;
    private readonly AutoDiscoveryOptions _options;
    private readonly ILogger<PostGisTableDiscoveryService> _logger;

    public PostGisTableDiscoveryService(
        IMetadataRegistry metadataRegistry,
        PostgresSchemaDiscoveryService schemaDiscovery,
        IOptions<AutoDiscoveryOptions> options,
        ILogger<PostGisTableDiscoveryService> logger)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _schemaDiscovery = schemaDiscovery ?? throw new ArgumentNullException(nameof(schemaDiscovery));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<DiscoveredTable>> DiscoverTablesAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Auto-discovery is disabled");
            return Array.Empty<DiscoveredTable>();
        }

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken);

        if (!snapshot.TryGetDataSource(dataSourceId, out var dataSource))
        {
            _logger.LogWarning("Data source {DataSourceId} not found for discovery", dataSourceId);
            return Array.Empty<DiscoveredTable>();
        }

        if (!string.Equals(dataSource.Provider, "postgis", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Data source {DataSourceId} is not a PostGIS provider", dataSourceId);
            return Array.Empty<DiscoveredTable>();
        }

        _logger.LogInformation("Starting table discovery for data source {DataSourceId}", dataSourceId);

        await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Discover all geometry tables
        var geometryTables = await DiscoverGeometryTablesAsync(connection, cancellationToken);

        _logger.LogInformation("Found {Count} geometry tables", geometryTables.Count);

        var discoveredTables = new List<DiscoveredTable>();

        foreach (var (schema, table, geomColumn, geomType, srid) in geometryTables)
        {
            if (ShouldExcludeTable(schema, table))
            {
                _logger.LogDebug("Excluding table {Schema}.{Table}", schema, table);
                continue;
            }

            try
            {
                var discoveredTable = await DiscoverTableDetailsAsync(
                    connection,
                    dataSource,
                    schema,
                    table,
                    geomColumn,
                    geomType,
                    srid,
                    cancellationToken);

                if (discoveredTable != null)
                {
                    // Check spatial index requirement
                    if (_options.RequireSpatialIndex && !discoveredTable.HasSpatialIndex)
                    {
                        _logger.LogDebug(
                            "Skipping table {Schema}.{Table} - no spatial index",
                            schema, table);
                        continue;
                    }

                    discoveredTables.Add(discoveredTable);

                    // Check max tables limit
                    if (_options.MaxTables > 0 && discoveredTables.Count >= _options.MaxTables)
                    {
                        _logger.LogWarning(
                            "Reached maximum table discovery limit of {MaxTables}",
                            _options.MaxTables);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error discovering table {Schema}.{Table}",
                    schema, table);
            }
        }

        _logger.LogInformation(
            "Discovery complete. Found {Count} tables",
            discoveredTables.Count);

        return discoveredTables;
    }

    public async Task<DiscoveredTable?> DiscoverTableAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken);

        if (!snapshot.TryGetDataSource(dataSourceId, out var dataSource))
        {
            return null;
        }

        var (schema, table) = ParseTableName(tableName);

        await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Get geometry info for this specific table
        var geometryInfo = await GetGeometryInfoAsync(connection, schema, table, cancellationToken);

        if (geometryInfo == null)
        {
            _logger.LogDebug("Table {Schema}.{Table} has no geometry column", schema, table);
            return null;
        }

        return await DiscoverTableDetailsAsync(
            connection,
            dataSource,
            schema,
            table,
            geometryInfo.Value.Column,
            geometryInfo.Value.Type,
            geometryInfo.Value.Srid,
            cancellationToken);
    }

    private async Task<List<(string Schema, string Table, string GeomColumn, string GeomType, int Srid)>>
        DiscoverGeometryTablesAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT
                f_table_schema,
                f_table_name,
                f_geometry_column,
                type,
                srid
            FROM geometry_columns
            WHERE f_table_schema NOT IN ('pg_catalog', 'information_schema', 'public_topology')
            ORDER BY f_table_schema, f_table_name";

        var tables = new List<(string, string, string, string, int)>();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var geomColumn = reader.GetString(2);
            var geomType = reader.GetString(3);
            var srid = reader.GetInt32(4);

            tables.Add((schema, table, geomColumn, geomType, srid));
        }

        return tables;
    }

    private async Task<(string Column, string Type, int Srid)?> GetGeometryInfoAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT
                f_geometry_column,
                type,
                srid
            FROM geometry_columns
            WHERE f_table_schema = @schema
              AND f_table_name = @table
            LIMIT 1";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return (
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2)
            );
        }

        return null;
    }

    private async Task<DiscoveredTable?> DiscoverTableDetailsAsync(
        NpgsqlConnection connection,
        DataSourceDefinition dataSource,
        string schema,
        string table,
        string geometryColumn,
        string geometryType,
        int srid,
        CancellationToken cancellationToken)
    {
        // Use existing schema discovery service
        var tableSchema = await _schemaDiscovery.DiscoverTableSchemaAsync(
            dataSource,
            $"{schema}.{table}",
            cancellationToken);

        if (tableSchema.PrimaryKey == null)
        {
            _logger.LogWarning(
                "Table {Schema}.{Table} has no primary key - skipping",
                schema, table);
            return null;
        }

        // Build column dictionary
        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in tableSchema.Columns)
        {
            // Skip geometry column
            if (string.Equals(col.Name, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            columns[col.Name] = new ColumnInfo
            {
                Name = col.Name,
                DataType = col.SuggestedDataType ?? "string",
                StorageType = col.SuggestedStorageType ?? col.DbType,
                IsNullable = col.IsNullable,
                IsPrimaryKey = col.IsPrimaryKey,
                Alias = _options.UseFriendlyNames ? col.Name.Humanize(LetterCasing.Title) : null
            };
        }

        // Check for spatial index
        var hasSpatialIndex = await CheckSpatialIndexAsync(
            connection, schema, table, geometryColumn, cancellationToken);

        // Get row count estimate
        var rowCount = await GetEstimatedRowCountAsync(
            connection, schema, table, cancellationToken);

        // Get extent if configured
        Envelope? extent = null;
        if (_options.ComputeExtentOnDiscovery)
        {
            extent = await ComputeExtentAsync(
                connection, schema, table, geometryColumn, cancellationToken);
        }

        // Get table description/comment
        var description = await GetTableDescriptionAsync(
            connection, schema, table, cancellationToken);

        return new DiscoveredTable
        {
            Schema = schema,
            TableName = table,
            GeometryColumn = geometryColumn,
            SRID = srid,
            GeometryType = geometryType,
            PrimaryKeyColumn = tableSchema.PrimaryKey,
            Columns = columns,
            HasSpatialIndex = hasSpatialIndex,
            EstimatedRowCount = rowCount,
            Extent = extent,
            Description = description
        };
    }

    private async Task<bool> CheckSpatialIndexAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        string geometryColumn,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = @schema
              AND tablename = @table
              AND indexdef LIKE '%USING gist%'
              AND indexdef LIKE '%' || @geomColumn || '%'";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("geomColumn", geometryColumn);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task<long> GetEstimatedRowCountAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT reltuples::bigint
            FROM pg_class
            WHERE oid = (@schema || '.' || @table)::regclass";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<Envelope?> ComputeExtentAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        string geometryColumn,
        CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT
                ST_XMin(extent) as minx,
                ST_YMin(extent) as miny,
                ST_XMax(extent) as maxx,
                ST_YMax(extent) as maxy
            FROM (
                SELECT ST_Extent(""{geometryColumn}"") as extent
                FROM ""{schema}"".""{table}""
            ) sub";

        await using var command = new NpgsqlCommand(sql, connection);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
            {
                return new Envelope
                {
                    MinX = reader.GetDouble(0),
                    MinY = reader.GetDouble(1),
                    MaxX = reader.GetDouble(2),
                    MaxY = reader.GetDouble(3)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute extent for {Schema}.{Table}", schema, table);
        }

        return null;
    }

    private async Task<string?> GetTableDescriptionAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT obj_description((@schema || '.' || @table)::regclass, 'pg_class')";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result as string;
        }
        catch
        {
            return null;
        }
    }

    private bool ShouldExcludeTable(string schema, string table)
    {
        // Always exclude system schemas
        if (schema.Equals("pg_catalog", StringComparison.OrdinalIgnoreCase) ||
            schema.Equals("information_schema", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check configured excluded schemas
        if (_options.ExcludeSchemas?.Any(s =>
            schema.Equals(s, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        // Check table name patterns
        if (_options.ExcludeTablePatterns != null)
        {
            foreach (var pattern in _options.ExcludeTablePatterns)
            {
                if (MatchesPattern(table, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesPattern(string text, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
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
}

/// <summary>
/// Extension methods for MetadataSnapshot to support discovery.
/// </summary>
internal static class MetadataSnapshotExtensions
{
    public static bool TryGetDataSource(this MetadataSnapshot snapshot, string dataSourceId, out DataSourceDefinition dataSource)
    {
        dataSource = snapshot.DataSources.FirstOrDefault(ds =>
            ds.Id.Equals(dataSourceId, StringComparison.OrdinalIgnoreCase))!;
        return dataSource != null;
    }
}
