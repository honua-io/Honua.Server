// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Processes;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Processes;

/// <summary>
/// Handlers for OGC API - Processes preview endpoints.
/// </summary>
internal static class OgcProcessesPreviewHandlers
{
    /// <summary>
    /// Executes a process in preview mode with optimizations for quick feedback.
    /// </summary>
    public static async Task<IResult> ExecutePreview(
        string processId,
        HttpRequest request,
        [FromBody] ExecuteRequest executeRequest,
        [FromServices] IProcessRegistry processRegistry,
        [FromServices] ProcessPreviewExecutor previewExecutor,
        [FromQuery] bool stream = false,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(processRegistry);
        Guard.NotNull(previewExecutor);

        var process = processRegistry.GetProcess(processId);
        if (process is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-process",
                title = "Process not found",
                detail = $"The process '{processId}' does not exist.",
                status = 404
            });
        }

        // Parse preview options from query parameters
        var options = ParsePreviewOptions(request);

        var previewRequest = new PreviewExecutionRequest
        {
            ProcessId = processId,
            Inputs = executeRequest.Inputs ?? new Dictionary<string, object>(),
            Options = options,
            Stream = stream
        };

        if (stream)
        {
            // Streaming response
            return Results.Stream(async (responseStream) =>
            {
                await StreamPreviewResultsAsync(
                    previewRequest,
                    previewExecutor,
                    responseStream,
                    cancellationToken).ConfigureAwait(false);
            }, contentType: "application/geo+json");
        }
        else
        {
            // Regular response
            var result = await previewExecutor.ExecutePreviewAsync(previewRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    type = "http://honua.io/errors/preview-failed",
                    title = "Preview execution failed",
                    detail = string.Join("; ", result.Metadata.Warnings ?? new List<string>()),
                    status = 400,
                    metadata = result.Metadata
                });
            }

            // Convert results to GeoJSON
            var geoJson = ConvertToGeoJson(result.Results!, result.Metadata);

            return Results.Ok(geoJson);
        }
    }

    /// <summary>
    /// Validates process inputs and returns validation results for preview.
    /// </summary>
    public static Task<IResult> ValidatePreviewInputs(
        string processId,
        HttpRequest request,
        [FromBody] Dictionary<string, object> inputs,
        [FromServices] IProcessRegistry processRegistry,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(processRegistry);

        var process = processRegistry.GetProcess(processId);
        if (process is null)
        {
            return Task.FromResult(Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-process",
                title = "Process not found",
                detail = $"The process '{processId}' does not exist.",
                status = 404
            }));
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var description = process.Description;

        // Validate required inputs
        foreach (var (name, inputDef) in description.Inputs)
        {
            if (!inputs.ContainsKey(name))
            {
                errors.Add($"Required input '{name}' is missing");
                continue;
            }

            var value = inputs[name];

            // Validate specific input types
            switch (name.ToLowerInvariant())
            {
                case "distance" when value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number:
                    var distance = jsonElement.GetDouble();
                    if (distance <= 0)
                    {
                        errors.Add("Buffer distance must be greater than 0");
                    }
                    else if (distance > 10000)
                    {
                        warnings.Add("Large buffer distance may result in slow preview");
                    }
                    break;

                case "geometries" when value is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array:
                    var count = jsonArray.GetArrayLength();
                    if (count == 0)
                    {
                        errors.Add("At least one geometry is required");
                    }
                    else if (count > 1000)
                    {
                        warnings.Add($"Large input ({count} features) will be sampled to 100 for preview");
                    }
                    break;
            }
        }

        var response = new
        {
            valid = errors.Count == 0,
            errors = errors.Any() ? errors : null,
            warnings = warnings.Any() ? warnings : null,
            processId,
            timestamp = DateTimeOffset.UtcNow
        };

        return Task.FromResult(Results.Ok(response));
    }

    /// <summary>
    /// Streams preview results incrementally for large operations.
    /// </summary>
    private static async Task StreamPreviewResultsAsync(
        PreviewExecutionRequest request,
        ProcessPreviewExecutor executor,
        Stream responseStream,
        CancellationToken cancellationToken)
    {
        var writer = new StreamWriter(responseStream, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync("{").ConfigureAwait(false);
        await writer.WriteLineAsync("  \"type\": \"FeatureCollection\",").ConfigureAwait(false);
        await writer.WriteLineAsync("  \"features\": [").ConfigureAwait(false);

        var result = await executor.ExecutePreviewAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.Success && result.Results != null)
        {
            var geometries = ExtractGeometries(result.Results);
            var serializer = GeoJsonSerializer.Create();
            var firstFeature = true;

            foreach (var geometry in geometries)
            {
                if (!firstFeature)
                {
                    await writer.WriteAsync(",").ConfigureAwait(false);
                }

                var feature = new Feature(geometry, new AttributesTable
                {
                    { "preview", true },
                    { "simplified", result.Metadata.Simplified }
                });

                var featureJson = JsonSerializer.Serialize(feature, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.WriteAsync("    ").ConfigureAwait(false);
                await writer.WriteAsync(featureJson).ConfigureAwait(false);

                firstFeature = false;
                await writer.FlushAsync().ConfigureAwait(false);

                // Small delay to demonstrate streaming
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("  ],").ConfigureAwait(false);
        await writer.WriteAsync("  \"metadata\": ").ConfigureAwait(false);
        await writer.WriteAsync(JsonSerializer.Serialize(result.Metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        })).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("}").ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Converts process results to GeoJSON format with preview metadata.
    /// </summary>
    private static object ConvertToGeoJson(Dictionary<string, object> results, PreviewMetadata metadata)
    {
        var geometries = ExtractGeometries(results);
        var features = geometries.Select(geom => new Feature(geom, new AttributesTable
        {
            { "preview", true },
            { "simplified", metadata.Simplified },
            { "spatialSampling", metadata.SpatialSampling }
        })).ToList();

        return new
        {
            type = "FeatureCollection",
            features,
            metadata = new
            {
                preview = metadata.IsPreview,
                totalFeatures = metadata.TotalFeatures,
                previewFeatures = metadata.PreviewFeatures,
                spatialSampling = metadata.SpatialSampling,
                simplified = metadata.Simplified,
                executionTimeMs = metadata.ExecutionTimeMs,
                message = metadata.Message,
                warnings = metadata.Warnings
            },
            style = new
            {
                // Preview-specific styling hints
                fillColor = "#3B82F6",
                fillOpacity = 0.3,
                strokeColor = "#2563EB",
                strokeWidth = 2,
                strokeDashArray = new[] { 5, 5 } // Dashed border for previews
            }
        };
    }

    /// <summary>
    /// Extracts geometries from process results.
    /// </summary>
    private static List<Geometry> ExtractGeometries(Dictionary<string, object> results)
    {
        var geometries = new List<Geometry>();

        foreach (var value in results.Values)
        {
            switch (value)
            {
                case Geometry geom:
                    geometries.Add(geom);
                    break;
                case IEnumerable<Geometry> geoms:
                    geometries.AddRange(geoms);
                    break;
                case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Array:
                    // Handle GeoJSON arrays
                    // This would require parsing GeoJSON, skipping for now
                    break;
            }
        }

        return geometries;
    }

    /// <summary>
    /// Parses preview options from query parameters.
    /// </summary>
    private static PreviewExecutionOptions ParsePreviewOptions(HttpRequest request)
    {
        var options = new PreviewExecutionOptions();

        if (request.Query.TryGetValue("maxFeatures", out var maxFeaturesStr) &&
            int.TryParse(maxFeaturesStr, out var maxFeatures))
        {
            options.MaxPreviewFeatures = Math.Min(maxFeatures, 1000); // Cap at 1000
        }

        if (request.Query.TryGetValue("timeout", out var timeoutStr) &&
            int.TryParse(timeoutStr, out var timeout))
        {
            options.PreviewTimeoutMs = Math.Min(timeout, 30000); // Cap at 30 seconds
        }

        if (request.Query.TryGetValue("spatialSampling", out var samplingStr) &&
            bool.TryParse(samplingStr, out var sampling))
        {
            options.UseSpatialSampling = sampling;
        }

        if (request.Query.TryGetValue("simplify", out var simplifyStr) &&
            bool.TryParse(simplifyStr, out var simplify))
        {
            options.SimplifyGeometries = simplify;
        }

        return options;
    }
}

/// <summary>
/// Execute request model.
/// </summary>
public record ExecuteRequest
{
    public Dictionary<string, object>? Inputs { get; init; }
}
