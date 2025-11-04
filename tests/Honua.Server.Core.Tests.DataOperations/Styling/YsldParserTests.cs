using FluentAssertions;
using Honua.Server.Core.Styling;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Styling;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class YsldParserTests
{
    [Fact]
    public void Parse_BasicPolygonStyle_ParsesCorrectly()
    {
        var ysld = @"
name: test-style
title: Test Style
feature-styles:
- rules:
  - name: default
    symbolizers:
    - polygon:
        fill:
          color: '#4A90E2'
        stroke:
          color: '#1F364D'
          width: 1.5
";

        var style = YsldParser.Parse(ysld, "test-style");

        style.Should().NotBeNull();
        style.Id.Should().Be("test-style");
        style.Title.Should().Be("Test Style");
        style.Format.Should().Be("ysld");
        style.GeometryType.Should().Be("polygon");
        style.Rules.Should().HaveCount(1);

        var rule = style.Rules[0];
        rule.Id.Should().Be("default");
        rule.Symbolizer.FillColor.Should().Be("#4A90E2");
        rule.Symbolizer.StrokeColor.Should().Be("#1F364D");
        rule.Symbolizer.StrokeWidth.Should().Be(1.5);
    }

    [Fact]
    public void Parse_LineStyle_ParsesCorrectly()
    {
        var ysld = @"
name: line-style
feature-styles:
- rules:
  - name: main-road
    symbolizers:
    - line:
        stroke:
          color: '#FF5733'
          width: 3
";

        var style = YsldParser.Parse(ysld, "line-style");

        style.GeometryType.Should().Be("line");
        style.Rules[0].Symbolizer.StrokeColor.Should().Be("#FF5733");
        style.Rules[0].Symbolizer.StrokeWidth.Should().Be(3);
    }

    [Fact]
    public void Parse_PointStyleWithMark_ParsesCorrectly()
    {
        var ysld = @"
name: point-style
feature-styles:
- rules:
  - name: marker
    symbolizers:
    - point:
        size: 12
        symbols:
        - mark:
            shape: circle
            fill:
              color: '#FF5733'
              opacity: 0.8
            stroke:
              color: '#AA3311'
              width: 1
";

        var style = YsldParser.Parse(ysld, "point-style");

        style.GeometryType.Should().Be("point");
        var rule = style.Rules[0];
        rule.Symbolizer.Size.Should().Be(12);
        rule.Symbolizer.FillColor.Should().Be("#FF5733");
        rule.Symbolizer.Opacity.Should().Be(0.8);
        rule.Symbolizer.StrokeColor.Should().Be("#AA3311");
        rule.Symbolizer.StrokeWidth.Should().Be(1);
    }

    [Fact]
    public void Parse_PointStyleWithExternalGraphic_ParsesCorrectly()
    {
        var ysld = @"
name: icon-style
feature-styles:
- rules:
  - name: marker
    symbolizers:
    - point:
        size: 16
        symbols:
        - external-graphic:
            url: icon.png
            format: image/png
";

        var style = YsldParser.Parse(ysld, "icon-style");

        style.GeometryType.Should().Be("point");
        style.Rules[0].Symbolizer.IconHref.Should().Be("icon.png");
        style.Rules[0].Symbolizer.Size.Should().Be(16);
    }

    [Fact]
    public void Parse_WithFilter_ParsesCorrectly()
    {
        var ysld = @"
name: filtered-style
feature-styles:
- rules:
  - name: residential
    filter: category = 'residential'
    symbolizers:
    - polygon:
        fill:
          color: '#90EE90'
";

        var style = YsldParser.Parse(ysld, "filtered-style");

        var rule = style.Rules[0];
        rule.Filter.Should().NotBeNull();
        rule.Filter!.Field.Should().Be("category");
        rule.Filter.Value.Should().Be("residential");
    }

    [Fact]
    public void Parse_WithScaleConstraints_ParsesCorrectly()
    {
        var ysld = @"
name: scale-style
feature-styles:
- rules:
  - name: medium-scale
    scale:
      min: 100000
      max: 500000
    symbolizers:
    - polygon:
        fill:
          color: '#FFD700'
";

        var style = YsldParser.Parse(ysld, "scale-style");

        var rule = style.Rules[0];
        rule.MinScale.Should().Be(100000);
        rule.MaxScale.Should().Be(500000);
    }

    [Fact]
    public void Parse_MultipleRules_ParsesAll()
    {
        var ysld = @"
name: multi-rule-style
feature-styles:
- rules:
  - name: water
    filter: type = 'water'
    symbolizers:
    - polygon:
        fill:
          color: '#0077BE'
  - name: forest
    filter: type = 'forest'
    symbolizers:
    - polygon:
        fill:
          color: '#228B22'
  - name: default
    symbolizers:
    - polygon:
        fill:
          color: '#CCCCCC'
";

        var style = YsldParser.Parse(ysld, "multi-rule-style");

        style.Rules.Should().HaveCount(3);
        style.Rules[0].Filter!.Value.Should().Be("water");
        style.Rules[0].Symbolizer.FillColor.Should().Be("#0077BE");
        style.Rules[1].Filter!.Value.Should().Be("forest");
        style.Rules[1].Symbolizer.FillColor.Should().Be("#228B22");
        style.Rules[2].IsDefault.Should().BeTrue();
        style.Rules[2].Symbolizer.FillColor.Should().Be("#CCCCCC");
    }

    [Fact]
    public void Parse_RasterSymbolizer_ParsesCorrectly()
    {
        var ysld = @"
name: raster-style
feature-styles:
- rules:
  - name: raster-rule
    symbolizers:
    - raster:
        opacity: 0.75
";

        var style = YsldParser.Parse(ysld, "raster-style");

        style.GeometryType.Should().Be("raster");
        style.Rules[0].Symbolizer.Opacity.Should().Be(0.75);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        var act = () => YsldParser.Parse("", "test");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyStyleId_ThrowsArgumentException()
    {
        var act = () => YsldParser.Parse("name: test", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_ComplexPolygonStyle_ParsesAll()
    {
        var ysld = @"
name: complex-style
title: Complex Park Style
feature-styles:
- rules:
  - name: park
    filter: landuse = 'park'
    scale:
      min: 50000
      max: 200000
    symbolizers:
    - polygon:
        fill:
          color: '#90EE90'
          opacity: 0.8
        stroke:
          color: '#228B22'
          width: 2
";

        var style = YsldParser.Parse(ysld, "complex-style");

        style.Title.Should().Be("Complex Park Style");
        var rule = style.Rules[0];
        rule.Id.Should().Be("park");
        rule.Filter!.Field.Should().Be("landuse");
        rule.Filter.Value.Should().Be("park");
        rule.MinScale.Should().Be(50000);
        rule.MaxScale.Should().Be(200000);
        rule.Symbolizer.FillColor.Should().Be("#90EE90");
        rule.Symbolizer.Opacity.Should().Be(0.8);
        rule.Symbolizer.StrokeColor.Should().Be("#228B22");
        rule.Symbolizer.StrokeWidth.Should().Be(2);
    }

    [Fact]
    public void Parse_WithGraphicSize_ParsesCorrectly()
    {
        var ysld = @"
name: graphic-style
feature-styles:
- rules:
  - name: marker
    symbolizers:
    - point:
        graphic:
          size: 18
          symbols:
          - mark:
              shape: square
              fill:
                color: '#FF0000'
";

        var style = YsldParser.Parse(ysld, "graphic-style");

        style.Rules[0].Symbolizer.Size.Should().Be(18);
        style.Rules[0].Symbolizer.FillColor.Should().Be("#FF0000");
    }
}
