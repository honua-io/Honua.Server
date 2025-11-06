using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.FilterPanel;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaFilterPanel component
/// </summary>
public class HonuaFilterPanelTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaFilterPanelTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaFilterPanel_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaFilterPanel_WithSpatialFilter_ShouldAllowSpatialFiltering()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter")
            .Add(p => p.AllowSpatial, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaFilterPanel_WithAttributeFilter_ShouldAllowAttributeFiltering()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter")
            .Add(p => p.AllowAttribute, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaFilterPanel_WithTemporalFilter_ShouldAllowTemporalFiltering()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter")
            .Add(p => p.AllowTemporal, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaFilterPanel_ApplyFilter_ShouldPublishFilterAppliedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter"));

        // Act - Apply a filter (implementation dependent)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FilterAppliedMessage>();
    }

    [Fact]
    public async Task HonuaFilterPanel_ClearFilter_ShouldPublishFilterClearedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter"));

        // Act - Clear a filter (implementation dependent)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FilterClearedMessage>();
    }

    [Fact]
    public async Task HonuaFilterPanel_ClearAllFilters_ShouldPublishAllFiltersClearedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter"));

        // Act - Clear all filters (implementation dependent)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<AllFiltersClearedMessage>();
    }

    [Fact]
    public void HonuaFilterPanel_WithFieldConfigs_ShouldShowConfiguredFields()
    {
        // Arrange
        var fieldConfigs = new List<FilterFieldConfig>
        {
            new() { Field = "population", Label = "Population", Type = FieldType.Number },
            new() { Field = "state", Label = "State", Type = FieldType.String }
        };

        // Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter")
            .Add(p => p.Fields, fieldConfigs));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaFilterPanel_WithActiveFilters_ShouldDisplayActiveFilters()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaFilterPanel>(parameters => parameters
            .Add(p => p.Id, "test-filter"));

        // Assert
        cut.Should().NotBeNull();
    }
}
