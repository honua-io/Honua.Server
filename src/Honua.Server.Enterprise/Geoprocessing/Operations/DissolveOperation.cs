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
/// Dissolve operation - merges overlapping or adjacent geometries into a single geometry
/// </summary>
public class DissolveOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Dissolve;
    public override string Description => "Dissolves geometries by merging overlapping or adjacent features into single geometries";

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

            var geometries = await LoadGeometriesAsync(inputs[0], cancellationToken);
            var totalFeatures = geometries.Count;

            ReportProgress(progress, 30, $"Dissolving {totalFeatures} features");

            cancellationToken.ThrowIfCancellationRequested();

            // Create geometry collection
            var factory = geometries[0].Factory;
            var collection = factory.CreateGeometryCollection(geometries.ToArray());

            ReportProgress(progress, 40, "Computing union (dissolve)");

            // Union dissolves all overlapping geometries
            var result = collection.Union();

            ReportProgress(progress, 90, "Serializing output");

            // Convert to GeoJSON
            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(result);

            // Count resulting features
            int resultCount = 1;
            if (result is GeometryCollection gc)
            {
                resultCount = gc.NumGeometries;
            }
            else if (result is MultiPolygon mp)
            {
                resultCount = mp.NumGeometries;
            }
            else if (result is MultiLineString mls)
            {
                resultCount = mls.NumGeometries;
            }
            else if (result is MultiPoint mpt)
            {
                resultCount = mpt.NumGeometries;
            }

            ReportProgress(progress, 100, "Completed");

            return new OperationResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    { "geojson", geoJson },
                    { "input_count", totalFeatures },
                    { "output_count", resultCount },
                    { "geometry_type", result.GeometryType },
                    { "reduction_percent", totalFeatures > 0 ? Math.Round((1 - (double)resultCount / totalFeatures) * 100, 2) : 0 }
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

        if (inputs != null && inputs.Count > 1)
        {
            result.Warnings.Add("Dissolve typically operates on a single input layer; multiple inputs will be merged together");
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 1000;

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 50, // Union is expensive
            EstimatedMemoryMB = estimatedFeatures / 5,
            EstimatedOutputSizeMB = estimatedFeatures / 100, // Output is typically smaller
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        return await GeometryLoader.LoadGeometriesAsync(input, cancellationToken);
    }
}
