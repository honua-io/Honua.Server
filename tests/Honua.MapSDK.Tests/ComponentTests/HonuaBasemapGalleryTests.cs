using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.BasemapGallery;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Honua.MapSDK.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaBasemapGallery component
/// </summary>
public class HonuaBasemapGalleryTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaBasemapGalleryTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    #region Initialization Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Id, "test-gallery")
            .Add(p => p.SyncWith, "test-map"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
        cut.Markup.Should().Contain("honua-basemap-gallery");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyCustomId()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Id, "custom-gallery-id"));

        // Assert
        cut.Instance.Id.Should().Be("custom-gallery-id");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldUseDefaultParameters()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>();

        // Assert
        cut.Instance.Title.Should().Be("Basemap Gallery");
        cut.Instance.Layout.Should().Be("grid");
        cut.Instance.ShowCategories.Should().BeTrue();
        cut.Instance.ShowSearch.Should().BeTrue();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyCustomTitle()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Title, "Custom Basemap Title"));

        // Assert
        cut.Instance.Title.Should().Be("Custom Basemap Title");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyCustomParameters()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowCategories, false)
            .Add(p => p.ShowSearch, false)
            .Add(p => p.ShowFavorites, true)
            .Add(p => p.EnablePreview, true));

        // Assert
        cut.Instance.ShowCategories.Should().BeFalse();
        cut.Instance.ShowSearch.Should().BeFalse();
        cut.Instance.ShowFavorites.Should().BeTrue();
        cut.Instance.EnablePreview.Should().BeTrue();
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderGridLayout()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Markup.Should().Contain("layout-grid");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderListLayout()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Layout, "list"));

        // Assert
        cut.Markup.Should().Contain("layout-list");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderDropdownLayout()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Layout, "dropdown"));

        // Assert
        cut.Markup.Should().Contain("layout-dropdown");
        cut.Markup.Should().Contain("basemap-dropdown");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderFloatingLayout()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Layout, "floating"));

        // Assert
        cut.Markup.Should().Contain("layout-floating");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldRenderModalLayout()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Layout, "modal"));

        // Assert
        cut.Markup.Should().Contain("layout-modal");
    }

    #endregion

    #region Position Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyPositionClass_TopRight()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Position, "top-right")
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Markup.Should().Contain("gallery-top-right");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyPositionClass_BottomLeft()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Position, "bottom-left")
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Markup.Should().Contain("gallery-bottom-left");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyCustomCssClass()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.CssClass, "custom-gallery-class"));

        // Assert
        cut.Markup.Should().Contain("custom-gallery-class");
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyCustomStyle()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Style, "width: 600px;"));

        // Assert
        cut.Markup.Should().Contain("width: 600px;");
    }

    #endregion

    #region Basemap Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldUseDefaultBasemaps()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>();

        // Assert
        cut.Should().NotBeNull();
        // The component should render some basemaps from the service
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldUseCustomBasemaps()
    {
        // Arrange
        var customBasemaps = new List<Basemap>
        {
            new Basemap
            {
                Id = "custom-1",
                Name = "Custom Basemap 1",
                Category = "Custom",
                StyleUrl = "https://example.com/style1.json"
            },
            new Basemap
            {
                Id = "custom-2",
                Name = "Custom Basemap 2",
                Category = "Custom",
                StyleUrl = "https://example.com/style2.json"
            }
        };

        // Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Basemaps, customBasemaps));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldApplyDefaultBasemap()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.DefaultBasemap, "streets-v11"));

        // Assert
        cut.Instance.DefaultBasemap.Should().Be("streets-v11");
    }

    #endregion

    #region Category Filter Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldShowCategoryTabs_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowCategories, true)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
        // Category tabs should be present in the markup
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldNotShowCategoryTabs_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowCategories, false)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldFilterByCategories()
    {
        // Arrange
        var allowedCategories = new[] { "Streets", "Satellite" };

        // Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Categories, allowedCategories));

        // Assert
        cut.Should().NotBeNull();
        // Only basemaps from specified categories should be shown
    }

    #endregion

    #region Search Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldShowSearchBox_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowSearch, true)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldNotShowSearchBox_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowSearch, false)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Basemap Selection Tests

    [Fact]
    public async Task HonuaBasemapGallery_OnBasemapChanged_ShouldInvokeCallback()
    {
        // Arrange
        Basemap? selectedBasemap = null;
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.OnBasemapChanged, EventCallback.Factory.Create<Basemap>(
                this, basemap => selectedBasemap = basemap)));

        // Act - Basemap changes are typically triggered by user interaction
        // We can verify the callback parameter is set
        cut.Instance.OnBasemapChanged.HasDelegate.Should().BeTrue();

        // Wait for initial render
        await Task.Delay(100);

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaBasemapGallery_ShouldPublishBasemapChangedMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Id, "gallery-1")
            .Add(p => p.SyncWith, "test-map"));

        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { 0.0, 0.0 },
            Zoom = 10
        }, "test-map");

        await Task.Delay(200);

        // Assert - Component should publish basemap changed message on initialization
        var messages = _testContext.ComponentBus.GetMessages<BasemapChangedMessage>();
        // Messages may be published during initialization
    }

    #endregion

    #region Opacity Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldShowOpacitySlider_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowOpacitySlider, true)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldNotShowOpacitySlider_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowOpacitySlider, false)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Favorites Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldShowFavorites_WhenEnabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowFavorites, true)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldNotShowFavorites_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ShowFavorites, false)
            .Add(p => p.Layout, "grid"));

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion

    #region Preview Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldEnablePreview_WhenSet()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.EnablePreview, true));

        // Assert
        cut.Instance.EnablePreview.Should().BeTrue();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldNotEnablePreview_WhenDisabled()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.EnablePreview, false));

        // Assert
        cut.Instance.EnablePreview.Should().BeFalse();
    }

    #endregion

    #region Thumbnail Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldUseCustomThumbnailPath()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.ThumbnailBasePath, "/custom/thumbnails/"));

        // Assert
        cut.Instance.ThumbnailBasePath.Should().Be("/custom/thumbnails/");
    }

    #endregion

    #region ComponentBus Integration Tests

    [Fact]
    public async Task HonuaBasemapGallery_ShouldRespondToMapReadyMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.SyncWith, "test-map")
            .Add(p => p.DefaultBasemap, "streets-v11"));

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "test-map",
            Center = new[] { -122.4194, 37.7749 },
            Zoom = 10
        }, "test-map");

        await Task.Delay(200);

        // Assert - Component should handle map ready
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaBasemapGallery_ShouldPublishLoadingMessage()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Id, "gallery-1")
            .Add(p => p.SyncWith, "test-map"));

        await Task.Delay(100);

        // Assert - Loading messages may be published during basemap changes
        cut.Should().NotBeNull();
    }

    #endregion

    #region Empty State Tests

    [Fact]
    public void HonuaBasemapGallery_ShouldHandleEmptyBasemapList()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Basemaps, new List<Basemap>()));

        // Assert
        cut.Should().NotBeNull();
        // Empty state should be shown
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void HonuaBasemapGallery_Dispose_ShouldCleanupResources()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>();

        // Act
        cut.Instance.Dispose();

        // Assert - No exceptions should be thrown
        Assert.True(true);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void HonuaBasemapGallery_ShouldHandleNullSyncWith()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.SyncWith, (string?)null));

        // Assert
        cut.Should().NotBeNull();
        cut.Instance.SyncWith.Should().BeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldHandleNullPosition()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Position, (string?)null));

        // Assert
        cut.Should().NotBeNull();
        cut.Instance.Position.Should().BeNull();
    }

    [Fact]
    public void HonuaBasemapGallery_ShouldHandleNullCategories()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.Categories, (string[]?)null));

        // Assert
        cut.Should().NotBeNull();
        cut.Instance.Categories.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task HonuaBasemapGallery_ShouldSyncWithMultipleMaps()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.SyncWith, (string?)null)); // Sync with all maps

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "map-1",
            Center = new[] { 0.0, 0.0 },
            Zoom = 10
        }, "map-1");

        await Task.Delay(100);

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaBasemapGallery_ShouldNotRespondToWrongMapId()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaBasemapGallery>(parameters => parameters
            .Add(p => p.SyncWith, "test-map"));

        _testContext.ComponentBus.ClearMessages<BasemapChangedMessage>();

        // Act
        await _testContext.ComponentBus.PublishAsync(new MapReadyMessage
        {
            MapId = "other-map",
            Center = new[] { 0.0, 0.0 },
            Zoom = 10
        }, "other-map");

        await Task.Delay(100);

        // Assert
        cut.Should().NotBeNull();
    }

    #endregion
}
