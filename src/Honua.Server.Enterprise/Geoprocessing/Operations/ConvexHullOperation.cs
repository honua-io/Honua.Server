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
/// Convex Hull operation - computes the smallest convex polygon containing all input geometries
/// </summary>
public class ConvexHullOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.ConvexHull;
    public override string Description => "Computes the convex hull (smallest convex polygon) that contains all input geometries";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var perFeature = Convert.ToBoolean(parameters.GetValueOrDefault("per_feature", false));

            ReportProgress(progress, 10, "Loading input geometries");

            var geometries = await LoadGeometriesAsync(inputs[0], cancellationToken);
            var totalFeatures = geometries.Count;

            ReportProgress(progress, 30, $"Computing convex hull for {totalFeatures} features");

            cancellationToken.ThrowIfCancellationRequested();

            List<Geometry> results;
            var factory = geometries[0].Factory;

            if (perFeature)
            {
                // Compute convex hull for each feature individually
                results = new List<Geometry>();
                for (int i = 0; i < geometries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hull = geometries[i].ConvexHull();
                    results.Add(hull);

                    if (i % 100 == 0)
                    {
                        var percent = 30 + (int)((i / (double)totalFeatures) * 60);
                        ReportProgress(progress, percent, $"Processed {i + 1}/{totalFeatures} features", i + 1, totalFeatures);
                    }
                }
            }
            else
            {
                // Compute single convex hull for all features combined
                ReportProgress(progress, 40, "Computing convex hull for all features");
                var collection = factory.CreateGeometryCollection(geometries.ToArray());
                var hull = collection.ConvexHull();
                results = new List<Geometry> { hull };
            }

            ReportProgress(progress, 90, "Serializing output");

            // Create result geometry
            var result = results.Count == 1
                ? results[0]
                : factory.CreateGeometryCollection(results.ToArray());

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
                    { "count", results.Count },
                    { "input_count", totalFeatures },
                    { "per_feature", perFeature },
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
        return base.Validate(parameters, inputs);
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 1000;

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 500, // Convex hull is fast
            EstimatedMemoryMB = estimatedFeatures / 50,
            EstimatedOutputSizeMB = estimatedFeatures / 500, // Output is typically much smaller
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        return await GeometryLoader.LoadGeometriesAsync(input, cancellationToken);
    }
}
