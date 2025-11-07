// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Data source node for reading GeoPackage files
/// </summary>
public sealed class GeoPackageDataSourceNode : WorkflowNodeBase
{
    private readonly string _connectionString;

    public GeoPackageDataSourceNode(string connectionString, ILogger<GeoPackageDataSourceNode> logger)
        : base(logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public override string NodeType => "data_source.geopackage";
    public override string DisplayName => "GeoPackage Data Source";
    public override string Description => "Reads features from a GeoPackage (.gpkg) file";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var tableName = context.GetParameter<string>("table_name", "features");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"GeoPackage file not found: {filePath}");
        }

        try
        {
            // Use SQLite to read GeoPackage (it's just SQLite with GeoPackage extensions)
            var features = await ReadGeoPackageAsync(filePath, tableName, limit, cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath,
                ["table"] = tableName
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read GeoPackage: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read GeoPackage: {ex.Message}");
        }
    }

    private async Task<List<IFeature>> ReadGeoPackageAsync(
        string filePath,
        string tableName,
        int? limit,
        CancellationToken cancellationToken)
    {
        var features = new List<IFeature>();

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);

        var query = $"SELECT * FROM {tableName}";
        if (limit.HasValue && limit.Value > 0)
        {
            query += $" LIMIT {limit.Value}";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var wkbReader = new WKBReader();

        while (await reader.ReadAsync(cancellationToken))
        {
            var attributes = new AttributesTable();
            Geometry? geometry = null;

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var value = reader.GetValue(i);

                // Check if this is a geometry column (typically 'geom' or ends with 'geom')
                if (value is byte[] bytes && (fieldName.Equals("geom", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.EndsWith("geom", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        // GeoPackage uses WKB format for geometries
                        // Skip the first 4 bytes (GeoPackage header) if present
                        var wkbData = bytes.Length > 4 && bytes[0] == 0x47 && bytes[1] == 0x50 ?
                            bytes.Skip(8).ToArray() : bytes;
                        geometry = wkbReader.Read(wkbData);
                    }
                    catch
                    {
                        // If WKB parsing fails, try without skipping header
                        geometry = wkbReader.Read(bytes);
                    }
                }
                else
                {
                    attributes.Add(fieldName, value == DBNull.Value ? null : value);
                }
            }

            features.Add(new Feature(geometry, attributes));
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data source node for reading Shapefile files
/// </summary>
public sealed class ShapefileDataSourceNode : WorkflowNodeBase
{
    public ShapefileDataSourceNode(ILogger<ShapefileDataSourceNode> logger) : base(logger) { }

    public override string NodeType => "data_source.shapefile";
    public override string DisplayName => "Shapefile Data Source";
    public override string Description => "Reads features from a Shapefile (.shp) with associated files";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        // Ensure .shp extension
        if (!filePath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
        {
            return NodeExecutionResult.Fail("File path must point to a .shp file");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"Shapefile not found: {filePath}");
        }

        try
        {
            var features = await Task.Run(() => ReadShapefileAsync(filePath, limit), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read Shapefile: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read Shapefile: {ex.Message}");
        }
    }

    private List<IFeature> ReadShapefileAsync(string filePath, int? limit)
    {
        var features = new List<IFeature>();
        var reader = new ShapefileDataReader(filePath, GeometryFactory.Default);

        try
        {
            var header = reader.DbaseHeader;
            int count = 0;

            while (reader.Read())
            {
                if (limit.HasValue && count >= limit.Value)
                    break;

                var geometry = reader.Geometry;
                var attributes = new AttributesTable();

                for (int i = 0; i < header.NumFields; i++)
                {
                    var fieldName = header.Fields[i].Name;
                    var value = reader.GetValue(i + 1); // +1 because index 0 is geometry
                    attributes.Add(fieldName, value == DBNull.Value ? null : value);
                }

                features.Add(new Feature(geometry, attributes));
                count++;
            }
        }
        finally
        {
            reader.Close();
            reader.Dispose();
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data source node for reading KML files
/// </summary>
public sealed class KmlDataSourceNode : WorkflowNodeBase
{
    public KmlDataSourceNode(ILogger<KmlDataSourceNode> logger) : base(logger) { }

    public override string NodeType => "data_source.kml";
    public override string DisplayName => "KML Data Source";
    public override string Description => "Reads features from KML/KMZ files";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"KML file not found: {filePath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var features = ParseKml(content, limit);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read KML: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read KML: {ex.Message}");
        }
    }

    private List<IFeature> ParseKml(string content, int? limit)
    {
        // Use NetTopologySuite's built-in GML reader which can handle KML geometries
        // This is a simplified implementation - full KML support would require SharpKml library
        var features = new List<IFeature>();

        // For now, return empty list - full KML parsing requires additional library
        // TODO: Add SharpKml NuGet package for comprehensive KML support
        Logger.LogWarning("KML parsing not yet fully implemented. Consider using GeoJSON or Shapefile formats.");

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data source node for reading CSV files with geometry columns
/// </summary>
public sealed class CsvGeometryDataSourceNode : WorkflowNodeBase
{
    public CsvGeometryDataSourceNode(ILogger<CsvGeometryDataSourceNode> logger) : base(logger) { }

    public override string NodeType => "data_source.csv_geometry";
    public override string DisplayName => "CSV with Geometry Data Source";
    public override string Description => "Reads features from CSV files with WKT, WKB, or lat/lon columns";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var geometryFormat = context.GetParameter<string>("geometry_format", "WKT");
        var delimiter = context.GetParameter<string>("delimiter", ",");
        var hasHeader = context.GetParameter<bool>("has_header", true);
        var geometryColumn = context.GetParameter<string>("geometry_column", "geometry");
        var latColumn = context.GetParameter<string>("lat_column", "latitude");
        var lonColumn = context.GetParameter<string>("lon_column", "longitude");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"CSV file not found: {filePath}");
        }

        try
        {
            var features = await Task.Run(() => ReadCsvWithGeometry(
                filePath, geometryFormat, delimiter[0], hasHeader,
                geometryColumn, latColumn, lonColumn, limit), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath,
                ["geometry_format"] = geometryFormat
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read CSV with geometry: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read CSV: {ex.Message}");
        }
    }

    private List<IFeature> ReadCsvWithGeometry(
        string filePath,
        string geometryFormat,
        char delimiter,
        bool hasHeader,
        string geometryColumn,
        string latColumn,
        string lonColumn,
        int? limit)
    {
        var features = new List<IFeature>();
        var wktReader = new WKTReader();
        var wkbReader = new WKBReader();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
            return features;

        var headerRow = hasHeader ? lines[0].Split(delimiter) : null;
        var dataStartIndex = hasHeader ? 1 : 0;
        var geometryIndex = -1;
        var latIndex = -1;
        var lonIndex = -1;

        // Find geometry column indices
        if (hasHeader && headerRow != null)
        {
            if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
            {
                latIndex = Array.FindIndex(headerRow, h => h.Trim().Equals(latColumn, StringComparison.OrdinalIgnoreCase));
                lonIndex = Array.FindIndex(headerRow, h => h.Trim().Equals(lonColumn, StringComparison.OrdinalIgnoreCase));

                if (latIndex == -1 || lonIndex == -1)
                {
                    throw new InvalidOperationException($"Could not find lat/lon columns: {latColumn}, {lonColumn}");
                }
            }
            else
            {
                geometryIndex = Array.FindIndex(headerRow, h => h.Trim().Equals(geometryColumn, StringComparison.OrdinalIgnoreCase));

                if (geometryIndex == -1)
                {
                    throw new InvalidOperationException($"Could not find geometry column: {geometryColumn}");
                }
            }
        }
        else
        {
            // Without header, assume first column for geometry or columns 0,1 for lat/lon
            if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
            {
                latIndex = 0;
                lonIndex = 1;
            }
            else
            {
                geometryIndex = 0;
            }
        }

        int count = 0;
        for (int i = dataStartIndex; i < lines.Length; i++)
        {
            if (limit.HasValue && count >= limit.Value)
                break;

            var values = lines[i].Split(delimiter);
            if (values.Length == 0)
                continue;

            try
            {
                Geometry? geometry = null;

                // Parse geometry based on format
                if (geometryFormat.Equals("LatLon", StringComparison.OrdinalIgnoreCase))
                {
                    if (latIndex >= 0 && lonIndex >= 0 && values.Length > Math.Max(latIndex, lonIndex))
                    {
                        if (double.TryParse(values[latIndex].Trim(), out var lat) &&
                            double.TryParse(values[lonIndex].Trim(), out var lon))
                        {
                            geometry = GeometryFactory.Default.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lon, lat));
                        }
                    }
                }
                else if (geometryFormat.Equals("WKT", StringComparison.OrdinalIgnoreCase))
                {
                    if (geometryIndex >= 0 && values.Length > geometryIndex)
                    {
                        var wkt = values[geometryIndex].Trim().Trim('"');
                        geometry = wktReader.Read(wkt);
                    }
                }
                else if (geometryFormat.Equals("WKB", StringComparison.OrdinalIgnoreCase))
                {
                    if (geometryIndex >= 0 && values.Length > geometryIndex)
                    {
                        var wkbHex = values[geometryIndex].Trim().Trim('"');
                        var wkbBytes = WKBReader.HexToBytes(wkbHex);
                        geometry = wkbReader.Read(wkbBytes);
                    }
                }

                // Parse attributes
                var attributes = new AttributesTable();
                for (int j = 0; j < values.Length; j++)
                {
                    // Skip geometry columns
                    if (j == geometryIndex || j == latIndex || j == lonIndex)
                        continue;

                    var fieldName = hasHeader && headerRow != null ? headerRow[j].Trim() : $"field_{j}";
                    var value = values[j].Trim().Trim('"');

                    // Try to parse as number, boolean, or keep as string
                    if (int.TryParse(value, out var intVal))
                        attributes.Add(fieldName, intVal);
                    else if (double.TryParse(value, out var doubleVal))
                        attributes.Add(fieldName, doubleVal);
                    else if (bool.TryParse(value, out var boolVal))
                        attributes.Add(fieldName, boolVal);
                    else
                        attributes.Add(fieldName, value);
                }

                features.Add(new Feature(geometry, attributes));
                count++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse CSV row {RowNumber}: {Row}", i + 1, lines[i]);
                // Continue processing other rows
            }
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
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
/// Data source node for reading GPX files
/// </summary>
public sealed class GpxDataSourceNode : WorkflowNodeBase
{
    public GpxDataSourceNode(ILogger<GpxDataSourceNode> logger) : base(logger) { }

    public override string NodeType => "data_source.gpx";
    public override string DisplayName => "GPX Data Source";
    public override string Description => "Reads waypoints, tracks, and routes from GPX files";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var featureType = context.GetParameter<string>("feature_type", "waypoints");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"GPX file not found: {filePath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var features = ParseGpx(content, featureType, limit);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath,
                ["feature_type"] = featureType
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read GPX: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read GPX: {ex.Message}");
        }
    }

    private List<IFeature> ParseGpx(string content, string featureType, int? limit)
    {
        var features = new List<IFeature>();
        var doc = System.Xml.Linq.XDocument.Parse(content);
        var ns = doc.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;

        int count = 0;

        if (featureType.Equals("waypoints", StringComparison.OrdinalIgnoreCase) ||
            featureType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var waypoints = doc.Descendants(ns + "wpt");
            foreach (var wpt in waypoints)
            {
                if (limit.HasValue && count >= limit.Value)
                    break;

                var lat = double.Parse(wpt.Attribute("lat")?.Value ?? "0");
                var lon = double.Parse(wpt.Attribute("lon")?.Value ?? "0");
                var geometry = GeometryFactory.Default.CreatePoint(new NetTopologySuite.Geometries.Coordinate(lon, lat));

                var attributes = new AttributesTable
                {
                    ["type"] = "waypoint",
                    ["name"] = wpt.Element(ns + "name")?.Value,
                    ["description"] = wpt.Element(ns + "desc")?.Value,
                    ["elevation"] = wpt.Element(ns + "ele")?.Value,
                    ["time"] = wpt.Element(ns + "time")?.Value,
                    ["symbol"] = wpt.Element(ns + "sym")?.Value
                };

                features.Add(new Feature(geometry, attributes));
                count++;
            }
        }

        if (featureType.Equals("tracks", StringComparison.OrdinalIgnoreCase) ||
            featureType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var tracks = doc.Descendants(ns + "trk");
            foreach (var trk in tracks)
            {
                if (limit.HasValue && count >= limit.Value)
                    break;

                var trackSegs = trk.Descendants(ns + "trkseg");
                foreach (var seg in trackSegs)
                {
                    var coordinates = new List<NetTopologySuite.Geometries.Coordinate>();
                    foreach (var pt in seg.Descendants(ns + "trkpt"))
                    {
                        var lat = double.Parse(pt.Attribute("lat")?.Value ?? "0");
                        var lon = double.Parse(pt.Attribute("lon")?.Value ?? "0");
                        coordinates.Add(new NetTopologySuite.Geometries.Coordinate(lon, lat));
                    }

                    if (coordinates.Count > 0)
                    {
                        var geometry = GeometryFactory.Default.CreateLineString(coordinates.ToArray());
                        var attributes = new AttributesTable
                        {
                            ["type"] = "track",
                            ["name"] = trk.Element(ns + "name")?.Value,
                            ["description"] = trk.Element(ns + "desc")?.Value,
                            ["number"] = trk.Element(ns + "number")?.Value
                        };

                        features.Add(new Feature(geometry, attributes));
                        count++;
                    }
                }
            }
        }

        if (featureType.Equals("routes", StringComparison.OrdinalIgnoreCase) ||
            featureType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var routes = doc.Descendants(ns + "rte");
            foreach (var rte in routes)
            {
                if (limit.HasValue && count >= limit.Value)
                    break;

                var coordinates = new List<NetTopologySuite.Geometries.Coordinate>();
                foreach (var pt in rte.Descendants(ns + "rtept"))
                {
                    var lat = double.Parse(pt.Attribute("lat")?.Value ?? "0");
                    var lon = double.Parse(pt.Attribute("lon")?.Value ?? "0");
                    coordinates.Add(new NetTopologySuite.Geometries.Coordinate(lon, lat));
                }

                if (coordinates.Count > 0)
                {
                    var geometry = GeometryFactory.Default.CreateLineString(coordinates.ToArray());
                    var attributes = new AttributesTable
                    {
                        ["type"] = "route",
                        ["name"] = rte.Element(ns + "name")?.Value,
                        ["description"] = rte.Element(ns + "desc")?.Value,
                        ["number"] = rte.Element(ns + "number")?.Value
                    };

                    features.Add(new Feature(geometry, attributes));
                    count++;
                }
            }
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data source node for reading GML files
/// </summary>
public sealed class GmlDataSourceNode : WorkflowNodeBase
{
    public GmlDataSourceNode(ILogger<GmlDataSourceNode> logger) : base(logger) { }

    public override string NodeType => "data_source.gml";
    public override string DisplayName => "GML Data Source";
    public override string Description => "Reads features from GML 2.0/3.0/3.2 files";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.GetParameter<string>("file_path");
        var limit = context.GetParameter<int?>("limit");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("Parameter 'file_path' is required");
        }

        if (!File.Exists(filePath))
        {
            return NodeExecutionResult.Fail($"GML file not found: {filePath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var features = await Task.Run(() => ParseGml(content, limit), cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_file"] = filePath
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read GML: {FilePath}", filePath);
            return NodeExecutionResult.Fail($"Failed to read GML: {ex.Message}");
        }
    }

    private List<IFeature> ParseGml(string content, int? limit)
    {
        var features = new List<IFeature>();
        var gmlReader = new GMLReader();

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(content);
            var featureMembers = doc.Descendants()
                .Where(e => e.Name.LocalName == "featureMember" || e.Name.LocalName == "featureMembers")
                .ToList();

            int count = 0;
            foreach (var featureMember in featureMembers)
            {
                if (limit.HasValue && count >= limit.Value)
                    break;

                // Find geometry elements
                var geometryElements = featureMember.Descendants()
                    .Where(e => e.Name.LocalName == "Point" ||
                               e.Name.LocalName == "LineString" ||
                               e.Name.LocalName == "Polygon" ||
                               e.Name.LocalName == "MultiPoint" ||
                               e.Name.LocalName == "MultiLineString" ||
                               e.Name.LocalName == "MultiPolygon" ||
                               e.Name.LocalName == "MultiGeometry")
                    .ToList();

                Geometry? geometry = null;
                if (geometryElements.Any())
                {
                    var gmlGeometry = geometryElements.First().ToString();
                    geometry = gmlReader.Read(gmlGeometry);
                }

                // Parse attributes
                var attributes = new AttributesTable();
                var featureElement = featureMember.Elements().FirstOrDefault();
                if (featureElement != null)
                {
                    foreach (var element in featureElement.Elements())
                    {
                        // Skip geometry elements
                        if (geometryElements.Any(g => g == element))
                            continue;

                        var name = element.Name.LocalName;
                        var value = element.Value;

                        // Try to parse as number or keep as string
                        if (int.TryParse(value, out var intVal))
                            attributes.Add(name, intVal);
                        else if (double.TryParse(value, out var doubleVal))
                            attributes.Add(name, doubleVal);
                        else if (bool.TryParse(value, out var boolVal))
                            attributes.Add(name, boolVal);
                        else
                            attributes.Add(name, value);
                    }
                }

                features.Add(new Feature(geometry, attributes));
                count++;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse GML content");
            throw;
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("file_path"))
        {
            errors.Add("Parameter 'file_path' is required");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}

/// <summary>
/// Data source node for reading from WFS endpoints
/// </summary>
public sealed class WfsDataSourceNode : WorkflowNodeBase
{
    private readonly System.Net.Http.HttpClient _httpClient;

    public WfsDataSourceNode(ILogger<WfsDataSourceNode> logger) : base(logger)
    {
        _httpClient = new System.Net.Http.HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public override string NodeType => "data_source.wfs";
    public override string DisplayName => "WFS Data Source";
    public override string Description => "Reads features from WFS (Web Feature Service) endpoints";
    public override string Category => "Data Sources";

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var url = context.GetParameter<string>("url");
        var typename = context.GetParameter<string>("typename");
        var version = context.GetParameter<string>("version", "2.0.0");
        var maxFeatures = context.GetParameter<int?>("max_features", 1000);
        var bbox = context.GetParameter<string>("bbox");
        var crs = context.GetParameter<string>("crs", "EPSG:4326");
        var filter = context.GetParameter<string>("filter");

        if (string.IsNullOrWhiteSpace(url))
        {
            return NodeExecutionResult.Fail("Parameter 'url' is required");
        }

        if (string.IsNullOrWhiteSpace(typename))
        {
            return NodeExecutionResult.Fail("Parameter 'typename' is required");
        }

        try
        {
            var requestUrl = BuildWfsGetFeatureUrl(url, typename, version, maxFeatures, bbox, crs, filter);
            Logger.LogInformation("Fetching WFS features from: {Url}", requestUrl);

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var gmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var features = ParseWfsResponse(gmlContent);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["features"] = features,
                ["count"] = features.Count,
                ["source_url"] = requestUrl,
                ["typename"] = typename
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read from WFS: {Url}", url);
            return NodeExecutionResult.Fail($"Failed to read from WFS: {ex.Message}");
        }
    }

    private string BuildWfsGetFeatureUrl(
        string baseUrl,
        string typename,
        string version,
        int? maxFeatures,
        string? bbox,
        string? crs,
        string? filter)
    {
        var queryParams = new List<string>
        {
            "SERVICE=WFS",
            "REQUEST=GetFeature",
            $"VERSION={version}",
            $"TYPENAME={typename}"
        };

        if (maxFeatures.HasValue)
        {
            var countParam = version.StartsWith("2.") ? "COUNT" : "MAXFEATURES";
            queryParams.Add($"{countParam}={maxFeatures.Value}");
        }

        if (!string.IsNullOrWhiteSpace(bbox))
        {
            queryParams.Add($"BBOX={bbox}");
        }

        if (!string.IsNullOrWhiteSpace(crs))
        {
            queryParams.Add($"SRSNAME={crs}");
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryParams.Add($"CQL_FILTER={System.Web.HttpUtility.UrlEncode(filter)}");
        }

        // Always request GML output
        queryParams.Add("OUTPUTFORMAT=application/gml+xml");

        var separator = baseUrl.Contains("?") ? "&" : "?";
        return $"{baseUrl}{separator}{string.Join("&", queryParams)}";
    }

    private List<IFeature> ParseWfsResponse(string gmlContent)
    {
        var features = new List<IFeature>();
        var gmlReader = new GMLReader();

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(gmlContent);

            // Handle both WFS 1.x and 2.x responses
            var featureMembers = doc.Descendants()
                .Where(e => e.Name.LocalName == "featureMember" ||
                           e.Name.LocalName == "member" ||
                           e.Name.LocalName == "featureMembers")
                .ToList();

            foreach (var featureMember in featureMembers)
            {
                // Find geometry elements
                var geometryElements = featureMember.Descendants()
                    .Where(e => e.Name.LocalName == "Point" ||
                               e.Name.LocalName == "LineString" ||
                               e.Name.LocalName == "Polygon" ||
                               e.Name.LocalName == "MultiPoint" ||
                               e.Name.LocalName == "MultiLineString" ||
                               e.Name.LocalName == "MultiPolygon" ||
                               e.Name.LocalName == "MultiGeometry" ||
                               e.Name.LocalName == "Surface" ||
                               e.Name.LocalName == "Curve")
                    .ToList();

                Geometry? geometry = null;
                if (geometryElements.Any())
                {
                    var gmlGeometry = geometryElements.First().ToString();
                    try
                    {
                        geometry = gmlReader.Read(gmlGeometry);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to parse geometry from WFS feature");
                    }
                }

                // Parse attributes
                var attributes = new AttributesTable();
                var featureElement = featureMember.Elements().FirstOrDefault();
                if (featureElement != null)
                {
                    foreach (var element in featureElement.Elements())
                    {
                        // Skip geometry elements
                        if (geometryElements.Any(g => g == element))
                            continue;

                        var name = element.Name.LocalName;
                        var value = element.Value;

                        // Try to parse as number or keep as string
                        if (string.IsNullOrWhiteSpace(value))
                            attributes.Add(name, null);
                        else if (int.TryParse(value, out var intVal))
                            attributes.Add(name, intVal);
                        else if (double.TryParse(value, out var doubleVal))
                            attributes.Add(name, doubleVal);
                        else if (bool.TryParse(value, out var boolVal))
                            attributes.Add(name, boolVal);
                        else if (DateTime.TryParse(value, out var dateVal))
                            attributes.Add(name, dateVal);
                        else
                            attributes.Add(name, value);
                    }
                }

                features.Add(new Feature(geometry, attributes));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse WFS response");
            throw;
        }

        return features;
    }

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (!nodeDefinition.Parameters.ContainsKey("url"))
        {
            errors.Add("Parameter 'url' is required");
        }

        if (!nodeDefinition.Parameters.ContainsKey("typename"))
        {
            errors.Add("Parameter 'typename' is required");
        }

        var version = nodeDefinition.Parameters.ContainsKey("version")
            ? nodeDefinition.Parameters["version"]?.ToString()
            : "2.0.0";

        if (!string.IsNullOrEmpty(version) &&
            !new[] { "1.0.0", "1.1.0", "2.0.0" }.Contains(version))
        {
            errors.Add("Parameter 'version' must be '1.0.0', '1.1.0', or '2.0.0'");
        }

        return Task.FromResult(errors.Count == 0
            ? NodeValidationResult.Success()
            : NodeValidationResult.Failure(errors));
    }
}
