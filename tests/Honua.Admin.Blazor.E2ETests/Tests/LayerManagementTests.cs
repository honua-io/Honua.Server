// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Layer management in the Honua Admin Blazor application.
/// Tests cover layer creation, configuration, geometry types, CRS setup, and field mapping.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("LayerManagement")]
public class LayerManagementTests : BaseE2ETest
{
    private string _testServiceId = null!;
    private string _testLayerId = null!;

    [SetUp]
    public async Task LayerTestSetUp()
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        _testServiceId = $"e2e-svc-{guid}";
        _testLayerId = $"e2e-layer-{guid}";

        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);

        // Create a service for the layers
        await CreateTestServiceAsync();
    }

    [TearDown]
    public async Task LayerTestTearDown()
    {
        try
        {
            // Delete layers
            await TryDeleteLayerAsync(_testLayerId);

            // Delete service
            await TryDeleteServiceAsync(_testServiceId);
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("Create a layer with Point geometry type")]
    public async Task CreateLayer_PointGeometry_ShouldSucceed()
    {
        // Arrange
        await NavigateToLayersPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Layer'), button:has-text('Create Layer'), button:has-text('Add Layer')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Layer ID").FillAsync(_testLayerId);
        await dialog.GetByLabel("Title").FillAsync("E2E Point Layer");

        // Select service
        await dialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        // Select Point geometry type
        await dialog.GetByLabel("Geometry Type").ClickAsync();
        var pointOption = Page.Locator("li:has-text('Point'), li:has-text('POINT')").First();
        await pointOption.ClickAsync();

        // Fill required fields
        await dialog.GetByLabel("ID Field").FillAsync("id");
        await dialog.GetByLabel("Geometry Field").FillAsync("geom");

        var displayField = dialog.GetByLabel("Display Field");
        if (await displayField.IsVisibleAsync())
        {
            await displayField.FillAsync("name");
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Create')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        var row = Page.Locator($"tr:has-text('{_testLayerId}')");
        await Expect(row.First()).ToBeVisibleAsync();
    }

    [Test]
    [Description("Create a layer with Polygon geometry type")]
    public async Task CreateLayer_PolygonGeometry_ShouldSucceed()
    {
        // Arrange
        await NavigateToLayersPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Layer')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Layer ID").FillAsync(_testLayerId);
        await dialog.GetByLabel("Title").FillAsync("E2E Polygon Layer");

        await dialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        // Select Polygon geometry type
        await dialog.GetByLabel("Geometry Type").ClickAsync();
        var polygonOption = Page.Locator("li:has-text('Polygon'), li:has-text('POLYGON')").First();
        await polygonOption.ClickAsync();

        await dialog.GetByLabel("ID Field").FillAsync("id");
        await dialog.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        await Expect(Page.Locator($"tr:has-text('{_testLayerId}')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Create a layer with LineString geometry type")]
    public async Task CreateLayer_LineStringGeometry_ShouldSucceed()
    {
        // Arrange
        await NavigateToLayersPageAsync();

        // Act
        var newButton = Page.Locator("button:has-text('New Layer')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Layer ID").FillAsync(_testLayerId);
        await dialog.GetByLabel("Title").FillAsync("E2E LineString Layer");

        await dialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        // Select LineString geometry type
        await dialog.GetByLabel("Geometry Type").ClickAsync();
        var lineOption = Page.Locator("li:has-text('LineString'), li:has-text('LINESTRING'), li:has-text('Line')").First();
        await lineOption.ClickAsync();

        await dialog.GetByLabel("ID Field").FillAsync("id");
        await dialog.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        await Expect(Page.Locator($"tr:has-text('{_testLayerId}')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("View layer details")]
    public async Task ViewLayer_ExistingLayer_ShouldDisplayDetails()
    {
        // Arrange - Create layer first
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Click on layer to view details
        var row = Page.Locator($"tr:has-text('{_testLayerId}')").First();
        await row.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Verify details are displayed
        await Expect(Page.Locator($"text={_testLayerId}")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Point, text=POINT")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Update layer title and description")]
    public async Task UpdateLayer_TitleAndDescription_ShouldSucceed()
    {
        // Arrange - Create layer first
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Edit layer
        var row = Page.Locator($"tr:has-text('{_testLayerId}')").First();
        var editButton = row.Locator("button:has-text('Edit')");

        if (!await editButton.IsVisibleAsync())
        {
            await row.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            editButton = Page.Locator("button:has-text('Edit')").First();
        }

        await editButton.ClickAsync();

        var dialog = await WaitForDialogAsync();

        // Update title
        var titleField = dialog.GetByLabel("Title");
        await titleField.ClearAsync();
        await titleField.FillAsync("Updated Layer Title");

        // Update description if available
        var descriptionField = dialog.GetByLabel("Description");
        if (await descriptionField.IsVisibleAsync())
        {
            await descriptionField.FillAsync("Updated layer description for E2E testing");
        }

        var saveButton = dialog.Locator("button:has-text('Save'), button:has-text('Update')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Configure layer CRS (Coordinate Reference System)")]
    public async Task ConfigureLayer_MultipleCRS_ShouldSucceed()
    {
        // Arrange - Create layer first
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Edit layer to add CRS
        var row = Page.Locator($"tr:has-text('{_testLayerId}')").First();
        var editButton = row.Locator("button:has-text('Edit')");

        if (!await editButton.IsVisibleAsync())
        {
            await row.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            editButton = Page.Locator("button:has-text('Edit')").First();
        }

        await editButton.ClickAsync();

        var dialog = await WaitForDialogAsync();

        // Look for CRS configuration section
        var crsSection = dialog.Locator("label:has-text('CRS'), label:has-text('Coordinate Reference System')");
        if (await crsSection.IsVisibleAsync())
        {
            // Add EPSG:3857 (Web Mercator)
            var addCrsButton = dialog.Locator("button:has-text('Add CRS')");
            if (await addCrsButton.IsVisibleAsync())
            {
                await addCrsButton.ClickAsync();
                var crsInput = dialog.Locator("input[placeholder*='EPSG']").Last();
                await crsInput.FillAsync("EPSG:3857");
            }
        }

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - CRS update should succeed (or skip if not available)
        if (await crsSection.IsVisibleAsync())
        {
            await WaitForSnackbarAsync("updated successfully");
        }
        else
        {
            Assert.Ignore("CRS configuration not available in UI");
        }
    }

    [Test]
    [Description("Delete layer")]
    public async Task DeleteLayer_ExistingLayer_ShouldSucceed()
    {
        // Arrange - Create layer first
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Delete layer
        var row = Page.Locator($"tr:has-text('{_testLayerId}')").First();
        var deleteButton = row.Locator("button:has-text('Delete')");
        await deleteButton.ClickAsync();

        // Confirm deletion
        var confirmDialog = await WaitForDialogAsync();
        var confirmButton = confirmDialog.Locator("button:has-text('Confirm'), button:has-text('Delete')");
        await confirmButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("deleted successfully");
        await Page.WaitForTimeoutAsync(1000);
        var deletedRow = Page.Locator($"tr:has-text('{_testLayerId}')");
        await Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Create layer with duplicate ID should show error")]
    public async Task CreateLayer_DuplicateId_ShouldShowError()
    {
        // Arrange - Create first layer
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Try to create another with same ID
        var newButton = Page.Locator("button:has-text('New Layer')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Layer ID").FillAsync(_testLayerId); // Same ID
        await dialog.GetByLabel("Title").FillAsync("Duplicate Layer");

        await dialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        await dialog.GetByLabel("Geometry Type").ClickAsync();
        var pointOption = Page.Locator("li:has-text('Point')").First();
        await pointOption.ClickAsync();

        await dialog.GetByLabel("ID Field").FillAsync("id");
        await dialog.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        // Assert - Should show error
        await WaitForSnackbarAsync("already exists");
    }

    [Test]
    [Description("Filter layers by service")]
    public async Task LayerList_FilterByService_ShouldShowOnlyMatchingLayers()
    {
        // Arrange - Create layer
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Apply service filter if available
        var filterDropdown = Page.Locator("label:has-text('Filter by Service'), select[name='service-filter']");
        if (await filterDropdown.IsVisibleAsync())
        {
            await filterDropdown.ClickAsync();
            var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
            await serviceOption.ClickAsync();

            await Page.WaitForTimeoutAsync(1000);

            // Assert - Should show layer from selected service
            await Expect(Page.Locator($"tr:has-text('{_testLayerId}')")).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("Service filter not available in UI");
        }
    }

    [Test]
    [Description("Search layers")]
    public async Task LayerList_Search_ShouldFilterResults()
    {
        // Arrange - Create layer
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Search for layer
        var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
        if (await searchBox.IsVisibleAsync())
        {
            await searchBox.FillAsync(_testLayerId);
            await Page.WaitForTimeoutAsync(1000);

            // Assert - Should show matching layer
            await Expect(Page.Locator($"tr:has-text('{_testLayerId}')")).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("Search functionality not available in UI");
        }
    }

    [Test]
    [Description("View layer metadata and properties")]
    public async Task LayerDetail_ViewMetadata_ShouldDisplayProperties()
    {
        // Arrange - Create layer
        await CreateTestLayerAsync();
        await NavigateToLayersPageAsync();

        // Act - Click on layer to view details
        var row = Page.Locator($"tr:has-text('{_testLayerId}')").First();
        await row.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should display key properties
        await Expect(Page.Locator("text=ID Field")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Geometry Field")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=id")).ToBeVisibleAsync(); // ID field value
        await Expect(Page.Locator("text=geom")).ToBeVisibleAsync(); // Geometry field value
    }

    #region Helper Methods

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

    private async Task CreateTestServiceAsync()
    {
        await NavigateToServicesPageAsync();

        var newButton = Page.Locator("button:has-text('New Service')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Service ID").FillAsync(_testServiceId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Service for Layers");
        await dialog.GetByLabel("Service Type").ClickAsync();
        var wmsOption = Page.Locator("li:has-text('WMS')").First();
        await wmsOption.ClickAsync();

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestLayerAsync()
    {
        await NavigateToLayersPageAsync();

        var newButton = Page.Locator("button:has-text('New Layer')");
        await newButton.First().ClickAsync();

        var dialog = await WaitForDialogAsync();
        await dialog.GetByLabel("Layer ID").FillAsync(_testLayerId);
        await dialog.GetByLabel("Title").FillAsync("E2E Test Layer");

        await dialog.GetByLabel("Service").ClickAsync();
        var serviceOption = Page.Locator($"li:has-text('{_testServiceId}')").First();
        await serviceOption.ClickAsync();

        await dialog.GetByLabel("Geometry Type").ClickAsync();
        var pointOption = Page.Locator("li:has-text('Point')").First();
        await pointOption.ClickAsync();

        await dialog.GetByLabel("ID Field").FillAsync("id");
        await dialog.GetByLabel("Geometry Field").FillAsync("geom");

        var saveButton = dialog.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task TryDeleteLayerAsync(string layerId)
    {
        try
        {
            await NavigateToLayersPageAsync();
            var row = Page.Locator($"tr:has-text('{layerId}')").First();
            if (await row.IsVisibleAsync())
            {
                var deleteButton = row.Locator("button:has-text('Delete')");
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
                var deleteButton = row.Locator("button:has-text('Delete')");
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
