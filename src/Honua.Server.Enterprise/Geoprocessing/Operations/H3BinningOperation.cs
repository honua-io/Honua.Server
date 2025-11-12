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
/// H3 Hexagonal Binning operation - bins point data into H3 hexagons with aggregation
/// Provides advanced spatial aggregation using Uber's H3 hierarchical hexagonal grid system
/// </summary>
public class H3BinningOperation : GeoprocessingOperationBase
{
    private readonly H3Service _h3Service;

    public H3BinningOperation()
    {
        _h3Service = new H3Service();
    }

    public override string Name => "h3_binning";

    public override string Description =>
        "Bins point data into H3 hexagonal grid cells with statistical aggregation. " +
        "Supports resolutions 0-15 and various aggregation functions (count, sum, avg, min, max).";

    public override async Task<OperationResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        List<GeoprocessingInput> inputs,
        IProgress<GeoprocessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Parse parameters
            var resolution = Convert.ToInt32(parameters.GetValueOrDefault("resolution", 7));
            var aggregationType = ParseAggregationType(parameters.GetValueOrDefault("aggregation", "count")?.ToString() ?? "count");
            var valueField = parameters.GetValueOrDefault("valueField", null)?.ToString();
            var includeBoundaries = Convert.ToBoolean(parameters.GetValueOrDefault("includeBoundaries", true));
            var includeStatistics = Convert.ToBoolean(parameters.GetValueOrDefault("includeStatistics", false));

            ReportProgress(progress, 10, "Loading input geometries");

            // Load input geometries (must be points)
            var geometries = await LoadGeometriesAsync(inputs[0], cancellationToken);
            var totalFeatures = geometries.Count;

            // Validate that all geometries are points
            var points = geometries.Where(g => g is Point).Cast<Point>().ToList();
            if (points.Count != totalFeatures)
            {
                throw new InvalidOperationException($"All input geometries must be points. Found {totalFeatures - points.Count} non-point geometries.");
            }

            ReportProgress(progress, 30, $"Binning {totalFeatures} points into H3 hexagons at resolution {resolution}", 0, totalFeatures);

            // Bin points into H3 hexagons
            var hexBins = new Dictionary<string, List<double>>();

            for (int i = 0; i < points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var point = points[i];
                var h3Index = _h3Service.PointToH3(point.Y, point.X, resolution);

                if (!hexBins.ContainsKey(h3Index))
                {
                    hexBins[h3Index] = new List<double>();
                }

                // Extract value if field is specified
                double value = 1.0; // Default to count
                if (!string.IsNullOrEmpty(valueField) && point.UserData is Dictionary<string, object> properties)
                {
                    if (properties.TryGetValue(valueField, out var fieldValue))
                    {
                        value = Convert.ToDouble(fieldValue);
                    }
                }

                hexBins[h3Index].Add(value);

                if (i % 1000 == 0)
                {
                    var percent = 30 + (int)((i / (double)totalFeatures) * 40);
                    ReportProgress(progress, percent, $"Binned {i + 1}/{totalFeatures} points", i + 1, totalFeatures);
                }
            }

            ReportProgress(progress, 70, $"Computing aggregations for {hexBins.Count} hexagons");

            // Aggregate values for each hexagon
            var results = new List<H3BinResult>();

            int hexIndex = 0;
            foreach (var (h3Index, values) in hexBins)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var binResult = new H3BinResult
                {
                    H3Index = h3Index,
                    Count = values.Count,
                    Value = ComputeAggregation(values, aggregationType)
                };

                // Include boundary if requested
                if (includeBoundaries)
                {
                    binResult.Boundary = _h3Service.GetH3Boundary(h3Index);
                    binResult.Center = _h3Service.GetH3Center(h3Index);
                }

                // Include additional statistics if requested
                if (includeStatistics && values.Count > 0)
                {
                    binResult.Statistics = new Dictionary<string, double>
                    {
                        ["min"] = values.Min(),
                        ["max"] = values.Max(),
                        ["avg"] = values.Average(),
                        ["sum"] = values.Sum(),
                        ["median"] = CalculateMedian(values),
                        ["stdDev"] = CalculateStdDev(values)
                    };
                }

                results.Add(binResult);

