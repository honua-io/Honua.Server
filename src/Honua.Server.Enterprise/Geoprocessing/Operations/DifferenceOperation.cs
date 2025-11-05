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
/// Difference operation - computes the geometric difference (subtraction) between two inputs
/// </summary>
public class DifferenceOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Difference;
    public override string Description => "Computes the geometric difference by subtracting the second input from the first";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            ReportProgress(progress, 10, "Loading first input (features to keep)");
            var geometries1 = await LoadGeometriesAsync(inputs[0], cancellationToken);

            ReportProgress(progress, 20, "Loading second input (features to subtract)");
            var geometries2 = await LoadGeometriesAsync(inputs[1], cancellationToken);

            // Create union of second input to subtract
            var factory = geometries1[0].Factory;
            var subtractCollection = factory.CreateGeometryCollection(geometries2.ToArray());
            var subtractGeometry = subtractCollection.Union();

            ReportProgress(progress, 40, $"Computing difference for {geometries1.Count} features", 0, geometries1.Count);

            var results = new List<Geometry>();
            for (int i = 0; i < geometries1.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var difference = geometries1[i].Difference(subtractGeometry);
                if (!difference.IsEmpty)
                {
                    results.Add(difference);
                }

                if (i % 100 == 0)
                {
                    var percent = 40 + (int)((i / (double)geometries1.Count) * 50);
                    ReportProgress(progress, percent, $"Processed {i + 1}/{geometries1.Count} features", i + 1, geometries1.Count);
                }
            }

            ReportProgress(progress, 90, "Serializing output");

            // Create result geometry collection
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
                    { "input_count", geometries1.Count },
                    { "subtract_count", geometries2.Count }
                },
                FeaturesProcessed = geometries1.Count,
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
            result.Errors.Add("Difference operation requires two input geometries (input to keep, input to subtract)");
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 1000;

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 100,
            EstimatedMemoryMB = estimatedFeatures / 10,
            EstimatedOutputSizeMB = estimatedFeatures / 50,
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
