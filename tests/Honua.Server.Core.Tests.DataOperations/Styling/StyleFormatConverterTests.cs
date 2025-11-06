using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Styling;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class StyleFormatConverterTests
{
    private static readonly XNamespace Sld = "http://www.opengis.net/sld";
    private static readonly XNamespace Ogc = "http://www.opengis.net/ogc";

    [Fact]
    public void CreateSld_WithUniqueValueStyle_ShouldEmitRulePerClass()
    {
        var style = new StyleDefinition
        {
            Id = "roads",
            GeometryType = "line",
            Renderer = "uniqueValue",
            UniqueValue = new UniqueValueStyleDefinition
            {
                Field = "type",
                DefaultSymbol = new SimpleStyleDefinition
                {
                    SymbolType = "line",
                    StrokeColor = "#333333",
                    StrokeWidth = 2d
                },
                Classes = new[]
                {
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "highway",
                        Symbol = new SimpleStyleDefinition
                        {
                            SymbolType = "line",
                            StrokeColor = "#FF0000",
                            StrokeWidth = 2.5d
                        }
                    }
                }
            }
        };

        var xml = StyleFormatConverter.CreateSld(style, "Roads", "line");
        var document = XDocument.Parse(xml);

        var rules = document.Descendants(Sld + "Rule").ToArray();
        rules.Should().HaveCount(2);

        rules.Any(rule => !rule.Elements(Ogc + "Filter").Any()).Should().BeTrue();
        rules.Any(rule =>
            rule.Element(Ogc + "Filter")?.Descendants(Ogc + "Literal").Any(node => node.Value == "highway") == true)
            .Should().BeTrue();
    }

    [Fact]
    public void CreateSld_WithRasterStyle_ShouldIncludeRasterSymbolizer()
    {
        var style = new StyleDefinition
        {
            Id = "imagery",
            GeometryType = "raster",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#5AA06EFF",
                Opacity = 0.6d
            }
        };

        var xml = StyleFormatConverter.CreateSld(style, "Imagery", "raster");
        var document = XDocument.Parse(xml);

        var rasterSymbolizers = document.Descendants(Sld + "RasterSymbolizer").ToArray();
        rasterSymbolizers.Should().HaveCount(1);

        var opacityElement = rasterSymbolizers[0].Element(Sld + "Opacity");
        opacityElement.Should().NotBeNull();
        opacityElement!.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateCss_BasicPolygonStyle_GeneratesValidCss()
    {
        var style = new StyleDefinition
        {
            Id = "parks",
            Title = "Parks",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "default",
                    IsDefault = true,
                    Symbolizer = new SimpleStyleDefinition
                    {
                        FillColor = "#90EE90",
                        StrokeColor = "#228B22",
                        StrokeWidth = 1.5
                    }
                }
            }
        };

        var css = StyleFormatConverter.CreateCss(style, "polygon");

        css.Should().Contain("/* Style: Parks */");
        css.Should().Contain("*");
        css.Should().Contain("fill: #90EE90;");
        css.Should().Contain("stroke: #228B22;");
        css.Should().Contain("stroke-width: 1.5;");
    }

    [Fact]
    public void CreateCss_WithFilter_GeneratesAttributeSelector()
    {
        var style = new StyleDefinition
        {
            Id = "filtered",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "residential",
                    Filter = new RuleFilterDefinition("category", "residential"),
                    Symbolizer = new SimpleStyleDefinition { FillColor = "#FFD700" }
                }
            }
        };

        var css = StyleFormatConverter.CreateCss(style);

        css.Should().Contain("[category = 'residential']");
        css.Should().Contain("fill: #FFD700;");
    }

    [Fact]
    public void CreateCss_WithScaleConstraints_GeneratesScaleSelectors()
    {
        var style = new StyleDefinition
        {
            Id = "scaled",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "medium-scale",
                    MinScale = 100000,
                    MaxScale = 500000,
                    Symbolizer = new SimpleStyleDefinition { FillColor = "#FF5733" }
                }
            }
        };

        var css = StyleFormatConverter.CreateCss(style);

        css.Should().Contain("[@scale > 100000]");
        css.Should().Contain("[@scale < 500000]");
    }

    [Fact]
    public void CreateYsld_BasicPolygonStyle_GeneratesValidYsld()
    {
        var style = new StyleDefinition
        {
            Id = "parks",
            Title = "Parks",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "default",
                    IsDefault = true,
                    Symbolizer = new SimpleStyleDefinition
                    {
                        FillColor = "#90EE90",
                        StrokeColor = "#228B22",
                        StrokeWidth = 1.5
                    }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style, "polygon");

        ysld.Should().Contain("name: parks");
        ysld.Should().Contain("title: Parks");
        ysld.Should().Contain("feature-styles:");
        ysld.Should().Contain("- rules:");
        ysld.Should().Contain("- name: default");
        ysld.Should().Contain("- polygon:");
        ysld.Should().Contain("color: '#90EE90'");
        ysld.Should().Contain("color: '#228B22'");
        ysld.Should().Contain("width: 1.5");
    }

    [Fact]
    public void CreateYsld_LineStyle_GeneratesLineSymbolizer()
    {
        var style = new StyleDefinition
        {
            Id = "roads",
            GeometryType = "line",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "main",
                    Symbolizer = new SimpleStyleDefinition
                    {
                        StrokeColor = "#FF5733",
                        StrokeWidth = 3
                    }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style, "line");

        ysld.Should().Contain("- line:");
        ysld.Should().Contain("stroke:");
        ysld.Should().Contain("color: '#FF5733'");
        ysld.Should().Contain("width: 3");
    }

    [Fact]
    public void CreateYsld_PointStyleWithIcon_GeneratesExternalGraphic()
    {
        var style = new StyleDefinition
        {
            Id = "markers",
            GeometryType = "point",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "default",
                    Symbolizer = new SimpleStyleDefinition
                    {
                        IconHref = "icon.png",
                        Size = 16
                    }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style, "point");

        ysld.Should().Contain("- point:");
        ysld.Should().Contain("size: 16");
        ysld.Should().Contain("external-graphic:");
        ysld.Should().Contain("url: icon.png");
    }

    [Fact]
    public void CreateYsld_WithFilter_GeneratesFilterClause()
    {
        var style = new StyleDefinition
        {
            Id = "filtered",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "residential",
                    Filter = new RuleFilterDefinition("category", "residential"),
                    Symbolizer = new SimpleStyleDefinition { FillColor = "#FFD700" }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style);

        ysld.Should().Contain("filter: category = 'residential'");
    }

    [Fact]
    public void CreateYsld_WithScaleConstraints_GeneratesScaleBlock()
    {
        var style = new StyleDefinition
        {
            Id = "scaled",
            GeometryType = "polygon",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "medium-scale",
                    MinScale = 100000,
                    MaxScale = 500000,
                    Symbolizer = new SimpleStyleDefinition { FillColor = "#FF5733" }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style);

        ysld.Should().Contain("scale:");
        ysld.Should().Contain("min: 100000");
        ysld.Should().Contain("max: 500000");
    }

    [Fact]
    public void CreateYsld_RasterStyle_GeneratesRasterSymbolizer()
    {
        var style = new StyleDefinition
        {
            Id = "imagery",
            GeometryType = "raster",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "default",
                    Symbolizer = new SimpleStyleDefinition
                    {
                        SymbolType = "raster",
                        Opacity = 0.75
                    }
                }
            }
        };

        var ysld = StyleFormatConverter.CreateYsld(style, "raster");

        ysld.Should().Contain("- raster:");
        ysld.Should().Contain("opacity: 0.75");
    }

    #region MapLibre Style Tests

    [Fact]
    public void CreateMapLibreStyle_SimplePolygon_GeneratesFillLayer()
    {
        var style = new StyleDefinition
        {
            Id = "parks",
            Title = "Parks",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#90EE90",
                StrokeColor = "#228B22",
                StrokeWidth = 1.5,
                Opacity = 0.8
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "parks-layer", "parks-source", "parks");

        // Verify version
        mapLibreStyle["version"]?.GetValue<int>().Should().Be(8);

        // Verify name
        mapLibreStyle["name"]?.GetValue<string>().Should().Be("Parks");

        // Verify metadata
        var metadata = mapLibreStyle["metadata"]?.AsObject();
        metadata.Should().NotBeNull();
        metadata!["honua:styleId"]?.GetValue<string>().Should().Be("parks");
        metadata["honua:renderer"]?.GetValue<string>().Should().Be("simple");

        // Verify sources
        var sources = mapLibreStyle["sources"]?.AsObject();
        sources.Should().NotBeNull();
        sources!["parks-source"].Should().NotBeNull();

        // Verify layers
        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();
        layer!["id"]?.GetValue<string>().Should().Be("parks-layer");
        layer["type"]?.GetValue<string>().Should().Be("fill");
        layer["source"]?.GetValue<string>().Should().Be("parks-source");
        layer["source-layer"]?.GetValue<string>().Should().Be("parks");

        // Verify paint properties
        var paint = layer["paint"]?.AsObject();
        paint.Should().NotBeNull();
        paint!["fill-color"]?.GetValue<string>().Should().Be("#90EE90");
        paint["fill-opacity"]?.GetValue<double>().Should().Be(0.8);
        paint["fill-outline-color"]?.GetValue<string>().Should().Be("#228B22");
    }

    [Fact]
    public void CreateMapLibreStyle_SimpleLine_GeneratesLineLayer()
    {
        var style = new StyleDefinition
        {
            Id = "roads",
            Title = "Roads",
            GeometryType = "line",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                StrokeColor = "#FF5733",
                StrokeWidth = 3,
                Opacity = 1.0
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "roads-layer", "roads-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();
        layer!["type"]?.GetValue<string>().Should().Be("line");

        var paint = layer["paint"]?.AsObject();
        paint.Should().NotBeNull();
        paint!["line-color"]?.GetValue<string>().Should().Be("#FF5733");
        paint["line-width"]?.GetValue<double>().Should().Be(3);
        paint["line-opacity"]?.GetValue<double>().Should().Be(1.0);
    }

    [Fact]
    public void CreateMapLibreStyle_SimplePoint_GeneratesCircleLayer()
    {
        var style = new StyleDefinition
        {
            Id = "markers",
            Title = "Markers",
            GeometryType = "point",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#FF0000",
                Size = 8,
                Opacity = 0.9
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "markers-layer", "markers-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();
        layer!["type"]?.GetValue<string>().Should().Be("circle");

        var paint = layer["paint"]?.AsObject();
        paint.Should().NotBeNull();
        paint!["circle-color"]?.GetValue<string>().Should().Be("#FF0000");
        paint["circle-radius"]?.GetValue<double>().Should().Be(8);
        paint["circle-opacity"]?.GetValue<double>().Should().Be(0.9);
    }

    [Fact]
    public void CreateMapLibreStyle_UniqueValue_GeneratesMatchExpression()
    {
        var style = new StyleDefinition
        {
            Id = "landuse",
            Title = "Land Use",
            GeometryType = "polygon",
            Renderer = "uniqueValue",
            UniqueValue = new UniqueValueStyleDefinition
            {
                Field = "type",
                DefaultSymbol = new SimpleStyleDefinition
                {
                    FillColor = "#E0E0E0",
                    Opacity = 0.7
                },
                Classes = new[]
                {
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "residential",
                        Symbol = new SimpleStyleDefinition
                        {
                            FillColor = "#FFD700",
                            Opacity = 0.7
                        }
                    },
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "commercial",
                        Symbol = new SimpleStyleDefinition
                        {
                            FillColor = "#FF69B4",
                            Opacity = 0.7
                        }
                    },
                    new UniqueValueStyleClassDefinition
                    {
                        Value = "industrial",
                        Symbol = new SimpleStyleDefinition
                        {
                            FillColor = "#9370DB",
                            Opacity = 0.7
                        }
                    }
                }
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "landuse-layer", "landuse-source", "landuse");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();
        layer!["type"]?.GetValue<string>().Should().Be("fill");

        var paint = layer["paint"]?.AsObject();
        paint.Should().NotBeNull();

        // Verify match expression structure
        var fillColor = paint!["fill-color"]?.AsArray();
        fillColor.Should().NotBeNull();
        fillColor![0]?.GetValue<string>().Should().Be("match");

        // Verify field reference
        var fieldRef = fillColor[1]?.AsArray();
        fieldRef.Should().NotBeNull();
        fieldRef![0]?.GetValue<string>().Should().Be("get");
        fieldRef[1]?.GetValue<string>().Should().Be("type");

        // Verify class mappings (residential, commercial, industrial)
        fillColor[2]?.GetValue<string>().Should().Be("residential");
        fillColor[3]?.GetValue<string>().Should().Be("#FFD700");
        fillColor[4]?.GetValue<string>().Should().Be("commercial");
        fillColor[5]?.GetValue<string>().Should().Be("#FF69B4");
        fillColor[6]?.GetValue<string>().Should().Be("industrial");
        fillColor[7]?.GetValue<string>().Should().Be("#9370DB");

        // Verify default value
        fillColor[8]?.GetValue<string>().Should().Be("#E0E0E0");

        // Verify opacity
        var fillOpacity = paint["fill-opacity"]?.GetValue<double>();
        fillOpacity.Should().Be(0.7);
    }

    [Fact]
    public void CreateMapLibreStyle_RuleBased_GeneratesMultipleLayers()
    {
        var style = new StyleDefinition
        {
            Id = "buildings",
            Title = "Buildings",
            GeometryType = "polygon",
            Renderer = "rule-based",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "large-scale",
                    MinScale = 1000,
                    MaxScale = 10000,
                    Symbolizer = new SimpleStyleDefinition
                    {
                        FillColor = "#8B4513",
                        StrokeColor = "#654321",
                        Opacity = 0.9
                    }
                },
                new StyleRuleDefinition
                {
                    Id = "medium-scale",
                    MinScale = 10000,
                    MaxScale = 100000,
                    Symbolizer = new SimpleStyleDefinition
                    {
                        FillColor = "#D2691E",
                        StrokeColor = "#A0522D",
                        Opacity = 0.8
                    }
                },
                new StyleRuleDefinition
                {
                    Id = "small-scale",
                    MinScale = 100000,
                    Symbolizer = new SimpleStyleDefinition
                    {
                        FillColor = "#DEB887",
                        StrokeColor = "#BC8F8F",
                        Opacity = 0.7
                    }
                }
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "buildings-layer", "buildings-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(3);

        // Verify first layer (large-scale)
        var layer1 = layers[0]?.AsObject();
        layer1.Should().NotBeNull();
        layer1!["id"]?.GetValue<string>().Should().Be("buildings-layer-large-scale");
        layer1["type"]?.GetValue<string>().Should().Be("fill");
        layer1["minzoom"]?.GetValue<int>().Should().Be(14); // 10000 scale
        layer1["maxzoom"]?.GetValue<int>().Should().Be(18); // 1000 scale

        var paint1 = layer1["paint"]?.AsObject();
        paint1.Should().NotBeNull();
        paint1!["fill-color"]?.GetValue<string>().Should().Be("#8B4513");

        // Verify second layer (medium-scale)
        var layer2 = layers[1]?.AsObject();
        layer2.Should().NotBeNull();
        layer2!["id"]?.GetValue<string>().Should().Be("buildings-layer-medium-scale");
        layer2["minzoom"]?.GetValue<int>().Should().Be(10); // 100000 scale
        layer2["maxzoom"]?.GetValue<int>().Should().Be(14); // 10000 scale

        // Verify third layer (small-scale)
        var layer3 = layers[2]?.AsObject();
        layer3.Should().NotBeNull();
        layer3!["id"]?.GetValue<string>().Should().Be("buildings-layer-small-scale");
        layer3["minzoom"]?.GetValue<int>().Should().Be(10); // 100000 scale
        layer3.Should().NotContainKey("maxzoom"); // No max scale defined
    }

    [Fact]
    public void CreateMapLibreStyle_WithFilter_AppliesFilterExpression()
    {
        var style = new StyleDefinition
        {
            Id = "filtered-roads",
            GeometryType = "line",
            Renderer = "rule-based",
            Rules = new[]
            {
                new StyleRuleDefinition
                {
                    Id = "highways",
                    Filter = new RuleFilterDefinition("type", "highway"),
                    Symbolizer = new SimpleStyleDefinition
                    {
                        StrokeColor = "#FF0000",
                        StrokeWidth = 4
                    }
                }
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "roads-layer", "roads-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();

        // Verify filter expression
        var filter = layer!["filter"]?.AsArray();
        filter.Should().NotBeNull();
        filter![0]?.GetValue<string>().Should().Be("==");

        var fieldRef = filter[1]?.AsArray();
        fieldRef.Should().NotBeNull();
        fieldRef![0]?.GetValue<string>().Should().Be("get");
        fieldRef[1]?.GetValue<string>().Should().Be("type");

        filter[2]?.GetValue<string>().Should().Be("highway");
    }

    [Fact]
    public void CreateMapLibreStyle_ColorWithAlpha_SeparatesOpacity()
    {
        var style = new StyleDefinition
        {
            Id = "translucent-areas",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#FF0000AA", // Color with alpha channel
                StrokeColor = "#00FF0080", // Stroke with alpha
                Opacity = 0.9
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "areas-layer", "areas-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        var layer = layers![0]?.AsObject();
        var paint = layer!["paint"]?.AsObject();

        // Verify colors have alpha stripped
        paint!["fill-color"]?.GetValue<string>().Should().Be("#FF0000");
        paint["fill-outline-color"]?.GetValue<string>().Should().Be("#00FF00");

        // Verify opacity is used from the Opacity property, not from alpha channel
        paint["fill-opacity"]?.GetValue<double>().Should().Be(0.9);
    }

    [Fact]
    public void CreateMapLibreStyle_RasterGeometry_GeneratesRasterLayer()
    {
        var style = new StyleDefinition
        {
            Id = "imagery",
            Title = "Satellite Imagery",
            GeometryType = "raster",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                Opacity = 0.75
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "imagery-layer", "imagery-source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(1);

        var layer = layers[0]?.AsObject();
        layer.Should().NotBeNull();
        layer!["type"]?.GetValue<string>().Should().Be("raster");

        var paint = layer["paint"]?.AsObject();
        paint.Should().NotBeNull();
        paint!["raster-opacity"]?.GetValue<double>().Should().Be(0.75);
    }

    [Fact]
    public void CreateMapLibreStyle_WithoutSourceLayer_OmitsSourceLayerProperty()
    {
        var style = new StyleDefinition
        {
            Id = "simple",
            GeometryType = "polygon",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                FillColor = "#FF0000"
            }
        };

        // Call without sourceLayer parameter
        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "layer-id", "source-id");

        var layers = mapLibreStyle["layers"]?.AsArray();
        var layer = layers![0]?.AsObject();

        // Verify source-layer is not present
        layer.Should().NotBeNull();
        layer!.Should().NotContainKey("source-layer");
    }

    [Fact]
    public void CreateMapLibreStyle_ScaleDenominatorConversion_MapsToCorrectZoom()
    {
        var style = new StyleDefinition
        {
            Id = "zoom-test",
            GeometryType = "line",
            Renderer = "rule-based",
            Rules = new[]
            {
                // Test various scale denominators
                new StyleRuleDefinition
                {
                    Id = "zoom-5-to-8",
                    MinScale = 1_091_958, // Should map to zoom 8
                    MaxScale = 8_735_660, // Should map to zoom 5
                    Symbolizer = new SimpleStyleDefinition { StrokeColor = "#FF0000" }
                },
                new StyleRuleDefinition
                {
                    Id = "zoom-12-to-15",
                    MinScale = 8_531, // Should map to zoom 15
                    MaxScale = 68_247, // Should map to zoom 12
                    Symbolizer = new SimpleStyleDefinition { StrokeColor = "#00FF00" }
                }
            }
        };

        var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
            style, "layer", "source");

        var layers = mapLibreStyle["layers"]?.AsArray();
        layers.Should().NotBeNull();
        layers!.Count.Should().Be(2);

        // Verify first rule zoom mapping
        var layer1 = layers[0]?.AsObject();
        layer1!["minzoom"]?.GetValue<int>().Should().Be(5);
        layer1["maxzoom"]?.GetValue<int>().Should().Be(8);

        // Verify second rule zoom mapping
        var layer2 = layers[1]?.AsObject();
        layer2!["minzoom"]?.GetValue<int>().Should().Be(12);
        layer2["maxzoom"]?.GetValue<int>().Should().Be(15);
    }

    #endregion
}
