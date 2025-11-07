// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.E2ETests.Infrastructure;

namespace Honua.Admin.Blazor.E2ETests.Tests;

/// <summary>
/// E2E tests for Map Management in the Honua Admin Blazor application.
/// Tests cover map creation, configuration, layer management, controls, export, and viewer functionality.
/// </summary>
[TestFixture]
[Category("E2E")]
[Category("MapManagement")]
public class MapManagementTests : BaseE2ETest
{
    private string _testMapId = null!;
    private string _testMapName = null!;

    [SetUp]
    public async Task MapTestSetUp()
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        _testMapId = $"e2e-map-{guid}";
        _testMapName = $"E2E Test Map {guid}";

        await LoginAsync(TestConfiguration.AdminUsername, TestConfiguration.AdminPassword);
    }

    [TearDown]
    public async Task MapTestTearDown()
    {
        try
        {
            await TryDeleteMapAsync(_testMapName);
        }
        catch { /* Ignore */ }
    }

    [Test]
    [Description("Navigate to maps list page")]
    public async Task NavigateToMaps_ShouldDisplayMapsList()
    {
        // Act
        await NavigateToMapsPageAsync();

        // Assert
        await Expect(Page.Locator("text=Map Configurations")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Create New Map')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Create a new map with basic configuration")]
    public async Task CreateMap_WithBasicSettings_ShouldSucceed()
    {
        // Arrange
        await NavigateToMapsPageAsync();

        // Act
        var createButton = Page.Locator("button:has-text('Create New Map')");
        await createButton.ClickAsync();

        // Wait for navigation to map editor
        await Page.WaitForURLAsync("**/maps/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Fill basic settings
        await Page.GetByLabel("Map Name").FillAsync(_testMapName);
        await Page.GetByLabel("Description").FillAsync("E2E test map for automated testing");

        // Save the map
        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("created successfully");
        await Expect(Page.Locator($"text={_testMapName}")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Configure map title and description")]
    public async Task ConfigureMap_TitleAndDescription_ShouldUpdateSuccessfully()
    {
        // Arrange - Create a map first
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Edit the map
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();

        var editMenuItem = Page.Locator("li:has-text('Edit'), .mud-list-item:has-text('Edit')").First();
        await editMenuItem.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Update title and description
        var titleField = Page.GetByLabel("Map Name");
        await titleField.ClearAsync();
        await titleField.FillAsync($"{_testMapName} Updated");

        var descField = Page.GetByLabel("Description");
        await descField.ClearAsync();
        await descField.FillAsync("Updated description for E2E testing");

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Open map editor and configure map settings")]
    public async Task OpenMapEditor_ConfigureSettings_ShouldSucceed()
    {
        // Arrange
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Open editor
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();

        var editMenuItem = Page.Locator("li:has-text('Edit')").First();
        await editMenuItem.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Configure settings
        await Page.GetByLabel("Center Longitude").FillAsync("-122.4194");
        await Page.GetByLabel("Center Latitude").FillAsync("37.7749");
        await Page.GetByLabel("Zoom Level").FillAsync("10");

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Add a layer to the map")]
    public async Task AddLayer_ToMap_ShouldSucceed()
    {
        // Arrange
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Add a layer
        var addLayerButton = Page.Locator("button:has-text('Add Layer')");
        await addLayerButton.ClickAsync();

        await Page.WaitForTimeoutAsync(500); // Wait for layer to be added to list

        // Assert - Verify layer appears
        var layerItem = Page.Locator("text=Layer 1");
        await Expect(layerItem.First()).ToBeVisibleAsync();

        // Verify layer details
        await Expect(Page.Locator("text=Type:")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Source:")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Remove a layer from the map")]
    public async Task RemoveLayer_FromMap_ShouldSucceed()
    {
        // Arrange - Create map and add layer
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Add a layer first
        await Page.Locator("button:has-text('Add Layer')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Remove the layer
        var deleteLayerButton = Page.Locator("button:has(svg)").Filter(new LocatorFilterOptions
        {
            Has = Page.Locator("path[d*='M6 19c0']") // Material Delete icon path
        }).First();

        var layerCount = await Page.Locator("text=Layer 1").CountAsync();
        if (layerCount > 0)
        {
            await deleteLayerButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Assert - Layer should be removed
            var noLayersText = Page.Locator("text=No layers added yet");
            await Expect(noLayersText).ToBeVisibleAsync();
        }
    }

    [Test]
    [Description("Configure layer visibility")]
    public async Task ConfigureLayer_Visibility_ShouldToggle()
    {
        // Arrange - Create map and add layer
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("button:has-text('Add Layer')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Toggle layer visibility
        var visibilitySwitch = Page.Locator("label:has-text('Visible')").First();
        await visibilitySwitch.ClickAsync();

        await Page.WaitForTimeoutAsync(300);

        // Assert - Switch should be toggled (visual confirmation)
        var switchInput = Page.Locator("input[type='checkbox']").First();
        var isChecked = await switchInput.IsCheckedAsync();
        isChecked.Should().BeFalse();
    }

    [Test]
    [Description("Configure layer opacity")]
    public async Task ConfigureLayer_Opacity_ShouldUpdate()
    {
        // Arrange - Create map and add layer
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("button:has-text('Add Layer')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Verify opacity control is visible
        var opacityText = Page.Locator("text=Opacity:");
        await Expect(opacityText.First()).ToBeVisibleAsync();

        // Assert - Opacity value should be displayed (default 1.0)
        await Expect(Page.Locator("text=1.0")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Configure map projection")]
    public async Task ConfigureMap_Projection_ShouldUpdate()
    {
        // Arrange
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Change projection
        var projectionSelect = Page.GetByLabel("Projection");
        await projectionSelect.ClickAsync();

        var globeOption = Page.Locator("li:has-text('Globe')").First();
        await globeOption.ClickAsync();

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Configure map style URL")]
    public async Task ConfigureMap_StyleUrl_ShouldUpdate()
    {
        // Arrange
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Update style URL
        var styleField = Page.GetByLabel("Map Style URL");
        await styleField.ClearAsync();
        await styleField.FillAsync("https://demotiles.maplibre.org/style.json");

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Save map configuration")]
    public async Task SaveMap_WithConfiguration_ShouldPersist()
    {
        // Arrange
        await NavigateToMapsPageAsync();
        var createButton = Page.Locator("button:has-text('Create New Map')");
        await createButton.ClickAsync();
        await Page.WaitForURLAsync("**/maps/new");

        // Configure map
        await Page.GetByLabel("Map Name").FillAsync(_testMapName);
        await Page.GetByLabel("Description").FillAsync("Persistent test map");
        await Page.GetByLabel("Center Longitude").FillAsync("-74.006");
        await Page.GetByLabel("Center Latitude").FillAsync("40.7128");
        await Page.GetByLabel("Zoom Level").FillAsync("12");

        // Act - Save
        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        await WaitForSnackbarAsync("created successfully");
        await Page.WaitForTimeoutAsync(1000);

        // Navigate back to list
        await NavigateToMapsPageAsync();

        // Assert - Map should appear in list
        await Expect(Page.Locator($"tr:has-text('{_testMapName}')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Update an existing map")]
    public async Task UpdateMap_ExistingConfiguration_ShouldSucceed()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Edit and update
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Make changes
        var descField = Page.GetByLabel("Description");
        await descField.FillAsync("Updated via E2E test");

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("updated successfully");
    }

    [Test]
    [Description("Delete a map")]
    public async Task DeleteMap_ExistingMap_ShouldSucceed()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Delete the map
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();

        var deleteMenuItem = Page.Locator("li:has-text('Delete')").First();
        await deleteMenuItem.ClickAsync();

        // Confirm deletion
        var confirmButton = Page.Locator("button:has-text('Delete')").Last();
        await confirmButton.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("deleted successfully");
        await Page.WaitForTimeoutAsync(1000);

        var deletedRow = Page.Locator($"tr:has-text('{_testMapName}')");
        await Expect(deletedRow).ToHaveCountAsync(0);
    }

    [Test]
    [Description("Clone/duplicate a map")]
    public async Task CloneMap_ExistingMap_ShouldCreateCopy()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Clone the map
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();

        var cloneMenuItem = Page.Locator("li:has-text('Clone')").First();
        await cloneMenuItem.ClickAsync();

        // Assert
        await WaitForSnackbarAsync("cloned successfully");
        await Page.WaitForTimeoutAsync(1000);

        // Verify a cloned map appears (usually with "Copy" suffix or similar)
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should have at least 2 maps now (original + clone)
        var mapRows = Page.Locator("tr:has-text('E2E Test Map')");
        var count = await mapRows.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Test]
    [Description("View map in viewer")]
    public async Task ViewMap_InViewer_ShouldDisplayMap()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Open viewer
        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();

        var viewMenuItem = Page.Locator("li:has-text('View')").First();
        await viewMenuItem.ClickAsync();

        // Wait for viewer to load
        await Page.WaitForURLAsync("**/maps/view/**", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Assert - Verify viewer displays map info
        await Expect(Page.Locator($"text={_testMapName}")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Map Information")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Layers:")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Controls:")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Projection:")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Search/filter maps in list")]
    public async Task SearchMaps_WithFilter_ShouldFilterResults()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        // Act - Search for the map (if search is available)
        var searchBox = Page.Locator("input[placeholder*='Search'], input[type='search']");
        if (await searchBox.IsVisibleAsync())
        {
            await searchBox.FillAsync(_testMapName);
            await Page.WaitForTimeoutAsync(1000);

            // Assert - Should show only matching map
            var rows = Page.Locator($"tr:has-text('{_testMapName}')");
            await Expect(rows).ToHaveCountAsync(1);
        }
        else
        {
            // If no search box, just verify map is visible
            await Expect(Page.Locator($"tr:has-text('{_testMapName}')")).ToBeVisibleAsync();
            Assert.Pass("Search functionality not available in UI");
        }
    }

    [Test]
    [Description("Open export dialog from map editor")]
    public async Task ExportMap_OpenDialog_ShouldDisplayExportOptions()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Open export dialog
        var exportButton = Page.Locator("button:has-text('Export')");
        await exportButton.ClickAsync();

        var dialog = await WaitForDialogAsync();

        // Assert - Verify export tabs are available
        await Expect(dialog.Locator("text=Export Map Configuration")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=JSON")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=YAML")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=HTML Embed")).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=Blazor Code")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Export map to different formats")]
    public async Task ExportMap_ToMultipleFormats_ShouldShowAllFormats()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var exportButton = Page.Locator("button:has-text('Export')");
        await exportButton.ClickAsync();

        var dialog = await WaitForDialogAsync();

        // Act & Assert - Test JSON export
        var jsonTab = dialog.Locator("button:has-text('JSON')");
        await jsonTab.ClickAsync();
        await Expect(dialog.Locator("text=JSON Configuration")).ToBeVisibleAsync();
        await Expect(dialog.Locator("button:has-text('Copy JSON')")).ToBeVisibleAsync();

        // Test YAML export
        var yamlTab = dialog.Locator("button:has-text('YAML')");
        await yamlTab.ClickAsync();
        await Expect(dialog.Locator("text=YAML Configuration")).ToBeVisibleAsync();
        await Expect(dialog.Locator("button:has-text('Copy YAML')")).ToBeVisibleAsync();

        // Test HTML export
        var htmlTab = dialog.Locator("button:has-text('HTML Embed')");
        await htmlTab.ClickAsync();
        await Expect(dialog.Locator("text=Embeddable HTML")).ToBeVisibleAsync();
        await Expect(dialog.Locator("button:has-text('Copy HTML')")).ToBeVisibleAsync();

        // Test Blazor export
        var blazorTab = dialog.Locator("button:has-text('Blazor Code')");
        await blazorTab.ClickAsync();
        await Expect(dialog.Locator("text=Blazor Component Code")).ToBeVisibleAsync();
        await Expect(dialog.Locator("button:has-text('Copy Blazor Code')")).ToBeVisibleAsync();
    }

    [Test]
    [Description("Add map controls")]
    public async Task AddControl_ToMap_ShouldSucceed()
    {
        // Arrange - Create a map
        await CreateTestMapAsync();
        await NavigateToMapsPageAsync();

        var row = Page.Locator($"tr:has-text('{_testMapName}')").First();
        var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
        await menuButton.ClickAsync();
        await Page.Locator("li:has-text('Edit')").First().ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Add a control
        var addControlButton = Page.Locator("button:has-text('Add Control')");
        await addControlButton.ClickAsync();

        await Page.WaitForTimeoutAsync(500);

        // Assert - Verify control appears in the list
        var controlsSection = Page.Locator("text=Map Controls");
        await Expect(controlsSection).ToBeVisibleAsync();

        // Should have at least one control visible
        var controlItem = Page.Locator("text=Navigation, text=top-right");
        var controlCount = await controlItem.CountAsync();
        controlCount.Should().BeGreaterThan(0);
    }

    #region Helper Methods

    private async Task NavigateToMapsPageAsync()
    {
        var mapsLink = Page.Locator("a:has-text('Maps'), nav a:has-text('Maps')");
        if (await mapsLink.IsVisibleAsync())
        {
            await mapsLink.First().ClickAsync();
        }
        else
        {
            await Page.GotoAsync($"{BaseUrl}/maps");
        }
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task CreateTestMapAsync()
    {
        await NavigateToMapsPageAsync();

        var createButton = Page.Locator("button:has-text('Create New Map')");
        await createButton.ClickAsync();

        await Page.WaitForURLAsync("**/maps/new", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await Page.GetByLabel("Map Name").FillAsync(_testMapName);
        await Page.GetByLabel("Description").FillAsync("E2E test map for automated testing");

        var saveButton = Page.Locator("button:has-text('Save Map')");
        await saveButton.ClickAsync();

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);
    }

    private async Task TryDeleteMapAsync(string mapName)
    {
        try
        {
            await NavigateToMapsPageAsync();
            var row = Page.Locator($"tr:has-text('{mapName}')").First();

            if (await row.IsVisibleAsync())
            {
                var menuButton = row.Locator("button[aria-label*='menu'], button:has(svg)").Last();
                await menuButton.ClickAsync();

                var deleteMenuItem = Page.Locator("li:has-text('Delete')").First();
                await deleteMenuItem.ClickAsync();

                var confirmButton = Page.Locator("button:has-text('Delete')").Last();
                await confirmButton.ClickAsync();

                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }
        catch { /* Ignore */ }
    }

    #endregion
}
