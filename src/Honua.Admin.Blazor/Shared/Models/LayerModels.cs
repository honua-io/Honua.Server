// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to create a new layer.
/// </summary>
public sealed class CreateLayerRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("geometryType")]
    public required string GeometryType { get; set; }

    [JsonPropertyName("idField")]
    public required string IdField { get; set; }

    [JsonPropertyName("geometryField")]
    public required string GeometryField { get; set; }

    [JsonPropertyName("crs")]
    public List<string> Crs { get; set; } = new() { "EPSG:4326" };

    [JsonPropertyName("displayField")]
    public string? DisplayField { get; set; }
}

/// <summary>
/// Request to update an existing layer.
/// </summary>
public sealed class UpdateLayerRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("geometryType")]
    public required string GeometryType { get; set; }

    [JsonPropertyName("idField")]
    public required string IdField { get; set; }

    [JsonPropertyName("geometryField")]
    public required string GeometryField { get; set; }

    [JsonPropertyName("crs")]
    public List<string> Crs { get; set; } = new() { "EPSG:4326" };

    [JsonPropertyName("displayField")]
    public string? DisplayField { get; set; }
}

/// <summary>
/// Layer response model.
/// </summary>
public sealed class LayerResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("geometryType")]
    public required string GeometryType { get; set; }

    [JsonPropertyName("idField")]
    public required string IdField { get; set; }

    [JsonPropertyName("geometryField")]
    public required string GeometryField { get; set; }

    [JsonPropertyName("crs")]
    public List<string> Crs { get; set; } = new();

    [JsonPropertyName("displayField")]
    public string? DisplayField { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Layer list item (lightweight).
/// </summary>
public sealed class LayerListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("geometryType")]
    public required string GeometryType { get; set; }

    [JsonPropertyName("crs")]
    public List<string>? Crs { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Common CRS (Coordinate Reference System) definitions.
/// </summary>
public static class CommonCrs
{
    public static readonly List<(string Code, string Name)> WellKnownCrs = new()
    {
        ("EPSG:4326", "WGS 84 (GPS coordinates)"),
        ("EPSG:3857", "Web Mercator (Google Maps, OpenStreetMap)"),
        ("EPSG:4269", "NAD83 (North America)"),
        ("EPSG:2154", "Lambert 93 (France)"),
        ("EPSG:27700", "OSGB 1936 / British National Grid"),
        ("EPSG:32633", "WGS 84 / UTM zone 33N"),
        ("EPSG:32634", "WGS 84 / UTM zone 34N"),
        ("EPSG:32635", "WGS 84 / UTM zone 35N"),
        ("EPSG:3395", "World Mercator"),
        ("EPSG:4258", "ETRS89 (Europe)"),
    };
}

/// <summary>
/// Common geometry types.
/// </summary>
public static class GeometryTypes
{
    public const string Point = "POINT";
    public const string LineString = "LINESTRING";
    public const string Polygon = "POLYGON";
    public const string MultiPoint = "MULTIPOINT";
    public const string MultiLineString = "MULTILINESTRING";
    public const string MultiPolygon = "MULTIPOLYGON";
    public const string GeometryCollection = "GEOMETRYCOLLECTION";

    public static readonly List<(string Type, string Icon)> AllTypes = new()
    {
        (Point, "place"),
        (LineString, "timeline"),
        (Polygon, "category"),
        (MultiPoint, "scatter_plot"),
        (MultiLineString, "route"),
        (MultiPolygon, "dataset"),
        (GeometryCollection, "layers"),
    };

    public static string GetDisplayName(string type) => type switch
    {
        Point => "Point",
        LineString => "Line String",
        Polygon => "Polygon",
        MultiPoint => "Multi-Point",
        MultiLineString => "Multi-Line String",
        MultiPolygon => "Multi-Polygon",
        GeometryCollection => "Geometry Collection",
        _ => type
    };
}
