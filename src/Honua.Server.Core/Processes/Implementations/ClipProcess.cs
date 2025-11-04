// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Processes.Implementations;

/// <summary>
/// Process that clips a geometry by another geometry.
/// </summary>
public sealed class ClipProcess : IProcess
{
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();

    public ProcessDescription Description { get; } = new ProcessDescription
    {
        Id = "clip",
        Version = "1.0.0",
        Title = "Clip Geometry",
        Description = "Clips a geometry by another geometry (intersection operation).",
        Keywords = new List<string> { "geometry", "clip", "intersect", "spatial" },
        JobControlOptions = new List<string> { "sync-execute", "async-execute" },
        OutputTransmission = new List<string> { "value", "reference" },
        Inputs = new Dictionary<string, ProcessInput>
        {
            ["geometry"] = new ProcessInput
            {
                Title = "Input Geometry",
                Description = "The geometry to clip (GeoJSON)",
                Schema = new
                {
                    type = "object",
                    contentMediaType = "application/geo+json"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            },
            ["clipGeometry"] = new ProcessInput
            {
                Title = "Clip Geometry",
                Description = "The geometry to clip by (GeoJSON)",
                Schema = new
                {
                    type = "object",
                    contentMediaType = "application/geo+json"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            }
        },
        Outputs = new Dictionary<string, ProcessOutput>
        {
            ["result"] = new ProcessOutput
            {
                Title = "Clipped Geometry",
                Description = "The resulting clipped geometry (GeoJSON)",
                Schema = new
                {
                    type = "object",
                    contentMediaType = "application/geo+json"
                }
            }
        }
    };

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object>? inputs,
        ProcessJob job,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null || !inputs.ContainsKey("geometry") || !inputs.ContainsKey("clipGeometry"))
        {
            throw new ArgumentException("Missing required inputs: geometry and clipGeometry");
        }

        job.UpdateProgress(20, "Parsing input geometries");

        // Parse input geometry
        var geometryJson = JsonSerializer.Serialize(inputs["geometry"]);
        var geometry = _geoJsonReader.Read<Geometry>(geometryJson);

        // Parse clip geometry
        var clipGeometryJson = JsonSerializer.Serialize(inputs["clipGeometry"]);
        var clipGeometry = _geoJsonReader.Read<Geometry>(clipGeometryJson);

        job.UpdateProgress(50, "Computing intersection");

        // Perform intersection (clip) operation
        await Task.Yield(); // Allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        var result = geometry.Intersection(clipGeometry);

        job.UpdateProgress(80, "Serializing result");

        // Serialize result
        var resultGeoJson = _geoJsonWriter.Write(result);

        job.UpdateProgress(100, "Complete");

        return new Dictionary<string, object>
        {
            ["result"] = JsonSerializer.Deserialize<object>(resultGeoJson)!
        };
    }
}
