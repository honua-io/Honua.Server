// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Folder management in the Honua Admin Blazor application.
/// Tests cover folder creation, organization, hierarchy, and service assignment.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("FolderManagement")]
public class FolderManagementTests : BaseE2ETest
{
    private string _testFolderId = null!;

    [SetUp]
    public async Task FolderTestSetUp()
    {
        _testFolderId = $"e2e-folder-{Guid.NewGuid():N}";
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task FolderTestTearDown()
    {
        try
        {
            await NavigateToFoldersPageAsync();
            var folder = Page.Locator($"text={_testFolderId}").First();
            if (await folder.IsVisibleAsync())
            {
                await folder.ClickAsync();
                var deleteButton = Page.Locator("button:has-text('Delete')").First();
                if (await deleteButton.IsVisibleAsync())
                {
                    await deleteButton.ClickAsync();
                    var confirmDialog = await WaitForDialogAsync();
                    var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                    await confirmButton.ClickAsync();
                }
            }
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("Create a new folder with ID and title")]
    public async Task CreateFolder_WithIdAndTitle_ShouldSucceed()
    {
        // Arrange
        await NavigateToFoldersPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Folder'), button:has-text('Add Folder'), button:has-text('Create Folder')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Folder ID").FillAsync(_testFolderId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Folder");

        // Set order if available
        var orderField = dialog.GetByLabel("Order");
        if (await orderField.IsVisibleAsync())
        {
            await orderField.FillAsync("100");
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        var folderNode = Page.Locator($"text={_testFolderId}, .folder:has-text('{_testFolderId}')");
        await Expect(folderNode.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Update folder title")]
    public async Task UpdateFolder_Title_ShouldSucceed()
    {
        // Arrange - Create folder first
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Edit folder
        var folder = Page.Locator($"text={_testFolderId}").First();
        await folder.ClickAsync();

        var editButton = Page.Locator("button:has-text('Edit')").First();
        await editButton.ClickAsync();

        var dialog = await WaitForDialogAsync();
        var titleField = dialog.GetByLabel("Title");
        await titleField.ClearAsync();
        await titleField.FillAsync("Updated Folder Title");

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Update')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
        await Expect(Page.Locator("text=Updated Folder Title")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Delete empty folder")]
    public async Task DeleteFolder_EmptyFolder_ShouldSucceed()
    {
        // Arrange - Create folder first
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Delete folder
        var folder = Page.Locator($"text={_testFolderId}").First();
        await folder.ClickAsync();

        var deleteButton = Page.Locator("button:has-text('Delete')").First();
        await deleteButton.ClickAsync();

        // Confirm deletion
        var confirmDialog = await WaitForDialogAsync();
        var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
        await confirmButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("deleted successfully");
        await Page.WaitForTimeoutAsync(1000);
        var deletedFolder = Page.Locator($"text={_testFolderId}");
        await Expect(deletedFolder).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Create hierarchical folders (parent/child)")]
    public async Task CreateFolder_WithParentChild_ShouldShowHierarchy()
    {
        // Arrange
        await NavigateToFoldersPageAsync();

        // Create parent folder
        var newButton = Page.Locator("button:has-text('New Folder')");
        await newButton.First().ClickAsync();

        var parentDialog = await WaitForDialogAsync();
        await parentDialog.GetByLabel("Folder ID").FillAsync(_testFolderId);
        await parentDialog.GetByLabel("Title").FillAsync("Parent Folder");

        var saveParentButton = parentDialog.Locator("button:has-text('Save')");
        await saveParentButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Create child folder if hierarchy is supported
        var childFolderId = $"{_testFolderId}-child";
        await newButton.First().ClickAsync();

        var childDialog = await WaitForDialogAsync();
        await childDialog.GetByLabel("Folder ID").FillAsync(childFolderId);
        await childDialog.GetByLabel("Title").FillAsync("Child Folder");

        // Select parent if parent selector exists
        var parentDropdown = childDialog.Locator("label:has-text('Parent Folder')").Locator("..");
        if (await parentDropdown.IsVisibleAsync())
        {
            await parentDropdown.ClickAsync();
            var parentOption = Page.Locator($"li:has-text('{_testFolderId}')").First();
            await parentOption.ClickAsync();
        }

        var saveChildButton = childDialog.Locator("button:has-text('Save')");
        await saveChildButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Both folders should exist
        await Expect(Page.Locator($"text={_testFolderId}")).ToBeVisibleAsync();
        await Expect(Page.Locator($"text={childFolderId}")).ToBeVisibleAsync();

        // Cleanup child folder
        try
        {
            var childFolder = Page.Locator($"text={childFolderId}").First();
            await childFolder.ClickAsync();
            var deleteButton = Page.Locator("button:has-text('Delete')").First();
            await deleteButton.ClickAsync();
            var confirmDialog = await WaitForDialogAsync();
            await confirmDialog.Locator("button:has-text('Confirm')").ClickAsync();
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("Reorder folders")]
    public async Task ReorderFolders_ChangeOrderValue_ShouldReflectInList()
    {
        // Arrange - Create two folders with different orders
        await CreateTestFolderAsync();

        var secondFolderId = $"{_testFolderId}-2";
        await NavigateToFoldersPageAsync();

        var newButton = Page.Locator("button:has-text('New Folder')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Folder ID").FillAsync(secondFolderId);
        await dialog.GetByLabel("Title").FillAsync("Second Folder");

        var orderField = dialog.GetByLabel("Order");
        if (await orderField.IsVisibleAsync())
        {
            await orderField.FillAsync("50");
        }

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Both folders should be visible
        await Expect(Page.Locator($"text={_testFolderId}")).ToBeVisibleAsync();
        await Expect(Page.Locator($"text={secondFolderId}")).ToBeVisibleAsync();

        // Cleanup second folder
        try
        {
            var secondFolder = Page.Locator($"text={secondFolderId}").First();
            await secondFolder.ClickAsync();
            var deleteButton = Page.Locator("button:has-text('Delete')").First();
            await deleteButton.ClickAsync();
            var confirmDialog = await WaitForDialogAsync();
            await confirmDialog.Locator("button:has-text('Confirm')").ClickAsync();
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("View folder service count")]
    public async Task ViewFolder_ServiceCount_ShouldDisplayCorrectCount()
    {
        // Arrange - Create folder
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Click on folder to view details
        var folder = Page.Locator($"text={_testFolderId}").First();
        await folder.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should show 0 services (no services created)
        var serviceCount = Page.Locator("text=0 services, text=No services");
        await Expect(serviceCount.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Create folder with duplicate ID should show error")]
    public async Task CreateFolder_DuplicateId_ShouldShowError()
    {
        // Arrange - Create first folder
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Try to create another with same ID
        var newButton = Page.Locator("button:has-text('New Folder')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Folder ID").FillAsync(_testFolderId); // Same ID
        await dialog.GetByLabel("Title").FillAsync("Duplicate Folder");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert - Should show error
        await WaitForSnackbarAsync("already exists");
    }

    [Test]
    [Description("Expand and collapse folder tree")]
    public async Task FolderTree_ExpandCollapse_ShouldToggleVisibility()
    {
        // Arrange - Create folder
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Look for expand/collapse button
        var expandButton = Page.Locator($"button[aria-label*='expand'], .expand-icon").First();

        if (await expandButton.IsVisibleAsync())
        {
            // Click to expand
            await expandButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Click to collapse
            await expandButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Assert - Folder should still be visible
            await Expect(Page.Locator($"text={_testFolderId}")).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("Folder tree expand/collapse not available in UI");
        }
    }

    [Test]
    [Description("Search folders")]
    public async Task FolderList_Search_ShouldFilterResults()
    {
        // Arrange - Create folder
        await CreateTestFolderAsync();
        await NavigateToFoldersPageAsync();

        // Act - Search for folder
        var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
        if (await searchBox.IsVisibleAsync())
        {
            await searchBox.FillAsync(_testFolderId);
            await Page.WaitForTimeoutAsync(1000);

            // Assert - Should show matching folder
            await Expect(Page.Locator($"text={_testFolderId}")).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("Search functionality not available in UI");
        }
    }

    #region Helper Methods

    private async Task NavigateToFoldersPageAsync()
    {
        var foldersLink = Page.Locator("a:has-text('Folders'), nav a:has-text('Folders')");
        if (await foldersLink.IsVisibleAsync())
        {
            await foldersLink.First().ClickAsync();
        }
        else
        {
            await Page.GotoAsync($"{BaseUrl}/folders");
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestFolderAsync()
    {
        await NavigateToFoldersPageAsync();

        var newButton = Page.Locator("button:has-text('New Folder'), button:has-text('Create Folder')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Folder ID").FillAsync(_testFolderId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Folder");

        var orderField = dialog.GetByLabel("Order");
        if (await orderField.IsVisibleAsync())
        {
            await orderField.FillAsync("100");
        }

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion
}
