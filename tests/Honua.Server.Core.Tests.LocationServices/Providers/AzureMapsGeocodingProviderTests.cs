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
public sealed class AzureMapsGeocodingProviderTests
{
    private readonly Mock<ILogger<AzureMapsGeocodingProvider>> _mockLogger;
    private readonly string _testSubscriptionKey = "test-key-12345";

    public AzureMapsGeocodingProviderTests()
    {
        _mockLogger = new Mock<ILogger<AzureMapsGeocodingProvider>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("azure-maps");
        provider.ProviderName.Should().Be("Azure Maps Geocoding");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AzureMapsGeocodingProvider(null!, _testSubscriptionKey, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullSubscriptionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new AzureMapsGeocodingProvider(httpClient, null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("subscriptionKey");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task GeocodeAsync_WithValidAddress_ReturnsResults()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new
                {
                    type = "Point Address",
                    score = 0.95,
                    address = new
                    {
                        freeformAddress = "1 Microsoft Way, Redmond, WA 98052",
                        streetNumber = "1",
                        streetName = "Microsoft Way",
                        municipality = "Redmond",
                        countrySubdivision = "WA",
                        postalCode = "98052",
                        country = "United States",
                        countryCode = "US"
                    },
                    position = new { lat = 47.64358, lon = -122.13493 },
                    viewport = new
                    {
                        topLeftPoint = new { lat = 47.64448, lon = -122.13593 },
                        btmRightPoint = new { lat = 47.64268, lon = -122.13393 }
                    },
                    matchType = "Address",
                    entityType = "Address"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new GeocodingRequest
        {
            Query = "1 Microsoft Way, Redmond, WA"
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.Attribution.Should().Be("© Microsoft Azure Maps");

        var result = response.Results.First();
        result.FormattedAddress.Should().Be("1 Microsoft Way, Redmond, WA 98052");
        result.Longitude.Should().BeApproximately(-122.13493, 0.00001);
        result.Latitude.Should().BeApproximately(47.64358, 0.00001);
        result.Confidence.Should().Be(0.95);
        result.Type.Should().Be("Point Address");
        result.Components.Should().NotBeNull();
        result.Components!.Street.Should().Be("Microsoft Way");
        result.Components.City.Should().Be("Redmond");
    }

    [Fact]
    public async Task GeocodeAsync_WithMaxResults_LimitsResults()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new { type = "Address", score = 0.9, address = new { freeformAddress = "Address 1" }, position = new { lat = 47.0, lon = -122.0 } },
                new { type = "Address", score = 0.8, address = new { freeformAddress = "Address 2" }, position = new { lat = 47.1, lon = -122.1 } }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new GeocodingRequest
        {
            Query = "Seattle",
            MaxResults = 2
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GeocodeAsync_WithCountryCode_FiltersResults()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new { type = "Address", score = 0.9, address = new { freeformAddress = "Paris, France", countryCode = "FR" }, position = new { lat = 48.85, lon = 2.35 } }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new GeocodingRequest
        {
            Query = "Paris",
            CountryCodes = new[] { "FR" }
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().HaveCount(1);
        response.Results.First().Components?.CountryCode.Should().Be("FR");
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithValidCoordinates_ReturnsAddress()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new
                {
                    type = "Point Address",
                    score = 0.95,
                    address = new
                    {
                        freeformAddress = "1 Microsoft Way, Redmond, WA 98052",
                        streetNumber = "1",
                        streetName = "Microsoft Way",
                        municipality = "Redmond",
                        countrySubdivision = "WA",
                        postalCode = "98052",
                        country = "United States",
                        countryCode = "US"
                    },
                    position = new { lat = 47.64358, lon = -122.13493 }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new ReverseGeocodingRequest
        {
            Longitude = -122.13493,
            Latitude = 47.64358
        };

        // Act
        var response = await provider.ReverseGeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.Results.First().FormattedAddress.Should().Contain("Microsoft Way");
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithLanguage_ReturnsLocalizedAddress()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new
                {
                    type = "Address",
                    score = 0.9,
                    address = new { freeformAddress = "Tokyo, Japan" },
                    position = new { lat = 35.6762, lon = 139.6503 }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new ReverseGeocodingRequest
        {
            Longitude = 139.6503,
            Latitude = 35.6762,
            Language = "ja"
        };

        // Act
        var response = await provider.ReverseGeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task GeocodeAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, new { error = "Invalid subscription key" });
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new GeocodingRequest { Query = "Test" };

        // Act
        var act = async () => await provider.GeocodeAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Azure Maps geocoding failed*");
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, new { error = "Service unavailable" });
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new ReverseGeocodingRequest { Longitude = 0, Latitude = 0 };

        // Act
        var act = async () => await provider.ReverseGeocodeAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Azure Maps reverse geocoding failed*");
    }

    [Fact]
    public async Task TestConnectivityAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new
        {
            results = new[]
            {
                new
                {
                    type = "Address",
                    score = 0.9,
                    address = new { freeformAddress = "Redmond, WA" },
                    position = new { lat = 47.64358, lon = -122.13493 }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

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
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GeocodeAsync_WithEmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var mockResponse = new { results = Array.Empty<object>() };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new AzureMapsGeocodingProvider(httpClient, _testSubscriptionKey, _mockLogger.Object);

        var request = new GeocodingRequest { Query = "NonexistentPlace12345" };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().BeEmpty();
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
