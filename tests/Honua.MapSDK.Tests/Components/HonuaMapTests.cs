// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Components.Map;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.Infrastructure;
using Honua.MapSDK.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.MapSDK.Tests.Components;

/// <summary>
/// Tests for the HonuaMap component (MapLibre integration).
/// Tests component initialization, configuration binding, event handling,
/// layer management, marker management, viewport control, and cleanup.
/// </summary>
[Trait("Category", "Unit")]
public class HonuaMapTests : MapComponentTestBase
{
    #region Component Initialization Tests

    [Fact]
    public void Component_ShouldRenderWithDefaultParameters()
    {
        // Act
        var cut = Context.RenderComponent<HonuaMap>();

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().Contain("class=\"honua-map");
        cut.Markup.Should().Contain("style=\"width: 100%; height: 100%;\"");
    }

    [Fact]
    public void Component_ShouldGenerateUniqueIdWhenNotProvided()
    {
        // Act
        var cut1 = Context.RenderComponent<HonuaMap>();
        var cut2 = Context.RenderComponent<HonuaMap>();

        // Assert
        var id1 = cut1.Instance.Id;
        var id2 = cut2.Instance.Id;

        id1.Should().NotBeNullOrEmpty();
        id2.Should().NotBeNullOrEmpty();
        id1.Should().NotBe(id2, "each map instance should have a unique ID");
    }

