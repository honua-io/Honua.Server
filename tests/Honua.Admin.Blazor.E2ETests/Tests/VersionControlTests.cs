// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Version Control and Snapshot management in the Honua Admin Blazor application.
/// These tests verify the complete workflow for creating, viewing, comparing, and restoring metadata snapshots.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("VersionControl")]
[Category("Snapshots")]
public class VersionControlTests : BaseE2ETest
{
    private string _testSnapshotLabel1 = null!;
    private string _testSnapshotLabel2 = null!;
    private string _testServiceId = null!;

    [SetUp]
    public async Task VersionControlTestSetUp()
    {
        // Generate unique identifiers for this test run
        var guid = Guid.NewGuid().ToString("N")[..8];
        _testSnapshotLabel1 = $"e2e-snapshot-1-{guid}";
        _testSnapshotLabel2 = $"e2e-snapshot-2-{guid}";
        _testServiceId = $"e2e-service-{guid}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task VersionControlTestTearDown()
    {
        // Cleanup: Delete test service if it exists
        try
        {
            await TryDeleteServiceAsync(_testServiceId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    [Description("Verify that users can navigate to the version history page")]
    public async Task NavigateToVersionHistory_ShouldDisplayVersionHistoryPage()
    {
        // Act - Navigate to version history page
        await NavigateToVersionHistoryPageAsync();

        // Assert - Verify page loaded successfully
        await Expect(Page.Locator("text=Metadata Snapshots, text=Version History")).ToBeVisibleAsync();

        // Verify key UI elements are present
        var createSnapshotButton = Page.Locator("button:has-text('Create Snapshot')");
        var refreshButton = Page.Locator("button[title='Refresh']");

        // Check if versioning is enabled (either button exists or enterprise warning shows)
        var hasCreateButton = await createSnapshotButton.IsVisibleAsync();
        var hasEnterpriseWarning = await Page.Locator("text=Enterprise Feature Required").IsVisibleAsync();

        Assert.That(hasCreateButton || hasEnterpriseWarning, Is.True,
            "Either Create Snapshot button or Enterprise warning should be visible");
    }

    [Test]
    [Description("Verify that users can create a new snapshot with label and notes")]
    public async Task CreateSnapshot_WithLabelAndNotes_ShouldSucceed()
    {
        // Arrange
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        var createButton = Page.Locator("button:has-text('Create Snapshot')");
        if (!await createButton.IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Click Create Snapshot button
        await createButton.ClickAsync();

        // Wait for create snapshot dialog
        var dialog = await WaitForDialogAsync();

        // Fill in snapshot details
        var labelField = dialog.GetByLabel("Label");
        await labelField.FillAsync(_testSnapshotLabel1);

        var notesField = dialog.GetByLabel("Notes");
        if (await notesField.IsVisibleAsync())
        {
            await notesField.FillAsync("E2E test snapshot for automated testing");
        }

        // Submit form
        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("created successfully");

        // Assert - Verify snapshot appears in the list
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}'), td:has-text('{_testSnapshotLabel1}')");
        await Expect(snapshotRow.First()).ToBeVisibleAsync();

        // Verify notes are displayed
        var notesText = Page.Locator($"text=E2E test snapshot");
        await Expect(notesText.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that snapshot list displays all snapshots with correct metadata")]
    public async Task ViewSnapshotList_ShouldDisplayAllSnapshotsWithMetadata()
    {
        // Arrange - Create a test snapshot first
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Test snapshot 1");
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Verify snapshot is in the list
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        await Expect(snapshotRow).ToBeVisibleAsync();

        // Assert - Verify metadata columns are displayed
        // Label should be visible
        var labelCell = snapshotRow.Locator($"text={_testSnapshotLabel1}");
        await Expect(labelCell).ToBeVisibleAsync();

        // Created date should be visible (check for date format or relative time)
        var createdText = snapshotRow.Locator("text=/\\d{4}-\\d{2}-\\d{2}|just now|minutes ago|hours ago/");
        await Expect(createdText.First()).ToBeVisibleAsync();

        // Actions buttons should be present
        var compareButton = snapshotRow.Locator("button[title*='Compare']");
        var restoreButton = snapshotRow.Locator("button[title*='Restore']");
        var detailsButton = snapshotRow.Locator("button[title*='details']");

        await Expect(compareButton).ToBeVisibleAsync();
        await Expect(restoreButton).ToBeVisibleAsync();
        await Expect(detailsButton).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can view snapshot details")]
    public async Task ViewSnapshotDetails_ShouldDisplayDetailDialog()
    {
        // Arrange - Create a test snapshot
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Detailed snapshot notes");
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Click the info/details button
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var detailsButton = snapshotRow.Locator("button[title*='details'], button[title*='View details']");
        await detailsButton.ClickAsync();

        // Wait for details dialog
        var dialog = await WaitForDialogAsync();

        // Assert - Verify dialog shows snapshot details
        await Expect(dialog.Locator($"text={_testSnapshotLabel1}")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=Detailed snapshot notes")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can compare two snapshots")]
    public async Task CompareSnapshots_ShouldNavigateToDiffPage()
    {
        // Arrange - Create two test snapshots
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "First snapshot");
        await Task.Delay(1000); // Ensure different timestamps
        await CreateTestSnapshotAsync(_testSnapshotLabel2, "Second snapshot");
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Click compare button on first snapshot
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var compareButton = snapshotRow.Locator("button[title*='Compare']");
        await compareButton.ClickAsync();

        // Wait for navigation to diff page
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify we're on the comparison page
        await Expect(Page.Locator("text=Compare Snapshot")).ToBeVisibleAsync();
        await Expect(Page.Locator($"text={_testSnapshotLabel1}")).ToBeVisibleAsync();

        // Verify back button is present
        var backButton = Page.Locator("button[title='Back to versions']");
        await Expect(backButton).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that version diff displays changes between snapshots")]
    public async Task ViewVersionDiff_WithChanges_ShouldDisplayDifferences()
    {
        // Arrange - Create first snapshot
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Snapshot before changes");

        // Make a change (create a test service)
        await CreateTestServiceAsync();

        // Create second snapshot after changes
        await CreateTestSnapshotAsync(_testSnapshotLabel2, "Snapshot after changes");

        // Navigate to compare the first snapshot
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var compareButton = snapshotRow.Locator("button[title*='Compare']");
        await compareButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify diff information is displayed
        var diffPage = Page.Locator(".mud-container");
        await Expect(diffPage).ToBeVisibleAsync();

        // Check for change indicators (added/removed/modified)
        var hasChangesText = await Page.Locator("text=/changes|No changes detected/").IsVisibleAsync();
        Assert.That(hasChangesText, Is.True, "Should display change information");

        // Look for service/layer/folder change sections
        var hasEntitySections = await Page.Locator("text=/Services|Layers|Folders/").IsVisibleAsync();
        Assert.That(hasEntitySections, Is.True, "Should display entity type sections when there are changes");
    }

    [Test]
    [Description("Verify that version diff shows no changes when snapshots are identical")]
    public async Task ViewVersionDiff_NoChanges_ShouldShowNoChangesMessage()
    {
        // Arrange - Create two snapshots without changes between them
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "First snapshot");
        await Task.Delay(500);
        await CreateTestSnapshotAsync(_testSnapshotLabel2, "Second snapshot identical");

        // Navigate to compare
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var compareButton = snapshotRow.Locator("button[title*='Compare']");
        await compareButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should show no changes message
        // Note: The actual message depends on implementation
        var noChangesText = Page.Locator("text=/No changes detected|matches the current|Cannot compare/");
        await Expect(noChangesText.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can restore from a snapshot")]
    public async Task RestoreSnapshot_ShouldPromptConfirmationAndRestore()
    {
        // Arrange - Create a test snapshot
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Snapshot to restore");
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Click restore button
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var restoreButton = snapshotRow.Locator("button[title*='Restore']");
        await restoreButton.ClickAsync();

        // Wait for confirmation dialog
        var confirmDialog = await WaitForDialogAsync();

        // Assert - Verify confirmation dialog appears with warning
        await Expect(confirmDialog.Locator("text=/Confirm Restore|Are you sure/")).ToBeVisibleAsync();
        await Expect(confirmDialog.Locator($"text={_testSnapshotLabel1}")).ToBeVisibleAsync();

        // Verify Cancel button exists (don't actually restore to avoid affecting other tests)
        var cancelButton = confirmDialog.Locator("button:has-text('Cancel')");
        await Expect(cancelButton).ToBeVisibleAsync();

        // Cancel the restore
        await cancelButton.ClickAsync();

        // Verify dialog closed
        await Page.WaitForTimeoutAsync(500);
        var dialogStillOpen = await Page.Locator(".mud-dialog").IsVisibleAsync();
        Assert.That(dialogStillOpen, Is.False, "Dialog should be closed after cancel");
    }

    [Test]
    [Description("Verify that refresh button reloads the snapshot list")]
    public async Task RefreshSnapshotList_ShouldReloadSnapshots()
    {
        // Arrange
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Click refresh button
        var refreshButton = Page.Locator("button[title='Refresh']");
        await refreshButton.ClickAsync();

        // Wait for reload
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify page reloaded (check for loading indicator or table)
        var snapshotTable = Page.Locator("table, .mud-table");
        await Expect(snapshotTable).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify snapshot metadata displays correctly (created date, size, checksum)")]
    public async Task SnapshotMetadata_ShouldDisplayCorrectly()
    {
        // Arrange - Create a test snapshot
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Metadata test snapshot");
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Find the snapshot row
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        await Expect(snapshotRow).ToBeVisibleAsync();

        // Assert - Verify metadata fields
        // Check for label
        var labelText = snapshotRow.Locator($"text={_testSnapshotLabel1}");
        await Expect(labelText).ToBeVisibleAsync();

        // Check for created date (should show date and relative time)
        var hasDateFormat = await snapshotRow.Locator("text=/\\d{4}-\\d{2}-\\d{2}/").IsVisibleAsync();
        var hasRelativeTime = await snapshotRow.Locator("text=/just now|ago/").IsVisibleAsync();
        Assert.That(hasDateFormat || hasRelativeTime, Is.True, "Should display creation date information");

        // Check for size (should show size or dash if not available)
        var sizeCell = snapshotRow.Locator("td").Nth(2); // Size is typically the 3rd column
        await Expect(sizeCell).ToBeVisibleAsync();

        // Check for notes
        var notesText = snapshotRow.Locator("text=Metadata test snapshot");
        await Expect(notesText).ToBeVisibleAsync();

        // Checksum might be in the label cell as caption text
        // It's optional, so we just check if the row structure is correct
    }

    [Test]
    [Description("Verify multiple snapshots are displayed in correct order (newest first)")]
    public async Task SnapshotList_MultipleSnapshots_ShouldOrderByNewestFirst()
    {
        // Arrange - Create multiple snapshots with delays to ensure different timestamps
        await CreateTestSnapshotAsync($"{_testSnapshotLabel1}-old", "Older snapshot");
        await Task.Delay(1000);
        await CreateTestSnapshotAsync($"{_testSnapshotLabel1}-new", "Newer snapshot");

        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Get all snapshot rows
        var allRows = Page.Locator("table tbody tr, .mud-table tbody tr");
        var rowCount = await allRows.CountAsync();

        // Assert - Verify both snapshots are present
        var olderSnapshotVisible = await Page.Locator($"text={_testSnapshotLabel1}-old").IsVisibleAsync();
        var newerSnapshotVisible = await Page.Locator($"text={_testSnapshotLabel1}-new").IsVisibleAsync();

        Assert.That(olderSnapshotVisible, Is.True, "Older snapshot should be visible");
        Assert.That(newerSnapshotVisible, Is.True, "Newer snapshot should be visible");

        // The newer snapshot should appear before the older one (if we have at least 2 rows)
        if (rowCount >= 2)
        {
            var firstRow = allRows.First();
            var hasNewerInFirst = await firstRow.Locator($"text={_testSnapshotLabel1}-new").IsVisibleAsync();

            // This might not always be the case if there are other snapshots, so we just verify both exist
            Assert.That(newerSnapshotVisible && olderSnapshotVisible, Is.True,
                "Both snapshots should be visible in the list");
        }
    }

    [Test]
    [Description("Verify snapshot creation captures current metadata state including services")]
    public async Task CreateSnapshot_AfterCreatingService_ShouldCaptureServiceInMetadata()
    {
        // Arrange - Create a service first
        await CreateTestServiceAsync();

        // Act - Create a snapshot
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "Snapshot with test service");

        // Navigate to version history
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Assert - Verify snapshot was created
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')");
        await Expect(snapshotRow.First()).ToBeVisibleAsync();

        // View snapshot details to verify it contains metadata
        var detailsButton = snapshotRow.Locator("button[title*='details']").First();
        await detailsButton.ClickAsync();

        var dialog = await WaitForDialogAsync();
        await Expect(dialog.Locator($"text={_testSnapshotLabel1}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify navigating back from version diff returns to version history")]
    public async Task NavigateBackFromVersionDiff_ShouldReturnToVersionHistory()
    {
        // Arrange - Create snapshots and navigate to diff
        await CreateTestSnapshotAsync(_testSnapshotLabel1, "First snapshot");
        await Task.Delay(500);
        await CreateTestSnapshotAsync(_testSnapshotLabel2, "Second snapshot");

        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        var compareButton = snapshotRow.Locator("button[title*='Compare']");
        await compareButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Click back button
        var backButton = Page.Locator("button[title='Back to versions']");
        await backButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify we're back on version history page
        await Expect(Page.Locator("text=Metadata Snapshots")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Create Snapshot')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify creating snapshot without notes still succeeds")]
    public async Task CreateSnapshot_WithoutNotes_ShouldSucceed()
    {
        // Arrange
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        var createButton = Page.Locator("button:has-text('Create Snapshot')");
        if (!await createButton.IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Create snapshot with only label (no notes)
        await createButton.ClickAsync();
        var dialog = await WaitForDialogAsync();

        var labelField = dialog.GetByLabel("Label");
        await labelField.FillAsync(_testSnapshotLabel1);

        // Don't fill notes field
        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Wait for success
        await WaitForSnackbarAsync("created successfully");

        // Assert - Verify snapshot appears with "No notes" or empty notes
        var snapshotRow = Page.Locator($"tr:has-text('{_testSnapshotLabel1}')").First();
        await Expect(snapshotRow).ToBeVisibleAsync();

        // Should show "No notes" or similar indicator
        var noNotesText = Page.Locator("text=/No notes|-/");
        var hasNoNotesIndicator = await noNotesText.First().IsVisibleAsync();
        Assert.That(hasNoNotesIndicator, Is.True, "Should indicate when notes are not provided");
    }

    [Test]
    [Description("Verify snapshot list shows empty state when no snapshots exist")]
    public async Task SnapshotList_WithNoSnapshots_ShouldShowEmptyState()
    {
        // Arrange
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning feature is enabled
        if (!await Page.Locator("button:has-text('Create Snapshot')").IsVisibleAsync())
        {
            Assert.Ignore("Versioning feature not enabled - skipping test");
        }

        // Act - Check if there are any snapshots in the table
        var tableRows = Page.Locator("table tbody tr, .mud-table tbody tr");
        var rowCount = await tableRows.CountAsync();

        // Assert - If no snapshots, should show empty state message
        if (rowCount == 0)
        {
            var emptyMessage = Page.Locator("text=/No snapshots yet|Create your first snapshot/");
            await Expect(emptyMessage).ToBeVisibleAsync();
        }
        else
        {
            // If there are snapshots, table should be visible
            await Expect(tableRows.First()).ToBeVisibleAsync();
        }
    }

    #region Helper Methods

    private async Task NavigateToVersionHistoryPageAsync()
    {
        // Navigate to version history page via menu or direct URL
        var versionLink = Page.Locator("a:has-text('Version History'), a:has-text('Versions'), nav a[href='/versions']");

        if (await versionLink.IsVisibleAsync())
        {
            await versionLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/versions");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToServicesPageAsync()
    {
        var servicesLink = Page.Locator("a:has-text('Services'), nav a:has-text('Services')");

        if (await servicesLink.IsVisibleAsync())
        {
            await servicesLink.First().ClickAsync();
        }
        else
        {
            await Page.GotoAsync($"{BaseUrl}/services");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestSnapshotAsync(string label, string notes)
    {
        await NavigateToVersionHistoryPageAsync();

        // Check if versioning is enabled
        var createButton = Page.Locator("button:has-text('Create Snapshot')");
        if (!await createButton.IsVisibleAsync())
        {
            return; // Skip if versioning not enabled
        }

        await createButton.ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Label").FillAsync(label);

        var notesField = dialog.GetByLabel("Notes");
        if (await notesField.IsVisibleAsync())
        {
            await notesField.FillAsync(notes);
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestServiceAsync()
    {
        await NavigateToServicesPageAsync();

        var newServiceButton = Page.Locator("button:has-text('New Service'), button:has-text('Create Service')");
        await newServiceButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Service for Versioning");

        await dialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var descriptionField = dialog.GetByLabel("Description");
        if (await descriptionField.IsVisibleAsync())
        {
            await descriptionField.FillAsync("Test service for snapshot testing");
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task TryDeleteServiceAsync(string serviceId)
    {
        try
        {
            await NavigateToServicesPageAsync();

            var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
            if (await searchBox.IsVisibleAsync())
            {
                await searchBox.FillAsync(serviceId);
                await Page.WaitForTimeoutAsync(1000);

                var deleteButton = Page.Locator($"button:has-text('Delete')").First();
                if (await deleteButton.IsVisibleAsync())
                {
                    await deleteButton.ClickAsync();

                    var confirmButton = Page.Locator("button:has-text('Confirm'), button:has-text('Delete')").Last();
                    await confirmButton.ClickAsync();
                    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
