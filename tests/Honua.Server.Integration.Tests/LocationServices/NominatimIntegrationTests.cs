// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.OpenStreetMap;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.Server.Integration.Tests.LocationServices;

/// <summary>
/// Integration tests for Nominatim geocoding provider.
/// These tests can run against the public Nominatim API (rate-limited).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "Nominatim")]
public sealed class NominatimIntegrationTests : IAsyncLifetime
{
    private readonly Mock<ILogger<NominatimGeocodingProvider>> _mockLogger;
    private NominatimGeocodingProvider? _provider;
    private HttpClient? _httpClient;

    public NominatimIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<NominatimGeocodingProvider>>();
    }

    public Task InitializeAsync()
    {
        _httpClient = new HttpClient();
        _provider = new NominatimGeocodingProvider(
            _httpClient,
            _mockLogger.Object,
            "https://nominatim.openstreetmap.org",
            "HonuaServer-Tests/1.0");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires internet connection and rate-limited by Nominatim")]
    public async Task GeocodeAsync_WithRealAddress_ReturnsValidResults()
    {
        // Arrange
        var request = new GeocodingRequest
        {
            Query = "1600 Pennsylvania Avenue NW, Washington, DC"
        };

        // Act
        var response = await _provider!.GeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeEmpty();
        response.Results.First().FormattedAddress.Should().Contain("Pennsylvania");
        response.Results.First().Latitude.Should().BeApproximately(38.897, 0.01);
        response.Results.First().Longitude.Should().BeApproximately(-77.036, 0.01);
    }

    [Fact(Skip = "Requires internet connection and rate-limited by Nominatim")]
    public async Task ReverseGeocodeAsync_WithRealCoordinates_ReturnsValidAddress()
    {
        // Arrange
        var request = new ReverseGeocodingRequest
        {
            Longitude = -77.0369,
            Latitude = 38.8977
        };

        // Act
        var response = await _provider!.ReverseGeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeEmpty();
        response.Results.First().FormattedAddress.Should().Contain("Washington");
    }

    [Fact(Skip = "Requires internet connection and rate-limited by Nominatim")]
    public async Task TestConnectivityAsync_WithPublicEndpoint_ReturnsTrue()
    {
        // Act
        var result = await _provider!.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip = "Requires internet connection and rate-limited by Nominatim")]
    public async Task GeocodeAsync_WithCountryCode_FiltersResults()
    {
        // Arrange
        var request = new GeocodingRequest
        {
            Query = "Paris",
            CountryCodes = new[] { "fr" }
        };

        // Act
        var response = await _provider!.GeocodeAsync(request);

        // Assert
        response.Results.Should().NotBeEmpty();
        response.Results.First().Components?.CountryCode.Should().Be("fr");
    }
}
