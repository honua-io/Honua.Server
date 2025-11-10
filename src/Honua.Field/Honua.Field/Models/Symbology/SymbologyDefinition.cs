// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HonuaField.Models.Symbology;

/// <summary>
/// Root symbology definition for a feature collection
/// Defines how features should be rendered on the map
/// </summary>
public class SymbologyDefinition
{
	/// <summary>
	/// Type of renderer (simple, unique-value, graduated, etc.)
	/// </summary>
	[JsonPropertyName("type")]
	public RendererType Type { get; set; } = RendererType.Simple;

	/// <summary>
	/// Label for the symbology (used in legends)
	/// </summary>
	[JsonPropertyName("label")]
	public string? Label { get; set; }

	/// <summary>
	/// Field name to use for unique value or graduated rendering
	/// </summary>
	[JsonPropertyName("field")]
	public string? Field { get; set; }

	/// <summary>
	/// Point symbology (for Point and MultiPoint geometries)
	/// </summary>
	[JsonPropertyName("point")]
	public PointSymbology? PointSymbology { get; set; }

	/// <summary>
	/// Line symbology (for LineString and MultiLineString geometries)
	/// </summary>
	[JsonPropertyName("line")]
	public LineSymbology? LineSymbology { get; set; }

	/// <summary>
	/// Polygon symbology (for Polygon and MultiPolygon geometries)
	/// </summary>
	[JsonPropertyName("polygon")]
	public PolygonSymbology? PolygonSymbology { get; set; }

	/// <summary>
	/// Unique value renderer rules (when Type = UniqueValue)
	/// Maps field values to specific symbology
	/// </summary>
	[JsonPropertyName("uniqueValueInfos")]
	public List<UniqueValueInfo>? UniqueValueInfos { get; set; }

	/// <summary>
	/// Class break renderer rules (when Type = Graduated)
	/// Maps numeric ranges to specific symbology
	/// </summary>
	[JsonPropertyName("classBreakInfos")]
	public List<ClassBreakInfo>? ClassBreakInfos { get; set; }

	/// <summary>
	/// Default symbology when no rules match
	/// </summary>
	[JsonPropertyName("defaultSymbol")]
	public SymbologyDefinition? DefaultSymbol { get; set; }
}

/// <summary>
/// Renderer type enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RendererType
{
	/// <summary>
	/// Same style for all features
	/// </summary>
	Simple,

	/// <summary>
	/// Different styles based on unique field values
	/// </summary>
	UniqueValue,

	/// <summary>
	/// Different styles based on numeric ranges
	/// </summary>
	Graduated,

	/// <summary>
	/// Heat map renderer
	/// </summary>
	Heatmap
}

/// <summary>
/// Unique value info for unique value renderer
/// </summary>
public class UniqueValueInfo
{
	/// <summary>
	/// Field value to match
	/// </summary>
	[JsonPropertyName("value")]
	public object? Value { get; set; }

	/// <summary>
	/// Label for this value (used in legends)
	/// </summary>
	[JsonPropertyName("label")]
	public string? Label { get; set; }

	/// <summary>
	/// Point symbology for this value
	/// </summary>
	[JsonPropertyName("point")]
	public PointSymbology? PointSymbology { get; set; }

	/// <summary>
	/// Line symbology for this value
	/// </summary>
	[JsonPropertyName("line")]
	public LineSymbology? LineSymbology { get; set; }

	/// <summary>
	/// Polygon symbology for this value
	/// </summary>
	[JsonPropertyName("polygon")]
	public PolygonSymbology? PolygonSymbology { get; set; }
}

/// <summary>
/// Class break info for graduated renderer
/// </summary>
public class ClassBreakInfo
{
	/// <summary>
	/// Minimum value (inclusive)
	/// </summary>
	[JsonPropertyName("minValue")]
	public double MinValue { get; set; }

	/// <summary>
	/// Maximum value (exclusive)
	/// </summary>
	[JsonPropertyName("maxValue")]
	public double MaxValue { get; set; }

	/// <summary>
	/// Label for this range (used in legends)
	/// </summary>
	[JsonPropertyName("label")]
	public string? Label { get; set; }

	/// <summary>
	/// Point symbology for this range
	/// </summary>
	[JsonPropertyName("point")]
	public PointSymbology? PointSymbology { get; set; }

	/// <summary>
	/// Line symbology for this range
	/// </summary>
	[JsonPropertyName("line")]
	public LineSymbology? LineSymbology { get; set; }

	/// <summary>
	/// Polygon symbology for this range
	/// </summary>
	[JsonPropertyName("polygon")]
	public PolygonSymbology? PolygonSymbology { get; set; }
}
