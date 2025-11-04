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
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ParquetSharp;
using ParquetSharp.IO;
using ParquetSharp.Schema;

namespace Honua.Server.Core.Raster.Export;

public interface IGeoParquetExporter
{
    Task<GeoParquetExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record GeoParquetExportResult(Stream Content, string FileName, long FeatureCount);

/// <summary>
/// Exports features to GeoParquet format (v1.1.0 specification)
/// </summary>
public sealed class GeoParquetExporter : IGeoParquetExporter
{
    private readonly WKBWriter _wkbWriter = new();
    private const int DefaultRowGroupSize = 4096;

    public async Task<GeoParquetExportResult> ExportAsync(
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
        var fieldMap = layer.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        var geometryColumn = new List<byte[]?>(DefaultRowGroupSize);
        var bboxXMin = new List<double?>(DefaultRowGroupSize);
        var bboxYMin = new List<double?>(DefaultRowGroupSize);
        var bboxXMax = new List<double?>(DefaultRowGroupSize);
        var bboxYMax = new List<double?>(DefaultRowGroupSize);
        var attributeColumns = CreateAttributeColumns(attributeFields.Count, DefaultRowGroupSize);

        var geometryTypes = new HashSet<string>();
        var globalBounds = new GlobalBoundingBox();
        var recordCount = 0L;

        var columns = BuildParquetColumns(layer, attributeFields);
        using var writerPropertiesBuilder = new WriterPropertiesBuilder();
        using var writerProperties = writerPropertiesBuilder.Build();
        var keyValueMetadata = new Dictionary<string, string>();

        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-geoparquet-{Guid.NewGuid():N}.parquet");
        FileStream? fileStream = null;
        var ownershipTransferred = false;
        try
        {
            // RESOURCE LEAK FIX: Add DeleteOnClose to ensure cleanup even if process crashes
            // TemporaryFileStream also handles cleanup, but this provides defense in depth
            fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 1 << 16,
                FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            using var parquetStream = new ManagedOutputStream(fileStream, leaveOpen: true);
            using var parquetWriter = new ParquetFileWriter(parquetStream, columns, writerProperties, keyValueMetadata);

            await foreach (var featureRecord in records.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                AppendFeature(
                    layer,
                    query,
                    geometryFactory,
                    attributeFields,
                    fieldMap,
                    featureRecord,
                    geometryColumn,
                    bboxXMin,
                    bboxYMin,
                    bboxXMax,
                    bboxYMax,
                    attributeColumns,
                    geometryTypes,
                    globalBounds,
                    _wkbWriter);

                recordCount++;

                if (geometryColumn.Count >= DefaultRowGroupSize)
                {
                    FlushRowGroup(
                        parquetWriter,
                        geometryColumn,
                        bboxXMin,
                        bboxYMin,
                        bboxXMax,
                        bboxYMax,
                        attributeColumns,
                        attributeFields,
                        fieldMap);
                }
            }

            if (geometryColumn.Count > 0)
            {
                FlushRowGroup(
                    parquetWriter,
                    geometryColumn,
                    bboxXMin,
                    bboxYMin,
                    bboxXMax,
                    bboxYMax,
                    attributeColumns,
                    attributeFields,
                    fieldMap);
            }

            var geoMetadata = BuildGeoParquetMetadata(contentCrs, geometryTypes, globalBounds, recordCount > 0);
            if (!string.IsNullOrWhiteSpace(geoMetadata))
            {
                keyValueMetadata["geo"] = geoMetadata;
            }

            parquetWriter.Close();

            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            fileStream.Position = 0;

            var resultStream = new TemporaryFileStream(fileStream, tempPath);
            ownershipTransferred = true;
            var fileName = SanitizeFileName(layer.Id) + ".parquet";
            return new GeoParquetExportResult(resultStream, fileName, recordCount);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                fileStream?.Dispose();
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }
            }

