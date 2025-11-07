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

    public override async Task<NodeExecutionResult> ExecuteAsync(
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

    public override async Task<NodeExecutionResult> ExecuteAsync(
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
