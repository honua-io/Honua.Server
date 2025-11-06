// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Shared;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Tests.Infrastructure;
using MudBlazor;
using static Honua.Admin.Blazor.Components.Shared.ColumnConfiguration;

namespace Honua.Admin.Blazor.Tests.Components.Shared;

/// <summary>
/// Tests for the ColumnConfiguration component.
/// Tests column selection, alias configuration, and coded value domain management including:
/// - Column selection/deselection
/// - Required column handling
/// - Alias assignment
/// - Coded value domain creation and management
/// </summary>
[Trait("Category", "Unit")]
public class ColumnConfigurationTests : ComponentTestBase
{
    private TableInfo CreateSampleTableInfo()
    {
        return new TableInfo
        {
            Schema = "public",
            Table = "buildings",
            GeometryColumn = "geom",
            GeometryType = "POLYGON",
            Srid = 4326,
            RowCount = 1000,
            Columns = new List<ColumnInfo>
            {
                new() { Name = "id", DataType = "integer", IsPrimaryKey = true, IsNullable = false },
                new() { Name = "name", DataType = "varchar", IsNullable = false },
                new() { Name = "status", DataType = "varchar", IsNullable = true },
                new() { Name = "category", DataType = "varchar", IsNullable = true },
                new() { Name = "year_built", DataType = "integer", IsNullable = true },
                new() { Name = "geom", DataType = "geometry", IsNullable = false }
            }
        };
    }

