using System.Text;
using System.Text.Json;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility methods for transforming data between different formats.
/// </summary>
public static class DataTransform
{
    /// <summary>
    /// Converts GeoJSON to CSV format.
    /// </summary>
    /// <param name="geoJson">GeoJSON string.</param>
    /// <param name="includeGeometry">Whether to include geometry as WKT in the CSV.</param>
    /// <returns>CSV string.</returns>
    public static string GeoJsonToCsv(string geoJson, bool includeGeometry = false)
    {
        using var document = JsonDocument.Parse(geoJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("features", out var features))
            throw new InvalidOperationException("Invalid GeoJSON: missing 'features' property");

        var csv = new StringBuilder();
        var headers = new List<string>();
        var rows = new List<List<string>>();

        // Extract headers and data
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var properties))
                continue;

            var row = new List<string>();

            foreach (var property in properties.EnumerateObject())
            {
                if (!headers.Contains(property.Name))
                    headers.Add(property.Name);

                var index = headers.IndexOf(property.Name);
                while (row.Count <= index)
                    row.Add("");

                row[index] = FormatCsvValue(property.Value);
            }

            if (includeGeometry && feature.TryGetProperty("geometry", out var geometry))
            {
                if (!headers.Contains("geometry"))
                    headers.Add("geometry");

                var geoIndex = headers.IndexOf("geometry");
                while (row.Count <= geoIndex)
                    row.Add("");

                row[geoIndex] = geometry.ToString();
            }

            rows.Add(row);
        }

        // Write headers
        csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        // Write rows
        foreach (var row in rows)
        {
            while (row.Count < headers.Count)
                row.Add("");

            csv.AppendLine(string.Join(",", row.Select(v => $"\"{v}\"")));
        }

        return csv.ToString();
    }

    /// <summary>
    /// Converts CSV to GeoJSON format.
    /// </summary>
    /// <param name="csv">CSV string.</param>
    /// <param name="longitudeColumn">Name of longitude column.</param>
    /// <param name="latitudeColumn">Name of latitude column.</param>
    /// <returns>GeoJSON string.</returns>
    public static string CsvToGeoJson(string csv, string longitudeColumn, string latitudeColumn)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new InvalidOperationException("CSV must have at least a header row and one data row");

        var headers = ParseCsvLine(lines[0]);
        var lonIndex = Array.IndexOf(headers, longitudeColumn);
        var latIndex = Array.IndexOf(headers, latitudeColumn);

        if (lonIndex == -1 || latIndex == -1)
            throw new InvalidOperationException($"Could not find columns '{longitudeColumn}' and/or '{latitudeColumn}'");

        var features = new List<string>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Length != headers.Length)
                continue;

            if (!double.TryParse(values[lonIndex], out var lon) || !double.TryParse(values[latIndex], out var lat))
                continue;

            var properties = new StringBuilder();
            properties.Append("{");

            var first = true;
            for (var j = 0; j < headers.Length; j++)
            {
                if (j == lonIndex || j == latIndex)
                    continue;

                if (!first)
                    properties.Append(",");

                properties.Append($"\"{headers[j]}\":");

                // Try to parse as number
                if (double.TryParse(values[j], out var numValue))
                    properties.Append(numValue);
                else
                    properties.Append($"\"{EscapeJson(values[j])}\"");

                first = false;
            }

            properties.Append("}");

            var feature = $@"{{
                ""type"": ""Feature"",
                ""geometry"": {{
                    ""type"": ""Point"",
                    ""coordinates"": [{lon}, {lat}]
                }},
                ""properties"": {properties}
            }}";

            features.Add(feature);
        }

        var geoJson = $@"{{
            ""type"": ""FeatureCollection"",
            ""features"": [{string.Join(",", features)}]
        }}";

        return geoJson;
    }

    /// <summary>
    /// Converts a JSON array to CSV format.
    /// </summary>
    /// <param name="jsonArray">JSON array string.</param>
    /// <returns>CSV string.</returns>
    public static string JsonArrayToCsv(string jsonArray)
    {
        using var document = JsonDocument.Parse(jsonArray);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Input must be a JSON array");

        var csv = new StringBuilder();
        var headers = new List<string>();
        var rows = new List<List<string>>();

        // Extract headers and data
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var row = new List<string>();

            foreach (var property in item.EnumerateObject())
            {
                if (!headers.Contains(property.Name))
                    headers.Add(property.Name);

                var index = headers.IndexOf(property.Name);
                while (row.Count <= index)
                    row.Add("");

                row[index] = FormatCsvValue(property.Value);
            }

            rows.Add(row);
        }

        // Write headers
        csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        // Write rows
        foreach (var row in rows)
        {
            while (row.Count < headers.Count)
                row.Add("");

            csv.AppendLine(string.Join(",", row.Select(v => $"\"{v}\"")));
        }

        return csv.ToString();
    }

    /// <summary>
    /// Converts CSV to a JSON array.
    /// </summary>
    /// <param name="csv">CSV string.</param>
    /// <returns>JSON array string.</returns>
    public static string CsvToJsonArray(string csv)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new InvalidOperationException("CSV must have at least a header row and one data row");

        var headers = ParseCsvLine(lines[0]);
        var items = new List<string>();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Length != headers.Length)
                continue;

            var properties = new StringBuilder();
            properties.Append("{");

            for (var j = 0; j < headers.Length; j++)
            {
                if (j > 0)
                    properties.Append(",");

                properties.Append($"\"{headers[j]}\":");

                // Try to parse as number
                if (double.TryParse(values[j], out var numValue))
                    properties.Append(numValue);
                else if (bool.TryParse(values[j], out var boolValue))
                    properties.Append(boolValue.ToString().ToLower());
                else
                    properties.Append($"\"{EscapeJson(values[j])}\"");
            }

            properties.Append("}");
            items.Add(properties.ToString());
        }

        return $"[{string.Join(",", items)}]";
    }

    /// <summary>
    /// Flattens a nested JSON object to a flat structure.
    /// </summary>
    /// <param name="json">JSON string.</param>
    /// <param name="separator">Separator for nested keys (default: ".").</param>
    /// <returns>Flattened JSON string.</returns>
    public static string FlattenJson(string json, string separator = ".")
    {
        using var document = JsonDocument.Parse(json);
        var flattened = new Dictionary<string, object?>();

        FlattenElement(document.RootElement, "", flattened, separator);

        return JsonSerializer.Serialize(flattened);
    }

    /// <summary>
    /// Recursively flattens a JSON element.
    /// </summary>
    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, object?> result, string separator)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}{separator}{property.Name}";
                    FlattenElement(property.Value, key, result, separator);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}[{index}]";
                    FlattenElement(item, key, result, separator);
                    index++;
                }
                break;

            default:
                result[prefix] = GetValue(element);
                break;
        }
    }

    /// <summary>
    /// Gets the value from a JSON element.
    /// </summary>
    private static object? GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Formats a JSON value for CSV output.
    /// </summary>
    private static string FormatCsvValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Parses a CSV line, handling quoted values.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString().Trim());
        return values.ToArray();
    }

    /// <summary>
    /// Escapes a string for JSON output.
    /// </summary>
    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
