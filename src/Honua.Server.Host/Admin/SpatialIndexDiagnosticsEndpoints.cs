// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin endpoints for spatial index diagnostics.
/// Provides HTTP API for checking spatial index health across all layers.
/// </summary>
public static class SpatialIndexDiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapSpatialIndexDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/diagnostics/spatial-indexes")
            .WithTags("Admin", "Diagnostics")
            .WithOpenApi();

        // Get diagnostics for all layers
        group.MapGet("/", HandleGetDiagnosticsAsync)
            .WithName("GetSpatialIndexDiagnostics")
            .WithSummary("Get spatial index diagnostics for all layers")
            .WithDescription("Analyzes spatial indexes across PostgreSQL, SQL Server, and MySQL databases. Returns index health, statistics, and recommendations.")
            .Produces<SpatialIndexDiagnosticsReport>()
            .Produces(StatusCodes.Status500InternalServerError);

        // Get diagnostics for specific data source
        group.MapGet("/datasource/{dataSourceId}", HandleGetDataSourceDiagnosticsAsync)
            .WithName("GetDataSourceSpatialIndexDiagnostics")
            .WithSummary("Get spatial index diagnostics for a specific data source")
            .Produces<SpatialIndexDiagnosticsReport>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        // Get diagnostics for specific layer
        group.MapGet("/layer/{serviceId}/{layerId}", HandleGetLayerDiagnosticsAsync)
            .WithName("GetLayerSpatialIndexDiagnostics")
            .WithSummary("Get spatial index diagnostics for a specific layer")
            .Produces<SpatialIndexLayerResult>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> HandleGetDiagnosticsAsync(
        IMetadataRegistry metadataRegistry,
        ILogger<SpatialIndexDiagnosticsService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);
            var service = new SpatialIndexDiagnosticsService(logger);

            var report = await service.DiagnoseAllAsync(snapshot, cancellationToken);
            return Results.Ok(report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating spatial index diagnostics");
            return Results.Problem(
                title: "Spatial Index Diagnostics Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleGetDataSourceDiagnosticsAsync(
        string dataSourceId,
        IMetadataRegistry metadataRegistry,
        ILogger<SpatialIndexDiagnosticsService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
                string.Equals(ds.Id, dataSourceId, StringComparison.OrdinalIgnoreCase));

            if (dataSource == null)
            {
                return Results.NotFound(new { Error = $"Data source '{dataSourceId}' not found" });
            }

            var service = new SpatialIndexDiagnosticsService(logger);
            var report = await service.DiagnoseDataSourceAsync(snapshot, dataSource, cancellationToken);

            return Results.Ok(report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating spatial index diagnostics for data source {DataSourceId}", dataSourceId);
            return Results.Problem(
                title: "Spatial Index Diagnostics Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleGetLayerDiagnosticsAsync(
        string serviceId,
        string layerId,
        IMetadataRegistry metadataRegistry,
        ILogger<SpatialIndexDiagnosticsService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken);

            if (!snapshot.TryGetLayer(serviceId, layerId, out var layer))
            {
                return Results.NotFound(new { Error = $"Layer '{layerId}' not found in service '{serviceId}'" });
            }

            if (!snapshot.TryGetService(serviceId, out var service))
            {
                return Results.NotFound(new { Error = $"Service '{serviceId}' not found" });
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds =>
                string.Equals(ds.Id, service.DataSourceId, StringComparison.OrdinalIgnoreCase));

            if (dataSource == null)
            {
                return Results.NotFound(new { Error = $"Data source '{service.DataSourceId}' not found" });
            }

            var diagnosticsService = new SpatialIndexDiagnosticsService(logger);
            var result = await diagnosticsService.DiagnoseLayerAsync(dataSource, layer, cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating spatial index diagnostics for layer {ServiceId}/{LayerId}", serviceId, layerId);
            return Results.Problem(
                title: "Spatial Index Diagnostics Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Service for diagnosing spatial index health across database providers
/// </summary>
public class SpatialIndexDiagnosticsService
{
    private readonly ILogger logger;

    public SpatialIndexDiagnosticsService(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SpatialIndexDiagnosticsReport> DiagnoseAllAsync(
        MetadataSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var report = new SpatialIndexDiagnosticsReport
        {
            GeneratedAt = DateTime.UtcNow
        };

        // Group layers by data source
        var layersByDataSource = snapshot.Layers
            .Join(snapshot.Services,
                layer => layer.ServiceId,
                service => service.Id,
                (layer, service) => new { Layer = layer, DataSourceId = service.DataSourceId })
            .GroupBy(x => x.DataSourceId);

        foreach (var group in layersByDataSource)
        {
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id == group.Key);
            if (dataSource == null)
            {
                this.logger.LogWarning("Data source '{DataSourceId}' not found", group.Key);
                continue;
            }

            try
            {
                var layers = group.Select(x => x.Layer).ToList();
                var results = await DiagnoseDataSourceLayersAsync(dataSource, layers, cancellationToken);
                report.Results.AddRange(results);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error diagnosing data source {DataSourceId}", dataSource.Id);
            }
        }

        // Calculate summary
        report.TotalLayers = report.Results.Count;
        report.LayersWithIndexes = report.Results.Count(r => r.HasSpatialIndex);
        report.LayersWithoutIndexes = report.TotalLayers - report.LayersWithIndexes;
        report.LayersWithIssues = report.Results.Count(r => r.Issues.Count > 0);

        return report;
    }

    public async Task<SpatialIndexDiagnosticsReport> DiagnoseDataSourceAsync(
        MetadataSnapshot snapshot,
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken)
    {
        var report = new SpatialIndexDiagnosticsReport
        {
            GeneratedAt = DateTime.UtcNow
        };

        // Get all layers for this data source
        var layers = snapshot.Layers
            .Join(snapshot.Services,
                layer => layer.ServiceId,
                service => service.Id,
                (layer, service) => new { Layer = layer, DataSourceId = service.DataSourceId })
            .Where(x => string.Equals(x.DataSourceId, dataSource.Id, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Layer)
            .ToList();

        var results = await DiagnoseDataSourceLayersAsync(dataSource, layers, cancellationToken);
        report.Results.AddRange(results);

        report.TotalLayers = report.Results.Count;
        report.LayersWithIndexes = report.Results.Count(r => r.HasSpatialIndex);
        report.LayersWithoutIndexes = report.TotalLayers - report.LayersWithIndexes;
        report.LayersWithIssues = report.Results.Count(r => r.Issues.Count > 0);

        return report;
    }

    public async Task<SpatialIndexLayerResult> DiagnoseLayerAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        CancellationToken cancellationToken)
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
            return CreateUnsupportedProviderResult(dataSource, layer);
        }

        await using var _ = connection.ConfigureAwait(false);

        try
        {
            await connection.OpenAsync(cancellationToken);

            var result = provider switch
            {
                "postgis" => await DiagnosePostgreSqlLayerAsync(connection, layer, cancellationToken),
                "sqlserver" => await DiagnoseSqlServerLayerAsync(connection, layer, cancellationToken),
                "mysql" => await DiagnoseMySqlLayerAsync(connection, layer, cancellationToken),
                _ => CreateUnsupportedProviderResult(dataSource, layer)
            };

            result.DataSourceId = dataSource.Id;
            result.Provider = dataSource.Provider;

            return result;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error diagnosing layer {LayerId}", layer.Id);
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task<List<SpatialIndexLayerResult>> DiagnoseDataSourceLayersAsync(
        DataSourceDefinition dataSource,
        List<LayerDefinition> layers,
        CancellationToken cancellationToken)
    {
        var results = new List<SpatialIndexLayerResult>();

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
            this.logger.LogWarning("Unsupported provider: {Provider}", dataSource.Provider);
            return layers.Select(layer => CreateUnsupportedProviderResult(dataSource, layer)).ToList();
        }

        await using var _ = connection.ConfigureAwait(false);

        try
        {
            await connection.OpenAsync(cancellationToken);

            foreach (var layer in layers)
            {
                try
                {
                    var result = provider switch
                    {
                        "postgis" => await DiagnosePostgreSqlLayerAsync(connection, layer, cancellationToken),
                        "sqlserver" => await DiagnoseSqlServerLayerAsync(connection, layer, cancellationToken),
                        "mysql" => await DiagnoseMySqlLayerAsync(connection, layer, cancellationToken),
                        _ => CreateUnsupportedProviderResult(dataSource, layer)
                    };

                    result.DataSourceId = dataSource.Id;
                    result.Provider = dataSource.Provider;
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error diagnosing layer {LayerId}", layer.Id);
                    results.Add(new SpatialIndexLayerResult
                    {
                        DataSourceId = dataSource.Id,
                        Provider = dataSource.Provider,
                        LayerId = layer.Id,
                        LayerTitle = layer.Title,
                        TableName = layer.Storage?.Table ?? "N/A",
                        GeometryColumn = layer.GeometryField,
                        GeometryType = layer.GeometryType,
                        HasSpatialIndex = false,
                        Issues = new List<string> { $"Error during diagnosis: {ex.Message}" }
                    });
                }
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        return results;
    }

    private async Task<SpatialIndexLayerResult> DiagnosePostgreSqlLayerAsync(
        DbConnection connection,
        LayerDefinition layer,
        CancellationToken cancellationToken)
    {
        var result = new SpatialIndexLayerResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

        var (schema, table) = ParseTableName(layer.Storage?.Table ?? "");

        // Check for GIST index
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

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            result.HasSpatialIndex = true;
            result.IndexName = reader.GetString(0);
            result.IndexType = reader.GetString(1);
            result.IndexSize = reader.GetString(2);
            result.Statistics["num_scans"] = reader.GetInt64(3).ToString();
            result.Statistics["tuples_read"] = reader.GetInt64(4).ToString();

            if (!reader.GetBoolean(5))
            {
                result.Issues.Add("Index is not valid - may need rebuild");
                result.Recommendations.Add($"REINDEX INDEX {result.IndexName};");
            }

            if (!reader.GetBoolean(6))
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

            // Add performance impact estimate
            var estimatedImpact = "10-100x speedup for spatial queries";
            result.Recommendations.Add($"Expected benefit: {estimatedImpact}");
        }

        await reader.CloseAsync();

        // Get table statistics if index exists
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
            await using var statsReader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await statsReader.ReadAsync(cancellationToken))
            {
                result.Statistics["estimated_rows"] = statsReader.GetInt64(0).ToString();
                result.Statistics["table_size"] = statsReader.GetString(1);
            }
        }

        return result;
    }

    private async Task<SpatialIndexLayerResult> DiagnoseSqlServerLayerAsync(
        DbConnection connection,
        LayerDefinition layer,
        CancellationToken cancellationToken)
    {
        var result = new SpatialIndexLayerResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

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

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            result.HasSpatialIndex = true;
            result.IndexName = reader.GetString(0);
            result.IndexType = reader.GetString(1);
            result.IndexSize = $"{reader.GetDouble(2):F2} MB";

            if (!reader.IsDBNull(3))
                result.Statistics["total_reads"] = reader.GetInt64(3).ToString();
            if (!reader.IsDBNull(4))
                result.Statistics["total_writes"] = reader.GetInt64(4).ToString();
            if (!reader.IsDBNull(6))
                result.Statistics["fill_factor"] = reader.GetByte(6).ToString();

            if (reader.GetBoolean(5))
            {
                result.Issues.Add("Spatial index is disabled");
                result.Recommendations.Add($"ALTER INDEX {result.IndexName} ON {schema}.{table} REBUILD;");
            }
        }
        else
        {
            result.HasSpatialIndex = false;
            result.Issues.Add("No spatial index found on geometry column");

            var gridDensity = layer.GeometryType.ToLowerInvariant() switch
            {
                "point" or "multipoint" => "MEDIUM",
                "polygon" or "multipolygon" => "HIGH",
                _ => "MEDIUM"
            };

            result.Recommendations.Add($@"CREATE SPATIAL INDEX {schema}_{table}_{layer.GeometryField}_spatial_idx ON {schema}.{table} ({layer.GeometryField}) USING GEOMETRY_GRID WITH (BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90), GRIDS = (LEVEL_1 = {gridDensity}, LEVEL_2 = {gridDensity}, LEVEL_3 = {gridDensity}, LEVEL_4 = {gridDensity}), CELLS_PER_OBJECT = 16);");
        }

        return result;
    }

    private async Task<SpatialIndexLayerResult> DiagnoseMySqlLayerAsync(
        DbConnection connection,
        LayerDefinition layer,
        CancellationToken cancellationToken)
    {
        var result = new SpatialIndexLayerResult
        {
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType
        };

        var (schema, table) = ParseTableName(layer.Storage?.Table ?? "");

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

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            result.HasSpatialIndex = true;
            result.IndexName = reader.GetString(0);
            result.IndexType = reader.GetString(1);

            if (!reader.IsDBNull(2))
                result.Statistics["cardinality"] = reader.GetInt64(2).ToString();
        }
        else
        {
            result.HasSpatialIndex = false;
            result.Issues.Add("No spatial index found on geometry column");
            result.Recommendations.Add(
                $"ALTER TABLE {schema}.{table} ADD SPATIAL INDEX {schema}_{table}_{layer.GeometryField}_spatial_idx ({layer.GeometryField});");
        }

        return result;
    }

    private static SpatialIndexLayerResult CreateUnsupportedProviderResult(
        DataSourceDefinition dataSource,
        LayerDefinition layer)
    {
        return new SpatialIndexLayerResult
        {
            DataSourceId = dataSource.Id,
            Provider = dataSource.Provider,
            LayerId = layer.Id,
            LayerTitle = layer.Title,
            TableName = layer.Storage?.Table ?? "N/A",
            GeometryColumn = layer.GeometryField,
            GeometryType = layer.GeometryType,
            HasSpatialIndex = false,
            Issues = new List<string> { $"Unsupported provider: {dataSource.Provider}" }
        };
    }

    private static (string Schema, string Table) ParseTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return ("public", "unknown");

        var parts = tableName.Split('.');
        if (parts.Length == 2)
            return (parts[0].Trim('"', '[', ']'), parts[1].Trim('"', '[', ']'));

        return ("public", tableName.Trim('"', '[', ']'));
    }

    private static string DetectProvider(string provider)
    {
        var lower = provider?.ToLowerInvariant() ?? "";

        if (lower.Contains("postgis") || lower.Contains("postgres") || lower.Contains("postgresql"))
            return "postgis";

        if (lower.Contains("sqlserver") || lower.Contains("mssql"))
            return "sqlserver";

        if (lower.Contains("mysql"))
            return "mysql";

        return "unknown";
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

/// <summary>
/// Complete diagnostics report for spatial indexes
/// </summary>
public class SpatialIndexDiagnosticsReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalLayers { get; set; }
    public int LayersWithIndexes { get; set; }
    public int LayersWithoutIndexes { get; set; }
    public int LayersWithIssues { get; set; }
    public List<SpatialIndexLayerResult> Results { get; set; } = new();
}

/// <summary>
/// Diagnostic result for a single layer's spatial index
/// </summary>
public class SpatialIndexLayerResult
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
