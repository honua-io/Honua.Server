// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

#nullable enable

namespace Honua.Server.Core.Raster.Serialization;

/// <summary>
/// Streaming GeoArrow writer implementing Apache Arrow IPC format with GeoArrow spatial extensions.
/// Writes features as Apache Arrow record batches with geometry encoded as WKB in a binary column.
///
/// GeoArrow Specification: https://geoarrow.org/
/// Apache Arrow IPC: https://arrow.apache.org/docs/format/Columnar.html#ipc-streaming-format
///
/// This writer uses the Arrow IPC Streaming Format which allows for:
/// - Constant memory usage (small batches)
/// - Incremental processing
/// - Network streaming
/// - Schema evolution
///
/// Schema Design:
/// - First column: "geometry" (Binary type) containing WKB-encoded geometries
/// - Remaining columns: Feature properties (currently all StringType for simplicity)
/// - Geometry metadata includes: encoding=WKB, geometry_type, crs
///
/// Future Enhancements:
/// - Native GeoArrow encoding (point arrays, linestring arrays, etc.)
/// - Proper type mapping for numeric/temporal fields
/// - Compression (LZ4, ZSTD)
/// - Dictionary encoding for string columns
/// - Multiple record batches for very large datasets
/// </summary>
public sealed class GeoArrowStreamingWriter : StreamingFeatureCollectionWriterBase
{
    // THREAD-SAFETY FIX: Create per-request instances instead of shared field instances
    // These will be created in WriteHeaderAsync when needed
    private WKBWriter? _wkbWriter;
    private GeoJsonReader? _geoJsonReader;

    // Batch size for Arrow record batches
    // Currently writes all features in a single batch, but could be enhanced
    // to write multiple batches for better streaming and memory characteristics
    private const int RecordBatchSize = 100_000;

    protected override string ContentType => "application/vnd.apache.arrow.stream";
    protected override string FormatName => "GeoArrow";

    // Override flush batch size for Arrow - batch per record batch write
    protected override int FlushBatchSize => RecordBatchSize;

    private Schema? _schema;
    private ArrowStreamWriter? _arrowWriter;
    private List<BinaryArray.Builder>? _geometryBuilders;
    private List<List<IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>>>? _propertyBuilders;
    private List<string>? _attributeFields;
    private List<IArrowType>? _arrowTypes;
    private int _currentBatchSize;

    /// <summary>
    /// Creates a new GeoArrow streaming writer.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public GeoArrowStreamingWriter(ILogger<GeoArrowStreamingWriter> logger)
        : base(logger)
    {
    }

    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // Initialize per-request instances for thread safety
        _wkbWriter = new WKBWriter();
        _geoJsonReader = new GeoJsonReader();

        // Build Arrow schema with geometry and property columns
        _attributeFields = ResolveFields(layer);
        _arrowTypes = ResolveFieldTypes(layer, _attributeFields);
        var crsString = context.TargetWkid != 4326
            ? $"EPSG:{context.TargetWkid}"
            : CrsHelper.DefaultCrsIdentifier;

        _schema = BuildSchema(layer, _attributeFields, _arrowTypes, crsString);

        // Initialize Arrow stream writer
        // leaveOpen: true because we don't own the output stream
        _arrowWriter = new ArrowStreamWriter(outputStream, _schema, leaveOpen: true);

        // Write Arrow IPC schema header
        await _arrowWriter.WriteStartAsync(cancellationToken).ConfigureAwait(false);