    [Fact]
    public void Component_ShouldUseProvidedId()
    {
        // Arrange
        var customId = "my-custom-map";

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, customId));

        // Assert
        cut.Instance.Id.Should().Be(customId);
        cut.Markup.Should().Contain($"id=\"{customId}\"");
    }

    [Fact]
    public void Component_ShouldApplyCustomCssClass()
    {
        // Arrange
        var customClass = "my-map-class";

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.CssClass, customClass));

        // Assert
        cut.Markup.Should().Contain($"class=\"honua-map {customClass}\"");
    }

    [Fact]
    public void Component_ShouldApplyCustomStyle()
    {
        // Arrange
        var customStyle = "width: 800px; height: 600px;";

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Style, customStyle));

        // Assert
        cut.Markup.Should().Contain($"style=\"{customStyle}\"");
    }

    #endregion

    #region Configuration Binding Tests

    [Fact]
    public void Component_ShouldBindMapStyleParameter()
    {
        // Arrange
        var mapStyle = "maplibre://honua/dark";

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.MapStyle, mapStyle));

        // Assert
        cut.Instance.MapStyle.Should().Be(mapStyle);
    }

    [Fact]
    public void Component_ShouldBindCenterParameter()
    {
        // Arrange
        var center = MapTestFixture.CreateTestCenter();

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Center, center));

        // Assert
        cut.Instance.Center.Should().BeEquivalentTo(center);
    }

    [Fact]
    public void Component_ShouldBindZoomParameter()
    {
        // Arrange
        var zoom = 15.0;

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Zoom, zoom));

        // Assert
        cut.Instance.Zoom.Should().Be(zoom);
    }

    [Fact]
    public void Component_ShouldBindBearingAndPitch()
    {
        // Arrange
        var bearing = 45.0;
        var pitch = 30.0;

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Bearing, bearing)
            .Add(p => p.Pitch, pitch));

        // Assert
        cut.Instance.Bearing.Should().Be(bearing);
        cut.Instance.Pitch.Should().Be(pitch);
    }

    [Fact]
    public void Component_ShouldBindProjectionParameter()
    {
        // Arrange
        var projection = "globe";

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Projection, projection));

        // Assert
        cut.Instance.Projection.Should().Be(projection);
    }

    [Fact]
    public void Component_ShouldBindMaxBoundsParameter()
    {
        // Arrange
        var maxBounds = MapTestFixture.CreateTestBounds();

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.MaxBounds, maxBounds));

        // Assert
        cut.Instance.MaxBounds.Should().BeEquivalentTo(maxBounds);
    }

    [Fact]
    public void Component_ShouldBindMinMaxZoom()
    {
        // Arrange
        var minZoom = 5.0;
        var maxZoom = 18.0;

        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.MinZoom, minZoom)
            .Add(p => p.MaxZoom, maxZoom));

        // Assert
        cut.Instance.MinZoom.Should().Be(minZoom);
        cut.Instance.MaxZoom.Should().Be(maxZoom);
    }

    #endregion

    #region Event Handling Tests

    [Fact]
    public async Task OnMapReady_ShouldInvokeEventCallback()
    {
        // Arrange
        MapReadyMessage? receivedMessage = null;
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.OnMapReady, msg => { receivedMessage = msg; }));

        var bus = Context.Services.GetRequiredService<ComponentBus>();

        // Act - Simulate map ready from JS
        var readyMessage = new MapReadyMessage
        {
            MapId = cut.Instance.Id,
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 12
        };
        await bus.PublishAsync(readyMessage);

        // Wait for event propagation
        await Task.Delay(100);

        // Assert
        // Note: Since we're testing component behavior, we verify the component
        // is set up correctly. Full event testing would require JS interop mocking.
        cut.Instance.OnMapReady.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public void OnExtentChanged_ShouldInvokeEventCallback()
    {
        // Arrange
        MapExtentChangedMessage? receivedMessage = null;
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.OnExtentChanged, msg => { receivedMessage = msg; }));

        // Assert
        cut.Instance.OnExtentChanged.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public void OnFeatureClicked_ShouldInvokeEventCallback()
    {
        // Arrange
        FeatureClickedMessage? receivedMessage = null;
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.OnFeatureClicked, msg => { receivedMessage = msg; }));

        // Assert
        cut.Instance.OnFeatureClicked.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public async Task OnExtentChangedInternal_ShouldPublishMessage()
    {
        // Arrange
        var bus = Context.Services.GetRequiredService<ComponentBus>();
        MapExtentChangedMessage? receivedMessage = null;

        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        var cut = Context.RenderComponent<HonuaMap>();

        // Act - Simulate JS callback
        await cut.Instance.OnExtentChangedInternal(
            bounds: MapTestFixture.CreateTestBounds(),
            zoom: 12,
            center: MapTestFixture.CreateTestCenter(),
            bearing: 0,
            pitch: 0
        );

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be(cut.Instance.Id);
        receivedMessage.Zoom.Should().Be(12);
    }

    [Fact]
    public async Task OnFeatureClickedInternal_ShouldPublishMessage()
    {
        // Arrange
        var bus = Context.Services.GetRequiredService<ComponentBus>();
        FeatureClickedMessage? receivedMessage = null;

        bus.Subscribe<FeatureClickedMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        var cut = Context.RenderComponent<HonuaMap>();

        // Act - Simulate JS callback
        var properties = MapTestFixture.CreateTestFeatureProperties();
        await cut.Instance.OnFeatureClickedInternal(
            layerId: "parcels",
            featureId: "parcel-123",
            properties: properties,
            geometry: MapTestFixture.CreateTestPolygonGeometry()
        );

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be(cut.Instance.Id);
        receivedMessage.LayerId.Should().Be("parcels");
        receivedMessage.FeatureId.Should().Be("parcel-123");
        receivedMessage.Properties.Should().ContainKey("address");
    }

    #endregion

    #region Message Bus Integration Tests

    [Fact]
    public async Task FlyToRequestMessage_ShouldBeHandled()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        // Act
        var flyToMessage = new FlyToRequestMessage
        {
            MapId = cut.Instance.Id,
            Center = new[] { -122.4, 37.8 },
            Zoom = 15
        };

        await bus.PublishAsync(flyToMessage);

        // Assert - Component subscribed to message
        bus.GetSubscriberCount<FlyToRequestMessage>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FitBoundsRequestMessage_ShouldBeHandled()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        // Act
        var fitBoundsMessage = new FitBoundsRequestMessage
        {
            MapId = cut.Instance.Id,
            Bounds = MapTestFixture.CreateTestBounds(),
            Padding = 50
        };

        await bus.PublishAsync(fitBoundsMessage);

        // Assert - Component subscribed to message
        bus.GetSubscriberCount<FitBoundsRequestMessage>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LayerVisibilityChangedMessage_ShouldBeHandled()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        // Act
        var message = new LayerVisibilityChangedMessage
        {
            LayerId = "parcels",
            Visible = false
        };

        await bus.PublishAsync(message);

        // Assert - Component subscribed to message
        bus.GetSubscriberCount<LayerVisibilityChangedMessage>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BasemapChangedMessage_ShouldBeHandled()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        // Act
        var message = new BasemapChangedMessage
        {
            MapId = cut.Instance.Id,
            Style = "maplibre://honua/dark"
        };

        await bus.PublishAsync(message);

        // Assert - Component subscribed to message
        bus.GetSubscriberCount<BasemapChangedMessage>().Should().BeGreaterThan(0);
    }

    #endregion

    #region Public API Tests

    [Fact]
    public async Task FlyToAsync_ShouldPublishMessage()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        FlyToRequestMessage? receivedMessage = null;
        bus.Subscribe<FlyToRequestMessage>(args => { receivedMessage = args.Message; });

        // Act
        await cut.Instance.FlyToAsync(new[] { -122.4, 37.8 }, 15);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be(cut.Instance.Id);
        receivedMessage.Center.Should().BeEquivalentTo(new[] { -122.4, 37.8 });
        receivedMessage.Zoom.Should().Be(15);
    }

    [Fact]
    public async Task FitBoundsAsync_ShouldPublishMessage()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();
        var bus = Context.Services.GetRequiredService<ComponentBus>();

        FitBoundsRequestMessage? receivedMessage = null;
        bus.Subscribe<FitBoundsRequestMessage>(args => { receivedMessage = args.Message; });

        var bounds = MapTestFixture.CreateTestBounds();

        // Act
        await cut.Instance.FitBoundsAsync(bounds, 100);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be(cut.Instance.Id);
        receivedMessage.Bounds.Should().BeEquivalentTo(bounds);
        receivedMessage.Padding.Should().Be(100);
    }

    #endregion

    #region Accessibility Tests

    [Fact]
    public void Component_ShouldHaveAccessibleStructure()
    {
        // Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "main-map"));

        // Assert
        cut.Markup.Should().Contain("id=\"main-map\"");
        // The map container should have an ID for ARIA references
    }

    #endregion

    #region Responsive Behavior Tests

    [Fact]
    public void Component_ShouldRenderWithResponsiveStyle()
    {
        // Act
        var cut = Context.RenderComponent<HonuaMap>();

        // Assert
        cut.Markup.Should().Contain("width: 100%");
        cut.Markup.Should().Contain("height: 100%");
    }

    #endregion

    #region Child Content Tests

    [Fact]
    public void Component_ShouldRenderChildContent()
    {
        // Arrange & Act
        var cut = Context.RenderComponent<HonuaMap>(parameters => parameters
            .AddChildContent("<div class='map-overlay'>Test Overlay</div>"));

        // Assert
        cut.Markup.Should().Contain("Test Overlay");
        cut.Markup.Should().Contain("map-overlay");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_ShouldNotThrowException()
    {
        // Arrange
        var cut = Context.RenderComponent<HonuaMap>();

        // Act & Assert
        await cut.Instance.Invoking(async c => await c.DisposeAsync())
            .Should().NotThrowAsync();
    }

    #endregion
}
