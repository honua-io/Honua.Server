using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Host.Health;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Host.Tests.Health;

/// <summary>
/// Comprehensive unit tests for OidcDiscoveryHealthCheck.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class OidcDiscoveryHealthCheckTests : IDisposable
{
    private readonly Mock<IOptionsMonitor<HonuaAuthenticationOptions>> _mockAuthOptions;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<OidcDiscoveryHealthCheck>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly OidcDiscoveryHealthCheck _healthCheck;
    private readonly HonuaAuthenticationOptions _authOptions;

    public OidcDiscoveryHealthCheckTests()
    {
        _mockAuthOptions = new Mock<IOptionsMonitor<HonuaAuthenticationOptions>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<OidcDiscoveryHealthCheck>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new JwtAuthenticationOptions
            {
                Authority = "https://auth.example.com"
            }
        };

        _mockAuthOptions.Setup(x => x.CurrentValue).Returns(_authOptions);

        _healthCheck = new OidcDiscoveryHealthCheck(
            _mockAuthOptions.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _memoryCache);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOidcModeNotEnabled_ReturnsHealthy()
    {
        // Arrange
        _authOptions.Mode = HonuaAuthenticationOptions.AuthenticationMode.JwtBearer;

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("OIDC mode not enabled", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAuthorityNotConfigured_ReturnsDegraded()
    {
        // Arrange
        _authOptions.Jwt.Authority = null;

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("OIDC authority not configured", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDiscoveryEndpointAccessible_ReturnsHealthy()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"issuer\":\"https://auth.example.com\"}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("accessible", result.Description);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("authority"));
        Assert.True(result.Data.ContainsKey("discovery_url"));
        Assert.True(result.Data.ContainsKey("cached"));
        Assert.False((bool)result.Data["cached"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDiscoveryEndpointReturnsNonSuccess_ReturnsDegraded()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("404", result.Description);
        Assert.True(result.Data.ContainsKey("status_code"));
        Assert.Equal(404, result.Data["status_code"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDiscoveryEndpointUnreachable_ReturnsDegraded()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("unreachable", result.Description);
        Assert.True(result.Data.ContainsKey("error"));
        Assert.Contains("Connection refused", result.Data["error"].ToString());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRequestTimesOut_ReturnsDegraded()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The operation was canceled."));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("timeout", result.Description);
        Assert.True(result.Data.ContainsKey("timeout_seconds"));
        Assert.Equal(5, result.Data["timeout_seconds"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnexpectedErrorOccurs_ReturnsDegraded()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Unexpected error", result.Description);
        Assert.True(result.Data.ContainsKey("error"));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCalledTwice_UsesCachedResultOnSecondCall()
    {
        // Arrange
        var callCount = 0;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"issuer\":\"https://auth.example.com\"}")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result1 = await _healthCheck.CheckHealthAsync(new HealthCheckContext());
        var result2 = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(1, callCount);
        Assert.Equal(HealthStatus.Healthy, result1.Status);
        Assert.Equal(HealthStatus.Healthy, result2.Status);
        Assert.False((bool)result1.Data["cached"]);
    }

    [Fact]
    public async Task CheckHealthAsync_CachesDegradedResultsForShorterDuration()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);

        // Verify the result is in cache
        var cacheKey = "OidcDiscoveryHealthCheck_https://auth.example.com";
        var cachedResult = _memoryCache.Get<HealthCheckResult>(cacheKey);
        Assert.NotNull(cachedResult);
    }

    [Fact]
    public async Task CheckHealthAsync_DoesNotCacheUnexpectedErrors()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);

        // Verify the result is NOT in cache
        var cacheKey = "OidcDiscoveryHealthCheck_https://auth.example.com";
        var cachedResult = _memoryCache.Get<HealthCheckResult>(cacheKey);
        Assert.Null(cachedResult);
    }

    [Fact]
    public async Task CheckHealthAsync_BuildsCorrectDiscoveryUrl()
    {
        // Arrange
        HttpRequestMessage capturedRequest = null;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                capturedRequest = request;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"issuer\":\"https://auth.example.com\"}")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://auth.example.com/.well-known/openid-configuration", capturedRequest.RequestUri.ToString());
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenAuthOptionsIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OidcDiscoveryHealthCheck(
            null,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _memoryCache));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientFactoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OidcDiscoveryHealthCheck(
            _mockAuthOptions.Object,
            null,
            _mockLogger.Object,
            _memoryCache));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OidcDiscoveryHealthCheck(
            _mockAuthOptions.Object,
            _mockHttpClientFactory.Object,
            null,
            _memoryCache));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCacheIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OidcDiscoveryHealthCheck(
            _mockAuthOptions.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            null));
    }
}
