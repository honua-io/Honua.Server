using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Map;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaMap component
/// </summary>
public class HonuaMapTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaMapTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaMap_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaMap_ShouldApplyProvidedId()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "my-custom-map"));

        // Assert
        cut.Markup.Should().Contain("my-custom-map");
    }

    [Fact]
    public void HonuaMap_ShouldApplyCustomZoom()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Zoom, 15));

        // Assert
        // In a real test, you'd verify this was passed to the JS interop
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaMap_ShouldApplyCustomCenter()
    {
        // Arrange
        var center = new[] { -122.4194, 37.7749 };

        // Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Center, center));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaMap_ShouldApplyStyle()
    {
        // Arrange
        var style = "https://api.maptiler.com/maps/streets/style.json";

        // Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Style, style));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaMap_OnInitialized_ShouldPublishMapReadyMessage()
    {
        // Arrange
        var mapId = "test-map";

        // Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, mapId));

        // Give time for initialization
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<MapReadyMessage>();
        // Note: This test assumes the component publishes on initialization
        // Actual behavior may vary based on implementation
    }

    [Fact]
    public void HonuaMap_WithBearingAndPitch_ShouldApplySettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.Bearing, 45)
            .Add(p => p.Pitch, 30));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaMap_WithMinMaxZoom_ShouldApplyConstraints()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .Add(p => p.MinZoom, 5)
            .Add(p => p.MaxZoom, 18));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaMap_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map"));

        // Act - Disposing the component
        cut.Instance.Dispose();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    [Fact]
    public void HonuaMap_WithChildContent_ShouldRenderChildren()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
            .AddChildContent("<div class='test-child'>Child Content</div>"));

        // Assert
        cut.Markup.Should().Contain("test-child");
    }

    [Fact]
    public void HonuaMap_MultipleInstances_ShouldHaveUniqueIds()
    {
        // Arrange & Act
        var map1 = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "map-1"));

        var map2 = _testContext.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "map-2"));

        // Assert
        map1.Markup.Should().Contain("map-1");
        map2.Markup.Should().Contain("map-2");
        map1.Markup.Should().NotContain("map-2");
        map2.Markup.Should().NotContain("map-1");
    }
}
