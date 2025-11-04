// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FlatGeobuf.NTS;
using Google.FlatBuffers;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using FgbColumnType = FlatGeobuf.ColumnType;
using FgbColumnT = FlatGeobuf.ColumnT;
using FgbCrsT = FlatGeobuf.CrsT;
using FgbGeometryType = FlatGeobuf.GeometryType;
using FgbHeaderT = FlatGeobuf.HeaderT;
using NtsFeature = NetTopologySuite.Features.Feature;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Export;

public interface IFlatGeobufExporter
{
    Task<FlatGeobufExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record FlatGeobufExportResult(Stream Content, string FileName, long FeatureCount);

public sealed class FlatGeobufExporter : IFlatGeobufExporter
{
    public FlatGeobufExporter()
    {
    }

    public async Task<FlatGeobufExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var srid = CrsHelper.ParseCrs(contentCrs);
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid);
        var geometryType = ResolveGeometryType(layer.GeometryType);
        var dimensions = ResolveDimensions(layer);

        var columns = BuildColumnMetadata(layer);
        var columnTypes = new Dictionary<string, FgbColumnType>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            columnTypes[column.Name] = column.Type;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-flat-{Guid.NewGuid():N}.fgb");
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

            // BUG FIX #19: Materialize async features to avoid sync-over-async pattern
            var headerTemplate = CreateHeaderTemplate(layer, geometryType, dimensions, columns, srid);
            var featureBuffers = new List<byte[]>();
            var indexNodes = new List<NodeItem>();
            ulong featureOffset = 0;
            long featureCount = 0;

            await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var feature = CreateFeature(layer, query, record, geometryFactory, columnTypes, columns, allowDynamicColumns: false);
                var buffer = FeatureConversions.ToByteBuffer(feature, headerTemplate);
                var slice = buffer.ToReadOnlyMemory(buffer.Position, buffer.Length - buffer.Position);
                var featureBytes = slice.ToArray();
                featureBuffers.Add(featureBytes);

