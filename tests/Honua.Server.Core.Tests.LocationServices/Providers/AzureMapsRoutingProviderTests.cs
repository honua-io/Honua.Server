// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.AzureMaps;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace Honua.Server.Core.Tests.LocationServices.Providers;

[Trait("Category", "Unit")]
public sealed class AzureMapsRoutingProviderTests
{
    private readonly Mock<ILogger<AzureMapsRoutingProvider>> _mockLogger;
    private readonly string _testSubscriptionKey = "test-key-12345";

    public AzureMapsRoutingProviderTests()
    {
        _mockLogger = new Mock<ILogger<AzureMapsRoutingProvider>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("azure-maps");
        provider.ProviderName.Should().Be("Azure Maps Routing");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AzureMapsRoutingProvider(null!, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public async Task CalculateRouteAsync_WithValidWaypoints_ReturnsRoute()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new
                    {
                        lengthInMeters = 15000.0,
                        travelTimeInSeconds = 900.0,
                        trafficDelayInSeconds = 120.0,
                        description = "Route from Seattle to Redmond"
                    },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                            points = new[]
                            {
                                new { latitude = 47.608013, longitude = -122.335167 },
                                new { latitude = 47.64358, longitude = -122.13493 }
                            }
                        }
                    },
                    guidance = new
                    {
                        instructions = new[]
                        {
                            new
                            {
                                message = "Turn right onto I-90",
                                distanceInMeters = 500.0,
                                timeInSeconds = 30.0,
                                maneuverType = "turn-right",
                                street = "I-90",
                                point = new { latitude = 47.608013, longitude = -122.335167 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.335167, 47.608013 },
                new[] { -122.13493, 47.64358 }
            }
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Routes.Should().HaveCount(1);
        response.Attribution.Should().Be("© Microsoft Azure Maps");

        var route = response.Routes.First();
        route.DistanceMeters.Should().Be(15000.0);
        route.DurationSeconds.Should().Be(900.0);
        route.DurationWithTrafficSeconds.Should().Be(1020.0);
        route.Summary.Should().Contain("Seattle");
        route.Instructions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CalculateRouteAsync_WithTruckMode_IncludesVehicleSpecs()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new { lengthInMeters = 20000.0, travelTimeInSeconds = 1200.0 },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 20000.0, travelTimeInSeconds = 1200.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.7, longitude = -122.2 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            },
            TravelMode = "truck",
            Vehicle = new VehicleSpecifications
            {
                WeightKg = 15000,
                HeightMeters = 4.0,
                WidthMeters = 2.5,
                LengthMeters = 12.0
            }
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(1);
        response.Routes.First().DistanceMeters.Should().Be(20000.0);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithAvoidOptions_AppliesConstraints()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new { lengthInMeters = 18000.0, travelTimeInSeconds = 1100.0 },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 18000.0, travelTimeInSeconds = 1100.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.7, longitude = -122.2 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            },
            AvoidTolls = true,
            AvoidHighways = true,
            AvoidFerries = true
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(1);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithTraffic_IncludesTrafficData()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new
                    {
                        lengthInMeters = 15000.0,
                        travelTimeInSeconds = 900.0,
                        trafficDelayInSeconds = 300.0
                    },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.7, longitude = -122.2 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            },
            UseTraffic = true
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.First().DurationWithTrafficSeconds.Should().Be(1200.0);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithDepartureTime_SchedulesRoute()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.7, longitude = -122.2 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            },
            DepartureTime = DateTimeOffset.UtcNow.AddHours(2)
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("car")]
    [InlineData("truck")]
    [InlineData("bicycle")]
    [InlineData("pedestrian")]
    public async Task CalculateRouteAsync_WithDifferentTravelModes_SupportsAllModes(string travelMode)
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 15000.0, travelTimeInSeconds = 900.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.7, longitude = -122.2 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            },
            TravelMode = travelMode
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(1);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithLessThanTwoWaypoints_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://atlas.microsoft.com") };
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[] { new[] { -122.3, 47.6 } }
        };

        // Act
        var act = async () => await provider.CalculateRouteAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CalculateRouteAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, new { error = "Invalid subscription key" });
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.7 }
            }
        };

        // Act
        var act = async () => await provider.CalculateRouteAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Azure Maps routing failed*");
    }

    [Fact]
    public async Task TestConnectivityAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new
        {
            routes = new[]
            {
                new
                {
                    summary = new { lengthInMeters = 10000.0, travelTimeInSeconds = 600.0 },
                    legs = new[]
                    {
                        new
                        {
                            summary = new { lengthInMeters = 10000.0, travelTimeInSeconds = 600.0 },
                            points = new[]
                            {
                                new { latitude = 47.6, longitude = -122.3 },
                                new { latitude = 47.64, longitude = -122.13 }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectivityAsync_WithFailedConnection_ReturnsFalse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, new { error = "Service unavailable" });
        var provider = new AzureMapsRoutingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeFalse();
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://atlas.microsoft.com")
        };
    }
}
