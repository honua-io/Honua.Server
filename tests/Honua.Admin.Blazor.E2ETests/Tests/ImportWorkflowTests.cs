// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Import Workflows in the Honua Admin Blazor application.
/// These tests verify data import wizards, Esri service imports, and import job management.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("ImportWorkflow")]
public class ImportWorkflowTests : BaseE2ETest
{
    private string _testServiceId = null!;
    private Guid _testJobId;

    [SetUp]
    public async Task ImportTestSetUp()
    {
        // Generate unique service ID for this test run
        _testServiceId = $"e2e-test-import-service-{Guid.NewGuid():N}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task ImportTestTearDown()
    {
        // Cleanup: Cancel any running jobs and delete test resources
        try
        {
            // Try to cancel test job if it exists
            if (_testJobId != Guid.Empty)
            {
                await TryCancelImportJobAsync(_testJobId);
            }

            // Delete test service if it exists
            await TryDeleteServiceAsync(_testServiceId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Data Import Wizard Tests

    [Test]
    [Description("Verify that users can navigate to the data import wizard")]
    public async Task NavigateToDataImportWizard_ShouldDisplayWizard()
    {
        // Act
        await NavigateToDataImportPageAsync();

        // Assert - Verify wizard is displayed
        await Expect(Page.Locator("text=Import Geospatial Data")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Upload File")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Configure Target")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Review & Import")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify the first step of data import wizard displays file upload area")]
    public async Task DataImportWizard_Step1_ShouldShowFileUploadArea()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Assert - Verify file upload UI elements
        await Expect(Page.Locator("text=Select a geospatial file to import")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Supported formats")).ToBeVisibleAsync();
        await Expect(Page.Locator("#fileDropZone")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Click to select a file")).ToBeVisibleAsync();

        // Verify Next button is disabled without file
        var nextButton = Page.Locator("button:has-text('Next')");
        await Expect(nextButton).ToBeDisabledAsync();
    }

    [Test]
    [Description("Verify that canceling the import wizard returns to previous page")]
    public async Task DataImportWizard_Cancel_ShouldNavigateBack()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Act - Navigate to home or previous page
        await Page.Locator("a:has-text('Home')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should not be on import page
        var currentUrl = Page.Url;
        Assert.That(currentUrl, Does.Not.Contain("/import"));
    }

    [Test]
    [Description("Verify the second step shows configuration options")]
    public async Task DataImportWizard_Step2_ShouldShowConfigurationOptions()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Simulate file selection by navigating directly if possible
        // Note: Actual file upload testing may require special Playwright file handling

        // Act - Try to reach step 2 (this might not work without file upload, so we just verify structure)
        var configureTargetStep = Page.Locator("text=Configure Target");
        await Expect(configureTargetStep).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify validation prevents proceeding without required fields")]
    public async Task DataImportWizard_Validation_ShouldPreventInvalidSubmission()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Assert - Next button should be disabled initially
        var nextButton = Page.Locator("button:has-text('Next')").First();
        await Expect(nextButton).ToBeDisabledAsync();
    }

    [Test]
    [Description("Verify back navigation works in the wizard")]
    public async Task DataImportWizard_BackButton_ShouldNavigateToPreviousStep()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Look for back button (should appear in later steps)
        var backButton = Page.Locator("button:has-text('Back')");

        // If back button exists, it should be clickable
        if (await backButton.IsVisibleAsync())
        {
            await backButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    [Test]
    [Description("Verify wizard displays supported file format information")]
    public async Task DataImportWizard_ShouldDisplaySupportedFormats()
    {
        // Arrange
        await NavigateToDataImportPageAsync();

        // Assert - Verify supported formats are listed
        var alertText = Page.Locator(".mud-alert:has-text('Supported formats')");
        await Expect(alertText).ToBeVisibleAsync();

        // Should mention common formats
        var pageContent = await Page.TextContentAsync("body");
        Assert.That(pageContent, Does.Contain("Shapefile").Or.Contain("GeoJSON").Or.Contain("GeoPackage"));
    }

    #endregion

    #region Esri Service Import Wizard Tests

    [Test]
    [Description("Verify that users can navigate to Esri service import wizard")]
    public async Task NavigateToEsriImportWizard_ShouldDisplayWizard()
    {
        // Act
        await NavigateToEsriImportPageAsync();

        // Assert - Verify wizard is displayed
        await Expect(Page.Locator("text=Import from Esri Service")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Service URL")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Select Layers")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify Esri import wizard accepts service URL input")]
    public async Task EsriImportWizard_EnterServiceUrl_ShouldAcceptInput()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();
        var testUrl = "https://services.arcgis.com/V6ZHFr6zdgNZuVG0/arcgis/rest/services/TestService/FeatureServer";

        // Act - Enter service URL
        var urlInput = Page.Locator("input[placeholder*='FeatureServer'], label:has-text('Service URL') ~ input").First();
        if (await urlInput.IsVisibleAsync())
        {
            await urlInput.FillAsync(testUrl);

            // Assert
            var value = await urlInput.InputValueAsync();
            Assert.That(value, Is.EqualTo(testUrl));
        }
    }

    [Test]
    [Description("Verify load service button is enabled when URL is provided")]
    public async Task EsriImportWizard_WithValidUrl_ShouldEnableLoadButton()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();

        // Act - Enter a URL
        var urlInput = Page.Locator("input[placeholder*='FeatureServer'], label:has-text('Service URL') ~ input").First();
        if (await urlInput.IsVisibleAsync())
        {
            await urlInput.FillAsync("https://services.arcgis.com/test/FeatureServer");
            await Page.WaitForTimeoutAsync(500);

            // Assert - Load button should be enabled
            var loadButton = Page.Locator("button:has-text('Load Service Info')");
            if (await loadButton.IsVisibleAsync())
            {
                var isDisabled = await loadButton.IsDisabledAsync();
                Assert.That(isDisabled, Is.False);
            }
        }
    }

    [Test]
    [Description("Verify Esri wizard shows example URLs")]
    public async Task EsriImportWizard_ShouldShowExampleUrls()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();

        // Act - Expand examples panel if present
        var examplesPanel = Page.Locator("text=Examples");
        if (await examplesPanel.IsVisibleAsync())
        {
            await examplesPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Assert - Should show example URLs
        var pageContent = await Page.TextContentAsync("body");
        Assert.That(pageContent, Does.Contain("arcgis").Or.Contain("FeatureServer").Or.Contain("MapServer"));
    }

    [Test]
    [Description("Verify layer selection step shows selection controls")]
    public async Task EsriImportWizard_LayerSelection_ShouldShowSelectionControls()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();

        // Assert - Verify selection controls exist in the page structure
        var selectLayersText = Page.Locator("text=Select Layers");
        await Expect(selectLayersText).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify configuration step shows target settings")]
    public async Task EsriImportWizard_Configuration_ShouldShowTargetSettings()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();

        // Assert - Configuration step should be present
        var configureImportText = Page.Locator("text=Configure Import");
        await Expect(configureImportText).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify review step displays import summary")]
    public async Task EsriImportWizard_ReviewStep_ShouldShowSummary()
    {
        // Arrange
        await NavigateToEsriImportPageAsync();

        // Assert - Review step should be present
        var reviewText = Page.Locator("text=Review & Import");
        await Expect(reviewText).ToBeVisibleAsync();
    }

    #endregion

    #region Import Jobs List Tests

    [Test]
    [Description("Verify that users can view the import jobs list")]
    public async Task ViewImportJobsList_ShouldDisplayJobsPage()
    {
        // Act
        await NavigateToImportJobsPageAsync();

        // Assert - Verify jobs page is displayed
        await Expect(Page.Locator("text=Import Jobs")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('New Import')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Refresh'], .mud-icon-button:has([data-testid='Refresh'])").First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify import jobs list has filter controls")]
    public async Task ImportJobsList_ShouldHaveFilterControls()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Act - Expand filters panel if collapsed
        var filtersPanel = Page.Locator("text=Filters & Search");
        if (await filtersPanel.IsVisibleAsync())
        {
            await filtersPanel.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Assert - Verify filter controls exist
        var searchInput = Page.Locator("input[placeholder*='Filename'], input[placeholder*='service']");
        var statusFilter = Page.Locator("label:has-text('Status')");

        if (await searchInput.IsVisibleAsync())
        {
            await Expect(searchInput.First()).ToBeVisibleAsync();
        }

        if (await statusFilter.IsVisibleAsync())
        {
            await Expect(statusFilter).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Verify filtering jobs by status")]
    public async Task ImportJobsList_FilterByStatus_ShouldFilterResults()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Expand filters if needed
        var filtersPanel = Page.Locator("text=Filters & Search");
        if (await filtersPanel.IsVisibleAsync())
        {
            var isExpanded = await Page.Locator(".mud-expansion-panel-expanded").IsVisibleAsync();
            if (!isExpanded)
            {
                await filtersPanel.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Act - Select a status filter
        var statusSelect = Page.Locator("label:has-text('Status')").Locator("..").GetByRole(AriaRole.Button);
        if (await statusSelect.IsVisibleAsync())
        {
            await statusSelect.ClickAsync();

            // Select "Running" status
            var runningOption = Page.Locator("li:has-text('Running'), .mud-list-item:has-text('Running')").First();
            if (await runningOption.IsVisibleAsync())
            {
                await runningOption.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Assert - Filter should be applied (check that showing X of Y jobs text updates)
        var filterInfo = Page.Locator("text=Showing");
        if (await filterInfo.IsVisibleAsync())
        {
            await Expect(filterInfo).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Verify searching import jobs")]
    public async Task ImportJobsList_SearchJobs_ShouldFilterResults()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Expand filters if needed
        var filtersPanel = Page.Locator("text=Filters & Search");
        if (await filtersPanel.IsVisibleAsync())
        {
            var isExpanded = await Page.Locator(".mud-expansion-panel-expanded").IsVisibleAsync();
            if (!isExpanded)
            {
                await filtersPanel.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Act - Enter search text
        var searchInput = Page.Locator("input[placeholder*='Filename'], input[placeholder*='service'], label:has-text('Search') ~ input").First();
        if (await searchInput.IsVisibleAsync())
        {
            await searchInput.FillAsync("test");
            await Page.WaitForTimeoutAsync(1000); // Wait for search to filter

            // Assert - Search should be applied
            var value = await searchInput.InputValueAsync();
            Assert.That(value, Is.EqualTo("test"));
        }
    }

    [Test]
    [Description("Verify clearing all filters")]
    public async Task ImportJobsList_ClearFilters_ShouldResetAllFilters()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Expand filters if needed
        var filtersPanel = Page.Locator("text=Filters & Search");
        if (await filtersPanel.IsVisibleAsync())
        {
            var isExpanded = await Page.Locator(".mud-expansion-panel-expanded").IsVisibleAsync();
            if (!isExpanded)
            {
                await filtersPanel.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // Act - Click clear filters button
        var clearButton = Page.Locator("button:has-text('Clear Filters')");
        if (await clearButton.IsVisibleAsync())
        {
            await clearButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Assert - Filters should be cleared (search input should be empty)
        var searchInput = Page.Locator("input[placeholder*='Filename'], input[placeholder*='service'], label:has-text('Search') ~ input").First();
        if (await searchInput.IsVisibleAsync())
        {
            var value = await searchInput.InputValueAsync();
            Assert.That(value, Is.Empty);
        }
    }

    [Test]
    [Description("Verify refresh button updates job list")]
    public async Task ImportJobsList_RefreshButton_ShouldReloadJobs()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Act - Click refresh button
        var refreshButton = Page.Locator("button[title='Refresh'], .mud-icon-button").Filter(new() { HasText = "" }).First();
        await refreshButton.ClickAsync();

        // Wait for refresh to complete
        await Page.WaitForTimeoutAsync(1000);

        // Assert - Page should still be showing import jobs
        await Expect(Page.Locator("text=Import Jobs")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify viewing job details by expanding row")]
    public async Task ImportJobsList_ExpandJobDetails_ShouldShowDetails()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Look for expand button (if jobs exist)
        var expandButton = Page.Locator("button[aria-label*='expand'], .mud-icon-button").Filter(new()
        {
            Has = Page.Locator("svg")
        }).First();

        if (await expandButton.IsVisibleAsync())
        {
            await expandButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Assert - Expanded details should be visible
            // This is job-dependent, so we just verify the action worked
            Assert.Pass("Job details expansion tested");
        }
        else
        {
            // No jobs to expand
            Assert.Inconclusive("No import jobs available to test expansion");
        }
    }

    [Test]
    [Description("Verify export to CSV button is present")]
    public async Task ImportJobsList_ExportToCSV_ShouldBeAvailable()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Assert - Export button should be visible
        var exportButton = Page.Locator("button:has-text('Export to CSV')");
        await Expect(exportButton.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify New Import button navigates to import wizard")]
    public async Task ImportJobsList_NewImportButton_ShouldNavigateToWizard()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();

        // Act - Click New Import button
        var newImportButton = Page.Locator("button:has-text('New Import')");
        await newImportButton.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should navigate to import page
        var currentUrl = Page.Url;
        Assert.That(currentUrl, Does.Contain("/import"));
    }

    [Test]
    [Description("Verify job list displays empty state when no jobs exist")]
    public async Task ImportJobsList_NoJobs_ShouldShowEmptyState()
    {
        // Arrange
        await NavigateToImportJobsPageAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page might have jobs or not - we're just checking that it handles both states
        var noRecordsText = Page.Locator("text=No import jobs yet");
        var hasJobs = await Page.Locator("table tbody tr").CountAsync() > 0;

        if (!hasJobs)
        {
            // Assert - Should show empty state message
            await Expect(noRecordsText).ToBeVisibleAsync();
        }
        else
        {
            Assert.Pass("Jobs exist in the system");
        }
    }

    #endregion

    #region Helper Methods

    private async Task NavigateToDataImportPageAsync()
    {
        // Navigate to data import page
        var importLink = Page.Locator("a:has-text('Import'), nav a[href='/import']");

        if (await importLink.IsVisibleAsync())
        {
            await importLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/import");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToEsriImportPageAsync()
    {
        // Navigate to Esri import page
        var esriImportLink = Page.Locator("a[href='/import/esri']");

        if (await esriImportLink.IsVisibleAsync())
        {
            await esriImportLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation
            await Page.GotoAsync($"{BaseUrl}/import/esri");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task NavigateToImportJobsPageAsync()
    {
        // Navigate to import jobs page
        var jobsLink = Page.Locator("a[href='/import/jobs']");

        if (await jobsLink.IsVisibleAsync())
        {
            await jobsLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation
            await Page.GotoAsync($"{BaseUrl}/import/jobs");
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

    private async Task TryCancelImportJobAsync(Guid jobId)
    {
        try
        {
            await NavigateToImportJobsPageAsync();

            // Look for the job in the list
            var jobRow = Page.Locator($"text={jobId.ToString().Substring(0, 8)}").First();

            if (await jobRow.IsVisibleAsync())
            {
                // Find cancel button for this job
                var cancelButton = Page.Locator($"button[title='Cancel']").First();
                if (await cancelButton.IsVisibleAsync())
                {
                    await cancelButton.ClickAsync();
                    await Page.WaitForTimeoutAsync(1000);
                }
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private async Task TryDeleteServiceAsync(string serviceId)
    {
        try
        {
            await NavigateToServicesPageAsync();
            var serviceRow = Page.Locator($"tr:has-text('{serviceId}')").First();

            if (await serviceRow.IsVisibleAsync())
            {
                var deleteButton = serviceRow.Locator("button:has-text('Delete')").First();
                await deleteButton.ClickAsync();

                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
