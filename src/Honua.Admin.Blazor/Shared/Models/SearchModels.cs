// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Search filter criteria for services and layers.
/// </summary>
public sealed class SearchFilter
{
    [JsonPropertyName("searchText")]
    public string? SearchText { get; set; }

    [JsonPropertyName("serviceTypes")]
    public List<string> ServiceTypes { get; set; } = new();

    [JsonPropertyName("geometryTypes")]
    public List<string> GeometryTypes { get; set; } = new();

    [JsonPropertyName("crs")]
    public List<string> Crs { get; set; } = new();

    [JsonPropertyName("folderIds")]
    public List<string> FolderIds { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("hasLayers")]
    public bool? HasLayers { get; set; }

    /// <summary>
    /// Checks if any filters are active.
    /// </summary>
    public bool IsActive()
    {
        return !string.IsNullOrWhiteSpace(SearchText)
            || ServiceTypes.Any()
            || GeometryTypes.Any()
            || Crs.Any()
            || FolderIds.Any()
            || Keywords.Any()
            || HasLayers.HasValue;
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public void Clear()
    {
        SearchText = null;
        ServiceTypes.Clear();
        GeometryTypes.Clear();
        Crs.Clear();
        FolderIds.Clear();
        Keywords.Clear();
        HasLayers = null;
    }

    /// <summary>
    /// Creates a deep copy of the filter.
    /// </summary>
    public SearchFilter Clone()
    {
        return new SearchFilter
        {
            SearchText = SearchText,
            ServiceTypes = new List<string>(ServiceTypes),
            GeometryTypes = new List<string>(GeometryTypes),
            Crs = new List<string>(Crs),
            FolderIds = new List<string>(FolderIds),
            Keywords = new List<string>(Keywords),
            HasLayers = HasLayers
        };
    }
}

/// <summary>
/// Saved filter preset.
/// </summary>
public sealed class FilterPreset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("filter")]
    public required SearchFilter Filter { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Search history entry.
/// </summary>
public sealed class SearchHistoryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("searchText")]
    public required string SearchText { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
}

/// <summary>
/// Global search result.
/// </summary>
public sealed class GlobalSearchResult
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }  // "Service", "Layer", "Folder"

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("parentTitle")]
    public string? ParentTitle { get; set; }

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Filter operator for column-specific filters.
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Between,
    In,
    NotIn,
    IsNull,
    IsNotNull
}

/// <summary>
/// Column-specific filter configuration.
/// </summary>
public sealed class ColumnFilter
{
    [JsonPropertyName("column")]
    public string Column { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("secondValue")]
    public string? SecondValue { get; set; } // For range filters (Between)

    [JsonPropertyName("values")]
    public List<string> Values { get; set; } = new(); // For In/NotIn operators
}

/// <summary>
/// Advanced filter preset for column-specific filtering.
/// </summary>
public sealed class AdvancedFilterPreset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("filters")]
    public List<ColumnFilter> Filters { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("tableType")]
    public string TableType { get; set; } = string.Empty; // "services", "users", "workflows", "layers"
}

/// <summary>
/// Predefined filter options.
/// </summary>
public static class FilterOptions
{
    public static readonly List<string> ServiceTypes = new()
    {
        "WMS",
        "WFS",
        "WMTS",
        "OGC"
    };

    public static readonly List<string> GeometryTypes = new()
    {
        "Point",
        "LineString",
        "Polygon",
        "MultiPoint",
        "MultiLineString",
        "MultiPolygon",
        "GeometryCollection"
    };

    public static readonly List<string> CommonCrs = new()
    {
        "EPSG:4326",  // WGS 84
        "EPSG:3857",  // Web Mercator
        "EPSG:4269",  // NAD83
        "EPSG:3395",  // World Mercator
        "EPSG:32633", // WGS 84 / UTM zone 33N
        "EPSG:32634", // WGS 84 / UTM zone 34N
        "EPSG:2193"   // NZGD2000
    };

    /// <summary>
    /// Gets display name for filter operator.
    /// </summary>
    public static string GetOperatorDisplayName(FilterOperator op) => op switch
    {
        FilterOperator.Equals => "Equals",
        FilterOperator.NotEquals => "Not Equals",
        FilterOperator.Contains => "Contains",
        FilterOperator.NotContains => "Does Not Contain",
        FilterOperator.StartsWith => "Starts With",
        FilterOperator.EndsWith => "Ends With",
        FilterOperator.GreaterThan => "Greater Than",
        FilterOperator.LessThan => "Less Than",
        FilterOperator.GreaterThanOrEqual => "Greater Than or Equal",
        FilterOperator.LessThanOrEqual => "Less Than or Equal",
        FilterOperator.Between => "Between",
        FilterOperator.In => "In List",
        FilterOperator.NotIn => "Not In List",
        FilterOperator.IsNull => "Is Empty",
        FilterOperator.IsNotNull => "Is Not Empty",
        _ => op.ToString()
    };
}
