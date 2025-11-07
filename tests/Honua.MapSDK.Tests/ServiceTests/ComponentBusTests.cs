using FluentAssertions;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.ServiceTests;

/// <summary>
/// Tests for the ComponentBus message bus system
/// </summary>
public class ComponentBusTests
{
    [Fact]
    public async Task PublishAsync_ShouldInvokeSubscribers()
    {
        // Arrange
        var bus = new ComponentBus();
        var messageReceived = false;
        MapExtentChangedMessage? receivedMessage = null;

        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            messageReceived = true;
            receivedMessage = args.Message;
            return Task.CompletedTask;
        });

        var testMessage = new MapExtentChangedMessage
        {
            MapId = "test-map",
            Bounds = new[] { -122.5, 37.5, -122.0, 38.0 },
            Zoom = 10,
            Center = new[] { -122.25, 37.75 }
        };

        // Act
        await bus.PublishAsync(testMessage, "sender");

        // Assert
        messageReceived.Should().BeTrue();
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be("test-map");
        receivedMessage.Zoom.Should().Be(10);
    }

    [Fact]
    public async Task PublishAsync_WithMultipleSubscribers_ShouldInvokeAll()
    {
        // Arrange
        var bus = new ComponentBus();
        var callCount = 0;

        bus.Subscribe<MapReadyMessage>(args =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        bus.Subscribe<MapReadyMessage>(args =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        bus.Subscribe<MapReadyMessage>(args =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 10
        };

        // Act
        await bus.PublishAsync(message, "sender");

        // Assert
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task PublishAsync_WithSynchronousHandler_ShouldInvokeHandler()
    {
        // Arrange
        var bus = new ComponentBus();
        var messageReceived = false;

        bus.Subscribe<FeatureClickedMessage>(args =>
        {
            messageReceived = true;
        });

        var message = new FeatureClickedMessage
        {
            MapId = "test-map",
            LayerId = "test-layer",
            FeatureId = "feature-1"
        };

        // Act
        await bus.PublishAsync(message, "sender");

        // Assert
        messageReceived.Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_ShouldStopReceivingMessages()
    {
        // Arrange
        var bus = new ComponentBus();
        var callCount = 0;

        Func<MessageArgs<FilterAppliedMessage>, Task> handler = args =>
        {
            callCount++;
            return Task.CompletedTask;
        };

        bus.Subscribe(handler);

        var message = new FilterAppliedMessage
        {
            FilterId = "filter-1",
            Type = FilterType.Spatial,
            Expression = new { }
        };

        // Act - First publish should invoke handler
        await bus.PublishAsync(message, "sender");
        callCount.Should().Be(1);

        // Unsubscribe
        bus.Unsubscribe<FilterAppliedMessage>(handler);

        // Act - Second publish should not invoke handler
        await bus.PublishAsync(message, "sender");

        // Assert
        callCount.Should().Be(1); // Still 1, not incremented
    }

    [Fact]
    public async Task PublishAsync_WithExceptionInHandler_ShouldNotAffectOtherHandlers()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ComponentBus>>();
        var bus = new ComponentBus(loggerMock.Object);
        var handler1Called = false;
        var handler2Called = false;
        var handler3Called = false;

        bus.Subscribe<DataLoadedMessage>(args =>
        {
            handler1Called = true;
            return Task.CompletedTask;
        });

        bus.Subscribe<DataLoadedMessage>(args =>
        {
            throw new InvalidOperationException("Test exception");
        });

        bus.Subscribe<DataLoadedMessage>(args =>
        {
            handler3Called = true;
            return Task.CompletedTask;
        });

        var message = new DataLoadedMessage
        {
            ComponentId = "test-component",
            FeatureCount = 100,
            Source = "test-source"
        };

        // Act
        await bus.PublishAsync(message, "sender");

        // Assert
        handler1Called.Should().BeTrue();
        handler3Called.Should().BeTrue(); // Should still be called despite handler2 throwing
        handler2Called.Should().BeFalse();

        // Verify error was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var bus = new ComponentBus();
        var message = new LayerVisibilityChangedMessage
        {
            LayerId = "test-layer",
            Visible = true
        };

        // Act & Assert - Should not throw
        await bus.PublishAsync(message, "sender");
    }

    [Fact]
    public void GetSubscriberCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var bus = new ComponentBus();

        // Act - No subscribers initially
        var initialCount = bus.GetSubscriberCount<TimeChangedMessage>();

        // Add subscribers
        bus.Subscribe<TimeChangedMessage>(args => Task.CompletedTask);
        bus.Subscribe<TimeChangedMessage>(args => Task.CompletedTask);
        bus.Subscribe<TimeChangedMessage>(args => Task.CompletedTask);

        var finalCount = bus.GetSubscriberCount<TimeChangedMessage>();

        // Assert
        initialCount.Should().Be(0);
        finalCount.Should().Be(3);
    }

    [Fact]
    public void Clear_ShouldRemoveAllSubscriptions()
    {
        // Arrange
        var bus = new ComponentBus();

        bus.Subscribe<MapExtentChangedMessage>(args => Task.CompletedTask);
        bus.Subscribe<FeatureClickedMessage>(args => Task.CompletedTask);
        bus.Subscribe<DataLoadedMessage>(args => Task.CompletedTask);

        // Act
        bus.Clear();

        // Assert
        bus.GetSubscriberCount<MapExtentChangedMessage>().Should().Be(0);
        bus.GetSubscriberCount<FeatureClickedMessage>().Should().Be(0);
        bus.GetSubscriberCount<DataLoadedMessage>().Should().Be(0);
    }

    [Fact]
    public async Task MessageArgs_ShouldContainMetadata()
    {
        // Arrange
        var bus = new ComponentBus();
        MessageArgs<LayerAddedMessage>? receivedArgs = null;

        bus.Subscribe<LayerAddedMessage>(args =>
        {
            receivedArgs = args;
            return Task.CompletedTask;
        });

        var message = new LayerAddedMessage
        {
            LayerId = "test-layer",
            LayerName = "Test Layer"
        };

        // Act
        await bus.PublishAsync(message, "test-source");

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.Message.Should().Be(message);
        receivedArgs.Source.Should().Be("test-source");
        receivedArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        receivedArgs.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PublishAsync_WithAsyncHandler_ShouldAwaitCompletion()
    {
        // Arrange
        var bus = new ComponentBus();
        var handlerCompleted = false;

        bus.Subscribe<BasemapChangedMessage>(async args =>
        {
            await Task.Delay(100);
            handlerCompleted = true;
        });

        var message = new BasemapChangedMessage
        {
            MapId = "test-map",
            Style = "streets"
        };

        // Act
        await bus.PublishAsync(message, "sender");

        // Assert
        handlerCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_SynchronousVersion_ShouldInvokeHandlers()
    {
        // Arrange
        var bus = new ComponentBus();
        var messageReceived = false;

        bus.Subscribe<HighlightFeaturesRequestMessage>(args =>
        {
            messageReceived = true;
        });

        var message = new HighlightFeaturesRequestMessage
        {
            MapId = "test-map",
            FeatureIds = new[] { "feature-1", "feature-2" }
        };

        // Act
        bus.Publish(message, "sender");

        // Give it a moment for async operation to complete
        await Task.Delay(100);

        // Assert
        messageReceived.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleMessageTypes_ShouldNotInterfere()
    {
        // Arrange
        var bus = new ComponentBus();
        var message1Received = false;
        var message2Received = false;

        bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            message1Received = true;
            return Task.CompletedTask;
        });

        bus.Subscribe<DataLoadedMessage>(args =>
        {
            message2Received = true;
            return Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "map-1",
            Bounds = new[] { 0.0, 0.0, 1.0, 1.0 },
            Zoom = 5,
            Center = new[] { 0.5, 0.5 }
        }, "sender1");

        await bus.PublishAsync(new DataLoadedMessage
        {
            ComponentId = "grid-1",
            FeatureCount = 50,
            Source = "api"
        }, "sender2");

        // Assert
        message1Received.Should().BeTrue();
        message2Received.Should().BeTrue();
    }
}
