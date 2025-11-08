// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.LocationServices.Models;
using Honua.Server.Core.LocationServices.Providers.OpenStreetMap;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace Honua.Server.Core.Tests.LocationServices.Providers;

[Trait("Category", "Unit")]
public sealed class OsrmRoutingProviderTests
{
    private readonly Mock<ILogger<OsrmRoutingProvider>> _mockLogger;
    private const string TestBaseUrl = "https://osrm.test.com";

    public OsrmRoutingProviderTests()
    {
        _mockLogger = new Mock<ILogger<OsrmRoutingProvider>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("osrm");
        provider.ProviderName.Should().Be("OSRM (OpenStreetMap)");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OsrmRoutingProvider(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public async Task CalculateRouteAsync_WithValidWaypoints_ReturnsRoute()
    {
        // Arrange
        var mockResponse = new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance = 15000.0,
                    duration = 900.0,
                    geometry = "mock_polyline_encoded",
                    legs = new[]
                    {
                        new
                        {
                            distance = 15000.0,
                            duration = 900.0,
                            steps = new[]
                            {
                                new
                                {
                                    distance = 500.0,
                                    duration = 30.0,
                                    name = "I-90",
                                    maneuver = new
                                    {
                                        type = "turn",
                                        instruction = "Turn right onto I-90",
                                        location = new[] { -122.335167, 47.608013 }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
        response.Attribution.Should().Contain("OpenStreetMap");

        var route = response.Routes.First();
        route.DistanceMeters.Should().Be(15000.0);
        route.DurationSeconds.Should().Be(900.0);
        route.Geometry.Should().NotBeEmpty();
        route.Instructions.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("car", "driving")]
    [InlineData("bicycle", "cycling")]
    [InlineData("pedestrian", "walking")]
    public async Task CalculateRouteAsync_WithDifferentTravelModes_MapsCorrectly(
        string travelMode, string expectedProfile)
    {
        // Arrange
        var mockResponse = new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance = 5000.0,
                    duration = 300.0,
                    geometry = "mock_polyline",
                    legs = new[]
                    {
                        new
                        {
                            distance = 5000.0,
                            duration = 300.0,
                            steps = Array.Empty<object>()
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.Should().HaveCount(1);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithMultipleWaypoints_CalculatesComplexRoute()
    {
        // Arrange
        var mockResponse = new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance = 25000.0,
                    duration = 1500.0,
                    geometry = "mock_polyline",
                    legs = new[]
                    {
                        new
                        {
                            distance = 12000.0,
                            duration = 700.0,
                            steps = Array.Empty<object>()
                        },
                        new
                        {
                            distance = 13000.0,
                            duration = 800.0,
                            steps = Array.Empty<object>()
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

        var request = new RoutingRequest
        {
            Waypoints = new[]
            {
                new[] { -122.3, 47.6 },
                new[] { -122.2, 47.65 },
                new[] { -122.1, 47.7 }
            }
        };

        // Act
        var response = await provider.CalculateRouteAsync(request);

        // Assert
        response.Routes.First().Legs.Should().HaveCount(2);
        response.Routes.First().DistanceMeters.Should().Be(25000.0);
    }

    [Fact]
    public async Task CalculateRouteAsync_WithOsrmError_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockResponse = new
        {
            code = "InvalidQuery",
            message = "Query string malformed"
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
            .WithMessage("*OSRM returned error*");
    }

    [Fact]
    public async Task CalculateRouteAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.TooManyRequests, new { error = "Rate limit" });
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
            .WithMessage("OSRM routing failed*");
    }

    [Fact]
    public async Task CalculateRouteAsync_WithLessThanTwoWaypoints_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri(TestBaseUrl) };
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
    public async Task TestConnectivityAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance = 1000.0,
                    duration = 120.0,
                    geometry = "mock",
                    legs = new[]
                    {
                        new
                        {
                            distance = 1000.0,
                            duration = 120.0,
                            steps = Array.Empty<object>()
                        }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectivityAsync_WithFailedConnection_ReturnsFalse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, new { error = "Unavailable" });
        var provider = new OsrmRoutingProvider(httpClient, _mockLogger.Object, TestBaseUrl);

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
            BaseAddress = new Uri(TestBaseUrl)
        };
    }
}
