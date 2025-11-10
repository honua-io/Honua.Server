// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HonuaField.Models.Symbology;

/// <summary>
/// Point symbology configuration
/// Defines how point features should be rendered
/// </summary>
public class PointSymbology
{
	/// <summary>
	/// Symbol type (circle, square, triangle, star, pin, diamond, custom)
	/// </summary>
	[JsonPropertyName("type")]
	public PointSymbolType Type { get; set; } = PointSymbolType.Circle;

	/// <summary>
	/// Fill color (hex, rgb, or named color)
	/// </summary>
	[JsonPropertyName("color")]
	public string Color { get; set; } = "#0066CC";

	/// <summary>
	/// Outline color
	/// </summary>
	[JsonPropertyName("outlineColor")]
	public string? OutlineColor { get; set; } = "#FFFFFF";

	/// <summary>
	/// Outline width in pixels
	/// </summary>
	[JsonPropertyName("outlineWidth")]
	public double OutlineWidth { get; set; } = 2;

	/// <summary>
	/// Symbol size in pixels (diameter for circles, side length for squares)
	/// </summary>
	[JsonPropertyName("size")]
	public double Size { get; set; } = 12;

	/// <summary>
	/// Symbol opacity (0.0 to 1.0)
	/// </summary>
	[JsonPropertyName("opacity")]
	public double Opacity { get; set; } = 1.0;

	/// <summary>
	/// Custom icon URL or path (when Type = Custom)
	/// </summary>
	[JsonPropertyName("icon")]
	public string? Icon { get; set; }

	/// <summary>
	/// Icon anchor point (x, y as fraction of icon size, 0.5 = center)
	/// </summary>
	[JsonPropertyName("iconAnchor")]
	public (double x, double y) IconAnchor { get; set; } = (0.5, 0.5);

	/// <summary>
	/// Rotation angle in degrees (0-360)
	/// </summary>
	[JsonPropertyName("rotation")]
	public double Rotation { get; set; } = 0;

	/// <summary>
	/// Z-index for layering (higher values render on top)
	/// </summary>
	[JsonPropertyName("zIndex")]
	public int ZIndex { get; set; } = 0;
}

/// <summary>
/// Point symbol type enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PointSymbolType
{
	/// <summary>
	/// Circle marker
	/// </summary>
	Circle,

	/// <summary>
	/// Square marker
	/// </summary>
	Square,

	/// <summary>
	/// Triangle marker
	/// </summary>
	Triangle,

	/// <summary>
	/// Star marker
	/// </summary>
	Star,

	/// <summary>
	/// Map pin marker
	/// </summary>
	Pin,

	/// <summary>
	/// Diamond marker
	/// </summary>
	Diamond,

	/// <summary>
	/// Cross marker
	/// </summary>
	Cross,

	/// <summary>
	/// X marker
	/// </summary>
	X,

	/// <summary>
	/// Custom icon/image
	/// </summary>
	Custom
}
