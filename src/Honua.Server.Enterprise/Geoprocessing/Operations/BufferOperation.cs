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
/// Buffer operation - creates a polygon around geometries at specified distance
/// </summary>
public class BufferOperation : GeoprocessingOperationBase
{
    public override string Name => GeoprocessingOperation.Buffer;
    public override string Description => "Creates a buffer (polygon) around input geometries at a specified distance";

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
            var distance = Convert.ToDouble(parameters.GetValueOrDefault("distance", 100.0));
            var units = parameters.GetValueOrDefault("units", "meters")?.ToString() ?? "meters";
            var segments = Convert.ToInt32(parameters.GetValueOrDefault("segments", 8));
            var dissolve = Convert.ToBoolean(parameters.GetValueOrDefault("dissolve", false));

            ReportProgress(progress, 10, "Loading input geometries");

            // Load input geometries
            var geometries = await LoadGeometriesAsync(inputs[0], cancellationToken);
            var totalFeatures = geometries.Count;

            ReportProgress(progress, 30, $"Buffering {totalFeatures} features", 0, totalFeatures);

            // Convert distance based on units
            var distanceInMeters = ConvertToMeters(distance, units);

            // Buffer each geometry
            var buffered = new List<Geometry>();
            for (int i = 0; i < geometries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bufferedGeom = geometries[i].Buffer(distanceInMeters, segments);
                buffered.Add(bufferedGeom);

                if (i % 100 == 0)
                {
                    var percent = 30 + (int)((i / (double)totalFeatures) * 50);
                    ReportProgress(progress, percent, $"Buffered {i + 1}/{totalFeatures} features", i + 1, totalFeatures);
                }
            }

            ReportProgress(progress, 80, "Processing results");

            // Optionally dissolve overlapping buffers
            Geometry result;
            if (dissolve && buffered.Count > 1)
            {
                ReportProgress(progress, 85, "Dissolving overlapping buffers");
                var collection = geometries[0].Factory.CreateGeometryCollection(buffered.ToArray());
                result = collection.Union();
            }
            else
            {
                result = geometries[0].Factory.CreateGeometryCollection(buffered.ToArray());
            }

            ReportProgress(progress, 95, "Serializing output");

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
                    { "count", buffered.Count },
                    { "dissolved", dissolve }
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

        if (!parameters.ContainsKey("distance"))
        {
            result.IsValid = false;
            result.Errors.Add("Parameter 'distance' is required");
        }

        if (parameters.TryGetValue("distance", out var distValue))
        {
            var distance = Convert.ToDouble(distValue);
            if (distance <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("Distance must be greater than 0");
            }
            if (distance > 100000) // 100km
            {
                result.Warnings.Add("Large buffer distance may result in long processing time");
            }
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        // Estimate based on feature count (if known)
        // This is simplified - in production, would query actual feature count
        var estimatedFeatures = 1000; // Default estimate

        return new JobEstimate
        {
            EstimatedDurationSeconds = estimatedFeatures / 100, // ~100 features per second
            EstimatedMemoryMB = estimatedFeatures / 10, // ~10 features per MB
            EstimatedOutputSizeMB = estimatedFeatures / 50,
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        // This is simplified - in production, would load from collection, URL, or inline GeoJSON
        // For now, return placeholder
        await Task.CompletedTask;

        var factory = GeometryFactory.Default;
        var reader = new WKTReader();

        // Sample: load from WKT
        if (input.Type == "wkt")
        {
            return new List<Geometry> { reader.Read(input.Source) };
        }

        // Sample: load from GeoJSON
        if (input.Type == "geojson")
        {
            var geoJsonReader = new GeoJsonReader();
            var geometry = geoJsonReader.Read<Geometry>(input.Source);
            return new List<Geometry> { geometry };
        }

        // TODO: Implement loading from collections, URLs, etc.
        throw new NotImplementedException($"Input type '{input.Type}' not yet implemented");
    }

    private double ConvertToMeters(double distance, string units)
    {
        return units.ToLowerInvariant() switch
        {
            "meters" or "m" => distance,
            "kilometers" or "km" => distance * 1000,
            "feet" or "ft" => distance * 0.3048,
            "miles" or "mi" => distance * 1609.34,
            _ => distance // Default to meters
        };
    }
}
