// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Alert Management in the Honua Admin Blazor application.
/// These tests verify alert rule creation, editing, deletion, notification channels,
/// and alert history viewing with comprehensive filtering capabilities.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("AlertManagement")]
public class AlertManagementTests : BaseE2ETest
{
    private string _testAlertRuleId = null!;
    private string _testAlertRuleName = null!;
    private string _testChannelId = null!;
    private string _testChannelName = null!;

    [SetUp]
    public async Task AlertTestSetUp()
    {
        // Generate unique identifiers for this test run to ensure isolation
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        _testAlertRuleId = $"e2e-alert-rule-{uniqueId}";
        _testAlertRuleName = $"E2E Test Alert Rule {uniqueId}";
        _testChannelId = $"e2e-channel-{uniqueId}";
        _testChannelName = $"E2E Test Channel {uniqueId}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task AlertTestTearDown()
    {
        // Cleanup: Delete test alert rules and channels if they exist
        try
        {
            // Navigate to alerts page
            await NavigateToAlertsPageAsync();

            // Search and delete test alert rule
            var ruleSearchBox = Page.Locator("input[placeholder*='Search rules']");
            if (await ruleSearchBox.IsVisibleAsync())
            {
                await ruleSearchBox.FillAsync(_testAlertRuleName);
                await Page.WaitForTimeoutAsync(1000); // Wait for search to filter

                // Try to delete if found
                var deleteButton = Page.Locator("button[title='Delete']").First();
                if (await deleteButton.IsVisibleAsync())
                {
                    await deleteButton.ClickAsync();

                    // Confirm deletion in dialog
                    var confirmButton = Page.Locator("button:has-text('Delete')").Last();
                    await confirmButton.ClickAsync();
                    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
            }

            // Switch to Notification Channels tab
            var channelsTab = Page.Locator("button:has-text('Notification Channels')");
            if (await channelsTab.IsVisibleAsync())
            {
                await channelsTab.ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Search and delete test channel
                var channelSearchBox = Page.Locator("input[placeholder*='Search channels']");
                if (await channelSearchBox.IsVisibleAsync())
                {
                    await channelSearchBox.FillAsync(_testChannelName);
                    await Page.WaitForTimeoutAsync(1000);

                    // Try to delete if found
                    var deleteChannelButton = Page.Locator("button[title='Delete']").First();
                    if (await deleteChannelButton.IsVisibleAsync())
                    {
                        await deleteChannelButton.ClickAsync();

                        // Confirm deletion
                        var confirmChannelButton = Page.Locator("button:has-text('Delete')").Last();
                        await confirmChannelButton.ClickAsync();
                        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    [Description("Verify that users can navigate to the alert configuration page")]
    public async Task NavigateToAlerts_FromDashboard_ShouldDisplayAlertsPage()
    {
        // Act - Navigate to alerts page
        await NavigateToAlertsPageAsync();

        // Assert - Verify page loaded correctly
        await Expect(Page.Locator("h4:has-text('Alert Configuration')")).ToBeVisibleAsync();

        // Verify all main tabs are present
        await Expect(Page.Locator("button:has-text('Alert Rules')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Notification Channels')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Alert History')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that alert configuration displays statistics correctly")]
    public async Task ViewAlertConfiguration_WithStats_ShouldDisplayStatCards()
    {
        // Arrange
        await NavigateToAlertsPageAsync();

        // Assert - Verify stat cards are displayed
        await Expect(Page.Locator("text=Firing Alerts")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Active Rules")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Active Channels")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Alerts (24h)")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can create a new alert rule with basic fields")]
    public async Task CreateAlertRule_WithBasicFields_ShouldSucceed()
    {
        // Arrange
        await NavigateToAlertsPageAsync();

        // Act - Click "Create Rule" button
        var createButton = Page.Locator("button:has-text('Create Rule')");
        await createButton.ClickAsync();

        // Wait for navigation to rule editor
        await Page.WaitForURLAsync("**/alerts/rules/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Fill in basic alert rule fields
        await Page.GetByLabel("Rule Name").FillAsync(_testAlertRuleName);
        await Page.GetByLabel("Description").FillAsync("This is a test alert rule for E2E testing");

        // Select severity
        await Page.GetByLabel("Severity").ClickAsync();
        var warningOption = Page.Locator("li:has-text('Warning')").First();
        await warningOption.ClickAsync();

        // Fill in alert expression
        await Page.GetByLabel("Alert Expression (Prometheus PromQL)").FillAsync("cpu_usage_percent > 80");

        // Fill in duration
        await Page.GetByLabel("Duration").FillAsync("5m");

        // Ensure enabled switch is on
        var enabledSwitch = Page.Locator("label:has-text('Enabled') input[type='checkbox']");
        if (!await enabledSwitch.IsCheckedAsync())
        {
            await enabledSwitch.ClickAsync();
        }

        // Submit form
        var saveButton = Page.Locator("button:has-text('Save')").First();
        await saveButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("Alert rule created successfully");

        // Assert - Verify redirected back to alerts page
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/alerts", "should be redirected to alerts page after creation");

        // Verify the new rule appears in the list
        await Expect(Page.Locator($"text={_testAlertRuleName}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can create an alert rule with labels and annotations")]
    public async Task CreateAlertRule_WithLabelsAndAnnotations_ShouldSucceed()
    {
        // Arrange
        await NavigateToAlertsPageAsync();
        await Page.Locator("button:has-text('Create Rule')").ClickAsync();
        await Page.WaitForURLAsync("**/alerts/rules/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Act - Fill basic fields
        await Page.GetByLabel("Rule Name").FillAsync(_testAlertRuleName);
        await Page.GetByLabel("Severity").ClickAsync();
        await Page.Locator("li:has-text('Critical')").First().ClickAsync();
        await Page.GetByLabel("Alert Expression (Prometheus PromQL)").FillAsync("memory_usage_percent > 90");
        await Page.GetByLabel("Duration").FillAsync("10m");

        // Add a label
        var newLabelKey = Page.Locator("input[label='New Label Key']").Last();
        var newLabelValue = Page.Locator("input[label='New Label Value']").Last();
        await newLabelKey.FillAsync("environment");
        await newLabelValue.FillAsync("production");
        await Page.Locator("button:has-text('Add')").First().ClickAsync();

        // Add an annotation
        var newAnnotationKey = Page.Locator("input[label='New Annotation Key']").Last();
        var newAnnotationValue = Page.Locator("input[label='New Annotation Value']").Last();
        await newAnnotationKey.FillAsync("summary");
        await newAnnotationValue.FillAsync("Memory usage is critically high");
        await Page.Locator("button:has-text('Add')").Last().ClickAsync();

        // Submit form
        await Page.Locator("button:has-text('Save')").First().ClickAsync();
        await WaitForSnackbarAsync("Alert rule created successfully");

        // Assert - Verify redirected and rule exists
        await Expect(Page.Locator($"text={_testAlertRuleName}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that alert rule validation works correctly")]
    public async Task CreateAlertRule_WithMissingRequiredFields_ShouldShowValidation()
    {
        // Arrange
        await NavigateToAlertsPageAsync();
        await Page.Locator("button:has-text('Create Rule')").ClickAsync();
        await Page.WaitForURLAsync("**/alerts/rules/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Act - Try to save without filling required fields
        var saveButton = Page.Locator("button:has-text('Save')").First();

        // Assert - Save button should be disabled due to validation
        await Expect(saveButton).ToBeDisabledAsync();
    }

    [Test]
    [Description("Verify that users can edit an existing alert rule")]
    public async Task EditAlertRule_ExistingRule_ShouldSucceed()
    {
        // Arrange - Create a test rule first
        await CreateTestAlertRuleAsync();
        await NavigateToAlertsPageAsync();

        // Act - Search for the rule
        var searchBox = Page.Locator("input[placeholder*='Search rules']");
        await searchBox.FillAsync(_testAlertRuleName);
        await Page.WaitForTimeoutAsync(1000);

        // Click edit button
        var editButton = Page.Locator("button[title='Edit']").First();
        await editButton.ClickAsync();

        // Wait for navigation to edit page
        await Page.WaitForURLAsync("**/alerts/rules/*/edit", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Update the rule name
        var updatedName = $"{_testAlertRuleName} Updated";
        var nameField = Page.GetByLabel("Rule Name");
        await nameField.FillAsync(updatedName);

        // Update description
        await Page.GetByLabel("Description").FillAsync("Updated description for E2E test");

        // Save changes
        await Page.Locator("button:has-text('Save')").First().ClickAsync();
        await WaitForSnackbarAsync("Alert rule updated successfully");

        // Assert - Verify updated name appears
        await Expect(Page.Locator($"text={updatedName}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can delete an alert rule")]
    public async Task DeleteAlertRule_ExistingRule_ShouldSucceed()
    {
        // Arrange - Create a test rule first
        await CreateTestAlertRuleAsync();
        await NavigateToAlertsPageAsync();

        // Act - Search for the rule
        var searchBox = Page.Locator("input[placeholder*='Search rules']");
        await searchBox.FillAsync(_testAlertRuleName);
        await Page.WaitForTimeoutAsync(1000);

        // Click delete button
        var deleteButton = Page.Locator("button[title='Delete']").First();
        await deleteButton.ClickAsync();

        // Confirm deletion in dialog
        var dialog = await WaitForDialogAsync();
        await Expect(dialog.Locator("text=Are you sure")).ToBeVisibleAsync();

        var confirmButton = dialog.Locator("button:has-text('Delete')");
        await confirmButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("deleted successfully");

        // Assert - Verify rule is removed from list
        await Page.WaitForTimeoutAsync(1000);
        var deletedRuleRow = Page.Locator($"text={_testAlertRuleName}");
        await Expect(deletedRuleRow).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Verify that users can test an alert rule")]
    public async Task TestAlertRule_ExistingRule_ShouldSendTestAlert()
    {
        // Arrange - Create a test rule first
        await CreateTestAlertRuleAsync();
        await NavigateToAlertsPageAsync();

        // Act - Search for the rule
        var searchBox = Page.Locator("input[placeholder*='Search rules']");
        await searchBox.FillAsync(_testAlertRuleName);
        await Page.WaitForTimeoutAsync(1000);

        // Click test alert button
        var testButton = Page.Locator("button[title='Test Alert']").First();
        await testButton.ClickAsync();

        // Assert - Verify success notification
        await WaitForSnackbarAsync("Test alert sent successfully");
    }

    [Test]
    [Description("Verify that alert rules table displays all necessary information")]
    public async Task ViewAlertRules_InTable_ShouldDisplayAllColumns()
    {
        // Arrange
        await NavigateToAlertsPageAsync();

        // Assert - Verify all table headers are present
        await Expect(Page.Locator("th:has-text('Name')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Severity')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Status')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Last Fired')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Channels')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Actions')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can switch to notification channels tab")]
    public async Task ViewNotificationChannels_SwitchTab_ShouldDisplayChannelsTable()
    {
        // Arrange
        await NavigateToAlertsPageAsync();

        // Act - Switch to Notification Channels tab
        var channelsTab = Page.Locator("button:has-text('Notification Channels')");
        await channelsTab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify channels table headers
        await Expect(Page.Locator("th:has-text('Name')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Type')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Status')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Last Used')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Alert Count')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Actions')")).ToBeVisibleAsync();

        // Verify "Add Channel" button is present
        await Expect(Page.Locator("button:has-text('Add Channel')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can navigate to alert history page")]
    public async Task NavigateToAlertHistory_FromConfiguration_ShouldDisplayHistoryPage()
    {
        // Arrange
        await NavigateToAlertsPageAsync();

        // Act - Switch to Alert History tab
        var historyTab = Page.Locator("button:has-text('Alert History')");
        await historyTab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Click "View Full Alert History" button
        var viewHistoryButton = Page.Locator("button:has-text('View Full Alert History')");
        await viewHistoryButton.ClickAsync();

        // Wait for navigation
        await Page.WaitForURLAsync("**/alerts/history", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Verify history page loaded
        await Expect(Page.Locator("h4:has-text('Alert History')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Back to Configuration')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that alert history page displays filter panel")]
    public async Task ViewAlertHistory_FilterPanel_ShouldDisplayAllFilterOptions()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/alerts/history");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Expand filters panel if collapsed
        var filtersPanel = Page.Locator("div:has-text('Filters')").First();
        if (await filtersPanel.IsVisibleAsync())
        {
            await filtersPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Assert - Verify all filter fields are present
        await Expect(Page.GetByLabel("Severity")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Status")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Start Date")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("End Date")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Search")).ToBeVisibleAsync();

        // Verify filter action buttons
        await Expect(Page.Locator("button:has-text('Apply Filters')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Clear Filters')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can filter alert history by severity")]
    public async Task FilterAlertHistory_BySeverity_ShouldApplyFilter()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/alerts/history");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Expand filters if needed
        var filtersPanel = Page.Locator("div:has-text('Filters')").First();
        if (await filtersPanel.IsVisibleAsync())
        {
            await filtersPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Act - Select severity filter
        await Page.GetByLabel("Severity").ClickAsync();
        var criticalOption = Page.Locator("li:has-text('Critical')").First();
        await criticalOption.ClickAsync();

        // Apply filters
        await Page.Locator("button:has-text('Apply Filters')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify filter was applied (even if no results)
        // The page should still display the table or "no results" message
        var noRecordsMessage = Page.Locator("text=No alerts match your filters");
        var tableRows = Page.Locator("tbody tr");

        // Either we have filtered results or a "no matches" message
        var hasNoResults = await noRecordsMessage.IsVisibleAsync();
        var hasResults = await tableRows.CountAsync() > 0;

        (hasNoResults || hasResults).Should().BeTrue("should show either filtered results or no results message");
    }

    [Test]
    [Description("Verify that users can search alert history by text")]
    public async Task SearchAlertHistory_ByText_ShouldFilterResults()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/alerts/history");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Expand filters if needed
        var filtersPanel = Page.Locator("div:has-text('Filters')").First();
        if (await filtersPanel.IsVisibleAsync())
        {
            await filtersPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Act - Enter search text
        await Page.GetByLabel("Search").FillAsync("test search query");

        // Apply filters
        await Page.Locator("button:has-text('Apply Filters')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify search was applied
        var searchInput = Page.GetByLabel("Search");
        var searchValue = await searchInput.InputValueAsync();
        searchValue.Should().Be("test search query", "search text should be retained");
    }

    [Test]
    [Description("Verify that users can clear all filters in alert history")]
    public async Task ClearAlertHistoryFilters_AfterApplying_ShouldResetFilters()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/alerts/history");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Expand filters
        var filtersPanel = Page.Locator("div:has-text('Filters')").First();
        if (await filtersPanel.IsVisibleAsync())
        {
            await filtersPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Apply some filters
        await Page.GetByLabel("Search").FillAsync("test");
        await Page.GetByLabel("Severity").ClickAsync();
        await Page.Locator("li:has-text('Warning')").First().ClickAsync();

        // Act - Clear filters
        await Page.Locator("button:has-text('Clear Filters')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify filters are cleared
        var searchInput = Page.GetByLabel("Search");
        var searchValue = await searchInput.InputValueAsync();
        searchValue.Should().BeEmpty("search should be cleared");
    }

    [Test]
    [Description("Verify that alert history table displays all necessary columns")]
    public async Task ViewAlertHistory_Table_ShouldDisplayAllColumns()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/alerts/history");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify all table headers are present
        await Expect(Page.Locator("th:has-text('Time')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Rule')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Severity')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Status')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Message')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Duration')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Channels')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Actions')")).ToBeVisibleAsync();
    }

    #region Helper Methods

    /// <summary>
    /// Navigates to the alerts configuration page.
    /// </summary>
    private async Task NavigateToAlertsPageAsync()
    {
        // Try to find alerts link in navigation
        var alertsLink = Page.Locator("a:has-text('Alerts'), nav a:has-text('Alerts')");

        if (await alertsLink.IsVisibleAsync())
        {
            await alertsLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/alerts");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Creates a test alert rule for use in tests that need an existing rule.
    /// </summary>
    private async Task CreateTestAlertRuleAsync()
    {
        await NavigateToAlertsPageAsync();

        var createButton = Page.Locator("button:has-text('Create Rule')");
        await createButton.ClickAsync();

        await Page.WaitForURLAsync("**/alerts/rules/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Fill in basic alert rule fields
        await Page.GetByLabel("Rule Name").FillAsync(_testAlertRuleName);
        await Page.GetByLabel("Description").FillAsync("Test alert rule for E2E testing");

        // Select severity
        await Page.GetByLabel("Severity").ClickAsync();
        var warningOption = Page.Locator("li:has-text('Warning')").First();
        await warningOption.ClickAsync();

        // Fill expression and duration
        await Page.GetByLabel("Alert Expression (Prometheus PromQL)").FillAsync("cpu_usage_percent > 80");
        await Page.GetByLabel("Duration").FillAsync("5m");

        // Save
        var saveButton = Page.Locator("button:has-text('Save')").First();
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion
}
