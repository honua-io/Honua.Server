// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.OpenStreetMap;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.Server.Integration.Tests.LocationServices;

/// <summary>
/// Integration tests for OSRM routing provider.
/// These tests can run against the public OSRM API (rate-limited).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "OSRM")]
public sealed class OsrmIntegrationTests : IAsyncLifetime
{
    private readonly Mock<ILogger<OsrmRoutingProvider>> _mockLogger;
    private OsrmRoutingProvider? _provider;
    private HttpClient? _httpClient;

    public OsrmIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<OsrmRoutingProvider>>();
    }

    public Task InitializeAsync()
    {
        _httpClient = new HttpClient();
        _provider = new OsrmRoutingProvider(
            _httpClient,
            _mockLogger.Object,
            "https://router.project-osrm.org");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires internet connection and rate-limited by OSRM")]
    public async Task CalculateRouteAsync_WithRealCoordinates_ReturnsValidRoute()
    {
        // Arrange
        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -0.1278, 51.5074 },  // London
                new[] { -0.0877, 51.5151 }   // Nearby point
            }
        };

        // Act
        var response = await _provider!.CalculateRouteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Routes.Should().NotBeEmpty();
        response.Routes.First().DistanceMeters.Should().BeGreaterThan(0);
        response.Routes.First().DurationSeconds.Should().BeGreaterThan(0);
        response.Routes.First().Geometry.Should().NotBeNullOrEmpty();
    }

    [Fact(Skip = "Requires internet connection and rate-limited by OSRM")]
    public async Task TestConnectivityAsync_WithPublicEndpoint_ReturnsTrue()
    {
        // Act
        var result = await _provider!.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Theory(Skip = "Requires internet connection and rate-limited by OSRM")]
    [InlineData("car")]
    [InlineData("bicycle")]
    [InlineData("pedestrian")]
    public async Task CalculateRouteAsync_WithDifferentTravelModes_ReturnsAppropriateRoute(string travelMode)
    {
        // Arrange
        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -0.1278, 51.5074 },
                new[] { -0.0877, 51.5151 }
            },
            TravelMode = travelMode
        };

        // Act
        var response = await _provider!.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().NotBeEmpty();
        response.Routes.First().DistanceMeters.Should().BeGreaterThan(0);
    }
}
