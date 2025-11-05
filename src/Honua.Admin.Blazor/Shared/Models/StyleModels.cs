// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Complete style definition in mvp-style format.
/// </summary>
public sealed class StyleDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("layerId")]
    public string LayerId { get; set; } = string.Empty;

    [JsonPropertyName("geometryType")]
    public string GeometryType { get; set; } = "point"; // point, line, polygon, raster

    [JsonPropertyName("renderer")]
    public RendererBase Renderer { get; set; } = new SimpleRenderer();

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Base class for all renderer types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SimpleRenderer), "simple")]
[JsonDerivedType(typeof(UniqueValueRenderer), "uniqueValue")]
[JsonDerivedType(typeof(RuleBasedRenderer), "ruleBased")]
public abstract class RendererBase
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>
/// Simple renderer - single symbol for all features.
/// </summary>
public sealed class SimpleRenderer : RendererBase
{
    public override string Type => "simple";

    [JsonPropertyName("symbol")]
    public SymbolBase Symbol { get; set; } = new PointSymbol();
}

/// <summary>
/// Unique value renderer - different symbols based on attribute values.
/// </summary>
public sealed class UniqueValueRenderer : RendererBase
{
    public override string Type => "uniqueValue";

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("fieldLabel")]
    public string? FieldLabel { get; set; }

    [JsonPropertyName("uniqueValueInfos")]
    public List<UniqueValueInfo> UniqueValueInfos { get; set; } = new();

    [JsonPropertyName("defaultSymbol")]
    public SymbolBase? DefaultSymbol { get; set; }

    [JsonPropertyName("defaultLabel")]
    public string DefaultLabel { get; set; } = "Other";
}

/// <summary>
/// Unique value information for a specific value.
/// </summary>
public sealed class UniqueValueInfo
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public SymbolBase Symbol { get; set; } = new PointSymbol();
}

/// <summary>
/// Rule-based renderer - symbols based on scale ranges and filters.
/// </summary>
public sealed class RuleBasedRenderer : RendererBase
{
    public override string Type => "ruleBased";

    [JsonPropertyName("rules")]
    public List<RenderRule> Rules { get; set; } = new();

    [JsonPropertyName("defaultSymbol")]
    public SymbolBase? DefaultSymbol { get; set; }
}

/// <summary>
/// Render rule with scale range and optional filter.
/// </summary>
public sealed class RenderRule
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public SymbolBase Symbol { get; set; } = new PointSymbol();

    [JsonPropertyName("minScale")]
    public double? MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double? MaxScale { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; } // CQL filter expression
}

/// <summary>
/// Base class for all symbol types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PointSymbol), "point")]
[JsonDerivedType(typeof(LineSymbol), "line")]
[JsonDerivedType(typeof(PolygonSymbol), "polygon")]
[JsonDerivedType(typeof(RasterSymbol), "raster")]
public abstract class SymbolBase
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Point symbol (circle or custom shape).
/// </summary>
public sealed class PointSymbol : SymbolBase
{
    public override string Type => "point";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#3388ff"; // Hex color with optional alpha

    [JsonPropertyName("size")]
    public double Size { get; set; } = 8.0;

    [JsonPropertyName("outlineColor")]
    public string? OutlineColor { get; set; } = "#ffffff";

    [JsonPropertyName("outlineWidth")]
    public double OutlineWidth { get; set; } = 2.0;

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "circle"; // circle, square, triangle, star

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;
}

/// <summary>
/// Line symbol.
/// </summary>
public sealed class LineSymbol : SymbolBase
{
    public override string Type => "line";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#3388ff";

    [JsonPropertyName("width")]
    public double Width { get; set; } = 2.0;

    [JsonPropertyName("style")]
    public string Style { get; set; } = "solid"; // solid, dashed, dotted

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonPropertyName("cap")]
    public string Cap { get; set; } = "round"; // round, square, butt

    [JsonPropertyName("join")]
    public string Join { get; set; } = "round"; // round, bevel, miter
}

/// <summary>
/// Polygon symbol with fill and outline.
/// </summary>
public sealed class PolygonSymbol : SymbolBase
{
    public override string Type => "polygon";

    [JsonPropertyName("fillColor")]
    public string FillColor { get; set; } = "#3388ff";

    [JsonPropertyName("fillOpacity")]
    public double FillOpacity { get; set; } = 0.6;

    [JsonPropertyName("outlineColor")]
    public string OutlineColor { get; set; } = "#3388ff";

    [JsonPropertyName("outlineWidth")]
    public double OutlineWidth { get; set; } = 2.0;

    [JsonPropertyName("outlineStyle")]
    public string OutlineStyle { get; set; } = "solid"; // solid, dashed, dotted
}

/// <summary>
/// Raster symbol with color mapping.
/// </summary>
public sealed class RasterSymbol : SymbolBase
{
    public override string Type => "raster";

    [JsonPropertyName("colorMap")]
    public List<ColorMapEntry> ColorMap { get; set; } = new();

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;
}

/// <summary>
/// Color map entry for raster styling.
/// </summary>
public sealed class ColorMapEntry
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>
/// Style list item for display in tables.
/// </summary>
public sealed class StyleListItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("layerId")]
    public string LayerId { get; set; } = string.Empty;

    [JsonPropertyName("layerName")]
    public string LayerName { get; set; } = string.Empty;

    [JsonPropertyName("geometryType")]
    public string GeometryType { get; set; } = string.Empty;

    [JsonPropertyName("rendererType")]
    public string RendererType { get; set; } = string.Empty;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a new style.
/// </summary>
public sealed class CreateStyleRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("layerId")]
    public string LayerId { get; set; } = string.Empty;

    [JsonPropertyName("geometryType")]
    public string GeometryType { get; set; } = "point";

    [JsonPropertyName("renderer")]
    public RendererBase Renderer { get; set; } = new SimpleRenderer();

    [JsonPropertyName("setAsDefault")]
    public bool SetAsDefault { get; set; }
}

/// <summary>
/// Request to update an existing style.
/// </summary>
public sealed class UpdateStyleRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("renderer")]
    public RendererBase? Renderer { get; set; }

    [JsonPropertyName("setAsDefault")]
    public bool? SetAsDefault { get; set; }
}

/// <summary>
/// Preset color for quick selection.
/// </summary>
public sealed class PresetColor
{
    public string Name { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Style template for quick creation.
/// </summary>
public sealed class StyleTemplate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("geometryType")]
    public string GeometryType { get; set; } = string.Empty;

    [JsonPropertyName("renderer")]
    public RendererBase Renderer { get; set; } = new SimpleRenderer();

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";
}
