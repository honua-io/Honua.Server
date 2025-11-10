// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace Honua.Server.Enterprise.Geoprocessing.Operations;

/// <summary>
/// Simplify operation - reduces the complexity of geometries using Douglas-Peucker algorithm
/// </summary>
public class SimplifyOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Simplify;
    public override string Description => "Simplifies geometries by reducing vertex count while preserving shape using Douglas-Peucker algorithm";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Get parameters
            var tolerance = Convert.ToDouble(parameters.GetValueOrDefault("tolerance", 0.0001));
            var preserveTopology = Convert.ToBoolean(parameters.GetValueOrDefault("preserve_topology", true));

            ReportProgress(progress, 10, "Loading input geometries");

            var geometries = await LoadGeometriesAsync(inputs[0], cancellationToken);
            var totalFeatures = geometries.Count;

            ReportProgress(progress, 30, $"Simplifying {totalFeatures} features with tolerance {tolerance}", 0, totalFeatures);

            var simplified = new List<Geometry>();
            var originalVertexCount = 0;
            var simplifiedVertexCount = 0;

            for (int i = 0; i < geometries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                originalVertexCount += geometries[i].NumPoints;

                Geometry simplifiedGeom;
                if (preserveTopology)
                {
                    // TopologyPreservingSimplifier ensures no self-intersections or invalid geometries
                    simplifiedGeom = TopologyPreservingSimplifier.Simplify(geometries[i], tolerance);
                }
                else
                {
                    // DouglasPeuckerSimplifier is faster but may create invalid geometries
                    simplifiedGeom = DouglasPeuckerSimplifier.Simplify(geometries[i], tolerance);
                }

                simplifiedVertexCount += simplifiedGeom.NumPoints;
                simplified.Add(simplifiedGeom);

                if (i % 100 == 0)
                {
                    var percent = 30 + (int)((i / (double)totalFeatures) * 60);
                    ReportProgress(progress, percent, $"Simplified {i + 1}/{totalFeatures} features", i + 1, totalFeatures);
                }
            }

            ReportProgress(progress, 90, "Serializing output");

            // Create result geometry collection
            var factory = geometries[0].Factory;
            var result = factory.CreateGeometryCollection(simplified.ToArray());

            // Convert to GeoJSON
            var writer = new GeoJsonWriter();
            var geoJson = writer.Write(result);

            var reductionPercent = originalVertexCount > 0
                ? Math.Round((1 - (double)simplifiedVertexCount / originalVertexCount) * 100, 2)
                : 0;

            ReportProgress(progress, 100, "Completed");

            return new OperationResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    { "geojson", geoJson },
                    { "count", simplified.Count },
                    { "original_vertices", originalVertexCount },
                    { "simplified_vertices", simplifiedVertexCount },
                    { "reduction_percent", reductionPercent },
                    { "tolerance", tolerance },
                    { "preserve_topology", preserveTopology }
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

        if (!parameters.ContainsKey("tolerance"))
        {
            result.IsValid = false;
            result.Errors.Add("Parameter 'tolerance' is required");
        }

        if (parameters.TryGetValue("tolerance", out var tolValue))
        {
            var tolerance = Convert.ToDouble(tolValue);
            if (tolerance <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("Tolerance must be greater than 0");
            }
            if (tolerance > 1.0)
            {
                result.Warnings.Add("Large tolerance may result in significant shape distortion");
            }
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 1000;

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 200,
            EstimatedMemoryMB = estimatedFeatures / 20,
            EstimatedOutputSizeMB = estimatedFeatures / 100, // Simplified output is smaller
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        return await GeometryLoader.LoadGeometriesAsync(input, cancellationToken);
    }
}
