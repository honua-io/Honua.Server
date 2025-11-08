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
public sealed class NominatimGeocodingProviderTests
{
    private readonly Mock<ILogger<NominatimGeocodingProvider>> _mockLogger;
    private const string TestBaseUrl = "https://nominatim.test.com";
    private const string TestUserAgent = "HonuaServer-Tests/1.0";

    public NominatimGeocodingProviderTests()
    {
        _mockLogger = new Mock<ILogger<NominatimGeocodingProvider>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderKey.Should().Be("nominatim");
        provider.ProviderName.Should().Be("Nominatim (OpenStreetMap)");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new NominatimGeocodingProvider(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesDefaults()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.UserAgent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GeocodeAsync_WithValidAddress_ReturnsResults()
    {
        // Arrange
        var mockResponse = new[]
        {
            new
            {
                place_id = 12345L,
                osm_type = "way",
                osm_id = 67890L,
                lat = "40.7128",
                lon = "-74.0060",
                @class = "place",
                type = "city",
                display_name = "New York, NY, United States",
                importance = 0.85,
                boundingbox = new[] { "40.4774", "40.9176", "-74.2591", "-73.7004" },
                address = new
                {
                    city = "New York",
                    state = "New York",
                    postcode = "10001",
                    country = "United States",
                    country_code = "us"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest
        {
            Query = "New York, NY"
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.Attribution.Should().Be("© OpenStreetMap contributors");

        var result = response.Results.First();
        result.FormattedAddress.Should().Be("New York, NY, United States");
        result.Longitude.Should().BeApproximately(-74.0060, 0.00001);
        result.Latitude.Should().BeApproximately(40.7128, 0.00001);
        result.Confidence.Should().Be(0.85);
        result.Type.Should().Be("city");
        result.Components.Should().NotBeNull();
        result.Components!.City.Should().Be("New York");
        result.Components.State.Should().Be("New York");
        result.Metadata.Should().ContainKey("placeId");
    }

    [Fact]
    public async Task GeocodeAsync_WithMaxResults_LimitsResults()
    {
        // Arrange
        var mockResponse = new[]
        {
            new { lat = "40.7128", lon = "-74.0060", display_name = "NYC 1", importance = 0.9 },
            new { lat = "40.7129", lon = "-74.0061", display_name = "NYC 2", importance = 0.8 }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest
        {
            Query = "New York",
            MaxResults = 2
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GeocodeAsync_WithCountryCodes_FiltersResults()
    {
        // Arrange
        var mockResponse = new[]
        {
            new
            {
                lat = "51.5074",
                lon = "-0.1278",
                display_name = "London, UK",
                importance = 0.9,
                address = new { country_code = "gb" }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest
        {
            Query = "London",
            CountryCodes = new[] { "gb" }
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().HaveCount(1);
        response.Results.First().Components?.CountryCode.Should().Be("gb");
    }

    [Fact]
    public async Task GeocodeAsync_WithBoundingBox_ConstrainsResults()
    {
        // Arrange
        var mockResponse = new[]
        {
            new
            {
                lat = "47.6062",
                lon = "-122.3321",
                display_name = "Seattle, WA",
                importance = 0.85
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest
        {
            Query = "Seattle",
            BoundingBox = new[] { -123.0, 47.0, -122.0, 48.0 }
        };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithValidCoordinates_ReturnsAddress()
    {
        // Arrange
        var mockResponse = new
        {
            place_id = 12345L,
            lat = "40.7128",
            lon = "-74.0060",
            display_name = "New York City Hall, New York, NY 10007",
            address = new
            {
                house_number = "1",
                road = "City Hall Park",
                city = "New York",
                state = "New York",
                postcode = "10007",
                country = "United States",
                country_code = "us"
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new ReverseGeocodingRequest
        {
            Longitude = -74.0060,
            Latitude = 40.7128
        };

        // Act
        var response = await provider.ReverseGeocodeAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.Results.First().FormattedAddress.Should().Contain("New York");
        response.Results.First().Components?.Street.Should().Be("City Hall Park");
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithLanguage_ReturnsLocalizedAddress()
    {
        // Arrange
        var mockResponse = new
        {
            lat = "35.6762",
            lon = "139.6503",
            display_name = "東京, 日本",
            address = new { city = "東京", country = "日本", country_code = "jp" }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

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
        var httpClient = CreateMockHttpClient(HttpStatusCode.TooManyRequests, new { error = "Rate limit exceeded" });
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest { Query = "Test" };

        // Act
        var act = async () => await provider.GeocodeAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nominatim geocoding failed*");
    }

    [Fact]
    public async Task ReverseGeocodeAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, new { error = "Invalid coordinates" });
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new ReverseGeocodingRequest { Longitude = 0, Latitude = 0 };

        // Act
        var act = async () => await provider.ReverseGeocodeAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nominatim reverse geocoding failed*");
    }

    [Fact]
    public async Task TestConnectivityAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new[]
        {
            new
            {
                lat = "52.5200",
                lon = "13.4050",
                display_name = "OpenStreetMap Foundation",
                importance = 0.9
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

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
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        // Act
        var result = await provider.TestConnectivityAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GeocodeAsync_WithEmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var mockResponse = Array.Empty<object>();

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest { Query = "NonexistentPlace12345" };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Seattle", 47.6062, -122.3321)]
    [InlineData("Paris", 48.8566, 2.3522)]
    [InlineData("Tokyo", 35.6762, 139.6503)]
    public async Task GeocodeAsync_WithVariousCities_ReturnsExpectedCoordinates(
        string city, double expectedLat, double expectedLon)
    {
        // Arrange
        var mockResponse = new[]
        {
            new
            {
                lat = expectedLat.ToString(),
                lon = expectedLon.ToString(),
                display_name = city,
                importance = 0.9
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var provider = new NominatimGeocodingProvider(httpClient, _mockLogger.Object, TestBaseUrl, TestUserAgent);

        var request = new GeocodingRequest { Query = city };

        // Act
        var response = await provider.GeocodeAsync(request);

        // Assert
        response.Results.First().Latitude.Should().BeApproximately(expectedLat, 0.01);
        response.Results.First().Longitude.Should().BeApproximately(expectedLon, 0.01);
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
