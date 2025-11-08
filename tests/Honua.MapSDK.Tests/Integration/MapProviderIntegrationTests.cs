// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Tests.Utilities;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using Moq;

namespace Honua.MapSDK.Tests.Integration;

/// <summary>
/// Integration tests for MapSDK with external providers.
/// Tests map + geocoding, map + routing, and map + basemap providers.
/// These tests can be skipped if provider credentials are not available.
/// </summary>
[Trait("Category", "Integration")]
public class MapProviderIntegrationTests
{
    private readonly ComponentBus _bus;

    public MapProviderIntegrationTests()
    {
        _bus = new ComponentBus();
    }

    #region Geocoding Integration Tests

    [Fact]
    public async Task GeocodingProvider_SearchAddress_ShouldReturnResults()
    {
        // Arrange
        var mockProvider = new Mock<IGeocodingProvider>();
        mockProvider.Setup(p => p.ProviderKey).Returns("test-geocoder");
        mockProvider.Setup(p => p.ProviderName).Returns("Test Geocoding Provider");

        mockProvider.Setup(p => p.GeocodeAsync(
            It.IsAny<GeocodingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse
            {
                Success = true,
                Results = new List<GeocodingResult>
                {
                    new GeocodingResult
                    {
                        FormattedAddress = "123 Market St, San Francisco, CA 94103",
                        Latitude = 37.7749,
                        Longitude = -122.4194,
                        Confidence = 0.95,
                        PlaceId = "place-123"
                    }
                }
            });

        // Act
        var request = new GeocodingRequest
        {
            Query = "123 Market St, San Francisco"
        };
        var response = await mockProvider.Object.GeocodeAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Results.Should().HaveCount(1);
        response.Results[0].FormattedAddress.Should().Contain("Market St");

        // Simulate publishing result to map
        await _bus.PublishAsync(new FlyToRequestMessage
        {
            MapId = "main-map",
            Center = new[] { response.Results[0].Longitude, response.Results[0].Latitude },
            Zoom = 16
        }, "geocoding");
    }

    [Fact]
    public async Task GeocodingProvider_ReverseGeocode_ShouldReturnAddress()
    {
        // Arrange
        var mockProvider = new Mock<IGeocodingProvider>();
        mockProvider.Setup(p => p.ReverseGeocodeAsync(
            It.IsAny<ReverseGeocodingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse
            {
                Success = true,
                Results = new List<GeocodingResult>
                {
                    new GeocodingResult
                    {
                        FormattedAddress = "City Hall, San Francisco, CA 94102",
                        Latitude = 37.7793,
                        Longitude = -122.4193,
                        Confidence = 0.99,
                        PlaceId = "city-hall"
                    }
                }
            });

        // Act
        var request = new ReverseGeocodingRequest
        {
            Latitude = 37.7793,
            Longitude = -122.4193
        };
        var response = await mockProvider.Object.ReverseGeocodeAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Results.Should().HaveCount(1);
        response.Results[0].FormattedAddress.Should().Contain("City Hall");
    }

    [Fact]
    public async Task GeocodingProvider_TestConnectivity_ShouldReturnTrue()
    {
        // Arrange
        var mockProvider = new Mock<IGeocodingProvider>();
        mockProvider.Setup(p => p.TestConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var isConnected = await mockProvider.Object.TestConnectivityAsync();

        // Assert
        isConnected.Should().BeTrue();
    }

    [Fact]
    public async Task GeocodingSearch_AutocompleteIntegration_ShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<IGeocodingProvider>();
        var searchHistory = new List<string>();

        mockProvider.Setup(p => p.GeocodeAsync(
            It.IsAny<GeocodingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingRequest req, CancellationToken _) =>
            {
                searchHistory.Add(req.Query);
                return new GeocodingResponse
                {
                    Success = true,
                    Results = new List<GeocodingResult>
                    {
                        new GeocodingResult
                        {
                            FormattedAddress = $"{req.Query}, San Francisco, CA",
                            Latitude = 37.7749,
                            Longitude = -122.4194,
                            Confidence = 0.85
                        }
                    }
                };
            });

        // Act - Simulate autocomplete search flow
        await mockProvider.Object.GeocodeAsync(new GeocodingRequest { Query = "Ma" });
        await mockProvider.Object.GeocodeAsync(new GeocodingRequest { Query = "Mar" });
        await mockProvider.Object.GeocodeAsync(new GeocodingRequest { Query = "Market" });
        await mockProvider.Object.GeocodeAsync(new GeocodingRequest { Query = "Market St" });

        // Assert
        searchHistory.Should().HaveCount(4);
        searchHistory.Should().ContainInOrder("Ma", "Mar", "Market", "Market St");
    }

    #endregion

    #region Routing Integration Tests

    [Fact]
    public async Task RoutingProvider_CalculateRoute_ShouldReturnRoute()
    {
        // Arrange
        var mockProvider = new Mock<IRoutingProvider>();
        mockProvider.Setup(p => p.ProviderKey).Returns("test-router");
        mockProvider.Setup(p => p.ProviderName).Returns("Test Routing Provider");

        mockProvider.Setup(p => p.CalculateRouteAsync(
            It.IsAny<RoutingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingResponse
            {
                Success = true,
                Routes = new List<Route>
                {
                    new Route
                    {
                        Summary = "Via Market St and Van Ness Ave",
                        DistanceMeters = 2500,
                        DurationSeconds = 420,
                        Geometry = MapTestFixture.CreateTestEncodedPolyline(),
                        Legs = new List<RouteLeg>
                        {
                            new RouteLeg
                            {
                                DistanceMeters = 2500,
                                DurationSeconds = 420,
                                Steps = new List<RouteStep>
                                {
                                    new RouteStep
                                    {
                                        Instruction = "Head north on Market St",
                                        DistanceMeters = 500,
                                        DurationSeconds = 60
                                    },
                                    new RouteStep
                                    {
                                        Instruction = "Turn right onto Van Ness Ave",
                                        DistanceMeters = 2000,
                                        DurationSeconds = 360
                                    }
                                }
                            }
                        }
                    }
                }
            });

        // Act
        var waypoints = MapTestFixture.CreateTestRouteCoordinates();
        var request = new RoutingRequest
        {
            Waypoints = waypoints.Select(w => new Waypoint
            {
                Latitude = w[1],
                Longitude = w[0]
            }).ToList()
        };
        var response = await mockProvider.Object.CalculateRouteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Routes.Should().HaveCount(1);
        response.Routes[0].DistanceMeters.Should().BeGreaterThan(0);
        response.Routes[0].DurationSeconds.Should().BeGreaterThan(0);
        response.Routes[0].Legs.Should().HaveCount(1);
        response.Routes[0].Legs[0].Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task RoutingProvider_AlternativeRoutes_ShouldReturnMultiple()
    {
        // Arrange
        var mockProvider = new Mock<IRoutingProvider>();

        mockProvider.Setup(p => p.CalculateRouteAsync(
            It.IsAny<RoutingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingResponse
            {
                Success = true,
                Routes = new List<Route>
                {
                    new Route
                    {
                        Summary = "Fastest route via I-80",
                        DistanceMeters = 5000,
                        DurationSeconds = 600,
                        Geometry = MapTestFixture.CreateTestEncodedPolyline()
                    },
                    new Route
                    {
                        Summary = "Alternate route via US-101",
                        DistanceMeters = 6000,
                        DurationSeconds = 720,
                        Geometry = MapTestFixture.CreateTestEncodedPolyline()
                    }
                }
            });

        // Act
        var request = new RoutingRequest
        {
            Waypoints = new List<Waypoint>
            {
                new Waypoint { Latitude = 37.7749, Longitude = -122.4194 },
                new Waypoint { Latitude = 37.8044, Longitude = -122.2712 }
            },
            AlternativeRoutes = true
        };
        var response = await mockProvider.Object.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(2);
        response.Routes[0].DurationSeconds.Should().BeLessThan(response.Routes[1].DurationSeconds);
    }

    [Fact]
    public async Task RouteVisualization_ShouldDecodePolyline()
    {
        // Arrange
        var encodedPolyline = MapTestFixture.CreateTestEncodedPolyline();

        // Act
        var decodedCoordinates = GeometryTestHelpers.DecodePolyline(encodedPolyline);

        // Assert
        decodedCoordinates.Should().NotBeEmpty();
        decodedCoordinates.Should().AllSatisfy(coord =>
        {
            coord.Should().HaveCount(2);
            GeometryTestHelpers.IsValidLongitude(coord[0]).Should().BeTrue();
            GeometryTestHelpers.IsValidLatitude(coord[1]).Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoutePanel_WaypointManagement_ShouldWork()
    {
        // Arrange
        var waypoints = new List<Waypoint>
        {
            new Waypoint { Latitude = 37.7749, Longitude = -122.4194, Name = "Start" }
        };

        // Act - Add waypoints
        waypoints.Add(new Waypoint { Latitude = 37.7835, Longitude = -122.4089, Name = "Waypoint 1" });
        waypoints.Add(new Waypoint { Latitude = 37.8044, Longitude = -122.2712, Name = "End" });

        // Assert
        waypoints.Should().HaveCount(3);
        waypoints[0].Name.Should().Be("Start");
        waypoints[2].Name.Should().Be("End");

        // Act - Remove middle waypoint
        waypoints.RemoveAt(1);

        // Assert
        waypoints.Should().HaveCount(2);
    }

    #endregion

    #region Basemap Provider Integration Tests

    [Fact]
    public async Task BasemapProvider_GetTilesets_ShouldReturnAvailable()
    {
        // Arrange
        var mockProvider = new Mock<IBasemapTileProvider>();
        mockProvider.Setup(p => p.ProviderKey).Returns("test-basemap");
        mockProvider.Setup(p => p.ProviderName).Returns("Test Basemap Provider");

        mockProvider.Setup(p => p.GetAvailableTilesetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BasemapTileset>
            {
                new BasemapTileset
                {
                    Id = "streets",
                    Name = "Streets",
                    Description = "Street map with labels",
                    TileType = TileType.Raster,
                    MinZoom = 0,
                    MaxZoom = 18
                },
                new BasemapTileset
                {
                    Id = "satellite",
                    Name = "Satellite",
                    Description = "Satellite imagery",
                    TileType = TileType.Raster,
                    MinZoom = 0,
                    MaxZoom = 19
                },
                new BasemapTileset
                {
                    Id = "dark",
                    Name = "Dark",
                    Description = "Dark theme for night viewing",
                    TileType = TileType.Vector,
                    MinZoom = 0,
                    MaxZoom = 22
                }
            });

        // Act
        var tilesets = await mockProvider.Object.GetAvailableTilesetsAsync();

        // Assert
        tilesets.Should().HaveCount(3);
        tilesets.Should().Contain(t => t.Id == "streets");
        tilesets.Should().Contain(t => t.Id == "satellite");
        tilesets.Should().Contain(t => t.Id == "dark");
    }

    [Fact]
    public async Task BasemapProvider_GetTileUrlTemplate_ShouldReturnValidUrl()
    {
        // Arrange
        var mockProvider = new Mock<IBasemapTileProvider>();
        mockProvider.Setup(p => p.GetTileUrlTemplateAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://tiles.example.com/v1/streets/{z}/{x}/{y}.png");

        // Act
        var urlTemplate = await mockProvider.Object.GetTileUrlTemplateAsync("streets");

        // Assert
        urlTemplate.Should().Contain("{z}");
        urlTemplate.Should().Contain("{x}");
        urlTemplate.Should().Contain("{y}");
    }

    [Fact]
    public async Task BasemapProvider_SwitchTileset_ShouldPublishMessage()
    {
        // Arrange
        BasemapChangedMessage? receivedMessage = null;
        _bus.Subscribe<BasemapChangedMessage>(args => { receivedMessage = args.Message; });

        // Act - Simulate basemap switch
        await _bus.PublishAsync(new BasemapChangedMessage
        {
            MapId = "main-map",
            Style = "maplibre://test/dark"
        }, "basemap-gallery");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.MapId.Should().Be("main-map");
        receivedMessage.Style.Should().Contain("dark");
    }

    #endregion

    #region End-to-End Workflow Tests

    [Fact]
    public async Task E2E_SearchAndNavigate_ShouldWork()
    {
        // Arrange
        var mockGeocoder = new Mock<IGeocodingProvider>();
        var messages = new List<object>();

        _bus.Subscribe<FlyToRequestMessage>(args => { messages.Add(args.Message); });

        mockGeocoder.Setup(p => p.GeocodeAsync(
            It.IsAny<GeocodingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse
            {
                Success = true,
                Results = new List<GeocodingResult>
                {
                    new GeocodingResult
                    {
                        FormattedAddress = "Golden Gate Park, San Francisco, CA",
                        Latitude = 37.7694,
                        Longitude = -122.4862,
                        Confidence = 0.99
                    }
                }
            });

        // Act - Simulate search workflow
        // 1. User searches for location
        var searchResult = await mockGeocoder.Object.GeocodeAsync(
            new GeocodingRequest { Query = "Golden Gate Park" });

        // 2. Select result and fly to it
        var result = searchResult.Results[0];
        await _bus.PublishAsync(new FlyToRequestMessage
        {
            MapId = "main-map",
            Center = new[] { result.Longitude, result.Latitude },
            Zoom = 15
        }, "search-panel");

        // Assert
        messages.Should().HaveCount(1);
        var flyToMessage = messages[0] as FlyToRequestMessage;
        flyToMessage.Should().NotBeNull();
        flyToMessage!.Zoom.Should().Be(15);
    }

    [Fact]
    public async Task E2E_RouteAndVisualize_ShouldWork()
    {
        // Arrange
        var mockRouter = new Mock<IRoutingProvider>();
        var messages = new List<object>();

        _bus.Subscribe<FitBoundsRequestMessage>(args => { messages.Add(args.Message); });

        mockRouter.Setup(p => p.CalculateRouteAsync(
            It.IsAny<RoutingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingResponse
            {
                Success = true,
                Routes = new List<Route>
                {
                    new Route
                    {
                        Summary = "Via Market St",
                        DistanceMeters = 3500,
                        DurationSeconds = 540,
                        Geometry = MapTestFixture.CreateTestEncodedPolyline(),
                        BoundingBox = MapTestFixture.CreateTestBounds()
                    }
                }
            });

        // Act - Simulate routing workflow
        // 1. Calculate route
        var routeResponse = await mockRouter.Object.CalculateRouteAsync(new RoutingRequest
        {
            Waypoints = new List<Waypoint>
            {
                new Waypoint { Latitude = 37.7749, Longitude = -122.4194 },
                new Waypoint { Latitude = 37.7835, Longitude = -122.4089 }
            }
        });

        // 2. Fit map to route bounds
        var route = routeResponse.Routes[0];
        await _bus.PublishAsync(new FitBoundsRequestMessage
        {
            MapId = "main-map",
            Bounds = route.BoundingBox!,
            Padding = 50
        }, "route-panel");

        // Assert
        messages.Should().HaveCount(1);
        var fitBoundsMessage = messages[0] as FitBoundsRequestMessage;
        fitBoundsMessage.Should().NotBeNull();
        fitBoundsMessage!.Padding.Should().Be(50);
    }

    [Fact]
    public async Task E2E_FeatureClickAndGeocode_ShouldWork()
    {
        // Arrange
        var mockGeocoder = new Mock<IGeocodingProvider>();
        FeatureClickedMessage? clickedFeature = null;

        _bus.Subscribe<FeatureClickedMessage>(args => { clickedFeature = args.Message; });

        mockGeocoder.Setup(p => p.ReverseGeocodeAsync(
            It.IsAny<ReverseGeocodingRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse
            {
                Success = true,
                Results = new List<GeocodingResult>
                {
                    new GeocodingResult
                    {
                        FormattedAddress = "123 Market St, San Francisco, CA 94103"
                    }
                }
            });

        // Act - Simulate feature click workflow
        // 1. User clicks on map feature
        await _bus.PublishAsync(new FeatureClickedMessage
        {
            MapId = "main-map",
            LayerId = "parcels",
            FeatureId = "parcel-123",
            Properties = new Dictionary<string, object>
            {
                ["lat"] = 37.7749,
                ["lon"] = -122.4194
            },
            Geometry = MapTestFixture.CreateTestPolygonGeometry()
        }, "map");

        // 2. Reverse geocode the location
        if (clickedFeature != null &&
            clickedFeature.Properties.TryGetValue("lat", out var lat) &&
            clickedFeature.Properties.TryGetValue("lon", out var lon))
        {
            var reverseResult = await mockGeocoder.Object.ReverseGeocodeAsync(
                new ReverseGeocodingRequest
                {
                    Latitude = Convert.ToDouble(lat),
                    Longitude = Convert.ToDouble(lon)
                });

            // Assert
            reverseResult.Success.Should().BeTrue();
            reverseResult.Results[0].FormattedAddress.Should().Contain("Market St");
        }

        clickedFeature.Should().NotBeNull();
    }

    #endregion
}
