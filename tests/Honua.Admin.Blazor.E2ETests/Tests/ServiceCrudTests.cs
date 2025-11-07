// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Service CRUD operations in the Honua Admin Blazor application.
/// These tests verify the complete workflow for creating, reading, updating, and deleting services.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("ServiceManagement")]
public class ServiceCrudTests : BaseE2ETest
{
    private string _testServiceId = null!;

    [SetUp]
    public async Task ServiceTestSetUp()
    {
        // Generate unique service ID for this test run
        _testServiceId = $"e2e-test-service-{Guid.NewGuid():N}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task ServiceTestTearDown()
    {
        // Cleanup: Delete test service if it exists
        try
        {
            // Navigate to services page
            await NavigateToServicesPageAsync();

            // Search for the test service
            var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
            if (await searchBox.IsVisibleAsync())
            {
                await searchBox.FillAsync(_testServiceId);
                await Page.WaitForTimeoutAsync(1000); // Wait for search to filter

                // Try to delete if found
                var deleteButton = Page.Locator($"button:has-text('Delete'), [data-testid='delete-{_testServiceId}']").First();
                if (await deleteButton.IsVisibleAsync())
                {
                    await deleteButton.ClickAsync();

                    // Confirm deletion in dialog
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

    [Test]
    [Description("Verify that users can create a new service successfully")]
    public async Task CreateService_WithValidData_ShouldSucceed()
    {
        // Arrange
        await NavigateToServicesPageAsync();

        // Act - Click "New Service" or "Create Service" button
        var newServiceButton = Page.Locator("button:has-text('New Service'), button:has-text('Create Service'), button:has-text('Add Service')");
        await newServiceButton.First().ClickAsync();

        // Wait for create service dialog or form
        var dialog = await WaitForDialogAsync();

        // Fill in service details
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test WMS Service");
        await dialog.GetByLabel("Service Type").ClickAsync();

        // Select WMS from dropdown
        var wmsOption = Page.Locator("li:has-text('WMS'), .mud-list-item:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        // Add description
        var descriptionField = dialog.GetByLabel("Description");
        if (await descriptionField.IsVisibleAsync())
        {
            await descriptionField.FillAsync("This is a test service created by E2E tests");
        }

        // Submit form
        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("Service created successfully");

        // Assert - Verify service appears in the list
        var serviceRow = Page.Locator($"tr:has-text('{_testServiceId}'), .service-item:has-text('{_testServiceId}')");
        await Expect(serviceRow.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can view service details")]
    public async Task ViewService_ExistingService_ShouldDisplayDetails()
    {
        // Arrange - Create a service first
        await CreateTestServiceAsync();
        await NavigateToServicesPageAsync();

        // Act - Click on the service to view details
        var serviceLink = Page.Locator($"a:has-text('{_testServiceId}'), tr:has-text('{_testServiceId}')").First();
        await serviceLink.ClickAsync();

        // Wait for service details page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify service details are displayed
        await Expect(Page.Locator($"text={_testServiceId}")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=E2E Test WMS Service")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=WMS")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can update an existing service")]
    public async Task UpdateService_ExistingService_ShouldSucceed()
    {
        // Arrange - Create a service first
        await CreateTestServiceAsync();
        await NavigateToServicesPageAsync();

        // Act - Click edit button
        var serviceRow = Page.Locator($"tr:has-text('{_testServiceId}')").First();
        var editButton = serviceRow.Locator("button:has-text('Edit'), [data-testid='edit']");

        if (!await editButton.IsVisibleAsync())
        {
            // Click on service row first to open details, then edit
            await serviceRow.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            editButton = Page.Locator("button:has-text('Edit')").First();
        }

        await editButton.ClickAsync();

        // Wait for edit dialog/form
        var dialog = await WaitForDialogAsync();

        // Update service title
        var titleField = dialog.GetByLabel("Title");
        await titleField.FillAsync("Updated E2E Test Service");

        // Update description
        var descriptionField = dialog.GetByLabel("Description");
        if (await descriptionField.IsVisibleAsync())
        {
            await descriptionField.FillAsync("Updated description for E2E testing");
        }

        // Save changes
        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Update')");
        await saveButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("Service updated successfully");

        // Assert - Verify updated title appears
        await Expect(Page.Locator("text=Updated E2E Test Service")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Verify that users can delete a service")]
    public async Task DeleteService_ExistingService_ShouldSucceed()
    {
        // Arrange - Create a service first
        await CreateTestServiceAsync();
        await NavigateToServicesPageAsync();

        // Act - Click delete button
        var serviceRow = Page.Locator($"tr:has-text('{_testServiceId}')").First();
        var deleteButton = serviceRow.Locator("button:has-text('Delete'), [data-testid='delete']");

        if (!await deleteButton.IsVisibleAsync())
        {
            // Click on service row first to open details, then delete
            await serviceRow.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            deleteButton = Page.Locator("button:has-text('Delete')").First();
        }

        await deleteButton.ClickAsync();

        // Confirm deletion in dialog
        var confirmDialog = await WaitForDialogAsync();
        var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
        await confirmButton.ClickAsync();

        // Wait for success notification
        await WaitForSnackbarAsync("Service deleted successfully");

        // Assert - Verify service is removed from list
        await Page.WaitForTimeoutAsync(1000); // Give time for UI to update
        var deletedServiceRow = Page.Locator($"tr:has-text('{_testServiceId}')");
        await Expect(deletedServiceRow).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Verify that creating a service with duplicate ID shows error")]
    public async Task CreateService_WithDuplicateId_ShouldShowError()
    {
        // Arrange - Create a service first
        await CreateTestServiceAsync();
        await NavigateToServicesPageAsync();

        // Act - Try to create another service with same ID
        var newServiceButton = Page.Locator("button:has-text('New Service'), button:has-text('Create Service')");
        await newServiceButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId); // Same ID
        await dialog.GetByLabel("Title").FillAsync("Duplicate Service");

        await dialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Assert - Verify error message appears
        await WaitForSnackbarAsync("already exists");
    }

    [Test]
    [Description("Verify that service list can be searched/filtered")]
    public async Task ServiceList_WithSearch_ShouldFilterResults()
    {
        // Arrange - Create a service
        await CreateTestServiceAsync();
        await NavigateToServicesPageAsync();

        // Act - Search for the service
        var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
        await searchBox.FillAsync(_testServiceId);
        await Page.WaitForTimeoutAsync(1000); // Wait for search to filter

        // Assert - Only matching service should be visible
        var serviceRows = Page.Locator($"tr:has-text('{_testServiceId}')");
        await Expect(serviceRows).ToHaveCountAsync(1);
    }

    #region Helper Methods

    private async Task NavigateToServicesPageAsync()
    {
        // Navigate to services page via menu or direct URL
        var servicesLink = Page.Locator("a:has-text('Services'), nav a:has-text('Services')");

        if (await servicesLink.IsVisibleAsync())
        {
            await servicesLink.First().ClickAsync();
        }
        else
        {
            // Direct navigation if menu item not found
            await Page.GotoAsync($"{BaseUrl}/services");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestServiceAsync()
    {
        await NavigateToServicesPageAsync();

        var newServiceButton = Page.Locator("button:has-text('New Service'), button:has-text('Create Service')");
        await newServiceButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test WMS Service");

        await dialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var descriptionField = dialog.GetByLabel("Description");
        if (await descriptionField.IsVisibleAsync())
        {
            await descriptionField.FillAsync("This is a test service created by E2E tests");
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion
}
