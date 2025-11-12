// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration for map printing via MapFish Print
/// </summary>
public class PrintConfiguration
{
    /// <summary>
    /// Paper size for the print output
    /// </summary>
    [JsonPropertyName("paperSize")]
    public PaperSize PaperSize { get; set; } = PaperSize.A4;

    /// <summary>
    /// Page orientation
    /// </summary>
    [JsonPropertyName("orientation")]
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;

    /// <summary>
    /// Print layout/template name
    /// </summary>
    [JsonPropertyName("layout")]
    public string Layout { get; set; } = "default";

    /// <summary>
    /// Map title
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Map description/subtitle
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Author/creator name
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// Copyright/attribution text
    /// </summary>
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }

    /// <summary>
    /// Map scale (e.g., 25000 for 1:25,000)
    /// </summary>
    [JsonPropertyName("scale")]
    public int? Scale { get; set; }

    /// <summary>
    /// DPI/resolution for output
    /// </summary>
    [JsonPropertyName("dpi")]
    public int Dpi { get; set; } = 150;

    /// <summary>
    /// Output format
    /// </summary>
    [JsonPropertyName("format")]
    public PrintFormat Format { get; set; } = PrintFormat.Pdf;

    /// <summary>
    /// Include legend in output
    /// </summary>
    [JsonPropertyName("includeLegend")]
    public bool IncludeLegend { get; set; } = true;

    /// <summary>
    /// Include scale bar in output
    /// </summary>
    [JsonPropertyName("includeScaleBar")]
    public bool IncludeScaleBar { get; set; } = true;

    /// <summary>
    /// Include north arrow in output
    /// </summary>
    [JsonPropertyName("includeNorthArrow")]
    public bool IncludeNorthArrow { get; set; } = true;

    /// <summary>
    /// Include attribution/copyright in output
    /// </summary>
    [JsonPropertyName("includeAttribution")]
    public bool IncludeAttribution { get; set; } = true;

    /// <summary>
    /// Map extent mode
    /// </summary>
    [JsonPropertyName("extentMode")]
    public PrintExtentMode ExtentMode { get; set; } = PrintExtentMode.CurrentView;

    /// <summary>
    /// Custom extent (when ExtentMode = Custom)
    /// </summary>
    [JsonPropertyName("customExtent")]
    public double[]? CustomExtent { get; set; }

    /// <summary>
    /// Map center coordinates [longitude, latitude]
    /// </summary>
    [JsonPropertyName("center")]
    public double[]? Center { get; set; }

    /// <summary>
    /// Map zoom level
    /// </summary>
    [JsonPropertyName("zoom")]
    public double? Zoom { get; set; }

    /// <summary>
    /// Map bearing/rotation in degrees
    /// </summary>
    [JsonPropertyName("bearing")]
    public double Bearing { get; set; } = 0;

    /// <summary>
    /// Layers to include in print (null = all visible layers)
    /// </summary>
    [JsonPropertyName("layers")]
    public List<string>? Layers { get; set; }

    /// <summary>
    /// Additional custom attributes for template
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object>? Attributes { get; set; }
}

/// <summary>
/// Standard paper sizes
/// </summary>
public enum PaperSize
{
    A3,
    A4,
    A5,
    Letter,
    Legal,
    Tabloid,
    Custom
}

/// <summary>
/// Page orientation
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>
/// Print output format
/// </summary>
public enum PrintFormat
{
    Pdf,
    Png,
    Jpeg
}

/// <summary>
/// Map extent mode for printing
/// </summary>
public enum PrintExtentMode
{
    /// <summary>
    /// Use current map view
    /// </summary>
    CurrentView,

    /// <summary>
    /// Custom extent specified by user
    /// </summary>
    Custom,

    /// <summary>
    /// Fit to all visible features
    /// </summary>
    FitFeatures
}

/// <summary>
/// Print job status information
/// </summary>
public class PrintJobStatus
{
    /// <summary>
    /// Job identifier
    /// </summary>
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Current status
    /// </summary>
    [JsonPropertyName("status")]
    public PrintJobState Status { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Download URL when complete
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Elapsed time
    /// </summary>
    [JsonPropertyName("elapsedTime")]
    public TimeSpan? ElapsedTime { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Completed timestamp
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Print job state
/// </summary>
public enum PrintJobState
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// MapFish Print capabilities
/// </summary>
public class PrintCapabilities
{
    /// <summary>
    /// Available layouts
    /// </summary>
    [JsonPropertyName("layouts")]
    public List<PrintLayout> Layouts { get; set; } = new();

    /// <summary>
    /// Available formats
    /// </summary>
    [JsonPropertyName("formats")]
    public List<string> Formats { get; set; } = new();

    /// <summary>
    /// Supported projections
    /// </summary>
    [JsonPropertyName("projections")]
    public List<string> Projections { get; set; } = new();
}

/// <summary>
/// Print layout template
/// </summary>
public class PrintLayout
{
    /// <summary>
    /// Layout name/identifier
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display label
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Layout attributes
    /// </summary>
    [JsonPropertyName("attributes")]
    public List<PrintLayoutAttribute> Attributes { get; set; } = new();

    /// <summary>
    /// Map configuration
    /// </summary>
    [JsonPropertyName("map")]
    public PrintMapConfig? Map { get; set; }
}

/// <summary>
/// Print layout attribute
/// </summary>
public class PrintLayoutAttribute
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// Print map configuration
/// </summary>
public class PrintMapConfig
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("maxDPI")]
    public int MaxDpi { get; set; }
}
