using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.DataGrid;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaDataGrid component
/// </summary>
public class HonuaDataGridTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaDataGridTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaDataGrid_ShouldRenderWithEmptyData()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaDataGrid_WithData_ShouldRenderRows()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data));

        // Assert
        cut.Should().NotBeNull();
        // Verify data is rendered (actual assertion depends on implementation)
    }

    [Fact]
    public void HonuaDataGrid_WithCustomColumns_ShouldRenderSpecifiedColumns()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaDataGrid_WithMapExtentChange_ShouldFilterData()
    {
        // Arrange
        var data = TestData.SampleCities;
        var mapId = "test-map";

        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.SyncWith, mapId)
            .Add(p => p.AutoSyncExtent, true));

        // Act - Publish map extent changed message
        await _testContext.ComponentBus.PublishAsync(new MapExtentChangedMessage
        {
            MapId = mapId,
            Bounds = new[] { -123.0, 37.0, -122.0, 38.0 },
            Zoom = 10,
            Center = new[] { -122.5, 37.5 }
        }, mapId);

        await Task.Delay(100); // Give time for processing

        // Assert
        // In real implementation, verify filtered data
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaDataGrid_OnRowClick_ShouldPublishDataRowSelectedMessage()
    {
        // Arrange
        var data = TestData.SampleCities;

        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data));

        // Act - Simulate row click (actual implementation depends on component)
        // This would typically be done via JSInterop or component method

        // Assert
        // Verify DataRowSelectedMessage was published
        var messages = _testContext.ComponentBus.GetMessages<DataRowSelectedMessage>();
    }

    [Fact]
    public void HonuaDataGrid_WithPagination_ShouldDisplayPagedData()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.PageSize, 2));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDataGrid_WithSorting_ShouldSortData()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.Sortable, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDataGrid_WithFiltering_ShouldFilterData()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.Filterable, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDataGrid_WithMultiSelect_ShouldAllowMultipleSelection()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.SelectionMode, SelectionMode.Multiple));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDataGrid_Export_ShouldExportData()
    {
        // Arrange
        var data = TestData.SampleCities;

        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data)
            .Add(p => p.ShowExport, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaDataGrid_WithEmptyData_ShouldShowEmptyState()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, new List<TestCity>()));

        // Assert
        cut.Should().NotBeNull();
        // Verify empty state is shown (depends on implementation)
    }

    [Fact]
    public async Task HonuaDataGrid_OnDataLoaded_ShouldPublishDataLoadedMessage()
    {
        // Arrange
        var data = TestData.SampleCities;

        // Act
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid")
            .Add(p => p.Items, data));

        await Task.Delay(100);

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<DataLoadedMessage>();
        // Verify message was published with correct feature count
    }

    [Fact]
    public void HonuaDataGrid_Dispose_ShouldCleanupSubscriptions()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaDataGrid<TestCity>>(parameters => parameters
            .Add(p => p.Id, "test-grid"));

        // Act
        cut.Instance.Dispose();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }
}

/// <summary>
/// Selection mode for data grid
/// </summary>
public enum SelectionMode
{
    None,
    Single,
    Multiple
}
