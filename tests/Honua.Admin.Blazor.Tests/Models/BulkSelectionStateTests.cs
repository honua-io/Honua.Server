// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Admin.Blazor.Shared.Models;
using Xunit;

namespace Honua.Admin.Blazor.Tests.Models;

public class BulkSelectionStateTests
{
    private class TestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Constructor_ShouldInitializeEmptyState()
    {
        // Act
        var state = new BulkSelectionState<TestItem>();

        // Assert
        state.SelectedIds.Should().BeEmpty();
        state.IsAllSelected.Should().BeFalse();
        state.Items.Should().BeEmpty();
        state.SelectedCount.Should().Be(0);
        state.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelection_WhenItemNotSelected_ShouldAddToSelection()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var itemId = "item1";

        // Act
        state.ToggleSelection(itemId);

        // Assert
        state.SelectedIds.Should().Contain(itemId);
        state.SelectedCount.Should().Be(1);
        state.HasSelection.Should().BeTrue();
        state.IsSelected(itemId).Should().BeTrue();
    }

    [Fact]
    public void ToggleSelection_WhenItemAlreadySelected_ShouldRemoveFromSelection()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var itemId = "item1";
        state.ToggleSelection(itemId);

        // Act
        state.ToggleSelection(itemId);

        // Assert
        state.SelectedIds.Should().NotContain(itemId);
        state.SelectedCount.Should().Be(0);
        state.HasSelection.Should().BeFalse();
        state.IsSelected(itemId).Should().BeFalse();
    }

    [Fact]
    public void SelectAll_ShouldSelectAllItemsAndSetFlag()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>
        {
            new TestItem { Id = "item1", Name = "Item 1" },
            new TestItem { Id = "item2", Name = "Item 2" },
            new TestItem { Id = "item3", Name = "Item 3" }
        };

        // Act
        state.SelectAll(items, item => item.Id);

        // Assert
        state.IsAllSelected.Should().BeTrue();
        state.SelectedIds.Should().HaveCount(3);
        state.SelectedIds.Should().Contain(new[] { "item1", "item2", "item3" });
        state.SelectedCount.Should().Be(3);
        state.HasSelection.Should().BeTrue();
    }

    [Fact]
    public void SelectAll_WithEmptyList_ShouldSetFlagButHaveNoSelections()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>();

        // Act
        state.SelectAll(items, item => item.Id);

        // Assert
        state.IsAllSelected.Should().BeTrue();
        state.SelectedIds.Should().BeEmpty();
        state.SelectedCount.Should().Be(0);
    }

    [Fact]
    public void DeselectAll_ShouldClearAllSelectionsAndFlag()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>
        {
            new TestItem { Id = "item1", Name = "Item 1" },
            new TestItem { Id = "item2", Name = "Item 2" }
        };
        state.SelectAll(items, item => item.Id);

        // Act
        state.DeselectAll();

        // Assert
        state.IsAllSelected.Should().BeFalse();
        state.SelectedIds.Should().BeEmpty();
        state.SelectedCount.Should().Be(0);
        state.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_WhenItemSelected_ShouldReturnTrue()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        state.ToggleSelection("item1");

        // Act
        var result = state.IsSelected("item1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_WhenItemNotSelected_ShouldReturnFalse()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();

        // Act
        var result = state.IsSelected("item1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelection_AfterSelectingAllItems_ShouldUpdateAllSelectedFlag()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>
        {
            new TestItem { Id = "item1", Name = "Item 1" },
            new TestItem { Id = "item2", Name = "Item 2" }
        };
        state.Items = items;
        state.ToggleSelection("item1");
        state.ToggleSelection("item2");

        // Assert - IsAllSelected should be true when all items are selected
        state.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void ToggleSelection_AfterDeselectingOneItem_ShouldClearAllSelectedFlag()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>
        {
            new TestItem { Id = "item1", Name = "Item 1" },
            new TestItem { Id = "item2", Name = "Item 2" }
        };
        state.SelectAll(items, item => item.Id);

        // Act - Deselect one item
        state.ToggleSelection("item1");

        // Assert
        state.IsAllSelected.Should().BeFalse();
        state.SelectedCount.Should().Be(1);
    }

    [Fact]
    public void HasSelection_WithMultipleSelections_ShouldReturnTrue()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        state.ToggleSelection("item1");
        state.ToggleSelection("item2");
        state.ToggleSelection("item3");

        // Act & Assert
        state.HasSelection.Should().BeTrue();
        state.SelectedCount.Should().Be(3);
    }

    [Fact]
    public void SelectedCount_ShouldReflectNumberOfSelectedItems()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();

        // Act & Assert - Initial state
        state.SelectedCount.Should().Be(0);

        // Add selections
        state.ToggleSelection("item1");
        state.SelectedCount.Should().Be(1);

        state.ToggleSelection("item2");
        state.SelectedCount.Should().Be(2);

        state.ToggleSelection("item3");
        state.SelectedCount.Should().Be(3);

        // Remove selection
        state.ToggleSelection("item2");
        state.SelectedCount.Should().Be(2);
    }

    [Fact]
    public void SelectAll_ThenToggleOne_ShouldMaintainOtherSelections()
    {
        // Arrange
        var state = new BulkSelectionState<TestItem>();
        var items = new List<TestItem>
        {
            new TestItem { Id = "item1", Name = "Item 1" },
            new TestItem { Id = "item2", Name = "Item 2" },
            new TestItem { Id = "item3", Name = "Item 3" }
        };
        state.SelectAll(items, item => item.Id);

        // Act
        state.ToggleSelection("item2");

        // Assert
        state.SelectedIds.Should().Contain("item1");
        state.SelectedIds.Should().NotContain("item2");
        state.SelectedIds.Should().Contain("item3");
        state.SelectedCount.Should().Be(2);
    }
}
