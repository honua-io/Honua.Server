using FluentAssertions;
using HonuaField.Models;
using HonuaField.Models.Symbology;
using HonuaField.Services;
using Mapsui.Styles;
using System.Text.Json;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Comprehensive unit tests for SymbologyService
/// Tests JSON parsing, style generation, and attribute-based styling
/// </summary>
public class SymbologyServiceTests
{
	private readonly SymbologyService _service;

	public SymbologyServiceTests()
	{
		_service = new SymbologyService();
	}

	#region Parsing Tests

	[Fact]
	public void ParseSymbology_NullJson_ReturnsNull()
	{
		// Act
		var result = _service.ParseSymbology(null);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void ParseSymbology_EmptyJson_ReturnsNull()
	{
		// Act
		var result = _service.ParseSymbology("");

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void ParseSymbology_SimplePointSymbology_ParsesCorrectly()
	{
		// Arrange
		var json = @"{
			""type"": ""Simple"",
			""label"": ""Test Points"",
			""point"": {
				""type"": ""Circle"",
				""color"": ""#FF0000"",
				""outlineColor"": ""#FFFFFF"",
				""outlineWidth"": 2,
				""size"": 12,
				""opacity"": 1.0
			}
		}";

		// Act
		var result = _service.ParseSymbology(json);

		// Assert
		result.Should().NotBeNull();
		result!.Type.Should().Be(RendererType.Simple);
		result.Label.Should().Be("Test Points");
		result.PointSymbology.Should().NotBeNull();
		result.PointSymbology!.Type.Should().Be(PointSymbolType.Circle);
		result.PointSymbology.Color.Should().Be("#FF0000");
		result.PointSymbology.Size.Should().Be(12);
	}

	[Fact]
	public void ParseSymbology_LineSymbology_ParsesCorrectly()
	{
		// Arrange
		var json = @"{
			""type"": ""Simple"",
			""line"": {
				""color"": ""#0000FF"",
				""width"": 3,
				""opacity"": 0.8,
				""cap"": ""Round"",
				""join"": ""Round"",
				""dashPattern"": [5, 3]
			}
		}";

		// Act
		var result = _service.ParseSymbology(json);

		// Assert
		result.Should().NotBeNull();
		result!.LineSymbology.Should().NotBeNull();
		result.LineSymbology!.Color.Should().Be("#0000FF");
		result.LineSymbology.Width.Should().Be(3);
		result.LineSymbology.Opacity.Should().Be(0.8);
		result.LineSymbology.DashPattern.Should().NotBeNull();
		result.LineSymbology.DashPattern!.Length.Should().Be(2);
	}

	[Fact]
	public void ParseSymbology_PolygonSymbology_ParsesCorrectly()
	{
		// Arrange
		var json = @"{
			""type"": ""Simple"",
			""polygon"": {
				""fillColor"": ""#00FF00"",
				""fillOpacity"": 0.5,
				""strokeColor"": ""#008800"",
				""strokeWidth"": 2,
				""strokeOpacity"": 1.0,
				""fillPattern"": ""Solid""
			}
		}";

		// Act
		var result = _service.ParseSymbology(json);

