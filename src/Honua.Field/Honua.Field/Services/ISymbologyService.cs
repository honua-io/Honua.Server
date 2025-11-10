// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using HonuaField.Models.Symbology;
using Mapsui.Styles;

namespace HonuaField.Services;

/// <summary>
/// Service for parsing and rendering symbology definitions
/// Converts symbology JSON to Mapsui styles for map rendering
/// </summary>
public interface ISymbologyService
{
	/// <summary>
	/// Parses symbology JSON into a SymbologyDefinition object
	/// </summary>
	/// <param name="symbologyJson">JSON string containing symbology definition</param>
	/// <returns>Parsed SymbologyDefinition or null if parsing fails</returns>
	SymbologyDefinition? ParseSymbology(string? symbologyJson);

	/// <summary>
	/// Gets the appropriate Mapsui style for a feature based on symbology definition
	/// </summary>
	/// <param name="feature">The feature to style</param>
	/// <param name="symbology">The symbology definition to apply</param>
	/// <param name="geometryType">The geometry type (Point, LineString, Polygon, etc.)</param>
	/// <returns>A Mapsui IStyle object</returns>
	IStyle GetStyleForFeature(Feature feature, SymbologyDefinition symbology, string geometryType);

	/// <summary>
	/// Creates a Mapsui SymbolStyle from PointSymbology
	/// </summary>
	/// <param name="pointSymbology">Point symbology configuration</param>
	/// <returns>A Mapsui SymbolStyle</returns>
	SymbolStyle CreatePointStyle(PointSymbology pointSymbology);

	/// <summary>
	/// Creates a Mapsui VectorStyle for lines from LineSymbology
	/// </summary>
	/// <param name="lineSymbology">Line symbology configuration</param>
	/// <returns>A Mapsui VectorStyle for lines</returns>
	VectorStyle CreateLineStyle(LineSymbology lineSymbology);

	/// <summary>
	/// Creates a Mapsui VectorStyle for polygons from PolygonSymbology
	/// </summary>
	/// <param name="polygonSymbology">Polygon symbology configuration</param>
	/// <returns>A Mapsui VectorStyle for polygons</returns>
	VectorStyle CreatePolygonStyle(PolygonSymbology polygonSymbology);

	/// <summary>
	/// Parses a color string (hex, rgb, or named) to Mapsui Color
	/// </summary>
	/// <param name="colorString">Color string (e.g., "#FF0000", "rgb(255,0,0)", "red")</param>
	/// <param name="defaultColor">Default color to return if parsing fails</param>
	/// <returns>Mapsui Color</returns>
	Mapsui.Styles.Color ParseColor(string? colorString, Mapsui.Styles.Color? defaultColor = null);

	/// <summary>
	/// Creates a default symbology definition for a given geometry type
	/// </summary>
	/// <param name="geometryType">The geometry type (Point, LineString, Polygon, etc.)</param>
	/// <returns>A default SymbologyDefinition</returns>
	SymbologyDefinition CreateDefaultSymbology(string geometryType);

	/// <summary>
	/// Generates legend items from a symbology definition
	/// </summary>
	/// <param name="symbology">The symbology definition</param>
	/// <param name="geometryType">The geometry type</param>
	/// <returns>List of legend items with labels and style previews</returns>
	List<LegendItem> GenerateLegend(SymbologyDefinition symbology, string geometryType);

	/// <summary>
	/// Clears the internal style cache
	/// </summary>
	void ClearCache();
}

/// <summary>
/// Legend item for symbology display
/// </summary>
public class LegendItem
{
	/// <summary>
	/// Label for the legend item
	/// </summary>
	public string Label { get; set; } = string.Empty;

	/// <summary>
	/// The style to preview
	/// </summary>
	public IStyle Style { get; set; } = null!;

	/// <summary>
	/// Field value (for unique value renderers)
	/// </summary>
	public object? Value { get; set; }

	/// <summary>
	/// Minimum value (for graduated renderers)
	/// </summary>
	public double? MinValue { get; set; }

	/// <summary>
	/// Maximum value (for graduated renderers)
	/// </summary>
	public double? MaxValue { get; set; }
}
