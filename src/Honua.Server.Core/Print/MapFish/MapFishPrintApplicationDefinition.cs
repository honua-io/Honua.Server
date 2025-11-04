// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Print.MapFish;

public sealed class MapFishPrintApplicationDefinition
{
    public string Id { get; set; } = "default";
    public string Title { get; set; } = "Honua Print";
    public string? Description { get; set; }
        = "Default MapFish-compatible print application provided by Honua.";
    public string DefaultLayout { get; set; } = "A4 Portrait";
    public string DefaultOutputFormat { get; set; } = "pdf";
    public int DefaultDpi { get; set; } = 150;
    public List<int> Dpis { get; set; } = new() { 96, 150, 300 };
    public List<string> OutputFormats { get; set; } = new() { "pdf" };
    public List<MapFishPrintLayoutDefinition> Layouts { get; set; } = new();
    public Dictionary<string, MapFishPrintAttributeDefinition> Attributes { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MapFishPrintLayoutDefinition
{
    public string Name { get; set; } = "A4 Portrait";
    public bool Default { get; set; }
        = true;
    public bool SupportsRotation { get; set; } = true;
    public MapFishPrintLayoutPageDefinition Page { get; set; } = MapFishPrintLayoutPageDefinition.A4Portrait();
    public MapFishPrintLayoutMapDefinition Map { get; set; } = MapFishPrintLayoutMapDefinition.Default();
    public MapFishPrintLayoutLegendDefinition Legend { get; set; } = MapFishPrintLayoutLegendDefinition.Disabled();
    public MapFishPrintLayoutTitleDefinition Title { get; set; } = MapFishPrintLayoutTitleDefinition.Default();
    public MapFishPrintLayoutScaleDefinition Scale { get; set; } = MapFishPrintLayoutScaleDefinition.Default();
}

public sealed class MapFishPrintLayoutPageDefinition
{
    public float WidthPoints { get; set; }
        = 595f; // A4 portrait width in points (8.27 inches * 72)
    public float HeightPoints { get; set; }
        = 842f; // A4 portrait height in points (11.69 inches * 72)
    public float MarginPoints { get; set; } = 36f; // 0.5" margin
    public string Size { get; set; } = "A4";
    public string Orientation { get; set; } = "portrait";

    public static MapFishPrintLayoutPageDefinition A4Portrait() => new();
    public static MapFishPrintLayoutPageDefinition A4Landscape() => new()
    {
        WidthPoints = 842f,
        HeightPoints = 595f,
        Orientation = "landscape"
    };
}

public sealed class MapFishPrintLayoutMapDefinition
{
    public int WidthPixels { get; set; } = 768;
    public int HeightPixels { get; set; } = 512;
    public float OffsetX { get; set; } = 36f;
    public float OffsetY { get; set; } = 120f;

    public static MapFishPrintLayoutMapDefinition Default() => new();
}

public sealed class MapFishPrintLayoutLegendDefinition
{
    public bool Enabled { get; set; } = true;
    public float OffsetX { get; set; } = 570f;
    public float OffsetY { get; set; } = 120f;
    public float Width { get; set; } = 180f;
    public float ItemHeight { get; set; } = 18f;
    public float SymbolSize { get; set; } = 12f;

    public static MapFishPrintLayoutLegendDefinition Disabled() => new() { Enabled = false };
}

public sealed class MapFishPrintLayoutTitleDefinition
{
    public float OffsetX { get; set; } = 36f;
    public float OffsetY { get; set; } = 54f;
    public float TitleFontSize { get; set; } = 20f;
    public float SubtitleFontSize { get; set; } = 12f;
    public float Spacing { get; set; } = 4f;

    public static MapFishPrintLayoutTitleDefinition Default() => new();
}

public sealed class MapFishPrintLayoutScaleDefinition
{
    public float OffsetX { get; set; } = 36f;
    public float OffsetY { get; set; } = 110f;
    public float FontSize { get; set; } = 10f;

    public static MapFishPrintLayoutScaleDefinition Default() => new();
}

public sealed class MapFishPrintAttributeDefinition
{
    public string Type { get; set; } = "String";
    public bool Required { get; set; }
        = false;
    public string? Description { get; set; }
        = null;
    public MapFishMapAttributeClientInfo? ClientInfo { get; set; }
        = null;
}

public sealed class MapFishMapAttributeClientInfo
{
    public List<int> Scales { get; set; } = new() { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000 };
    public List<int> DpiSuggestions { get; set; } = new() { 96, 150, 300 };
    public string Projection { get; set; } = "EPSG:3857";
    public bool Rotatable { get; set; } = true;
}
