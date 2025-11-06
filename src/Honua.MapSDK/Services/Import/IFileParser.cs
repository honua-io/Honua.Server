using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Interface for file parsers
/// </summary>
public interface IFileParser
{
    /// <summary>
    /// Formats supported by this parser
    /// </summary>
    ImportFormat[] SupportedFormats { get; }

    /// <summary>
    /// Parse a file into structured data
    /// </summary>
    /// <param name="content">File content as byte array</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed data</returns>
    Task<ParsedData> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect if this parser can handle the given content
    /// </summary>
    /// <param name="content">File content</param>
    /// <param name="fileName">File name</param>
    /// <returns>Detection confidence (0-1)</returns>
    double CanParse(byte[] content, string fileName);
}

/// <summary>
/// Base class for file parsers
/// </summary>
public abstract class FileParserBase : IFileParser
{
    public abstract ImportFormat[] SupportedFormats { get; }

    public abstract Task<ParsedData> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);

    public virtual double CanParse(byte[] content, string fileName)
    {
        var format = FormatInfo.DetectFromExtension(fileName);
        return SupportedFormats.Contains(format) ? 1.0 : 0.0;
    }

    protected static string DetectEncoding(byte[] content)
    {
        // Check for BOM
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return "UTF-8";

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return "UTF-16LE";

        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return "UTF-16BE";

        // Default to UTF-8
        return "UTF-8";
    }

    protected static FieldType DetectFieldType(IEnumerable<object?> values)
    {
        var nonNullValues = values.Where(v => v != null).ToList();
        if (!nonNullValues.Any()) return FieldType.String;

        // Check if all values are numbers
        if (nonNullValues.All(v => double.TryParse(v?.ToString(), out _)))
        {
            // Check if all are integers
            if (nonNullValues.All(v => int.TryParse(v?.ToString(), out _)))
                return FieldType.Integer;
            return FieldType.Number;
        }

        // Check if all values are booleans
        if (nonNullValues.All(v => bool.TryParse(v?.ToString(), out _)))
            return FieldType.Boolean;

        // Check if all values are dates
        if (nonNullValues.All(v => DateTime.TryParse(v?.ToString(), out _)))
            return FieldType.DateTime;

        return FieldType.String;
    }

    protected static bool IsLikelyLatitude(string fieldName)
    {
        var normalized = fieldName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
        return normalized.Contains("lat") || normalized == "y" || normalized.Contains("latitude");
    }

    protected static bool IsLikelyLongitude(string fieldName)
    {
        var normalized = fieldName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
        return normalized.Contains("lon") || normalized.Contains("lng") || normalized == "x" || normalized.Contains("longitude");
    }

    protected static bool IsLikelyAddress(string fieldName)
    {
        var normalized = fieldName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
        return normalized.Contains("address") || normalized.Contains("location") || normalized.Contains("addr");
    }

    protected static void CalculateBoundingBox(ParsedData data)
    {
        double? minLon = null, minLat = null, maxLon = null, maxLat = null;

        foreach (var feature in data.Features.Where(f => f.Geometry != null))
        {
            // This is a simplified version - in production you'd need proper geometry parsing
            // For now, just look for simple point geometries
            if (feature.Geometry is Dictionary<string, object> geom &&
                geom.TryGetValue("type", out var typeObj) &&
                typeObj?.ToString() == "Point" &&
                geom.TryGetValue("coordinates", out var coordsObj) &&
                coordsObj is IEnumerable<object> coords)
            {
                var coordArray = coords.Select(c => Convert.ToDouble(c)).ToArray();
                if (coordArray.Length >= 2)
                {
                    var lon = coordArray[0];
                    var lat = coordArray[1];

                    minLon = minLon == null ? lon : Math.Min(minLon.Value, lon);
                    maxLon = maxLon == null ? lon : Math.Max(maxLon.Value, lon);
                    minLat = minLat == null ? lat : Math.Min(minLat.Value, lat);
                    maxLat = maxLat == null ? lat : Math.Max(maxLat.Value, lat);
                }
            }
        }

        if (minLon != null && minLat != null && maxLon != null && maxLat != null)
        {
            data.BoundingBox = new[] { minLon.Value, minLat.Value, maxLon.Value, maxLat.Value };
        }
    }
}
