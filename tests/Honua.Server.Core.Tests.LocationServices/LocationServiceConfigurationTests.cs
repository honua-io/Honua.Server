// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Tests.LocationServices;

[Trait("Category", "Unit")]
public sealed class LocationServiceConfigurationTests
{
    [Fact]
    public void Configuration_WithDefaults_UsesExpectedDefaults()
    {
        // Arrange & Act
        var config = new LocationServiceConfiguration();

        // Assert
        config.GeocodingProvider.Should().Be("nominatim");
        config.RoutingProvider.Should().Be("osrm");
        config.BasemapTileProvider.Should().Be("openstreetmap");
    }

    [Fact]
    public void Configuration_CanBindFromConfigurationSection()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["LocationServices:GeocodingProvider"] = "azure-maps",
            ["LocationServices:RoutingProvider"] = "azure-maps",
            ["LocationServices:BasemapTileProvider"] = "azure-maps",
            ["LocationServices:AzureMaps:SubscriptionKey"] = "test-key-12345",
            ["LocationServices:AzureMaps:BaseUrl"] = "https://atlas.microsoft.com",
            ["LocationServices:Nominatim:BaseUrl"] = "https://nominatim.test.com",
            ["LocationServices:Nominatim:UserAgent"] = "TestApp/1.0",
            ["LocationServices:Osrm:BaseUrl"] = "https://osrm.test.com"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var config = new LocationServiceConfiguration();

        // Act
        configuration.GetSection(LocationServiceConfiguration.SectionName).Bind(config);

        // Assert
        config.GeocodingProvider.Should().Be("azure-maps");
        config.RoutingProvider.Should().Be("azure-maps");
        config.BasemapTileProvider.Should().Be("azure-maps");
        config.AzureMaps.Should().NotBeNull();
        config.AzureMaps!.SubscriptionKey.Should().Be("test-key-12345");
        config.Nominatim.Should().NotBeNull();
        config.Nominatim!.BaseUrl.Should().Be("https://nominatim.test.com");
        config.Osrm.Should().NotBeNull();
        config.Osrm!.BaseUrl.Should().Be("https://osrm.test.com");
    }

    [Fact]
    public void AzureMapsConfiguration_WithRequiredSubscriptionKey_IsValid()
    {
        // Arrange & Act
        var config = new AzureMapsConfiguration
        {
            SubscriptionKey = "test-key-12345"
        };

        // Assert
        config.SubscriptionKey.Should().Be("test-key-12345");
        config.BaseUrl.Should().Be("https://atlas.microsoft.com");
    }

    [Fact]
    public void NominatimConfiguration_WithDefaults_UsesPublicServer()
    {
        // Arrange & Act
        var config = new NominatimConfiguration();

        // Assert
        config.BaseUrl.Should().Be("https://nominatim.openstreetmap.org");
        config.UserAgent.Should().Be("HonuaServer/1.0");
    }

    [Fact]
    public void OsrmConfiguration_WithDefaults_UsesPublicServer()
    {
        // Arrange & Act
        var config = new OsrmConfiguration();

        // Assert
        config.BaseUrl.Should().Be("https://router.project-osrm.org");
    }

    [Fact]
    public void OsmTilesConfiguration_WithDefaults_HasUserAgent()
    {
        // Arrange & Act
        var config = new OsmTilesConfiguration();

        // Assert
        config.UserAgent.Should().Be("HonuaServer/1.0");
    }

    [Fact]
    public void OsmTilesConfiguration_CanSetCustomTileUrls()
    {
        // Arrange
        var customUrls = new Dictionary<string, string>
        {
            ["custom"] = "https://custom.tiles.com/{z}/{x}/{y}.png"
        };

        // Act
        var config = new OsmTilesConfiguration
        {
            CustomTileUrls = customUrls
        };

        // Assert
        config.CustomTileUrls.Should().ContainKey("custom");
        config.CustomTileUrls!["custom"].Should().Be("https://custom.tiles.com/{z}/{x}/{y}.png");
    }

    [Theory]
    [InlineData("nominatim", "osrm", "openstreetmap")]
    [InlineData("azure-maps", "azure-maps", "azure-maps")]
    [InlineData("nominatim", "azure-maps", "openstreetmap")]
    public void Configuration_CanSetDifferentProviderCombinations(
        string geocodingProvider,
        string routingProvider,
        string basemapProvider)
    {
        // Arrange & Act
        var config = new LocationServiceConfiguration
        {
            GeocodingProvider = geocodingProvider,
            RoutingProvider = routingProvider,
            BasemapTileProvider = basemapProvider
        };

        // Assert
        config.GeocodingProvider.Should().Be(geocodingProvider);
        config.RoutingProvider.Should().Be(routingProvider);
        config.BasemapTileProvider.Should().Be(basemapProvider);
    }
}
