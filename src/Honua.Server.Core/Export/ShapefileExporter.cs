// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Export;

public interface IShapefileExporter
{
    Task<ShapefileExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record ShapefileExportResult(Stream Content, string FileName, long FeatureCount);

public sealed record ShapefileExportOptions
{
    public static ShapefileExportOptions Default { get; } = new();

    public int MaxFeatures { get; init; } = int.MaxValue;
    public int MaxStringLength { get; init; } = 254;
    public int MaxNumericPrecision { get; init; } = 18;
    public int MaxNumericScale { get; init; } = 6;
}

public sealed class ShapefileExporter : IShapefileExporter
{
    private const string DefaultGeometryField = "geom";
    private static readonly Encoding DbaseEncoding = Encoding.UTF8;

    private readonly ShapefileExportOptions _options;

    public ShapefileExporter()
        : this(null)
    {
    }

    public ShapefileExporter(ShapefileExportOptions? options)
    {
        _options = options ?? ShapefileExportOptions.Default;
    }

    public async Task<ShapefileExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var srid = CrsHelper.ParseCrs(contentCrs);
        var baseName = SanitizeFileName(layer.Id);
        var workingDirectory = CreateWorkingDirectory();

        // Validate working directory is within temp directory to prevent path traversal
        var tempPath = Path.GetFullPath(Path.GetTempPath());
        var validatedWorkingDir = SecurePathValidator.ValidatePath(workingDirectory, tempPath);

        var shapefilePath = Path.Combine(validatedWorkingDir, baseName + ".shp");

        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid);
        var fieldMappings = BuildFieldMappings(layer, query);
        var header = BuildHeader(fieldMappings);

        // BUG FIX #18: Buffer features in memory to avoid sync-over-async in GDAL callbacks
        // GDAL's C API requires synchronous IEnumerable, so we materialize the async stream first
        var bufferedFeatures = await BufferFeaturesAsync(layer, records, geometryFactory, fieldMappings, cancellationToken).ConfigureAwait(false);

        var writer = new ShapefileDataWriter(shapefilePath, geometryFactory)
        {
            Header = header
        };
        writer.Write(bufferedFeatures);

        var prjPath = Path.Combine(validatedWorkingDir, baseName + ".prj");
        await File.WriteAllTextAsync(prjPath, ResolveProjectionWkt(srid), DbaseEncoding, cancellationToken).ConfigureAwait(false);

