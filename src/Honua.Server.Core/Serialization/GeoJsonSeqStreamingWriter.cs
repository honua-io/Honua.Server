// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

#nullable enable

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Streaming GeoJSON Text Sequences (GeoJSONSeq) writer implementing RFC 8142.
/// Writes newline-delimited GeoJSON features without wrapping in a FeatureCollection.
/// Also known as GeoJSON-LD (Line Delimited) or ndjson-geo.
///
/// Format: Each feature is a complete GeoJSON Feature on a single line, separated by newlines.
/// This format is ideal for:
/// - Line-by-line streaming processing
/// - Append-only log files
/// - Large datasets that don't fit in memory
/// - Real-time data feeds
/// </summary>
public sealed class GeoJsonSeqStreamingWriter : StreamingFeatureCollectionWriterBase
{
    private static readonly byte[] _newlineBytes = Encoding.UTF8.GetBytes("\n");

    // RFC 8142 recommends using record separator (ASCII 0x1E) before each JSON text
    // This is optional but helps with recovery from parsing errors
    private static readonly byte[] _recordSeparator = new byte[] { 0x1E };

    private readonly bool _useRecordSeparator;

    protected override string ContentType => "application/geo+json-seq";
    protected override string FormatName => "GeoJSONSeq";

    /// <summary>
    /// Creates a new GeoJSON Text Sequences streaming writer.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="useRecordSeparator">Whether to use ASCII RS (0x1E) record separators (RFC 8142 compliant)</param>
    public GeoJsonSeqStreamingWriter(ILogger<GeoJsonSeqStreamingWriter> logger, bool useRecordSeparator = false)
        : base(logger)
    {
        _useRecordSeparator = useRecordSeparator;
    }

    protected override Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // GeoJSONSeq has no header - each feature is independent
        return Task.CompletedTask;
    }

    protected override async Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // Write newline before each feature except the first
        if (!isFirst)
        {
            await outputStream.WriteAsync(_newlineBytes, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // Optionally write record separator (RFC 8142)
        if (_useRecordSeparator)
        {
            await outputStream.WriteAsync(_recordSeparator, cancellationToken).ConfigureAwait(false);
        }

        // Write feature as compact JSON on a single line
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 4096);
        await using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions
        {
            Indented = false, // Must be compact for line-delimited format
            SkipValidation = false
        });

        WriteFeature(writer, feature, layer, context);

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (bufferWriter.WrittenCount > 0)
        {
            await outputStream.WriteAsync(bufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        // Write final newline
        await outputStream.WriteAsync(_newlineBytes, cancellationToken).ConfigureAwait(false);
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void WriteFeature(
        Utf8JsonWriter writer,
        FeatureRecord record,
        LayerDefinition layer,
        StreamingWriterContext context)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        // Write ID if available
        if (TryGetAttribute(record, layer.IdField, out var idValue) && idValue != null)
        {
            WriteJsonValue(writer, "id", idValue);
        }

        // Write geometry if requested
        if (context.ReturnGeometry &&
            record.Attributes.TryGetValue(layer.GeometryField, out var geomObj) &&
            geomObj is Geometry ntsGeom &&
            !ntsGeom.IsEmpty)
        {
            // THREAD-SAFETY FIX: Create per-request GeoJsonWriter instead of static shared instance
            var geoJsonWriter = new GeoJsonWriter();

            writer.WritePropertyName("geometry");
            var geoJsonGeom = geoJsonWriter.Write(ntsGeom);
            using var doc = JsonDocument.Parse(geoJsonGeom);
            doc.RootElement.WriteTo(writer);
        }
        else
        {
            writer.WriteNull("geometry");
        }

        // Write properties
        writer.WritePropertyName("properties");
        writer.WriteStartObject();

        // SECURITY FIX (Issue #21): Respect PropertyNames filter to prevent data leakage
        // Only write properties that were explicitly requested (or all if no filter specified)
        var requestedProperties = context.PropertyNames != null && context.PropertyNames.Count > 0
            ? new HashSet<string>(context.PropertyNames, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var field in layer.Fields)
        {
            // Skip geometry field
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Apply property filter if specified
            if (requestedProperties != null && !requestedProperties.Contains(field.Name))
            {
                continue;
            }

            if (TryGetAttribute(record, field.Name, out var value))
            {
                WriteJsonValue(writer, field.Name, value);
            }
        }

        writer.WriteEndObject(); // properties
        writer.WriteEndObject(); // feature
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string propertyName, object? value)
    {
        if (value == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        switch (value)
        {
            case string s:
                writer.WriteString(propertyName, s);
                break;
            case int i:
                writer.WriteNumber(propertyName, i);
                break;
            case long l:
                writer.WriteNumber(propertyName, l);
                break;
            case float f:
                writer.WriteNumber(propertyName, f);
                break;
            case double d:
                writer.WriteNumber(propertyName, d);
                break;
            case decimal m:
                writer.WriteNumber(propertyName, m);
                break;
            case bool b:
                writer.WriteBoolean(propertyName, b);
                break;
            case DateTime dt:
                writer.WriteString(propertyName, dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteString(propertyName, dto.ToString("O"));
                break;
            case Guid g:
                writer.WriteString(propertyName, g.ToString());
                break;
            default:
                writer.WriteString(propertyName, value.ToString());
                break;
        }
    }

    private static bool TryGetAttribute(FeatureRecord record, string fieldName, out object? value)
    {
        if (record.Attributes.TryGetValue(fieldName, out value))
        {
            return true;
        }

        // Case-insensitive fallback
        foreach (var (key, val) in record.Attributes)
        {
            if (key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                value = val;
                return true;
            }
        }

        value = null;
        return false;
    }
}
