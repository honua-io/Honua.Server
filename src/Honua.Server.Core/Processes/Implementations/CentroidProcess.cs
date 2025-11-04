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
/// Process that computes the centroid of a geometry.
/// </summary>
public sealed class CentroidProcess : IProcess
{
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();

    public ProcessDescription Description { get; } = new ProcessDescription
    {
        Id = "centroid",
        Version = "1.0.0",
        Title = "Compute Centroid",
        Description = "Computes the geometric centroid (center of mass) of a geometry.",
        Keywords = new List<string> { "geometry", "centroid", "spatial", "center" },
        JobControlOptions = new List<string> { "sync-execute", "async-execute" },
        OutputTransmission = new List<string> { "value", "reference" },
        Inputs = new Dictionary<string, ProcessInput>
        {
            ["geometry"] = new ProcessInput
            {
                Title = "Input Geometry",
                Description = "The geometry to compute the centroid for (GeoJSON)",
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
                Title = "Centroid Point",
                Description = "The centroid as a point geometry (GeoJSON)",
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
        if (inputs is null || !inputs.ContainsKey("geometry"))
        {
            throw new ArgumentException("Missing required input: geometry");
        }

        job.UpdateProgress(20, "Parsing input geometry");

        // Parse geometry
        var geometryJson = JsonSerializer.Serialize(inputs["geometry"]);
        var geometry = _geoJsonReader.Read<Geometry>(geometryJson);

        job.UpdateProgress(50, "Computing centroid");

        // Perform centroid operation
        await Task.Yield(); // Allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        var centroid = geometry.Centroid;

        job.UpdateProgress(80, "Serializing result");

        // Serialize result
        var resultGeoJson = _geoJsonWriter.Write(centroid);

        job.UpdateProgress(100, "Complete");

        return new Dictionary<string, object>
        {
            ["result"] = JsonSerializer.Deserialize<object>(resultGeoJson)!
        };
    }
}
