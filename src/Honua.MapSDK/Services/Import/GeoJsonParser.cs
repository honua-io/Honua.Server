// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Parser for GeoJSON files
/// </summary>
public class GeoJsonParser : FileParserBase
{
    public override ImportFormat[] SupportedFormats => new[] { ImportFormat.GeoJson };

    public override async Task<ParsedData> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var result = new ParsedData
        {
            Format = ImportFormat.GeoJson,
            Encoding = DetectEncoding(content)
        };

        try
        {
            var json = Encoding.UTF8.GetString(content);
            var geoJson = JsonSerializer.Deserialize<JsonElement>(json);

            if (!geoJson.TryGetProperty("type", out var typeElement))
            {
                throw new FormatException("Invalid GeoJSON: missing 'type' property");
            }

            var type = typeElement.GetString();

            if (type == "FeatureCollection")
            {
                await ParseFeatureCollection(geoJson, result, cancellationToken);
            }
            else if (type == "Feature")
            {
                ParseFeature(geoJson, result, 0);
            }
            else
            {
                // Single geometry
                result.Features.Add(new ParsedFeature
                {
                    Id = "1",
                    Geometry = JsonSerializer.Deserialize<Dictionary<string, object>>(geoJson.GetRawText()),
                    RowNumber = 0,
                    IsValid = true
                });
            }

            // Extract CRS if present
            if (geoJson.TryGetProperty("crs", out var crs))
            {
                if (crs.TryGetProperty("properties", out var props) &&
                    props.TryGetProperty("name", out var name))
                {
                    result.CRS = name.GetString();
                }
            }

            result.TotalRows = result.Features.Count;
            result.ValidRows = result.Features.Count(f => f.IsValid);

            DetectFields(result);
            CalculateBoundingBox(result);

            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ValidationError
            {
                RowNumber = 0,
                Message = $"Failed to parse GeoJSON: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            return result;
        }
    }

    private async Task ParseFeatureCollection(JsonElement geoJson, ParsedData result, CancellationToken cancellationToken)
    {
        if (!geoJson.TryGetProperty("features", out var featuresElement))
        {
            throw new FormatException("FeatureCollection missing 'features' property");
        }

        var features = featuresElement.EnumerateArray();
        var rowNumber = 0;

        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ParseFeature(feature, result, rowNumber++);
        }

        await Task.CompletedTask;
    }

    private void ParseFeature(JsonElement feature, ParsedData result, int rowNumber)
    {
        try
        {
            var parsedFeature = new ParsedFeature
            {
                RowNumber = rowNumber,
                IsValid = true
            };

            // Get ID
            if (feature.TryGetProperty("id", out var idElement))
            {
                parsedFeature.Id = idElement.ToString();
            }

            // Get geometry
            if (feature.TryGetProperty("geometry", out var geometryElement))
            {
                parsedFeature.Geometry = JsonSerializer.Deserialize<Dictionary<string, object>>(geometryElement.GetRawText());
            }

            // Get properties
            if (feature.TryGetProperty("properties", out var propertiesElement))
            {
                parsedFeature.Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(propertiesElement.GetRawText())
                    ?? new Dictionary<string, object?>();
            }

            // Validate geometry
            if (parsedFeature.Geometry == null)
            {
                parsedFeature.IsValid = false;
                parsedFeature.Errors.Add(new ValidationError
                {
                    RowNumber = rowNumber,
                    Message = "Feature has no geometry",
                    Severity = ValidationSeverity.Warning
                });
            }

            result.Features.Add(parsedFeature);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ValidationError
            {
                RowNumber = rowNumber,
                Message = $"Error parsing feature: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
        }
    }

    private void DetectFields(ParsedData result)
    {
        if (!result.Features.Any()) return;

        // Collect all unique property names
        var fieldNames = result.Features
            .SelectMany(f => f.Properties.Keys)
            .Distinct()
            .ToList();

        foreach (var fieldName in fieldNames)
        {
            var values = result.Features
                .Select(f => f.Properties.GetValueOrDefault(fieldName))
                .ToList();

            var nonNullValues = values.Where(v => v != null).ToList();

            var field = new FieldDefinition
            {
                Name = fieldName,
                DisplayName = fieldName,
                Type = DetectFieldType(values),
                SampleValues = values.Take(5).ToList(),
                NullCount = values.Count - nonNullValues.Count,
                UniqueCount = values.Distinct().Count(),
                IsLikelyLatitude = IsLikelyLatitude(fieldName),
                IsLikelyLongitude = IsLikelyLongitude(fieldName),
                IsLikelyAddress = IsLikelyAddress(fieldName)
            };

            // Calculate min/max for numeric fields
            if (field.Type == FieldType.Number || field.Type == FieldType.Integer)
            {
                var numericValues = nonNullValues
                    .Select(v => Convert.ToDouble(v))
                    .ToList();

                if (numericValues.Any())
                {
                    field.MinValue = numericValues.Min();
                    field.MaxValue = numericValues.Max();
                }
            }

            result.Fields.Add(field);
        }
    }

    public override double CanParse(byte[] content, string fileName)
    {
        var baseScore = base.CanParse(content, fileName);
        if (baseScore > 0) return baseScore;

        // Try to detect GeoJSON by content
        try
        {
            var json = Encoding.UTF8.GetString(content.Take(1000).ToArray());
            if (json.Contains("\"type\"") && (json.Contains("FeatureCollection") || json.Contains("Feature") || json.Contains("Point") || json.Contains("LineString") || json.Contains("Polygon")))
            {
                return 0.8;
            }
        }
        catch
        {
            // Not valid UTF-8 or JSON
        }

        return 0;
    }
}
