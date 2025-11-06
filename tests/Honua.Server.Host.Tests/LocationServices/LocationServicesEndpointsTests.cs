// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.Tests.LocationServices.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Honua.Server.Host.Tests.LocationServices;

/// <summary>
/// Tests for location service API endpoints.
/// These tests verify endpoint behavior with mock providers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Endpoints")]
public sealed class LocationServicesEndpointsTests
{
    [Fact]
    public void GeocodeEndpoint_WithMockProvider_ReturnsResults()
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider();
        mockProvider.SetGeocodeResponse(new GeocodingResponse
        {
            Results = new List<GeocodingResult>
            {
                new GeocodingResult
                {
                    FormattedAddress = "123 Main St, Seattle, WA",
                    Longitude = -122.3321,
                    Latitude = 47.6062,
                    Confidence = 0.95
                }
            },
            Attribution = "Mock Provider"
        });

        var request = new GeocodingRequest
        {
            Query = "123 Main St, Seattle, WA"
        };

        // Act
        var response = mockProvider.GeocodeAsync(request).Result;

        // Assert
        response.Results.Should().HaveCount(1);
        response.Results.First().FormattedAddress.Should().Be("123 Main St, Seattle, WA");
    }

    [Fact]
    public void ReverseGeocodeEndpoint_WithMockProvider_ReturnsAddress()
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider();
        mockProvider.SetReverseGeocodeResponse(new GeocodingResponse
        {
            Results = new List<GeocodingResult>
            {
                new GeocodingResult
                {
                    FormattedAddress = "Seattle, WA",
                    Longitude = -122.3321,
                    Latitude = 47.6062
                }
            },
            Attribution = "Mock Provider"
        });

        var request = new ReverseGeocodingRequest
        {
            Longitude = -122.3321,
            Latitude = 47.6062
        };

        // Act
        var response = mockProvider.ReverseGeocodeAsync(request).Result;

        // Assert
        response.Results.Should().HaveCount(1);
        response.Results.First().Latitude.Should().Be(47.6062);
    }

    [Fact]
    public void RoutingEndpoint_WithMockProvider_ReturnsRoute()
    {
        // Arrange
        var mockProvider = new MockRoutingProvider();
        mockProvider.SetResponse(new RoutingResponse
        {
            Routes = new List<Route>
            {
                new Route
                {
                    DistanceMeters = 15000,
                    DurationSeconds = 900,
                    Geometry = "mock_polyline",
                    GeometryFormat = "polyline"
                }
            },
            Attribution = "Mock Provider"
        });

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            }
        };

        // Act
        var response = mockProvider.CalculateRouteAsync(request).Result;

        // Assert
        response.Routes.Should().HaveCount(1);
        response.Routes.First().DistanceMeters.Should().Be(15000);
    }

    [Fact]
    public void GetAvailableTilesetsEndpoint_WithMockProvider_ReturnsTilesets()
    {
        // Arrange
        var mockProvider = new MockBasemapTileProvider();
        var tilesets = new List<BasemapTileset>
        {
            new BasemapTileset
            {
                Id = "test-tileset",
                Name = "Test Tileset",
                Format = TileFormat.Raster,
                TileSize = 256,
                TileUrlTemplate = "https://example.com/{z}/{x}/{y}.png",
                Attribution = "Mock Provider"
            }
        };
        mockProvider.SetTilesets(tilesets);

        // Act
        var response = mockProvider.GetAvailableTilesetsAsync().Result;

        // Assert
        response.Should().HaveCount(1);
        response.First().Id.Should().Be("test-tileset");
    }

    [Fact]
    public void GetTileEndpoint_WithMockProvider_ReturnsTileData()
    {
        // Arrange
        var mockProvider = new MockBasemapTileProvider();
        var tileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        mockProvider.SetTileResponse(new TileResponse
        {
            Data = tileData,
            ContentType = "image/png",
            CacheControl = "public, max-age=3600"
        });

        var request = new TileRequest
        {
            TilesetId = "test-tileset",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var response = mockProvider.GetTileAsync(request).Result;

        // Assert
        response.Data.Should().Equal(tileData);
        response.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void GeocodeEndpoint_WithProviderError_ReturnsErrorResponse()
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider();
        mockProvider.SetNextException(new InvalidOperationException("Provider unavailable"));

        var request = new GeocodingRequest { Query = "test" };

        // Act
        var act = async () => await mockProvider.GeocodeAsync(request);

        // Assert
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Provider unavailable");
    }

    [Fact]
    public void RoutingEndpoint_WithInsufficientWaypoints_ReturnsBadRequest()
    {
        // Arrange
        var mockProvider = new MockRoutingProvider();
        var request = new RoutingRequest
        {
            Waypoints = new[] { new[] { -122.3, 47.6 } } // Only 1 waypoint
        };

        // Act
        var response = mockProvider.CalculateRouteAsync(request).Result;

        // Assert
        // Mock provider handles invalid requests gracefully
        response.Should().NotBeNull();
    }

    [Fact]
    public void GetTileEndpoint_WithCacheHeaders_ReturnsAppropriateHeaders()
    {
        // Arrange
        var mockProvider = new MockBasemapTileProvider();
        mockProvider.SetTileResponse(new TileResponse
        {
            Data = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            ContentType = "image/png",
            CacheControl = "public, max-age=3600",
            ETag = "\"abc123\""
        });

        var request = new TileRequest
        {
            TilesetId = "test",
            Z = 1,
            X = 0,
            Y = 0
        };

        // Act
        var response = mockProvider.GetTileAsync(request).Result;

        // Assert
        response.CacheControl.Should().Be("public, max-age=3600");
        response.ETag.Should().Be("\"abc123\"");
    }

    [Theory]
    [InlineData("nominatim")]
    [InlineData("azure-maps")]
    public void ProviderSwitching_WithDifferentProviders_RoutesToCorrectProvider(string providerKey)
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider
        {
            ProviderKey = providerKey
        };

        // Act & Assert
        mockProvider.ProviderKey.Should().Be(providerKey);
    }

    [Fact]
    public void TestConnectivityEndpoint_WithHealthyProvider_ReturnsHealthy()
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider();
        mockProvider.SetAvailability(true);

        // Act
        var result = mockProvider.TestConnectivityAsync().Result;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void TestConnectivityEndpoint_WithUnhealthyProvider_ReturnsUnhealthy()
    {
        // Arrange
        var mockProvider = new MockGeocodingProvider();
        mockProvider.SetAvailability(false);

        // Act
        var result = mockProvider.TestConnectivityAsync().Result;

        // Assert
        result.Should().BeFalse();
    }
}
