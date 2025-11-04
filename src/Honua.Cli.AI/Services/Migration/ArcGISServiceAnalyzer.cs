// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Migration;

/// <summary>
/// Service for analyzing ArcGIS REST services and detecting their structure, layers, and capabilities.
/// </summary>
public sealed class ArcGISServiceAnalyzer
{
    /// <summary>
    /// Analyzes an ArcGIS REST service and reports structure, layers, and capabilities.
    /// </summary>
    /// <param name="serviceUrl">ArcGIS REST service URL (e.g., https://server/arcgis/rest/services/MyService/MapServer)</param>
    /// <returns>JSON analysis report</returns>
    public string AnalyzeService(string serviceUrl)
    {
        var analysis = new
        {
            serviceUrl,
            detectedType = serviceUrl.Contains("MapServer") ? "MapServer" :
                          serviceUrl.Contains("FeatureServer") ? "FeatureServer" :
                          serviceUrl.Contains("ImageServer") ? "ImageServer" : "Unknown",
            inspectionSteps = new[]
            {
                new
                {
                    step = 1,
                    action = "Fetch service metadata",
                    command = $"curl '{serviceUrl}?f=json'",
                    dataExtracted = new[]
                    {
                        "Service name and description",
                        "Spatial reference (WKID)",
                        "Supported operations",
                        "Max record count",
                        "Layer list with IDs"
                    }
                },
                new
                {
                    step = 2,
                    action = "Enumerate layers",
                    command = $"curl '{serviceUrl}/layers?f=json'",
                    dataExtracted = new[]
                    {
                        "Layer IDs and names",
                        "Geometry types",
                        "Field schemas",
                        "Min/Max scale ranges"
                    }
                },
                new
                {
                    step = 3,
                    action = "Inspect each layer",
                    command = $"curl '{serviceUrl}/0?f=json'  # For layer 0",
                    dataExtracted = new[]
                    {
                        "Detailed field definitions",
                        "Geometry type and spatial reference",
                        "Capabilities (Query, Create, Update, Delete)",
                        "Drawing info and symbology",
                        "Extent and feature count estimate"
                    }
                },
                new
                {
                    step = 4,
                    action = "Test query capabilities",
                    command = $"curl '{serviceUrl}/0/query?where=1=1&returnCountOnly=true&f=json'",
                    dataExtracted = new[]
                    {
                        "Total feature count",
                        "Query performance baseline",
                        "Supported query formats"
                    }
                }
            },
            keyMetadata = new
            {
                serviceDefinition = new
                {
                    url = serviceUrl,
                    type = "MapServer or FeatureServer",
                    spatialReference = "Extract WKID from JSON response",
                    maxRecordCount = "Default pagination limit",
                    capabilities = "Query, Create, Update, Delete, Extract"
                },
                layers = new[]
                {
                    new
                    {
                        id = 0,
                        name = "Layer name from JSON",
                        geometryType = "esriGeometryPoint/Polyline/Polygon",
                        fields = "Array of field definitions",
                        extent = "Bounding box coordinates",
                        featureCount = "Approximate count from query"
                    }
                }
            },
            compatibilityChecks = new[]
            {
                new
                {
                    check = "Geometry Type Mapping",
                    arcgisTypes = (string[]?)new[] { "esriGeometryPoint", "esriGeometryPolyline", "esriGeometryPolygon" },
                    honuaTypes = (string[]?)new[] { "Point", "LineString", "Polygon" },
                    compatible = true,
                    arcgisSR = (string?)null,
                    honuaCRS = (string?)null,
                    conversion = (string?)null,
                    arcgisQuery = (string?)null,
                    ogcCQL = (string?)null,
                    notes = (string?)null,
                    postgisTypes = (string[]?)null
                },
                new
                {
                    check = "Spatial Reference",
                    arcgisTypes = (string[]?)null,
                    honuaTypes = (string[]?)null,
                    compatible = false,
                    arcgisSR = (string?)"WKID (e.g., 102100 for Web Mercator)",
                    honuaCRS = (string?)"EPSG codes (e.g., 3857 for Web Mercator)",
                    conversion = (string?)"Map WKID to EPSG: 102100 -> 3857, 4326 -> 4326",
                    arcgisQuery = (string?)null,
                    ogcCQL = (string?)null,
                    notes = (string?)null,
                    postgisTypes = (string[]?)null
                },
                new
                {
                    check = "Field Types",
                    arcgisTypes = (string[]?)new[] { "esriFieldTypeString", "esriFieldTypeInteger", "esriFieldTypeDouble", "esriFieldTypeDate" },
                    honuaTypes = (string[]?)null,
                    compatible = true,
                    arcgisSR = (string?)null,
                    honuaCRS = (string?)null,
                    conversion = (string?)null,
                    arcgisQuery = (string?)null,
                    ogcCQL = (string?)null,
                    notes = (string?)null,
                    postgisTypes = (string[]?)new[] { "VARCHAR", "INTEGER", "DOUBLE PRECISION", "TIMESTAMP" }
                },
                new
                {
                    check = "Query Capabilities",
                    arcgisTypes = (string[]?)null,
                    honuaTypes = (string[]?)null,
                    compatible = false,
                    arcgisSR = (string?)null,
                    honuaCRS = (string?)null,
                    conversion = (string?)null,
                    arcgisQuery = (string?)"WHERE clause, geometry filters, spatial relationships",
                    ogcCQL = (string?)"CQL filter expressions, bbox parameter",
                    notes = (string?)"Syntax differences require translation",
                    postgisTypes = (string[]?)null
                }
            },
            automatedAnalysisCommand = new
            {
                tool = "esri2geojson or ogr2ogr",
                command = $"ogr2ogr -f GeoJSON output.geojson '{serviceUrl}/0/query?where=1=1&outFields=*&f=geojson'",
                benefit = "Direct export to GeoJSON for schema inspection"
            }
        };

        return JsonSerializer.Serialize(new
        {
            analysis,
            nextSteps = new[]
            {
                "Document all layers, field schemas, and geometry types",
                "Map WKID spatial references to EPSG codes",
                "Identify custom domains and coded values for translation",
                "Estimate data volume and migration time",
                "Create migration plan with layer prioritization"
            },
            tools = new[]
            {
                new { tool = "ArcGIS REST API Documentation", url = (string?)"https://developers.arcgis.com/rest/", command = (string?)null, usage = (string?)null },
                new { tool = "GDAL/OGR with ArcGIS support", url = (string?)null, command = (string?)"ogr2ogr --formats | grep -i esri", usage = (string?)null },
                new { tool = "esri2geojson", url = (string?)"https://github.com/feomike/esri2geojson", command = (string?)null, usage = (string?)null },
                new { tool = "Postman ArcGIS Collection", url = (string?)null, command = (string?)null, usage = (string?)"Test REST endpoints interactively" }
            }
        }, CliJsonOptions.Indented);
    }
}