                hexIndex++;
                if (hexIndex % 100 == 0)
                {
                    var percent = 70 + (int)((hexIndex / (double)hexBins.Count) * 20);
                    ReportProgress(progress, percent, $"Processed {hexIndex}/{hexBins.Count} hexagons");
                }
            }

            ReportProgress(progress, 90, "Generating GeoJSON output");

            // Convert to GeoJSON
            var geoJson = GenerateGeoJSON(results, resolution);

            ReportProgress(progress, 100, "Completed");

            return new OperationResult
            {
                Success = true,
                Data = new Dictionary<string, object>
                {
                    { "geojson", geoJson },
                    { "hexagonCount", results.Count },
                    { "pointCount", totalFeatures },
                    { "resolution", resolution },
                    { "aggregationType", aggregationType.ToString() },
                    { "avgHexagonArea", _h3Service.GetAverageArea(resolution) },
                    { "avgEdgeLength", _h3Service.GetAverageEdgeLength(resolution) },
                    { "results", results }
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

        // Validate resolution
        if (parameters.TryGetValue("resolution", out var resValue))
        {
            var resolution = Convert.ToInt32(resValue);
            if (resolution < 0 || resolution > 15)
            {
                result.IsValid = false;
                result.Errors.Add("H3 resolution must be between 0 and 15");
            }

            // Warn about extreme resolutions
            if (resolution > 12)
            {
                result.Warnings.Add("High resolution (>12) may generate many hexagons and impact performance");
            }
            if (resolution < 3)
            {
                result.Warnings.Add("Low resolution (<3) may result in very large hexagons with limited detail");
            }
        }

        // Validate aggregation type
        if (parameters.TryGetValue("aggregation", out var aggValue))
        {
            var aggType = aggValue?.ToString()?.ToLowerInvariant();
            if (aggType != null && !new[] { "count", "sum", "average", "min", "max", "stddev", "median" }.Contains(aggType))
            {
                result.IsValid = false;
                result.Errors.Add("Aggregation type must be one of: count, sum, average, min, max, stddev, median");
            }
        }

        return result;
    }

    public override JobEstimate Estimate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
    {
        var estimatedFeatures = 10000; // Default estimate
        var resolution = Convert.ToInt32(parameters.GetValueOrDefault("resolution", 7));

        // Higher resolution = more hexagons = longer processing time
        var resolutionMultiplier = Math.Pow(1.5, resolution - 7);

        return new JobEstimate
        {
            EstimatedDurationSeconds = (long)(estimatedFeatures / 1000.0 * resolutionMultiplier),
            EstimatedMemoryMB = (long)(estimatedFeatures / 100.0 * resolutionMultiplier),
            EstimatedOutputSizeMB = (long)(estimatedFeatures / 200.0 * resolutionMultiplier),
            EstimatedCpuCores = 1
        };
    }

    private async Task<List<Geometry>> LoadGeometriesAsync(GeoprocessingInput input, CancellationToken cancellationToken)
    {
        return await GeometryLoader.LoadGeometriesAsync(input, cancellationToken);
    }

    private H3AggregationType ParseAggregationType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "count" => H3AggregationType.Count,
            "sum" => H3AggregationType.Sum,
            "average" or "avg" or "mean" => H3AggregationType.Average,
            "min" or "minimum" => H3AggregationType.Min,
            "max" or "maximum" => H3AggregationType.Max,
            "stddev" or "std" => H3AggregationType.StdDev,
            "median" or "med" => H3AggregationType.Median,
            _ => H3AggregationType.Count
        };
    }

    private double ComputeAggregation(List<double> values, H3AggregationType aggregationType)
    {
        if (values.Count == 0) return 0;

        return aggregationType switch
        {
            H3AggregationType.Count => values.Count,
            H3AggregationType.Sum => values.Sum(),
            H3AggregationType.Average => values.Average(),
            H3AggregationType.Min => values.Min(),
            H3AggregationType.Max => values.Max(),
            H3AggregationType.StdDev => CalculateStdDev(values),
            H3AggregationType.Median => CalculateMedian(values),
            _ => values.Count
        };
    }

    private double CalculateStdDev(List<double> values)
    {
        if (values.Count == 0) return 0;

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    private double CalculateMedian(List<double> values)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }
        else
        {
            return sorted[mid];
        }
    }

    private string GenerateGeoJSON(List<H3BinResult> results, int resolution)
    {
        var features = new List<object>();

        foreach (var result in results)
        {
            if (result.Boundary == null) continue;

            var properties = new Dictionary<string, object>
            {
                ["h3Index"] = result.H3Index,
                ["count"] = result.Count,
                ["value"] = result.Value,
                ["resolution"] = resolution
            };

            // Add statistics if available
            foreach (var (key, value) in result.Statistics)
            {
                properties[key] = value;
            }

            // Convert polygon to GeoJSON coordinates
            var coordinates = new List<List<double[]>>();
            var ring = new List<double[]>();

            foreach (var coord in result.Boundary.Coordinates)
            {
                ring.Add(new[] { coord.X, coord.Y });
            }
            coordinates.Add(ring);

            features.Add(new
            {
                type = "Feature",
                properties,
                geometry = new
                {
                    type = "Polygon",
                    coordinates
                }
            });
        }

        var featureCollection = new
        {
            type = "FeatureCollection",
            features
        };

        return System.Text.Json.JsonSerializer.Serialize(featureCollection);
    }
}
