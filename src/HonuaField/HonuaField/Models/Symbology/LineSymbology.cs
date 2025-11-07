using System.Text.Json.Serialization;

namespace HonuaField.Models.Symbology;

/// <summary>
/// Line symbology configuration
/// Defines how line features should be rendered
/// </summary>
public class LineSymbology
{
	/// <summary>
	/// Line color (hex, rgb, or named color)
	/// </summary>
	[JsonPropertyName("color")]
	public string Color { get; set; } = "#0066CC";

	/// <summary>
	/// Line width in pixels
	/// </summary>
	[JsonPropertyName("width")]
	public double Width { get; set; } = 2;

	/// <summary>
	/// Line opacity (0.0 to 1.0)
	/// </summary>
	[JsonPropertyName("opacity")]
	public double Opacity { get; set; } = 1.0;

	/// <summary>
	/// Line cap style (butt, round, square)
	/// </summary>
	[JsonPropertyName("cap")]
	public LineCapStyle Cap { get; set; } = LineCapStyle.Round;

	/// <summary>
	/// Line join style (miter, round, bevel)
	/// </summary>
	[JsonPropertyName("join")]
	public LineJoinStyle Join { get; set; } = LineJoinStyle.Round;

	/// <summary>
	/// Dash pattern (e.g., [5, 3] for 5px dash, 3px gap)
	/// Null or empty array = solid line
	/// </summary>
	[JsonPropertyName("dashPattern")]
	public double[]? DashPattern { get; set; }

	/// <summary>
	/// Arrow style for line endings
	/// </summary>
	[JsonPropertyName("arrowStyle")]
	public ArrowStyle ArrowStyle { get; set; } = ArrowStyle.None;

	/// <summary>
	/// Z-index for layering (higher values render on top)
	/// </summary>
	[JsonPropertyName("zIndex")]
	public int ZIndex { get; set; } = 0;
}

/// <summary>
/// Line cap style enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineCapStyle
{
	/// <summary>
	/// Flat cap at line end
	/// </summary>
	Butt,

	/// <summary>
	/// Rounded cap at line end
	/// </summary>
	Round,

	/// <summary>
	/// Square cap extending beyond line end
	/// </summary>
	Square
}

/// <summary>
/// Line join style enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineJoinStyle
{
	/// <summary>
	/// Sharp angled join
	/// </summary>
	Miter,

	/// <summary>
	/// Rounded join
	/// </summary>
	Round,

	/// <summary>
	/// Beveled join
	/// </summary>
	Bevel
}

/// <summary>
/// Arrow style enumeration for line endings
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArrowStyle
{
	/// <summary>
	/// No arrows
	/// </summary>
	None,

	/// <summary>
	/// Arrow at end of line
	/// </summary>
	End,

	/// <summary>
	/// Arrow at start of line
	/// </summary>
	Start,

	/// <summary>
	/// Arrows at both ends
	/// </summary>
	Both
}
