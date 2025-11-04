using FluentAssertions;
using Honua.Server.Core.Styling;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Styling;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class CssParserTests
{
    [Fact]
    public void Parse_BasicPolygonStyle_ParsesCorrectly()
    {
        var css = @"
* {
  fill: #4A90E2;
  stroke: #1F364D;
  stroke-width: 1.5;
}";

        var style = CssParser.Parse(css, "test-style");

        style.Should().NotBeNull();
        style.Id.Should().Be("test-style");
        style.Format.Should().Be("css");
        style.GeometryType.Should().Be("polygon");
        style.Rules.Should().HaveCount(1);

        var rule = style.Rules[0];
        rule.IsDefault.Should().BeTrue();
        rule.Symbolizer.FillColor.Should().Be("#4A90E2");
        rule.Symbolizer.StrokeColor.Should().Be("#1F364D");
        rule.Symbolizer.StrokeWidth.Should().Be(1.5);
    }

    [Fact]
    public void Parse_LineStyle_InfersLineGeometry()
    {
        var css = @"
* {
  stroke: #FF5733;
  stroke-width: 2;
}";

        var style = CssParser.Parse(css, "line-style");

        style.GeometryType.Should().Be("line");
        style.Rules[0].Symbolizer.StrokeColor.Should().Be("#FF5733");
        style.Rules[0].Symbolizer.StrokeWidth.Should().Be(2);
    }

    [Fact]
    public void Parse_PointStyleWithMark_InfersPointGeometry()
    {
        var css = @"
* {
  mark: url('icon.png');
  mark-size: 16;
}";

        var style = CssParser.Parse(css, "point-style");

        style.GeometryType.Should().Be("point");
        style.Rules[0].Symbolizer.IconHref.Should().Be("icon.png");
        style.Rules[0].Symbolizer.Size.Should().Be(16);
    }

    [Fact]
    public void Parse_AttributeFilter_ParsesCorrectly()
    {
        var css = @"
[category = 'residential'] {
  fill: #90EE90;
  stroke: #228B22;
}";

        var style = CssParser.Parse(css, "filtered-style");

        style.Rules.Should().HaveCount(1);
        var rule = style.Rules[0];
        rule.Filter.Should().NotBeNull();
        rule.Filter!.Field.Should().Be("category");
        rule.Filter.Value.Should().Be("residential");
    }

    [Fact]
    public void Parse_ScaleFilters_ParsesCorrectly()
    {
        var css = @"
[@scale > 100000][@scale < 500000] {
  fill: #FFD700;
}";

        var style = CssParser.Parse(css, "scale-style");

        style.Rules.Should().HaveCount(1);
        var rule = style.Rules[0];
        rule.MinScale.Should().Be(100000);
        rule.MaxScale.Should().Be(500000);
    }

    [Fact]
    public void Parse_MultipleRules_ParsesAll()
    {
        var css = @"
[type = 'water'] {
  fill: #0077BE;
  stroke: #003D5C;
}

[type = 'forest'] {
  fill: #228B22;
  stroke: #006400;
}

* {
  fill: #CCCCCC;
}";

        var style = CssParser.Parse(css, "multi-rule-style");

        style.Rules.Should().HaveCount(3);
        style.Rules[0].Filter!.Value.Should().Be("water");
        style.Rules[0].Symbolizer.FillColor.Should().Be("#0077BE");
        style.Rules[1].Filter!.Value.Should().Be("forest");
        style.Rules[1].Symbolizer.FillColor.Should().Be("#228B22");
        style.Rules[2].IsDefault.Should().BeTrue();
        style.Rules[2].Symbolizer.FillColor.Should().Be("#CCCCCC");
    }

    [Fact]
    public void Parse_WithOpacity_ParsesCorrectly()
    {
        var css = @"
* {
  fill: #FF5733;
  fill-opacity: 0.7;
}";

        var style = CssParser.Parse(css, "opacity-style");

        style.Rules[0].Symbolizer.FillColor.Should().Be("#FF5733");
        style.Rules[0].Symbolizer.Opacity.Should().Be(0.7);
    }

    [Fact]
    public void Parse_WithSizeUnits_StripsUnits()
    {
        var css = @"
* {
  stroke-width: 3px;
  mark-size: 20px;
}";

        var style = CssParser.Parse(css, "units-style");

        style.Rules[0].Symbolizer.StrokeWidth.Should().Be(3);
        style.Rules[0].Symbolizer.Size.Should().Be(20);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        var act = () => CssParser.Parse("", "test");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyStyleId_ThrowsArgumentException()
    {
        var act = () => CssParser.Parse("* { fill: #000; }", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyCssBlock_CreatesDefaultRule()
    {
        var css = "/* just a comment */";

        var style = CssParser.Parse(css, "empty-style");

        style.Rules.Should().HaveCount(1);
        style.Rules[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Parse_ComplexPolygonStyle_ParsesAll()
    {
        var css = @"
[landuse = 'park'][@scale > 50000][@scale < 200000] {
  fill: #90EE90;
  fill-opacity: 0.8;
  stroke: #228B22;
  stroke-width: 2;
}";

        var style = CssParser.Parse(css, "complex-style");

        var rule = style.Rules[0];
        rule.Filter!.Field.Should().Be("landuse");
        rule.Filter.Value.Should().Be("park");
        rule.MinScale.Should().Be(50000);
        rule.MaxScale.Should().Be(200000);
        rule.Symbolizer.FillColor.Should().Be("#90EE90");
        rule.Symbolizer.Opacity.Should().Be(0.8);
        rule.Symbolizer.StrokeColor.Should().Be("#228B22");
        rule.Symbolizer.StrokeWidth.Should().Be(2);
    }
}
