// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.Geoprocessing.Operations;

/// <summary>
/// Intersection operation - computes the geometric intersection of two input geometries
/// </summary>
public class IntersectionOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Intersection;
    public override string Description => "Computes the geometric intersection between two sets of input geometries";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            ReportProgress(progress, 10, "Loading first input geometries");
            var geometries1 = await LoadGeometriesAsync(inputs[0], cancellationToken);

            ReportProgress(progress, 20, "Loading second input geometries");
            var geometries2 = await LoadGeometriesAsync(inputs[1], cancellationToken);

            var totalOperations = geometries1.Count * geometries2.Count;
            ReportProgress(progress, 30, $"Computing intersections for {geometries1.Count} x {geometries2.Count} features", 0, totalOperations);

            var results = new List<Geometry>();
            var processedCount = 0;

            foreach (var geom1 in geometries1)
            {
                foreach (var geom2 in geometries2)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (geom1.Intersects(geom2))
                    {
                        var intersection = geom1.Intersection(geom2);
                        if (!intersection.IsEmpty)
                        {
                            results.Add(intersection);
                        }
                    }

                    processedCount++;
                    if (processedCount % 100 == 0)
                    {
                        var percent = 30 + (int)((processedCount / (double)totalOperations) * 60);
                        ReportProgress(progress, percent, $"Processed {processedCount}/{totalOperations} intersections", processedCount, totalOperations);
                    }
                }
            }

            ReportProgress(progress, 90, "Serializing output");

            // Create result geometry collection
            var factory = geometries1[0].Factory;
            var resultGeometry = factory.CreateGeometryCollection(results.ToArray());

            // Convert to GeoJSON
            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(resultGeometry);

            ReportProgress(progress, 100, "Completed");

            return new OperationResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    { "geojson", geoJson },
                    { "count", results.Count },
                    { "input1_count", geometries1.Count },
                    { "input2_count", geometries2.Count }
                },
                FeaturesProcessed = processedCount,
                DurationMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    public override ValidationResult Validate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var result = new ValidationResult { IsValid = true };

        if (inputs == null || inputs.Count < 2)
        {
            result.IsValid = false;
            result.Errors.Add("Intersection operation requires two input geometries");
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 1000 * 1000; // n x m combinations

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 500,
            EstimatedMemoryMB = estimatedFeatures / 100,
            EstimatedOutputSizeMB = estimatedFeatures / 200,
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var factory = GeometryFactory.Default;
        var reader = new WKTReader();

        if (input.Type == "wkt")
        {
            return new List<Geometry> { reader.Read(input.Source) };
        }

        if (input.Type == "geojson")
        {
            var geoJsonReader = new GeoJsonReader();
            var geometry = geoJsonReader.Read<Geometry>(input.Source);
            return new List<Geometry> { geometry };
        }

        throw new NotImplementedException($"Input type '{input.Type}' not yet implemented");
    }
}
