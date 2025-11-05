// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Data source node that reads features from a PostGIS table or query
/// </summary>
public class PostGisDataSourceNode : WorkflowNodeBase
{
    private readonly string _connectionString;

    public PostGisDataSourceNode(string connectionString, ILogger<PostGisDataSourceNode> logger)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override string NodeType => "data_source.postgis";
    public override string DisplayName => "PostGIS Data Source";
    public override string Description => "Reads features from a PostGIS database table or query";

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var result = new NodeValidationResult { IsValid = true };

        // Check required parameters
        if (!nodeDefinition.Parameters.ContainsKey("table") && !nodeDefinition.Parameters.ContainsKey("query"))
        {
            result.IsValid = false;
            result.Errors.Add("Either 'table' or 'query' parameter is required");
        }

        if (!nodeDefinition.Parameters.ContainsKey("geometry_column"))
        {
            result.Warnings.Add("'geometry_column' not specified, will use first geometry column found");
        }

        return Task.FromResult(result);
    }

    public override async Task<NodeEstimate> EstimateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get approximate row count
            var table = GetOptionalParameter<string>(nodeDefinition.Parameters, "table", null);
            var query = GetOptionalParameter<string>(nodeDefinition.Parameters, "query", null);
            var filter = GetOptionalParameter<string>(nodeDefinition.Parameters, "filter", null);

            long estimatedRows = 1000; // Default estimate

            if (!string.IsNullOrEmpty(table))
            {
                var countQuery = filter != null
                    ? $"SELECT COUNT(*) FROM {table} WHERE {filter}"
                    : $"SELECT reltuples::bigint FROM pg_class WHERE relname = @table";

                var param = filter != null ? new { } : new { table };
                estimatedRows = await connection.ExecuteScalarAsync<long>(countQuery, param);
            }

            return new NodeEstimate
            {
                EstimatedDurationSeconds = Math.Max(1, estimatedRows / 1000), // ~1000 rows/sec
                EstimatedMemoryMB = Math.Max(256, estimatedRows / 100), // ~100 rows per MB
                EstimatedFeatures = estimatedRows
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error estimating PostGIS data source");
            return await base.EstimateAsync(nodeDefinition, runtimeParameters, cancellationToken);
        }
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Executing PostGIS data source node {NodeId}", context.NodeDefinition.Id);

            ReportProgress(context, 0, "Connecting to PostGIS database");

            var table = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "table", null);
            var query = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "query", null);
            var filter = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "filter", null);
            var geometryColumn = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "geometry_column", "geometry");
            var limit = GetOptionalParameter<int?>(context.NodeDefinition.Parameters, "limit", null);

            // Build query
            string sql;
            if (!string.IsNullOrEmpty(query))
            {
                sql = query;
            }
            else if (!string.IsNullOrEmpty(table))
            {
                sql = $"SELECT *, ST_AsGeoJSON({geometryColumn}) as geojson FROM {table}";
                if (!string.IsNullOrEmpty(filter))
                {
                    sql += $" WHERE {filter}";
                }
                if (limit.HasValue)
                {
                    sql += $" LIMIT {limit.Value}";
                }
            }
            else
            {
                return NodeExecutionResult.Fail("Either 'table' or 'query' parameter is required");
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            ReportProgress(context, 10, "Executing query");

            var features = new List<Dictionary<string, object>>();
            var reader = await connection.ExecuteReaderAsync(sql);

            int count = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var feature = new Dictionary<string, object>();

                // Read all columns
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var value = reader.GetValue(i);

                    if (value != DBNull.Value)
                    {
                        feature[name] = value;
                    }
                }

                features.Add(feature);
                count++;

                if (count % 100 == 0)
                {
                    ReportProgress(context, 10 + (count * 80 / Math.Max(1, limit ?? 10000)),
                        $"Read {count} features", count);
                }
            }

            await reader.CloseAsync();

            stopwatch.Stop();

            Logger.LogInformation(
                "PostGIS data source node {NodeId} read {Count} features in {DurationMs}ms",
                context.NodeDefinition.Id,
                count,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(context, 100, $"Read {count} features complete");

            return new NodeExecutionResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["features"] = features,
                    ["count"] = count,
                    ["source"] = table ?? "query"
                },
                FeaturesProcessed = count,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "PostGIS data source node {NodeId} failed", context.NodeDefinition.Id);
            return NodeExecutionResult.Fail($"PostGIS query failed: {ex.Message}", ex.StackTrace);
        }
    }
}

