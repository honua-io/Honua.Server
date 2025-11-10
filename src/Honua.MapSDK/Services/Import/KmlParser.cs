// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Parser for KML/KMZ files
/// </summary>
public class KmlParser : FileParserBase
{
    public override ImportFormat[] SupportedFormats => new[] { ImportFormat.KML };

    public override async Task<ParsedData> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var result = new ParsedData
        {
            Format = ImportFormat.KML,
            Encoding = DetectEncoding(content)
        };

        try
        {
            var xml = Encoding.UTF8.GetString(content);
            var doc = XDocument.Parse(xml);

            // KML uses namespaces
            XNamespace kmlNs = "http://www.opengis.net/kml/2.2";
            if (doc.Root?.Name.Namespace == XNamespace.None)
            {
                kmlNs = XNamespace.None; // Old KML without namespace
            }

            var placemarks = doc.Descendants(kmlNs + "Placemark");
            var rowNumber = 0;

            foreach (var placemark in placemarks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ParsePlacemark(placemark, kmlNs, result, rowNumber++);
            }

            result.TotalRows = rowNumber;
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
                Message = $"Failed to parse KML: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
            return result;
        }
    }

    private async Task ParsePlacemark(XElement placemark, XNamespace ns, ParsedData result, int rowNumber)
    {
        try
        {
            var feature = new ParsedFeature
            {
                Id = rowNumber.ToString(),
                RowNumber = rowNumber,
                IsValid = true
            };

            // Get name
            var name = placemark.Element(ns + "name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                feature.Properties["name"] = name;
            }

            // Get description
            var description = placemark.Element(ns + "description")?.Value;
            if (!string.IsNullOrEmpty(description))
            {
                feature.Properties["description"] = description;
            }

            // Parse extended data
            var extendedData = placemark.Element(ns + "ExtendedData");
            if (extendedData != null)
            {
                foreach (var data in extendedData.Descendants(ns + "Data"))
                {
                    var dataName = data.Attribute("name")?.Value;
                    var dataValue = data.Element(ns + "value")?.Value;

                    if (!string.IsNullOrEmpty(dataName) && !string.IsNullOrEmpty(dataValue))
                    {
                        feature.Properties[dataName] = dataValue;
                    }
                }

                // Also check for SimpleData (used in some KML variants)
                foreach (var data in extendedData.Descendants(ns + "SimpleData"))
                {
                    var dataName = data.Attribute("name")?.Value;
                    var dataValue = data.Value;

                    if (!string.IsNullOrEmpty(dataName))
                    {
                        feature.Properties[dataName] = dataValue;
                    }
                }
            }

            // Parse geometry
            var geometry = ParseGeometry(placemark, ns);
            if (geometry != null)
            {
                feature.Geometry = geometry;
            }
            else
            {
                feature.IsValid = false;
                feature.Errors.Add(new ValidationError
                {
                    RowNumber = rowNumber,
                    Message = "Placemark has no valid geometry",
                    Severity = ValidationSeverity.Warning
                });
            }

            result.Features.Add(feature);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ValidationError
            {
                RowNumber = rowNumber,
                Message = $"Error parsing placemark: {ex.Message}",
                Severity = ValidationSeverity.Error
            });
        }

        await Task.CompletedTask;
    }

    private Dictionary<string, object>? ParseGeometry(XElement placemark, XNamespace ns)
    {
        // Try Point
        var point = placemark.Descendants(ns + "Point").FirstOrDefault();
        if (point != null)
        {
            var coordinates = point.Element(ns + "coordinates")?.Value;
            if (!string.IsNullOrEmpty(coordinates))
            {
                var coords = ParseCoordinates(coordinates).FirstOrDefault();
                if (coords != null)
                {
                    return new Dictionary<string, object>
                    {
                        ["type"] = "Point",
                        ["coordinates"] = coords
                    };
                }
            }
        }

        // Try LineString
        var lineString = placemark.Descendants(ns + "LineString").FirstOrDefault();
        if (lineString != null)
        {
            var coordinates = lineString.Element(ns + "coordinates")?.Value;
            if (!string.IsNullOrEmpty(coordinates))
            {
                var coords = ParseCoordinates(coordinates);
                if (coords.Any())
                {
                    return new Dictionary<string, object>
                    {
                        ["type"] = "LineString",
                        ["coordinates"] = coords
                    };
                }
            }
        }

        // Try Polygon
        var polygon = placemark.Descendants(ns + "Polygon").FirstOrDefault();
        if (polygon != null)
        {
            var outerBoundary = polygon.Element(ns + "outerBoundaryIs")?.Element(ns + "LinearRing")?.Element(ns + "coordinates")?.Value;
            if (!string.IsNullOrEmpty(outerBoundary))
            {
                var coords = ParseCoordinates(outerBoundary);
                if (coords.Any())
                {
                    return new Dictionary<string, object>
                    {
                        ["type"] = "Polygon",
                        ["coordinates"] = new[] { coords }
                    };
                }
            }
        }

        return null;
    }

    private List<double[]> ParseCoordinates(string coordinatesText)
    {
        var result = new List<double[]>();

        var coordPairs = coordinatesText
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in coordPairs)
        {
            var parts = pair.Split(',');
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[0], out var lon) && double.TryParse(parts[1], out var lat))
                {
                    var coord = parts.Length >= 3 && double.TryParse(parts[2], out var elevation)
                        ? new[] { lon, lat, elevation }
                        : new[] { lon, lat };

                    result.Add(coord);
                }
            }
        }

        return result;
    }

    private void DetectFields(ParsedData result)
    {
        if (!result.Features.Any()) return;

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
                UniqueCount = values.Distinct().Count()
            };

            result.Fields.Add(field);
        }
    }

    public override double CanParse(byte[] content, string fileName)
    {
        var baseScore = base.CanParse(content, fileName);
        if (baseScore > 0) return baseScore;

        // Try to detect KML by content
        try
        {
            var text = Encoding.UTF8.GetString(content.Take(1000).ToArray());
            if (text.Contains("<kml") || text.Contains("<Placemark"))
            {
                return 0.9;
            }
        }
        catch
        {
            // Not valid UTF-8 or XML
        }

        return 0;
    }
}
