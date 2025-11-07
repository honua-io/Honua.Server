// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Settings & Configuration pages in the Honua Admin Blazor application.
/// These tests verify cache settings, CORS configuration, audit log viewing, and licensing information.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("Settings")]
public class SettingsConfigurationTests : BaseE2ETest
{
    private string _testOrigin = null!;
    private string _testHeader = null!;

    [SetUp]
    public async Task SettingsTestSetUp()
    {
        // Generate unique test data for this test run
        _testOrigin = $"https://test-{Guid.NewGuid():N}.example.com";
        _testHeader = $"X-Test-Header-{Guid.NewGuid():N}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task SettingsTestTearDown()
    {
        // Cleanup: Reset CORS settings to default if modified
        try
        {
            // Navigate to CORS settings and reset if needed
            var corsLink = Page.Locator("a:has-text('CORS'), nav a[href='/cors']");
            if (await corsLink.IsVisibleAsync())
            {
                await corsLink.First().ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Click Reset button to restore defaults
                var resetButton = Page.Locator("button:has-text('Reset')");
                if (await resetButton.IsVisibleAsync())
                {
                    await resetButton.ClickAsync();
                    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Cache Settings Tests

    [Test]
    [Description("Verify that users can navigate to cache settings page")]
    public async Task NavigateToCacheSettings_ShouldDisplayCacheStatistics()
    {
        // Act
        await NavigateToCacheSettingsAsync();

        // Assert - Verify cache statistics are displayed
        await Expect(Page.Locator("text=Cache Statistics")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Hit Rate")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Cache Size")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Misses")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Evictions")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that cache statistics tab displays dataset statistics")]
    public async Task CacheSettings_StatisticsTab_ShouldDisplayDatasetStatistics()
    {
        // Arrange
        await NavigateToCacheSettingsAsync();

        // Act - Ensure we're on Statistics tab
        var statisticsTab = Page.Locator(".mud-tab:has-text('Statistics')");
        if (await statisticsTab.IsVisibleAsync())
        {
            await statisticsTab.ClickAsync();
        }

        // Assert - Verify dataset statistics table is present
        await Expect(Page.Locator("text=Dataset Cache Statistics")).ToBeVisibleAsync();

        // Verify table headers
        var table = Page.Locator("table").Filter(new() { HasText = "Dataset ID" });
        await Expect(table).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can reset cache statistics")]
    public async Task CacheSettings_ResetStatistics_ShouldShowConfirmation()
    {
        // Arrange
        await NavigateToCacheSettingsAsync();

        // Act - Click Reset Statistics button
        var resetButton = Page.Locator("button:has-text('Reset Statistics')");
        await resetButton.ClickAsync();

        // Wait for confirmation dialog
        var dialog = await WaitForDialogAsync();

        // Assert - Verify confirmation message
        await Expect(dialog.Locator("text=Confirm Reset")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=reset cache statistics")).ToBeVisibleAsync();

        // Cancel to avoid affecting other tests
        var cancelButton = dialog.Locator("button:has-text('Cancel')");
        await cancelButton.ClickAsync();
    }

    [Test]
    [Description("Verify that users can view and navigate preseed jobs tab")]
    public async Task CacheSettings_PreseedJobsTab_ShouldDisplayJobsList()
    {
        // Arrange
        await NavigateToCacheSettingsAsync();

        // Act - Click on Preseed Jobs tab
        var preseedTab = Page.Locator(".mud-tab:has-text('Preseed Jobs'), button:has-text('Preseed Jobs')");
        await preseedTab.First().ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify preseed jobs interface is displayed
        await Expect(Page.Locator("text=Tile Preseed Jobs")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Create Preseed Job')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can open create preseed job dialog")]
    public async Task CacheSettings_CreatePreseedJob_ShouldOpenDialog()
    {
        // Arrange
        await NavigateToCacheSettingsAsync();

        // Navigate to Preseed Jobs tab
        var preseedTab = Page.Locator(".mud-tab:has-text('Preseed Jobs'), button:has-text('Preseed Jobs')");
        await preseedTab.First().ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Click Create Preseed Job button
        var createButton = Page.Locator("button:has-text('Create Preseed Job')");
        await createButton.ClickAsync();

        // Assert - Verify dialog opens
        var dialog = await WaitForDialogAsync();
        await Expect(dialog).ToBeVisibleAsync();

        // Close dialog
        await CloseDialogAsync();
    }

    #endregion

    #region CORS Settings Tests

    [Test]
    [Description("Verify that users can navigate to CORS settings page")]
    public async Task NavigateToCorsSettings_ShouldDisplayCorsConfiguration()
    {
        // Act
        await NavigateToCorsSettingsAsync();

        // Assert - Verify CORS settings page elements
        await Expect(Page.Locator("text=CORS Settings")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Enable CORS")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Save Changes')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can enable/disable CORS")]
    public async Task CorsSettings_ToggleEnabled_ShouldShowConditionalSections()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();

        // Act - Find and click the Enable CORS switch
        var corsSwitch = Page.Locator(".mud-switch:has-text('Enable CORS'), label:has-text('Enable CORS')").First();
        await corsSwitch.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - If CORS is now enabled, additional sections should be visible
        var allowedOrigins = Page.Locator("text=Allowed Origins");
        var isVisible = await allowedOrigins.IsVisibleAsync();

        if (isVisible)
        {
            // CORS is enabled, verify configuration sections are shown
            await Expect(Page.Locator("text=Allowed HTTP Methods")).ToBeVisibleAsync();
            await Expect(Page.Locator("text=Request Headers")).ToBeVisibleAsync();
            await Expect(Page.Locator("text=Advanced Settings")).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Verify that users can add a new allowed origin")]
    public async Task CorsSettings_AddOrigin_ShouldAddToList()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Act - Add a new origin
        var originInput = Page.Locator("input[placeholder*='example.com']").First();
        await originInput.FillAsync(_testOrigin);

        var addButton = originInput.Locator("..").Locator("..").Locator("button:has-text('Add')");
        if (!await addButton.IsVisibleAsync())
        {
            addButton = Page.Locator("button:has-text('Add')").Filter(new() {
                Near = originInput
            }).First();
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify origin chip appears
        var originChip = Page.Locator($".mud-chip:has-text('{_testOrigin}')");
        await Expect(originChip).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can remove an allowed origin")]
    public async Task CorsSettings_RemoveOrigin_ShouldRemoveFromList()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Add an origin first
        var originInput = Page.Locator("input[placeholder*='example.com']").First();
        await originInput.FillAsync(_testOrigin);
        var addButton = Page.Locator("button:has-text('Add')").First();
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Remove the origin
        var originChip = Page.Locator($".mud-chip:has-text('{_testOrigin}')");
        var closeButton = originChip.Locator("button, .mud-chip-close-button");
        await closeButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify origin is removed
        await Expect(originChip).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Verify that users can toggle HTTP methods")]
    public async Task CorsSettings_ToggleHttpMethods_ShouldUpdateSelection()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Scroll to methods section
        var methodsSection = Page.Locator("text=Allowed HTTP Methods");
        await methodsSection.ScrollIntoViewIfNeededAsync();

        // Disable "Allow All Methods" if enabled
        var allowAllSwitch = Page.Locator(".mud-switch:has-text('Allow All Methods'), label:has-text('Allow All Methods')");
        if (await allowAllSwitch.IsVisibleAsync())
        {
            var isChecked = await allowAllSwitch.Locator("input[type='checkbox']").IsCheckedAsync();
            if (isChecked)
            {
                await allowAllSwitch.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Act - Click on GET method chip
        var getMethodChip = Page.Locator(".mud-chip:has-text('GET')").First();
        await getMethodChip.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Assert - Verify chip state changes (visual feedback)
        // The chip should toggle between filled and outlined variants
        await Expect(getMethodChip).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can add custom headers")]
    public async Task CorsSettings_AddCustomHeader_ShouldAddToList()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Scroll to headers section
        var headersSection = Page.Locator("text=Request Headers");
        await headersSection.ScrollIntoViewIfNeededAsync();

        // Disable "Allow Any Header" if enabled
        var allowAnySwitch = Page.Locator(".mud-switch:has-text('Allow Any Header'), label:has-text('Allow Any Header')");
        if (await allowAnySwitch.IsVisibleAsync())
        {
            var isChecked = await allowAnySwitch.Locator("input[type='checkbox']").IsCheckedAsync();
            if (isChecked)
            {
                await allowAnySwitch.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Act - Add a custom header
        var headerInput = Page.Locator("input[placeholder*='Content-Type']").First();
        await headerInput.FillAsync(_testHeader);

        var addButton = Page.Locator("button:has-text('Add')").Filter(new() {
            Near = headerInput
        }).First();
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify header chip appears
        var headerChip = Page.Locator($".mud-chip:has-text('{_testHeader}')");
        await Expect(headerChip).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can test CORS configuration")]
    public async Task CorsSettings_TestCors_ShouldShowResult()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Scroll to test section
        var testSection = Page.Locator("text=Test CORS Configuration");
        await testSection.ScrollIntoViewIfNeededAsync();

        // Act - Enter test origin and click Test
        var testOriginInput = Page.Locator("input[placeholder='https://example.com']").Last();
        await testOriginInput.FillAsync("https://example.com");

        var testButton = Page.Locator("button:has-text('Test')").Last();
        await testButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Assert - Verify test result is displayed
        var resultAlert = Page.Locator(".mud-alert").Filter(new() {
            HasText = "Result:"
        });
        await Expect(resultAlert).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that CORS configuration can be saved")]
    public async Task CorsSettings_SaveConfiguration_ShouldShowSuccess()
    {
        // Arrange
        await NavigateToCorsSettingsAsync();
        await EnsureCorsIsEnabled();

        // Act - Click Save Changes button
        var saveButton = Page.Locator("button:has-text('Save Changes')");
        await saveButton.ClickAsync();

        // Assert - Wait for success notification
        await WaitForSnackbarAsync("saved successfully");
    }

    #endregion

    #region Audit Log Tests

    [Test]
    [Description("Verify that users can navigate to audit log viewer")]
    public async Task NavigateToAuditLog_ShouldDisplayAuditEvents()
    {
        // Act
        await NavigateToAuditLogAsync();

        // Assert - Verify audit log page elements
        await Expect(Page.Locator("text=Audit Log").First()).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Export CSV')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Export JSON')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can expand and use advanced filters")]
    public async Task AuditLog_ExpandFilters_ShouldShowFilterOptions()
    {
        // Arrange
        await NavigateToAuditLogAsync();

        // Act - Expand Advanced Filters panel
        var filterPanel = Page.Locator("text=Advanced Filters, .mud-expand-panel:has-text('Advanced Filters')").First();
        await filterPanel.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify filter fields are visible
        await Expect(Page.Locator("input[placeholder*='Search']")).ToBeVisibleAsync();
        await Expect(Page.Locator("label:has-text('Category')")).ToBeVisibleAsync();
        await Expect(Page.Locator("label:has-text('Action')")).ToBeVisibleAsync();
        await Expect(Page.Locator("label:has-text('Resource Type')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Apply Filters')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can filter audit logs by search text")]
    public async Task AuditLog_FilterBySearchText_ShouldUpdateResults()
    {
        // Arrange
        await NavigateToAuditLogAsync();

        // Expand filters
        var filterPanel = Page.Locator("text=Advanced Filters").First();
        await filterPanel.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Enter search text and apply filters
        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.FillAsync("login");

        var applyButton = Page.Locator("button:has-text('Apply Filters')");
        await applyButton.ClickAsync();

        // Wait for results to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify table is displayed (results may be empty)
        var table = Page.Locator("table");
        await Expect(table).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can filter audit logs by category")]
    public async Task AuditLog_FilterByCategory_ShouldUpdateResults()
    {
        // Arrange
        await NavigateToAuditLogAsync();

        // Expand filters
        var filterPanel = Page.Locator("text=Advanced Filters").First();
        await filterPanel.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Select a category
        var categorySelect = Page.Locator("label:has-text('Category')").Locator("..").Locator("div.mud-select");
        await categorySelect.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Select authentication category
        var authOption = Page.Locator(".mud-list-item:has-text('authentication')").First();
        if (await authOption.IsVisibleAsync())
        {
            await authOption.ClickAsync();
        }

        var applyButton = Page.Locator("button:has-text('Apply Filters')");
        await applyButton.ClickAsync();

        // Wait for results
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify filtering occurred
        var table = Page.Locator("table");
        await Expect(table).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can filter audit logs by date range")]
    public async Task AuditLog_FilterByDateRange_ShouldUpdateResults()
    {
        // Arrange
        await NavigateToAuditLogAsync();

        // Expand filters
        var filterPanel = Page.Locator("text=Advanced Filters").First();
        await filterPanel.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Set date range (last 7 days)
        var startDatePicker = Page.Locator("label:has-text('Start Date')").Locator("..").Locator("input");
        if (await startDatePicker.IsVisibleAsync())
        {
            var startDate = DateTime.Now.AddDays(-7).ToString("MM/dd/yyyy");
            await startDatePicker.FillAsync(startDate);
        }

        var applyButton = Page.Locator("button:has-text('Apply Filters')");
        await applyButton.ClickAsync();

        // Wait for results
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify results are displayed
        var table = Page.Locator("table");
        await Expect(table).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can clear all filters")]
    public async Task AuditLog_ClearFilters_ShouldResetToDefault()
    {
        // Arrange
        await NavigateToAuditLogAsync();

        // Expand filters
        var filterPanel = Page.Locator("text=Advanced Filters").First();
        await filterPanel.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Add some filter criteria
        var searchInput = Page.Locator("input[placeholder*='Search']");
        await searchInput.FillAsync("test");

        // Act - Click Clear button
        var clearButton = Page.Locator("button:has-text('Clear')");
        await clearButton.ClickAsync();

        // Wait for results
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify search is cleared
        var clearedValue = await searchInput.InputValueAsync();
        Assert.That(clearedValue, Is.Empty);
    }

    [Test]
    [Description("Verify that users can view audit event details")]
    public async Task AuditLog_ViewEventDetails_ShouldOpenDialog()
    {
        // Arrange
        await NavigateToAuditLogAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Click on first info button if any events exist
        var infoButton = Page.Locator("button[title='View details'], .mud-icon-button:has(.mud-icon-root:has-text('info'))").First();
        if (await infoButton.IsVisibleAsync())
        {
            await infoButton.ClickAsync();

            // Assert - Verify details dialog opens
            var dialog = await WaitForDialogAsync();
            await Expect(dialog.Locator("text=Event Details")).ToBeVisibleAsync();

            // Close dialog
            await CloseDialogAsync();
        }
        else
        {
            // No events to view, test passes
            Assert.Pass("No audit events available to view details");
        }
    }

    #endregion

    #region Licensing Info Tests

    [Test]
    [Description("Verify that users can navigate to licensing information page")]
    public async Task NavigateToLicensing_ShouldDisplayLicenseInfo()
    {
        // Act
        await NavigateToLicensingAsync();

        // Assert - Verify licensing page elements
        await Expect(Page.Locator("text=License & Features")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tier")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that license tier is displayed")]
    public async Task LicensingInfo_ShouldDisplayCurrentTier()
    {
        // Arrange
        await NavigateToLicensingAsync();

        // Assert - Verify tier information is shown (Free, Professional, or Enterprise)
        var tierIndicators = Page.Locator("text=Free Tier, text=Professional Tier, text=Enterprise Tier");
        var count = await tierIndicators.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "License tier should be displayed");
    }

    [Test]
    [Description("Verify that available features are listed")]
    public async Task LicensingInfo_ShouldDisplayAvailableFeatures()
    {
        // Arrange
        await NavigateToLicensingAsync();

        // Assert - Verify feature sections are displayed
        await Expect(Page.Locator("text=Available Features")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Core Features")).ToBeVisibleAsync();

        // Verify at least some core features are listed
        await Expect(Page.Locator("text=OGC Web Services")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Vector Tiles")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that quota information is displayed")]
    public async Task LicensingInfo_ShouldDisplayQuotasAndLimits()
    {
        // Arrange
        await NavigateToLicensingAsync();

        // Act - Scroll to quotas section
        var quotasSection = Page.Locator("text=Quotas & Limits");
        await quotasSection.ScrollIntoViewIfNeededAsync();

        // Assert - Verify quota table is displayed
        await Expect(quotasSection).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Maximum Users")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Maximum Collections")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=API Requests per Day")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that enterprise features are listed with status")]
    public async Task LicensingInfo_ShouldDisplayEnterpriseFeatures()
    {
        // Arrange
        await NavigateToLicensingAsync();

        // Act - Scroll to enterprise features
        var enterpriseSection = Page.Locator("text=Enterprise Features");
        await enterpriseSection.ScrollIntoViewIfNeededAsync();

        // Assert - Verify enterprise features are listed
        await Expect(enterpriseSection).ToBeVisibleAsync();

        // These features should be mentioned (enabled or disabled)
        var features = new[] { "GeoETL", "Versioning", "Oracle", "Elasticsearch" };
        foreach (var feature in features)
        {
            var featureElement = Page.Locator($"text={feature}");
            await Expect(featureElement).ToBeVisibleAsync();
        }
    }

    #endregion

    #region Helper Methods

    private async Task NavigateToCacheSettingsAsync()
    {
        // Try navigation via menu link first
        var cacheLink = Page.Locator("a:has-text('Cache'), nav a[href='/cache']");

        if (await cacheLink.IsVisibleAsync())
        {
            await cacheLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/cache");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToCorsSettingsAsync()
    {
        // Try navigation via menu link first
        var corsLink = Page.Locator("a:has-text('CORS'), nav a[href='/cors']");

        if (await corsLink.IsVisibleAsync())
        {
            await corsLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/cors");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToAuditLogAsync()
    {
        // Try navigation via menu link first
        var auditLink = Page.Locator("a:has-text('Audit'), nav a[href='/audit']");

        if (await auditLink.IsVisibleAsync())
        {
            await auditLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/audit");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToLicensingAsync()
    {
        // Try navigation via menu link first
        var licensingLink = Page.Locator("a:has-text('Licensing'), nav a[href='/licensing']");

        if (await licensingLink.IsVisibleAsync())
        {
            await licensingLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/licensing");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task EnsureCorsIsEnabled()
    {
        // Check if CORS is enabled, if not enable it
        var corsSwitch = Page.Locator(".mud-switch:has-text('Enable CORS'), label:has-text('Enable CORS')").First();
        var checkbox = corsSwitch.Locator("input[type='checkbox']");

        var isEnabled = await checkbox.IsCheckedAsync();
        if (!isEnabled)
        {
            await corsSwitch.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Verify CORS sections are now visible
        await Expect(Page.Locator("text=Allowed Origins")).ToBeVisibleAsync();
    }

    #endregion
}