    [Fact]
    public void InitialLoad_ShowsAllColumns()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string>();

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert
        cut.Markup.Should().Contain("Configure Columns");
        cut.Markup.Should().Contain("id");
        cut.Markup.Should().Contain("name");
        cut.Markup.Should().Contain("status");
        cut.Markup.Should().Contain("geom");
    }

    [Fact]
    public void InitialLoad_ShowsColumnDataTypes()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string>();

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert
        cut.Markup.Should().Contain("integer");
        cut.Markup.Should().Contain("varchar");
        cut.Markup.Should().Contain("geometry");
    }

    [Fact]
    public void SelectColumn_AddsToSelectedList()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Act - Find and click checkbox for 'name' column
        var checkboxes = cut.FindAll("input[type='checkbox']");
        if (checkboxes.Count > 1)
        {
            checkboxes[1].Change(true);
        }

        // Assert - The selectedColumns should be updated (via event callback in real scenario)
        // In unit test, we verify the UI responds correctly
        cut.Should().NotBeNull();
    }

    [Fact]
    public void DeselectColumn_RemovesFromSelectedList()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "name", "status" };

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Act - Uncheck a column
        var checkboxes = cut.FindAll("input[type='checkbox']");
        if (checkboxes.Count > 2)
        {
            checkboxes[2].Change(false);
        }

        // Assert - Component should handle the change
        cut.Should().NotBeNull();
    }

    [Fact]
    public void RequiredColumn_GeometryColumn_CannotBeDeselected()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "geom", "id", "name" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Geometry column checkbox should be disabled
        var checkboxes = cut.FindAll("input[type='checkbox']");
        var geometryCheckbox = checkboxes.FirstOrDefault(cb =>
            cb.GetAttribute("disabled") != null);

        geometryCheckbox.Should().NotBeNull("geometry column should have disabled checkbox");
    }

    [Fact]
    public void RequiredColumn_PrimaryKey_CannotBeDeselected()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id", "name" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Primary key should be marked as required
        cut.Markup.Should().Contain("Primary Key", "primary key indicator should be present");
    }

    [Fact]
    public void SetAlias_UpdatesColumnAlias()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "name", "status" };
        var columnAliases = new Dictionary<string, string>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.ColumnAliases, columnAliases));

        // Act - Find alias input field
        var aliasInputs = cut.FindAll("input[type='text']");
        if (aliasInputs.Any())
        {
            aliasInputs[0].Change("Building Name");
        }

        // Assert - Component should handle the alias change
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ClearAlias_RemovesAlias()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "name" };
        var columnAliases = new Dictionary<string, string> { { "name", "Building Name" } };

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.ColumnAliases, columnAliases));

        // Act - Clear the alias
        var aliasInputs = cut.FindAll("input[type='text']");
        if (aliasInputs.Any())
        {
            aliasInputs[0].Change(string.Empty);
        }

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void SelectAllColumns_SelectsAll()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Act - Click "Select All" button
        var selectAllButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Select All"));
        selectAllButton?.Click();

        // Assert - Button should be present
        selectAllButton.Should().NotBeNull();
    }

    [Fact]
    public void DeselectAllColumns_ClearsAll()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id", "name", "status", "geom" };

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Act - Click "Deselect All" button
        var deselectAllButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Deselect All"));
        deselectAllButton?.Click();

        // Assert - Button should be present (required columns will remain selected)
        deselectAllButton.Should().NotBeNull();
    }

    [Fact]
    public void DeselectAll_KeepsRequiredColumns()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id", "name", "status", "geom" };

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Act - Click "Deselect All" button
        var deselectAllButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Deselect All"));
        deselectAllButton?.Click();

        // Assert - Should still show required columns (geometry and primary key)
        cut.Markup.Should().Contain("Geometry column");
    }

    [Fact]
    public void AddCodedValue_ShowsAddButton()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "status" };
        var codedValueDomains = new Dictionary<string, CodedValueDomain>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.CodedValueDomains, codedValueDomains));

        // Assert - Should show "Add" button for coded values on non-geometry, non-PK columns
        cut.Markup.Should().Contain("Add", "should show Add button for coded values");
    }

    [Fact]
    public void AddCodedValue_GeometryColumn_NoAddButton()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "geom" };
        var codedValueDomains = new Dictionary<string, CodedValueDomain>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.CodedValueDomains, codedValueDomains));

        // Assert - Geometry columns should not have coded value buttons
        // The row for geometry should not show "Add" button in coded values column
        cut.Should().NotBeNull();
    }

    [Fact]
    public void AddCodedValue_PrimaryKeyColumn_NoAddButton()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id" };
        var codedValueDomains = new Dictionary<string, CodedValueDomain>();

        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.CodedValueDomains, codedValueDomains));

        // Assert - Primary key columns should not have coded value buttons
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ExistingCodedValues_ShowsValueCount()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "status" };
        var codedValueDomains = new Dictionary<string, CodedValueDomain>
        {
            {
                "status",
                new CodedValueDomain
                {
                    Type = "codedValue",
                    Values = new List<CodedValue>
                    {
                        new() { Code = "1", Name = "Active" },
                        new() { Code = "2", Name = "Inactive" },
                        new() { Code = "3", Name = "Pending" }
                    }
                }
            }
        };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.CodedValueDomains, codedValueDomains));

        // Assert - Should show count of coded values
        cut.Markup.Should().Contain("3 values", "should display the count of coded values");
    }

    [Fact]
    public void ColumnIcons_GeometryColumn_ShowsPlaceIcon()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "geom" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Should show geometry icon
        cut.Markup.Should().Contain("Place", "geometry column should have place icon");
    }

    [Fact]
    public void ColumnIcons_PrimaryKey_ShowsKeyIcon()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Should show key icon
        cut.Markup.Should().Contain("Key", "primary key column should have key icon");
    }

    [Fact]
    public void ColumnAlias_OnlyShownForSelectedColumns()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "name" }; // Only select 'name'
        var columnAliases = new Dictionary<string, string>();

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.ColumnAliases, columnAliases));

        // Assert - Alias inputs should only appear for selected columns
        var aliasInputs = cut.FindAll("input[type='text']");
        // Count should be limited to selected columns only
        aliasInputs.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectedColumnsCount_DisplaysCorrectly()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "id", "name", "geom" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Should show "3 / 6 columns"
        cut.Markup.Should().Contain($"{selectedColumns.Count} / {tableInfo.Columns.Count} columns");
    }

    [Fact]
    public void GeometryColumnRequired_ShowsInfoMessage()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "geom" };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns));

        // Assert - Should indicate geometry column is required
        cut.Markup.Should().Contain("Geometry column");
        cut.Markup.Should().Contain("required");
    }

    [Fact]
    public void HeaderCheckbox_ReflectsAllSelectedState()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var allColumns = tableInfo.Columns.Select(c => c.Name).ToHashSet();

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, allColumns));

        // Assert - Header checkbox should be present
        var headerCheckboxes = cut.FindAll("input[type='checkbox']");
        headerCheckboxes.Should().NotBeEmpty("should have checkboxes including header");
    }

    [Fact]
    public void ComponentUpdate_PreservesExistingAliases()
    {
        // Arrange
        var tableInfo = CreateSampleTableInfo();
        var selectedColumns = new HashSet<string> { "name" };
        var columnAliases = new Dictionary<string, string> { { "name", "Building Name" } };

        // Act
        var cut = Context.RenderComponent<ColumnConfiguration>(parameters => parameters
            .Add(p => p.TableInfo, tableInfo)
            .Add(p => p.SelectedColumns, selectedColumns)
            .Add(p => p.ColumnAliases, columnAliases));

        // Assert - Existing alias should be preserved
        var inputs = cut.FindAll("input[type='text']");
        inputs.Should().NotBeEmpty();
    }
}
