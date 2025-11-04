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
/// Process that buffers geometries by a specified distance.
/// </summary>
public sealed class BufferProcess : IProcess
{
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();

    public ProcessDescription Description { get; } = new ProcessDescription
    {
        Id = "buffer",
        Version = "1.0.0",
        Title = "Buffer Geometry",
        Description = "Creates a buffer polygon around a geometry at a specified distance.",
        Keywords = new List<string> { "geometry", "buffer", "spatial" },
        JobControlOptions = new List<string> { "sync-execute", "async-execute" },
        OutputTransmission = new List<string> { "value", "reference" },
        Inputs = new Dictionary<string, ProcessInput>
        {
            ["geometry"] = new ProcessInput
            {
                Title = "Input Geometry",
                Description = "The geometry to buffer (GeoJSON)",
                Schema = new
                {
                    type = "object",
                    contentMediaType = "application/geo+json"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            },
            ["distance"] = new ProcessInput
            {
                Title = "Buffer Distance",
                Description = "The buffer distance in the units of the geometry's coordinate system",
                Schema = new
                {
                    type = "number",
                    minimum = 0
                },
                MinOccurs = 1,
                MaxOccurs = 1
            },
            ["segments"] = new ProcessInput
            {
                Title = "Quadrant Segments",
                Description = "Number of segments per quadrant (default: 8)",
                Schema = new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = 100,
                    @default = 8
                },
                MinOccurs = 0,
                MaxOccurs = 1
            }
        },
        Outputs = new Dictionary<string, ProcessOutput>
        {
            ["result"] = new ProcessOutput
            {
                Title = "Buffered Geometry",
                Description = "The resulting buffered geometry (GeoJSON)",
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
        if (inputs is null || !inputs.ContainsKey("geometry") || !inputs.ContainsKey("distance"))
        {
            throw new ArgumentException("Missing required inputs: geometry and distance");
        }

        job.UpdateProgress(10, "Parsing input geometry");

        // Parse geometry
        var geometryJson = JsonSerializer.Serialize(inputs["geometry"]);
        var geometry = _geoJsonReader.Read<Geometry>(geometryJson);

        job.UpdateProgress(30, "Parsing buffer parameters");

        // Parse distance
        var distance = Convert.ToDouble(inputs["distance"]);
        if (distance < 0)
        {
            throw new ArgumentException("Distance must be non-negative");
        }

        // Parse optional segments parameter
        var segments = 8;
        if (inputs.TryGetValue("segments", out var segmentsValue))
        {
            segments = Convert.ToInt32(segmentsValue);
            if (segments < 1 || segments > 100)
            {
                throw new ArgumentException("Segments must be between 1 and 100");
            }
        }

        job.UpdateProgress(50, "Computing buffer");

        // Perform buffer operation
        await Task.Yield(); // Allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        var buffered = geometry.Buffer(distance, segments);

        job.UpdateProgress(80, "Serializing result");

        // Serialize result
        var resultGeoJson = _geoJsonWriter.Write(buffered);

        job.UpdateProgress(100, "Complete");

        return new Dictionary<string, object>
        {
            ["result"] = JsonSerializer.Deserialize<object>(resultGeoJson)!
        };
    }
}