        // Validate zip output path is within temp directory
        var zipPath = Path.Combine(tempPath, $"honua-shp-{Guid.NewGuid():N}.zip");
        zipPath = SecurePathValidator.ValidatePath(zipPath, tempPath);
        await using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var filePath in Directory.EnumerateFiles(validatedWorkingDir))
            {
                // Validate each file path is within working directory before adding to archive
                var validatedFilePath = SecurePathValidator.ValidatePath(filePath, validatedWorkingDir);
                var entry = archive.CreateEntry(Path.GetFileName(validatedFilePath), CompressionLevel.Fastest);
                await using var source = new FileStream(validatedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                await using var destination = entry.Open();
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
        }

        CleanupWorkingDirectory(validatedWorkingDir);

        var resultStream = new FileStream(zipPath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
        });

        return new ShapefileExportResult(resultStream, baseName + ".zip", bufferedFeatures.Count);
    }

    private IReadOnlyList<FieldMapping> BuildFieldMappings(LayerDefinition layer, FeatureQuery? query)
    {
        var propertyFilter = query?.PropertyNames is { Count: > 0 }
            ? new HashSet<string>(query.PropertyNames!, StringComparer.OrdinalIgnoreCase)
            : null;

        var mappings = new List<FieldMapping>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (propertyFilter is not null && !propertyFilter.Contains(field.Name))
            {
                continue;
            }

            var descriptor = CreateFieldDescriptor(field);
            var columnName = SanitizeColumnName(field.Alias ?? field.Name, usedNames);
            mappings.Add(new FieldMapping(field.Name, columnName, descriptor));
        }

        if (!mappings.Any(m => string.Equals(m.SourceName, layer.IdField, StringComparison.OrdinalIgnoreCase)))
        {
            var idDescriptor = new FieldDescriptor(ShapefileFieldKind.Character, 'C', Math.Min(32, _options.MaxStringLength), 0);
            var idColumn = SanitizeColumnName(layer.IdField, usedNames);
            mappings.Insert(0, new FieldMapping(layer.IdField, idColumn, idDescriptor));
        }

        return mappings;
    }

    private static DbaseFileHeader BuildHeader(IReadOnlyList<FieldMapping> mappings)
    {
        var header = new DbaseFileHeader();

        foreach (var mapping in mappings)
        {
            header.AddColumn(mapping.ColumnName, mapping.Descriptor.DbaseType, mapping.Descriptor.Length, mapping.Descriptor.DecimalCount);
        }

        return header;
    }

    private AttributesTable BuildAttributes(FeatureRecord record, IReadOnlyList<FieldMapping> mappings)
    {
        var table = new AttributesTable();

        foreach (var mapping in mappings)
        {
            record.Attributes.TryGetValue(mapping.SourceName, out var value);
            table.Add(mapping.ColumnName, NormalizeValue(value, mapping));
        }

        return table;
    }

    private object? NormalizeValue(object? value, FieldMapping mapping)
    {
        if (value is null)
        {
            return null;
        }

        return mapping.Descriptor.Kind switch
        {
            ShapefileFieldKind.Logical => ConvertToLogical(value),
            ShapefileFieldKind.Numeric or ShapefileFieldKind.Float => ConvertToNumeric(value, mapping),
            ShapefileFieldKind.Date => ConvertToDate(value),
            _ => TruncateString(ConvertToString(value), mapping.Descriptor.Length)
        };
    }

    private static bool? ConvertToLogical(object value)
    {
        return value switch
        {
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private object? ConvertToNumeric(object value, FieldMapping mapping)
    {
        decimal? number = value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.TryGetDecimal(out var dec) ? dec : (decimal?)null,
            JsonElement element when element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) => dec,
            string text when decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) => dec,
            IConvertible convertible => Convert.ToDecimal(convertible, CultureInfo.InvariantCulture),
            _ => null
        };

        if (number is null)
        {
            return null;
        }

        var descriptor = mapping.Descriptor;
        var scale = Math.Clamp(descriptor.DecimalCount, 0, _options.MaxNumericScale);
        var rounded = Math.Round(number.Value, scale, MidpointRounding.AwayFromZero);

        return descriptor.Kind switch
        {
            ShapefileFieldKind.Numeric when scale == 0 => ConvertIntegral(rounded),
            ShapefileFieldKind.Float => Convert.ToDouble(rounded, CultureInfo.InvariantCulture),
            ShapefileFieldKind.Numeric => rounded,
            _ => rounded
        };
    }

    private static object ConvertIntegral(decimal value)
    {
        var truncated = decimal.Truncate(value);
        if (truncated >= long.MinValue && truncated <= long.MaxValue)
        {
            return (long)truncated;
        }

        return truncated;
    }

    private static DateTime? ConvertToDate(object value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).Date : dateTime.ToUniversalTime().Date,
            DateTimeOffset offset => offset.UtcDateTime.Date,
            JsonElement element when element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.ToUniversalTime().Date,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed.ToUniversalTime().Date,
            _ => null
        };
    }

    private static string ConvertToString(object value)
    {
        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => element.GetRawText(),
            JsonNode node => node.ToJsonString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private string TruncateString(string text, int maxLength)
    {
        if (text.IsNullOrEmpty())
        {
            return text;
        }

        var limit = Math.Min(maxLength, _options.MaxStringLength);
        return text.Length <= limit ? text : text[..limit];
    }

    private FieldDescriptor CreateFieldDescriptor(FieldDefinition field)
    {
        var type = (field.DataType ?? field.StorageType ?? string.Empty).Trim().ToLowerInvariant();
        var maxLength = field.MaxLength.HasValue && field.MaxLength.Value > 0
            ? Math.Min(field.MaxLength.Value, _options.MaxStringLength)
            : _options.MaxStringLength;

        return type switch
        {
            "int" or "int32" or "int64" or "integer" => new FieldDescriptor(ShapefileFieldKind.Numeric, 'N', Math.Clamp(field.Precision ?? 10, 1, _options.MaxNumericPrecision), 0),
            "double" or "float" => new FieldDescriptor(ShapefileFieldKind.Float, 'F', Math.Clamp(field.Precision ?? _options.MaxNumericPrecision, 1, _options.MaxNumericPrecision), Math.Clamp(field.Scale ?? _options.MaxNumericScale, 0, _options.MaxNumericScale)),
            "decimal" or "numeric" => new FieldDescriptor(ShapefileFieldKind.Numeric, 'N', Math.Clamp(field.Precision ?? _options.MaxNumericPrecision, 1, _options.MaxNumericPrecision), Math.Clamp(field.Scale ?? _options.MaxNumericScale, 0, _options.MaxNumericScale)),
            "bool" or "boolean" => new FieldDescriptor(ShapefileFieldKind.Logical, 'L', 1, 0),
            "datetime" or "date" => new FieldDescriptor(ShapefileFieldKind.Date, 'D', 8, 0),
            _ => new FieldDescriptor(ShapefileFieldKind.Character, 'C', Math.Clamp(maxLength, 1, _options.MaxStringLength), 0)
        };
    }

    private static Geometry? TryReadGeometry(
        FeatureRecord record,
        string? geometryField,
        GeoJsonReader reader,
        GeometryFactory factory)
    {
        var fieldName = geometryField.IsNullOrWhiteSpace() ? DefaultGeometryField : geometryField;
        if (!record.Attributes.TryGetValue(fieldName, out var raw) || raw is null)
        {
            return null;
        }

        try
        {
            var geometry = raw switch
            {
                Geometry g => g,
                JsonNode node => reader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String => reader.Read<Geometry>(element.GetString() ?? string.Empty),
                JsonElement element => reader.Read<Geometry>(element.GetRawText()),
                string text => reader.Read<Geometry>(text),
                _ => reader.Read<Geometry>(raw.ToString() ?? string.Empty)
            };

            if (geometry is null || geometry.IsEmpty)
            {
                return null;
            }

            return geometry.Factory == factory ? geometry : factory.CreateGeometry(geometry);
        }
        catch
        {
            return null;
        }
    }

    // BUG FIX #18: Materialize async features into memory to avoid sync-over-async
    // GDAL's ShapefileDataWriter requires synchronous IEnumerable, so we buffer all features first
    private async Task<BufferedFeatureCollection> BufferFeaturesAsync(
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        GeometryFactory geometryFactory,
        IReadOnlyList<FieldMapping> fieldMappings,
        CancellationToken cancellationToken)
    {
        var features = new List<IFeature>();
        var reader = new GeoJsonReader();

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var geometry = TryReadGeometry(record, layer.GeometryField, reader, geometryFactory);
            if (geometry is not null && geometry.SRID == 0)
            {
                geometry.SRID = geometryFactory.SRID;
            }

            var attributes = BuildAttributes(record, fieldMappings);
            var feature = new Feature(geometry, attributes);
            features.Add(feature);

            if (_options.MaxFeatures > 0 && features.Count > _options.MaxFeatures)
            {
                throw new InvalidOperationException($"Shapefile export limit of {_options.MaxFeatures.ToString("N0", CultureInfo.InvariantCulture)} features exceeded.");
            }
        }

        return new BufferedFeatureCollection(features);
    }

    private sealed class BufferedFeatureCollection : IEnumerable<IFeature>
    {
        private readonly List<IFeature> _features;

        public BufferedFeatureCollection(List<IFeature> features)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        public int Count => _features.Count;

        public IEnumerator<IFeature> GetEnumerator() => _features.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static string SanitizeColumnName(string name, ISet<string> usedNames)
    {
        var builder = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        var sanitized = builder.Length == 0 ? "FIELD" : builder.ToString();
        if (sanitized.Length > 10)
        {
            sanitized = sanitized[..10];
        }

        var baseName = sanitized;
        var candidate = baseName;
        var index = 1;
        while (!usedNames.Add(candidate))
        {
            var suffix = index.ToString(CultureInfo.InvariantCulture);
            var prefixLength = Math.Max(0, 10 - suffix.Length);
            var prefix = baseName.Length > prefixLength ? baseName[..prefixLength] : baseName.PadRight(prefixLength, 'X');
            candidate = prefix + suffix;
            index++;
        }

        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "export";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (sanitized.Length > 64)
        {
            sanitized = sanitized[..64];
        }

        return sanitized.IsNullOrWhiteSpace() ? "export" : sanitized;
    }

    private static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"honua-shp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupWorkingDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static string ResolveProjectionWkt(int srid)
    {
        if (srid == 4326)
        {
            return "GEOGCS[\"WGS 84\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"degree\",0.0174532925199433],AUTHORITY[\"EPSG\",\"4326\"]]";
        }

        return $"AUTHORITY[\"EPSG\",\"{srid}\"]";
    }

    private sealed record FieldDescriptor(ShapefileFieldKind Kind, char DbaseType, int Length, int DecimalCount);

    private sealed record FieldMapping(string SourceName, string ColumnName, FieldDescriptor Descriptor);

    private enum ShapefileFieldKind
    {
        Character,
        Numeric,
        Float,
        Logical,
        Date
    }
}
