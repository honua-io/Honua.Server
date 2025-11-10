// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models.Import;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Factory for creating appropriate file parsers
/// </summary>
public class FileParserFactory
{
    private readonly List<IFileParser> _parsers = new();

    public FileParserFactory()
    {
        // Register all parsers
        RegisterParser(new GeoJsonParser());
        RegisterParser(new CsvParser());
        RegisterParser(new KmlParser());
    }

    /// <summary>
    /// Register a custom parser
    /// </summary>
    public void RegisterParser(IFileParser parser)
    {
        _parsers.Add(parser);
    }

    /// <summary>
    /// Get the best parser for the given file
    /// </summary>
    public IFileParser? GetParser(byte[] content, string fileName)
    {
        var scores = _parsers
            .Select(p => new { Parser = p, Score = p.CanParse(content, fileName) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        return scores.FirstOrDefault()?.Parser;
    }

    /// <summary>
    /// Get parser for a specific format
    /// </summary>
    public IFileParser? GetParser(ImportFormat format)
    {
        return _parsers.FirstOrDefault(p => p.SupportedFormats.Contains(format));
    }

    /// <summary>
    /// Detect format from file
    /// </summary>
    public FormatDetectionResult DetectFormat(byte[] content, string fileName)
    {
        var result = new FormatDetectionResult
        {
            Extension = Path.GetExtension(fileName).ToLowerInvariant()
        };

        // Try extension-based detection first
        var formatFromExtension = FormatInfo.DetectFromExtension(fileName);
        if (formatFromExtension != ImportFormat.Unknown)
        {
            result.Format = formatFromExtension;
            result.Confidence = 0.7;
        }

        // Try content-based detection
        var parser = GetParser(content, fileName);
        if (parser != null)
        {
            var confidence = parser.CanParse(content, fileName);
            if (confidence > result.Confidence)
            {
                result.Format = parser.SupportedFormats.FirstOrDefault();
                result.Confidence = confidence;
            }
        }

        // Detect encoding
        if (result.Format == ImportFormat.CSV ||
            result.Format == ImportFormat.TSV ||
            result.Format == ImportFormat.GeoJson ||
            result.Format == ImportFormat.KML)
        {
            result.Encoding = DetectEncoding(content);
        }

        return result;
    }

    private static string DetectEncoding(byte[] content)
    {
        // Check for BOM
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return "UTF-8";

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return "UTF-16LE";

        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return "UTF-16BE";

        return "UTF-8";
    }

    /// <summary>
    /// Get all supported formats
    /// </summary>
    public ImportFormat[] GetSupportedFormats()
    {
        return _parsers
            .SelectMany(p => p.SupportedFormats)
            .Distinct()
            .ToArray();
    }
}
