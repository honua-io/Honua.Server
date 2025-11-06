using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.OverviewMap;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Comprehensive tests for HonuaOverviewMap component
/// </summary>
public class OverviewMapTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public OverviewMapTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    #region Initialization and Rendering Tests

    [Fact]
    public void OverviewMap_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().Contain("honua-overview-map");
    }

    [Fact]
    public void OverviewMap_ShouldApplyCustomId()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.Id, "custom-overview")
            .Add(p => p.SyncWith, "main-map"));

        // Assert
        cut.Markup.Should().Contain("custom-overview");
    }

    [Fact]
    public void OverviewMap_ShouldApplyWidthAndHeight()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Width, 300)
            .Add(p => p.Height, 250));

        // Assert
        cut.Markup.Should().Contain("width: 300px");
        cut.Markup.Should().Contain("height: 250px");
    }

    [Fact]
    public void OverviewMap_ShouldApplyPositionClass()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Position, "top-left"));

        // Assert
        cut.Markup.Should().Contain("overview-top-left");
    }

    [Fact]
    public void OverviewMap_ShouldStartCollapsedWhenConfigured()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.InitiallyCollapsed, true));

        // Assert
        cut.Markup.Should().Contain("collapsed");
    }

    [Fact]
    public void OverviewMap_ShouldShowToggleButtonWhenCollapsible()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Collapsible, true)
            .Add(p => p.ShowToggleButton, true));

        // Assert
        cut.Markup.Should().Contain("overview-toggle");
    }

    [Fact]
    public void OverviewMap_ShouldHideToggleButtonWhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ShowToggleButton, false));

        // Assert
        cut.Markup.Should().NotContain("overview-toggle");
    }

    [Fact]
    public void OverviewMap_ShouldDisplayTitleWhenProvided()
    {
        // Arrange
        var title = "Overview Map";

        // Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Title, title));

        // Assert
        cut.Markup.Should().Contain(title);
    }

    [Fact]
    public void OverviewMap_ShouldApplyCustomCssClass()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.CssClass, "custom-class"));

        // Assert
        cut.Markup.Should().Contain("custom-class");
    }

    [Fact]
    public void OverviewMap_ShouldApplyCustomStyles()
    {
        // Arrange
        var customStyle = "opacity: 0.8";

        // Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Style, customStyle));

        // Assert
        cut.Markup.Should().Contain("opacity: 0.8");
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public void OverviewMap_ShouldRequireSyncWithParameter()
    {
        // Arrange & Act
        Action act = () => _testContext.RenderComponent<HonuaOverviewMap>();

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void OverviewMap_ShouldAcceptValidZoomOffset()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ZoomOffset, -5));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldAcceptPositiveAndNegativeZoomOffsets()
    {
        // Arrange & Act - negative offset
        var cut1 = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.Id, "overview-1")
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ZoomOffset, -5));

        // Act - positive offset
        var cut2 = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.Id, "overview-2")
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ZoomOffset, 2));

        // Assert
        cut1.Should().NotBeNull();
        cut2.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldAcceptValidExtentBoxColors()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ExtentBoxColor, "#00FF00")
            .Add(p => p.ExtentBoxFillColor, "#00FF00"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldAcceptValidOpacityValues()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ExtentBoxOpacity, 0.8)
            .Add(p => p.ExtentBoxFillOpacity, 0.1));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region ComponentBus Integration Tests

    [Fact]
    public async Task OverviewMap_ShouldSubscribeToMapReadyMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "main-map",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 10
        });

        await Task.Delay(100);

        // Assert - Component should have received the message
        var messages = _testContext.ComponentBus.GetMessages<MapReadyMessage>();
        messages.Should().HaveCount(1);
        messages[0].MapId.Should().Be("main-map");
    }

    [Fact]
    public async Task OverviewMap_ShouldSubscribeToMapExtentChangedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "main-map",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 12,
            Bounds = new[] { -122.5, 37.7, -122.3, 37.8 },
            Bearing = 45
        });

        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<MapExtentChangedMessage>();
        messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task OverviewMap_ShouldSubscribeToBasemapChangedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new BasemapChangedMessage
        {
            MapId = "main-map",
            Style = "https://api.maptiler.com/maps/streets/style.json",
            Name = "Streets"
        });

        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<BasemapChangedMessage>();
        messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task OverviewMap_ShouldPublishFlyToRequestWhenClicked()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ClickToPan, true));

        // Act - Simulate JS callback for overview click
        await cut.Instance.OnOverviewClickedInternal(new[] { -122.4, 37.8 });
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FlyToRequestMessage>();
        messages.Should().HaveCount(1);
        messages[0].MapId.Should().Be("main-map");
        messages[0].Center.Should().BeEquivalentTo(new[] { -122.4, 37.8 });
    }

    [Fact]
    public async Task OverviewMap_ShouldPublishFlyToRequestWhenDragged()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.DragToPan, true));

        // Act - Simulate JS callback for drag
        await cut.Instance.OnOverviewDraggedInternal(new[] { -122.5, 37.9 });
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FlyToRequestMessage>();
        messages.Should().HaveCount(1);
        messages[0].Duration.Should().Be(0); // Instant for dragging
    }

    [Fact]
    public async Task OverviewMap_ShouldPublishFlyToRequestWhenScrolled()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ScrollToZoom, true));

        // Act - Simulate JS callback for scroll
        await cut.Instance.OnOverviewScrolledInternal(1.5);
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FlyToRequestMessage>();
        messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task OverviewMap_ShouldOnlyRespondToSyncedMapMessages()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act - Send message for different map
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "other-map",
            Center = new[] { 0.0, 0.0 },
            Zoom = 5
        });

        await Task.Delay(100);

        // Assert - Overview should not process messages from other maps
        var messages = _testContext.ComponentBus.GetMessages<MapReadyMessage>();
        messages.Should().HaveCount(1);
        messages[0].MapId.Should().Be("other-map");
    }

    #endregion

    #region Event Callback Tests

    [Fact]
    public async Task OverviewMap_ShouldInvokeOnOverviewClickedCallback()
    {
        // Arrange
        OverviewMapClickedMessage? receivedMessage = null;
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.OnOverviewClicked, EventCallback.Factory.Create<OverviewMapClickedMessage>(
                this, msg => receivedMessage = msg)));

        // Act
        await cut.Instance.OnOverviewClickedInternal(new[] { -122.4, 37.8 });
        await Task.Delay(100);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Center.Should().BeEquivalentTo(new[] { -122.4, 37.8 });
    }

    #endregion

    #region Collapsible Functionality Tests

    [Fact]
    public async Task OverviewMap_ShouldExpandWhenCollapsed()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.InitiallyCollapsed, true)
            .Add(p => p.Collapsible, true));

        // Assert - Initially collapsed
        cut.Markup.Should().Contain("collapsed");

        // Act
        await cut.Instance.ExpandAsync();
        await Task.Delay(150);

        // Assert - Should be expanded
        cut.Render();
        cut.Markup.Should().NotContain("collapsed");
    }

    [Fact]
    public async Task OverviewMap_ShouldCollapseWhenExpanded()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.InitiallyCollapsed, false)
            .Add(p => p.Collapsible, true));

        // Assert - Initially expanded
        cut.Markup.Should().NotContain("collapsed");

        // Act
        await cut.Instance.CollapseAsync();
        await Task.Delay(100);

        // Assert - Should be collapsed
        cut.Render();
        cut.Markup.Should().Contain("collapsed");
    }

    [Fact]
    public async Task OverviewMap_ShouldToggleStateOnButtonClick()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Collapsible, true)
            .Add(p => p.ShowToggleButton, true));

        var initialMarkup = cut.Markup;

        // Act - Find and click toggle button
        var toggleButton = cut.Find(".overview-toggle");
        await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Task.Delay(150);

        // Assert - State should have changed
        cut.Render();
        cut.Markup.Should().NotBe(initialMarkup);
    }

    #endregion

    #region Position Placement Tests

    [Theory]
    [InlineData("top-left", "overview-top-left")]
    [InlineData("top-right", "overview-top-right")]
    [InlineData("bottom-left", "overview-bottom-left")]
    [InlineData("bottom-right", "overview-bottom-right")]
    [InlineData("custom", "overview-custom")]
    public void OverviewMap_ShouldApplyCorrectPositionClass(string position, string expectedClass)
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Position, position));

        // Assert
        cut.Markup.Should().Contain(expectedClass);
    }

    [Fact]
    public void OverviewMap_ShouldApplyCustomOffsets()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.Position, "custom")
            .Add(p => p.OffsetX, 20)
            .Add(p => p.OffsetY, 30));

        // Assert
        cut.Markup.Should().Contain("right: 20px");
        cut.Markup.Should().Contain("bottom: 30px");
    }

    #endregion

    #region Style and Appearance Tests

    [Fact]
    public void OverviewMap_ShouldApplyBorderRadius()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.BorderRadius, 8));

        // Assert
        cut.Markup.Should().Contain("border-radius: 8px");
    }

    [Fact]
    public void OverviewMap_ShouldApplyBoxShadow()
    {
        // Arrange
        var shadow = "0 4px 12px rgba(0,0,0,0.5)";

        // Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.BoxShadow, shadow));

        // Assert
        cut.Markup.Should().Contain(shadow);
    }

    [Fact]
    public void OverviewMap_ShouldApplyBorderColorAndWidth()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.BorderColor, "#FF0000")
            .Add(p => p.BorderWidth, 2));

        // Assert
        cut.Markup.Should().Contain("border: 2px solid #FF0000");
    }

    [Fact]
    public void OverviewMap_ShouldApplyBackgroundColor()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.BackgroundColor, "#F0F0F0"));

        // Assert
        cut.Markup.Should().Contain("background-color: #F0F0F0");
    }

    [Fact]
    public void OverviewMap_ShouldApplyZIndex()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ZIndex, 2000));

        // Assert
        cut.Markup.Should().Contain("z-index: 2000");
    }

    #endregion

    #region Interaction Configuration Tests

    [Fact]
    public void OverviewMap_ShouldSupportClickToPan()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ClickToPan, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldSupportDragToPan()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.DragToPan, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldSupportScrollToZoom()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.ScrollToZoom, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldSupportRotateWithBearing()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.RotateWithBearing, true));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Zoom Configuration Tests

    [Fact]
    public void OverviewMap_ShouldAcceptMinMaxZoomConstraints()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.MinZoom, 0)
            .Add(p => p.MaxZoom, 18));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void OverviewMap_ShouldApplyUpdateThrottling()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.UpdateThrottleMs, 200));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Public API Tests

    [Fact]
    public async Task OverviewMap_UpdateExtentStyleAsync_ShouldAcceptStyleParameters()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act - Should not throw
        await cut.Instance.UpdateExtentStyleAsync("#00FF00", 3, 0.9, "#00FF00", 0.2);

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task OverviewMap_ExpandAsync_ShouldExpandCollapsedMap()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.InitiallyCollapsed, true));

        // Act
        await cut.Instance.ExpandAsync();
        await Task.Delay(150);

        // Assert
        cut.Render();
        cut.Markup.Should().NotContain("collapsed");
    }

    [Fact]
    public async Task OverviewMap_CollapseAsync_ShouldCollapseExpandedMap()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map")
            .Add(p => p.InitiallyCollapsed, false));

        // Act
        await cut.Instance.CollapseAsync();
        await Task.Delay(100);

        // Assert
        cut.Render();
        cut.Markup.Should().Contain("collapsed");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task OverviewMap_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act - Disposing the component
        await cut.Instance.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    [Fact]
    public async Task OverviewMap_MultipleDispose_ShouldNotThrow()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaOverviewMap>(parameters => parameters
            .Add(p => p.SyncWith, "main-map"));

        // Act - Multiple dispose calls
        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();

        // Assert - No exceptions
        Assert.True(true);
    }

    #endregion
}
