// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.AzureMaps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.Server.Integration.Tests.LocationServices;

/// <summary>
/// Integration tests for Azure Maps providers.
/// These tests require a valid Azure Maps subscription key in configuration.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "AzureMaps")]
public sealed class AzureMapsIntegrationTests : IAsyncLifetime
{
    private readonly Mock<ILogger<AzureMapsGeocodingProvider>> _mockGeocodingLogger;
    private readonly Mock<ILogger<AzureMapsRoutingProvider>> _mockRoutingLogger;
    private readonly Mock<ILogger<AzureMapsBasemapTileProvider>> _mockBasemapLogger;
    private HttpClient? _httpClient;
    private string? _subscriptionKey;
    private AzureMapsGeocodingProvider? _geocodingProvider;
    private AzureMapsRoutingProvider? _routingProvider;
    private AzureMapsBasemapTileProvider? _basemapProvider;

    public AzureMapsIntegrationTests()
    {
        _mockGeocodingLogger = new Mock<ILogger<AzureMapsGeocodingProvider>>();
        _mockRoutingLogger = new Mock<ILogger<AzureMapsRoutingProvider>>();
        _mockBasemapLogger = new Mock<ILogger<AzureMapsBasemapTileProvider>>();
    }

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AzureMapsIntegrationTests>()
            .AddEnvironmentVariables()
            .Build();

        _subscriptionKey = configuration["AZURE_MAPS_SUBSCRIPTION_KEY"] ??
                          configuration["LocationServices:AzureMaps:SubscriptionKey"];

        if (!string.IsNullOrEmpty(_subscriptionKey))
        {
            _httpClient = new HttpClient();
            _geocodingProvider = new AzureMapsGeocodingProvider(_httpClient, _subscriptionKey, _mockGeocodingLogger.Object);
            _routingProvider = new AzureMapsRoutingProvider(_httpClient, _subscriptionKey, _mockRoutingLogger.Object);
            _basemapProvider = new AzureMapsBasemapTileProvider(_httpClient, _subscriptionKey, _mockBasemapLogger.Object);
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires Azure Maps subscription key")]
    public async Task GeocodingProvider_WithRealApiKey_CanGeocodeAddress()
    {
        // Arrange
        SkipIfNoApiKey();
        var request = new GeocodingRequest
        {
            Query = "1 Microsoft Way, Redmond, WA"
        };

        // Act
        var response = await _geocodingProvider!.GeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeEmpty();
        response.Results.First().FormattedAddress.Should().Contain("Microsoft");
    }

    [Fact(Skip = "Requires Azure Maps subscription key")]
    public async Task RoutingProvider_WithRealApiKey_CanCalculateRoute()
    {
        // Arrange
        SkipIfNoApiKey();
        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.335167, 47.608013 },  // Seattle
                new[] { -122.13493, 47.64358 }     // Redmond
            }
        };

        // Act
        var response = await _routingProvider!.CalculateRouteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Routes.Should().NotBeEmpty();
        response.Routes.First().DistanceMeters.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "Requires Azure Maps subscription key")]
    public async Task BasemapProvider_WithRealApiKey_CanFetchTile()
    {
        // Arrange
        SkipIfNoApiKey();
        var request = new TileRequest
        {
            TilesetId = "road",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var response = await _basemapProvider!.GetTileAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Data.Should().NotBeEmpty();
        response.ContentType.Should().Contain("image");
    }

    [Fact(Skip = "Requires Azure Maps subscription key")]
    public async Task GeocodingProvider_TestConnectivity_ReturnsTrue()
    {
        // Arrange
        SkipIfNoApiKey();

        // Act
        var result = await _geocodingProvider!.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    private void SkipIfNoApiKey()
    {
        if (string.IsNullOrEmpty(_subscriptionKey))
        {
            throw new SkipException("Azure Maps subscription key not configured");
        }
    }

    private class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