/// <summary>
/// Data source node that accepts inline GeoJSON features or file content
/// </summary>
public class FileDataSourceNode : WorkflowNodeBase
{
    public FileDataSourceNode(ILogger<FileDataSourceNode> logger) : base(logger)
    {
    }

    public override string NodeType => "data_source.file";
    public override string DisplayName => "File Data Source";
    public override string Description => "Reads features from uploaded GeoJSON, GeoPackage, or Shapefile";

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var result = new NodeValidationResult { IsValid = true };

        if (!nodeDefinition.Parameters.ContainsKey("content") && !nodeDefinition.Parameters.ContainsKey("url"))
        {
            result.IsValid = false;
            result.Errors.Add("Either 'content' or 'url' parameter is required");
        }

        return Task.FromResult(result);
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Executing file data source node {NodeId}", context.NodeDefinition.Id);

            ReportProgress(context, 0, "Reading file content");

            var content = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "content", null);
            var url = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "url", null);
            var format = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "format", "geojson");

            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(url))
            {
                return NodeExecutionResult.Fail("Either 'content' or 'url' parameter is required");
            }

            // For now, only support inline GeoJSON content
            // TODO: Add support for URL download and other formats (Shapefile, GeoPackage)
            if (format.ToLowerInvariant() != "geojson")
            {
                return NodeExecutionResult.Fail($"Format '{format}' not yet supported. Currently only 'geojson' is supported.");
            }

            ReportProgress(context, 20, "Parsing GeoJSON");

            var geojson = content ?? throw new InvalidOperationException("Content is required");
            var doc = JsonDocument.Parse(geojson);
            var root = doc.RootElement;

            var features = new List<Dictionary<string, object>>();

            if (root.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();

                if (type == "FeatureCollection")
                {
                    if (root.TryGetProperty("features", out var featuresArray))
                    {
                        foreach (var feature in featuresArray.EnumerateArray())
                        {
                            features.Add(ParseFeature(feature));
                        }
                    }
                }
                else if (type == "Feature")
                {
                    features.Add(ParseFeature(root));
                }
            }

            stopwatch.Stop();

            Logger.LogInformation(
                "File data source node {NodeId} parsed {Count} features in {DurationMs}ms",
                context.NodeDefinition.Id,
                features.Count,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(context, 100, $"Parsed {features.Count} features");

            return new NodeExecutionResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["features"] = features,
                    ["count"] = features.Count,
                    ["format"] = format
                },
                FeaturesProcessed = features.Count,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "File data source node {NodeId} failed", context.NodeDefinition.Id);
            return NodeExecutionResult.Fail($"File parsing failed: {ex.Message}", ex.StackTrace);
        }
    }

    private Dictionary<string, object> ParseFeature(JsonElement feature)
    {
        var result = new Dictionary<string, object>();

        // Parse properties
        if (feature.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                result[prop.Name] = JsonElementToObject(prop.Value);
            }
        }

        // Store geometry as GeoJSON string
        if (feature.TryGetProperty("geometry", out var geometry))
        {
            result["geometry"] = geometry.GetRawText();
        }

        // Store ID if present
        if (feature.TryGetProperty("id", out var id))
        {
            result["id"] = JsonElementToObject(id);
        }

        return result;
    }

    private object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => element.GetRawText()
        };
    }
}
