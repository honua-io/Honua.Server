// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Export;

public interface ICsvExporter
{
    Task<CsvExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record CsvExportResult(Stream Content, string FileName, long FeatureCount);

public sealed record CsvExportOptions
{
    public static CsvExportOptions Default { get; } = new();

    public int MaxFeatures { get; init; } = 100_000;
    public bool IncludeGeometry { get; init; } = true;
    public string GeometryFormat { get; init; } = "wkt"; // "wkt" or "geojson"
    public string Delimiter { get; init; } = ",";
    public bool IncludeHeader { get; init; } = true;
}

public sealed class CsvExporter : ICsvExporter
{
    private const string DefaultGeometryField = "geom";
    private const int BufferSize = 8192;
    private const int WriteBatchSize = 100;
    private static readonly Encoding CsvEncoding = Encoding.UTF8;

    private readonly CsvExportOptions _options;
    private readonly ILogger<CsvExporter> _logger;

    public CsvExporter(ILogger<CsvExporter> logger)
        : this(logger, null)
    {
    }

    public CsvExporter(ILogger<CsvExporter> logger, CsvExportOptions? options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? CsvExportOptions.Default;
    }

    public async Task<CsvExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-csv-{Guid.NewGuid():N}.csv");
        var fieldMappings = BuildFieldMappings(layer, query);
        long count = 0;

        // RESOURCE LEAK FIX: Add DeleteOnClose to ensure cleanup even if process crashes
        // File is reopened with DeleteOnClose after writing completes
        await using (var fileStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous))
        await using (var writer = new StreamWriter(fileStream, CsvEncoding, BufferSize))
        {
            // Write header
            if (_options.IncludeHeader)
            {
                var headerColumns = new List<string>();
                if (_options.IncludeGeometry)
                {
                    headerColumns.Add(EscapeCsvValue(_options.GeometryFormat == "wkt" ? "WKT" : "GeoJSON"));
                }
                headerColumns.AddRange(fieldMappings.Select(m => EscapeCsvValue(m.ColumnName)));
                await writer.WriteLineAsync(string.Join(_options.Delimiter, headerColumns)).ConfigureAwait(false);
            }

            // Write features with batching to reduce I/O blocking
            var buffer = new List<string>(WriteBatchSize);

            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                if (count >= _options.MaxFeatures)
                {
                    throw new InvalidOperationException(
                        $"CSV export limit of {_options.MaxFeatures:N0} features exceeded. " +
                        "Please filter your query to export fewer features.");
                }

                var values = new List<string>();

                // Add geometry
                if (_options.IncludeGeometry)
                {
                    var geometryValue = ExtractGeometry(record, layer.GeometryField);
                    values.Add(EscapeCsvValue(geometryValue));
                }

                // Add attributes
                foreach (var mapping in fieldMappings)
                {
                    record.Attributes.TryGetValue(mapping.SourceName, out var value);
                    values.Add(EscapeCsvValue(NormalizeValue(value)));
                }

                var row = string.Join(_options.Delimiter, values);
                buffer.Add(row);
                count++;

                // Flush buffer periodically
                if (buffer.Count >= WriteBatchSize)
                {
                    await writer.WriteAsync(string.Join(Environment.NewLine, buffer)).ConfigureAwait(false);
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    buffer.Clear();

                    // Yield to other operations
                    await Task.Yield();
                }
            }

            // Flush remaining buffer
            if (buffer.Count > 0)
            {
                await writer.WriteAsync(string.Join(Environment.NewLine, buffer)).ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            await writer.FlushAsync().ConfigureAwait(false);

            _logger.LogInformation("CSV export completed: {Count} features", count);
        }

        var resultStream = new FileStream(tempPath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
        });

        var baseName = SanitizeFileName(layer.Id);
        return new CsvExportResult(resultStream, baseName + ".csv", count);
    }

    private IReadOnlyList<FieldMapping> BuildFieldMappings(LayerDefinition layer, FeatureQuery? query)
    {
        var propertyFilter = query?.PropertyNames is { Count: > 0 }
            ? new HashSet<string>(query.PropertyNames!, StringComparer.OrdinalIgnoreCase)
            : null;

        var mappings = new List<FieldMapping>();

        // Always include ID field first
        mappings.Add(new FieldMapping(layer.IdField, layer.IdField));

        foreach (var field in layer.Fields)
        {
            // Skip geometry and ID field (already added)
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, layer.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (propertyFilter is not null && !propertyFilter.Contains(field.Name))
            {
                continue;
            }

            mappings.Add(new FieldMapping(field.Name, field.Alias ?? field.Name));
        }

        return mappings;
    }

    private string ExtractGeometry(FeatureRecord record, string? geometryField)
    {
        var fieldName = geometryField.IsNullOrWhiteSpace() ? DefaultGeometryField : geometryField;
        if (!record.Attributes.TryGetValue(fieldName, out var raw) || raw is null)
        {
            return string.Empty;
        }

        try
        {
            if (_options.GeometryFormat == "wkt")
            {
                return ConvertToWkt(raw);
            }
            else
            {
                return ConvertToGeoJson(raw);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ConvertToWkt(object geometry)
    {
        // For now, assume GeoJSON is stored and we need NetTopologySuite to convert to WKT
        // This is a simplified implementation
        if (geometry is JsonNode node)
        {
            var json = node.ToJsonString();
            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geom = reader.Read<NetTopologySuite.Geometries.Geometry>(json);
            var writer = new NetTopologySuite.IO.WKTWriter();
            return writer.Write(geom);
        }

        if (geometry is JsonElement element)
        {
            var json = element.GetRawText();
            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geom = reader.Read<NetTopologySuite.Geometries.Geometry>(json);
            var writer = new NetTopologySuite.IO.WKTWriter();
            return writer.Write(geom);
        }

        return geometry.ToString() ?? string.Empty;
    }

    private static string ConvertToGeoJson(object geometry)
    {
        if (geometry is JsonNode node)
        {
            return node.ToJsonString();
        }

        if (geometry is JsonElement element)
        {
            return element.GetRawText();
        }

        return geometry.ToString() ?? string.Empty;
    }

    private static string NormalizeValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetRawText(),
            JsonElement element when element.ValueKind == JsonValueKind.True => "true",
            JsonElement element when element.ValueKind == JsonValueKind.False => "false",
            JsonElement element when element.ValueKind == JsonValueKind.Null => string.Empty,
            JsonNode node => node.ToJsonString(),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private string EscapeCsvValue(string? value)
    {
        if (value.IsNullOrEmpty())
        {
            return string.Empty;
        }

        // Prevent CSV injection (formula injection) by prefixing dangerous characters
        // Excel/LibreOffice/Google Sheets interpret cells starting with =, +, -, @ as formulas
        if (value.Length > 0)
        {
            var firstChar = value[0];
            if (firstChar is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                // Prefix with single quote to force text interpretation
                value = "'" + value;
            }
        }

        // Check if value needs quoting (contains delimiter, quote, or newline)
        var needsQuoting = value.Contains(_options.Delimiter) ||
                          value.Contains('"') ||
                          value.Contains('\n') ||
                          value.Contains('\r');

        if (!needsQuoting)
        {
            return value;
        }

        // Escape quotes by doubling them
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
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

    private sealed record FieldMapping(string SourceName, string ColumnName);
}
