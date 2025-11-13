// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlatGeobuf.NTS;
using Google.FlatBuffers;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
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

#nullable enable

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Streaming FlatGeobuf writer implementing the StreamingFeatureCollectionWriterBase pattern.
/// Writes features incrementally in FlatGeobuf binary format without buffering all features in memory.
///
/// FlatGeobuf (https://flatgeobuf.org/) is a performant binary encoding format for geographic data
/// optimized for HTTP range requests and streaming access. It consists of:
/// - Magic bytes (8 bytes)
/// - Header with schema and metadata
/// - Optional spatial index (omitted in streaming mode for true streaming)
/// - Feature data (written incrementally)
///
/// Key differences from FlatGeobufExporter:
/// - Streaming: Writes features as they arrive without buffering
/// - No spatial index: Cannot build Hilbert R-Tree without knowing all features upfront
/// - Lower memory: Constant memory usage regardless of dataset size
/// - Use case: Large result sets, real-time feeds, HTTP streaming responses
///
/// FlatGeobufExporter should be used when:
/// - Full spatial index is required for range queries
/// - Dataset fits in memory
/// - Output is saved to file rather than streamed over HTTP
/// </summary>
public sealed class FlatGeobufStreamingWriter : StreamingFeatureCollectionWriterBase
{
    protected override string ContentType => "application/flatgeobuf";
    protected override string FormatName => "FlatGeobuf";

    // Flush more frequently for binary format to enable HTTP streaming
    protected override int FlushBatchSize => 50;

    // BUG FIX #46: Cache header template to enable true streaming without rebuilding for each feature
    private FgbHeaderT? _cachedHeaderTemplate;

    /// <summary>
    /// Creates a new FlatGeobuf streaming writer.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public FlatGeobufStreamingWriter(ILogger<FlatGeobufStreamingWriter> logger)
        : base(logger)
    {
    }

    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // Write FlatGeobuf magic bytes (fgb + version + reserved)
        await outputStream.WriteAsync(FlatGeobuf.Constants.MagicBytes.AsMemory(), cancellationToken).ConfigureAwait(false);

        // Build header with schema information
        var srid = context.TargetWkid;
        var geometryType = ResolveGeometryType(layer.GeometryType);
        var dimensions = ResolveDimensions(layer);
        var columns = BuildColumnMetadata(layer, context);

        var header = new FgbHeaderT
        {
            Name = layer.Id.IsNullOrWhiteSpace() ? "layer" : layer.Id,
            GeometryType = geometryType,
            HasZ = dimensions >= 3,
            HasM = dimensions >= 4,
            // BUG FIX #47: Columns are ordered explicitly to ensure proper column indices per FlatGeobuf spec
            // Column index is implicit based on list position (0-based)
            Columns = columns.Select(c => new FgbColumnT { Name = c.Name, Type = c.Type }).ToList(),
            FeaturesCount = 0, // Unknown in streaming mode
            IndexNodeSize = 0  // No spatial index in streaming mode
        };

        if (srid > 0)
        {
            header.Crs = new FgbCrsT { Code = srid };
        }

        // BUG FIX #46: Cache header template for feature encoding to avoid rebuilding for each feature
        this.cachedHeaderTemplate = header;

        // Serialize header to FlatBuffers
        var builder = new FlatBufferBuilder(1024);
        var offset = FlatGeobuf.Header.Pack(builder, header);
        builder.FinishSizePrefixed(offset.Value);
        var buffer = builder.DataBuffer;
        var headerBytes = buffer.ToReadOnlyMemory(buffer.Position, buffer.Length - buffer.Position);

        // Write size-prefixed header
        await outputStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

