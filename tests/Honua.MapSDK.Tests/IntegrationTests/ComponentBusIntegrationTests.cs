using FluentAssertions;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.IntegrationTests;

/// <summary>
/// Integration tests for ComponentBus pub/sub system
/// </summary>
public class ComponentBusIntegrationTests
{
    [Fact]
    public async Task MultipleSubscribers_ShouldAllReceiveMessages()
    {
        // Arrange
        var bus = new TestComponentBus();
        var subscriber1Received = false;
        var subscriber2Received = false;
        var subscriber3Received = false;

        bus.Subscribe<MapExtentChangedMessage>(args => { subscriber1Received = true; });
        bus.Subscribe<MapExtentChangedMessage>(args => { subscriber2Received = true; });
        bus.Subscribe<MapExtentChangedMessage>(args => { subscriber3Received = true; });

        var message = new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = TestData.SampleBounds,
            Zoom = 10,
            Center = TestData.SampleCenter
        };

        // Act
        await bus.PublishAsync(message, "test-source");

        // Assert
        subscriber1Received.Should().BeTrue();
        subscriber2Received.Should().BeTrue();
        subscriber3Received.Should().BeTrue();
    }

    [Fact]
    public async Task ComponentBus_ShouldIsolateMessageTypes()
    {
        // Arrange
        var bus = new TestComponentBus();
        var mapMessageReceived = false;
        var filterMessageReceived = false;

        bus.Subscribe<MapExtentChangedMessage>(args => { mapMessageReceived = true; });
        bus.Subscribe<FilterAppliedMessage>(args => { filterMessageReceived = true; });

        // Act - Publish only MapExtentChangedMessage
        await bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = TestData.SampleBounds,
            Zoom = 10,
            Center = TestData.SampleCenter
        }, "source");

        // Assert
        mapMessageReceived.Should().BeTrue();
        filterMessageReceived.Should().BeFalse();
    }

    [Fact]
    public async Task AsyncHandlers_ShouldExecuteSequentially()
    {
        // Arrange
        var bus = new ComponentBus();
        var executionOrder = new List<int>();

        bus.Subscribe<DataLoadedMessage>(async args =>
        {
            await Task.Delay(50);
            executionOrder.Add(1);
        });

        bus.Subscribe<DataLoadedMessage>(async args =>
        {
            await Task.Delay(25);
            executionOrder.Add(2);
        });

        bus.Subscribe<DataLoadedMessage>(async args =>
        {
            executionOrder.Add(3);
        });

        var message = new DataLoadedMessage
        {
            ComponentId = "test",
            FeatureCount = 100,
            Source = "test"
        };

        // Act
        await bus.PublishAsync(message, "source");

        // Assert
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task MessageTracking_ShouldTrackPublishedMessages()
    {
        // Arrange
        var bus = new TestComponentBus();

        // Act
        await bus.PublishAsync(new MapReadyMessage
        {
            MapId = "map-1",
            Center = TestData.SampleCenter,
            Zoom = 10
        }, "map");

        await bus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "map-1",
            LayerId = "layer-1",
            FeatureId = "feature-1"
        }, "map");

        await bus.PublishAsync(new DataLoadedMessage
        {
            ComponentId = "grid-1",
            FeatureCount = 50,
            Source = "api"
        }, "grid");

        // Assert
        bus.PublishedMessages.Should().HaveCount(3);
        bus.GetMessages<MapReadyMessage>().Should().HaveCount(1);
        bus.GetMessages<FeatureClickedMessage>().Should().HaveCount(1);
        bus.GetMessages<DataLoadedMessage>().Should().HaveCount(1);
    }

    [Fact]
    public async Task WaitForMessageAsync_ShouldWaitForSpecificMessage()
    {
        // Arrange
        var bus = new TestComponentBus();

        // Act - Publish message after a delay
        var waitTask = bus.WaitForMessageAsync<LayerAddedMessage>(TimeSpan.FromSeconds(2));

        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            await bus.PublishAsync(new LayerAddedMessage
            {
                LayerId = "layer-1",
                LayerName = "Test Layer"
            }, "source");
        });

        var message = await waitTask;

        // Assert
        message.Should().NotBeNull();
        message.LayerId.Should().Be("layer-1");
    }

    [Fact]
    public async Task WaitForMessageAsync_WithTimeout_ShouldThrow()
    {
        // Arrange
        var bus = new TestComponentBus();

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await bus.WaitForMessageAsync<LayerAddedMessage>(TimeSpan.FromMilliseconds(100));
        });
    }

    [Fact]
    public async Task MessageMetadata_ShouldContainCorrectInformation()
    {
        // Arrange
        var bus = new ComponentBus();
        MessageArgs<FilterAppliedMessage>? receivedArgs = null;

        bus.Subscribe<FilterAppliedMessage>(args =>
        {
            receivedArgs = args;
            return Task.CompletedTask;
        });

        var message = new FilterAppliedMessage
        {
            FilterId = "filter-1",
            Type = FilterType.Spatial,
            Expression = new { type = "bbox" }
        };

        // Act
        await bus.PublishAsync(message, "filter-panel");

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.Message.Should().Be(message);
        receivedArgs.Source.Should().Be("filter-panel");
        receivedArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        receivedArgs.CorrelationId.Should().NotBeNullOrEmpty();
    }
}
