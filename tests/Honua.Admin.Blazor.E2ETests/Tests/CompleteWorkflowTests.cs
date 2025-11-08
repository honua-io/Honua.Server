// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// Comprehensive E2E workflow tests covering the complete user journey from
/// data source creation through folder organization, service setup, and layer configuration.
/// These tests verify the entire workflow that a typical admin user would perform.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("Workflow")]
[Category("FullWorkflow")]
public class CompleteWorkflowTests : BaseE2ETest
{
    private string _testDataSourceId = null!;
    private string _testFolderId = null!;
    private string _testServiceId = null!;
    private string _testLayerId = null!;

    [SetUp]
    public async Task WorkflowTestSetUp()
    {
        // Generate unique IDs for this test run
        var guid = Guid.NewGuid().ToString("N")[..8];
        _testDataSourceId = $"e2e-ds-{guid}";
        _testFolderId = $"e2e-folder-{guid}";
        _testServiceId = $"e2e-service-{guid}";
        _testLayerId = $"e2e-layer-{guid}";

        // Login before each test
        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task WorkflowTestTearDown()
    {
        // Cleanup: Delete resources in reverse order (layer → service → folder → datasource)
        try
        {
            // Delete layer
            await TryDeleteLayerAsync(_testLayerId);

            // Delete service
            await TryDeleteServiceAsync(_testServiceId);

            // Delete folder
            await TryDeleteFolderAsync(_testFolderId);

            // Delete data source
            await TryDeleteDataSourceAsync(_testDataSourceId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    [Description("Complete workflow: Create DataSource → Folder → Service → Layer")]
    public async Task CompleteWorkflow_DataSourceToLayer_ShouldSucceed()
    {
        // Step 1: Create Data Source
        await NavigateToDataSourcesPageAsync();

        var newDataSourceButton = Page.Locator("button:has-text('New Data Source'), button:has-text('Add Data Source')");
        await newDataSourceButton.First().ClickAsync();

        var dataSourceDialog = await WaitForDialogAsync();
        await dataSourceDialog.GetByLabel("ID").FillAsync(_testDataSourceId);
        await dataSourceDialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS'), .mud-list-item:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        // Fill connection string fields
        var connectionStringField = dataSourceDialog.GetByLabel("Connection String");
        await connectionStringField.FillAsync("Host=localhost;Port=5432;Database=testdb;Username=test;Password=test");

        var saveDataSourceButton = dataSourceDialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveDataSourceButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify data source appears in list
        var dataSourceRow = Page.Locator($"tr:has-text('{_testDataSourceId}'), .data-source-item:has-text('{_testDataSourceId}')");
        await Expect(dataSourceRow.First()).ToBeVisibleAsync();

        // Step 2: Create Folder
        await NavigateToFoldersPageAsync();

        var newFolderButton = Page.Locator("button:has-text('New Folder'), button:has-text('Add Folder'), button:has-text('Create Folder')");
        await newFolderButton.First().ClickAsync();

        var folderDialog = await WaitForDialogAsync();
        await folderDialog.GetByLabel("Folder ID").FillAsync(_testFolderId);
        await folderDialog.GetByLabel("Title").FillAsync("E2E Test Folder");

        var saveFolderButton = folderDialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveFolderButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify folder appears
        var folderNode = Page.Locator($"text={_testFolderId}, .folder:has-text('{_testFolderId}')");
        await Expect(folderNode.First()).ToBeVisibleAsync();

        // Step 3: Create Service
        await NavigateToServicesPageAsync();

        var newServiceButton = Page.Locator("button:has-text('New Service'), button:has-text('Create Service')");
        await newServiceButton.First().ClickAsync();

        var serviceDialog = await WaitForDialogAsync();
        await serviceDialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await serviceDialog.GetByLabel("Title").FillAsync("E2E Test WMS Service");

        // Select service type
        await serviceDialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        // Select data source
        var dataSourceDropdown = serviceDialog.Locator("label:has-text('Data Source')").Locator("..");
        if (await dataSourceDropdown.IsVisibleAsync())
        {
            await dataSourceDropdown.ClickAsync();
            var dataSourceOption = Page.Locator($"li:has-text('{_testDataSourceId}')").First();
            if (await dataSourceOption.IsVisibleAsync())
            {
                await dataSourceOption.ClickAsync();
            }
        }

        // Select folder
        var folderDropdown = serviceDialog.Locator("label:has-text('Folder')").Locator("..");
        if (await folderDropdown.IsVisibleAsync())
        {
            await folderDropdown.ClickAsync();
            var folderOption = Page.Locator($"li:has-text('{_testFolderId}')").First();
            if (await folderOption.IsVisibleAsync())
            {
                await folderOption.ClickAsync();
            }
        }

        var saveServiceButton = serviceDialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveServiceButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify service appears in list
        var serviceRow = Page.Locator($"tr:has-text('{_testServiceId}')");
        await Expect(serviceRow.First()).ToBeVisibleAsync();

        // Step 4: Create Layer
        await NavigateToLayersPageAsync();

        var newLayerButton = Page.Locator("button:has-text('New Layer'), button:has-text('Create Layer'), button:has-text('Add Layer')");
        await newLayerButton.First().ClickAsync();

        var layerDialog = await WaitForDialogAsync();
        await layerDialog.GetByLabel("Layer ID").FillAsync(_testLayerId);
        await layerDialog.GetByLabel("Title").FillAsync("E2E Test Layer");

        // Select service
        await layerDialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        // Select geometry type
        await layerDialog.GetByLabel("Geometry Type").ClickAsync();
        var pointOption = Page.Locator("li:has-text('Point')").First();
        await pointOption.ClickAsync();

        // Fill required fields
        await layerDialog.GetByLabel("ID Field").FillAsync("id");
        await layerDialog.GetByLabel("Geometry Field").FillAsync("geom");

        var displayFieldInput = layerDialog.GetByLabel("Display Field");
        if (await displayFieldInput.IsVisibleAsync())
        {
            await displayFieldInput.FillAsync("name");
        }

        var saveLayerButton = layerDialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveLayerButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify layer appears in list
        var layerRow = Page.Locator($"tr:has-text('{_testLayerId}')");
        await Expect(layerRow.First()).ToBeVisibleAsync();

        // Step 5: Verify the complete hierarchy
        // Navigate to service detail and verify it shows the layer
        await NavigateToServicesPageAsync();
        var serviceLink = Page.Locator($"a:has-text('{_testServiceId}')").First();
        await serviceLink.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify service details page shows the layer
        await Expect(Page.Locator($"text={_testLayerId}")).ToBeVisibleAsync();
        await Expect(Page.Locator($"text={_testDataSourceId}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Workflow: Create DataSource, test connection, then create Service")]
    public async Task Workflow_DataSourceWithConnectionTest_ThenService_ShouldSucceed()
    {
        // Step 1: Create Data Source
        await NavigateToDataSourcesPageAsync();

        var newDataSourceButton = Page.Locator("button:has-text('New Data Source')");
        await newDataSourceButton.First().ClickAsync();

        var dataSourceDialog = await WaitForDialogAsync();
        await dataSourceDialog.GetByLabel("ID").FillAsync(_testDataSourceId);
        await dataSourceDialog.GetByLabel("Provider").ClickAsync();
        var postgisOption = Page.Locator("li:has-text('PostGIS')").First();
        await postgisOption.ClickAsync();

        await dataSourceDialog.GetByLabel("Connection String").FillAsync(
            "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test");

        var saveButton = dataSourceDialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Step 2: Test connection (if UI has test button)
        var dataSourceRow = Page.Locator($"tr:has-text('{_testDataSourceId}')").First();
        var testConnectionButton = dataSourceRow.Locator("button:has-text('Test'), [data-testid='test-connection']");

        if (await testConnectionButton.IsVisibleAsync())
        {
            await testConnectionButton.ClickAsync();
            // Wait for test result - could be success or failure
            await Page.WaitForTimeoutAsync(2000);
        }

        // Step 3: Create Service with the data source
        await NavigateToServicesPageAsync();

        var newServiceButton = Page.Locator("button:has-text('New Service')");
        await newServiceButton.First().ClickAsync();

        var serviceDialog = await WaitForDialogAsync();
        await serviceDialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await serviceDialog.GetByLabel("Title").FillAsync("Service with Tested DataSource");
        await serviceDialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var saveServiceButton = serviceDialog.Locator("button:has-text('Save')");
        await saveServiceButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify
        await Expect(Page.Locator($"tr:has-text('{_testServiceId}')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Workflow: Create multiple layers in the same service")]
    public async Task Workflow_CreateMultipleLayersInService_ShouldSucceed()
    {
        // Setup: Create data source and service first
        await CreateTestDataSourceAsync();
        await CreateTestServiceAsync();

        // Create first layer
        await NavigateToLayersPageAsync();

        var newLayerButton = Page.Locator("button:has-text('New Layer'), button:has-text('Create Layer')");
        await newLayerButton.First().ClickAsync();

        var layerDialog = await WaitForDialogAsync();
        await layerDialog.GetByLabel("Layer ID").FillAsync($"{_testLayerId}-1");
        await layerDialog.GetByLabel("Title").FillAsync("First Test Layer");
        await layerDialog.GetByLabel("Service").ClickAsync();
        var serviceOption1 = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption1.ClickAsync();

        await layerDialog.GetByLabel("Geometry Type").ClickAsync();
        var pointOption1 = Page.Locator("li:has-text('Point')").First();
        await pointOption1.ClickAsync();

        await layerDialog.GetByLabel("ID Field").FillAsync("id");
        await layerDialog.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton1 = layerDialog.Locator("button:has-text('Save')");
        await saveButton1.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Create second layer
        await newLayerButton.First().ClickAsync();

        var layerDialog2 = await WaitForDialogAsync();
        await layerDialog2.GetByLabel("Layer ID").FillAsync($"{_testLayerId}-2");
        await layerDialog2.GetByLabel("Title").FillAsync("Second Test Layer");
        await layerDialog2.GetByLabel("Service").ClickAsync();
        var serviceOption2 = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption2.ClickAsync();

        await layerDialog2.GetByLabel("Geometry Type").ClickAsync();
        var polygonOption = Page.Locator("li:has-text('Polygon')").First();
        await polygonOption.ClickAsync();

        await layerDialog2.GetByLabel("ID Field").FillAsync("id");
        await layerDialog2.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton2 = layerDialog2.Locator("button:has-text('Save')");
        await saveButton2.ClickAsync();

        await WaitForSnackbarAsync("created successfully");

        // Verify both layers exist
        await Expect(Page.Locator($"tr:has-text('{_testLayerId}-1')")).ToBeVisibleAsync();
        await Expect(Page.Locator($"tr:has-text('{_testLayerId}-2')")).ToBeVisibleAsync();

        // Cleanup both layers
        await TryDeleteLayerAsync($"{_testLayerId}-1");
        await TryDeleteLayerAsync($"{_testLayerId}-2");
    }

    [Test]
    [Description("Workflow: Move service between folders")]
    public async Task Workflow_MoveServiceBetweenFolders_ShouldSucceed()
    {
        // Setup: Create two folders and a service
        await CreateTestFolderAsync(_testFolderId);
        var secondFolderId = $"{_testFolderId}-2";
        await CreateTestFolderAsync(secondFolderId);
        await CreateTestServiceAsync();

        // Navigate to service list
        await NavigateToServicesPageAsync();

        // Find and edit the service to move it to second folder
        var serviceRow = Page.Locator($"tr:has-text('{_testServiceId}')").First();
        var editButton = serviceRow.Locator("button:has-text('Edit')");

        if (!await editButton.IsVisibleAsync())
        {
            await serviceRow.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            editButton = Page.Locator("button:has-text('Edit')").First();
        }

        await editButton.ClickAsync();

        var editDialog = await WaitForDialogAsync();

        // Change folder
        var folderDropdown = editDialog.Locator("label:has-text('Folder')").Locator("..");
        if (await folderDropdown.IsVisibleAsync())
        {
            await folderDropdown.ClickAsync();
            var newFolderOption = Page.Locator($"li:has-text('{secondFolderId}')").First();
            await newFolderOption.ClickAsync();
        }

        var saveButton = editDialog.Locator("button:has-text('Save'), button:has-text('Update')");
        await saveButton.ClickAsync();

        await WaitForSnackbarAsync("updated successfully");

        // Cleanup second folder
        await TryDeleteFolderAsync(secondFolderId);
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

    private async Task NavigateToLayersPageAsync()
    {
        var layersLink = Page.Locator("a:has-text('Layers'), nav a:has-text('Layers')");
        if (await layersLink.IsVisibleAsync())
        {
            await layersLink.First().ClickAsync();
        }
        else
        {
            await Page.GotoAsync($"{BaseUrl}/layers");
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

    private async Task CreateTestFolderAsync(string folderId)
    {
        await NavigateToFoldersPageAsync();

        var newButton = Page.Locator("button:has-text('New Folder'), button:has-text('Create Folder')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Folder ID").FillAsync(folderId);
        await dialog.GetByLabel("Title").FillAsync($"Test Folder {folderId}");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestServiceAsync()
    {
        await NavigateToServicesPageAsync();

        var newButton = Page.Locator("button:has-text('New Service')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Service");
        await dialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task TryDeleteDataSourceAsync(string dataSourceId)
    {
        try
        {
            await NavigateToDataSourcesPageAsync();
            var row = Page.Locator($"tr:has-text('{dataSourceId}')").First();
            if (await row.IsVisibleAsync())
            {
                var deleteButton = row.Locator("button:has-text('Delete')").First();
                await deleteButton.ClickAsync();
                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch { /* Ignore */ }
    }

    private async Task TryDeleteFolderAsync(string folderId)
    {
        try
        {
            await NavigateToFoldersPageAsync();
            var folder = Page.Locator($"text={folderId}").First();
            if (await folder.IsVisibleAsync())
            {
                await folder.ClickAsync();
                var deleteButton = Page.Locator("button:has-text('Delete')").First();
                await deleteButton.ClickAsync();
                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch { /* Ignore */ }
    }

    private async Task TryDeleteServiceAsync(string serviceId)
    {
        try
        {
            await NavigateToServicesPageAsync();
            var row = Page.Locator($"tr:has-text('{serviceId}')").First();
            if (await row.IsVisibleAsync())
            {
                var deleteButton = row.Locator("button:has-text('Delete')").First();
                await deleteButton.ClickAsync();
                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch { /* Ignore */ }
    }

    private async Task TryDeleteLayerAsync(string layerId)
    {
        try
        {
            await NavigateToLayersPageAsync();
            var row = Page.Locator($"tr:has-text('{layerId}')").First();
            if (await row.IsVisibleAsync())
            {
                var deleteButton = row.Locator("button:has-text('Delete')").First();
                await deleteButton.ClickAsync();
                var confirmDialog = await WaitForDialogAsync();
                var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
                await confirmButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch { /* Ignore */ }
    }

    #endregion
}
