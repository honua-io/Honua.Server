// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Executes process operations in preview mode with optimizations for quick feedback.
/// </summary>
public sealed class ProcessPreviewExecutor
{
    private readonly IProcessRegistry _processRegistry;
    private readonly PreviewExecutionOptions _defaultOptions;

    public ProcessPreviewExecutor(
        IProcessRegistry processRegistry,
        PreviewExecutionOptions? defaultOptions = null)
    {
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        _defaultOptions = defaultOptions ?? new PreviewExecutionOptions();
    }

    /// <summary>
    /// Executes a process in preview mode.
    /// </summary>
    public async Task<PreviewExecutionResult> ExecutePreviewAsync(
        PreviewExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var process = _processRegistry.GetProcess(request.ProcessId);
        if (process is null)
        {
            throw new InvalidOperationException($"Process '{request.ProcessId}' not found.");
        }

        var options = request.Options ?? _defaultOptions;
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        // Apply preview optimizations to inputs
        var previewInputs = ApplyPreviewOptimizations(request.Inputs, options, warnings);

        // Validate inputs
        var validationErrors = ValidateInputs(process, previewInputs);
        if (validationErrors.Any())
        {
            return new PreviewExecutionResult
            {
                Success = false,
                Metadata = new PreviewMetadata
                {
                    IsPreview = true,
                    PreviewFeatures = 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Warnings = validationErrors.ToList()
                }
            };
        }

        // Create a preview job (lightweight, no persistence)
        var jobId = $"preview-{Guid.NewGuid()}";
        var job = new ProcessJob(jobId, request.ProcessId, previewInputs);

        try
        {
            // Execute with timeout
            using var timeoutCts = new CancellationTokenSource(options.PreviewTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            job.MarkStarted();
            var results = await process.ExecuteAsync(previewInputs, job, linkedCts.Token)
                .ConfigureAwait(false);

            job.MarkCompleted(results);
            stopwatch.Stop();

            // Extract feature count and apply post-processing
            var (processedResults, featureCount) = PostProcessResults(results, options);

            return new PreviewExecutionResult
            {
                Success = true,
                Results = processedResults,
                Metadata = new PreviewMetadata
                {
                    IsPreview = true,
                    PreviewFeatures = featureCount,
                    SpatialSampling = options.UseSpatialSampling,
                    Simplified = options.SimplifyGeometries,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Message = $"Preview showing {featureCount} features. Use full execution for complete results.",
                    Warnings = warnings.Any() ? warnings : null
                }
            };
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            job.MarkFailed("Preview operation timed out");
            return new PreviewExecutionResult
            {
                Success = false,
                Metadata = new PreviewMetadata
                {
                    IsPreview = true,
                    PreviewFeatures = 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Warnings = new List<string> { "Preview operation timed out. Try reducing the input size or use full execution." }
                }
            };
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            return new PreviewExecutionResult
            {
                Success = false,
                Metadata = new PreviewMetadata
                {
                    IsPreview = true,
                    PreviewFeatures = 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Warnings = new List<string> { $"Preview execution failed: {ex.Message}" }
                }
            };
        }
        finally
        {
            job.Dispose();
        }
    }

    /// <summary>
    /// Applies preview optimizations to input parameters.
    /// </summary>
    private Dictionary<string, object> ApplyPreviewOptimizations(
        Dictionary<string, object> inputs,
        PreviewExecutionOptions options,
        List<string> warnings)
    {
        var optimizedInputs = new Dictionary<string, object>(inputs);

        foreach (var (key, value) in inputs)
        {
            // Limit feature collections
            if (value is IEnumerable<Geometry> geometries)
            {
                var geometryList = geometries.ToList();
                if (geometryList.Count > options.MaxPreviewFeatures)
                {
                    var sampled = options.UseSpatialSampling
                        ? SpatialSample(geometryList, options.MaxPreviewFeatures)
                        : geometryList.Take(options.MaxPreviewFeatures).ToList();

                    optimizedInputs[key] = sampled;
                    warnings.Add($"Input '{key}' limited to {options.MaxPreviewFeatures} features (original: {geometryList.Count})");
                }
                else if (options.SimplifyGeometries)
                {
                    optimizedInputs[key] = SimplifyGeometries(geometryList, options.SimplificationTolerance);
                }
            }
            // Limit string arrays (like feature IDs)
            else if (value is IEnumerable<string> stringList)
            {
                var list = stringList.ToList();
                if (list.Count > options.MaxPreviewFeatures)
                {
                    optimizedInputs[key] = list.Take(options.MaxPreviewFeatures).ToList();
                    warnings.Add($"Input '{key}' limited to {options.MaxPreviewFeatures} items (original: {list.Count})");
                }
            }
        }

        return optimizedInputs;
    }

    /// <summary>
    /// Performs spatial sampling to get representative features across the extent.
    /// </summary>
    private List<Geometry> SpatialSample(List<Geometry> geometries, int maxCount)
    {
        if (geometries.Count <= maxCount)
        {
            return geometries;
        }

        // Calculate envelope of all geometries
        var envelope = new Envelope();
        foreach (var geom in geometries)
        {
            envelope.ExpandToInclude(geom.EnvelopeInternal);
        }

        // Divide space into grid cells
        var gridSize = (int)Math.Ceiling(Math.Sqrt(maxCount));
        var cellWidth = envelope.Width / gridSize;
        var cellHeight = envelope.Height / gridSize;

        var sampled = new List<Geometry>();
        var cellMap = new Dictionary<(int, int), Geometry>();

        // Assign geometries to grid cells and keep first one per cell
        foreach (var geom in geometries)
        {
            var center = geom.Centroid;
            var cellX = (int)((center.X - envelope.MinX) / cellWidth);
            var cellY = (int)((center.Y - envelope.MinY) / cellHeight);
            var cell = (Math.Min(cellX, gridSize - 1), Math.Min(cellY, gridSize - 1));

            if (!cellMap.ContainsKey(cell))
            {
                cellMap[cell] = geom;
                sampled.Add(geom);

                if (sampled.Count >= maxCount)
                {
                    break;
                }
            }
        }

        // If we didn't get enough, add more from the original list
        if (sampled.Count < maxCount)
        {
            foreach (var geom in geometries)
            {
                if (!sampled.Contains(geom))
                {
                    sampled.Add(geom);
                    if (sampled.Count >= maxCount)
                    {
                        break;
                    }
                }
            }
        }

        return sampled;
    }

    /// <summary>
    /// Simplifies geometries for faster preview rendering.
    /// </summary>
    private List<Geometry> SimplifyGeometries(List<Geometry> geometries, double toleranceFraction)
    {
        var simplified = new List<Geometry>(geometries.Count);

        foreach (var geom in geometries)
        {
            var envelope = geom.EnvelopeInternal;
            var tolerance = Math.Max(envelope.Width, envelope.Height) * toleranceFraction;

            if (tolerance > 0)
            {
                var simplifiedGeom = DouglasPeuckerSimplifier.Simplify(geom, tolerance);
                simplifiedGeom.SRID = geom.SRID;
                simplified.Add(simplifiedGeom);
            }
            else
            {
                simplified.Add(geom);
            }
        }

        return simplified;
    }

    /// <summary>
    /// Post-processes results to extract metadata and format for preview.
    /// </summary>
    private (Dictionary<string, object>, int) PostProcessResults(
        Dictionary<string, object> results,
        PreviewExecutionOptions options)
    {
        var processed = new Dictionary<string, object>(results);
        var featureCount = 0;

        foreach (var (key, value) in results)
        {
            if (value is IEnumerable<Geometry> geometries)
            {
                var geometryList = geometries.ToList();
                featureCount = geometryList.Count;

                // Apply simplification if not already done
                if (options.SimplifyGeometries)
                {
                    processed[key] = SimplifyGeometries(geometryList, options.SimplificationTolerance);
                }
            }
            else if (value is Geometry)
            {
                featureCount = 1;
            }
        }

        return (processed, featureCount);
    }

    /// <summary>
    /// Validates inputs against process requirements.
    /// </summary>
    private List<string> ValidateInputs(IProcess process, Dictionary<string, object> inputs)
    {
        var errors = new List<string>();
        var description = process.Description;

        foreach (var (name, inputDef) in description.Inputs)
        {
            // Check required inputs
            if (!inputs.ContainsKey(name))
            {
                errors.Add($"Required input '{name}' is missing");
                continue;
            }

            var value = inputs[name];

            // Validate buffer distance
            if (name == "distance" && value is double distance)
            {
                if (distance <= 0)
                {
                    errors.Add("Buffer distance must be greater than 0");
                }
            }

            // Validate geometries are not empty
            if (value is IEnumerable<Geometry> geometries)
            {
                if (!geometries.Any())
                {
                    errors.Add($"Input '{name}' must contain at least one geometry");
                }
            }
        }

        return errors;
    }
}

/// <summary>
/// Represents the result of a preview execution.
/// </summary>
public sealed class PreviewExecutionResult
{
    /// <summary>
    /// Gets or sets whether the preview execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the preview results.
    /// </summary>
    public Dictionary<string, object>? Results { get; init; }

    /// <summary>
    /// Gets or sets the preview metadata.
    /// </summary>
    public required PreviewMetadata Metadata { get; init; }
}
