using System;
using System.Linq;
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
}