        // Initialize builders for the first batch
        InitializeBatchBuilders();
    }

    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // Arrow format doesn't require separators - features are batched
        return Task.CompletedTask;
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        if (_geometryBuilders == null || _propertyBuilders == null || _attributeFields == null || _arrowTypes == null || _wkbWriter == null)
        {
            throw new InvalidOperationException("Batch builders not initialized. WriteHeaderAsync must be called first.");
        }

        // Get the current batch builders
        var geometryBuilder = _geometryBuilders[_geometryBuilders.Count - 1];
        var currentBuilders = _propertyBuilders[_propertyBuilders.Count - 1];

        // Extract and encode geometry as WKB
        if (context.ReturnGeometry &&
            feature.Attributes.TryGetValue(layer.GeometryField, out var geomObj))
        {
            var geometry = ExtractGeometry(geomObj, layer, context.TargetWkid);
            if (geometry != null && !geometry.IsEmpty)
            {
                var wkb = _wkbWriter.Write(geometry);
                geometryBuilder.Append(wkb.AsSpan());
            }
            else
            {
                geometryBuilder.AppendNull();
            }
        }
        else
        {
            geometryBuilder.AppendNull();
        }

        // Extract property values
        var values = ResolveFieldValuesTyped(layer, _attributeFields, feature);
        for (var i = 0; i < currentBuilders.Count; i++)
        {
            AppendTypedValue(currentBuilders[i], _arrowTypes[i], values[i]);
        }

        _currentBatchSize++;

        // Check if we should flush the current batch
        // This creates multiple record batches for very large datasets
        if (_currentBatchSize >= RecordBatchSize)
        {
            await FlushCurrentBatchAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        // Flush any remaining features in the current batch
        if (_currentBatchSize > 0)
        {
            await FlushCurrentBatchAsync(cancellationToken).ConfigureAwait(false);
        }

        // Write Arrow IPC end-of-stream marker
        if (_arrowWriter != null)
        {
            await _arrowWriter.WriteEndAsync(cancellationToken).ConfigureAwait(false);
            _arrowWriter.Dispose();
            _arrowWriter = null;
        }

        // Clean up builders
        _geometryBuilders?.Clear();
        _propertyBuilders?.Clear();
        _schema = null;
        _attributeFields = null;
    }

    /// <summary>
    /// Flushes the current batch by building Arrow arrays and writing a RecordBatch.
    /// </summary>
    private async Task FlushCurrentBatchAsync(CancellationToken cancellationToken)
    {
        if (_arrowWriter == null || _schema == null || _geometryBuilders == null || _propertyBuilders == null)
        {
            return;
        }

        if (_currentBatchSize == 0)
        {
            return;
        }

        var currentGeometryBuilder = _geometryBuilders[_geometryBuilders.Count - 1];
        var currentBuilders = _propertyBuilders[_propertyBuilders.Count - 1];

        // Build arrays from current batch builders
        var arrays = new IArrowArray[currentBuilders.Count + 1];
        arrays[0] = currentGeometryBuilder.Build(default);

        for (var i = 0; i < currentBuilders.Count; i++)
        {
            arrays[i + 1] = currentBuilders[i].Build(default);
        }

        RecordBatch? recordBatch = null;
        try
        {
            // Create record batch with current batch size
            recordBatch = new RecordBatch(_schema, arrays, _currentBatchSize);

            // Write the record batch to the Arrow stream
            await _arrowWriter.WriteRecordBatchAsync(recordBatch, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Clean up arrays and record batch
            recordBatch?.Dispose();

            foreach (var array in arrays)
            {
                array?.Dispose();
            }
        }

        // Reset for next batch
        _currentBatchSize = 0;
        InitializeBatchBuilders();
    }

    /// <summary>
    /// Initializes a new set of array builders for the next batch.
    /// </summary>
    private void InitializeBatchBuilders()
    {
        _geometryBuilders ??= new List<BinaryArray.Builder>();
        _propertyBuilders ??= new List<List<IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>>>();

        // Add new builders for the next batch
        _geometryBuilders.Add(new BinaryArray.Builder());

        var attributeCount = _attributeFields?.Count ?? 0;
        var builders = new List<IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>>();
        for (var i = 0; i < attributeCount; i++)
        {
            builders.Add(CreateBuilderForType(_arrowTypes![i]));
        }
        _propertyBuilders.Add(builders);
    }

    /// <summary>
    /// Builds the Arrow schema with geometry column and property columns.
    /// </summary>
    private static Schema BuildSchema(
        LayerDefinition layer,
        IReadOnlyList<string> attributeFields,
        IReadOnlyList<IArrowType> arrowTypes,
        string contentCrs)
    {
        var geometryMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ARROW:extension:name"] = "geoarrow.wkb",
            ["ARROW:extension:metadata"] = JsonSerializer.Serialize(new
            {
                encoding = "WKB",
                geometry_types = !string.IsNullOrWhiteSpace(layer.GeometryType)
                    ? new[] { layer.GeometryType }
                    : System.Array.Empty<string>(),
                crs = contentCrs
            })
        };

        // Bug 23 fix: Add required GeoArrow schema metadata for Arrow Flight clients
        var geometryFieldType = new BinaryType();

        var fields = new List<Field>
        {
            new Field(
                "geometry",
                geometryFieldType,
                nullable: true,
                geometryMetadata)
        };

        // Add property fields with proper type mapping
        for (var i = 0; i < attributeFields.Count; i++)
        {
            fields.Add(new Field(attributeFields[i], arrowTypes[i], nullable: true));
        }

        return new Schema(fields, metadata: null);
    }

    /// <summary>
    /// Resolves the list of attribute fields to include in the export.
    /// Excludes geometry field and deduplicates.
    /// </summary>
    private static List<string> ResolveFields(LayerDefinition layer)
    {
        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Include ID field first if present
        if (!string.IsNullOrWhiteSpace(layer.IdField))
        {
            fields.Add(layer.IdField);
            seen.Add(layer.IdField);
        }

        // Include all other fields except geometry
        foreach (var field in layer.Fields)
        {
            if (field.Name.Equals(layer.GeometryField, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Extracts geometry from feature record and transforms to target CRS.
    /// </summary>
    private Geometry? ExtractGeometry(object? geomObj, LayerDefinition layer, int targetWkid)
    {
        if (geomObj == null || _geoJsonReader == null)
        {
            return null;
        }

        // Handle NTS Geometry directly
        if (geomObj is Geometry ntsGeom)
        {
            return TransformGeometry(ntsGeom, layer, targetWkid);
        }

        // Handle GeoJSON geometry (JsonElement or string)
        string? geoJsonString = null;
        if (geomObj is JsonElement jsonElement)
        {
            geoJsonString = jsonElement.GetRawText();
        }
        else if (geomObj is string str)
        {
            geoJsonString = str;
        }

        if (string.IsNullOrWhiteSpace(geoJsonString))
        {
            return null;
        }

        try
        {
            var geometry = _geoJsonReader.Read<Geometry>(geoJsonString);
            return TransformGeometry(geometry, layer, targetWkid);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Transforms geometry to target CRS if needed.
    /// Bug 25 fix: Check per-feature SRID if available before falling back to layer SRID.
    /// </summary>
    private static Geometry? TransformGeometry(Geometry? geometry, LayerDefinition layer, int targetWkid)
    {
        if (geometry == null || geometry.IsEmpty)
        {
            return geometry;
        }

        // Bug 25 fix: Prioritize per-feature SRID if set, then fall back to layer storage SRID
        var sourceSrid = geometry.SRID;

        // Only use layer.Storage.Srid if geometry doesn't have its own SRID set
        if (sourceSrid == 0 && layer.Storage?.Srid != null)
        {
            sourceSrid = layer.Storage.Srid.Value;
        }

        // If source SRID is still unknown, assume it's already in target CRS
        if (sourceSrid == 0)
        {
            geometry.SRID = targetWkid;
            return geometry;
        }

        // If source and target are the same, no transformation needed
        if (sourceSrid == targetWkid)
        {
            geometry.SRID = targetWkid;
            return geometry;
        }

        // Perform actual coordinate transformation using CrsTransform utility
        return CrsTransform.TransformGeometry(geometry, sourceSrid, targetWkid);
    }

    /// <summary>
    /// Resolves field values from feature record.
    /// </summary>
    private static string?[] ResolveFieldValues(
        LayerDefinition layer,
        IReadOnlyList<string> attributeFields,
        FeatureRecord record)
    {
        var values = new string?[attributeFields.Count];

        for (var i = 0; i < attributeFields.Count; i++)
        {
            var fieldName = attributeFields[i];

            // Try exact match first
            if (record.Attributes.TryGetValue(fieldName, out var value))
            {
                values[i] = NormalizeValue(value);
                continue;
            }

            // Case-insensitive fallback
            foreach (var (key, val) in record.Attributes)
            {
                if (key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    values[i] = NormalizeValue(val);
                    break;
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Resolves Arrow types for attribute fields based on layer field definitions.
    /// Bug 24 fix: Better type detection logic instead of defaulting to StringType.
    /// </summary>
    private static List<IArrowType> ResolveFieldTypes(LayerDefinition layer, IReadOnlyList<string> attributeFields)
    {
        var types = new List<IArrowType>();
        var fieldMap = layer.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var fieldName in attributeFields)
        {
            if (fieldMap.TryGetValue(fieldName, out var fieldDef))
            {
                IArrowType arrowType;
                if (!string.IsNullOrWhiteSpace(fieldDef.DataType))
                {
                    arrowType = MapDataTypeToArrowType(fieldDef.DataType, fieldDef);
                }
                else
                {
                    arrowType = InferFieldType(fieldName, fieldDef);
                }

                types.Add(arrowType);
            }
            else
            {
                // Bug 24 fix: Try to infer type from field name patterns when metadata is missing
                var inferredType = InferFieldType(fieldName, null);
                types.Add(inferredType);
            }
        }

        return types;
    }

    /// <summary>
    /// Infers field type from field name patterns and metadata when explicit type is unavailable.
    /// </summary>
    private static IArrowType InferFieldType(string fieldName, FieldDefinition? fieldDef)
    {
        if (fieldDef is not null)
        {
            if (!string.IsNullOrWhiteSpace(fieldDef.StorageType))
            {
                var storage = fieldDef.StorageType.Trim().ToLowerInvariant();
                switch (storage)
                {
                    case "bigint" or "int8" or "int64":
                        return Int64Type.Default;
                    case "int" or "integer" or "int4" or "int32" or "smallint" or "int2":
                        return Int32Type.Default;
                    case "float" or "single":
                        return FloatType.Default;
                    case "double" or "real":
                        return DoubleType.Default;
                    case "decimal" or "numeric":
                        return CreateDecimalArrowType(fieldDef);
                    case "bool" or "boolean" or "bit":
                        return BooleanType.Default;
                    case "timestamp" or "datetime" or "datetimeoffset" or "date":
                        return TimestampType.Default;
                    case "blob" or "bytea" or "binary":
                        return BinaryType.Default;
                    case "uuid" or "guid":
                        return StringType.Default;
                }
            }

            if (fieldDef.Precision.HasValue && fieldDef.Scale.HasValue)
            {
                return CreateDecimalArrowType(fieldDef);
            }
        }

        // Check common ID field patterns
        var lowerName = fieldName.ToLowerInvariant();
        if (lowerName.EndsWith("_id") || lowerName.EndsWith("id") || lowerName == "fid" || lowerName == "objectid")
        {
            return Int64Type.Default;
        }

        // Check common numeric patterns
        if (lowerName.Contains("count") || lowerName.Contains("num") || lowerName.Contains("quantity"))
        {
            return Int64Type.Default;
        }

        // Check common decimal patterns
        if (lowerName.Contains("amount") || lowerName.Contains("price") || lowerName.Contains("cost") ||
            lowerName.Contains("rate") || lowerName.Contains("percent"))
        {
            return DoubleType.Default;
        }

        // Check date/time patterns
        if (lowerName.Contains("date") || lowerName.Contains("time") || lowerName.Contains("timestamp") ||
            lowerName.EndsWith("_at") || lowerName.StartsWith("created") || lowerName.StartsWith("updated"))
        {
            return TimestampType.Default;
        }

        // Check boolean patterns
        if (lowerName.StartsWith("is_") || lowerName.StartsWith("has_") || lowerName.EndsWith("_flag") ||
            lowerName == "active" || lowerName == "enabled" || lowerName == "deleted")
        {
            return BooleanType.Default;
        }

        // Default to string if type cannot be inferred
        return StringType.Default;
    }

    private static IArrowType MapEsriFieldType(string dataType, FieldDefinition? fieldDefinition)
    {
        return dataType switch
        {
            "esrifieldtypestring" => StringType.Default,
            "esrifieldtypeinteger" => Int32Type.Default,
            "esrifieldtypesmallinteger" => Int32Type.Default,
            "esrifieldtypebigint" => Int64Type.Default,
            "esrifieldtypeoid" => Int64Type.Default,
            "esrifieldtypesingle" => FloatType.Default,
            "esrifieldtypedouble" => DoubleType.Default,
            "esrifieldtypenumeric" => CreateDecimalArrowType(fieldDefinition),
            "esrifieldtypefloat" => FloatType.Default,
            "esrifieldtypedate" or "esrifieldtypedate2" => TimestampType.Default,
            "esrifieldtypeguid" or "esrifieldtypeglobalid" => StringType.Default,
            "esrifieldtypeblob" or "esrifieldtyperaster" or "esrifieldtypegeometry" => BinaryType.Default,
            "esrifieldtypexml" => StringType.Default,
            _ => StringType.Default
        };
    }

    private static IArrowType CreateDecimalArrowType(FieldDefinition? fieldDefinition)
    {
        var precision = fieldDefinition?.Precision ?? 18;
        var scale = fieldDefinition?.Scale ?? 6;

        precision = Math.Clamp(precision, 1, 38);
        scale = Math.Clamp(scale, 0, precision);

        return new Decimal128Type(precision, scale);
    }

    /// <summary>
    /// Maps a Honua field data type to an Apache Arrow type.
    /// </summary>
    private static IArrowType MapDataTypeToArrowType(string dataType, FieldDefinition? fieldDefinition)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return StringType.Default;
        }

        var normalized = dataType.Trim();
        var lower = normalized.ToLowerInvariant();

        switch (lower)
        {
            case "int" or "integer" or "int32" or "smallint" or "short":
                return Int32Type.Default;
            case "long" or "int64" or "bigint":
                return Int64Type.Default;
            case "float" or "single" or "real4":
                return FloatType.Default;
            case "double" or "real" or "float8":
                return DoubleType.Default;
            case "numeric" or "decimal":
                return CreateDecimalArrowType(fieldDefinition);
            case "bool" or "boolean" or "bit":
                return BooleanType.Default;
            case "date" or "datetime" or "timestamp" or "timestamptz" or "datetimeoffset":
                return TimestampType.Default;
            case "string" or "text" or "varchar" or "nvarchar" or "char":
                return StringType.Default;
        }

        if (lower.StartsWith("esrifieldtype", StringComparison.Ordinal))
        {
            return MapEsriFieldType(lower, fieldDefinition);
        }

        if (lower.Contains("decimal", StringComparison.Ordinal) || lower.Contains("numeric", StringComparison.Ordinal))
        {
            return CreateDecimalArrowType(fieldDefinition);
        }

        if (lower.Contains("json", StringComparison.Ordinal) || lower.Contains("xml", StringComparison.Ordinal))
        {
            return StringType.Default;
        }

        if (lower.Contains("blob", StringComparison.Ordinal) || lower.Contains("binary", StringComparison.Ordinal))
        {
            return BinaryType.Default;
        }

        return StringType.Default;
    }

    /// <summary>
    /// Creates an Arrow array builder for the specified type.
    /// </summary>
    private static IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>> CreateBuilderForType(IArrowType arrowType)
    {
        return arrowType switch
        {
            Int32Type => new Int32Array.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            Int64Type => new Int64Array.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            FloatType => new FloatArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            DoubleType => new DoubleArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            BooleanType => new BooleanArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            TimestampType => new TimestampArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            Decimal128Type decimalType => new Decimal128Array.Builder(decimalType) as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            BinaryType => new BinaryArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>,
            _ => new StringArray.Builder() as IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>>
        };
    }

    /// <summary>
    /// Resolves typed field values from feature record.
    /// </summary>
    private static object?[] ResolveFieldValuesTyped(
        LayerDefinition layer,
        IReadOnlyList<string> attributeFields,
        FeatureRecord record)
    {
        var values = new object?[attributeFields.Count];

        for (var i = 0; i < attributeFields.Count; i++)
        {
            var fieldName = attributeFields[i];

            // Try exact match first
            if (record.Attributes.TryGetValue(fieldName, out var value))
            {
                values[i] = value;
                continue;
            }

            // Case-insensitive fallback
            foreach (var (key, val) in record.Attributes)
            {
                if (key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    values[i] = val;
                    break;
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Appends a typed value to an Arrow array builder.
    /// </summary>
    private static void AppendTypedValue(
        IArrowArrayBuilder<IArrowArray, IArrowArrayBuilder<IArrowArray>> builder,
        IArrowType arrowType,
        object? value)
    {
        if (value is null or DBNull)
        {
            builder.AppendNull();
            return;
        }

        // Extract from JsonElement if needed
        if (value is JsonElement element)
        {
            value = ExtractJsonElementTyped(element);
            if (value is null)
            {
                builder.AppendNull();
                return;
            }
        }

        switch (builder)
        {
            case Int32Array.Builder int32Builder:
                int32Builder.Append(ConvertToInt32(value));
                break;
            case Int64Array.Builder int64Builder:
                int64Builder.Append(ConvertToInt64(value));
                break;
            case FloatArray.Builder floatBuilder:
                floatBuilder.Append(ConvertToFloat(value));
                break;
            case DoubleArray.Builder doubleBuilder:
                doubleBuilder.Append(ConvertToDouble(value));
                break;
            case Decimal128Array.Builder decimalBuilder when arrowType is Decimal128Type decimalType:
                decimalBuilder.Append(ConvertToSqlDecimal(value, decimalType));
                break;
            case BooleanArray.Builder boolBuilder:
                boolBuilder.Append(ConvertToBoolean(value));
                break;
            case TimestampArray.Builder timestampBuilder:
                timestampBuilder.Append(ConvertToTimestamp(value));
                break;
            case BinaryArray.Builder binaryBuilder:
                if (TryConvertToBinary(value, out var binaryValue))
                {
                    binaryBuilder.Append(binaryValue.AsSpan());
                }
                else
                {
                    binaryBuilder.AppendNull();
                }
                break;
            case StringArray.Builder stringBuilder:
                stringBuilder.Append(ConvertToString(value));
                break;
            default:
                // Fallback to string representation
                ((StringArray.Builder)builder).Append(ConvertToString(value));
                break;
        }
    }

    private static SqlDecimal ConvertToSqlDecimal(object value, Decimal128Type decimalType)
    {
        var precision = decimalType.Precision <= 0 ? 18 : decimalType.Precision;
        precision = Math.Clamp(precision, 1, 38);

        var scale = decimalType.Scale < 0 ? 0 : decimalType.Scale;
        scale = Math.Clamp(scale, 0, precision);

        static SqlDecimal ToPrecScale(SqlDecimal input, int desiredPrecision, int desiredScale)
        {
            try
            {
                return SqlDecimal.ConvertToPrecScale(input, desiredPrecision, desiredScale);
            }
            catch
            {
                try
                {
                return SqlDecimal.ConvertToPrecScale(new SqlDecimal(0), desiredPrecision, desiredScale);
            }
            catch
            {
                return new SqlDecimal(0);
            }
        }
        }

        string text = value switch
        {
            SqlDecimal sql => sql.ToString(),
            decimal dec => dec.ToString(null, CultureInfo.InvariantCulture),
            double dbl => dbl.ToString("G17", CultureInfo.InvariantCulture),
            float flt => flt.ToString("G9", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            string str => str,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetRawText(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return ToPrecScale(new SqlDecimal(0), precision, scale);
        }

        text = text.Trim();

        try
        {
            var parsed = SqlDecimal.Parse(text);
            return ToPrecScale(parsed, precision, scale);
        }
        catch
        {
            try
            {
                var parsedDecimal = decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return ToPrecScale(new SqlDecimal(parsedDecimal), precision, scale);
            }
            catch
            {
                return ToPrecScale(new SqlDecimal(0), precision, scale);
            }
        }
    }

    private static bool TryConvertToBinary(object value, out byte[]? buffer)
    {
        switch (value)
        {
            case byte[] byteArray:
                buffer = byteArray;
                return true;
            case ReadOnlyMemory<byte> memory:
                buffer = memory.ToArray();
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                return TryDecodeBase64(element.GetString(), out buffer);
            case string str:
                return TryDecodeBase64(str, out buffer);
            default:
                buffer = null;
                return false;
        }
    }

    private static bool TryDecodeBase64(string? value, out byte[]? buffer)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            buffer = System.Array.Empty<byte>();
            return true;
        }

        try
        {
            buffer = Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            buffer = null;
            return false;
        }
    }

    private static int ConvertToInt32(object value)
    {
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
            _ => 0
        };
    }

    private static long ConvertToInt64(object value)
    {
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
            _ => 0L
        };
    }

    private static float ConvertToFloat(object value)
    {
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            decimal dec => (float)dec,
            string str when float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
            _ => 0f
        };
    }

    private static double ConvertToDouble(object value)
    {
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
            _ => 0.0
        };
    }

    private static bool ConvertToBoolean(object value)
    {
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string str when bool.TryParse(str, out var result) => result,
            string str => str.Equals("1", StringComparison.Ordinal) || str.Equals("yes", StringComparison.OrdinalIgnoreCase) || str.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static DateTimeOffset ConvertToTimestamp(object value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt.ToUniversalTime()),
            string str when DateTimeOffset.TryParse(str, out var result) => result,
            long ticks => DateTimeOffset.FromUnixTimeMilliseconds(ticks),
            _ => DateTimeOffset.UtcNow
        };
    }

    private static string ConvertToString(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        return value switch
        {
            Guid guid => guid.ToString(),
            Enum enumValue => enumValue.ToString(),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
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

    /// <summary>
    /// Normalizes a property value to string representation.
    /// Bug 26 fix: Handle complex types properly - avoid mixing primitives and JSON blobs.
    /// </summary>
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

    /// <summary>
    /// Extracts value from JsonElement.
    /// Bug 26 fix: Reject complex types (arrays/objects) instead of stringifying them.
    /// </summary>
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
            JsonValueKind.Array => null, // Bug 26 fix: Reject arrays instead of stringifying
            JsonValueKind.Object => null, // Bug 26 fix: Reject objects instead of stringifying
            _ => element.GetRawText()
        };
    }
}