		// Assert
		result.Should().NotBeNull();
		result!.PolygonSymbology.Should().NotBeNull();
		result.PolygonSymbology!.FillColor.Should().Be("#00FF00");
		result.PolygonSymbology.FillOpacity.Should().Be(0.5);
		result.PolygonSymbology.StrokeWidth.Should().Be(2);
	}

	[Fact]
	public void ParseSymbology_UniqueValueRenderer_ParsesCorrectly()
	{
		// Arrange
		var json = @"{
			""type"": ""UniqueValue"",
			""field"": ""status"",
			""uniqueValueInfos"": [
				{
					""value"": ""active"",
					""label"": ""Active"",
					""point"": {
						""type"": ""Circle"",
						""color"": ""#00FF00"",
						""size"": 10
					}
				},
				{
					""value"": ""inactive"",
					""label"": ""Inactive"",
					""point"": {
						""type"": ""Circle"",
						""color"": ""#FF0000"",
						""size"": 10
					}
				}
			]
		}";

		// Act
		var result = _service.ParseSymbology(json);

		// Assert
		result.Should().NotBeNull();
		result!.Type.Should().Be(RendererType.UniqueValue);
		result.Field.Should().Be("status");
		result.UniqueValueInfos.Should().NotBeNull();
		result.UniqueValueInfos!.Count.Should().Be(2);
		result.UniqueValueInfos[0].Value.Should().Be("active");
		result.UniqueValueInfos[1].Value.Should().Be("inactive");
	}

	[Fact]
	public void ParseSymbology_GraduatedRenderer_ParsesCorrectly()
	{
		// Arrange
		var json = @"{
			""type"": ""Graduated"",
			""field"": ""population"",
			""classBreakInfos"": [
				{
					""minValue"": 0,
					""maxValue"": 1000,
					""label"": ""Small"",
					""point"": {
						""type"": ""Circle"",
						""color"": ""#FFFF00"",
						""size"": 8
					}
				},
				{
					""minValue"": 1000,
					""maxValue"": 10000,
					""label"": ""Medium"",
					""point"": {
						""type"": ""Circle"",
						""color"": ""#FFA500"",
						""size"": 12
					}
				}
			]
		}";

		// Act
		var result = _service.ParseSymbology(json);

		// Assert
		result.Should().NotBeNull();
		result!.Type.Should().Be(RendererType.Graduated);
		result.Field.Should().Be("population");
		result.ClassBreakInfos.Should().NotBeNull();
		result.ClassBreakInfos!.Count.Should().Be(2);
		result.ClassBreakInfos[0].MinValue.Should().Be(0);
		result.ClassBreakInfos[0].MaxValue.Should().Be(1000);
	}

	#endregion

	#region Color Parsing Tests

	[Theory]
	[InlineData("#FF0000", 255, 0, 0)]
	[InlineData("#00FF00", 0, 255, 0)]
	[InlineData("#0000FF", 0, 0, 255)]
	[InlineData("#F00", 255, 0, 0)] // Short form
	public void ParseColor_HexColor_ParsesCorrectly(string colorString, int expectedR, int expectedG, int expectedB)
	{
		// Act
		var result = _service.ParseColor(colorString);

		// Assert
		result.R.Should().Be(expectedR);
		result.G.Should().Be(expectedG);
		result.B.Should().Be(expectedB);
	}

	[Theory]
	[InlineData("red")]
	[InlineData("RED")]
	[InlineData("Red")]
	public void ParseColor_NamedColor_ParsesCorrectly(string colorString)
	{
		// Act
		var result = _service.ParseColor(colorString);

		// Assert
		result.Should().NotBeNull();
		// Red color should be returned
		result.R.Should().BeGreaterThan(200);
		result.G.Should().BeLessThan(100);
		result.B.Should().BeLessThan(100);
	}

	[Fact]
	public void ParseColor_RgbFormat_ParsesCorrectly()
	{
		// Act
		var result = _service.ParseColor("rgb(128, 64, 32)");

		// Assert
		result.R.Should().Be(128);
		result.G.Should().Be(64);
		result.B.Should().Be(32);
	}

	[Fact]
	public void ParseColor_RgbaFormat_ParsesCorrectly()
	{
		// Act
		var result = _service.ParseColor("rgba(128, 64, 32, 0.5)");

		// Assert
		result.R.Should().Be(128);
		result.G.Should().Be(64);
		result.B.Should().Be(32);
		// Alpha should be approximately 127 (0.5 * 255)
		result.A.Should().BeApproximately(127, 5);
	}

	[Fact]
	public void ParseColor_InvalidColor_ReturnsDefault()
	{
		// Act
		var result = _service.ParseColor("not-a-color");

		// Assert
		result.Should().NotBeNull();
		// Should return blue as default
		result.B.Should().BeGreaterThan(200);
	}

	[Fact]
	public void ParseColor_NullColor_ReturnsDefault()
	{
		// Act
		var result = _service.ParseColor(null);

		// Assert
		result.Should().NotBeNull();
	}

	#endregion

	#region Style Creation Tests

	[Fact]
	public void CreatePointStyle_CreatesValidStyle()
	{
		// Arrange
		var pointSymbology = new PointSymbology
		{
			Type = PointSymbolType.Circle,
			Color = "#FF0000",
			OutlineColor = "#FFFFFF",
			OutlineWidth = 2,
			Size = 12,
			Opacity = 1.0
		};

		// Act
		var result = _service.CreatePointStyle(pointSymbology);

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<SymbolStyle>();
		result.Fill.Should().NotBeNull();
		result.Outline.Should().NotBeNull();
	}

	[Fact]
	public void CreatePointStyle_WithOpacity_AppliesOpacity()
	{
		// Arrange
		var pointSymbology = new PointSymbology
		{
			Color = "#FF0000",
			Opacity = 0.5
		};

		// Act
		var result = _service.CreatePointStyle(pointSymbology);

		// Assert
		result.Fill.Should().NotBeNull();
		var fillColor = result.Fill!.Color;
		// Alpha should be approximately 127 (0.5 * 255)
		fillColor!.A.Should().BeApproximately(127, 5);
	}

	[Fact]
	public void CreateLineStyle_CreatesValidStyle()
	{
		// Arrange
		var lineSymbology = new LineSymbology
		{
			Color = "#0000FF",
			Width = 3,
			Opacity = 1.0,
			Cap = LineCapStyle.Round
		};

		// Act
		var result = _service.CreateLineStyle(lineSymbology);

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<VectorStyle>();
		result.Line.Should().NotBeNull();
		result.Line!.Width.Should().Be(3);
	}

	[Fact]
	public void CreateLineStyle_WithDashPattern_AppliesDashPattern()
	{
		// Arrange
		var lineSymbology = new LineSymbology
		{
			Color = "#0000FF",
			Width = 2,
			DashPattern = new[] { 5.0, 3.0 }
		};

		// Act
		var result = _service.CreateLineStyle(lineSymbology);

		// Assert
		result.Line.Should().NotBeNull();
		result.Line!.PenStyle.Should().NotBe(PenStyle.Solid);
	}

	[Fact]
	public void CreatePolygonStyle_CreatesValidStyle()
	{
		// Arrange
		var polygonSymbology = new PolygonSymbology
		{
			FillColor = "#00FF00",
			FillOpacity = 0.3,
			StrokeColor = "#008800",
			StrokeWidth = 2
		};

		// Act
		var result = _service.CreatePolygonStyle(polygonSymbology);

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<VectorStyle>();
		result.Fill.Should().NotBeNull();
		result.Outline.Should().NotBeNull();
	}

	[Fact]
	public void CreatePolygonStyle_WithNoFillPattern_CreatesTransparentFill()
	{
		// Arrange
		var polygonSymbology = new PolygonSymbology
		{
			FillPattern = FillPattern.None,
			StrokeColor = "#000000",
			StrokeWidth = 2
		};

		// Act
		var result = _service.CreatePolygonStyle(polygonSymbology);

		// Assert
		result.Fill.Should().BeNull();
		result.Outline.Should().NotBeNull();
	}

	#endregion

	#region Feature Styling Tests

	[Fact]
	public void GetStyleForFeature_SimpleRenderer_ReturnsCorrectStyle()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Simple,
			PointSymbology = new PointSymbology
			{
				Color = "#FF0000",
				Size = 12
			}
		};

		var feature = new Feature
		{
			Properties = "{}"
		};

		// Act
		var result = _service.GetStyleForFeature(feature, symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<SymbolStyle>();
	}

	[Fact]
	public void GetStyleForFeature_UniqueValueRenderer_ReturnsCorrectStyleForValue()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.UniqueValue,
			Field = "status",
			UniqueValueInfos = new List<UniqueValueInfo>
			{
				new UniqueValueInfo
				{
					Value = "active",
					PointSymbology = new PointSymbology { Color = "#00FF00", Size = 10 }
				},
				new UniqueValueInfo
				{
					Value = "inactive",
					PointSymbology = new PointSymbology { Color = "#FF0000", Size = 10 }
				}
			}
		};

		var feature = new Feature
		{
			Properties = JsonSerializer.Serialize(new Dictionary<string, object>
			{
				{ "status", "active" }
			})
		};

		// Act
		var result = _service.GetStyleForFeature(feature, symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<SymbolStyle>();
		// The style should have green color (for "active" status)
		var symbolStyle = (SymbolStyle)result;
		symbolStyle.Fill.Should().NotBeNull();
	}

	[Fact]
	public void GetStyleForFeature_GraduatedRenderer_ReturnsCorrectStyleForValue()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Graduated,
			Field = "population",
			ClassBreakInfos = new List<ClassBreakInfo>
			{
				new ClassBreakInfo
				{
					MinValue = 0,
					MaxValue = 1000,
					PointSymbology = new PointSymbology { Color = "#FFFF00", Size = 8 }
				},
				new ClassBreakInfo
				{
					MinValue = 1000,
					MaxValue = 10000,
					PointSymbology = new PointSymbology { Color = "#FF0000", Size = 12 }
				}
			}
		};

		var feature = new Feature
		{
			Properties = JsonSerializer.Serialize(new Dictionary<string, object>
			{
				{ "population", 500 }
			})
		};

		// Act
		var result = _service.GetStyleForFeature(feature, symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<SymbolStyle>();
		// The style should be for the first class break (0-1000)
	}

	[Fact]
	public void GetStyleForFeature_LineGeometry_ReturnsLineStyle()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Simple,
			LineSymbology = new LineSymbology
			{
				Color = "#0000FF",
				Width = 3
			}
		};

		var feature = new Feature();

		// Act
		var result = _service.GetStyleForFeature(feature, symbology, "LineString");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<VectorStyle>();
		((VectorStyle)result).Line.Should().NotBeNull();
	}

	[Fact]
	public void GetStyleForFeature_PolygonGeometry_ReturnsPolygonStyle()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Simple,
			PolygonSymbology = new PolygonSymbology
			{
				FillColor = "#00FF00",
				StrokeColor = "#008800"
			}
		};

		var feature = new Feature();

		// Act
		var result = _service.GetStyleForFeature(feature, symbology, "Polygon");

		// Assert
		result.Should().NotBeNull();
		result.Should().BeOfType<VectorStyle>();
		var vectorStyle = (VectorStyle)result;
		vectorStyle.Fill.Should().NotBeNull();
		vectorStyle.Outline.Should().NotBeNull();
	}

	#endregion

	#region Default Symbology Tests

	[Theory]
	[InlineData("Point")]
	[InlineData("MultiPoint")]
	public void CreateDefaultSymbology_PointGeometry_CreatesPointSymbology(string geometryType)
	{
		// Act
		var result = _service.CreateDefaultSymbology(geometryType);

		// Assert
		result.Should().NotBeNull();
		result.Type.Should().Be(RendererType.Simple);
		result.PointSymbology.Should().NotBeNull();
		result.LineSymbology.Should().BeNull();
		result.PolygonSymbology.Should().BeNull();
	}

	[Theory]
	[InlineData("LineString")]
	[InlineData("MultiLineString")]
	public void CreateDefaultSymbology_LineGeometry_CreatesLineSymbology(string geometryType)
	{
		// Act
		var result = _service.CreateDefaultSymbology(geometryType);

		// Assert
		result.Should().NotBeNull();
		result.Type.Should().Be(RendererType.Simple);
		result.LineSymbology.Should().NotBeNull();
		result.PointSymbology.Should().BeNull();
		result.PolygonSymbology.Should().BeNull();
	}

	[Theory]
	[InlineData("Polygon")]
	[InlineData("MultiPolygon")]
	public void CreateDefaultSymbology_PolygonGeometry_CreatesPolygonSymbology(string geometryType)
	{
		// Act
		var result = _service.CreateDefaultSymbology(geometryType);

		// Assert
		result.Should().NotBeNull();
		result.Type.Should().Be(RendererType.Simple);
		result.PolygonSymbology.Should().NotBeNull();
		result.PointSymbology.Should().BeNull();
		result.LineSymbology.Should().BeNull();
	}

	#endregion

	#region Legend Generation Tests

	[Fact]
	public void GenerateLegend_SimpleRenderer_ReturnsSingleItem()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Simple,
			Label = "All Features",
			PointSymbology = new PointSymbology { Color = "#FF0000" }
		};

		// Act
		var result = _service.GenerateLegend(symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Count.Should().Be(1);
		result[0].Label.Should().Be("All Features");
	}

	[Fact]
	public void GenerateLegend_UniqueValueRenderer_ReturnsMultipleItems()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.UniqueValue,
			Field = "status",
			UniqueValueInfos = new List<UniqueValueInfo>
			{
				new UniqueValueInfo
				{
					Value = "active",
					Label = "Active",
					PointSymbology = new PointSymbology { Color = "#00FF00" }
				},
				new UniqueValueInfo
				{
					Value = "inactive",
					Label = "Inactive",
					PointSymbology = new PointSymbology { Color = "#FF0000" }
				}
			}
		};

		// Act
		var result = _service.GenerateLegend(symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Count.Should().Be(2);
		result[0].Label.Should().Be("Active");
		result[1].Label.Should().Be("Inactive");
	}

	[Fact]
	public void GenerateLegend_GraduatedRenderer_ReturnsMultipleItems()
	{
		// Arrange
		var symbology = new SymbologyDefinition
		{
			Type = RendererType.Graduated,
			ClassBreakInfos = new List<ClassBreakInfo>
			{
				new ClassBreakInfo
				{
					MinValue = 0,
					MaxValue = 1000,
					Label = "0 - 1000",
					PointSymbology = new PointSymbology { Color = "#FFFF00" }
				},
				new ClassBreakInfo
				{
					MinValue = 1000,
					MaxValue = 10000,
					Label = "1000 - 10000",
					PointSymbology = new PointSymbology { Color = "#FF0000" }
				}
			}
		};

		// Act
		var result = _service.GenerateLegend(symbology, "Point");

		// Assert
		result.Should().NotBeNull();
		result.Count.Should().Be(2);
		result[0].Label.Should().Be("0 - 1000");
		result[1].Label.Should().Be("1000 - 10000");
	}

	#endregion

	#region Caching Tests

	[Fact]
	public void ParseSymbology_SameJsonTwice_UsesCachedResult()
	{
		// Arrange
		var json = @"{""type"": ""Simple"", ""label"": ""Test""}";

		// Act
		var result1 = _service.ParseSymbology(json);
		var result2 = _service.ParseSymbology(json);

		// Assert
		result1.Should().NotBeNull();
		result2.Should().NotBeNull();
		// Both should be the same instance (cached)
		result1.Should().BeSameAs(result2);
	}

	[Fact]
	public void ClearCache_RemovesCachedData()
	{
		// Arrange
		var json = @"{""type"": ""Simple"", ""label"": ""Test""}";
		var result1 = _service.ParseSymbology(json);

		// Act
		_service.ClearCache();
		var result2 = _service.ParseSymbology(json);

		// Assert
		result1.Should().NotBeNull();
		result2.Should().NotBeNull();
		// After clearing cache, should be different instances
		result1.Should().NotBeSameAs(result2);
	}

	#endregion
}
