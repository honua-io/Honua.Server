// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Tests.LocationServices;

[Trait("Category", "Unit")]
public sealed class LocationServiceExtensionsTests
{
    [Fact]
    public void AddLocationServices_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();

        // Act
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<LocationServiceConfiguration>().Should().NotBeNull();
        serviceProvider.GetService<IGeocodingProvider>().Should().NotBeNull();
        serviceProvider.GetService<IRoutingProvider>().Should().NotBeNull();
        serviceProvider.GetService<IBasemapTileProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddLocationServices_WithNominatimProvider_RegistersNominatim()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration("nominatim", "osrm", "openstreetmap");

        // Act
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var geocodingProvider = serviceProvider.GetRequiredService<IGeocodingProvider>();
        geocodingProvider.ProviderKey.Should().Be("nominatim");

        var routingProvider = serviceProvider.GetRequiredService<IRoutingProvider>();
        routingProvider.ProviderKey.Should().Be("osrm");

        var basemapProvider = serviceProvider.GetRequiredService<IBasemapTileProvider>();
        basemapProvider.ProviderKey.Should().Be("openstreetmap");
    }

    [Fact]
    public void AddLocationServices_WithAzureMapsProvider_RegistersAzureMaps()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration("azure-maps", "azure-maps", "azure-maps");

        // Act
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var geocodingProvider = serviceProvider.GetRequiredService<IGeocodingProvider>();
        geocodingProvider.ProviderKey.Should().Be("azure-maps");

        var routingProvider = serviceProvider.GetRequiredService<IRoutingProvider>();
        routingProvider.ProviderKey.Should().Be("azure-maps");

        var basemapProvider = serviceProvider.GetRequiredService<IBasemapTileProvider>();
        basemapProvider.ProviderKey.Should().Be("azure-maps");
    }

    [Fact]
    public void GetGeocodingProvider_WithValidProviderKey_ReturnsProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider = serviceProvider.GetGeocodingProvider("nominatim");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("nominatim");
    }

    [Fact]
    public void GetRoutingProvider_WithValidProviderKey_ReturnsProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider = serviceProvider.GetRoutingProvider("osrm");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("osrm");
    }

    [Fact]
    public void GetBasemapTileProvider_WithValidProviderKey_ReturnsProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider = serviceProvider.GetBasemapTileProvider("openstreetmap");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("openstreetmap");
    }

    [Fact]
    public void GetGeocodingProvider_WithCaseInsensitiveKey_ReturnsProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider = serviceProvider.GetGeocodingProvider("NOMINATIM");

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("nominatim");
    }

    [Fact]
    public void AddLocationServices_WithMultipleProviders_SupportsProviderSwitching()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        services.AddLocationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var nominatimProvider = serviceProvider.GetGeocodingProvider("nominatim");
        var azureMapsProvider = serviceProvider.GetGeocodingProvider("azure-maps");

        // Assert
        nominatimProvider.ProviderKey.Should().Be("nominatim");
        azureMapsProvider.ProviderKey.Should().Be("azure-maps");
    }

    private IConfiguration CreateTestConfiguration(
        string geocodingProvider = "nominatim",
        string routingProvider = "osrm",
        string basemapProvider = "openstreetmap")
    {
        var configData = new Dictionary<string, string?>
        {
            ["LocationServices:GeocodingProvider"] = geocodingProvider,
            ["LocationServices:RoutingProvider"] = routingProvider,
            ["LocationServices:BasemapTileProvider"] = basemapProvider,
            ["LocationServices:AzureMaps:SubscriptionKey"] = "test-key-12345",
            ["LocationServices:AzureMaps:BaseUrl"] = "https://atlas.microsoft.com",
            ["LocationServices:Nominatim:BaseUrl"] = "https://nominatim.test.com",
            ["LocationServices:Nominatim:UserAgent"] = "TestApp/1.0",
            ["LocationServices:Osrm:BaseUrl"] = "https://osrm.test.com",
            ["LocationServices:OsmTiles:UserAgent"] = "TestApp/1.0"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
