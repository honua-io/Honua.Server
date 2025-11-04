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
/// Union operation - computes the geometric union of input geometries
/// </summary>
public class UnionOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Union;
    public override string Description => "Computes the geometric union of all input geometries, merging overlapping areas";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            ReportProgress(progress, 10, "Loading input geometries");

            var allGeometries = new List<Geometry>();
            for (int i = 0; i < inputs.Count; i++)
            {
                var geometries = await LoadGeometriesAsync(inputs[i], cancellationToken);
                allGeometries.AddRange(geometries);

                var percent = 10 + (int)((i / (double)inputs.Count) * 20);
                ReportProgress(progress, percent, $"Loaded input {i + 1}/{inputs.Count}");
            }

            var totalFeatures = allGeometries.Count;
            ReportProgress(progress, 30, $"Computing union of {totalFeatures} features", 0, totalFeatures);

            cancellationToken.ThrowIfCancellationRequested();

            // Create geometry collection and compute union
            var factory = allGeometries[0].Factory;
            var collection = factory.CreateGeometryCollection(allGeometries.ToArray());

            ReportProgress(progress, 40, "Computing union (this may take a while for complex geometries)");

            var result = collection.Union();

            ReportProgress(progress, 90, "Serializing output");

            // Convert to GeoJSON
            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(result);

            ReportProgress(progress, 100, "Completed");

            return new OperationResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    { "geojson", geoJson },
                    { "input_count", totalFeatures },
                    { "geometry_type", result.GeometryType }
                },
                FeaturesProcessed = totalFeatures,
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
        var result = base.Validate(parameters, inputs);

        if (inputs != null && inputs.Count > 10)
        {
            result.Warnings.Add("Large number of inputs may result in long processing time");
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = inputs?.Count ?? 1 * 1000;

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 50, // Union is more expensive
            EstimatedMemoryMB = estimatedFeatures / 5,
            EstimatedOutputSizeMB = estimatedFeatures / 100,
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
