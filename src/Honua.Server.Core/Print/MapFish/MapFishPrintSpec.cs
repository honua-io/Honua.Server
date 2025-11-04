// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Print.MapFish;

public sealed class MapFishPrintSpec
{
    [JsonPropertyName("layout")]
    public string? Layout { get; set; }
        = null;

    [JsonPropertyName("outputFormat")]
    public string? OutputFormat { get; set; }
        = null;

    [JsonPropertyName("attributes")]
    public MapFishPrintSpecAttributes Attributes { get; set; } = new();
}

public sealed class MapFishPrintSpecAttributes
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
        = null;

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }
        = null;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
        = null;

    [JsonPropertyName("map")]
    public MapFishPrintMapSpec? Map { get; set; }
        = null;

    [JsonPropertyName("legend")]
    public MapFishPrintLegendSpec? Legend { get; set; }
        = null;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string?> Metadata { get; set; }
        = new();
}

public sealed class MapFishPrintMapSpec
{
    [JsonPropertyName("bbox")]
    public double[]? BoundingBox { get; set; }
        = null;

    [JsonPropertyName("center")]
    public double[]? Center { get; set; }
        = null;

    [JsonPropertyName("scale")]
    public double? Scale { get; set; }
        = null;

    [JsonPropertyName("dpi")]
    public int? Dpi { get; set; }
        = null;

    [JsonPropertyName("projection")]
    public string? Projection { get; set; }
        = null;

    [JsonPropertyName("rotation")]
    public double? Rotation { get; set; }
        = 0d;

    [JsonPropertyName("layers")]
    public List<MapFishPrintLayerSpec> Layers { get; set; }
        = new();
}

public sealed class MapFishPrintLayerSpec
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "wms";

    [JsonPropertyName("baseURL")]
    public string? BaseUrl { get; set; }
        = null;

    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = new();

    [JsonPropertyName("styles")]
    public List<string>? Styles { get; set; }
        = null;

    [JsonPropertyName("format")]
    public string? Format { get; set; }
        = null;

    [JsonPropertyName("imageFormat")]
    public string? ImageFormat { get; set; }
        = null;

    [JsonPropertyName("opacity")]
    public double? Opacity { get; set; }
        = null;

    [JsonPropertyName("transparent")]
    public bool? Transparent { get; set; }
        = null;

    [JsonPropertyName("customParams")]
    public Dictionary<string, string>? CustomParams { get; set; }
        = null;
}

public sealed class MapFishPrintLegendSpec
{
    [JsonPropertyName("items")]
    public List<MapFishPrintLegendItem> Items { get; set; }
        = new();
}

public sealed class MapFishPrintLegendItem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
        = null;

    [JsonPropertyName("classes")]
    public List<MapFishPrintLegendClass> Classes { get; set; }
        = new();
}

public sealed class MapFishPrintLegendClass
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
        = null;

    [JsonPropertyName("icons")]
    public List<string> Icons { get; set; }
        = new();
}
