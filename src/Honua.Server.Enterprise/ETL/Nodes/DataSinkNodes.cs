// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Data sink node that writes features to a PostGIS table
/// </summary>
public class PostGisDataSinkNode : WorkflowNodeBase
{
    private readonly string _connectionString;

    public PostGisDataSinkNode(string connectionString, ILogger<PostGisDataSinkNode> logger)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override string NodeType => "data_sink.postgis";
    public override string DisplayName => "PostGIS Data Sink";
    public override string Description => "Writes features to a PostGIS database table";

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var result = new NodeValidationResult { IsValid = true };

        if (!nodeDefinition.Parameters.ContainsKey("table"))
        {
            result.IsValid = false;
            result.Errors.Add("'table' parameter is required");
        }

        return Task.FromResult(result);
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Executing PostGIS data sink node {NodeId}", context.NodeDefinition.Id);

            ReportProgress(context, 0, "Connecting to PostGIS database");

            var table = GetRequiredParameter<string>(context.NodeDefinition.Parameters, "table");
            var geometryColumn = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "geometry_column", "geometry");
            var srid = GetOptionalParameter<int>(context.NodeDefinition.Parameters, "srid", 4326);
            var mode = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "mode", "insert"); // insert, replace, append

            // Get input features from upstream node
            var inputNodeId = context.Inputs.Keys.FirstOrDefault();
            if (inputNodeId == null)
            {
                return NodeExecutionResult.Fail("No input node connected");
            }

            var inputResult = context.Inputs[inputNodeId];
            if (!inputResult.Data.TryGetValue("features", out var featuresObj) || featuresObj is not List<Dictionary<string, object>> features)
            {
                return NodeExecutionResult.Fail("Input does not contain 'features' data");
            }

            ReportProgress(context, 10, $"Writing {features.Count} features to {table}");

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Handle mode
            if (mode == "replace")
            {
                await connection.ExecuteAsync($"TRUNCATE TABLE {table}");
                Logger.LogInformation("Truncated table {Table}", table);
            }

            // Insert features
            int inserted = 0;
            foreach (var feature in features)
            {
                // Build INSERT statement dynamically based on feature properties
                var columns = feature.Keys.Where(k => k != "geometry" && k != "geojson").ToList();
                columns.Add(geometryColumn);

                var columnNames = string.Join(", ", columns);
                var paramNames = string.Join(", ", columns.Select((_, i) => $"@p{i}"));

                var sql = $"INSERT INTO {table} ({columnNames}) VALUES ({paramNames})";

                var parameters = new DynamicParameters();
                for (int i = 0; i < columns.Count - 1; i++)
                {
                    var column = columns[i];
                    parameters.Add($"p{i}", feature.TryGetValue(column, out var value) ? value : null);
                }

                // Add geometry parameter (from GeoJSON or WKT)
                var geometryValue = feature.TryGetValue("geometry", out var geom) ? geom?.ToString() :
                                  feature.TryGetValue("geojson", out var gj) ? gj?.ToString() : null;

                if (geometryValue != null)
                {
                    parameters.Add($"p{columns.Count - 1}", geometryValue); // Will use ST_GeomFromGeoJSON in actual impl
                    // TODO: Properly parse and insert geometry using ST_GeomFromGeoJSON or ST_GeomFromText
                }

                try
                {
                    await connection.ExecuteAsync(sql, parameters);
                    inserted++;

                    if (inserted % 100 == 0)
                    {
                        ReportProgress(context, 10 + (inserted * 80 / features.Count),
                            $"Inserted {inserted} / {features.Count} features", inserted, features.Count);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error inserting feature {Index}", inserted);
                }
            }

            stopwatch.Stop();

            Logger.LogInformation(
                "PostGIS data sink node {NodeId} wrote {Count} features in {DurationMs}ms",
                context.NodeDefinition.Id,
                inserted,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(context, 100, $"Wrote {inserted} features to {table}");

            return new NodeExecutionResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["table"] = table,
                    ["inserted"] = inserted,
                    ["total"] = features.Count
                },
                FeaturesProcessed = inserted,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "PostGIS data sink node {NodeId} failed", context.NodeDefinition.Id);
            return NodeExecutionResult.Fail($"PostGIS insert failed: {ex.Message}", ex.StackTrace);
        }
    }
}

