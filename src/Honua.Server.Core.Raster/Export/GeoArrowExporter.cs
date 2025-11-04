// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Raster.Export;

public interface IGeoArrowExporter
{
    Task<GeoArrowExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record GeoArrowExportResult(Stream Content, string FileName, long FeatureCount);

public sealed class GeoArrowExporter : IGeoArrowExporter
{
    private readonly WKBWriter _wkbWriter = new();

    public async Task<GeoArrowExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(CrsHelper.ParseCrs(contentCrs));
        var attributeFields = ResolveFields(layer);
        var schema = BuildSchema(layer, attributeFields, contentCrs);
        var builders = CreateBuilders(attributeFields.Count);
        var geometryBuilder = new BinaryArray.Builder();
        var recordCount = 0L;

        await foreach (var featureRecord in records.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendFeature(layer, query, geometryFactory, attributeFields, geometryBuilder, builders, featureRecord);
            recordCount++;
        }

        if (recordCount > int.MaxValue)
        {
            throw new InvalidOperationException("GeoArrow export does not support more than Int32.MaxValue features in a single batch.");
        }

        var arrays = BuildArrays(geometryBuilder, builders);
        RecordBatch? recordBatch = null;
        try
        {
            recordBatch = new RecordBatch(schema, arrays, (int)recordCount);

            var resultStream = new MemoryStream();
            try
            {
                using (var writer = new ArrowStreamWriter(resultStream, schema, leaveOpen: true))
                {
                    await writer.WriteRecordBatchAsync(recordBatch, cancellationToken).ConfigureAwait(false);
                    await writer.WriteEndAsync(cancellationToken).ConfigureAwait(false);
                }

                resultStream.Seek(0, SeekOrigin.Begin);
                var fileName = SanitizeFileName(layer.Id) + ".arrow";
                return new GeoArrowExportResult(resultStream, fileName, recordCount);
            }
            catch
            {
                resultStream.Dispose();
                throw;
            }
        }
        finally
        {
            recordBatch?.Dispose();

            foreach (var array in arrays)
            {
                array.Dispose();
            }
        }
    }

    private static Schema BuildSchema(LayerDefinition layer, IReadOnlyList<string> attributeFields, string contentCrs)
    {
        var geometryMetadata = new Dictionary<string, string>
        {
            ["encoding"] = "WKB",
            ["geometry_type"] = layer.GeometryType ?? string.Empty
        };

        // Add CRS metadata if available
        if (!string.IsNullOrWhiteSpace(contentCrs))
        {
            geometryMetadata["crs"] = contentCrs;
        }

        var fields = new List<Field>
        {
            new Field(
                "geometry",
                BinaryType.Default,
                true,
                geometryMetadata)
        };

        foreach (var field in attributeFields)
        {
            fields.Add(new Field(field, StringType.Default, true));
        }

        return new Schema(fields, null);
    }

    private static List<string> ResolveFields(LayerDefinition layer)
    {
        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(layer.IdField))
        {
            fields.Add(layer.IdField);
            seen.Add(layer.IdField);
        }

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(field.Name))
            {
                fields.Add(field.Name);
            }
        }

        return fields;
    }

    private static List<StringArray.Builder> CreateBuilders(int attributeCount)
    {
        var builders = new List<StringArray.Builder>();
        for (var i = 0; i < attributeCount; i++)
        {
            builders.Add(new StringArray.Builder());
        }

        return builders;
    }

    private void AppendFeature(
        LayerDefinition layer,
        FeatureQuery query,
        GeometryFactory geometryFactory,
        IReadOnlyList<string> attributeFields,
        BinaryArray.Builder geometryBuilder,
        List<StringArray.Builder> builders,
        FeatureRecord record)
    {
        var components = FeatureComponentBuilder.BuildComponents(layer, record, query);

        var geometry = ExtractGeometry(geometryFactory, components);
        if (geometry is null)
        {
            geometryBuilder.AppendNull();
        }
        else
        {
            geometryBuilder.Append(_wkbWriter.Write(geometry).AsSpan());
        }

        var values = ResolveFieldValues(layer, attributeFields, components);
        for (var i = 0; i < builders.Count; i++)
        {
            var value = values[i];
            if (value is null)
            {
                builders[i].AppendNull();
            }
            else
            {
                builders[i].Append(value);
            }
        }
    }

    private static Geometry? ExtractGeometry(GeometryFactory geometryFactory, FeatureComponents components)
    {
        JsonNode? node = components.GeometryNode;
        if (node is null && components.Geometry is JsonNode geometryNode)
        {
            node = geometryNode;
        }

        if (node is null)
        {
            return null;
        }

        try
        {
            var reader = new GeoJsonReader();
            var geometry = reader.Read<Geometry>(node.ToJsonString());
            if (geometry is null)
            {
                return null;
            }

            geometry = geometryFactory.CreateGeometry(geometry);
            if (geometry.SRID == 0)
            {
                geometry.SRID = geometryFactory.SRID;
            }

            return geometry;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string?[] ResolveFieldValues(LayerDefinition layer, IReadOnlyList<string> attributeFields, FeatureComponents components)
    {
        var values = new string?[attributeFields.Count];

        for (var i = 0; i < attributeFields.Count; i++)
        {
            var fieldName = attributeFields[i];
            object? value;
            if (string.Equals(fieldName, layer.IdField, StringComparison.OrdinalIgnoreCase))
            {
                value = components.RawId ?? components.FeatureId;
            }
            else
            {
                components.Properties.TryGetValue(fieldName, out value);
            }

            values[i] = NormalizeValue(value);
        }

        return values;
    }

    private static IArrowArray[] BuildArrays(BinaryArray.Builder geometryBuilder, List<StringArray.Builder> builders)
    {
        var arrays = new IArrowArray[builders.Count + 1];
        arrays[0] = geometryBuilder.Build();

        for (var i = 0; i < builders.Count; i++)
        {
            arrays[i + 1] = builders[i].Build();
        }

        return arrays;
    }

    private static string? NormalizeValue(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return ExtractJsonElement(element);
        }

        return value switch
        {
            Guid guid => guid.ToString(),
            Enum enumValue => enumValue.ToString(),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string? ExtractJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => element.TryGetInt64(out var intValue)
                ? intValue.ToString(CultureInfo.InvariantCulture)
                : element.TryGetDouble(out var doubleValue)
                    ? doubleValue.ToString(CultureInfo.InvariantCulture)
                    : element.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private static string SanitizeFileName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "collection" : value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return name;
    }
}
