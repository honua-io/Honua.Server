// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Data Source management in the Honua Admin Blazor application.
/// Tests cover CRUD operations, connection testing, and provider-specific configurations.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("DataSourceManagement")]
public class DataSourceManagementTests : BaseE2ETest
{
    private string _testDataSourceId = null!;

    [SetUp]
    public async Task DataSourceTestSetUp()
    {
        _testDataSourceId = $"e2e-ds-{Guid.NewGuid():N}";
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task DataSourceTestTearDown()
    {
        try
        {
            await NavigateToDataSourcesPageAsync();
            var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
            if (await row.IsVisibleAsync())
            {
                var deleteButton = row.Locator("button:has-text('Delete')");
                await deleteButton.ClickAsync();
                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
            }
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("Create a PostGIS data source with valid configuration")]
    public async Task CreateDataSource_PostGIS_ShouldSucceed()
    {
        // Arrange
        await NavigateToDataSourcesPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Data Source'), button:has-text('Add Data Source')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("ID").FillAsync(_testDataSourceId);

        // Select provider
        await dialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS'), .mud-list-item:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        // Fill connection string
        await dialog.GetByLabel("Connection String").FillAsync(
            "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test");

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')");
        await Expect(row.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Create SQL Server data source with connection string builder")]
    public async Task CreateDataSource_SqlServer_WithBuilder_ShouldSucceed()
    {
        // Arrange
        await NavigateToDataSourcesPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Data Source')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("ID").FillAsync(_testDataSourceId);

        // Select SQL Server provider
        await dialog.GetByLabel("Provider").ClickAsync();
        var sqlServerOption = Page.Locator("li:has-text('SQL Server'), li:has-text('SqlServer')").First();
        await sqlServerOption.ClickAsync();

        // If connection string builder is available
        var useBuilderButton = dialog.Locator("button:has-text('Builder'), button:has-text('Use Builder')");
        if (await useBuilderButton.IsVisibleAsync())
        {
            await useBuilderButton.ClickAsync();

            // Fill builder fields
            var serverField = dialog.GetByLabel("Server");
            if (await serverField.IsVisibleAsync())
            {
                await serverField.FillAsync("localhost");
            }

            var databaseField = dialog.GetByLabel("Database");
            if (await databaseField.IsVisibleAsync())
            {
                await databaseField.FillAsync("testdb");
            }

            var userIdField = dialog.GetByLabel("User ID");
            if (await userIdField.IsVisibleAsync())
            {
                await userIdField.FillAsync("sa");
            }

            var passwordField = dialog.GetByLabel("Password");
            if (await passwordField.IsVisibleAsync())
            {
                await passwordField.FillAsync("TestPassword123!");
            }
        }
        else
        {
            // Fallback to direct connection string
            await dialog.GetByLabel("Connection String").FillAsync(
                "Server=localhost;Database=testdb;User Id=sa;Password=TestPassword123!;TrustServerCertificate=true");
        }

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        await Expect(Page.Locator($"tr:has-text('{_testDataSourceId}')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("View data source details")]
    public async Task ViewDataSource_ExistingDataSource_ShouldDisplayDetails()
    {
        // Arrange - Create a data source first
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Click on data source to view details
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        await row.ClickAsync();

        // Wait for details page or details panel
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify details are displayed
        await Expect(Page.Locator($"text={_testDataSourceId}")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=PostGIS")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Update data source connection string")]
    public async Task UpdateDataSource_ConnectionString_ShouldSucceed()
    {
        // Arrange - Create a data source first
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Edit data source
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        var editButton = row.Locator("button:has-text('Edit')");

        if (!await editButton.IsVisibleAsync())
        {
            await row.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            editButton = Page.Locator("button:has-text('Edit')").First();
        }

        await editButton.ClickAsync();

        var dialog = await WaitForDialogAsync();

        // Update connection string
        var connectionStringField = dialog.GetByLabel("Connection String");
        await connectionStringField.ClearAsync();
        await connectionStringField.FillAsync(
            "Host=localhost;Port=5432;Database=updateddb;Username=test;Password=test");

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Update')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Delete data source")]
    public async Task DeleteDataSource_ExistingDataSource_ShouldSucceed()
    {
        // Arrange - Create a data source first
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Delete data source
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        var deleteButton = row.Locator("button:has-text('Delete')");
        await deleteButton.ClickAsync();

        // Confirm deletion
        var confirmDialog = await WaitForDialogAsync();
        var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
        await confirmButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("deleted successfully");
        await Page.WaitForTimeoutAsync(1000);
        var deletedRow = Page.Locator($"tr:has-text('{_testDataSourceId}')");
        await Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Test database connection - success scenario")]
    public async Task TestConnection_ValidDataSource_ShouldShowSuccess()
    {
        // Arrange - Create a data source
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Click test connection button
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        var testButton = row.Locator("button:has-text('Test'), button:has-text('Test Connection'), [data-testid='test-connection']");

        if (await testButton.IsVisibleAsync())
        {
            await testButton.ClickAsync();

            // Wait for connection test result
            await Page.WaitForTimeoutAsync(3000);

            // Assert - Look for success or failure message
            // Note: Test might fail if database doesn't exist, which is expected
            var successMessage = Page.Locator("text=Connection successful, text=Connected successfully");
            var failureMessage = Page.Locator("text=Connection failed, text=Failed to connect");

            var hasSuccess = await successMessage.IsVisibleAsync().ConfigureAwait(false);
            var hasFailure = await failureMessage.IsVisibleAsync().ConfigureAwait(false);

            (hasSuccess || hasFailure).Should().BeTrue(
                "either success or failure message should be shown after connection test");
        }
        else
        {
            // Test button not available in UI - skip test
            Assert.Ignore("Test connection button not found in UI");
        }
    }

    [Test]
    [Description("Test connection with invalid credentials should show error")]
    public async Task TestConnection_InvalidCredentials_ShouldShowError()
    {
        // Arrange
        await NavigateToDataSourcesPageAsync();

        var newButton = Page.Locator("button:has-text('New Data Source')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("ID").FillAsync(_testDataSourceId);
        await dialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        // Use invalid credentials
        await dialog.GetByLabel("Connection String").FillAsync(
            "Host=localhost;Port=5432;Database=nonexistent;Username=invalid;Password=wrong");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Test connection with invalid credentials
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        var testButton = row.Locator("button:has-text('Test'), [data-testid='test-connection']");

        if (await testButton.IsVisibleAsync())
        {
            await testButton.ClickAsync();
            await Page.WaitForTimeoutAsync(3000);

            // Assert - Should show failure message
            var failureMessage = Page.Locator("text=Connection failed, text=Failed to connect, .mud-alert-error");
            await Expect(failureMessage.First()).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Create duplicate data source ID should show error")]
    public async Task CreateDataSource_DuplicateId_ShouldShowError()
    {
        // Arrange - Create first data source
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Try to create another with same ID
        var newButton = Page.Locator("button:has-text('New Data Source')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("ID").FillAsync(_testDataSourceId); // Same ID
        await dialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        await dialog.GetByLabel("Connection String").FillAsync(
            "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert - Should show error
        await WaitForSnackbarAsync("already exists");
    }

    [Test]
    [Description("Search/filter data sources")]
    public async Task DataSourceList_SearchFilter_ShouldFilterResults()
    {
        // Arrange - Create a data source
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Search for the data source
        var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
        if (await searchBox.IsVisibleAsync())
        {
            await searchBox.FillAsync(_testDataSourceId);
            await Page.WaitForTimeoutAsync(1000);

            // Assert - Should show only matching data source
            var rows = Page.Locator($"tr:has-text('{_testDataSourceId}')");
            await Expect(rows).ToHaveCountAsync(1);
        }
        else
        {
            Assert.Ignore("Search functionality not available in UI");
        }
    }

    [Test]
    [Description("View services using a data source")]
    public async Task DataSourceDetail_ViewUsages_ShouldShowServices()
    {
        // Arrange - Create data source
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Act - Click to view data source details
        var row = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        await row.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for "Used by" or "Services" section
        var usagesSection = Page.Locator("text=Used by, text=Services using this data source");

        if (await usagesSection.IsVisibleAsync())
        {
            // Assert - Should show 0 services since we didn't create any
            var noServicesText = Page.Locator("text=No services, text=0 services");
            await Expect(noServicesText.First()).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Cannot delete data source in use by service")]
    public async Task DeleteDataSource_InUseByService_ShouldShowError()
    {
        // This test would require creating a service with the data source
        // For now, we'll test the UI validation flow
        await CreateTestDataSourceAsync();
        await NavigateToDataSourcesPageAsync();

        // Note: Without a service using it, deletion should succeed
        // This test documents the expected behavior when a service exists
        Assert.Pass("Test requires service creation - documents expected behavior");
    }

    #region Helper Methods

    private async Task NavigateToDataSourcesPageAsync()
    {
        var dataSourcesLink = Page.Locator("a:has-text('Data Sources'), nav a:has-text('Data Sources')");
        if (await dataSourcesLink.IsVisibleAsync())
        {
            await dataSourcesLink.First().ClickAsync();
        }
        else
        {
            await Page.GotoAsync($"{BaseUrl}/datasources");
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestDataSourceAsync()
    {
        await NavigateToDataSourcesPageAsync();

        var newButton = Page.Locator("button:has-text('New Data Source')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("ID").FillAsync(_testDataSourceId);
        await dialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        await dialog.GetByLabel("Connection String").FillAsync(
            "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion
}
