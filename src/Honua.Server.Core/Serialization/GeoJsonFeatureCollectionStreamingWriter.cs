// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Streams a GeoJSON FeatureCollection directly to the response stream without buffering the entire payload in memory.
/// Preserves WFS metadata such as numberMatched/numberReturned and optional CRS information.
/// </summary>
public sealed class GeoJsonFeatureCollectionStreamingWriter : StreamingFeatureCollectionWriterBase
{
    private static readonly byte[] _comma = Encoding.UTF8.GetBytes(",");

    protected override string ContentType => "application/geo+json";
    protected override string FormatName => "GeoJSON";

    public GeoJsonFeatureCollectionStreamingWriter(ILogger<GeoJsonFeatureCollectionStreamingWriter> logger) : base(logger)
    {
    }

    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(context);

        var builder = new StringBuilder();
        builder.Append("{\"type\":\"FeatureCollection\"");

        if (context.TotalCount.HasValue)
        {
            builder.Append(",\"numberMatched\":");
            builder.Append(context.TotalCount.Value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append(",\"numberMatched\":\"unknown\"");
        }

        if (context.TargetWkid > 0)
        {
            builder.Append(",\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"");
            builder.Append(BuildSrsName(context.TargetWkid));
            builder.Append("\"}}");
        }

        builder.Append(",\"features\":[");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await outputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        if (!isFirst)
        {
            return outputStream.WriteAsync(_comma, cancellationToken).AsTask();
        }

        return Task.CompletedTask;
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(feature);
        Guard.NotNull(layer);

        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 4096);
        await using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = context.PrettyPrint,
            SkipValidation = false
        });

        WriteFeature(writer, feature, layer, context);

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (buffer.WrittenCount > 0)
        {
            await outputStream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);

        var builder = new StringBuilder();
        builder.Append(']');
        builder.Append(",\"numberReturned\":");
        builder.Append(featuresWritten.ToString(CultureInfo.InvariantCulture));
        builder.Append('}');

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await outputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteFeature(
        Utf8JsonWriter writer,
        FeatureRecord record,
        LayerDefinition layer,
        StreamingWriterContext context)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        if (layer.IdField.HasValue() && TryGetAttribute(record, layer.IdField, out var idValue) && idValue is not null)
        {
            WriteJsonValue(writer, "id", idValue);
        }

        writer.WritePropertyName("geometry");
        if (context.ReturnGeometry && TryGetAttribute(record, layer.GeometryField, out var geomValue) && geomValue is not null)
        {
            WriteGeometry(writer, geomValue, context.TargetWkid);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("properties");
        writer.WriteStartObject();

        var requestedProperties = context.PropertyNames is { Count: > 0 }
            ? new HashSet<string>(context.PropertyNames, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (requestedProperties != null && !requestedProperties.Contains(field.Name))
            {
                continue;
            }

            if (TryGetAttribute(record, field.Name, out var value))
            {
                WriteJsonValue(writer, field.Name, value);
            }
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static bool TryGetAttribute(FeatureRecord record, string? key, out object? value)
    {
        value = null;
        if (key.IsNullOrWhiteSpace())
        {
            return false;
        }

        return record.Attributes.TryGetValue(key, out value);
    }

    private static void WriteGeometry(Utf8JsonWriter writer, object geometryValue, int targetWkid)
    {
        Geometry? geometry = geometryValue switch
        {
            Geometry ntsGeometry => ntsGeometry,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object => ReadGeometryFromJson(jsonElement),
            JsonNode jsonNode => ReadGeometryFromJson(jsonNode),
            string text when !text.IsNullOrWhiteSpace() => ReadGeometryFromText(text),
            _ => null
        };

        if (geometry is null || geometry.IsEmpty)
        {
            writer.WriteNullValue();
            return;
        }

        if (targetWkid > 0 && geometry.SRID != targetWkid)
        {
            geometry = (Geometry)geometry.Copy();
            geometry.SRID = targetWkid;
        }

        var geoJsonWriter = new GeoJsonWriter();
        var geoJson = geoJsonWriter.Write(geometry);
        using var document = JsonDocument.Parse(geoJson);
        document.RootElement.WriteTo(writer);
    }

    private static Geometry? ReadGeometryFromJson(JsonElement element)
    {
        var text = element.GetRawText();
        return ReadGeometryFromText(text);
    }

    private static Geometry? ReadGeometryFromJson(JsonNode node)
    {
        return ReadGeometryFromText(node.ToJsonString());
    }

    private static Geometry? ReadGeometryFromText(string? text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            var geoJsonReader = new GeoJsonReader();
            return geoJsonReader.Read<Geometry>(text);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse geometry from text");
            return null;
        }
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string propertyName, object? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        switch (value)
        {
            case string s:
                writer.WriteString(propertyName, s);
                break;
            case bool b:
                writer.WriteBoolean(propertyName, b);
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
            case DateTime dt:
                writer.WriteString(propertyName, dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dto:
                writer.WriteString(propertyName, dto.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                break;
            case JsonElement element:
                writer.WritePropertyName(propertyName);
                element.WriteTo(writer);
                break;
            case JsonNode node:
                writer.WritePropertyName(propertyName);
                node.WriteTo(writer);
                break;
            default:
                writer.WriteString(propertyName, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static string BuildSrsName(int wkid)
        => wkid <= 0 ? "urn:ogc:def:crs:EPSG::4326" : $"urn:ogc:def:crs:EPSG::{wkid}";
}
