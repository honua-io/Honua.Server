// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using HonuaField.Models.Symbology;
using Mapsui.Styles;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HonuaField.Services;

/// <summary>
/// Implementation of symbology service for parsing and rendering feature symbology
/// Includes caching for performance optimization
/// </summary>
public class SymbologyService : ISymbologyService
{
	// Cache for parsed symbology definitions
	private readonly ConcurrentDictionary<string, SymbologyDefinition?> _symbologyCache = new();

	// Cache for generated styles
	private readonly ConcurrentDictionary<string, IStyle> _styleCache = new();

	// Named color mappings
	private static readonly Dictionary<string, Mapsui.Styles.Color> NamedColors = new()
	{
		{ "red", Mapsui.Styles.Color.Red },
		{ "blue", Mapsui.Styles.Color.Blue },
		{ "green", Mapsui.Styles.Color.Green },
		{ "yellow", Mapsui.Styles.Color.Yellow },
		{ "orange", Mapsui.Styles.Color.Orange },
		{ "purple", Mapsui.Styles.Color.Purple },
		{ "cyan", Mapsui.Styles.Color.Cyan },
		{ "magenta", Mapsui.Styles.Color.Magenta },
		{ "white", Mapsui.Styles.Color.White },
		{ "black", Mapsui.Styles.Color.Black },
		{ "gray", Mapsui.Styles.Color.Gray },
		{ "brown", new Mapsui.Styles.Color(165, 42, 42) },
		{ "pink", new Mapsui.Styles.Color(255, 192, 203) }
	};

	/// <inheritdoc />
	public SymbologyDefinition? ParseSymbology(string? symbologyJson)
	{
		if (string.IsNullOrWhiteSpace(symbologyJson))
			return null;

		// Check cache first
		if (_symbologyCache.TryGetValue(symbologyJson, out var cached))
			return cached;

		try
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				AllowTrailingCommas = true
			};

			var symbology = JsonSerializer.Deserialize<SymbologyDefinition>(symbologyJson, options);

			// Cache the result
			_symbologyCache[symbologyJson] = symbology;

