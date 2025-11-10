// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Parser for CSV and TSV files
/// </summary>
public class CsvParser : FileParserBase
{
    public override ImportFormat[] SupportedFormats => new[] { ImportFormat.CSV, ImportFormat.TSV };

    public override async Task<ParsedData> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var format = FormatInfo.DetectFromExtension(fileName);
        var delimiter = format == ImportFormat.TSV ? '\t' : ',';

        var result = new ParsedData
        {
            Format = format,
            Encoding = DetectEncoding(content)
        };

        try
        {
            var text = Encoding.UTF8.GetString(content);
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                throw new FormatException("CSV file is empty");
            }

            // Parse header
            var headers = ParseLine(lines[0], delimiter);
            if (headers.Length == 0)
            {
                throw new FormatException("CSV file has no columns");
            }

            // Initialize fields
            var fields = headers.Select(h => new FieldDefinition
            {
                Name = h.Trim(),
                DisplayName = h.Trim(),
                Type = FieldType.String,
                IsLikelyLatitude = IsLikelyLatitude(h),
                IsLikelyLongitude = IsLikelyLongitude(h),
                IsLikelyAddress = IsLikelyAddress(h)
            }).ToList();

            // Parse data rows
            var rowNumber = 0;
            var allValues = new Dictionary<string, List<object?>>();

            foreach (var header in headers)
            {
                allValues[header] = new List<object?>();
            }

            for (int i = 1; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseLine(line, delimiter);
                rowNumber++;

                var feature = new ParsedFeature
                {
                    Id = rowNumber.ToString(),
                    RowNumber = rowNumber,
                    IsValid = true
                };

                for (int j = 0; j < headers.Length; j++)
                {
                    var header = headers[j].Trim();
                    var value = j < values.Length ? values[j].Trim() : null;

                    object? parsedValue = string.IsNullOrEmpty(value) ? null : value;

                    feature.Properties[header] = parsedValue;
                    allValues[header].Add(parsedValue);
                }

                result.Features.Add(feature);
            }

            result.TotalRows = rowNumber;
            result.ValidRows = result.Features.Count;

            // Detect field types and calculate statistics
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var values = allValues[field.Name];

                field.Type = DetectFieldType(values);
                field.SampleValues = values.Take(5).ToList();
                field.NullCount = values.Count(v => v == null);
                field.UniqueCount = values.Distinct().Count();

                // Calculate min/max for numeric fields
                if (field.Type == FieldType.Number || field.Type == FieldType.Integer)
                {
                    var numericValues = values
                        .Where(v => v != null && double.TryParse(v?.ToString(), out _))
                        .Select(v => Convert.ToDouble(v))
                        .ToList();

                    if (numericValues.Any())
                    {
                        field.MinValue = numericValues.Min();
                        field.MaxValue = numericValues.Max();
                    }
                }
            }

            result.Fields = fields;

            // Try to detect lat/lon fields and create geometry
            await TryCreateGeometry(result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ValidationError
            {
                RowNumber = 0,
                Message = $"Failed to parse CSV: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            return result;
        }
    }

    private string[] ParseLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private async Task TryCreateGeometry(ParsedData result, CancellationToken cancellationToken)
    {
        // Find likely latitude and longitude fields
        var latField = result.Fields.FirstOrDefault(f => f.IsLikelyLatitude && (f.Type == FieldType.Number || f.Type == FieldType.Integer));
        var lonField = result.Fields.FirstOrDefault(f => f.IsLikelyLongitude && (f.Type == FieldType.Number || f.Type == FieldType.Integer));

        if (latField == null || lonField == null)
        {
            await Task.CompletedTask;
            return;
        }

        // Create point geometries
        foreach (var feature in result.Features)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feature.Properties.TryGetValue(latField.Name, out var latValue) &&
                feature.Properties.TryGetValue(lonField.Name, out var lonValue) &&
                latValue != null && lonValue != null)
            {
                if (double.TryParse(latValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(lonValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
                {
                    // Validate coordinate ranges
                    if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                    {
                        feature.Geometry = new Dictionary<string, object>
                        {
                            ["type"] = "Point",
                            ["coordinates"] = new[] { lon, lat }
                        };
                    }
                    else
                    {
                        feature.Errors.Add(new ValidationError
                        {
                            RowNumber = feature.RowNumber,
                            Field = $"{latField.Name}, {lonField.Name}",
                            Message = $"Coordinates out of range: ({lat}, {lon})",
                            Severity = ValidationSeverity.Warning
                        });
                    }
                }
            }
        }

        CalculateBoundingBox(result);
        await Task.CompletedTask;
    }

    public override double CanParse(byte[] content, string fileName)
    {
        var baseScore = base.CanParse(content, fileName);
        if (baseScore > 0) return baseScore;

        // Try to detect CSV by content
        try
        {
            var text = Encoding.UTF8.GetString(content.Take(1000).ToArray());
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (lines.Length > 1)
            {
                var firstLine = lines[0];
                var commaCount = firstLine.Count(c => c == ',');
                var tabCount = firstLine.Count(c => c == '\t');

                if (commaCount > 0 || tabCount > 0)
                {
                    return 0.6;
                }
            }
        }
        catch
        {
            // Not valid UTF-8
        }

        return 0;
    }
}
