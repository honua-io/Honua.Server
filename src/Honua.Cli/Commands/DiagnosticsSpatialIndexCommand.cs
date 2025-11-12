// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Configuration;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Diagnostic command to verify spatial indexes on all layers.
/// Checks PostgreSQL GIST indexes, SQL Server spatial indexes, and SQLite R*Tree indexes.
/// Provides recommendations for missing indexes and performance statistics.
/// </summary>
public sealed class DiagnosticsSpatialIndexCommand : AsyncCommand<DiagnosticsSpatialIndexCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliConfigStore _configStore;
    private readonly IHttpClientFactory _httpClientFactory;

    public DiagnosticsSpatialIndexCommand(
        IAnsiConsole console,
        IHonuaCliConfigStore configStore,
        IHttpClientFactory httpClientFactory)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]Spatial Index Diagnostics[/]");
        _console.WriteLine();

        try
        {
            // Get metadata from server
            var metadata = await FetchMetadataAsync(settings.ServerUrl, CancellationToken.None);
            if (metadata == null)
            {
                _console.MarkupLine("[red]Failed to fetch metadata from server[/]");
                return 1;
            }

            var allResults = new List<SpatialIndexDiagnosticResult>();

            // Group layers by data source
            var layersByDataSource = metadata.Layers
                .Join(metadata.Services,
                    layer => layer.ServiceId,
                    service => service.Id,
                    (layer, service) => new { Layer = layer, DataSourceId = service.DataSourceId })
                .GroupBy(x => x.DataSourceId);

            foreach (var group in layersByDataSource)
            {
                var dataSource = metadata.DataSources.FirstOrDefault(ds => ds.Id == group.Key);
                if (dataSource == null)
                {
                    _console.MarkupLine($"[yellow]Warning: Data source '{group.Key}' not found[/]");
                    continue;
                }

                _console.MarkupLine($"[bold]Data Source:[/] [cyan]{dataSource.Id}[/] ([dim]{dataSource.Provider}[/])");
                _console.WriteLine();

                var results = await DiagnoseDataSourceAsync(dataSource, group.Select(x => x.Layer).ToList());
                allResults.AddRange(results);

                DisplayResults(results, settings.Verbose);
                _console.WriteLine();
            }

            // Output JSON if requested
            if (!string.IsNullOrWhiteSpace(settings.OutputJson))
            {
                await OutputJsonReportAsync(allResults, settings.OutputJson);
                _console.MarkupLine($"[green]JSON report written to: {settings.OutputJson}[/]");
            }

            // Summary
            var totalLayers = allResults.Count;
            var layersWithIndexes = allResults.Count(r => r.HasSpatialIndex);
            var layersWithoutIndexes = totalLayers - layersWithIndexes;
            var layersWithIssues = allResults.Count(r => r.Issues.Any());

            _console.WriteLine();
            _console.MarkupLine("[bold underline]Summary[/]");
            _console.MarkupLine($"Total Layers: [cyan]{totalLayers}[/]");
            _console.MarkupLine($"With Spatial Indexes: [green]{layersWithIndexes}[/]");
            _console.MarkupLine($"Missing Spatial Indexes: [red]{layersWithoutIndexes}[/]");
            _console.MarkupLine($"Layers with Issues: [yellow]{layersWithIssues}[/]");

            return layersWithoutIndexes > 0 || layersWithIssues > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    private async Task<ServerMetadata?> FetchMetadataAsync(string? serverUrl, CancellationToken cancellationToken)
    {
        var url = serverUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var config = await _configStore.LoadAsync(cancellationToken);
                url = config.Host;
            }
            catch
            {
                // Ignore if no config exists
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            _console.MarkupLine("[yellow]No server URL configured. Use --server or run 'honua config init'[/]");
            return null;
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("DiagnosticsSpatialIndex");
            var response = await httpClient.GetAsync($"{url.TrimEnd('/')}/admin/metadata", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _console.MarkupLine($"[red]Failed to fetch metadata: HTTP {(int)response.StatusCode}[/]");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var metadata = JsonSerializer.Deserialize<ServerMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return metadata;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error fetching metadata: {ex.Message}[/]");
            return null;
        }
    }

    private async Task<List<SpatialIndexDiagnosticResult>> DiagnoseDataSourceAsync(
        DataSourceInfo dataSource,
        List<LayerInfo> layers)
    {
        var results = new List<SpatialIndexDiagnosticResult>();

        try
        {
            var provider = DetectProvider(dataSource.Provider);
            DbConnection? connection = provider switch
            {
                "postgis" => new NpgsqlConnection(dataSource.ConnectionString),
                "sqlserver" => new SqlConnection(dataSource.ConnectionString),
                "mysql" => new MySqlConnection(dataSource.ConnectionString),
                _ => null
            };

            if (connection == null)
            {
                _console.MarkupLine($"[yellow]Unsupported provider: {dataSource.Provider}[/]");
                return results;
            }

            await using var _ = connection.ConfigureAwait(false);
            await connection.OpenAsync();

            foreach (var layer in layers)
            {
                var result = provider switch
                {
                    "postgis" => await DiagnosePostgreSqlLayerAsync(connection, layer),
                    "sqlserver" => await DiagnoseSqlServerLayerAsync(connection, layer),
                    "mysql" => await DiagnoseMySqlLayerAsync(connection, layer),
                    _ => CreateUnknownProviderResult(layer, dataSource.Provider)
                };

                result.DataSourceId = dataSource.Id;
                result.Provider = dataSource.Provider;
                results.Add(result);
            }

            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error diagnosing data source '{dataSource.Id}': {ex.Message}[/]");
        }

        return results;
    }

    private async Task<SpatialIndexDiagnosticResult> DiagnosePostgreSqlLayerAsync(
        DbConnection connection,
        LayerInfo layer)
    {
        var result = new SpatialIndexDiagnosticResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

        try
        {
            // Parse schema and table name
            var (schema, table) = ParseTableName(layer.Storage?.Table ?? "");

            // Check for GIST index on geometry column
            var indexCheckSql = @"
                SELECT
                    i.relname AS index_name,
                    am.amname AS index_type,
                    pg_size_pretty(pg_relation_size(i.oid)) AS index_size,
                    pg_stat_get_numscans(i.oid) AS num_scans,
                    pg_stat_get_tuples_returned(i.oid) AS tuples_read,
                    idx.indisvalid AS is_valid,
                    idx.indisready AS is_ready
                FROM pg_index idx
                JOIN pg_class i ON i.oid = idx.indexrelid
                JOIN pg_class t ON t.oid = idx.indrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                JOIN pg_am am ON am.oid = i.relam
                WHERE n.nspname = @schema
                  AND t.relname = @table
                  AND am.amname = 'gist'
                  AND EXISTS (
                      SELECT 1 FROM pg_attribute a
                      WHERE a.attrelid = t.oid
                        AND a.attname = @geometryColumn
                        AND a.attnum = ANY(idx.indkey)
                  )";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = indexCheckSql;
            AddParameter(cmd, "@schema", schema);
            AddParameter(cmd, "@table", table);
            AddParameter(cmd, "@geometryColumn", layer.GeometryField);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.HasSpatialIndex = true;
                result.IndexName = reader.GetString(0);
                result.IndexType = reader.GetString(1);
                result.IndexSize = reader.GetString(2);
                result.Statistics["num_scans"] = reader.GetInt64(3).ToString();
                result.Statistics["tuples_read"] = reader.GetInt64(4).ToString();

                var isValid = reader.GetBoolean(5);
                var isReady = reader.GetBoolean(6);

                if (!isValid)
                {
                    result.Issues.Add("Index is not valid - may need rebuild");
                    result.Recommendations.Add($"REINDEX INDEX {result.IndexName};");
                }

                if (!isReady)
                {
                    result.Issues.Add("Index is not ready - may be building");
                }
            }
            else
            {
                result.HasSpatialIndex = false;
                result.Issues.Add("No GIST spatial index found on geometry column");
                result.Recommendations.Add(
                    $"CREATE INDEX {schema}_{table}_{layer.GeometryField}_gist_idx ON {schema}.{table} USING GIST ({layer.GeometryField});");
            }

            await reader.CloseAsync();

            // Get table statistics
            if (result.HasSpatialIndex)
            {
                var statsSql = @"
                    SELECT
                        reltuples::bigint AS estimated_rows,
                        pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = @schema AND c.relname = @table";

                cmd.CommandText = statsSql;
                await using var statsReader = await cmd.ExecuteReaderAsync();
                if (await statsReader.ReadAsync())
                {
                    result.Statistics["estimated_rows"] = statsReader.GetInt64(0).ToString();
                    result.Statistics["table_size"] = statsReader.GetString(1);
                }
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Error during diagnosis: {ex.Message}");
        }

        return result;
    }

    private async Task<SpatialIndexDiagnosticResult> DiagnoseSqlServerLayerAsync(
        DbConnection connection,
        LayerInfo layer)
    {
        var result = new SpatialIndexDiagnosticResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

        try
        {
            // Parse schema and table name
            var (schema, table) = ParseTableName(layer.Storage?.Table ?? "");

            // Check for spatial index
            var indexCheckSql = @"
                SELECT
                    i.name AS index_name,
                    i.type_desc AS index_type,
                    ps.used_page_count * 8 / 1024.0 AS index_size_mb,
                    ius.user_seeks + ius.user_scans + ius.user_lookups AS total_reads,
                    ius.user_updates AS total_writes,
                    i.is_disabled,
                    i.fill_factor
                FROM sys.indexes i
                JOIN sys.objects o ON i.object_id = o.object_id
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                LEFT JOIN sys.dm_db_index_usage_stats ius ON i.object_id = ius.object_id AND i.index_id = ius.index_id AND ius.database_id = DB_ID()
                WHERE s.name = @schema
                  AND o.name = @table
                  AND c.name = @geometryColumn
                  AND i.type_desc = 'SPATIAL'";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = indexCheckSql;
            AddParameter(cmd, "@schema", schema);
            AddParameter(cmd, "@table", table);
            AddParameter(cmd, "@geometryColumn", layer.GeometryField);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.HasSpatialIndex = true;
                result.IndexName = reader.GetString(0);
                result.IndexType = reader.GetString(1);
                result.IndexSize = $"{reader.GetDouble(2):F2} MB";

                if (!reader.IsDBNull(3))
                {
                    result.Statistics["total_reads"] = reader.GetInt64(3).ToString();
                }
                if (!reader.IsDBNull(4))
                {
                    result.Statistics["total_writes"] = reader.GetInt64(4).ToString();
                }
                if (!reader.IsDBNull(6))
                {
                    result.Statistics["fill_factor"] = reader.GetByte(6).ToString();
                }

                var isDisabled = reader.GetBoolean(5);
                if (isDisabled)
                {
                    result.Issues.Add("Spatial index is disabled");
                    result.Recommendations.Add($"ALTER INDEX {result.IndexName} ON {schema}.{table} REBUILD;");
                }
            }
            else
            {
                result.HasSpatialIndex = false;
                result.Issues.Add("No spatial index found on geometry column");

                // Determine grid density based on geometry type
                var gridDensity = layer.GeometryType.ToLowerInvariant() switch
                {
                    "point" or "multipoint" => "MEDIUM",
                    "polygon" or "multipolygon" => "HIGH",
                    _ => "MEDIUM"
                };

                result.Recommendations.Add($@"
CREATE SPATIAL INDEX {schema}_{table}_{layer.GeometryField}_spatial_idx
ON {schema}.{table} ({layer.GeometryField})
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = {gridDensity}, LEVEL_2 = {gridDensity}, LEVEL_3 = {gridDensity}, LEVEL_4 = {gridDensity}),
    CELLS_PER_OBJECT = 16
);");
            }

            await reader.CloseAsync();

            // Get table statistics
            if (result.HasSpatialIndex)
            {
                var statsSql = @"
                    SELECT
                        SUM(p.rows) AS row_count,
                        SUM(a.total_pages) * 8 / 1024.0 AS total_size_mb
                    FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    JOIN sys.partitions p ON t.object_id = p.object_id
                    JOIN sys.allocation_units a ON p.partition_id = a.container_id
                    WHERE s.name = @schema AND t.name = @table
                    GROUP BY t.object_id";

                cmd.CommandText = statsSql;
                await using var statsReader = await cmd.ExecuteReaderAsync();
                if (await statsReader.ReadAsync())
                {
                    result.Statistics["row_count"] = reader.GetInt64(0).ToString();
                    result.Statistics["table_size"] = $"{reader.GetDouble(1):F2} MB";
                }
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Error during diagnosis: {ex.Message}");
        }

        return result;
    }

    private async Task<SpatialIndexDiagnosticResult> DiagnoseMySqlLayerAsync(
        DbConnection connection,
        LayerInfo layer)
    {
        var result = new SpatialIndexDiagnosticResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

        try
        {
            var (schema, table) = ParseTableName(layer.Storage?.Table ?? "");

            // Check for spatial index (R-Tree for MySQL/MariaDB)
            var indexCheckSql = @"
                SELECT
                    INDEX_NAME,
                    INDEX_TYPE,
                    CARDINALITY
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = @schema
                  AND TABLE_NAME = @table
                  AND COLUMN_NAME = @geometryColumn
                  AND INDEX_TYPE = 'SPATIAL'";

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = indexCheckSql;
            AddParameter(cmd, "@schema", schema);
            AddParameter(cmd, "@table", table);
            AddParameter(cmd, "@geometryColumn", layer.GeometryField);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.HasSpatialIndex = true;
                result.IndexName = reader.GetString(0);
                result.IndexType = reader.GetString(1);

                if (!reader.IsDBNull(2))
                {
                    result.Statistics["cardinality"] = reader.GetInt64(2).ToString();
                }
            }
            else
            {
                result.HasSpatialIndex = false;
                result.Issues.Add("No spatial index found on geometry column");
                result.Recommendations.Add(
                    $"ALTER TABLE {schema}.{table} ADD SPATIAL INDEX {schema}_{table}_{layer.GeometryField}_spatial_idx ({layer.GeometryField});");
            }

            await reader.CloseAsync();

            // Get table statistics
            if (result.HasSpatialIndex)
            {
                var statsSql = @"
                    SELECT
                        TABLE_ROWS,
                        ROUND((DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) AS size_mb
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";

                cmd.CommandText = statsSql;
                await using var statsReader = await cmd.ExecuteReaderAsync();
                if (await statsReader.ReadAsync())
                {
                    if (!statsReader.IsDBNull(0))
                    {
                        result.Statistics["estimated_rows"] = statsReader.GetInt64(0).ToString();
                    }
                    if (!statsReader.IsDBNull(1))
                    {
                        result.Statistics["table_size"] = $"{statsReader.GetDouble(1):F2} MB";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Error during diagnosis: {ex.Message}");
        }

        return result;
    }

    private static SpatialIndexDiagnosticResult CreateUnknownProviderResult(LayerInfo layer, string provider)
    {
        return new SpatialIndexDiagnosticResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType,
            HasSpatialIndex = false,
            Issues = new List<string> { $"Unsupported provider: {provider}" }
        };
    }

    private void DisplayResults(List<SpatialIndexDiagnosticResult> results, bool verbose)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);

        if (verbose)
        {
            table.AddColumn("Layer");
            table.AddColumn("Table");
            table.AddColumn("Geometry Column");
            table.AddColumn("Index Status");
            table.AddColumn("Index Name");
            table.AddColumn("Index Size");
            table.AddColumn("Issues");

            foreach (var result in results)
            {
                var statusMarkup = result.HasSpatialIndex ? "[green]✓ Indexed[/]" : "[red]✗ Missing[/]";
                var issuesMarkup = result.Issues.Any()
                    ? $"[yellow]{result.Issues.Count} issue(s)[/]"
                    : "[dim]None[/]";

                table.AddRow(
                    result.LayerTitle,
                    result.TableName,
                    result.GeometryColumn,
                    statusMarkup,
                    result.IndexName ?? "[dim]N/A[/]",
                    result.IndexSize ?? "[dim]N/A[/]",
                    issuesMarkup
                );
            }
        }
        else
        {
            table.AddColumn("Layer");
            table.AddColumn("Index Status");
            table.AddColumn("Issues");

            foreach (var result in results)
            {
                var statusMarkup = result.HasSpatialIndex ? "[green]✓[/]" : "[red]✗[/]";
                var issuesMarkup = result.Issues.Any()
                    ? $"[yellow]{string.Join("; ", result.Issues)}[/]"
                    : "[dim]None[/]";

                table.AddRow(
                    result.LayerTitle,
                    statusMarkup,
                    issuesMarkup
                );
            }
        }

        _console.Write(table);

        // Show recommendations for layers without indexes or with issues
        var layersNeedingAttention = results.Where(r => !r.HasSpatialIndex || r.Issues.Any()).ToList();
        if (layersNeedingAttention.Any())
        {
            _console.WriteLine();
            _console.MarkupLine("[bold yellow]Recommendations:[/]");

            foreach (var result in layersNeedingAttention)
            {
                _console.MarkupLine($"[cyan]{result.LayerTitle}[/] ([dim]{result.LayerId}[/]):");

                foreach (var recommendation in result.Recommendations)
                {
                    _console.MarkupLine($"  [dim]{recommendation.EscapeMarkup()}[/]");
                }

                _console.WriteLine();
            }
        }
    }

    private async Task OutputJsonReportAsync(List<SpatialIndexDiagnosticResult> results, string outputPath)
    {
        var report = new
        {
            GeneratedAt = DateTime.UtcNow,
            TotalLayers = results.Count,
            LayersWithIndexes = results.Count(r => r.HasSpatialIndex),
            LayersWithoutIndexes = results.Count(r => !r.HasSpatialIndex),
            LayersWithIssues = results.Count(r => r.Issues.Any()),
            Results = results
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        await System.IO.File.WriteAllTextAsync(outputPath, json);
    }

    private static (string Schema, string Table) ParseTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return ("public", "unknown");
        }

        var parts = tableName.Split('.');
        if (parts.Length == 2)
        {
            return (parts[0].Trim('"', '[', ']'), parts[1].Trim('"', '[', ']'));
        }

        return ("public", tableName.Trim('"', '[', ']'));
    }

    private static string DetectProvider(string provider)
    {
        var lower = provider?.ToLowerInvariant() ?? "";

        if (lower.Contains("postgis") || lower.Contains("postgres") || lower.Contains("postgresql"))
        {
            return "postgis";
        }

        if (lower.Contains("sqlserver") || lower.Contains("mssql"))
        {
            return "sqlserver";
        }

        if (lower.Contains("mysql"))
        {
            return "mysql";
        }

        return "unknown";
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--server <URL>")]
        [Description("Honua server URL (defaults to configured host)")]
        public string? ServerUrl { get; init; }

        [CommandOption("--output-json <PATH>")]
        [Description("Output diagnostic results to JSON file")]
        public string? OutputJson { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed information including statistics")]
        public bool Verbose { get; init; }
    }
}

/// <summary>
/// Diagnostic result for a single layer's spatial index
/// </summary>
public class SpatialIndexDiagnosticResult
{
    public string DataSourceId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string LayerId { get; set; } = "";
    public string LayerTitle { get; set; } = "";
    public string TableName { get; set; } = "";
    public string GeometryColumn { get; set; } = "";
    public string GeometryType { get; set; } = "";
    public bool HasSpatialIndex { get; set; }
    public string? IndexName { get; set; }
    public string? IndexType { get; set; }
    public string? IndexSize { get; set; }
    public Dictionary<string, string> Statistics { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Simplified metadata structure for API response
/// </summary>
public class ServerMetadata
{
    public List<DataSourceInfo> DataSources { get; set; } = new();
    public List<ServiceInfo> Services { get; set; } = new();
    public List<LayerInfo> Layers { get; set; } = new();
}

public class DataSourceInfo
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}

public class ServiceInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string DataSourceId { get; set; } = "";
}

public class LayerInfo
{
    public string Id { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string GeometryType { get; set; } = "";
    public string GeometryField { get; set; } = "";
    public LayerStorageInfo? Storage { get; set; }
}

public class LayerStorageInfo
{
    public string? Table { get; set; }
}
