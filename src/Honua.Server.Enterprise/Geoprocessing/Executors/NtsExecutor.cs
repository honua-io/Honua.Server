// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.Geoprocessing.Executors;

/// <summary>
/// Tier 1 executor using NetTopologySuite for in-process operations
/// Fast, simple vector operations that complete in &lt;100ms
/// </summary>
public class NtsExecutor : INtsExecutor
{
    private readonly ILogger<NtsExecutor> _logger;
    private readonly GeometryFactory _geometryFactory;
    private readonly WKTReader _wktReader;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly GeoJsonWriter _geoJsonWriter;

    public NtsExecutor(ILogger<NtsExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _geometryFactory = GeometryFactory.Default;
        _wktReader = new WKTReader(_geometryFactory);
        _geoJsonReader = new GeoJsonReader();
        _geoJsonWriter = new GeoJsonWriter();
    }

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRun run,
        ProcessDefinition process,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Executing process {ProcessId} on NTS tier for job {JobId}",
            process.Id, run.JobId);

        try
        {
            ReportProgress(progress, 0, "Starting NTS execution");

            // Route to appropriate operation
            var result = process.Id switch
            {
                "buffer" => await ExecuteBufferAsync(run, progress, ct),
                "intersection" => await ExecuteIntersectionAsync(run, progress, ct),
                "union" => await ExecuteUnionAsync(run, progress, ct),
                "difference" => await ExecuteDifferenceAsync(run, progress, ct),
                "convex-hull" => await ExecuteConvexHullAsync(run, progress, ct),
                "centroid" => await ExecuteCentroidAsync(run, progress, ct),
                "simplify" => await ExecuteSimplifyAsync(run, progress, ct),
                _ => throw new NotSupportedException($"Process '{process.Id}' not supported by NTS executor")
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
                FeaturesProcessed = 1 // NTS typically works on single geometries
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "NTS execution failed for job {JobId}", run.JobId);

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
        // NTS can handle most simple vector operations with small inputs
        var supported = process.Id switch
        {
            "buffer" or "intersection" or "union" or "difference" or
            "convex-hull" or "centroid" or "simplify" => true,
            _ => false
        };

        if (!supported)
            return Task.FromResult(false);

        // Check input size constraints
        var thresholds = process.ExecutionConfig.Thresholds;
        if (thresholds != null)
        {
            // For now, assume NTS can handle it if thresholds allow
            // In production, would estimate input size from request
            return Task.FromResult(true);
        }

        return Task.FromResult(true);
    }

    // Operation implementations

    private Task<Dictionary<string, object>> ExecuteBufferAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometry");

        var geometry = ParseGeometry(run.Inputs["geometry"]);
        var distance = Convert.ToDouble(run.Inputs["distance"]);
        var segments = run.Inputs.TryGetValue("segments", out var seg) ? Convert.ToInt32(seg) : 8;

        ReportProgress(progress, 50, "Computing buffer");

        var buffered = geometry.Buffer(distance, segments);

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(buffered);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = buffered.GeometryType,
            ["area"] = buffered.Area,
            ["length"] = buffered.Length
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteIntersectionAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometries");

        var geometry1 = ParseGeometry(run.Inputs["geometry1"]);
        var geometry2 = ParseGeometry(run.Inputs["geometry2"]);

        ReportProgress(progress, 50, "Computing intersection");

        var intersection = geometry1.Intersection(geometry2);

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(intersection);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = intersection.GeometryType,
            ["isEmpty"] = intersection.IsEmpty,
            ["area"] = intersection.Area
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteUnionAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometries");

        var geometry1 = ParseGeometry(run.Inputs["geometry1"]);
        var geometry2 = ParseGeometry(run.Inputs["geometry2"]);

        ReportProgress(progress, 50, "Computing union");

        var union = geometry1.Union(geometry2);

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(union);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = union.GeometryType,
            ["area"] = union.Area
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteDifferenceAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometries");

        var geometry1 = ParseGeometry(run.Inputs["geometry1"]);
        var geometry2 = ParseGeometry(run.Inputs["geometry2"]);

        ReportProgress(progress, 50, "Computing difference");

        var difference = geometry1.Difference(geometry2);

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(difference);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = difference.GeometryType,
            ["area"] = difference.Area
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteConvexHullAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometry");

        var geometry = ParseGeometry(run.Inputs["geometry"]);

        ReportProgress(progress, 50, "Computing convex hull");

        var convexHull = geometry.ConvexHull();

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(convexHull);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = convexHull.GeometryType,
            ["area"] = convexHull.Area
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteCentroidAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometry");

        var geometry = ParseGeometry(run.Inputs["geometry"]);

        ReportProgress(progress, 50, "Computing centroid");

        var centroid = geometry.Centroid;

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(centroid);

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = centroid.GeometryType,
            ["x"] = centroid.X,
            ["y"] = centroid.Y
        };

        return Task.FromResult(result);
    }

    private Task<Dictionary<string, object>> ExecuteSimplifyAsync(
        ProcessRun run,
        IProgress<ProcessProgress>? progress,
        CancellationToken ct)
    {
        ReportProgress(progress, 10, "Parsing input geometry");

        var geometry = ParseGeometry(run.Inputs["geometry"]);
        var tolerance = Convert.ToDouble(run.Inputs["tolerance"]);

        ReportProgress(progress, 50, "Simplifying geometry");

        var simplified = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(geometry, tolerance);

        ReportProgress(progress, 90, "Serializing result");

        var geoJson = _geoJsonWriter.Write(simplified);

        var originalPoints = geometry.NumPoints;
        var simplifiedPoints = simplified.NumPoints;

        var result = new Dictionary<string, object>
        {
            ["geojson"] = geoJson,
            ["type"] = simplified.GeometryType,
            ["originalPoints"] = originalPoints,
            ["simplifiedPoints"] = simplifiedPoints,
            ["reduction"] = ((originalPoints - simplifiedPoints) / (double)originalPoints) * 100
        };

        return Task.FromResult(result);
    }

    // Helper methods

    private Geometry ParseGeometry(object input)
    {
        if (input is string str)
        {
            // Try WKT first
            if (str.StartsWith("POINT") || str.StartsWith("LINESTRING") || str.StartsWith("POLYGON") ||
                str.StartsWith("MULTIPOINT") || str.StartsWith("MULTILINESTRING") || str.StartsWith("MULTIPOLYGON"))
            {
                return _wktReader.Read(str);
            }

            // Try GeoJSON
            try
            {
                return _geoJsonReader.Read<Geometry>(str);
            }
            catch
            {
                throw new ArgumentException($"Invalid geometry format: {str}");
            }
        }

        throw new ArgumentException($"Unsupported geometry input type: {input.GetType().Name}");
    }

    private void ReportProgress(IProgress<ProcessProgress>? progress, int percent, string? message = null)
    {
        progress?.Report(new ProcessProgress
        {
            Percent = percent,
            Message = message,
            Stage = "NTS Execution"
        });
    }
}
