// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Import job creation request.
/// </summary>
public sealed class CreateImportJobRequest
{
    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("layerId")]
    public required string LayerId { get; set; }

    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; set; } = false;
}

/// <summary>
/// Import job snapshot/status.
/// </summary>
public sealed class ImportJobSnapshot
{
    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("serviceId")]
    public string? ServiceId { get; set; }

    [JsonPropertyName("layerId")]
    public string? LayerId { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("recordsProcessed")]
    public long RecordsProcessed { get; set; }

    [JsonPropertyName("recordsTotal")]
    public long RecordsTotal { get; set; }
}

/// <summary>
/// Paginated response for import jobs.
/// </summary>
public sealed class PaginatedImportJobs
{
    [JsonPropertyName("items")]
    public List<ImportJobSnapshot> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Import wizard state.
/// </summary>
public sealed class ImportWizardState
{
    public int CurrentStep { get; set; } = 0;
    public string? SelectedFile { get; set; }
    public long FileSize { get; set; }
    public string? TargetServiceId { get; set; }
    public string? TargetLayerId { get; set; }
    public string? TargetLayerName { get; set; }
    public bool CreateNewLayer { get; set; } = true;
    public Guid? JobId { get; set; }
}

/// <summary>
/// Supported file types for import.
/// </summary>
public static class SupportedFileTypes
{
    public static readonly Dictionary<string, string> Extensions = new()
    {
        { ".shp", "Shapefile (requires .shx, .dbf in ZIP)" },
        { ".geojson", "GeoJSON" },
        { ".json", "GeoJSON" },
        { ".gpkg", "GeoPackage" },
        { ".kml", "KML/KMZ" },
        { ".gml", "Geography Markup Language" },
        { ".csv", "CSV with geometry column" },
        { ".zip", "ZIP archive (Shapefile)" }
    };

    public static readonly string[] AllExtensions = Extensions.Keys.ToArray();

    public static string GetAcceptString() => string.Join(",", AllExtensions);
}
