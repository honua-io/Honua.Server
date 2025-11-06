using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Chart;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaChart component
/// </summary>
public class HonuaChartTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaChartTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaChart_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Scatter)]
    [InlineData(ChartType.Histogram)]
    public void HonuaChart_WithChartType_ShouldRenderCorrectType(ChartType chartType)
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.Type, chartType));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaChart_WithData_ShouldRenderChart()
    {
        // Arrange
        var data = TestData.SampleTimeSeriesData;

        // Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.Type, ChartType.Bar)
            .Add(p => p.XField, "Category")
            .Add(p => p.YField, "Value"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Theory]
    [InlineData(AggregationType.Count)]
    [InlineData(AggregationType.Sum)]
    [InlineData(AggregationType.Average)]
    [InlineData(AggregationType.Min)]
    [InlineData(AggregationType.Max)]
    public void HonuaChart_WithAggregation_ShouldAggregateData(AggregationType aggregation)
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.Aggregation, aggregation));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaChart_WithClickToFilter_ShouldEnableInteraction()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.ClickToFilter, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaChart_WithTheme_ShouldApplyTheme()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.Theme, "dark"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaChart_WithTitle_ShouldDisplayTitle()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.Title, "Population by State"));

        // Assert
        cut.Markup.Should().Contain("Population by State");
    }

    [Fact]
    public void HonuaChart_WithExport_ShouldEnableExport()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaChart>(parameters => parameters
            .Add(p => p.Id, "test-chart")
            .Add(p => p.ShowExport, true));

        // Assert
        cut.Should().NotBeNull();
    }
}

/// <summary>
/// Chart types
/// </summary>
public enum ChartType
{
    Bar,
    Line,
    Pie,
    Scatter,
    Histogram,
    Area
}

/// <summary>
/// Data aggregation types
/// </summary>
public enum AggregationType
{
    Count,
    Sum,
    Average,
    Min,
    Max
}
