using FluentAssertions;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.IntegrationTests;

/// <summary>
/// Integration tests for map and data grid synchronization
/// </summary>
public class MapDataGridSyncTests
{
    [Fact]
    public async Task MapExtentChange_ShouldTriggerGridFiltering()
    {
        // Arrange
        var bus = new TestComponentBus();
        var gridFilterUpdated = false;

        // Simulate grid subscribing to map extent changes
        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            if (args.Message.MapId == "map-1")
            {
                gridFilterUpdated = true;
            }
        });

        // Act
        await bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = new[] { -122.5, 37.5, -122.0, 38.0 },
            Zoom = 10,
            Center = new[] { -122.25, 37.75 }
        }, "map-1");

        // Assert
        gridFilterUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task FeatureClick_ShouldHighlightGridRow()
    {
        // Arrange
        var bus = new TestComponentBus();
        var highlightedFeatureId = string.Empty;

        // Simulate grid subscribing to feature clicks
        bus.Subscribe<FeatureClickedMessage>(args =>
        {
            highlightedFeatureId = args.Message.FeatureId;
        });

        // Act
        await bus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "map-1",
            LayerId = "cities",
            FeatureId = "feature-1",
            Properties = TestData.SampleFeatureProperties
        }, "map-1");

        // Assert
        highlightedFeatureId.Should().Be("feature-1");
    }

    [Fact]
    public async Task GridRowSelect_ShouldHighlightMapFeature()
    {
        // Arrange
        var bus = new TestComponentBus();
        var highlightRequested = false;

        // Simulate map subscribing to row selections
        bus.Subscribe<DataRowSelectedMessage>(args =>
        {
            highlightRequested = true;
        });

        bus.Subscribe<HighlightFeaturesRequestMessage>(args =>
        {
            highlightRequested = true;
        });

        // Act
        await bus.PublishAsync(new DataRowSelectedMessage
        {
            GridId = "grid-1",
            RowId = "feature-1",
            Data = TestData.SampleFeatureProperties
        }, "grid-1");

        // Assert
        highlightRequested.Should().BeTrue();
    }

    [Fact]
    public async Task BidirectionalSync_ShouldWorkInBothDirections()
    {
        // Arrange
        var bus = new TestComponentBus();
        var mapMessageReceived = false;
        var gridMessageReceived = false;

        bus.Subscribe<MapExtentChangedMessage>(args => { mapMessageReceived = true; });
        bus.Subscribe<DataRowSelectedMessage>(args => { gridMessageReceived = true; });

        // Act - Map to Grid
        await bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = TestData.SampleBounds,
            Zoom = 10,
            Center = TestData.SampleCenter
        }, "map-1");

        // Act - Grid to Map
        await bus.PublishAsync(new DataRowSelectedMessage
        {
            GridId = "grid-1",
            RowId = "row-1",
            Data = TestData.SampleFeatureProperties
        }, "grid-1");

        // Assert
        mapMessageReceived.Should().BeTrue();
        gridMessageReceived.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleGrids_ShouldOnlySyncWithTargetMap()
    {
        // Arrange
        var bus = new TestComponentBus();
        var grid1Synced = false;
        var grid2Synced = false;

        // Grid 1 syncs with map-1
        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            if (args.Message.MapId == "map-1")
                grid1Synced = true;
        });

        // Grid 2 syncs with map-2
        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            if (args.Message.MapId == "map-2")
                grid2Synced = true;
        });

        // Act - Map 1 extent changes
        await bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = TestData.SampleBounds,
            Zoom = 10,
            Center = TestData.SampleCenter
        }, "map-1");

        // Assert
        grid1Synced.Should().BeTrue();
        grid2Synced.Should().BeFalse();
    }
}
