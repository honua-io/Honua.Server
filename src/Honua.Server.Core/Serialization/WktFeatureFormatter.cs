// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Formats features as Well-Known Text (WKT) for OGC API Features output.
/// </summary>
public static class WktFeatureFormatter
{
    private static readonly WKTWriter WktWriter = new() { Formatted = false };
    private static readonly GeoJsonReader GeoJsonReader = new();

    /// <summary>
    /// Writes a feature collection as WKT.
    /// For collections with multiple features, each feature's geometry is output on a separate line
    /// with properties as comments.
    /// </summary>
    public static string WriteFeatureCollection(
        string collectionId,
        LayerDefinition layer,
        IEnumerable<FeatureRecord> features,
        long numberMatched,
        long numberReturned)
    {
        Guard.NotNull(layer);
        Guard.NotNull(features);

        var sb = new StringBuilder();

        // Add collection metadata as comments
        sb.AppendLine($"# Collection: {collectionId}");
        if (!string.IsNullOrWhiteSpace(layer.Title))
        {
            sb.AppendLine($"# Title: {layer.Title}");
        }
        if (!string.IsNullOrWhiteSpace(layer.Description))
        {
            sb.AppendLine($"# Description: {layer.Description}");
        }
        sb.AppendLine($"# numberMatched: {numberMatched}");
        sb.AppendLine($"# numberReturned: {numberReturned}");
        sb.AppendLine();

        foreach (var feature in features)
        {
            WriteFeature(sb, layer, feature);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes a single feature as WKT with its properties as a comment.
    /// </summary>
    public static string WriteSingleFeature(LayerDefinition layer, FeatureRecord feature)
    {
        Guard.NotNull(layer);
        Guard.NotNull(feature);

        var sb = new StringBuilder();
        WriteFeature(sb, layer, feature);
        return sb.ToString();
    }

    private static void WriteFeature(StringBuilder sb, LayerDefinition layer, FeatureRecord feature)
    {
        // Extract feature ID
        if (feature.Attributes.TryGetValue(layer.IdField, out var idValue) && idValue is not null)
        {
            sb.AppendLine($"# Feature ID: {idValue}");
        }

        // Write properties as comments (excluding geometry and ID fields)
        foreach (var kvp in feature.Attributes)
        {
            if (string.Equals(kvp.Key, layer.GeometryField, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, layer.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = kvp.Value?.ToString() ?? "null";
            sb.AppendLine($"# {kvp.Key}: {value}");
        }

        // Extract and write geometry as WKT
        var geometry = ExtractGeometry(feature, layer.GeometryField);
        if (geometry is not null)
        {
            try
            {
                var wkt = WktWriter.Write(geometry);
                sb.AppendLine(wkt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to write geometry as WKT for feature {idValue}: {ex.Message}", ex);
            }
        }
        else
        {
            sb.AppendLine("# No geometry");
        }

        sb.AppendLine(); // Blank line between features
    }

    private static Geometry? ExtractGeometry(FeatureRecord record, string geometryField)
    {
        if (!record.Attributes.TryGetValue(geometryField, out var raw) || raw is null)
        {
            return null;
        }

        try
        {
            return raw switch
            {
                Geometry g => g,
                JsonNode node => GeoJsonReader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String => GeoJsonReader.Read<Geometry>(element.GetString() ?? string.Empty),
                JsonElement element => GeoJsonReader.Read<Geometry>(element.GetRawText()),
                string text => GeoJsonReader.Read<Geometry>(text),
                _ => GeoJsonReader.Read<Geometry>(raw.ToString() ?? string.Empty)
            };
        }
        catch
        {
            return null;
        }
    }
}