/// <summary>
/// Data sink node that exports features to GeoJSON format
/// </summary>
public class GeoJsonExportNode : WorkflowNodeBase
{
    public GeoJsonExportNode(ILogger<GeoJsonExportNode> logger) : base(logger)
    {
    }

    public override string NodeType => "data_sink.geojson";
    public override string DisplayName => "GeoJSON Export";
    public override string Description => "Exports features to GeoJSON format";

    protected override Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Executing GeoJSON export node {NodeId}", context.NodeDefinition.Id);

            ReportProgress(context, 0, "Preparing GeoJSON export");

            // Get input features from upstream node
            var inputNodeId = context.Inputs.Keys.FirstOrDefault();
            if (inputNodeId == null)
            {
                return Task.FromResult(NodeExecutionResult.Fail("No input node connected"));
            }

            var inputResult = context.Inputs[inputNodeId];
            if (!inputResult.Data.TryGetValue("features", out var featuresObj) || featuresObj is not List<Dictionary<string, object>> features)
            {
                return Task.FromResult(NodeExecutionResult.Fail("Input does not contain 'features' data"));
            }

            ReportProgress(context, 20, $"Exporting {features.Count} features");

            // Build GeoJSON FeatureCollection
            var featureCollection = new
            {
                type = "FeatureCollection",
                features = features.Select(f => new
                {
                    type = "Feature",
                    id = f.TryGetValue("id", out var id) ? id : null,
                    geometry = f.TryGetValue("geometry", out var geom) ?
                        (geom is string geomStr ? JsonDocument.Parse(geomStr).RootElement : geom) : null,
                    properties = f.Where(kvp => kvp.Key != "id" && kvp.Key != "geometry" && kvp.Key != "geojson")
                                  .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }).ToList()
            };

            var geojson = JsonSerializer.Serialize(featureCollection, new JsonSerializerOptions
            {
                WriteIndented = GetOptionalParameter<bool>(context.NodeDefinition.Parameters, "pretty", true)
            });

            stopwatch.Stop();

            Logger.LogInformation(
                "GeoJSON export node {NodeId} exported {Count} features in {DurationMs}ms",
                context.NodeDefinition.Id,
                features.Count,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(context, 100, $"Exported {features.Count} features");

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["geojson"] = geojson,
                    ["count"] = features.Count,
                    ["size_bytes"] = Encoding.UTF8.GetByteCount(geojson)
                },
                FeaturesProcessed = features.Count,
                DurationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "GeoJSON export node {NodeId} failed", context.NodeDefinition.Id);
            return Task.FromResult(NodeExecutionResult.Fail($"GeoJSON export failed: {ex.Message}", ex.StackTrace));
        }
    }
}

/// <summary>
/// Data sink node that stores output in workflow run state for later retrieval
/// </summary>
public class OutputNode : WorkflowNodeBase
{
    public OutputNode(ILogger<OutputNode> logger) : base(logger)
    {
    }

    public override string NodeType => "data_sink.output";
    public override string DisplayName => "Output";
    public override string Description => "Stores workflow output for retrieval";

    protected override Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Executing output node {NodeId}", context.NodeDefinition.Id);

            var outputName = GetOptionalParameter<string>(context.NodeDefinition.Parameters, "name", "output");

            // Get input from upstream node
            var inputNodeId = context.Inputs.Keys.FirstOrDefault();
            if (inputNodeId == null)
            {
                return Task.FromResult(NodeExecutionResult.Fail("No input node connected"));
            }

            var inputResult = context.Inputs[inputNodeId];

            // Store in workflow run state
            context.State[$"output.{outputName}"] = inputResult.Data;

            stopwatch.Stop();

            Logger.LogInformation(
                "Output node {NodeId} stored output '{OutputName}' in {DurationMs}ms",
                context.NodeDefinition.Id,
                outputName,
                stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["output_name"] = outputName,
                    ["stored"] = true
                },
                DurationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Output node {NodeId} failed", context.NodeDefinition.Id);
            return Task.FromResult(NodeExecutionResult.Fail($"Output storage failed: {ex.Message}", ex.StackTrace));
        }
    }
}
