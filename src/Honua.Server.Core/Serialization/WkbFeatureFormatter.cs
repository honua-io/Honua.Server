// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
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
/// Formats features as Well-Known Binary (WKB) for OGC API Features output.
/// </summary>
public static class WkbFeatureFormatter
{
    private static readonly WKBWriter WkbWriter = new();
    private static readonly GeoJsonReader GeoJsonReader = new();

    /// <summary>
    /// Writes a feature collection as WKB.
    /// For collections with multiple features, each feature's geometry is output sequentially
    /// with a simple binary header structure.
    /// Format: [4-byte feature count][features...]
    /// Each feature: [4-byte geometry length][WKB geometry bytes]
    /// </summary>
    public static byte[] WriteFeatureCollection(
        string collectionId,
        LayerDefinition layer,
        IEnumerable<FeatureRecord> features,
        long numberMatched,
        long numberReturned)
    {
        Guard.NotNull(layer);
        Guard.NotNull(features);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write header: feature count (32-bit integer)
        writer.Write((int)numberReturned);

        var featureCount = 0;
        foreach (var feature in features)
        {
            var geometry = ExtractGeometry(feature, layer.GeometryField);
            if (geometry is not null)
            {
                try
                {
                    var wkb = WkbWriter.Write(geometry);

                    // Write WKB length (32-bit integer)
                    writer.Write(wkb.Length);

                    // Write WKB geometry bytes
                    writer.Write(wkb);

                    featureCount++;
                }
                catch (Exception ex)
                {
                    var idValue = feature.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
                    throw new InvalidOperationException($"Failed to write geometry as WKB for feature {idValue}: {ex.Message}", ex);
                }
            }
        }

        // Update feature count at the beginning if different
        if (featureCount != numberReturned)
        {
            ms.Position = 0;
            writer.Write(featureCount);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Writes a single feature's geometry as WKB bytes.
    /// </summary>
    public static byte[] WriteSingleFeature(LayerDefinition layer, FeatureRecord feature)
    {
        Guard.NotNull(layer);
        Guard.NotNull(feature);

        var geometry = ExtractGeometry(feature, layer.GeometryField);
        if (geometry is null)
        {
            return Array.Empty<byte>();
        }

        try
        {
            return WkbWriter.Write(geometry);
        }
        catch (Exception ex)
        {
            var idValue = feature.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
            throw new InvalidOperationException($"Failed to write geometry as WKB for feature {idValue}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes a GeometryCollection containing all feature geometries as a single WKB.
    /// This is useful for representing a feature collection as a single geometry.
    /// </summary>
    public static byte[] WriteAsGeometryCollection(
        LayerDefinition layer,
        IEnumerable<FeatureRecord> features,
        GeometryFactory? factory = null)
    {
        Guard.NotNull(layer);
        Guard.NotNull(features);

        factory ??= new GeometryFactory();

        var geometries = new List<Geometry>();
        foreach (var feature in features)
        {
            var geometry = ExtractGeometry(feature, layer.GeometryField);
            if (geometry is not null)
            {
                geometries.Add(geometry);
            }
        }

        if (geometries.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var geometryCollection = factory.CreateGeometryCollection(geometries.ToArray());

        try
        {
            return WkbWriter.Write(geometryCollection);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to write GeometryCollection as WKB: {ex.Message}", ex);
        }
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
