// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Data sink node for exporting to GeoPackage format
/// </summary>
public sealed class GeoPackageSinkNode : WorkflowNodeBase
{
    private readonly IGeoPackageExporter _exporter;

    public GeoPackageSinkNode(
        IGeoPackageExporter exporter,
        ILogger<GeoPackageSinkNode> logger)
        : base(logger)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public override string NodeType => "data_sink.geopackage";
    public override string DisplayName => "GeoPackage Export";
    public override string Description => "Exports features to GeoPackage (.gpkg) format";
    public override string Category => "Data Sinks";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = context.GetParameter<string>("output_path");
        var tableName = context.GetParameter<string>("table_name", "features");
        var crs = context.GetParameter<string>("crs", "EPSG:4326");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return NodeExecutionResult.Fail("Parameter 'output_path' is required");
        }

        // Get features from input
        var features = context.GetInputData<List<IFeature>>("features");
        if (features == null || features.Count == 0)
        {
            return NodeExecutionResult.Fail("No features provided for export");
        }

        try
        {
            // Create a temporary layer definition for export
            var layer = CreateLayerDefinition(features, tableName);

            // Convert features to FeatureRecord enumerable
            var records = ConvertToFeatureRecords(features);

            // Export using the existing GeoPackage exporter
            var result = await _exporter.ExportAsync(
                layer,
                null,
                crs,
                records,
                cancellationToken);

            // Save to specified path
            await using (var outputFile = File.Create(outputPath))
            {
                await result.Content.CopyToAsync(outputFile, cancellationToken);
            }

            await result.Content.DisposeAsync();

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["output_path"] = outputPath,
                ["feature_count"] = result.FeatureCount,
                ["format"] = "GeoPackage"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to GeoPackage: {OutputPath}", outputPath);
            return NodeExecutionResult.Fail($"Failed to export to GeoPackage: {ex.Message}");
        }
    }

    private LayerDefinition CreateLayerDefinition(List<IFeature> features, string tableName)
    {
        var fields = new List<FieldDefinition>();

        // Infer schema from first feature
        if (features.Count > 0)
        {
            var firstFeature = features[0];
            if (firstFeature.Attributes != null)
            {
                foreach (var name in firstFeature.Attributes.GetNames())
                {
                    var value = firstFeature.Attributes[name];
                    var fieldType = InferFieldType(value);

                    fields.Add(new FieldDefinition
                    {
                        Name = name,
                        Type = fieldType,
                        Nullable = true
                    });
                }
            }
        }

        return new LayerDefinition
        {
            Id = tableName,
            Title = tableName,
            GeometryField = "geom",
            GeometryType = features.FirstOrDefault()?.Geometry?.GeometryType ?? "Geometry",
            Fields = fields,
            Storage = new StorageDefinition
            {
                Table = tableName,
                Schema = "public"
            }
        };
    }

    private string InferFieldType(object? value)
    {
        return value switch
        {
            null => "string",
            int => "integer",
            long => "integer",
            float => "float",
            double => "float",
            bool => "boolean",
            DateTime => "datetime",
            _ => "string"
        };
    }

    private async IAsyncEnumerable<Core.Data.FeatureRecord> ConvertToFeatureRecords(List<IFeature> features)
    {
        foreach (var feature in features)
        {
            var properties = new Dictionary<string, object?>();

            if (feature.Attributes != null)
            {
                foreach (var name in feature.Attributes.GetNames())
                {
                    properties[name] = feature.Attributes[name];
                }
            }

            // Add geometry as GeoJSON
            if (feature.Geometry != null)
            {
                var writer = new GeoJsonWriter();
                var geoJson = writer.Write(feature.Geometry);
                properties["geom"] = geoJson;
            }

            yield return new Core.Data.FeatureRecord(properties);
            await Task.CompletedTask; // Make it async
        }
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("output_path"))
        {
            errors.Add("Parameter 'output_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data sink node for exporting to Shapefile format
/// </summary>
public sealed class ShapefileSinkNode : WorkflowNodeBase
{
    private readonly IShapefileExporter _exporter;

    public ShapefileSinkNode(
        IShapefileExporter exporter,
        ILogger<ShapefileSinkNode> logger)
        : base(logger)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public override string NodeType => "data_sink.shapefile";
    public override string DisplayName => "Shapefile Export";
    public override string Description => "Exports features to Shapefile (.shp) format as a ZIP archive";
    public override string Category => "Data Sinks";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = context.GetParameter<string>("output_path");
        var crs = context.GetParameter<string>("crs", "EPSG:4326");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return NodeExecutionResult.Fail("Parameter 'output_path' is required");
        }

        // Ensure .zip extension for shapefile output
        if (!outputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            outputPath += ".zip";
        }

        // Get features from input
        var features = context.GetInputData<List<IFeature>>("features");
        if (features == null || features.Count == 0)
        {
            return NodeExecutionResult.Fail("No features provided for export");
        }

        try
        {
            // Create a temporary layer definition for export
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            var layer = CreateLayerDefinition(features, baseName);

            // Convert features to FeatureRecord enumerable
            var records = ConvertToFeatureRecords(features);

            // Export using the existing Shapefile exporter
            var result = await _exporter.ExportAsync(
                layer,
                null,
                crs,
                records,
                cancellationToken);

            // Save to specified path
            await using (var outputFile = File.Create(outputPath))
            {
                await result.Content.CopyToAsync(outputFile, cancellationToken);
            }

            await result.Content.DisposeAsync();

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["output_path"] = outputPath,
                ["feature_count"] = result.FeatureCount,
                ["format"] = "Shapefile (ZIP)"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to Shapefile: {OutputPath}", outputPath);
            return NodeExecutionResult.Fail($"Failed to export to Shapefile: {ex.Message}");
        }
    }

    private LayerDefinition CreateLayerDefinition(List<IFeature> features, string layerId)
    {
        var fields = new List<FieldDefinition>();

        // Infer schema from first feature
        if (features.Count > 0)
        {
            var firstFeature = features[0];
            if (firstFeature.Attributes != null)
            {
                foreach (var name in firstFeature.Attributes.GetNames())
                {
                    var value = firstFeature.Attributes[name];
                    var fieldType = InferFieldType(value);

                    fields.Add(new FieldDefinition
                    {
                        Name = name,
                        Type = fieldType,
                        Nullable = true
                    });
                }
            }
        }

        return new LayerDefinition
        {
            Id = layerId,
            Title = layerId,
            GeometryField = "geom",
            GeometryType = features.FirstOrDefault()?.Geometry?.GeometryType ?? "Geometry",
            Fields = fields,
            Storage = new StorageDefinition
            {
                Table = layerId,
                Schema = "public"
            }
        };
    }

    private string InferFieldType(object? value)
    {
        return value switch
        {
            null => "string",
            int => "integer",
            long => "integer",
            float => "float",
            double => "float",
            bool => "boolean",
            DateTime => "datetime",
            _ => "string"
        };
    }

    private async IAsyncEnumerable<Core.Data.FeatureRecord> ConvertToFeatureRecords(List<IFeature> features)
    {
        foreach (var feature in features)
        {
            var properties = new Dictionary<string, object?>();

            if (feature.Attributes != null)
            {
                foreach (var name in feature.Attributes.GetNames())
                {
                    properties[name] = feature.Attributes[name];
                }
            }

            // Add geometry as GeoJSON
            if (feature.Geometry != null)
            {
                var writer = new GeoJsonWriter();
                var geoJson = writer.Write(feature.Geometry);
                properties["geom"] = geoJson;
            }

            yield return new Core.Data.FeatureRecord(properties);
            await Task.CompletedTask; // Make it async
        }
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("output_path"))
        {
            errors.Add("Parameter 'output_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data sink node for exporting to CSV with geometry
/// </summary>
public sealed class CsvGeometrySinkNode : WorkflowNodeBase
{
    public CsvGeometrySinkNode(ILogger<CsvGeometrySinkNode> logger) : base(logger) { }

    public override string NodeType => "data_sink.csv_geometry";
    public override string DisplayName => "CSV with Geometry Export";
    public override string Description => "Exports features to CSV files with WKT, WKB, or lat/lon columns";
    public override string Category => "Data Sinks";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = context.GetParameter<string>("output_path");
        var geometryFormat = context.GetParameter<string>("geometry_format", "WKT");
        var delimiter = context.GetParameter<string>("delimiter", ",");
        var quoteChar = context.GetParameter<string>("quote_char", "\"");
        var hasHeader = context.GetParameter<bool>("has_header", true);
        var geometryColumn = context.GetParameter<string>("geometry_column", "geometry");
        var latColumn = context.GetParameter<string>("lat_column", "latitude");
        var lonColumn = context.GetParameter<string>("lon_column", "longitude");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return NodeExecutionResult.Fail("Parameter 'output_path' is required");
        }

        // Get features from input
        var features = context.GetInputData<List<IFeature>>("features");
        if (features == null || features.Count == 0)
        {
            return NodeExecutionResult.Fail("No features provided for export");
        }

        try
        {
            await Task.Run(() => WriteCsvWithGeometry(
                outputPath, features, geometryFormat, delimiter[0],
                quoteChar[0], hasHeader, geometryColumn, latColumn, lonColumn), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["output_path"] = outputPath,
                ["feature_count"] = features.Count,
                ["format"] = $"CSV ({geometryFormat})"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to CSV: {OutputPath}", outputPath);
            return NodeExecutionResult.Fail($"Failed to export to CSV: {ex.Message}");
        }
    }

    private void WriteCsvWithGeometry(
        string outputPath,
        List<IFeature> features,
        string geometryFormat,
        char delimiter,
        char quoteChar,
        bool hasHeader,
        string geometryColumn,
        string latColumn,
        string lonColumn)
    {
        var wktWriter = new WKTWriter();
        var wkbWriter = new WKBWriter();
        var lines = new List<string>();

        if (features.Count == 0)
        {
            File.WriteAllLines(outputPath, lines);
            return;
        }

        // Determine field names from first feature
        var firstFeature = features[0];
        var fieldNames = new List<string>();

        if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
        {
            fieldNames.Add(latColumn);
            fieldNames.Add(lonColumn);
        }
        else
        {
            fieldNames.Add(geometryColumn);
        }

        if (firstFeature.Attributes != null)
        {
            foreach (var name in firstFeature.Attributes.GetNames())
            {
                fieldNames.Add(name);
            }
        }

        // Write header
        if (hasHeader)
        {
            lines.Add(string.Join(delimiter.ToString(), fieldNames));
        }

        // Write features
        foreach (var feature in features)
        {
            var values = new List<string>();

            // Add geometry
            if (feature.Geometry != null)
            {
                if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract lat/lon from point geometry
                    if (feature.Geometry is NetTopologySuite.Geometries.Point point)
                    {
                        values.Add(point.Y.ToString("F8")); // Latitude
                        values.Add(point.X.ToString("F8")); // Longitude
                    }
                    else
                    {
                        // Use centroid for non-point geometries
                        var centroid = feature.Geometry.Centroid;
                        values.Add(centroid.Y.ToString("F8"));
                        values.Add(centroid.X.ToString("F8"));
                    }
                }
                else if (geometryFormat.Equals("WKT", StringComparison.OrdinalIgnoreCase))
                {
                    var wkt = wktWriter.Write(feature.Geometry);
                    values.Add($"{quoteChar}{wkt}{quoteChar}");
                }
                else if (geometryFormat.Equals("WKB", StringComparison.OrdinalIgnoreCase))
                {
                    var wkb = wkbWriter.Write(feature.Geometry);
                    var wkbHex = WKBWriter.ToHex(wkb);
                    values.Add($"{quoteChar}{wkbHex}{quoteChar}");
                }
            }
            else
            {
                if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add("");
                    values.Add("");
                }
                else
                {
                    values.Add("");
                }
            }

            // Add attributes
            if (firstFeature.Attributes != null)
            {
                foreach (var name in firstFeature.Attributes.GetNames())
                {
                    var value = feature.Attributes?[name]?.ToString() ?? "";
                    // Quote if contains delimiter or quote char
                    if (value.Contains(delimiter) || value.Contains(quoteChar))
                    {
                        value = $"{quoteChar}{value.Replace(quoteChar.ToString(), $"{quoteChar}{quoteChar}")}{quoteChar}";
                    }
                    values.Add(value);
                }
            }

            lines.Add(string.Join(delimiter.ToString(), values));
        }

        File.WriteAllLines(outputPath, lines);
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("output_path"))
        {
            errors.Add("Parameter 'output_path' is required");
        }

        var geometryFormat = nodeDefinition.Parameters.ContainsKey("geometry_format")
            ? nodeDefinition.Parameters["geometry_format"]?.ToString()
            : "WKT";

        if (!string.IsNullOrEmpty(geometryFormat) &&
            !new[] { "WKT", "WKB", "LatLon" }.Contains(geometryFormat, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Parameter 'geometry_format' must be 'WKT', 'WKB', or 'LatLon'");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data sink node for exporting to GPX format
/// </summary>
public sealed class GpxSinkNode : WorkflowNodeBase
{
    public GpxSinkNode(ILogger<GpxSinkNode> logger) : base(logger) { }

    public override string NodeType => "data_sink.gpx";
    public override string DisplayName => "GPX Export";
    public override string Description => "Exports features as GPX waypoints, tracks, or routes";
    public override string Category => "Data Sinks";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = context.GetParameter<string>("output_path");
        var featureType = context.GetParameter<string>("feature_type", "waypoints");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return NodeExecutionResult.Fail("Parameter 'output_path' is required");
        }

        // Get features from input
        var features = context.GetInputData<List<IFeature>>("features");
        if (features == null || features.Count == 0)
        {
            return NodeExecutionResult.Fail("No features provided for export");
        }

        try
        {
            await Task.Run(() => WriteGpx(outputPath, features, featureType), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["output_path"] = outputPath,
                ["feature_count"] = features.Count,
                ["format"] = "GPX",
                ["feature_type"] = featureType
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to GPX: {OutputPath}", outputPath);
            return NodeExecutionResult.Fail($"Failed to export to GPX: {ex.Message}");
        }
    }

    private void WriteGpx(string outputPath, List<IFeature> features, string featureType)
    {
        var gpxNamespace = System.Xml.Linq.XNamespace.Get("http://www.topografix.com/GPX/1/1");
        var doc = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null),
            new System.Xml.Linq.XElement(gpxNamespace + "gpx",
                new System.Xml.Linq.XAttribute("version", "1.1"),
                new System.Xml.Linq.XAttribute("creator", "Honua GeoETL"),
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation",
                    "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                new System.Xml.Linq.XElement(gpxNamespace + "metadata",
                    new System.Xml.Linq.XElement(gpxNamespace + "name", "Honua Export"),
                    new System.Xml.Linq.XElement(gpxNamespace + "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                )
            )
        );

        var root = doc.Root;
        if (root == null)
            throw new InvalidOperationException("Failed to create GPX document");

        if (featureType.Equals("waypoints", StringComparison.OrdinalIgnoreCase) ||
            featureType.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // Export point geometries as waypoints
            foreach (var feature in features)
            {
                if (feature.Geometry is NetTopologySuite.Geometries.Point point)
                {
                    var wpt = new System.Xml.Linq.XElement(gpxNamespace + "wpt",
                        new System.Xml.Linq.XAttribute("lat", point.Y),
                        new System.Xml.Linq.XAttribute("lon", point.X)
                    );

                    // Add attributes as GPX elements
                    if (feature.Attributes != null)
                    {
                        if (feature.Attributes.Exists("name"))
                            wpt.Add(new System.Xml.Linq.XElement(gpxNamespace + "name", feature.Attributes["name"]));
                        if (feature.Attributes.Exists("description") || feature.Attributes.Exists("desc"))
                            wpt.Add(new System.Xml.Linq.XElement(gpxNamespace + "desc",
                                feature.Attributes["description"] ?? feature.Attributes["desc"]));
                        if (feature.Attributes.Exists("elevation") || feature.Attributes.Exists("ele"))
                            wpt.Add(new System.Xml.Linq.XElement(gpxNamespace + "ele",
                                feature.Attributes["elevation"] ?? feature.Attributes["ele"]));
                        if (feature.Attributes.Exists("time"))
                            wpt.Add(new System.Xml.Linq.XElement(gpxNamespace + "time", feature.Attributes["time"]));
                    }

                    root.Add(wpt);
                }
            }
        }

        if (featureType.Equals("tracks", StringComparison.OrdinalIgnoreCase) ||
            (featureType.Equals("auto", StringComparison.OrdinalIgnoreCase)))
        {
            // Export line geometries as tracks
            foreach (var feature in features)
            {
                if (feature.Geometry is NetTopologySuite.Geometries.LineString lineString)
                {
                    var trk = new System.Xml.Linq.XElement(gpxNamespace + "trk");

                    if (feature.Attributes != null)
                    {
                        if (feature.Attributes.Exists("name"))
                            trk.Add(new System.Xml.Linq.XElement(gpxNamespace + "name", feature.Attributes["name"]));
                        if (feature.Attributes.Exists("description") || feature.Attributes.Exists("desc"))
                            trk.Add(new System.Xml.Linq.XElement(gpxNamespace + "desc",
                                feature.Attributes["description"] ?? feature.Attributes["desc"]));
                    }

                    var trkseg = new System.Xml.Linq.XElement(gpxNamespace + "trkseg");
                    foreach (var coord in lineString.Coordinates)
                    {
                        trkseg.Add(new System.Xml.Linq.XElement(gpxNamespace + "trkpt",
                            new System.Xml.Linq.XAttribute("lat", coord.Y),
                            new System.Xml.Linq.XAttribute("lon", coord.X)
                        ));
                    }

                    trk.Add(trkseg);
                    root.Add(trk);
                }
                else if (feature.Geometry is NetTopologySuite.Geometries.MultiLineString multiLineString)
                {
                    var trk = new System.Xml.Linq.XElement(gpxNamespace + "trk");

                    if (feature.Attributes != null)
                    {
                        if (feature.Attributes.Exists("name"))
                            trk.Add(new System.Xml.Linq.XElement(gpxNamespace + "name", feature.Attributes["name"]));
                    }

                    foreach (var lineString2 in multiLineString.Geometries.Cast<NetTopologySuite.Geometries.LineString>())
                    {
                        var trkseg = new System.Xml.Linq.XElement(gpxNamespace + "trkseg");
                        foreach (var coord in lineString2.Coordinates)
                        {
                            trkseg.Add(new System.Xml.Linq.XElement(gpxNamespace + "trkpt",
                                new System.Xml.Linq.XAttribute("lat", coord.Y),
                                new System.Xml.Linq.XAttribute("lon", coord.X)
                            ));
                        }
                        trk.Add(trkseg);
                    }

                    root.Add(trk);
                }
            }
        }

        doc.Save(outputPath);
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("output_path"))
        {
            errors.Add("Parameter 'output_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data sink node for exporting to GML format
/// </summary>
public sealed class GmlSinkNode : WorkflowNodeBase
{
    public GmlSinkNode(ILogger<GmlSinkNode> logger) : base(logger) { }

    public override string NodeType => "data_sink.gml";
    public override string DisplayName => "GML Export";
    public override string Description => "Exports features to GML 3.2 format";
    public override string Category => "Data Sinks";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = context.GetParameter<string>("output_path");
        var featureCollectionName = context.GetParameter<string>("feature_collection_name", "FeatureCollection");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return NodeExecutionResult.Fail("Parameter 'output_path' is required");
        }

        // Get features from input
        var features = context.GetInputData<List<IFeature>>("features");
        if (features == null || features.Count == 0)
        {
            return NodeExecutionResult.Fail("No features provided for export");
        }

        try
        {
            await Task.Run(() => WriteGml(outputPath, features, featureCollectionName), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["output_path"] = outputPath,
                ["feature_count"] = features.Count,
                ["format"] = "GML 3.2"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export to GML: {OutputPath}", outputPath);
            return NodeExecutionResult.Fail($"Failed to export to GML: {ex.Message}");
        }
    }

    private void WriteGml(string outputPath, List<IFeature> features, string featureCollectionName)
    {
        var gmlNamespace = System.Xml.Linq.XNamespace.Get("http://www.opengis.net/gml/3.2");
        var wfsNamespace = System.Xml.Linq.XNamespace.Get("http://www.opengis.net/wfs/2.0");
        var xsiNamespace = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        var doc = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null),
            new System.Xml.Linq.XElement(wfsNamespace + "FeatureCollection",
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "wfs", wfsNamespace),
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "gml", gmlNamespace),
                new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "xsi", xsiNamespace),
                new System.Xml.Linq.XAttribute("timeStamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new System.Xml.Linq.XAttribute("numberMatched", features.Count),
                new System.Xml.Linq.XAttribute("numberReturned", features.Count)
            )
        );

        var root = doc.Root;
        if (root == null)
            throw new InvalidOperationException("Failed to create GML document");

        var gmlWriter = new GMLWriter();

        for (int i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            var featureMember = new System.Xml.Linq.XElement(wfsNamespace + "member");
            var featureElement = new System.Xml.Linq.XElement("Feature",
                new System.Xml.Linq.XAttribute(gmlNamespace + "id", $"feature.{i + 1}")
            );

            // Add geometry
            if (feature.Geometry != null)
            {
                try
                {
                    var gmlGeometry = gmlWriter.Write(feature.Geometry);
                    var gmlDoc = System.Xml.Linq.XDocument.Parse(gmlGeometry);
                    if (gmlDoc.Root != null)
                    {
                        var geometryElement = new System.Xml.Linq.XElement("geometry", gmlDoc.Root);
                        featureElement.Add(geometryElement);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to write geometry for feature {FeatureId}", i);
                }
            }

            // Add attributes
            if (feature.Attributes != null)
            {
                foreach (var name in feature.Attributes.GetNames())
                {
                    var value = feature.Attributes[name];
                    if (value != null)
                    {
                        featureElement.Add(new System.Xml.Linq.XElement(name, value));
                    }
                }
            }

            featureMember.Add(featureElement);
            root.Add(featureMember);
        }

        doc.Save(outputPath);
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("output_path"))
        {
            errors.Add("Parameter 'output_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}
