// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.MapSDK.Tests.Services;

/// <summary>
/// Tests for the ComponentBus message bus.
/// Tests pub/sub pattern, message delivery, error handling, and subscription management.
/// </summary>
[Trait("Category", "Unit")]
public class ComponentBusTests
{
    private readonly ComponentBus _bus;

    public ComponentBusTests()
    {
        _bus = new ComponentBus(NullLogger<ComponentBus>.Instance);
    }

    #region Basic Publish/Subscribe Tests

    [Fact]
    public async Task Subscribe_ShouldReceivePublishedMessage()
    {
        // Arrange
        MapReadyMessage? receivedMessage = null;
        _bus.Subscribe<MapReadyMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be("test-map");
        receivedMessage.Zoom.Should().Be(12);
    }

    [Fact]
    public async Task Subscribe_Async_ShouldReceivePublishedMessage()
    {
        // Arrange
        MapReadyMessage? receivedMessage = null;
        _bus.Subscribe<MapReadyMessage>(async args =>
        {
            await Task.Delay(10); // Simulate async work
            receivedMessage = args.Message;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be("test-map");
    }

    [Fact]
    public async Task Subscribe_MultipleHandlers_ShouldReceiveAllMessages()
    {
        // Arrange
        var receivedCount = 0;
        _bus.Subscribe<MapReadyMessage>(args => { receivedCount++; });
        _bus.Subscribe<MapReadyMessage>(args => { receivedCount++; });
        _bus.Subscribe<MapReadyMessage>(args => { receivedCount++; });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        receivedCount.Should().Be(3);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act & Assert
        await _bus.Invoking(b => b.PublishAsync(message, "test"))
            .Should().NotThrowAsync();
    }

    #endregion

    #region Message Args Tests

    [Fact]
    public async Task MessageArgs_ShouldContainSource()
    {
        // Arrange
        string? receivedSource = null;
        _bus.Subscribe<MapReadyMessage>(args =>
        {
            receivedSource = args.Source;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test-source");

        // Assert
        receivedSource.Should().Be("test-source");
    }

    [Fact]
    public async Task MessageArgs_ShouldContainTimestamp()
    {
        // Arrange
        DateTime? receivedTimestamp = null;
        _bus.Subscribe<MapReadyMessage>(args =>
        {
            receivedTimestamp = args.Timestamp;
        });

        var beforePublish = DateTime.UtcNow;
        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");
        var afterPublish = DateTime.UtcNow;

        // Assert
        receivedTimestamp.Should().NotBeNull();
        receivedTimestamp.Should().BeOnOrAfter(beforePublish);
        receivedTimestamp.Should().BeOnOrBefore(afterPublish);
    }

    [Fact]
    public async Task MessageArgs_ShouldContainCorrelationId()
    {
        // Arrange
        string? receivedCorrelationId = null;
        _bus.Subscribe<MapReadyMessage>(args =>
        {
            receivedCorrelationId = args.CorrelationId;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        receivedCorrelationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(receivedCorrelationId, out _).Should().BeTrue();
    }

    #endregion

    #region Different Message Types Tests

    [Fact]
    public async Task Subscribe_DifferentMessageTypes_ShouldOnlyReceiveMatchingType()
    {
        // Arrange
        var mapReadyReceived = false;
        var extentChangedReceived = false;

        _bus.Subscribe<MapReadyMessage>(args => { mapReadyReceived = true; });
        _bus.Subscribe<MapExtentChangedMessage>(args => { extentChangedReceived = true; });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        mapReadyReceived.Should().BeTrue();
        extentChangedReceived.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_FilterMessages_ShouldWork()
    {
        // Arrange
        FilterAppliedMessage? receivedMessage = null;
        _bus.Subscribe<FilterAppliedMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        var message = new FilterAppliedMessage
        {
            FilterId = "filter-1",
            Type = FilterType.Attribute,
            Expression = new { field = "zoning", value = "Commercial" }
        };

        // Act
        await _bus.PublishAsync(message, "filter-panel");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.FilterId.Should().Be("filter-1");
        receivedMessage.Type.Should().Be(FilterType.Attribute);
    }

    [Fact]
    public async Task PublishAsync_LayerMessages_ShouldWork()
    {
        // Arrange
        LayerVisibilityChangedMessage? receivedMessage = null;
        _bus.Subscribe<LayerVisibilityChangedMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        var message = new LayerVisibilityChangedMessage
        {
            LayerId = "parcels",
            Visible = false
        };

        // Act
        await _bus.PublishAsync(message, "layer-panel");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.LayerId.Should().Be("parcels");
        receivedMessage.Visible.Should().BeFalse();
    }

    #endregion

    #region Unsubscribe Tests

    [Fact]
    public async Task Unsubscribe_ShouldStopReceivingMessages()
    {
        // Arrange
        var receivedCount = 0;
        Action<MessageArgs<MapReadyMessage>> handler = args => { receivedCount++; };

        _bus.Subscribe<MapReadyMessage>(handler);

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act - Publish before unsubscribe
        await _bus.PublishAsync(message, "test");
        receivedCount.Should().Be(1);

        // Unsubscribe
        _bus.Unsubscribe<MapReadyMessage>(handler);

        // Publish after unsubscribe
        await _bus.PublishAsync(message, "test");

        // Assert
        receivedCount.Should().Be(1, "handler should not receive messages after unsubscribe");
    }

    [Fact]
    public async Task Unsubscribe_OneHandler_ShouldNotAffectOthers()
    {
        // Arrange
        var handler1Count = 0;
        var handler2Count = 0;

        Action<MessageArgs<MapReadyMessage>> handler1 = args => { handler1Count++; };
        Action<MessageArgs<MapReadyMessage>> handler2 = args => { handler2Count++; };

        _bus.Subscribe<MapReadyMessage>(handler1);
        _bus.Subscribe<MapReadyMessage>(handler2);

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act - Unsubscribe handler1
        _bus.Unsubscribe<MapReadyMessage>(handler1);
        await _bus.PublishAsync(message, "test");

        // Assert
        handler1Count.Should().Be(0);
        handler2Count.Should().Be(1);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_ShouldRemoveAllSubscriptions()
    {
        // Arrange
        var receivedCount = 0;
        _bus.Subscribe<MapReadyMessage>(args => { receivedCount++; });
        _bus.Subscribe<MapExtentChangedMessage>(args => { receivedCount++; });

        // Act
        _bus.Clear();

        await _bus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        }, "test");

        await _bus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = "test-map",
            Bounds = new[] { -122.5, 37.7, -122.3, 37.8 },
            Zoom = 12,
            Center = new[] { -122.4, 37.7 }
        }, "test");

        // Assert
        receivedCount.Should().Be(0);
    }

    #endregion

    #region Subscriber Count Tests

    [Fact]
    public void GetSubscriberCount_NoSubscribers_ShouldReturnZero()
    {
        // Act
        var count = _bus.GetSubscriberCount<MapReadyMessage>();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetSubscriberCount_WithSubscribers_ShouldReturnCorrectCount()
    {
        // Arrange
        _bus.Subscribe<MapReadyMessage>(args => { });
        _bus.Subscribe<MapReadyMessage>(args => { });
        _bus.Subscribe<MapReadyMessage>(args => { });

        // Act
        var count = _bus.GetSubscriberCount<MapReadyMessage>();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void GetSubscriberCount_DifferentMessageTypes_ShouldCountSeparately()
    {
        // Arrange
        _bus.Subscribe<MapReadyMessage>(args => { });
        _bus.Subscribe<MapReadyMessage>(args => { });
        _bus.Subscribe<MapExtentChangedMessage>(args => { });

        // Act
        var mapReadyCount = _bus.GetSubscriberCount<MapReadyMessage>();
        var extentChangedCount = _bus.GetSubscriberCount<MapExtentChangedMessage>();

        // Assert
        mapReadyCount.Should().Be(2);
        extentChangedCount.Should().Be(1);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PublishAsync_HandlerThrowsException_ShouldContinueToOtherHandlers()
    {
        // Arrange
        var handler1Executed = false;
        var handler2Executed = false;
        var handler3Executed = false;

        _bus.Subscribe<MapReadyMessage>(args => { handler1Executed = true; });
        _bus.Subscribe<MapReadyMessage>(args =>
        {
            throw new InvalidOperationException("Test exception");
        });
        _bus.Subscribe<MapReadyMessage>(args => { handler3Executed = true; });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert - All handlers should execute despite exception in handler 2
        handler1Executed.Should().BeTrue();
        handler3Executed.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_AsyncHandlerThrowsException_ShouldContinueToOtherHandlers()
    {
        // Arrange
        var handler1Executed = false;
        var handler3Executed = false;

        _bus.Subscribe<MapReadyMessage>(async args =>
        {
            await Task.Delay(10);
            handler1Executed = true;
        });

        _bus.Subscribe<MapReadyMessage>(async args =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Test async exception");
        });

        _bus.Subscribe<MapReadyMessage>(async args =>
        {
            await Task.Delay(10);
            handler3Executed = true;
        });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        await _bus.PublishAsync(message, "test");

        // Assert
        handler1Executed.Should().BeTrue();
        handler3Executed.Should().BeTrue();
    }

    #endregion

    #region Synchronous Publish Tests

    [Fact]
    public void Publish_ShouldInvokeAsyncPublish()
    {
        // Arrange
        var received = false;
        _bus.Subscribe<MapReadyMessage>(args => { received = true; });

        var message = new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        };

        // Act
        _bus.Publish(message, "test");

        // Note: Synchronous publish uses fire-and-forget pattern
        // We need to wait a bit for async execution
        Task.Delay(100).Wait();

        // Assert
        received.Should().BeTrue();
    }

    #endregion

    #region Complex Workflow Tests

    [Fact]
    public async Task ComplexWorkflow_MapInteraction_ShouldWorkCorrectly()
    {
        // Arrange - Simulate a complex interaction workflow
        var events = new List<string>();

        _bus.Subscribe<MapReadyMessage>(args =>
        {
            events.Add($"Map ready: {args.Message.MapId}");
        });

        _bus.Subscribe<FeatureClickedMessage>(args =>
        {
            events.Add($"Feature clicked: {args.Message.FeatureId}");
        });

        _bus.Subscribe<FilterAppliedMessage>(args =>
        {
            events.Add($"Filter applied: {args.Message.FilterId}");
        });

        _bus.Subscribe<LayerVisibilityChangedMessage>(args =>
        {
            events.Add($"Layer visibility: {args.Message.LayerId} = {args.Message.Visible}");
        });

        // Act - Simulate a workflow
        await _bus.PublishAsync(new MapReadyMessage
        {
            MapId = "main-map",
            Center = new[] { -122.4, 37.7 },
            Zoom = 12
        }, "map");

        await _bus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "main-map",
            LayerId = "parcels",
            FeatureId = "parcel-123",
            Properties = new Dictionary<string, object> { ["address"] = "123 Main St" }
        }, "map");

        await _bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "zoning-filter",
            Type = FilterType.Attribute,
            Expression = new { field = "zoning", value = "Commercial" }
        }, "filter-panel");

        await _bus.PublishAsync(new LayerVisibilityChangedMessage
        {
            LayerId = "parcels",
            Visible = false
        }, "layer-panel");

        // Assert
        events.Should().HaveCount(4);
        events[0].Should().Contain("Map ready");
        events[1].Should().Contain("Feature clicked");
        events[2].Should().Contain("Filter applied");
        events[3].Should().Contain("Layer visibility");
    }

    #endregion
}
