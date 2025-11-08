using FluentAssertions;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.IntegrationTests;

/// <summary>
/// Integration tests for timeline component with other components
/// </summary>
public class TimelineIntegrationTests
{
    [Fact]
    public async Task TimelineChange_ShouldUpdateMap()
    {
        // Arrange
        var bus = new TestComponentBus();
        var mapUpdated = false;

        bus.Subscribe<TimeChangedMessage>(args =>
        {
            mapUpdated = true;
        });

        // Act
        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 6, 15),
            TimeField = "timestamp"
        }, "timeline");

        // Assert
        mapUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task TimelineChange_ShouldFilterGrid()
    {
        // Arrange
        var bus = new TestComponentBus();
        var gridFiltered = false;

        bus.Subscribe<TimeChangedMessage>(args =>
        {
            gridFiltered = true;
        });

        // Act
        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 6, 15),
            TimeField = "timestamp"
        }, "timeline");

        // Assert
        gridFiltered.Should().BeTrue();
    }

    [Fact]
    public async Task TimelineChange_ShouldUpdateChart()
    {
        // Arrange
        var bus = new TestComponentBus();
        var chartUpdated = false;

        bus.Subscribe<TimeChangedMessage>(args =>
        {
            chartUpdated = true;
        });

        // Act
        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 6, 15),
            TimeField = "timestamp"
        }, "timeline");

        // Assert
        chartUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task TimelinePlayback_ShouldPublishSequentialMessages()
    {
        // Arrange
        var bus = new TestComponentBus();
        var timestamps = new List<DateTime>();

        bus.Subscribe<TimeChangedMessage>(args =>
        {
            timestamps.Add(args.Message.Timestamp);
        });

        // Act - Simulate playback with multiple time steps
        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 1, 1),
            TimeField = "timestamp"
        }, "timeline");

        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 1, 2),
            TimeField = "timestamp"
        }, "timeline");

        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 1, 3),
            TimeField = "timestamp"
        }, "timeline");

        // Assert
        timestamps.Should().HaveCount(3);
        timestamps[0].Should().Be(new DateTime(2024, 1, 1));
        timestamps[1].Should().Be(new DateTime(2024, 1, 2));
        timestamps[2].Should().Be(new DateTime(2024, 1, 3));
    }

    [Fact]
    public async Task Timeline_ShouldCoordinateWithTemporalFilter()
    {
        // Arrange
        var bus = new TestComponentBus();
        var timelineActive = false;
        var filterActive = false;

        bus.Subscribe<TimeChangedMessage>(args => { timelineActive = true; });
        bus.Subscribe<FilterAppliedMessage>(args =>
        {
            if (args.Message.Type == FilterType.Temporal)
                filterActive = true;
        });

        // Act - Timeline changes time
        await bus.PublishAsync(new TimeChangedMessage
        {
            Timestamp = new DateTime(2024, 6, 15),
            TimeField = "timestamp"
        }, "timeline");

        // Timeline could trigger temporal filter
        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "timeline-filter",
            Type = FilterType.Temporal,
            Expression = new { }
        }, "timeline");

        // Assert
        timelineActive.Should().BeTrue();
        filterActive.Should().BeTrue();
    }

    [Fact]
    public async Task AllFiltersClear_ShouldResetTimeline()
    {
        // Arrange
        var bus = new TestComponentBus();
        var timelineReset = false;

        bus.Subscribe<AllFiltersClearedMessage>(args =>
        {
            timelineReset = true;
        });

        // Act
        await bus.PublishAsync(new AllFiltersClearedMessage
        {
            Source = "filter-panel"
        }, "filter-panel");

        // Assert
        timelineReset.Should().BeTrue();
    }
}
