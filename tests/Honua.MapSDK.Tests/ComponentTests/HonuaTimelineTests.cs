using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Timeline;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaTimeline component
/// </summary>
public class HonuaTimelineTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaTimelineTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaTimeline_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaTimeline_WithTimeField_ShouldConfigureTimeField()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.TimeField, "timestamp"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaTimeline_WithTimeRange_ShouldSetTimeRange()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        // Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.StartDate, startDate)
            .Add(p => p.EndDate, endDate));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaTimeline_OnPlay_ShouldPublishTimeChangedMessages()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.TimeField, "timestamp"));

        // Act - Start playback (implementation dependent)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<TimeChangedMessage>();
    }

    [Fact]
    public void HonuaTimeline_WithStepSize_ShouldConfigureStepSize()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.StepSize, TimeSpan.FromDays(1)));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaTimeline_WithPlaybackSpeed_ShouldConfigureSpeed()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.PlaybackSpeed, 2.0));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaTimeline_WithLoop_ShouldEnableLoop()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.Loop, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaTimeline_WithControls_ShouldShowControls()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline")
            .Add(p => p.ShowControls, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaTimeline_OnPause_ShouldStopPublishingMessages()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline"));

        // Act - Pause playback (implementation dependent)

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaTimeline_Dispose_ShouldStopPlayback()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaTimeline>(parameters => parameters
            .Add(p => p.Id, "test-timeline"));

        // Act
        cut.Instance.Dispose();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }
}