			return symbology;
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to parse symbology JSON: {ex.Message}");
			return null;
		}
	}

	/// <inheritdoc />
	public IStyle GetStyleForFeature(Feature feature, SymbologyDefinition symbology, string geometryType)
	{
		if (symbology == null)
			return GetDefaultStyle(geometryType);

		try
		{
			switch (symbology.Type)
			{
				case RendererType.Simple:
					return GetSimpleStyle(symbology, geometryType);

				case RendererType.UniqueValue:
					return GetUniqueValueStyle(feature, symbology, geometryType);

				case RendererType.Graduated:
					return GetGraduatedStyle(feature, symbology, geometryType);

				default:
					return GetSimpleStyle(symbology, geometryType);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error generating style for feature: {ex.Message}");
			return GetDefaultStyle(geometryType);
		}
	}

	/// <summary>
	/// Gets style for simple renderer (same style for all features)
	/// </summary>
	private IStyle GetSimpleStyle(SymbologyDefinition symbology, string geometryType)
	{
		return geometryType switch
		{
			"Point" or "MultiPoint" when symbology.PointSymbology != null
				=> CreatePointStyle(symbology.PointSymbology),
			"LineString" or "MultiLineString" when symbology.LineSymbology != null
				=> CreateLineStyle(symbology.LineSymbology),
			"Polygon" or "MultiPolygon" when symbology.PolygonSymbology != null
				=> CreatePolygonStyle(symbology.PolygonSymbology),
			_ => GetDefaultStyle(geometryType)
		};
	}

	/// <summary>
	/// Gets style for unique value renderer (different styles based on field value)
	/// </summary>
	private IStyle GetUniqueValueStyle(Feature feature, SymbologyDefinition symbology, string geometryType)
	{
		if (string.IsNullOrEmpty(symbology.Field) || symbology.UniqueValueInfos == null)
			return GetSimpleStyle(symbology, geometryType);

		// Get the field value from feature properties
		var properties = feature.GetPropertiesDict();
		if (!properties.TryGetValue(symbology.Field, out var fieldValue))
			return GetDefaultSymbolStyle(symbology, geometryType);

		// Find matching unique value info
		var matchingInfo = symbology.UniqueValueInfos
			.FirstOrDefault(info => MatchesValue(info.Value, fieldValue));

		if (matchingInfo == null)
			return GetDefaultSymbolStyle(symbology, geometryType);

		// Return style based on geometry type
		return geometryType switch
		{
			"Point" or "MultiPoint" when matchingInfo.PointSymbology != null
				=> CreatePointStyle(matchingInfo.PointSymbology),
			"LineString" or "MultiLineString" when matchingInfo.LineSymbology != null
				=> CreateLineStyle(matchingInfo.LineSymbology),
			"Polygon" or "MultiPolygon" when matchingInfo.PolygonSymbology != null
				=> CreatePolygonStyle(matchingInfo.PolygonSymbology),
			_ => GetDefaultSymbolStyle(symbology, geometryType)
		};
	}

	/// <summary>
	/// Gets style for graduated renderer (different styles based on numeric ranges)
	/// </summary>
	private IStyle GetGraduatedStyle(Feature feature, SymbologyDefinition symbology, string geometryType)
	{
		if (string.IsNullOrEmpty(symbology.Field) || symbology.ClassBreakInfos == null)
			return GetSimpleStyle(symbology, geometryType);

		// Get the field value from feature properties
		var properties = feature.GetPropertiesDict();
		if (!properties.TryGetValue(symbology.Field, out var fieldValue))
			return GetDefaultSymbolStyle(symbology, geometryType);

		// Convert to numeric value
		if (!TryConvertToDouble(fieldValue, out var numericValue))
			return GetDefaultSymbolStyle(symbology, geometryType);

		// Find matching class break
		var matchingBreak = symbology.ClassBreakInfos
			.FirstOrDefault(cb => numericValue >= cb.MinValue && numericValue < cb.MaxValue);

		if (matchingBreak == null)
			return GetDefaultSymbolStyle(symbology, geometryType);

		// Return style based on geometry type
		return geometryType switch
		{
			"Point" or "MultiPoint" when matchingBreak.PointSymbology != null
				=> CreatePointStyle(matchingBreak.PointSymbology),
			"LineString" or "MultiLineString" when matchingBreak.LineSymbology != null
				=> CreateLineStyle(matchingBreak.LineSymbology),
			"Polygon" or "MultiPolygon" when matchingBreak.PolygonSymbology != null
				=> CreatePolygonStyle(matchingBreak.PolygonSymbology),
			_ => GetDefaultSymbolStyle(symbology, geometryType)
		};
	}

	/// <summary>
	/// Gets the default symbol style from symbology definition
	/// </summary>
	private IStyle GetDefaultSymbolStyle(SymbologyDefinition symbology, string geometryType)
	{
		if (symbology.DefaultSymbol != null)
			return GetSimpleStyle(symbology.DefaultSymbol, geometryType);

		return GetSimpleStyle(symbology, geometryType);
	}

	/// <inheritdoc />
	public SymbolStyle CreatePointStyle(PointSymbology pointSymbology)
	{
		var cacheKey = $"point:{JsonSerializer.Serialize(pointSymbology)}";
		if (_styleCache.TryGetValue(cacheKey, out var cached))
			return (SymbolStyle)cached;

		var color = ParseColor(pointSymbology.Color);
		var outlineColor = ParseColor(pointSymbology.OutlineColor, Mapsui.Styles.Color.White);

		// Apply opacity to colors
		if (pointSymbology.Opacity < 1.0)
		{
			var alpha = (int)(pointSymbology.Opacity * 255);
			color = new Mapsui.Styles.Color(color.R, color.G, color.B, alpha);
		}

		var style = new SymbolStyle
		{
			SymbolScale = pointSymbology.Size / 16.0, // Normalize to default size
			Fill = new Brush(color),
			Outline = pointSymbology.OutlineWidth > 0
				? new Pen(outlineColor, pointSymbology.OutlineWidth)
				: null,
			SymbolRotation = pointSymbology.Rotation
		};

		// TODO: Handle different point symbol types (circle, square, triangle, etc.)
		// For now, Mapsui uses default circular symbols

		_styleCache[cacheKey] = style;
		return style;
	}

	/// <inheritdoc />
	public VectorStyle CreateLineStyle(LineSymbology lineSymbology)
	{
		var cacheKey = $"line:{JsonSerializer.Serialize(lineSymbology)}";
		if (_styleCache.TryGetValue(cacheKey, out var cached))
			return (VectorStyle)cached;

		var color = ParseColor(lineSymbology.Color);

		// Apply opacity
		if (lineSymbology.Opacity < 1.0)
		{
			var alpha = (int)(lineSymbology.Opacity * 255);
			color = new Mapsui.Styles.Color(color.R, color.G, color.B, alpha);
		}

		var pen = new Pen(color, lineSymbology.Width);

		// Apply dash pattern
		if (lineSymbology.DashPattern != null && lineSymbology.DashPattern.Length > 0)
		{
			pen.PenStyle = PenStyle.DashDot; // Simplified; Mapsui has limited dash pattern support
		}

		// Apply pen cap and join styles
		pen.PenStrokeCap = lineSymbology.Cap switch
		{
			LineCapStyle.Butt => PenStrokeCap.Butt,
			LineCapStyle.Round => PenStrokeCap.Round,
			LineCapStyle.Square => PenStrokeCap.Square,
			_ => PenStrokeCap.Round
		};

		var style = new VectorStyle
		{
			Line = pen
		};

		_styleCache[cacheKey] = style;
		return style;
	}

	/// <inheritdoc />
	public VectorStyle CreatePolygonStyle(PolygonSymbology polygonSymbology)
	{
		var cacheKey = $"polygon:{JsonSerializer.Serialize(polygonSymbology)}";
		if (_styleCache.TryGetValue(cacheKey, out var cached))
			return (VectorStyle)cached;

		var fillColor = ParseColor(polygonSymbology.FillColor);
		var strokeColor = ParseColor(polygonSymbology.StrokeColor);

		// Apply fill opacity
		if (polygonSymbology.FillOpacity < 1.0)
		{
			var alpha = (int)(polygonSymbology.FillOpacity * 255);
			fillColor = new Mapsui.Styles.Color(fillColor.R, fillColor.G, fillColor.B, alpha);
		}

		// Apply stroke opacity
		if (polygonSymbology.StrokeOpacity < 1.0)
		{
			var alpha = (int)(polygonSymbology.StrokeOpacity * 255);
			strokeColor = new Mapsui.Styles.Color(strokeColor.R, strokeColor.G, strokeColor.B, alpha);
		}

		var fill = polygonSymbology.FillPattern == FillPattern.None
			? null
			: new Brush(fillColor);

		var outline = polygonSymbology.StrokeWidth > 0
			? new Pen(strokeColor, polygonSymbology.StrokeWidth)
			: null;

		// Apply stroke dash pattern
		if (outline != null && polygonSymbology.StrokeDashPattern != null && polygonSymbology.StrokeDashPattern.Length > 0)
		{
			outline.PenStyle = PenStyle.DashDot;
		}

		var style = new VectorStyle
		{
			Fill = fill,
			Outline = outline
		};

		_styleCache[cacheKey] = style;
		return style;
	}

	/// <inheritdoc />
	public Mapsui.Styles.Color ParseColor(string? colorString, Mapsui.Styles.Color? defaultColor = null)
	{
		if (string.IsNullOrWhiteSpace(colorString))
			return defaultColor ?? Mapsui.Styles.Color.Blue;

		colorString = colorString.Trim().ToLowerInvariant();

		// Try named colors first
		if (NamedColors.TryGetValue(colorString, out var namedColor))
			return namedColor;

		// Try hex color (#RGB or #RRGGBB or #RRGGBBAA)
		if (colorString.StartsWith("#"))
		{
			var hex = colorString[1..];

			try
			{
				if (hex.Length == 3) // #RGB
				{
					var r = Convert.ToByte(hex.Substring(0, 1) + hex.Substring(0, 1), 16);
					var g = Convert.ToByte(hex.Substring(1, 1) + hex.Substring(1, 1), 16);
					var b = Convert.ToByte(hex.Substring(2, 1) + hex.Substring(2, 1), 16);
					return new Mapsui.Styles.Color(r, g, b);
				}
				else if (hex.Length == 6) // #RRGGBB
				{
					var r = Convert.ToByte(hex.Substring(0, 2), 16);
					var g = Convert.ToByte(hex.Substring(2, 2), 16);
					var b = Convert.ToByte(hex.Substring(4, 2), 16);
					return new Mapsui.Styles.Color(r, g, b);
				}
				else if (hex.Length == 8) // #RRGGBBAA
				{
					var r = Convert.ToByte(hex.Substring(0, 2), 16);
					var g = Convert.ToByte(hex.Substring(2, 2), 16);
					var b = Convert.ToByte(hex.Substring(4, 2), 16);
					var a = Convert.ToByte(hex.Substring(6, 2), 16);
					return Mapsui.Styles.Color.FromArgb(a, r, g, b);
				}
			}
			catch
			{
				// Fall through to default
			}
		}

		// Try rgb/rgba format: rgb(255, 0, 0) or rgba(255, 0, 0, 0.5)
		var rgbMatch = Regex.Match(colorString, @"rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)");
		if (rgbMatch.Success)
		{
			try
			{
				var r = int.Parse(rgbMatch.Groups[1].Value);
				var g = int.Parse(rgbMatch.Groups[2].Value);
				var b = int.Parse(rgbMatch.Groups[3].Value);
				var a = rgbMatch.Groups[4].Success ? double.Parse(rgbMatch.Groups[4].Value) : 1.0;

				return Mapsui.Styles.Color.FromArgb((int)(a * 255), r, g, b);
			}
			catch
			{
				// Fall through to default
			}
		}

		return defaultColor ?? Mapsui.Styles.Color.Blue;
	}

	/// <inheritdoc />
	public SymbologyDefinition CreateDefaultSymbology(string geometryType)
	{
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Simple,
			Label = $"Default {geometryType}"
		};

		switch (geometryType)
		{
			case "Point":
			case "MultiPoint":
				symbology.PointSymbology = new PointSymbology
				{
					Type = PointSymbolType.Circle,
					Color = "#0066CC",
					OutlineColor = "#FFFFFF",
					OutlineWidth = 2,
					Size = 12,
					Opacity = 1.0
				};
				break;

			case "LineString":
			case "MultiLineString":
				symbology.LineSymbology = new LineSymbology
				{
					Color = "#0066CC",
					Width = 2,
					Opacity = 1.0,
					Cap = LineCapStyle.Round,
					Join = LineJoinStyle.Round
				};
				break;

			case "Polygon":
			case "MultiPolygon":
				symbology.PolygonSymbology = new PolygonSymbology
				{
					FillColor = "#0066CC",
					FillOpacity = 0.3,
					StrokeColor = "#0066CC",
					StrokeWidth = 2,
					StrokeOpacity = 1.0,
					FillPattern = FillPattern.Solid
				};
				break;
		}

		return symbology;
	}

	/// <inheritdoc />
	public List<LegendItem> GenerateLegend(SymbologyDefinition symbology, string geometryType)
	{
		var items = new List<LegendItem>();

		if (symbology == null)
			return items;

		switch (symbology.Type)
		{
			case RendererType.Simple:
				items.Add(new LegendItem
				{
					Label = symbology.Label ?? "All Features",
					Style = GetSimpleStyle(symbology, geometryType)
				});
				break;

			case RendererType.UniqueValue:
				if (symbology.UniqueValueInfos != null)
				{
					foreach (var info in symbology.UniqueValueInfos)
					{
						var style = geometryType switch
						{
							"Point" or "MultiPoint" when info.PointSymbology != null
								=> CreatePointStyle(info.PointSymbology),
							"LineString" or "MultiLineString" when info.LineSymbology != null
								=> CreateLineStyle(info.LineSymbology),
							"Polygon" or "MultiPolygon" when info.PolygonSymbology != null
								=> CreatePolygonStyle(info.PolygonSymbology),
							_ => GetDefaultStyle(geometryType)
						};

						items.Add(new LegendItem
						{
							Label = info.Label ?? info.Value?.ToString() ?? "Unknown",
							Style = style,
							Value = info.Value
						});
					}
				}
				break;

			case RendererType.Graduated:
				if (symbology.ClassBreakInfos != null)
				{
					foreach (var classBreak in symbology.ClassBreakInfos)
					{
						var style = geometryType switch
						{
							"Point" or "MultiPoint" when classBreak.PointSymbology != null
								=> CreatePointStyle(classBreak.PointSymbology),
							"LineString" or "MultiLineString" when classBreak.LineSymbology != null
								=> CreateLineStyle(classBreak.LineSymbology),
							"Polygon" or "MultiPolygon" when classBreak.PolygonSymbology != null
								=> CreatePolygonStyle(classBreak.PolygonSymbology),
							_ => GetDefaultStyle(geometryType)
						};

						items.Add(new LegendItem
						{
							Label = classBreak.Label ?? $"{classBreak.MinValue} - {classBreak.MaxValue}",
							Style = style,
							MinValue = classBreak.MinValue,
							MaxValue = classBreak.MaxValue
						});
					}
				}
				break;
		}

		return items;
	}

	/// <inheritdoc />
	public void ClearCache()
	{
		_symbologyCache.Clear();
		_styleCache.Clear();
	}

	/// <summary>
	/// Gets a default style for the given geometry type
	/// </summary>
	private IStyle GetDefaultStyle(string geometryType)
	{
		var defaultSymbology = CreateDefaultSymbology(geometryType);
		return GetSimpleStyle(defaultSymbology, geometryType);
	}

	/// <summary>
	/// Checks if a value matches a unique value info value
	/// </summary>
	private bool MatchesValue(object? expected, object? actual)
	{
		if (expected == null && actual == null)
			return true;
		if (expected == null || actual == null)
			return false;

		// Convert both to strings for comparison
		return expected.ToString()?.Equals(actual.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
	}

	/// <summary>
	/// Tries to convert a value to double
	/// </summary>
	private bool TryConvertToDouble(object? value, out double result)
	{
		result = 0;

		if (value == null)
			return false;

		if (value is double d)
		{
			result = d;
			return true;
		}

		if (value is int i)
		{
			result = i;
			return true;
		}

		if (value is float f)
		{
			result = f;
			return true;
		}

		if (value is string s)
		{
			return double.TryParse(s, out result);
		}

		return false;
	}
}
