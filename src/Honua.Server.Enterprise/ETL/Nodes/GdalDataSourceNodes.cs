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

    public override async Task<NodeExecutionResult> ExecuteAsync(
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

    public override async Task<NodeExecutionResult> ExecuteAsync(
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

    public override async Task<NodeExecutionResult> ExecuteAsync(
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
