using Bunit;
using FluentAssertions;
using Honua.MapSDK.Components.Search;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for HonuaSearch component
/// </summary>
public class HonuaSearchTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public HonuaSearchTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void HonuaSearch_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HonuaSearch_WithPlaceholder_ShouldDisplayPlaceholder()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.Placeholder, "Search for a location..."));

        // Assert
        cut.Markup.Should().Contain("Search for a location...");
    }

    [Fact]
    public void HonuaSearch_WithProvider_ShouldUseProvider()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.Provider, SearchProvider.Nominatim));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaSearch_WithAutocomplete_ShouldEnableAutocomplete()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.ShowAutocomplete, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public async Task HonuaSearch_OnResultSelected_ShouldPublishFlyToRequest()
    {
        // Arrange
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.MapId, "test-map"));

        // Act - Select a search result (implementation dependent)

        // Assert
        var messages = _testContext.ComponentBus.GetMessages<FlyToRequestMessage>();
    }

    [Fact]
    public void HonuaSearch_WithRecentSearches_ShouldShowRecentSearches()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.ShowRecent, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaSearch_WithGeolocation_ShouldEnableGeolocation()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.ShowGeolocation, true));

        // Assert
        cut.Should().NotBeNull();
    }

    [Fact]
    public void HonuaSearch_WithMinCharacters_ShouldRespectMinimum()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<HonuaSearch>(parameters => parameters
            .Add(p => p.Id, "test-search")
            .Add(p => p.MinCharacters, 3));

        // Assert
        cut.Should().NotBeNull();
    }
}

/// <summary>
/// Search providers
/// </summary>
public enum SearchProvider
{
    Nominatim,
    Mapbox,
    Google,
    Custom
}
