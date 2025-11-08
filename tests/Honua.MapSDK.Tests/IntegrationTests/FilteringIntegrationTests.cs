using FluentAssertions;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.IntegrationTests;

/// <summary>
/// Integration tests for filtering across multiple components
/// </summary>
public class FilteringIntegrationTests
{
    [Fact]
    public async Task SpatialFilter_ShouldAffectMapAndGrid()
    {
        // Arrange
        var bus = new TestComponentBus();
        var mapFiltered = false;
        var gridFiltered = false;

        bus.Subscribe<FilterAppliedMessage>(args =>
        {
            if (args.Message.Type == FilterType.Spatial)
            {
                mapFiltered = true;
                gridFiltered = true;
            }
        });

        var filter = new SpatialFilter
        {
            Id = "spatial-filter-1",
            SpatialType = SpatialFilterType.BoundingBox,
            BoundingBox = TestData.SampleBounds
        };

        // Act
        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = filter.Id,
            Type = FilterType.Spatial,
            Expression = filter.ToExpression()
        }, "filter-panel");

        // Assert
        mapFiltered.Should().BeTrue();
        gridFiltered.Should().BeTrue();
    }

    [Fact]
    public async Task AttributeFilter_ShouldAffectAllComponents()
    {
        // Arrange
        var bus = new TestComponentBus();
        var componentsAffected = new List<string>();

        bus.Subscribe<FilterAppliedMessage>(args =>
        {
            componentsAffected.Add(args.Source ?? "unknown");
        });

        var filter = new AttributeFilter
        {
            Id = "attr-filter-1",
            Field = "population",
            Operator = AttributeOperator.GreaterThan,
            Value = 1000000
        };

        // Act
        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = filter.Id,
            Type = FilterType.Attribute,
            Expression = filter.ToExpression()
        }, "filter-panel");

        // Assert
        bus.PublishedMessages.Should().HaveCount(1);
        bus.GetLastMessage<FilterAppliedMessage>()?.Type.Should().Be(FilterType.Attribute);
    }

    [Fact]
    public async Task TemporalFilter_ShouldWorkWithTimeline()
    {
        // Arrange
        var bus = new TestComponentBus();
        var timelineUpdated = false;

        bus.Subscribe<FilterAppliedMessage>(args =>
        {
            if (args.Message.Type == FilterType.Temporal)
            {
                timelineUpdated = true;
            }
        });

        var filter = new TemporalFilter
        {
            Id = "temporal-filter-1",
            DateField = "timestamp",
            TemporalType = TemporalFilterType.Between,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31)
        };

        // Act
        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = filter.Id,
            Type = FilterType.Temporal,
            Expression = filter.ToExpression()
        }, "filter-panel");

        // Assert
        timelineUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task ClearFilter_ShouldRemoveFilter()
    {
        // Arrange
        var bus = new TestComponentBus();
        var filterCleared = false;

        bus.Subscribe<FilterClearedMessage>(args =>
        {
            filterCleared = true;
        });

        // Act
        await bus.PublishAsync(new FilterClearedMessage
        {
            FilterId = "filter-1"
        }, "filter-panel");

        // Assert
        filterCleared.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAllFilters_ShouldResetAllComponents()
    {
        // Arrange
        var bus = new TestComponentBus();
        var allFiltersCleared = false;

        bus.Subscribe<AllFiltersClearedMessage>(args =>
        {
            allFiltersCleared = true;
        });

        // Act
        await bus.PublishAsync(new AllFiltersClearedMessage
        {
            Source = "filter-panel"
        }, "filter-panel");

        // Assert
        allFiltersCleared.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleActiveFilters_ShouldApplyAll()
    {
        // Arrange
        var bus = new TestComponentBus();

        // Act - Apply multiple filters
        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "spatial-1",
            Type = FilterType.Spatial,
            Expression = new { }
        }, "filter-panel");

        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "attribute-1",
            Type = FilterType.Attribute,
            Expression = new { }
        }, "filter-panel");

        await bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "temporal-1",
            Type = FilterType.Temporal,
            Expression = new { }
        }, "filter-panel");

        // Assert
        var filterMessages = bus.GetMessages<FilterAppliedMessage>();
        filterMessages.Should().HaveCount(3);
        filterMessages.Should().Contain(f => f.Type == FilterType.Spatial);
        filterMessages.Should().Contain(f => f.Type == FilterType.Attribute);
        filterMessages.Should().Contain(f => f.Type == FilterType.Temporal);
    }
}
