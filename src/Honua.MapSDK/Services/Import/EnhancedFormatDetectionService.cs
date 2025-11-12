// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Enhanced format detection with content sniffing and CRS detection
/// </summary>
public class EnhancedFormatDetectionService
{
    private readonly FileParserFactory _parserFactory;

    public EnhancedFormatDetectionService()
    {
        _parserFactory = new FileParserFactory();
    }

    /// <summary>
    /// Detect format with comprehensive content analysis
    /// </summary>
    public async Task<EnhancedFormatDetectionResult> DetectFormatAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var result = new EnhancedFormatDetectionResult
        {
            FileName = fileName,
            FileSize = content.Length,
            Extension = Path.GetExtension(fileName).ToLowerInvariant()
        };

        // 1. Extension-based detection
        result.Format = FormatInfo.DetectFromExtension(fileName);
        if (result.Format != ImportFormat.Unknown)
        {
            result.Confidence = 0.5;
            result.DetectionMethod = "Extension";
        }

        // 2. Magic number detection (file signatures)
        DetectByMagicNumber(content, result);

        // 3. Content-based detection
        await DetectByContentAsync(content, fileName, result, cancellationToken);

        // 4. Detect encoding
        result.Encoding = DetectEncoding(content);

        // 5. Detect CRS if possible
        await DetectCRSAsync(content, result, cancellationToken);

        // 6. Quick content analysis
        if (result.Format != ImportFormat.Unknown)
        {
            await AnalyzeContentAsync(content, result, cancellationToken);
        }