            ClearBuffers(geometryColumn, bboxXMin, bboxYMin, bboxXMax, bboxYMax, attributeColumns);
        }
    }

    private static List<List<object?>> CreateAttributeColumns(int attributeCount, int capacity = 0)
    {
        var columns = new List<List<object?>>(attributeCount);
        for (var i = 0; i < attributeCount; i++)
        {
            columns.Add(capacity > 0 ? new List<object?>(capacity) : new List<object?>());
        }

        return columns;
    }

    private static Column[] BuildParquetColumns(LayerDefinition layer, IReadOnlyList<string> attributeFields)
    {
        var columns = new List<Column>
        {
            new Column<byte[]?>("geometry"),
            new Column<double?>("bbox.xmin"),
            new Column<double?>("bbox.ymin"),
            new Column<double?>("bbox.xmax"),
            new Column<double?>("bbox.ymax")
        };

        var fieldMap = layer.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var fieldName in attributeFields)
        {
            if (fieldMap.TryGetValue(fieldName, out var fieldDef) && !string.IsNullOrWhiteSpace(fieldDef.DataType))
            {
                columns.Add(CreateColumnForType(fieldName, fieldDef.DataType));
            }
            else
            {
                // Default to string if type is unknown
                columns.Add(new Column<string?>(fieldName));
            }
        }

        return columns.ToArray();
    }

    /// <summary>
    /// Bug 27 fix: Configure proper Parquet logical types for decimals and booleans.
    /// </summary>
    private static Column CreateColumnForType(string fieldName, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" or "int32" or "smallint" or "short" => new Column<int?>(fieldName, LogicalType.Int(32, isSigned: true)),
            "long" or "int64" or "bigint" => new Column<long?>(fieldName, LogicalType.Int(64, isSigned: true)),
            "float" or "single" => new Column<float?>(fieldName),
            // Bug 27 fix: Decimal types should use double without forcing nullable bool
            "double" or "real" => new Column<double?>(fieldName),
            "numeric" or "decimal" => new Column<double?>(fieldName, LogicalType.Decimal(precision: 18, scale: 9)),
            "bool" or "boolean" or "bit" => new Column<bool?>(fieldName),
            "date" or "datetime" or "timestamp" or "timestamptz" => new Column<DateTimeOffset?>(fieldName, LogicalType.Timestamp(isAdjustedToUtc: true, TimeUnit.Millis)),
            _ => new Column<string?>(fieldName, LogicalType.String())
        };
    }

    /// <summary>
    /// Bug 28 fix: Emit valid GeoParquet metadata even without geometry.
    /// </summary>
    private static string BuildGeoParquetMetadata(
        string contentCrs,
        HashSet<string> geometryTypes,
        GlobalBoundingBox globalBbox,
        bool hasGeometry)
    {
        double[]? bbox = null;

        // Bug 28 fix: Only emit bbox if we actually have bounds, not just if hasGeometry
        if (globalBbox.HasBounds)
        {
            bbox = new[]
            {
                globalBbox.MinX,
                globalBbox.MinY,
                globalBbox.MaxX,
                globalBbox.MaxY
            };
        }

        var metadata = new
        {
            version = "1.1.0",
            primary_column = "geometry",
            columns = new
            {
                geometry = new
                {
                    encoding = "WKB",
                    // Bug 28 fix: Emit empty array for geometry_types if no geometry present
                    geometry_types = geometryTypes.OrderBy(x => x).ToArray(),
                    crs = !string.IsNullOrWhiteSpace(contentCrs)
                        ? (object)new { type = "name", properties = new { name = contentCrs } }
                        : null,
                    bbox,
                    covering = new
                    {
                        bbox = new
                        {
                            xmin = new[] { "bbox", "xmin" },
                            ymin = new[] { "bbox", "ymin" },
                            xmax = new[] { "bbox", "xmax" },
                            ymax = new[] { "bbox", "ymax" }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(metadata, JsonSerializerOptionsRegistry.Web);
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

    private static void AppendFeature(
        LayerDefinition layer,
        FeatureQuery query,
        GeometryFactory geometryFactory,
        IReadOnlyList<string> attributeFields,
        IReadOnlyDictionary<string, FieldDefinition> fieldMap,
        FeatureRecord record,
        List<byte[]?> geometryColumn,
        List<double?> bboxXMin,
        List<double?> bboxYMin,
        List<double?> bboxXMax,
        List<double?> bboxYMax,
        IList<List<object?>> attributeColumns,
        HashSet<string> geometryTypes,
        GlobalBoundingBox globalBbox,
        WKBWriter wkbWriter)
    {
        var components = FeatureComponentBuilder.BuildComponents(layer, record, query);

        var geometry = ExtractGeometry(geometryFactory, components);
        if (geometry is null)
        {
            geometryColumn.Add(null);
            bboxXMin.Add(null);
            bboxYMin.Add(null);
            bboxXMax.Add(null);
            bboxYMax.Add(null);
        }
        else
        {
            var wkb = wkbWriter.Write(geometry);
            geometryColumn.Add(wkb);

            geometryTypes.Add(geometry.GeometryType);

            var envelope = geometry.EnvelopeInternal;
            bboxXMin.Add(envelope.MinX);
            bboxYMin.Add(envelope.MinY);
            bboxXMax.Add(envelope.MaxX);
            bboxYMax.Add(envelope.MaxY);

            globalBbox.Update(envelope);
        }

        var values = ResolveFieldValues(layer, attributeFields, fieldMap, components);
        for (var i = 0; i < attributeColumns.Count; i++)
        {
            var value = values[i];
            attributeColumns[i].Add(value);
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

    private static object?[] ResolveFieldValues(
        LayerDefinition layer,
        IReadOnlyList<string> attributeFields,
        IReadOnlyDictionary<string, FieldDefinition> fieldMap,
        FeatureComponents components)
    {
        var values = new object?[attributeFields.Count];

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

            // Extract from JsonElement if needed
            if (value is JsonElement element)
            {
                value = ExtractJsonElementTyped(element);
            }

            // Convert to appropriate type based on field definition
            if (fieldMap.TryGetValue(fieldName, out var fieldDef) && !string.IsNullOrWhiteSpace(fieldDef.DataType))
            {
                values[i] = ConvertToTypedValue(value, fieldDef.DataType);
            }
            else
            {
                // Default to string conversion
                values[i] = NormalizeValue(value);
            }
        }

        return values;
    }

    private static object? ConvertToTypedValue(object? value, string dataType)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        return dataType.ToLowerInvariant() switch
        {
            "int" or "integer" or "int32" or "smallint" or "short" => ConvertToInt32(value),
            "long" or "int64" or "bigint" => ConvertToInt64(value),
            "float" or "single" => ConvertToFloat(value),
            "double" or "real" or "numeric" or "decimal" => ConvertToDouble(value),
            "bool" or "boolean" or "bit" => ConvertToBoolean(value),
            "date" or "datetime" or "timestamp" or "timestamptz" => ConvertToDateTimeOffset(value),
            _ => NormalizeValue(value)
        };
    }

    private static int? ConvertToInt32(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            double d => (int)d,
            float f => (int)f,
            decimal dec => (int)dec,
            string str when int.TryParse(str, out var result) => result,
            _ => null
        };
    }

    private static long? ConvertToInt64(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            double d => (long)d,
            float f => (long)f,
            decimal dec => (long)dec,
            string str when long.TryParse(str, out var result) => result,
            _ => null
        };
    }

    private static float? ConvertToFloat(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            decimal dec => (float)dec,
            string str when float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
            _ => null
        };
    }

    private static double? ConvertToDouble(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
            _ => null
        };
    }

    private static bool? ConvertToBoolean(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string str when bool.TryParse(str, out var result) => result,
            string str => str.Equals("1", StringComparison.Ordinal) || str.Equals("yes", StringComparison.OrdinalIgnoreCase) || str.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => null
        };
    }

    private static DateTimeOffset? ConvertToDateTimeOffset(object? value)
    {
        if (value is null or DBNull) return null;
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt.ToUniversalTime()),
            string str when DateTimeOffset.TryParse(str, out var result) => result,
            long ticks => DateTimeOffset.FromUnixTimeMilliseconds(ticks),
            _ => null
        };
    }

    private static object? ExtractJsonElementTyped(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue :
                                    element.TryGetInt64(out var longValue) ? longValue :
                                    element.TryGetDouble(out var doubleValue) ? doubleValue :
                                    (object)element.GetDecimal(),
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private static void WriteColumn<T>(RowGroupWriter rowGroupWriter, T[] values)
    {
        using var columnWriter = rowGroupWriter.NextColumn().LogicalWriter<T>();
        columnWriter.WriteBatch(values);
    }

    private static void FlushRowGroup(
        ParquetFileWriter parquetWriter,
        List<byte[]?> geometryColumn,
        List<double?> bboxXMin,
        List<double?> bboxYMin,
        List<double?> bboxXMax,
        List<double?> bboxYMax,
        IList<List<object?>> attributeColumns,
        IReadOnlyList<string> attributeFields,
        IReadOnlyDictionary<string, FieldDefinition> fieldMap)
    {
        if (geometryColumn.Count == 0)
        {
            return;
        }

        using var rowGroupWriter = parquetWriter.AppendRowGroup();

        WriteColumn(rowGroupWriter, geometryColumn.ToArray());
        WriteColumn(rowGroupWriter, bboxXMin.ToArray());
        WriteColumn(rowGroupWriter, bboxYMin.ToArray());
        WriteColumn(rowGroupWriter, bboxXMax.ToArray());
        WriteColumn(rowGroupWriter, bboxYMax.ToArray());

        for (var i = 0; i < attributeColumns.Count; i++)
        {
            var fieldName = attributeFields[i];
            if (fieldMap.TryGetValue(fieldName, out var fieldDef) && !string.IsNullOrWhiteSpace(fieldDef.DataType))
            {
                WriteTypedColumn(rowGroupWriter, attributeColumns[i], fieldDef.DataType);
            }
            else
            {
                WriteColumn(
                    rowGroupWriter,
                    attributeColumns[i]
                        .Select(value => value switch
                        {
                            null => null,
                            string str => str,
                            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                            _ => value.ToString()
                        })
                        .ToArray());
            }
        }

        ClearBuffers(geometryColumn, bboxXMin, bboxYMin, bboxXMax, bboxYMax, attributeColumns);
    }

    private static void WriteTypedColumn(RowGroupWriter rowGroupWriter, List<object?> values, string dataType)
    {
        switch (dataType.ToLowerInvariant())
        {
            case "int" or "integer" or "int32" or "smallint" or "short":
                WriteColumn(rowGroupWriter, values.Cast<int?>().ToArray());
                break;
            case "long" or "int64" or "bigint":
                WriteColumn(rowGroupWriter, values.Cast<long?>().ToArray());
                break;
            case "float" or "single":
                WriteColumn(rowGroupWriter, values.Cast<float?>().ToArray());
                break;
            case "double" or "real" or "numeric" or "decimal":
                WriteColumn(rowGroupWriter, values.Cast<double?>().ToArray());
                break;
            case "bool" or "boolean" or "bit":
                WriteColumn(rowGroupWriter, values.Cast<bool?>().ToArray());
                break;
            case "date" or "datetime" or "timestamp" or "timestamptz":
                WriteColumn(rowGroupWriter, values.Cast<DateTimeOffset?>().ToArray());
                break;
            default:
                WriteColumn(rowGroupWriter, values.Cast<string?>().ToArray());
                break;
        }
    }

    private static void ClearBuffers(
        List<byte[]?> geometryColumn,
        List<double?> bboxXMin,
        List<double?> bboxYMin,
        List<double?> bboxXMax,
        List<double?> bboxYMax,
        IList<List<object?>> attributeColumns)
    {
        geometryColumn.Clear();
        bboxXMin.Clear();
        bboxYMin.Clear();
        bboxXMax.Clear();
        bboxYMax.Clear();
        foreach (var column in attributeColumns)
        {
            column.Clear();
        }
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

    private sealed class GlobalBoundingBox
    {
        public double MinX { get; private set; } = double.PositiveInfinity;
        public double MinY { get; private set; } = double.PositiveInfinity;
        public double MaxX { get; private set; } = double.NegativeInfinity;
        public double MaxY { get; private set; } = double.NegativeInfinity;

        public bool HasBounds { get; private set; }

        public void Update(Envelope envelope)
        {
            if (!HasBounds)
            {
                MinX = envelope.MinX;
                MinY = envelope.MinY;
                MaxX = envelope.MaxX;
                MaxY = envelope.MaxY;
                HasBounds = true;
                return;
            }

            if (envelope.MinX < MinX) MinX = envelope.MinX;
            if (envelope.MinY < MinY) MinY = envelope.MinY;
            if (envelope.MaxX > MaxX) MaxX = envelope.MaxX;
            if (envelope.MaxY > MaxY) MaxY = envelope.MaxY;
        }
    }

    private sealed class TemporaryFileStream : Stream
    {
        private readonly FileStream _inner;
        private readonly string _path;
        private bool _disposed;

        public TemporaryFileStream(FileStream inner, string path)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override bool CanTimeout => _inner.CanTimeout;

        public override int ReadTimeout
        {
            get => _inner.ReadTimeout;
            set => _inner.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => _inner.WriteTimeout;
            set => _inner.WriteTimeout = value;
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    TryDelete();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await _inner.DisposeAsync().ConfigureAwait(false);
                TryDelete();
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        private void TryDelete()
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
