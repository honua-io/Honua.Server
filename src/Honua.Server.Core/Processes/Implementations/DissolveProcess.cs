// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;

namespace Honua.Server.Core.Processes.Implementations;

/// <summary>
/// Process that dissolves (unions) multiple geometries into a single geometry.
/// </summary>
public sealed class DissolveProcess : IProcess
{
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();

    public ProcessDescription Description { get; } = new ProcessDescription
    {
        Id = "dissolve",
        Version = "1.0.0",
        Title = "Dissolve Geometries",
        Description = "Dissolves (unions) multiple geometries into a single geometry, removing internal boundaries.",
        Keywords = new List<string> { "geometry", "dissolve", "union", "merge", "spatial" },
        JobControlOptions = new List<string> { "sync-execute", "async-execute" },
        OutputTransmission = new List<string> { "value", "reference" },
        Inputs = new Dictionary<string, ProcessInput>
        {
            ["geometries"] = new ProcessInput
            {
                Title = "Input Geometries",
                Description = "Array of geometries to dissolve (GeoJSON)",
                Schema = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        contentMediaType = "application/geo+json"
                    },
                    minItems = 1
                },
                MinOccurs = 1,
                MaxOccurs = 1
            }
        },
        Outputs = new Dictionary<string, ProcessOutput>
        {
            ["result"] = new ProcessOutput
            {
                Title = "Dissolved Geometry",
                Description = "The resulting dissolved geometry (GeoJSON)",
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
        if (inputs is null || !inputs.ContainsKey("geometries"))
        {
            throw new ArgumentException("Missing required input: geometries");
        }

        job.UpdateProgress(10, "Parsing input geometries");

        // Parse geometries array
        var geometriesInput = inputs["geometries"];
        List<Geometry> geometries;

        if (geometriesInput is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            geometries = new List<Geometry>();
            foreach (var item in jsonElement.EnumerateArray())
            {
                var itemJson = JsonSerializer.Serialize(item);
                var geometry = _geoJsonReader.Read<Geometry>(itemJson);
                geometries.Add(geometry);
            }
        }
        else if (geometriesInput is IEnumerable<object> enumerable)
        {
            geometries = enumerable.Select(obj =>
            {
                var json = JsonSerializer.Serialize(obj);
                return _geoJsonReader.Read<Geometry>(json);
            }).ToList();
        }
        else
        {
            throw new ArgumentException("Invalid geometries format");
        }

        if (geometries.Count == 0)
        {
            throw new ArgumentException("At least one geometry is required");
        }

        job.UpdateProgress(30, $"Dissolving {geometries.Count} geometries");

        // Perform union operation
        await Task.Yield(); // Allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        Geometry result;
        if (geometries.Count == 1)
        {
            result = geometries[0];
        }
        else
        {
            // Use CascadedPolygonUnion for better performance with many polygons
            var unionOp = new CascadedPolygonUnion(geometries);
            result = unionOp.Union();
        }

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
