// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Geoprocessing.Executors;

/// <summary>
/// Tier 2 executor using PostGIS stored procedures
/// Server-side operations that complete in 1-10 seconds
/// </summary>
public class PostGisExecutor : IPostGisExecutor
{
    private readonly string _connectionString;
    private readonly ILogger<PostGisExecutor> _logger;

    public PostGisExecutor(string connectionString, ILogger<PostGisExecutor> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Executing process {ProcessId} on PostGIS tier for job {JobId}",
            process.Id, run.JobId);

        try
        {
            // Check if operation is supported before attempting database connection
            var supportedOperations = new[] { "buffer", "intersection", "union", "spatial-join", "dissolve" };
            if (!supportedOperations.Contains(process.Id))
            {
                throw new NotSupportedException($"Process '{process.Id}' not supported by PostGIS executor");
            }

            ReportProgress(progress, 0, "Connecting to PostGIS");

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            ReportProgress(progress, 10, "Executing PostGIS operation");

            // Route to appropriate stored procedure
            var result = process.Id switch
            {
                "buffer" => await ExecuteBufferAsync(connection, run, progress, ct),
                "intersection" => await ExecuteIntersectionAsync(connection, run, progress, ct),
                "union" => await ExecuteUnionAsync(connection, run, progress, ct),
                "spatial-join" => await ExecuteSpatialJoinAsync(connection, run, progress, ct),
                _ => throw new NotSupportedException($"Process '{process.Id}' not supported by PostGIS executor")
            };

            stopwatch.Stop();

            ReportProgress(progress, 100, "Completed");

            return new ProcessResult
            {
                JobId = run.JobId,
                ProcessId = run.ProcessId,
                Status = ProcessRunStatus.Completed,
                Success = true,
                Output = result,
                DurationMs = stopwatch.ElapsedMilliseconds,
                FeaturesProcessed = result.TryGetValue("count", out var count) ? Convert.ToInt64(count) : 0
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "PostGIS execution failed for job {JobId}", run.JobId);

            return new ProcessResult
            {
                JobId = run.JobId,
                ProcessId = run.ProcessId,
                Status = ProcessRunStatus.Failed,
                Success = false,
                ErrorMessage = ex.Message,
                ErrorDetails = ex.ToString(),
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public Task<bool> CanExecuteAsync(ProcessDefinition process, ProcessExecutionRequest request, CancellationToken ct = default)
    {
        // PostGIS can handle medium-sized operations
        var supported = process.Id switch
        {
            "buffer" or "intersection" or "union" or "spatial-join" or "dissolve" => true,
            _ => false
        };

        if (!supported)
            return Task.FromResult(false);

        // Check thresholds
        var thresholds = process.ExecutionConfig.Thresholds;
        if (thresholds != null)
        {
            // In production, would estimate input size and feature count
            // For now, assume PostGIS can handle it
            return Task.FromResult(true);
        }

        return Task.FromResult(true);
    }

    // Operation implementations

    private async Task<Dictionary<string, object>> ExecuteBufferAsync(
        NpgsqlConnection connection,
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 30, "Executing ST_Buffer");

        var geometryWkt = run.Inputs["geometry"].ToString();
        var distance = Convert.ToDouble(run.Inputs["distance"]);

        // Use PostGIS ST_Buffer function
        var sql = @"
            SELECT ST_AsGeoJSON(ST_Buffer(ST_GeomFromText(@Geometry, 4326), @Distance)) as geojson,
                   ST_Area(ST_Buffer(ST_GeomFromText(@Geometry, 4326), @Distance)) as area";

        var result = await connection.QuerySingleAsync(sql, new { Geometry = geometryWkt, Distance = distance });

        ReportProgress(progress, 90, "Formatting result");

        return new Dictionary<string, object>
        {
            ["geojson"] = result.geojson,
            ["area"] = result.area,
            ["count"] = 1
        };
    }

    private async Task<Dictionary<string, object>> ExecuteIntersectionAsync(
        NpgsqlConnection connection,
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 30, "Executing ST_Intersection");

        var geometry1Wkt = run.Inputs["geometry1"].ToString();
        var geometry2Wkt = run.Inputs["geometry2"].ToString();

        var sql = @"
            SELECT ST_AsGeoJSON(ST_Intersection(
                       ST_GeomFromText(@Geometry1, 4326),
                       ST_GeomFromText(@Geometry2, 4326)
                   )) as geojson,
                   ST_IsEmpty(ST_Intersection(
                       ST_GeomFromText(@Geometry1, 4326),
                       ST_GeomFromText(@Geometry2, 4326)
                   )) as is_empty";

        var result = await connection.QuerySingleAsync(sql, new
        {
            Geometry1 = geometry1Wkt,
            Geometry2 = geometry2Wkt
        });

        ReportProgress(progress, 90, "Formatting result");

        return new Dictionary<string, object>
        {
            ["geojson"] = result.geojson,
            ["isEmpty"] = result.is_empty,
            ["count"] = 1
        };
    }

    private async Task<Dictionary<string, object>> ExecuteUnionAsync(
        NpgsqlConnection connection,
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 30, "Executing ST_Union");

        var geometry1Wkt = run.Inputs["geometry1"].ToString();
        var geometry2Wkt = run.Inputs["geometry2"].ToString();

        var sql = @"
            SELECT ST_AsGeoJSON(ST_Union(
                       ST_GeomFromText(@Geometry1, 4326),
                       ST_GeomFromText(@Geometry2, 4326)
                   )) as geojson";

        var result = await connection.QuerySingleAsync(sql, new
        {
            Geometry1 = geometry1Wkt,
            Geometry2 = geometry2Wkt
        });

        ReportProgress(progress, 90, "Formatting result");

        return new Dictionary<string, object>
        {
            ["geojson"] = result.geojson,
            ["count"] = 1
        };
    }

    private async Task<Dictionary<string, object>> ExecuteSpatialJoinAsync(
        NpgsqlConnection connection,
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        // This would typically join two feature collections
        // For now, return a placeholder result
        ReportProgress(progress, 50, "Performing spatial join");

        await Task.Delay(100, ct); // Simulate work

        return new Dictionary<string, object>
        {
            ["geojson"] = "{}",
            ["count"] = 0,
            ["message"] = "Spatial join operation - full implementation pending"
        };
    }

    private void ReportProgress(IProgress<ProcessProgress>? progress, int percent, string? message = null)
    {
        progress?.Report(new ProcessProgress
        {
            Percent = percent,
            Message = message,
            Stage = "PostGIS Execution"
        });
    }
}
