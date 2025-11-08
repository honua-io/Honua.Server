using System.Text.Json.Serialization;

namespace HonuaField.Models.Symbology;

/// <summary>
/// Polygon symbology configuration
/// Defines how polygon features should be rendered
/// </summary>
public class PolygonSymbology
{
	/// <summary>
	/// Fill color (hex, rgb, or named color)
	/// </summary>
	[JsonPropertyName("fillColor")]
	public string FillColor { get; set; } = "#0066CC";

	/// <summary>
	/// Fill opacity (0.0 to 1.0)
	/// </summary>
	[JsonPropertyName("fillOpacity")]
	public double FillOpacity { get; set; } = 0.3;

	/// <summary>
	/// Stroke/outline color
	/// </summary>
	[JsonPropertyName("strokeColor")]
	public string StrokeColor { get; set; } = "#0066CC";

	/// <summary>
	/// Stroke width in pixels
	/// </summary>
	[JsonPropertyName("strokeWidth")]
	public double StrokeWidth { get; set; } = 2;

	/// <summary>
	/// Stroke opacity (0.0 to 1.0)
	/// </summary>
	[JsonPropertyName("strokeOpacity")]
	public double StrokeOpacity { get; set; } = 1.0;

	/// <summary>
	/// Stroke dash pattern (e.g., [5, 3] for 5px dash, 3px gap)
	/// Null or empty array = solid line
	/// </summary>
	[JsonPropertyName("strokeDashPattern")]
	public double[]? StrokeDashPattern { get; set; }

	/// <summary>
	/// Fill pattern type
	/// </summary>
	[JsonPropertyName("fillPattern")]
	public FillPattern FillPattern { get; set; } = FillPattern.Solid;

	/// <summary>
	/// Pattern color (for hatched/striped patterns)
	/// </summary>
	[JsonPropertyName("patternColor")]
	public string? PatternColor { get; set; }

	/// <summary>
	/// Z-index for layering (higher values render on top)
	/// </summary>
	[JsonPropertyName("zIndex")]
	public int ZIndex { get; set; } = 0;
}

/// <summary>
/// Fill pattern enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FillPattern
{
	/// <summary>
	/// Solid fill
	/// </summary>
	Solid,

	/// <summary>
	/// No fill (transparent)
	/// </summary>
	None,

	/// <summary>
	/// Horizontal lines
	/// </summary>
	HorizontalHatch,

	/// <summary>
	/// Vertical lines
	/// </summary>
	VerticalHatch,

	/// <summary>
	/// Diagonal lines (top-left to bottom-right)
	/// </summary>
	DiagonalHatch,

	/// <summary>
	/// Diagonal lines (top-right to bottom-left)
	/// </summary>
	BackwardDiagonalHatch,

	/// <summary>
	/// Crosshatch pattern
	/// </summary>
	CrossHatch,

	/// <summary>
	/// Diagonal crosshatch pattern
	/// </summary>
	DiagonalCrossHatch,

	/// <summary>
	/// Dot pattern
	/// </summary>
	Dots
}
