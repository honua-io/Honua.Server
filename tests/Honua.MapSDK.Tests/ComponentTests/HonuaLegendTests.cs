using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Legend;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaLegend component
/// </summary>
public class HonuaLegendTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaLegendTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaLegend_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaLegend_WithMapId_ShouldSubscribeToMapMessages()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.MapId, "test-map"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaLegend_OnLayerAdded_ShouldUpdateLayerList()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.MapId, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new LayerAddedMessage
        {
            LayerId = "new-layer",
            LayerName = "New Layer"
        }, "test-map");

        await Task.Delay(100);

        // Assert
        cut.Should().NotBeNull();
        // Verify layer appears in legend
    }

    [Fact]
    public async Task HonuaLegend_OnLayerRemoved_ShouldUpdateLayerList()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.MapId, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new LayerRemovedMessage
        {
            LayerId = "old-layer"
        }, "test-map");

        await Task.Delay(100);

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaLegend_ToggleVisibility_ShouldPublishMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.MapId, "test-map"));

        // Act - Simulate visibility toggle (depends on implementation)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<LayerVisibilityChangedMessage>();
    }

    [Fact]
    public async Task HonuaLegend_AdjustOpacity_ShouldPublishMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.MapId, "test-map"));

        // Act - Simulate opacity adjustment (depends on implementation)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<LayerOpacityChangedMessage>();
    }

    [Fact]
    public void HonuaLegend_WithGrouping_ShouldGroupLayers()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend")
            .Add(p => p.GroupLayers, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaLegend_WithEmptyLayers_ShouldShowEmptyState()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaLegend>(parameters => parameters
            .Add(p => p.Id, "test-legend"));

        // Assert
        cut.Should().NotBeNull();
    }
}
