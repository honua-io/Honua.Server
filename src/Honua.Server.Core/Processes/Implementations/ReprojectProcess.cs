// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Honua.Server.Core.Processes.Implementations;

/// <summary>
/// Process that reprojects a geometry from one CRS to another.
/// </summary>
public sealed class ReprojectProcess : IProcess
{
    private static readonly GeoJsonReader _geoJsonReader = new();
    private static readonly GeoJsonWriter _geoJsonWriter = new();
    private static readonly CoordinateSystemFactory _csFactory = new();

    public ProcessDescription Description { get; } = new ProcessDescription
    {
        Id = "reproject",
        Version = "1.0.0",
        Title = "Reproject Geometry",
        Description = "Reprojects a geometry from one coordinate reference system to another.",
        Keywords = new List<string> { "geometry", "reproject", "transform", "crs", "spatial" },
        JobControlOptions = new List<string> { "sync-execute", "async-execute" },
        OutputTransmission = new List<string> { "value", "reference" },
        Inputs = new Dictionary<string, ProcessInput>
        {
            ["geometry"] = new ProcessInput
            {
                Title = "Input Geometry",
                Description = "The geometry to reproject (GeoJSON)",
                Schema = new
                {
                    type = "object",
                    contentMediaType = "application/geo+json"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            },
            ["sourceCrs"] = new ProcessInput
            {
                Title = "Source CRS",
                Description = "The source coordinate reference system (EPSG code, e.g., 'EPSG:4326')",
                Schema = new
                {
                    type = "string",
                    pattern = "^EPSG:\\d+$"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            },
            ["targetCrs"] = new ProcessInput
            {
                Title = "Target CRS",
                Description = "The target coordinate reference system (EPSG code, e.g., 'EPSG:3857')",
                Schema = new
                {
                    type = "string",
                    pattern = "^EPSG:\\d+$"
                },
                MinOccurs = 1,
                MaxOccurs = 1
            }
        },
        Outputs = new Dictionary<string, ProcessOutput>
        {
            ["result"] = new ProcessOutput
            {
                Title = "Reprojected Geometry",
                Description = "The geometry reprojected to the target CRS (GeoJSON)",
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
        if (inputs is null || !inputs.ContainsKey("geometry") || !inputs.ContainsKey("sourceCrs") || !inputs.ContainsKey("targetCrs"))
        {
            throw new ArgumentException("Missing required inputs: geometry, sourceCrs, and targetCrs");
        }

        job.UpdateProgress(10, "Parsing input geometry");

        // Parse geometry
        var geometryJson = JsonSerializer.Serialize(inputs["geometry"]);
        var geometry = _geoJsonReader.Read<Geometry>(geometryJson);

        job.UpdateProgress(30, "Parsing CRS parameters");

        // Parse CRS codes
        var sourceCrsStr = inputs["sourceCrs"]?.ToString() ?? throw new ArgumentException("Invalid sourceCrs");
        var targetCrsStr = inputs["targetCrs"]?.ToString() ?? throw new ArgumentException("Invalid targetCrs");

        if (!TryParseEpsgCode(sourceCrsStr, out var sourceEpsg))
        {
            throw new ArgumentException($"Invalid source CRS format: {sourceCrsStr}");
        }

        if (!TryParseEpsgCode(targetCrsStr, out var targetEpsg))
        {
            throw new ArgumentException($"Invalid target CRS format: {targetCrsStr}");
        }

        // Skip transformation if source and target are the same
        if (sourceEpsg == targetEpsg)
        {
            job.UpdateProgress(100, "No transformation needed (source equals target)");
            return new Dictionary<string, object>
            {
                ["result"] = JsonSerializer.Deserialize<object>(geometryJson)!
            };
        }

        job.UpdateProgress(50, $"Transforming from EPSG:{sourceEpsg} to EPSG:{targetEpsg}");

        await Task.Yield(); // Allow cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        // Perform reprojection
        // Note: In a real implementation, you would use a proper CRS transformation library
        // For this example, we'll create a simple transformation
        var transformed = TransformGeometry(geometry, sourceEpsg, targetEpsg);

        job.UpdateProgress(80, "Serializing result");

        // Serialize result
        var resultGeoJson = _geoJsonWriter.Write(transformed);

        job.UpdateProgress(100, "Complete");

        return new Dictionary<string, object>
        {
            ["result"] = JsonSerializer.Deserialize<object>(resultGeoJson)!
        };
    }

    private static bool TryParseEpsgCode(string crsString, out int epsgCode)
    {
        epsgCode = 0;
        if (string.IsNullOrWhiteSpace(crsString))
        {
            return false;
        }

        var parts = crsString.Split(':');
        if (parts.Length != 2 || !parts[0].Equals("EPSG", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(parts[1], out epsgCode);
    }

    private static Geometry TransformGeometry(Geometry geometry, int sourceEpsg, int targetEpsg)
    {
        // For demonstration purposes, we'll implement basic WGS84 <-> Web Mercator transformation
        // In a production system, use a proper library like ProjNet or GDAL

        if (sourceEpsg == 4326 && targetEpsg == 3857)
        {
            // WGS84 to Web Mercator
            return TransformWgs84ToWebMercator(geometry);
        }
        else if (sourceEpsg == 3857 && targetEpsg == 4326)
        {
            // Web Mercator to WGS84
            return TransformWebMercatorToWgs84(geometry);
        }
        else
        {
            throw new NotSupportedException($"Transformation from EPSG:{sourceEpsg} to EPSG:{targetEpsg} is not supported in this demo implementation");
        }
    }

    private static Geometry TransformWgs84ToWebMercator(Geometry geometry)
    {
        var factory = geometry.Factory;
        var coords = geometry.Coordinates;
        var transformed = new Coordinate[coords.Length];

        for (int i = 0; i < coords.Length; i++)
        {
            var lon = coords[i].X;
            var lat = coords[i].Y;

            var x = lon * 20037508.34 / 180.0;
            var y = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0);
            y = y * 20037508.34 / 180.0;

            transformed[i] = new Coordinate(x, y);
        }

        return factory.CreateGeometry(geometry).Copy();
    }

    private static Geometry TransformWebMercatorToWgs84(Geometry geometry)
    {
        var factory = geometry.Factory;
        var coords = geometry.Coordinates;
        var transformed = new Coordinate[coords.Length];

        for (int i = 0; i < coords.Length; i++)
        {
            var x = coords[i].X;
            var y = coords[i].Y;

            var lon = (x / 20037508.34) * 180.0;
            var lat = (y / 20037508.34) * 180.0;
            lat = 180.0 / Math.PI * (2.0 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);

            transformed[i] = new Coordinate(lon, lat);
        }

        return factory.CreateGeometry(geometry).Copy();
    }
}
