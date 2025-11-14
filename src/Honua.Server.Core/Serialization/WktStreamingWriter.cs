// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

#nullable enable

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Streaming Well-Known Text (WKT) writer for OGC API Features output.
/// Writes geometries as WKT with optional feature metadata as comments.
///
/// Output format options:
/// 1. Geometry-only mode: Just WKT geometries, one per line
/// 2. Annotated mode (default): Feature ID and properties as comments, then WKT
///
/// Example annotated output:
/// <code>
/// # Collection: lakes
/// # Title: World Lakes
/// # numberMatched: 1000
/// # numberReturned: 100
///
/// # Feature ID: 1
/// # name: Lake Superior
/// # area_km2: 82100
/// POLYGON ((-92.0 46.5, -84.5 46.5, -84.5 49.0, -92.0 49.0, -92.0 46.5))
///
/// # Feature ID: 2
/// # name: Lake Huron
/// # area_km2: 59600
/// POLYGON ((-83.5 43.0, -81.0 43.0, -81.0 46.0, -83.5 46.0, -83.5 43.0))
/// </code>
/// </summary>
public sealed class WktStreamingWriter : StreamingFeatureCollectionWriterBase
{
    private static readonly byte[] _newlineBytes = Encoding.UTF8.GetBytes("\n");

    private readonly bool _includeAnnotations;
    private readonly bool _includeProperties;

    protected override string ContentType => "text/plain; charset=utf-8";
    protected override string FormatName => "WKT";

    /// <summary>
    /// Creates a new WKT streaming writer.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="includeAnnotations">Whether to include feature IDs and properties as comments (default: true)</param>
    /// <param name="includeProperties">Whether to include all properties as comments (default: true, only used if includeAnnotations is true)</param>
    public WktStreamingWriter(
        ILogger<WktStreamingWriter> logger,
        bool includeAnnotations = true,
        bool includeProperties = true)
        : base(logger)
    {
        _includeAnnotations = includeAnnotations;
        _includeProperties = includeProperties;
    }

    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        if (!_includeAnnotations)
        {
            // Geometry-only mode - no header
            return;
        }

        // Write collection metadata as comments
        var sb = new StringBuilder();
        sb.AppendLine($"# Collection: {layer.Id}");

        if (!string.IsNullOrWhiteSpace(layer.Title))
        {
            sb.AppendLine($"# Title: {layer.Title}");
        }

        if (!string.IsNullOrWhiteSpace(layer.Description))
        {
            sb.AppendLine($"# Description: {layer.Description}");
        }

        if (context.TotalCount.HasValue)
        {
            sb.AppendLine($"# numberMatched: {context.TotalCount.Value}");
        }

        sb.AppendLine($"# Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();

        // Use ArrayPool to reduce allocations
        var text = sb.ToString();
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var buffer = ObjectPools.ByteArrayPool.Rent(byteCount);
        try
        {
            var bytesWritten = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesWritten), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ObjectPools.ByteArrayPool.Return(buffer);
        }
    }

    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // Features are separated by blank lines in annotated mode, or just newlines in geometry-only mode
        // This is handled in WriteFeatureAsync, so no separator needed here
        return Task.CompletedTask;
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // Write feature annotations (ID and properties as comments)
        if (_includeAnnotations)
        {
            // Write feature ID
            if (feature.Attributes.TryGetValue(layer.IdField, out var idValue) && idValue is not null)
            {
                sb.AppendLine($"# Feature ID: {idValue}");
            }

            // Write properties as comments (excluding geometry and ID fields)
            if (_includeProperties)
            {
                foreach (var kvp in feature.Attributes)
                {
                    if (string.Equals(kvp.Key, layer.GeometryField, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kvp.Key, layer.IdField, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = FormatPropertyValue(kvp.Value);
                    sb.AppendLine($"# {kvp.Key}: {value}");
                }
            }
        }

        // Extract and write geometry as WKT
        if (context.ReturnGeometry)
        {
            var geometry = ExtractGeometry(feature, layer.GeometryField);
            if (geometry is not null)
            {
                try
                {
                    // THREAD-SAFETY FIX: Create per-request WKTWriter instead of static shared instance
                    var wktWriter = new WKTWriter { Formatted = false };
                    var wkt = wktWriter.Write(geometry);
                    sb.AppendLine(wkt);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to write geometry as WKT for feature {feature.Attributes.GetValueOrDefault(layer.IdField)}: {ex.Message}",
                        ex);
                }
            }
            else
            {
                if (_includeAnnotations)
                {
                    sb.AppendLine("# No geometry");
                }
            }
        }

        // Add blank line between features in annotated mode
        if (_includeAnnotations)
        {
            sb.AppendLine();
        }

        // Write to stream using ArrayPool to reduce GC pressure (hot path - called per feature)
        var text = sb.ToString();
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var buffer = ObjectPools.ByteArrayPool.Rent(byteCount);
        try
        {
            var bytesWritten = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesWritten), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ObjectPools.ByteArrayPool.Return(buffer);
        }
    }

    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        if (!_includeAnnotations)
        {
            // Geometry-only mode - just ensure final newline
            await outputStream.WriteAsync(_newlineBytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Write summary metadata
        var footer = new StringBuilder();
        footer.AppendLine($"# numberReturned: {featuresWritten}");
        footer.AppendLine($"# End of collection");

        // Use ArrayPool to reduce allocations
        var text = footer.ToString();
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var buffer = ObjectPools.ByteArrayPool.Rent(byteCount);
        try
        {
            var bytesWritten = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesWritten), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ObjectPools.ByteArrayPool.Return(buffer);
        }
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a geometry from a feature record, handling various input formats.
    /// </summary>
    private static Geometry? ExtractGeometry(FeatureRecord record, string geometryField)
    {
        if (!record.Attributes.TryGetValue(geometryField, out var raw) || raw is null)
        {
            return null;
        }

        try
        {
            // THREAD-SAFETY FIX: Create per-request GeoJsonReader instead of static shared instance
            var geoJsonReader = new GeoJsonReader();

            return raw switch
            {
                Geometry g => g,
                JsonNode node => geoJsonReader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String =>
                    geoJsonReader.Read<Geometry>(element.GetString() ?? string.Empty),
                JsonElement element => geoJsonReader.Read<Geometry>(element.GetRawText()),
                string text => geoJsonReader.Read<Geometry>(text),
                _ => geoJsonReader.Read<Geometry>(raw.ToString() ?? string.Empty)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats a property value for comment output, handling nulls and special types.
    /// </summary>
    private static string FormatPropertyValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value switch
        {
            DateTime dt => dt.ToString("O"), // ISO 8601
            DateTimeOffset dto => dto.ToString("O"),
            Guid g => g.ToString(),
            _ => value.ToString() ?? "null"
        };
    }
}