                var envelope = feature.Geometry?.EnvelopeInternal;
                indexNodes.Add(NodeItem.FromEnvelope(envelope, featureOffset));
                featureOffset += (ulong)featureBytes.LongLength;
                featureCount++;
            }
            var indexResult = HilbertRTreeBuilder.Build(indexNodes, featureCount > 0 ? HilbertRTreeBuilder.DefaultNodeSize : (ushort)0);
            var headerMemory = BuildHeaderBuffer(headerTemplate, featureCount, indexResult);

            await fileStream.WriteAsync(FlatGeobuf.Constants.MagicBytes, cancellationToken).ConfigureAwait(false);
            await fileStream.WriteAsync(headerMemory, cancellationToken).ConfigureAwait(false);

            if (indexResult.IndexBytes.Length > 0)
            {
                await fileStream.WriteAsync(indexResult.IndexBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            foreach (var featureBytes in featureBuffers)
            {
                await fileStream.WriteAsync(featureBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            fileStream.Position = 0;

            var fileName = SanitizeFileName(layer.Id) + ".fgb";
            var resultStream = new TemporaryFileStream(fileStream, tempPath);
            ownershipTransferred = true;

            return new FlatGeobufExportResult(resultStream, fileName, featureCount);
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
        }
    }

    private static NtsFeature CreateFeature(
        LayerDefinition layer,
        FeatureQuery query,
        FeatureRecord record,
        GeometryFactory geometryFactory,
        IDictionary<string, FgbColumnType> columnTypes,
        IList<ColumnMeta> columns,
        bool allowDynamicColumns = true)
    {
        var components = FeatureComponentBuilder.BuildComponents(layer, record, query);

        NtsGeometry? geometry = null;
        if (components.GeometryNode is not null)
        {
            geometry = ParseGeometry(components.GeometryNode, geometryFactory);
        }
        else if (components.Geometry is JsonNode geometryNode)
        {
            geometry = ParseGeometry(geometryNode, geometryFactory);
        }

        if (geometry is not null && geometry.SRID == 0)
        {
            geometry.SRID = geometryFactory.SRID;
        }

        var attributes = new AttributesTable();

        if (layer.IdField.HasValue())
        {
            var idValue = components.RawId ?? components.FeatureId;
            var normalizedId = NormalizeAttributeValue(idValue);
            if (allowDynamicColumns)
            {
                EnsureColumnMeta(layer.IdField, normalizedId, columnTypes, columns);
            }

            if (allowDynamicColumns || columnTypes.ContainsKey(layer.IdField))
            {
                attributes.Add(layer.IdField, ConvertValueForColumn(layer.IdField, normalizedId, columnTypes, columns));
            }
        }

        foreach (var property in components.Properties)
        {
            var normalized = NormalizeAttributeValue(property.Value);
            if (allowDynamicColumns)
            {
                EnsureColumnMeta(property.Key, normalized, columnTypes, columns);
            }

            if (allowDynamicColumns || columnTypes.ContainsKey(property.Key))
            {
                attributes.Add(property.Key, ConvertValueForColumn(property.Key, normalized, columnTypes, columns));
            }
        }

        foreach (var name in attributes.GetNames().ToArray())
        {
            var value = attributes[name];
            switch (value)
            {
                case TimeSpan:
                    attributes[name] = value.ToString();
                    break;
            }
        }

        return new Feature(geometry, attributes);
    }

    private static NtsGeometry? ParseGeometry(JsonNode node, GeometryFactory geometryFactory)
    {
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
            return geometry;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FgbColumnType MapColumnType(string? dataType)
    {
        if (dataType.IsNullOrWhiteSpace())
        {
            return FgbColumnType.String;
        }

        return dataType.Trim().ToLowerInvariant() switch
        {
            "bool" or "boolean" => FgbColumnType.Bool,
            "sbyte" or "tinyint" => FgbColumnType.Byte,
            "byte" or "tinyint unsigned" => FgbColumnType.UByte,
            "short" or "smallint" or "int2" => FgbColumnType.Short,
            "smallint unsigned" or "uint16" or "ushort" => FgbColumnType.UShort,
            "int" or "integer" or "int4" or "int32" => FgbColumnType.Long,
            "int unsigned" or "uint" or "uint32" => FgbColumnType.ULong,
            "long" or "int8" or "int64" or "bigint" => FgbColumnType.Long,
            "bigint unsigned" or "uint64" or "ulong" => FgbColumnType.ULong,
            "float" or "real" or "float4" => FgbColumnType.Float,
            "double" or "float8" => FgbColumnType.Double,
            "decimal" or "numeric" or "money" => FgbColumnType.Double,
            "date" or "datetime" or "timestamp" or "timestamptz" or "smalldatetime" => FgbColumnType.DateTime,
            "json" or "jsonb" => FgbColumnType.Json,
            "blob" or "bytea" or "varbinary" or "binary" or "image" => FgbColumnType.Binary,
            "uuid" => FgbColumnType.String,
            _ when dataType.Contains("char", StringComparison.OrdinalIgnoreCase) => FgbColumnType.String,
            _ when dataType.Contains("text", StringComparison.OrdinalIgnoreCase) => FgbColumnType.String,
            _ => FgbColumnType.String
        };
    }

    private static FgbGeometryType ResolveGeometryType(string? geometryType)
    {
        if (geometryType.IsNullOrWhiteSpace())
        {
            return FgbGeometryType.Unknown;
        }

        return geometryType.ToLowerInvariant() switch
        {
            "point" => FgbGeometryType.Point,
            "multipoint" => FgbGeometryType.MultiPoint,
            "linestring" => FgbGeometryType.LineString,
            "multilinestring" => FgbGeometryType.MultiLineString,
            "polyline" => FgbGeometryType.MultiLineString,
            "polygon" => FgbGeometryType.Polygon,
            "multipolygon" => FgbGeometryType.MultiPolygon,
            "geometrycollection" => FgbGeometryType.GeometryCollection,
            _ => FgbGeometryType.Unknown
        };
    }

    private static byte ResolveDimensions(LayerDefinition layer)
    {
        return 2;
    }

    private static IList<ColumnMeta> BuildColumnMetadata(LayerDefinition layer)
    {
        var columns = new List<ColumnMeta>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddColumn(string name, string? dataType)
        {
            if (name.IsNullOrWhiteSpace() || !seen.Add(name))
            {
                return;
            }

            columns.Add(new ColumnMeta
            {
                Name = name,
                Type = MapColumnType(dataType)
            });
        }

        if (layer.IdField.HasValue())
        {
            var idField = layer.Fields.FirstOrDefault(f => string.Equals(f.Name, layer.IdField, StringComparison.OrdinalIgnoreCase));
            AddColumn(layer.IdField, idField?.DataType ?? idField?.StorageType);
        }

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddColumn(field.Name, field.DataType ?? field.StorageType);
        }

        return columns;
    }

    private static void EnsureColumnMeta(string name, object? value, IDictionary<string, FgbColumnType> columnTypes, IList<ColumnMeta> columns)
    {
        if (name.IsNullOrWhiteSpace() || columnTypes.ContainsKey(name) || value is null)
        {
            return;
        }

        var inferred = InferColumnType(value);
        columnTypes[name] = inferred;
        columns.Add(new ColumnMeta
        {
            Name = name,
            Type = inferred
        });
    }

    private static FgbColumnType InferColumnType(object value)
    {
        return value switch
        {
            bool => FgbColumnType.Bool,
            sbyte => FgbColumnType.Byte,
            byte => FgbColumnType.UByte,
            short => FgbColumnType.Short,
            ushort => FgbColumnType.UShort,
            int => FgbColumnType.Int,
            uint => FgbColumnType.UInt,
            long => FgbColumnType.Long,
            ulong => FgbColumnType.ULong,
            float => FgbColumnType.Float,
            double => FgbColumnType.Double,
            decimal => FgbColumnType.Double,
            DateTime => FgbColumnType.DateTime,
            DateTimeOffset => FgbColumnType.DateTime,
            byte[] => FgbColumnType.Binary,
            ReadOnlyMemory<byte> => FgbColumnType.Binary,
            _ => value is string text && LooksLikeJson(text) ? FgbColumnType.Json : FgbColumnType.String
        };
    }

    private static bool LooksLikeJson(string text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return false;
        }

        text = text.Trim();
        if ((text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]")))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return false;
    }

    private static object? ConvertValueForColumn(
        string name,
        object? value,
        IDictionary<string, FgbColumnType> columnTypes,
        IList<ColumnMeta> columns)
    {
        if (value is null)
        {
            return null;
        }

        if (!columnTypes.TryGetValue(name, out var columnType))
        {
            return value;
        }

        try
        {
            return ConvertValue(columnType, value);
        }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        {
            if (TryPromoteColumnType(name, value, columnTypes, columns, columnType, out var promotedType))
            {
                return ConvertValue(promotedType, value);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    private static object? ConvertValue(FgbColumnType columnType, object value)
    {
        return columnType switch
        {
            FgbColumnType.Bool => value is bool b ? b : Convert.ToBoolean(value, CultureInfo.InvariantCulture),
            FgbColumnType.Byte => value is sbyte sb ? sb : Convert.ToSByte(value, CultureInfo.InvariantCulture),
            FgbColumnType.UByte => value is byte ub ? ub : Convert.ToByte(value, CultureInfo.InvariantCulture),
            FgbColumnType.Short => value is short s ? s : Convert.ToInt16(value, CultureInfo.InvariantCulture),
            FgbColumnType.UShort => value is ushort us ? us : Convert.ToUInt16(value, CultureInfo.InvariantCulture),
            FgbColumnType.Int => value is int i ? i : Convert.ToInt32(value, CultureInfo.InvariantCulture),
            FgbColumnType.UInt => value is uint ui ? ui : Convert.ToUInt32(value, CultureInfo.InvariantCulture),
            FgbColumnType.Long => value is long l ? l : Convert.ToInt64(value, CultureInfo.InvariantCulture),
            FgbColumnType.ULong => value is ulong ul ? ul : Convert.ToUInt64(value, CultureInfo.InvariantCulture),
            FgbColumnType.Float => value is float f ? f : Convert.ToSingle(value, CultureInfo.InvariantCulture),
            FgbColumnType.Double => value is double d ? d : Convert.ToDouble(value, CultureInfo.InvariantCulture),
            FgbColumnType.String => value is string str ? str : Convert.ToString(value, CultureInfo.InvariantCulture),
            FgbColumnType.Json => value switch
            {
                string json => json,
                JsonElement element => element.GetRawText(),
                _ => JsonSerializer.Serialize(value)
            },
            FgbColumnType.DateTime => value switch
            {
                DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime(),
                DateTimeOffset dto => dto.UtcDateTime,
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
                _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            },
            FgbColumnType.Binary => value switch
            {
                byte[] bytes => bytes,
                ReadOnlyMemory<byte> memory => memory.ToArray(),
                string s => TryDecodeBase64(s, out var decoded) ? decoded : Encoding.UTF8.GetBytes(s),
                _ => value
            },
            _ => value
        };
    }

    private static bool TryPromoteColumnType(
        string name,
        object value,
        IDictionary<string, FgbColumnType> columnTypes,
        IList<ColumnMeta> columns,
        FgbColumnType currentType,
        out FgbColumnType promotedType)
    {
        var inferred = InferColumnType(value);
        promotedType = DeterminePromotion(currentType, inferred);

        if (promotedType == currentType)
        {
            if (currentType == FgbColumnType.String)
            {
                return false;
            }

            promotedType = FgbColumnType.String;
        }

        columnTypes[name] = promotedType;

        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var column = columns[i];
                column.Type = promotedType;
                columns[i] = column;
                break;
            }
        }

        return true;
    }

    private static FgbColumnType DeterminePromotion(FgbColumnType current, FgbColumnType candidate)
    {
        if (current == candidate)
        {
            return current;
        }

        var currentRank = GetTypeRank(current);
        var candidateRank = GetTypeRank(candidate);
        return candidateRank > currentRank ? candidate : current;
    }

    private static int GetTypeRank(FgbColumnType type)
    {
        return type switch
        {
            FgbColumnType.Bool => 0,
            FgbColumnType.Byte or FgbColumnType.UByte => 1,
            FgbColumnType.Short or FgbColumnType.UShort => 2,
            FgbColumnType.Int or FgbColumnType.UInt => 3,
            FgbColumnType.Long or FgbColumnType.ULong => 4,
            FgbColumnType.Float => 5,
            FgbColumnType.Double => 6,
            FgbColumnType.DateTime => 7,
            FgbColumnType.Json => 8,
            FgbColumnType.Binary => 9,
            FgbColumnType.String => 10,
            _ => 0
        };
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var trimmed = value.Trim();
        if (trimmed.IsNullOrEmpty())
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(trimmed);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static object? NormalizeAttributeValue(object? value)
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
            sbyte sb => (long)sb,
            byte b => (ulong)b,
            short s => (long)s,
            ushort us => (ulong)us,
            int i => (long)i,
            uint ui => (ulong)ui,
            long l => l,
            ulong ul => ul,
            float f => (double)f,
            double d => d,
            decimal dec => (double)dec,
            DateTimeOffset dto => dto.UtcDateTime,
            DateTime dt => dt,
            Guid guid => guid.ToString(),
            Enum enumValue => enumValue.ToString(),
            _ => value
        };
    }

    private static object? ExtractJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var intValue)
                ? intValue
                : element.TryGetDouble(out var doubleValue)
                    ? doubleValue
                    : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
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
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

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

    private static string SanitizeFileName(string? value)
    {
        var name = value.IsNullOrWhiteSpace() ? "collection" : value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return name;
    }

    private static FgbHeaderT CreateHeaderTemplate(
        LayerDefinition layer,
        FgbGeometryType geometryType,
        byte dimensions,
        IList<ColumnMeta> columns,
        int srid)
    {
        var header = new FgbHeaderT
        {
            Name = layer.Id.IsNullOrWhiteSpace() ? "layer" : layer.Id,
            GeometryType = geometryType,
            HasZ = dimensions >= 3,
            HasM = dimensions >= 4,
            Columns = columns.Select(c => new FgbColumnT { Name = c.Name, Type = c.Type }).ToList(),
            FeaturesCount = 0,
            IndexNodeSize = 0
        };

        if (srid > 0)
        {
            header.Crs = new FgbCrsT { Code = srid };
        }

        return header;
    }

    private static ReadOnlyMemory<byte> BuildHeaderBuffer(FgbHeaderT template, long featureCount, HilbertRTreeResult indexResult)
    {
        var header = new FgbHeaderT
        {
            Name = template.Name,
            GeometryType = template.GeometryType,
            HasZ = template.HasZ,
            HasM = template.HasM,
            HasT = template.HasT,
            HasTm = template.HasTm,
            Columns = template.Columns,
            FeaturesCount = (ulong)featureCount,
            IndexNodeSize = indexResult.NodeSize,
            Crs = template.Crs,
            Title = template.Title,
            Description = template.Description,
            Metadata = template.Metadata,
            Envelope = indexResult.Envelope.Length == 4 ? indexResult.Envelope.ToList() : template.Envelope
        };

        var builder = new FlatBufferBuilder(1024);
        var offset = FlatGeobuf.Header.Pack(builder, header);
        builder.FinishSizePrefixed(offset.Value);
        var buffer = builder.DataBuffer;
        return buffer.ToReadOnlyMemory(buffer.Position, buffer.Length - buffer.Position);
    }

    private struct NodeItem
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
        public ulong Offset;

        public static NodeItem FromEnvelope(Envelope? envelope, ulong offset)
        {
            if (envelope is null)
            {
                return new NodeItem
                {
                    MinX = 0,
                    MinY = 0,
                    MaxX = 0,
                    MaxY = 0,
                    Offset = offset
                };
            }

            return new NodeItem
            {
                MinX = envelope.MinX,
                MinY = envelope.MinY,
                MaxX = envelope.MaxX,
                MaxY = envelope.MaxY,
                Offset = offset
            };
        }

        public static NodeItem CreateInternal(ulong childIndex)
        {
            return new NodeItem
            {
                MinX = double.PositiveInfinity,
                MinY = double.PositiveInfinity,
                MaxX = double.NegativeInfinity,
                MaxY = double.NegativeInfinity,
                Offset = childIndex
            };
        }

        public void Expand(NodeItem other)
        {
            if (other.MinX < MinX) MinX = other.MinX;
            if (other.MinY < MinY) MinY = other.MinY;
            if (other.MaxX > MaxX) MaxX = other.MaxX;
            if (other.MaxY > MaxY) MaxY = other.MaxY;
        }

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    private readonly struct HilbertRTreeResult
    {
        public HilbertRTreeResult(byte[] indexBytes, double[] envelope, ushort nodeSize)
        {
            IndexBytes = indexBytes;
            Envelope = envelope;
            NodeSize = nodeSize;
        }

        public byte[] IndexBytes { get; }
        public double[] Envelope { get; }
        public ushort NodeSize { get; }

        public static HilbertRTreeResult Empty => new(Array.Empty<byte>(), Array.Empty<double>(), 0);
    }

    private static class HilbertRTreeBuilder
    {
        private const uint HilbertMax = (1u << 16) - 1;
        public const ushort DefaultNodeSize = 16;

        public static HilbertRTreeResult Build(IReadOnlyList<NodeItem> nodes, ushort nodeSize)
        {
            if (nodes.Count == 0 || nodeSize == 0)
            {
                return HilbertRTreeResult.Empty;
            }

            var leafNodes = new List<NodeItem>(nodes.Count);
            foreach (var node in nodes)
            {
                leafNodes.Add(node);
            }

            var extent = CalculateExtent(leafNodes);
            HilbertSort(leafNodes, extent);
            var tree = BuildTree(leafNodes, extent, nodeSize);
            var indexBytes = SerializeTree(tree);
            var envelope = new[] { extent.MinX, extent.MinY, extent.MaxX, extent.MaxY };
            return new HilbertRTreeResult(indexBytes, envelope, nodeSize);
        }

        private static NodeItem CalculateExtent(List<NodeItem> nodes)
        {
            var extent = NodeItem.CreateInternal(0);
            foreach (var node in nodes)
            {
                extent.Expand(node);
            }

            if (double.IsPositiveInfinity(extent.MinX) || double.IsPositiveInfinity(extent.MinY))
            {
                extent.MinX = 0;
                extent.MinY = 0;
            }

            if (double.IsNegativeInfinity(extent.MaxX) || double.IsNegativeInfinity(extent.MaxY))
            {
                extent.MaxX = 0;
                extent.MaxY = 0;
            }

            return extent;
        }

        private static void HilbertSort(List<NodeItem> nodes, NodeItem extent)
        {
            var width = extent.Width;
            if (width <= 0) width = 1;
            var height = extent.Height;
            if (height <= 0) height = 1;

            nodes.Sort((a, b) =>
            {
                var ha = HilbertValue(a, extent, width, height);
                var hb = HilbertValue(b, extent, width, height);
                return hb.CompareTo(ha);
            });
        }

        private static uint HilbertValue(NodeItem node, NodeItem extent, double width, double height)
        {
            var centerX = (node.MinX + node.MaxX) * 0.5;
            var centerY = (node.MinY + node.MaxY) * 0.5;

            var x = (uint)Math.Clamp(Math.Floor(HilbertMax * ((centerX - extent.MinX) / width)), 0, HilbertMax);
            var y = (uint)Math.Clamp(Math.Floor(HilbertMax * ((centerY - extent.MinY) / height)), 0, HilbertMax);
            return Hilbert(x, y);
        }

        private static NodeItem[] BuildTree(List<NodeItem> leafNodes, NodeItem extent, ushort nodeSize)
        {
            var branchingFactor = Math.Clamp(nodeSize, (ushort)2, (ushort)65535);
            var levelBounds = GenerateLevelBounds(leafNodes.Count, branchingFactor);
            var numNodes = levelBounds[0].end;
            var nodeItems = new NodeItem[numNodes];
            var leafStart = numNodes - leafNodes.Count;

            for (var i = 0; i < leafNodes.Count; i++)
            {
                nodeItems[leafStart + i] = leafNodes[i];
            }

            for (var level = 0; level < levelBounds.Count - 1; level++)
            {
                var childrenRange = levelBounds[level];
                var parentRange = levelBounds[level + 1];
                var parentIndex = parentRange.start;
                var childIndex = childrenRange.start;

                while (childIndex < childrenRange.end)
                {
                    var parentNode = NodeItem.CreateInternal((ulong)childIndex);
                    for (int j = 0; j < branchingFactor && childIndex < childrenRange.end; j++, childIndex++)
                    {
                        parentNode.Expand(nodeItems[childIndex]);
                    }

                    nodeItems[parentIndex++] = parentNode;
                }
            }

            return nodeItems;
        }

        private static List<(int start, int end)> GenerateLevelBounds(int numItems, ushort nodeSize)
        {
            if (numItems <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numItems));
            }

            var branching = Math.Clamp(nodeSize, (ushort)2, (ushort)65535);
            var levelNumNodes = new List<int> { numItems };
            var n = numItems;
            var totalNodes = n;

            while (true)
            {
                n = (n + branching - 1) / branching;
                totalNodes += n;
                levelNumNodes.Add(n);
                if (n == 1)
                {
                    break;
                }
            }

            var levelBounds = new List<(int start, int end)>(levelNumNodes.Count);
            var current = totalNodes;
            foreach (var size in levelNumNodes)
            {
                current -= size;
                levelBounds.Add((current, current + size));
            }

            return levelBounds;
        }

        private static byte[] SerializeTree(NodeItem[] nodes)
        {
            if (nodes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var buffer = new byte[nodes.Length * (sizeof(double) * 4 + sizeof(ulong))];
            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream);
            foreach (var node in nodes)
            {
                writer.Write(node.MinX);
                writer.Write(node.MinY);
                writer.Write(node.MaxX);
                writer.Write(node.MaxY);
                writer.Write(node.Offset);
            }

            return buffer;
        }

        private static uint Hilbert(uint x, uint y)
        {
            var a = x ^ y;
            var b = 0xFFFF ^ a;
            var c = 0xFFFF ^ (x | y);
            var d = x & (y ^ 0xFFFF);

            var A = a | (b >> 1);
            var B = (a >> 1) ^ a;
            var C = ((c >> 1) ^ b) ^ c;
            var D = ((d >> 1) ^ (a & (b >> 1))) ^ d;

            a = A;
            b = B;
            c = C;
            d = D;

            A = (a & (a >> 2)) ^ (b & (b >> 2));
            B = (((a & (b >> 2)) ^ (b & ((a ^ b) >> 2))) ^ a) ^ b;
            C = c ^ ((a & (c >> 2)) ^ ((a ^ b) & (d >> 2)));
            D = d ^ ((b & (c >> 2)) ^ ((a ^ b) & (d >> 2)));

            a = A;
            b = B;
            c = C;
            d = D;

            A = (a & (a >> 4)) ^ (b & (b >> 4));
            B = (((a & (b >> 4)) ^ (b & ((a ^ b) >> 4))) ^ a) ^ b;
            C = c ^ ((a & (c >> 4)) ^ ((a ^ b) & (d >> 4)));
            D = d ^ ((b & (c >> 4)) ^ ((a ^ b) & (d >> 4)));

            a = A;
            b = B;
            c = C;
            d = D;

            var i0 = x ^ y;
            var i1 = d | (0xFFFF ^ (i0 | c));

            i0 = (i0 | (i0 << 8)) & 0x00FF00FF;
            i0 = (i0 | (i0 << 4)) & 0x0F0F0F0F;
            i0 = (i0 | (i0 << 2)) & 0x33333333;
            i0 = (i0 | (i0 << 1)) & 0x55555555;

            i1 = (i1 | (i1 << 8)) & 0x00FF00FF;
            i1 = (i1 | (i1 << 4)) & 0x0F0F0F0F;
            i1 = (i1 | (i1 << 2)) & 0x33333333;
            i1 = (i1 | (i1 << 1)) & 0x55555555;

            return (i1 << 1) | i0;
        }
    }
}
