// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Services.Models.Import;

/// <summary>
/// Supported import formats
/// </summary>
public enum ImportFormat
{
    /// <summary>
    /// GeoJSON format
    /// </summary>
    GeoJson,

    /// <summary>
    /// Comma-separated values
    /// </summary>
    CSV,

    /// <summary>
    /// Tab-separated values
    /// </summary>
    TSV,

    /// <summary>
    /// Google Earth KML
    /// </summary>
    KML,

    /// <summary>
    /// Compressed KML (KMZ)
    /// </summary>
    KMZ,

    /// <summary>
    /// ESRI Shapefile (zipped)
    /// </summary>
    Shapefile,

    /// <summary>
    /// GPS Exchange Format
    /// </summary>
    GPX,

    /// <summary>
    /// Excel spreadsheet
    /// </summary>
    Excel,

    /// <summary>
    /// Generic JSON
    /// </summary>
    Json,

    /// <summary>
    /// Well-Known Text geometry
    /// </summary>
    WKT,

    /// <summary>
    /// TopoJSON format
    /// </summary>
    TopoJson,

    /// <summary>
    /// Unknown/unsupported format
    /// </summary>
    Unknown
}

/// <summary>
/// Format detection result
/// </summary>
public class FormatDetectionResult
{
    /// <summary>
    /// Detected format
    /// </summary>
    public ImportFormat Format { get; set; } = ImportFormat.Unknown;

    /// <summary>
    /// Confidence level (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// File extension
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// MIME type
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Character encoding detected
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// Additional metadata about the format
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// File format metadata
/// </summary>
public static class FormatInfo
{
    public static readonly Dictionary<ImportFormat, FormatMetadata> Formats = new()
    {
        [ImportFormat.GeoJson] = new FormatMetadata
        {
            Name = "GeoJSON",
            Extensions = new[] { ".geojson", ".json" },
            MimeTypes = new[] { "application/geo+json", "application/json" },
            Description = "Standard geographic JSON format",
            HasGeometry = true,
            SupportsAttributes = true
        },
        [ImportFormat.CSV] = new FormatMetadata
        {
            Name = "CSV",
            Extensions = new[] { ".csv" },
            MimeTypes = new[] { "text/csv", "application/csv" },
            Description = "Comma-separated values",
            HasGeometry = false,
            SupportsAttributes = true
        },
        [ImportFormat.TSV] = new FormatMetadata
        {
            Name = "TSV",
            Extensions = new[] { ".tsv", ".tab" },
            MimeTypes = new[] { "text/tab-separated-values" },
            Description = "Tab-separated values",
            HasGeometry = false,
            SupportsAttributes = true
        },
        [ImportFormat.KML] = new FormatMetadata
        {
            Name = "KML",
            Extensions = new[] { ".kml" },
            MimeTypes = new[] { "application/vnd.google-earth.kml+xml" },
            Description = "Keyhole Markup Language",
            HasGeometry = true,
            SupportsAttributes = true
        },
        [ImportFormat.KMZ] = new FormatMetadata
        {
            Name = "KMZ",
            Extensions = new[] { ".kmz" },
            MimeTypes = new[] { "application/vnd.google-earth.kmz" },
            Description = "Compressed KML",
            HasGeometry = true,
            SupportsAttributes = true
        },
        [ImportFormat.Shapefile] = new FormatMetadata
        {
            Name = "Shapefile",
            Extensions = new[] { ".zip", ".shp" },
            MimeTypes = new[] { "application/zip", "application/x-shapefile" },
            Description = "ESRI Shapefile (zipped)",
            HasGeometry = true,
            SupportsAttributes = true
        },
        [ImportFormat.GPX] = new FormatMetadata
        {
            Name = "GPX",
            Extensions = new[] { ".gpx" },
            MimeTypes = new[] { "application/gpx+xml" },
            Description = "GPS Exchange Format",
            HasGeometry = true,
            SupportsAttributes = true
        },
        [ImportFormat.Excel] = new FormatMetadata
        {
            Name = "Excel",
            Extensions = new[] { ".xlsx", ".xls" },
            MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            Description = "Microsoft Excel spreadsheet",
            HasGeometry = false,
            SupportsAttributes = true
        },
        [ImportFormat.WKT] = new FormatMetadata
        {
            Name = "WKT",
            Extensions = new[] { ".wkt", ".txt" },
            MimeTypes = new[] { "text/plain" },
            Description = "Well-Known Text geometry",
            HasGeometry = true,
            SupportsAttributes = false
        }
    };

    public static ImportFormat DetectFromExtension(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();

        foreach (var (format, metadata) in Formats)
        {
            if (metadata.Extensions.Contains(ext))
            {
                return format;
            }
        }

        return ImportFormat.Unknown;
    }
}

/// <summary>
/// Metadata about a file format
/// </summary>
public class FormatMetadata
{
    public required string Name { get; init; }
    public required string[] Extensions { get; init; }
    public required string[] MimeTypes { get; init; }
    public required string Description { get; init; }
    public bool HasGeometry { get; init; }
    public bool SupportsAttributes { get; init; }
}
