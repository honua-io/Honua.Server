#!/usr/bin/env dotnet-script
#r "nuget: System.Text.Json, 9.0.0"

using System;
using System.IO;
using System.Text.Json;
using System.Globalization;

var connectionString = "Data Source=/tmp/test.db";
var metadata = new
{
    catalog = new { id = "honua-sample", title = "Honua Sample Catalog" },
    folders = new[] { new { id = "transportation" } },
    dataSources = new[]
    {
        new { id = "sqlite-primary", provider = "sqlite", connectionString }
    },
    services = new[]
    {
        new
        {
            id = "roads",
            title = "Road Centerlines",
            folderId = "transportation",
            serviceType = "feature",
            dataSourceId = "sqlite-primary",
            enabled = true,
            ogc = new { collectionsEnabled = true, itemLimit = 1000 }
        }
    },
    layers = new[]
    {
        new
        {
            id = "roads-primary",
            serviceId = "roads",
            title = "Primary Roads",
            geometryType = "LineString",
            idField = "road_id",
            displayField = "name",
            geometryField = "geom",
            itemType = "feature",
            fields = new object[]
            {
                new { name = "road_id", type = "int", dataType = "int", storageType = "int", nullable = false },
                new { name = "name", type = "string", dataType = "string" },
                new { name = "status", type = "string", dataType = "string" },
                new { name = "observed_at", type = "datetimeoffset", dataType = "datetime" },
                new { name = "geom", type = "geometry", dataType = "geometry" }
            },
            storage = new
            {
                table = "roads_primary",
                geometryColumn = "geom",
                primaryKey = "road_id",
                srid = 4326
            }
        }
    },
    server = new
    {
        allowedHosts = new[] { "*" }
    }
};

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    MaxDepth = 64,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
};

var json = JsonSerializer.Serialize(metadata, options);
Console.WriteLine(json);

// Count lines to see where line 46 would be
var lines = json.Split('\n');
for (int i = 0; i < Math.Min(50, lines.Length); i++)
{
    Console.WriteLine($"Line {i + 1}: {lines[i]}");
}