        // Note: No spatial index written in streaming mode
        // This enables true streaming but sacrifices spatial query performance
    }

    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // FlatGeobuf is a binary format with size-prefixed features
        // No separator needed - each feature is self-delimiting
        return Task.CompletedTask;
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord featureRecord,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // BUG FIX #46: Use cached header template for true streaming without rebuilding schema for each feature
        if (_cachedHeaderTemplate == null)
        {
            throw new InvalidOperationException("Header must be written before features. Call WriteHeaderAsync first.");
        }

        // Convert FeatureRecord to NTS Feature
        var srid = context.TargetWkid;
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid);
        var feature = CreateNtsFeature(featureRecord, layer, geometryFactory, context);

        // Serialize feature to FlatBuffers using cached header template
        var featureBuffer = FeatureConversions.ToByteBuffer(feature, _cachedHeaderTemplate);
        var featureBytes = featureBuffer.ToReadOnlyMemory(
            featureBuffer.Position,
            featureBuffer.Length - featureBuffer.Position);

        // Write size-prefixed feature
        await outputStream.WriteAsync(featureBytes, cancellationToken).ConfigureAwait(false);
    }

    protected override Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        // FlatGeobuf has no footer - file ends after last feature
        // The format is designed to support streaming and append operations
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an NTS Feature from a FeatureRecord.
    /// </summary>
    private static NtsFeature CreateNtsFeature(
        FeatureRecord record,
        LayerDefinition layer,
        GeometryFactory geometryFactory,
        StreamingWriterContext context)
    {
        // Extract geometry
        NtsGeometry? geometry = null;
        if (record.Attributes.TryGetValue(layer.GeometryField, out var geomValue))
        {
            geometry = geomValue switch
            {
                NtsGeometry ntsGeom => geometryFactory.CreateGeometry(ntsGeom),
                string geoJsonStr => ParseGeoJson(geoJsonStr, geometryFactory),
                _ => null
            };
        }

        if (geometry is not null && geometry.SRID == 0)
        {
            geometry.SRID = geometryFactory.SRID;
        }

        // Build attributes table
        var attributes = new AttributesTable();

        // SECURITY FIX (Issue #38): Respect PropertyNames filter to prevent data leakage
        // Only include properties that were explicitly requested (or all if no filter specified)
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

            if (record.Attributes.TryGetValue(field.Name, out var value))
            {
                var normalizedValue = NormalizeAttributeValue(value);
                if (normalizedValue != null)
                {
                    attributes.Add(field.Name, normalizedValue);
                }
            }
        }

        return new NtsFeature(geometry, attributes);
    }

    /// <summary>
    /// Parses GeoJSON string to NTS geometry.
    /// </summary>
    private static NtsGeometry? ParseGeoJson(string geoJson, GeometryFactory geometryFactory)
    {
        if (geoJson.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            // THREAD-SAFETY FIX: Create per-request GeoJsonReader instead of static shared instance
            var geoJsonReader = new GeoJsonReader();
            var geometry = geoJsonReader.Read<Geometry>(geoJson);
            return geometry != null ? geometryFactory.CreateGeometry(geometry) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes attribute values for FlatGeobuf encoding.
    /// </summary>
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
            // Normalize numeric types to supported FlatGeobuf types
            sbyte sb => (int)sb,
            byte b => (int)b,
            short s => (int)s,
            ushort us => (int)us,
            uint ui => (long)ui,
            float f => (double)f,
            decimal dec => (double)dec,

            // Normalize temporal types
            DateTimeOffset dto => dto.UtcDateTime,

            // Normalize Guid to string
            Guid guid => guid.ToString(),
            Enum enumValue => enumValue.ToString(),

            // Keep as-is: int, long, ulong, double, string, bool, DateTime, byte[]
            _ => value
        };
    }

    /// <summary>
    /// Extracts value from JsonElement.
    /// </summary>
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
                    : (object)element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Resolves FlatGeobuf geometry type from layer geometry type.
    /// </summary>
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

    /// <summary>
    /// Resolves dimensions (2D, 3D, 4D) from layer definition.
    /// Detects Z and M dimensions from geometry type suffix (e.g., "PointZ", "PointZM").
    /// </summary>
    private static byte ResolveDimensions(LayerDefinition layer)
    {
        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            return 2; // Default to 2D (XY)
        }

        var geomType = layer.GeometryType.Trim();

        // Check for ZM suffix (4D: X, Y, Z, M)
        if (geomType.EndsWith("ZM", StringComparison.OrdinalIgnoreCase) ||
            geomType.EndsWith("Z M", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        // Check for Z suffix (3D: X, Y, Z)
        if (geomType.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        // Check for M suffix (3D: X, Y, M)
        if (geomType.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        // Default to 2D (XY)
        return 2;
    }

    /// <summary>
    /// Builds FlatGeobuf column metadata from layer field definitions.
    /// SECURITY FIX (Issue #38): Respects PropertyNames filter to align schema with filtered feature data.
    /// </summary>
    private static IList<ColumnMeta> BuildColumnMetadata(LayerDefinition layer, StreamingWriterContext context)
    {
        var columns = new List<ColumnMeta>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // SECURITY FIX (Issue #38): Respect PropertyNames filter in schema
        // This ensures the header schema matches the filtered feature data
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

            // Apply property filter if specified (matches CreateNtsFeature logic)
            if (requestedProperties != null && !requestedProperties.Contains(field.Name))
            {
                continue;
            }

            // Skip duplicates
            if (!seen.Add(field.Name))
            {
                continue;
            }

            if (field.Name.IsNullOrWhiteSpace())
            {
                continue;
            }

            columns.Add(new ColumnMeta
            {
                Name = field.Name,
                Type = MapColumnType(field.DataType ?? field.StorageType)
            });
        }

        return columns;
    }

    /// <summary>
    /// Maps layer field data type to FlatGeobuf column type.
    /// </summary>
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
            "int" or "integer" or "int4" or "int32" => FgbColumnType.Int,
            "int unsigned" or "uint" or "uint32" => FgbColumnType.UInt,
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

    /// <summary>
    /// Helper struct for column metadata.
    /// Mirrors the structure expected by FlatGeobuf ColumnT.
    /// </summary>
    private struct ColumnMeta
    {
        public string Name { get; set; }
        public FgbColumnType Type { get; set; }
    }
}
