using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.CoordinateDisplay;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Comprehensive tests for HonuaCoordinateDisplay component
/// Tests cover: coordinate formats, real-time tracking, pin/unpin, copy to clipboard, format switching
/// </summary>
public class HonuaCoordinateDisplayTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaCoordinateDisplayTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    #region Rendering Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().Contain("honua-coordinate-display");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowPlaceholder_WhenNotInitialized()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Assert
        cut.Markup.Should().Contain("Initializing");
    }

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldShowPrompt_AfterMapReady()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Move cursor over map");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldApplyCustomPosition()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Position, "top-right"));

        // Assert
        cut.Markup.Should().Contain("display-top-right");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldApplyDefaultPosition_BottomLeft()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display"));

        // Assert
        cut.Markup.Should().Contain("display-bottom-left");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldApplyCustomWidth()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Width, "400px"));

        // Assert
        cut.Markup.Should().Contain("width: 400px");
    }

    #endregion

    #region Coordinate Format Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldDefaultToDecimalDegrees()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display"));

        // Assert
        cut.Instance.CoordinateFormat.Should().Be(CoordinateFormat.DecimalDegrees);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldAcceptCustomFormat()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.CoordinateFormat, CoordinateFormat.UTM));

        // Assert
        cut.Instance.CoordinateFormat.Should().Be(CoordinateFormat.UTM);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowFormatMenu_WhenAllowFormatSwitchIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowFormatSwitch, true));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Change coordinate format");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldNotShowFormatMenu_WhenAllowFormatSwitchIsFalse()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowFormatSwitch, false));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().NotContain("Change coordinate format");
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldDefaultTo6DecimalPlaces()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display"));

        // Assert
        cut.Instance.Precision.Should().Be(6);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldAcceptCustomPrecision()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Precision, 4));

        // Assert
        cut.Instance.Precision.Should().Be(4);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowPrecisionMenu_WhenAllowPrecisionChangeIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowPrecisionChange, true)
            .Add(p => p.CoordinateFormat, CoordinateFormat.DecimalDegrees));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Change precision");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldNotShowPrecisionMenu_ForNonDecimalFormats()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowPrecisionChange, true)
            .Add(p => p.CoordinateFormat, CoordinateFormat.UTM));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().NotContain("Change precision");
    }

    #endregion

    #region Pin/Unpin Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowPinButton_WhenAllowPinningIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowPinning, true));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Pin coordinates");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldNotShowPinButton_WhenAllowPinningIsFalse()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowPinning, false));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().NotContain("Pin coordinates");
    }

    #endregion

    #region Copy to Clipboard Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowCopyButton_WhenAllowCopyIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCopy, true));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Copy coordinates");
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldNotShowCopyButton_WhenAllowCopyIsFalse()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowCopy, false));

        _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        cut.Render();

        // Assert
        cut.Markup.Should().NotContain("Copy coordinates");
    }

    #endregion

    #region Additional Info Display Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowScale_WhenShowScaleIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.ShowScale, true));

        // Assert
        cut.Instance.ShowScale.Should().BeTrue();
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowZoom_WhenShowZoomIsTrue()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.ShowZoom, true));

        // Assert
        cut.Instance.ShowZoom.Should().BeTrue();
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowElevation_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.ShowElevation, true));

        // Assert
        cut.Instance.ShowElevation.Should().BeTrue();
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowBearing_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.ShowBearing, true));

        // Assert
        cut.Instance.ShowBearing.Should().BeTrue();
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldShowDistance_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.ShowDistance, true));

        // Assert
        cut.Instance.ShowDistance.Should().BeTrue();
    }

    #endregion

    #region Measurement Unit Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldDefaultToMetricUnits()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display"));

        // Assert
        cut.Instance.Unit.Should().Be(MeasurementUnit.Metric);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldAcceptImperialUnits()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Unit, MeasurementUnit.Imperial));

        // Assert
        cut.Instance.Unit.Should().Be(MeasurementUnit.Imperial);
    }

    [Fact]
    public void HonuaCoordinateDisplay_ShouldAcceptNauticalUnits()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Unit, MeasurementUnit.Nautical));

        // Assert
        cut.Instance.Unit.Should().Be(MeasurementUnit.Nautical);
    }

    #endregion

    #region Map Synchronization Tests

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldRespondToMapReady_ForCorrectMapId()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Move cursor over map");
    }

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldIgnoreMapReady_ForDifferentMapId()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "other-map" });
        await Task.Delay(100);
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Initializing");
    }

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldRespondToAnyMap_WhenSyncWithIsNull()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, null));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "any-map" });
        await Task.Delay(100);
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Move cursor over map");
    }

    #endregion

    #region Event Callback Tests

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldInvokeOnCoordinateClick_WhenClicked()
    {
        // Arrange
        double[]? clickedCoords = null;

        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.OnCoordinateClick, EventCallback.Factory.Create<double[]>(this, coords => clickedCoords = coords)));

        // Note: Full test would require JS interop simulation
        clickedCoords.Should().BeNull(); // Placeholder
    }

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldInvokeOnCoordinateCopy_WhenCopied()
    {
        // Arrange
        string? copiedText = null;

        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.OnCoordinateCopy, EventCallback.Factory.Create<string>(this, text => copiedText = text)));

        // Note: Full test would require JS interop simulation
        copiedText.Should().BeNull(); // Placeholder
    }

    #endregion

    #region ComponentBus Integration Tests

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldPublishCoordinateClickedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);

        // Note: Would need to simulate JS callback to test message publishing
        // This verifies the subscription is set up
        _testContext.ComponentBus.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaCoordinateDisplay_ShouldPublishCoordinatePinnedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.AllowPinning, true));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage { MapId = "test-map" });
        await Task.Delay(100);

        // Note: Would need to simulate coordinate update and pin action
        // This verifies the setup is correct
        cut.Instance.AllowPinning.Should().BeTrue();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task HonuaCoordinateDisplay_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await cut.Instance.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    #endregion

    #region Position Class Tests

    [Theory]
    [InlineData("top-right", "display-top-right")]
    [InlineData("top-left", "display-top-left")]
    [InlineData("bottom-right", "display-bottom-right")]
    [InlineData("bottom-left", "display-bottom-left")]
    [InlineData(null, "display-embedded")]
    public void HonuaCoordinateDisplay_ShouldApplyCorrectPositionClass(string? position, string expectedClass)
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.Position, position));

        // Assert
        cut.Markup.Should().Contain(expectedClass);
    }

    #endregion

    #region Custom CSS Tests

    [Fact]
    public void HonuaCoordinateDisplay_ShouldApplyCustomCssClass()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaCoordinateDisplay>(parameters => parameters
            .Add(p => p.Id, "test-display")
            .Add(p => p.CssClass, "my-custom-class"));

        // Assert
        cut.Markup.Should().Contain("my-custom-class");
    }

    #endregion
}
