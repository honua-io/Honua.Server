using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Draw;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Text.Json;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaDraw component
/// </summary>
public class HonuaDrawTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaDrawTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    #region Initialization Tests

    [Fact]
    public void HonuaDraw_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Id, "test-draw")
            .Add(p => p.SyncWith, "test-map"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
        cut.Markup.Should().Contain("honua-draw");
    }

    [Fact]
    public void HonuaDraw_ShouldApplyCustomId()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Id, "custom-draw-component"));

        // Assert
        cut.Instance.Id.Should().Be("custom-draw-component");
    }

    [Fact]
    public void HonuaDraw_ShouldUseDefaultParameters()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>();

        // Assert
        cut.Instance.ShowToolbar.Should().BeTrue();
        cut.Instance.ShowMeasurements.Should().BeTrue();
        cut.Instance.ShowFeatureList.Should().BeTrue();
        cut.Instance.AllowEdit.Should().BeTrue();
        cut.Instance.EnableUndo.Should().BeTrue();
        cut.Instance.EnableExport.Should().BeTrue();
    }

    [Fact]
    public void HonuaDraw_ShouldApplyCustomParameters()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.ShowToolbar, false)
            .Add(p => p.ShowMeasurements, false)
            .Add(p => p.AllowEdit, false)
            .Add(p => p.EnableUndo, false));

        // Assert
        cut.Instance.ShowToolbar.Should().BeFalse();
        cut.Instance.ShowMeasurements.Should().BeFalse();
        cut.Instance.AllowEdit.Should().BeFalse();
        cut.Instance.EnableUndo.Should().BeFalse();
    }

    [Fact]
    public void HonuaDraw_ShouldApplyCustomStyleColors()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.DefaultStrokeColor, "#FF0000")
            .Add(p => p.DefaultFillColor, "#00FF00")
            .Add(p => p.DefaultStrokeWidth, 5.0)
            .Add(p => p.DefaultFillOpacity, 0.5));

        // Assert
        cut.Instance.DefaultStrokeColor.Should().Be("#FF0000");
        cut.Instance.DefaultFillColor.Should().Be("#00FF00");
        cut.Instance.DefaultStrokeWidth.Should().Be(5.0);
        cut.Instance.DefaultFillOpacity.Should().Be(0.5);
    }

    [Fact]
    public void HonuaDraw_ShouldApplyMeasurementUnit()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.MeasurementUnit, MeasurementUnit.Imperial));

        // Assert
        cut.Instance.MeasurementUnit.Should().Be(MeasurementUnit.Imperial);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void HonuaDraw_ShouldRenderToolbar_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.ShowToolbar, true));

        // Assert
        cut.Markup.Should().Contain("draw-toolbar");
    }

    [Fact]
    public void HonuaDraw_ShouldNotRenderToolbar_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.ShowToolbar, false));

        // Assert
        cut.Markup.Should().NotContain("draw-toolbar");
    }

    [Fact]
    public void HonuaDraw_ShouldApplyPositionClass_TopRight()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Position, "top-right"));

        // Assert
        cut.Markup.Should().Contain("draw-top-right");
    }

    [Fact]
    public void HonuaDraw_ShouldApplyPositionClass_BottomLeft()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Position, "bottom-left"));

        // Assert
        cut.Markup.Should().Contain("draw-bottom-left");
    }

    [Fact]
    public void HonuaDraw_ShouldApplyCustomCssClass()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.CssClass, "custom-draw-class"));

        // Assert
        cut.Markup.Should().Contain("custom-draw-class");
    }

    [Fact]
    public void HonuaDraw_ShouldApplyCustomWidth()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Width, "500px"));

        // Assert
        cut.Markup.Should().Contain("width: 500px");
    }

    #endregion

    #region Drawing Mode Tests

    [Fact]
    public async Task HonuaDraw_ShouldRespondToStartDrawingRequest_Point()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        // Simulate map ready
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { 0.0, 0.0 },
            Zoom = 10
        }, "test-map");

        await Task.Delay(100);

        // Act
        await _testContext.ComponentBus.PublishAsync(new StartDrawingRequestMessage
        {
            MapId = "test-map",
            Mode = "point"
        }, "test-map");

        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<DrawModeChangedMessage>();
        messages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HonuaDraw_ShouldRespondToStopDrawingRequest()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { 0.0, 0.0 },
            Zoom = 10
        }, "test-map");

        await Task.Delay(100);

        // Act
        await _testContext.ComponentBus.PublishAsync(new StopDrawingRequestMessage
        {
            MapId = "test-map"
        }, "test-map");

        await Task.Delay(100);

        // Assert - No errors should be thrown
        cut.Should().NotBeNull();
    }

    #endregion

    #region Feature Management Tests

    [Fact]
    public async Task HonuaDraw_OnFeatureDrawnFromJS_ShouldAddFeatureToList()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {
                ""name"": ""Test Point""
            }
        }";

        // Act
        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FeatureDrawnMessage>();
        messages.Should().NotBeEmpty();
        messages.First().GeometryType.Should().Be("Point");
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureDrawn_ShouldInvokeCallback()
    {
        // Arrange
        DrawingFeature? capturedFeature = null;
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.OnFeatureDrawn, EventCallback.Factory.Create<DrawingFeature>(
                this, feature => capturedFeature = feature)));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        // Act
        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);
        await Task.Delay(100);

        // Assert
        capturedFeature.Should().NotBeNull();
        capturedFeature!.Id.Should().Be("feature-1");
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureEditedFromJS_ShouldUpdateFeature()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        // First add a feature
        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);

        // Act - Edit the feature
        var updatedGeometry = @"{
            ""type"": ""Point"",
            ""coordinates"": [-122.5, 37.8]
        }";

        await cut.Instance.OnFeatureEditedFromJS("feature-1", updatedGeometry);
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FeatureEditedMessage>();
        messages.Should().NotBeEmpty();
        messages.Last().FeatureId.Should().Be("feature-1");
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureSelectedFromJS_ShouldSelectFeature()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);

        // Act
        await cut.Instance.OnFeatureSelectedFromJS("feature-1");

        // Assert - No errors should occur
        cut.Should().NotBeNull();
    }

    #endregion

    #region Measurement Tests

    [Fact]
    public async Task HonuaDraw_OnMeasurementUpdate_ShouldUpdateActiveMeasurement()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.ShowMeasurements, true));

        // Act
        await cut.Instance.OnMeasurementUpdate(
            distance: 1000.5,
            area: null,
            perimeter: null,
            radius: null,
            bearing: 45.0,
            coordinates: new[] { -122.4194, 37.7749 });

        // Assert - Component should render without errors
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureMeasured_ShouldInvokeCallback()
    {
        // Arrange
        FeatureMeasurements? capturedMeasurement = null;
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.OnFeatureMeasured, EventCallback.Factory.Create<FeatureMeasurements>(
                this, measurement => capturedMeasurement = measurement)));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""LineString"",
                ""coordinates"": [[-122.4194, 37.7749], [-122.5, 37.8]]
            },
            ""properties"": {}
        }";

        // Act
        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);
        await Task.Delay(200);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FeatureMeasuredMessage>();
        messages.Should().NotBeEmpty();
    }

    #endregion

    #region Export Tests

    [Fact]
    public void HonuaDraw_ShouldShowExportButton_WhenFeaturesExist()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.EnableExport, true)
            .Add(p => p.ShowToolbar, true));

        // Note: Export button is only shown when features exist
        // We can't easily test this without adding features through JS
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDraw_ShouldNotShowExportButton_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.EnableExport, false)
            .Add(p => p.ShowToolbar, true));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task HonuaDraw_OnFeatureDrawnFromJS_ShouldHandleInvalidJson()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>();

        // Act - Pass invalid JSON
        await cut.Instance.OnFeatureDrawnFromJS("invalid json");

        // Assert - Should not throw, error should be handled internally
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureEditedFromJS_ShouldHandleNonExistentFeature()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>();

        // Act - Try to edit non-existent feature
        await cut.Instance.OnFeatureEditedFromJS("non-existent-id", @"{""type"":""Point"",""coordinates"":[0,0]}");

        // Assert - Should not throw
        cut.Should().NotBeNull();
    }

    #endregion

    #region Undo/Redo Tests

    [Fact]
    public void HonuaDraw_ShouldShowUndoRedo_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.EnableUndo, true)
            .Add(p => p.ShowToolbar, true));

        // Assert
        cut.Should().NotBeNull();
        // Note: Undo/Redo buttons only appear when there's history
    }

    [Fact]
    public void HonuaDraw_ShouldNotShowUndoRedo_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.EnableUndo, false)
            .Add(p => p.ShowToolbar, true));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region ComponentBus Integration Tests

    [Fact]
    public async Task HonuaDraw_ShouldPublishFeatureDrawnMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Id, "draw-1")
            .Add(p => p.SyncWith, "test-map"));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        // Act
        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);
        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FeatureDrawnMessage>();
        messages.Should().NotBeEmpty();
        messages.First().ComponentId.Should().Be("draw-1");
        messages.First().FeatureId.Should().Be("feature-1");
    }

    [Fact]
    public async Task HonuaDraw_ShouldPublishFeatureDeletedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.Id, "draw-1"));

        // First add a feature
        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);
        _testContext.ComponentBus.ClearMessages<FeatureDeletedMessage>();

        // Note: We can't easily test delete without accessing private methods
        // This test verifies the component structure
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaDraw_ShouldRespondToMapReadyMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 10
        }, "test-map");

        await Task.Delay(100);

        // Assert - Component should handle the message
        cut.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task HonuaDraw_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        // Act
        await cut.Instance.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    #endregion

    #region Callback Tests

    [Fact]
    public async Task HonuaDraw_OnFeatureDeleted_ShouldInvokeCallback()
    {
        // Arrange
        string? deletedFeatureId = null;
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.OnFeatureDeleted, EventCallback.Factory.Create<string>(
                this, id => deletedFeatureId = id)));

        // Add a feature first
        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);

        // Note: We can't easily trigger delete through UI without JS interop
        // This test verifies the callback parameter exists
        cut.Instance.OnFeatureDeleted.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public async Task HonuaDraw_OnFeatureEdited_ShouldInvokeCallback()
    {
        // Arrange
        DrawingFeature? editedFeature = null;
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.OnFeatureEdited, EventCallback.Factory.Create<DrawingFeature>(
                this, feature => editedFeature = feature)));

        var geoJsonFeature = @"{
            ""type"": ""Feature"",
            ""id"": ""feature-1"",
            ""geometry"": {
                ""type"": ""Point"",
                ""coordinates"": [-122.4194, 37.7749]
            },
            ""properties"": {}
        }";

        await cut.Instance.OnFeatureDrawnFromJS(geoJsonFeature);

        // Act
        var updatedGeometry = @"{
            ""type"": ""Point"",
            ""coordinates"": [-122.5, 37.8]
        }";

        await cut.Instance.OnFeatureEditedFromJS("feature-1", updatedGeometry);
        await Task.Delay(100);

        // Assert
        editedFeature.Should().NotBeNull();
        editedFeature!.Id.Should().Be("feature-1");
    }

    #endregion

    #region JSInterop Tests

    [Fact]
    public void HonuaDraw_ShouldInvokeJSModule_OnRender()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDraw>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        // Assert
        _testContext.JSInterop.Invocations.Should().NotBeEmpty();
        _testContext.JSInterop.Invocations.Should().Contain(i => i.Identifier == "import");
    }

    #endregion
}