        return result;
    }

    private void DetectByMagicNumber(byte[] content, EnhancedFormatDetectionResult result)
    {
        if (content.Length < 4) return;

        // ZIP files (Shapefile, KMZ, GeoPackage in some cases)
        if (content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04)
        {
            // Check if it's a shapefile by looking for .shp/.shx/.dbf in the archive
            var zipContent = Encoding.UTF8.GetString(content.Take(Math.Min(1000, content.Length)).ToArray());
            if (zipContent.Contains(".shp", StringComparison.OrdinalIgnoreCase))
            {
                result.Format = ImportFormat.Shapefile;
                result.Confidence = 0.95;
                result.DetectionMethod = "Magic Number + Content";
                result.IsCompressed = true;
                return;
            }

            if (zipContent.Contains("doc.kml", StringComparison.OrdinalIgnoreCase))
            {
                result.Format = ImportFormat.KMZ;
                result.Confidence = 0.95;
                result.DetectionMethod = "Magic Number + Content";
                result.IsCompressed = true;
                return;
            }

            result.IsCompressed = true;
            result.Confidence = 0.7;
            result.DetectionMethod = "Magic Number (ZIP)";
        }

        // SQLite (GeoPackage)
        if (content.Length >= 16 &&
            content[0] == 0x53 && content[1] == 0x51 &&
            content[2] == 0x4C && content[3] == 0x69)
        {
            result.Format = ImportFormat.Shapefile; // Using Shapefile enum for now
            result.Confidence = 0.9;
            result.DetectionMethod = "Magic Number (SQLite/GeoPackage)";
            result.Metadata["IsSQLite"] = true;
        }
    }

    private async Task DetectByContentAsync(
        byte[] content,
        string fileName,
        EnhancedFormatDetectionResult result,
        CancellationToken cancellationToken)
    {
        // Try parser-based detection
        var parser = _parserFactory.GetParser(content, fileName);
        if (parser != null)
        {
            var confidence = parser.CanParse(content, fileName);
            if (confidence > result.Confidence)
            {
                result.Format = parser.SupportedFormats.FirstOrDefault();
                result.Confidence = confidence;
                result.DetectionMethod = "Content Analysis";
            }
        }

        // Additional content inspection
        try
        {
            var text = Encoding.UTF8.GetString(content.Take(Math.Min(2000, content.Length)).ToArray());

            // GeoJSON detection
            if (text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("["))
            {
                if (text.Contains("\"type\"") && (
                    text.Contains("FeatureCollection") ||
                    text.Contains("Feature") ||
                    text.Contains("Point") ||
                    text.Contains("LineString") ||
                    text.Contains("Polygon") ||
                    text.Contains("MultiPoint") ||
                    text.Contains("MultiLineString") ||
                    text.Contains("MultiPolygon") ||
                    text.Contains("GeometryCollection")))
                {
                    result.Format = ImportFormat.GeoJson;
                    result.Confidence = 0.95;
                    result.DetectionMethod = "Content Analysis (JSON)";
                    result.IsGeoJSON = true;
                }
            }

            // KML detection
            if (text.TrimStart().StartsWith("<?xml") || text.TrimStart().StartsWith("<kml"))
            {
                if (text.Contains("<kml", StringComparison.OrdinalIgnoreCase))
                {
                    result.Format = ImportFormat.KML;
                    result.Confidence = 0.95;
                    result.DetectionMethod = "Content Analysis (XML)";
                }
            }

            // GPX detection
            if (text.Contains("<gpx", StringComparison.OrdinalIgnoreCase))
            {
                result.Format = ImportFormat.GPX;
                result.Confidence = 0.95;
                result.DetectionMethod = "Content Analysis (XML)";
            }

            // CSV/TSV detection
            if (result.Format == ImportFormat.CSV || result.Format == ImportFormat.TSV || result.Format == ImportFormat.Unknown)
            {
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 2)
                {
                    var firstLine = lines[0];
                    var commaCount = firstLine.Count(c => c == ',');
                    var tabCount = firstLine.Count(c => c == '\t');

                    if (tabCount > commaCount && tabCount >= 2)
                    {
                        result.Format = ImportFormat.TSV;
                        result.Confidence = 0.85;
                        result.DetectionMethod = "Content Analysis (Delimiter)";
                    }
                    else if (commaCount >= 2)
                    {
                        result.Format = ImportFormat.CSV;
                        result.Confidence = 0.85;
                        result.DetectionMethod = "Content Analysis (Delimiter)";
                    }
                }
            }
        }
        catch
        {
            // Not text-based format or encoding issue
        }

        await Task.CompletedTask;
    }

    private async Task DetectCRSAsync(
        byte[] content,
        EnhancedFormatDetectionResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (result.Format == ImportFormat.GeoJson)
            {
                var text = Encoding.UTF8.GetString(content);
                var json = JsonDocument.Parse(text);

                // Check for CRS property
                if (json.RootElement.TryGetProperty("crs", out var crs))
                {
                    if (crs.TryGetProperty("properties", out var props) &&
                        props.TryGetProperty("name", out var name))
                    {
                        result.CRS = name.GetString();
                    }
                }
                else
                {
                    // GeoJSON default is WGS84 (EPSG:4326)
                    result.CRS = "EPSG:4326";
                }
            }
            else if (result.Format == ImportFormat.KML)
            {
                // KML is always in WGS84 (EPSG:4326)
                result.CRS = "EPSG:4326";
            }
            else if (result.Format == ImportFormat.CSV || result.Format == ImportFormat.TSV)
            {
                // CSV typically uses WGS84 for lat/lon coordinates
                result.CRS = "EPSG:4326";
            }
        }
        catch
        {
            // CRS detection failed, not critical
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeContentAsync(
        byte[] content,
        EnhancedFormatDetectionResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Quick feature count estimation
            if (result.Format == ImportFormat.GeoJson)
            {
                var text = Encoding.UTF8.GetString(content);
                var json = JsonDocument.Parse(text);

                if (json.RootElement.TryGetProperty("type", out var type) &&
                    type.GetString() == "FeatureCollection" &&
                    json.RootElement.TryGetProperty("features", out var features))
                {
                    result.EstimatedFeatureCount = features.GetArrayLength();
                }
                else if (type.GetString() == "Feature")
                {
                    result.EstimatedFeatureCount = 1;
                }

                // Detect geometry type
                if (json.RootElement.TryGetProperty("features", out var featureArray))
                {
                    var firstFeature = featureArray.EnumerateArray().FirstOrDefault();
                    if (firstFeature.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("type", out var geomType))
                    {
                        result.GeometryType = geomType.GetString();
                    }
                }
            }
            else if (result.Format == ImportFormat.CSV || result.Format == ImportFormat.TSV)
            {
                var text = Encoding.UTF8.GetString(content);
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                result.EstimatedFeatureCount = Math.Max(0, lines.Length - 1); // Subtract header
            }
        }
        catch
        {
            // Analysis failed, not critical
        }

        await Task.CompletedTask;
    }

    private string DetectEncoding(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return "UTF-8";

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return "UTF-16LE";

        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return "UTF-16BE";

        if (content.Length >= 4 && content[0] == 0x00 && content[1] == 0x00 && content[2] == 0xFE && content[3] == 0xFF)
            return "UTF-32BE";

        if (content.Length >= 4 && content[0] == 0xFF && content[1] == 0xFE && content[2] == 0x00 && content[3] == 0x00)
            return "UTF-32LE";

        return "UTF-8"; // Default
    }
}

/// <summary>
/// Enhanced format detection result with additional metadata
/// </summary>
public class EnhancedFormatDetectionResult
{
    /// <summary>
    /// Original file name
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// File extension
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Detected format
    /// </summary>
    public ImportFormat Format { get; set; } = ImportFormat.Unknown;

    /// <summary>
    /// Detection confidence (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Method used for detection
    /// </summary>
    public string DetectionMethod { get; set; } = "Unknown";

    /// <summary>
    /// Character encoding
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// Coordinate reference system
    /// </summary>
    public string? CRS { get; set; }

    /// <summary>
    /// Whether file is compressed
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Whether file contains GeoJSON
    /// </summary>
    public bool IsGeoJSON { get; set; }

    /// <summary>
    /// Estimated number of features
    /// </summary>
    public int? EstimatedFeatureCount { get; set; }

    /// <summary>
    /// Detected geometry type
    /// </summary>
    public string? GeometryType { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Validation issues found during detection
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
