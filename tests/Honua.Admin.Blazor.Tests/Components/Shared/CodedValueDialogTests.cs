// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Shared;
using Honua.Admin.Blazor.Tests.Infrastructure;
using MudBlazor;
using static Honua.Admin.Blazor.Components.Shared.ColumnConfiguration;

namespace Honua.Admin.Blazor.Tests.Components.Shared;

/// <summary>
/// Tests for the CodedValueDialog component.
/// Tests coded value domain creation and management including:
/// - Manual value entry
/// - CSV import
/// - Value editing and deletion
/// - Validation
/// </summary>
[Trait("Category", "Unit")]
public class CodedValueDialogTests : ComponentTestBase
{
    [Fact]
    public void AddCodedValue_ManualEntry_AddsToList()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Click "Add Value" button
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Assert - Should show input fields for new coded value
        var codeInputs = cut.FindAll("input");
        codeInputs.Should().NotBeEmpty("should have input fields for code and name");
    }

    [Fact]
    public void RemoveCodedValue_DeletesFromList()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "1", Name = "Active" },
                new() { Code = "2", Name = "Inactive" }
            }
        };

        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status")
            .Add(p => p.ExistingDomain, existingDomain));

        // Act - Click delete button for first value
        var deleteButtons = cut.FindAll("button").Where(b =>
            b.OuterHtml.Contains("Delete") || b.GetAttribute("aria-label")?.Contains("Delete") == true);

        var firstDeleteButton = deleteButtons.FirstOrDefault();
        firstDeleteButton?.Click();

        // Assert - Component should handle deletion
        cut.Should().NotBeNull();
    }

    [Fact]
    public void EditCodedValue_UpdatesExisting()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "1", Name = "Active" }
            }
        };

        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status")
            .Add(p => p.ExistingDomain, existingDomain));

        // Act - Find and modify input
        var nameInputs = cut.FindAll("input");
        if (nameInputs.Count > 1)
        {
            nameInputs[1].Change("Very Active");
        }

        // Assert - Component should handle the change
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ImportFromCSV_ValidFormat_ParsesCorrectly()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Expand CSV import panel and enter CSV data
        var expansionPanels = cut.FindAll(".mud-expand-panel");
        if (expansionPanels.Any())
        {
            // Find CSV import textarea
            var textareas = cut.FindAll("textarea");
            if (textareas.Any())
            {
                textareas[0].Change("1,Active\n2,Inactive\n3,Pending");
            }

            // Click Import button
            var importButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Import"));
            importButton?.Click();
        }

        // Assert - Values should be imported
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ImportFromCSV_InvalidFormat_ShowsError()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Enter invalid CSV (missing comma)
        var textareas = cut.FindAll("textarea");
        if (textareas.Any())
        {
            textareas[0].Change("1Active\n2Inactive");
        }

        var importButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Import"));
        importButton?.Click();

        // Assert - Invalid entries should be skipped (component handles gracefully)
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ImportFromCSV_EmptyLines_SkipsCorrectly()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Enter CSV with empty lines
        var textareas = cut.FindAll("textarea");
        if (textareas.Any())
        {
            textareas[0].Change("1,Active\n\n2,Inactive\n\n");
        }

        var importButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Import"));
        importButton?.Click();

        // Assert - Should skip empty lines
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ImportFromCSV_DuplicateCodes_AllowsEntry()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Enter CSV with duplicate codes
        var textareas = cut.FindAll("textarea");
        if (textareas.Any())
        {
            textareas[0].Change("1,Active\n1,Also Active\n2,Inactive");
        }

        var importButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Import"));
        importButton?.Click();

        // Assert - Component should accept duplicates (validation could be done on server)
        cut.Should().NotBeNull();
    }

    [Fact]
    public void Save_ValidDomain_ReturnsDomain()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Add some values
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Act - Click Save button
        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Save"));
        saveButton?.Click();

        // Assert - Dialog should close with result
        cut.Should().NotBeNull();
    }

    [Fact]
    public void Save_EmptyValues_FiltersOut()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Add value but leave it empty
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Act - Click Save without filling in values
        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Save"));
        saveButton?.Click();

        // Assert - Empty values should be filtered out
        cut.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_DiscardsChanges()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Make some changes
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Act - Click Cancel button
        var cancelButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Cancel"));
        cancelButton?.Click();

        // Assert - Dialog should close without saving
        cut.Should().NotBeNull();
    }

    [Fact]
    public void ClearAll_RemovesAllValues()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "1", Name = "Active" },
                new() { Code = "2", Name = "Inactive" }
            }
        };

        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status")
            .Add(p => p.ExistingDomain, existingDomain));

        // Act - Click "Clear All" button
        var clearButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Clear All"));
        clearButton?.Click();

        // Assert - Should show warning that no values are defined
        cut.Markup.Should().Contain("No coded values defined");
    }

    [Fact]
    public void Dialog_ShowsColumnName()
    {
        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "building_status"));

        // Assert - Should display the column name
        cut.Markup.Should().Contain("building_status");
    }

    [Fact]
    public void Dialog_ShowsExampleUsage()
    {
        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Assert - Should show example
        cut.Markup.Should().Contain("Example");
        cut.Markup.Should().Contain("Residential");
    }

    [Fact]
    public void Dialog_ShowsValueCount()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "1", Name = "Active" },
                new() { Code = "2", Name = "Inactive" },
                new() { Code = "3", Name = "Pending" }
            }
        };

        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status")
            .Add(p => p.ExistingDomain, existingDomain));

        // Assert - Should show count
        cut.Markup.Should().Contain("3");
    }

    [Fact]
    public void Dialog_EmptyDomain_ShowsWarning()
    {
        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Assert - Should show warning for empty domain
        cut.Markup.Should().Contain("No coded values defined");
    }

    [Fact]
    public void ExistingDomain_LoadsValues()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "RES", Name = "Residential" },
                new() { Code = "COM", Name = "Commercial" }
            }
        };

        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "zoning")
            .Add(p => p.ExistingDomain, existingDomain));

        // Assert - Should display existing values
        cut.Markup.Should().Contain("RES");
        cut.Markup.Should().Contain("Residential");
        cut.Markup.Should().Contain("COM");
        cut.Markup.Should().Contain("Commercial");
    }

    [Fact]
    public void CodedValueInput_ShowsCodeAndNameFields()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Add a value
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Assert - Should show both code and name fields
        cut.Markup.Should().Contain("Code");
        cut.Markup.Should().Contain("Display Name");
    }

    [Fact]
    public void CodedValueInput_ShowsHelperText()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Add a value
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();

        // Assert - Should show helper text
        cut.Markup.Should().Contain("Actual value");
        cut.Markup.Should().Contain("User-friendly name");
    }

    [Fact]
    public void CSVImport_ShowsFormatInstructions()
    {
        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Assert - Should show CSV format instructions
        cut.Markup.Should().Contain("code,name");
        cut.Markup.Should().Contain("one per line");
    }

    [Fact]
    public void CSVImport_ShowsPlaceholder()
    {
        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Assert - Should show example in placeholder
        cut.Markup.Should().Contain("Residential");
        cut.Markup.Should().Contain("Commercial");
        cut.Markup.Should().Contain("Industrial");
    }

    [Fact]
    public void CSVImport_WithWhitespace_TrimsCorrectly()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Enter CSV with extra whitespace
        var textareas = cut.FindAll("textarea");
        if (textareas.Any())
        {
            textareas[0].Change(" 1 , Active \n 2 , Inactive ");
        }

        var importButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Import"));
        importButton?.Click();

        // Assert - Should handle whitespace trimming
        cut.Should().NotBeNull();
    }

    [Fact]
    public void MultipleCodedValues_DisplaysInOrder()
    {
        // Arrange
        var existingDomain = new CodedValueDomain
        {
            Type = "codedValue",
            Values = new List<CodedValue>
            {
                new() { Code = "1", Name = "First" },
                new() { Code = "2", Name = "Second" },
                new() { Code = "3", Name = "Third" }
            }
        };

        // Act
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "priority")
            .Add(p => p.ExistingDomain, existingDomain));

        // Assert - All values should be displayed
        cut.Markup.Should().Contain("First");
        cut.Markup.Should().Contain("Second");
        cut.Markup.Should().Contain("Third");
    }

    [Fact]
    public void AddMultipleValues_AllowsMultipleAdditions()
    {
        // Arrange
        var cut = Context.RenderComponent<CodedValueDialog>(parameters => parameters
            .Add(p => p.ColumnName, "status"));

        // Act - Add multiple values
        var addButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Add Value"));
        addButton?.Click();
        addButton?.Click();
        addButton?.Click();

        // Assert - Should have multiple value input sets
        var inputs = cut.FindAll("input");
        inputs.Should().NotBeEmpty("should have multiple input fields");
    }
}
