using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Certificates.DnsChallenge;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Certificates;

[Trait("Category", "Unit")]
public class CloudflareDnsProviderTests
{
    private readonly Mock<ILogger<CloudflareDnsProvider>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private const string TestApiToken = "test-api-token-12345";
    private const string TestZoneId = "zone-id-12345";
    private const string TestDomain = "example.com";
    private const string TestRecordId = "record-id-12345";

    public CloudflareDnsProviderTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareDnsProvider>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_SetsUpHttpClient()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        // Act
        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        // Assert
        provider.Should().NotBeNull();
        _httpClient.BaseAddress.Should().Be(new Uri("https://api.cloudflare.com/client/v4"));
        _httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        _httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        _httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TestApiToken);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions { ApiToken = TestApiToken };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareDnsProvider(null!, options, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareDnsProvider(_httpClient, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions { ApiToken = TestApiToken };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareDnsProvider(_httpClient, options, null!));
    }

    [Fact]
    public void Constructor_WithEmptyApiToken_ThrowsArgumentException()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions { ApiToken = string.Empty };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object));
    }

    [Fact]
    public async Task DeployChallengeAsync_WithValidRequest_CreatesRecordSuccessfully()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId,
            PropagationWaitSeconds = 1 // Short wait for testing
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        var createRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{TestDomain}",
                content = "test-key-authz",
                ttl = 60
            },
            errors = new object[] { },
            messages = new object[] { }
        };

        var verifyRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{TestDomain}",
                content = "test-key-authz"
            }
        };

        SetupHttpResponse(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(createRecordResponse));

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records/{TestRecordId}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(verifyRecordResponse));

        // Act
        await provider.DeployChallengeAsync(
            TestDomain,
            "test-token",
            "test-key-authz",
            "Dns01",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains($"/zones/{TestZoneId}/dns_records")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeployChallengeAsync_WithHttp01Challenge_ThrowsArgumentException()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.DeployChallengeAsync(
                TestDomain,
                "test-token",
                "test-key-authz",
                "Http01",
                CancellationToken.None));
    }

    [Fact]
    public async Task DeployChallengeAsync_WithoutZoneId_DiscoverZoneAutomatically()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            PropagationWaitSeconds = 1
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        var zoneDiscoveryResponse = new
        {
            success = true,
            result = new[]
            {
                new
                {
                    id = TestZoneId,
                    name = TestDomain,
                    status = "active"
                }
            }
        };

        var createRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{TestDomain}",
                content = "test-key-authz",
                ttl = 60
            },
            errors = new object[] { },
            messages = new object[] { }
        };

        var verifyRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{TestDomain}",
                content = "test-key-authz"
            }
        };

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones?name={TestDomain}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(zoneDiscoveryResponse));

        SetupHttpResponse(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(createRecordResponse));

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records/{TestRecordId}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(verifyRecordResponse));

        // Act
        await provider.DeployChallengeAsync(
            TestDomain,
            "test-token",
            "test-key-authz",
            "Dns01",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"zones?name={TestDomain}")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeployChallengeAsync_WithSubdomain_DiscoverParentZone()
    {
        // Arrange
        var subdomain = $"api.{TestDomain}";
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            PropagationWaitSeconds = 1
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        // First attempt with full subdomain should fail
        var emptyZoneResponse = new
        {
            success = true,
            result = new object[] { }
        };

        // Second attempt with parent domain should succeed
        var zoneDiscoveryResponse = new
        {
            success = true,
            result = new[]
            {
                new
                {
                    id = TestZoneId,
                    name = TestDomain,
                    status = "active"
                }
            }
        };

        var createRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{subdomain}",
                content = "test-key-authz",
                ttl = 60
            },
            errors = new object[] { },
            messages = new object[] { }
        };

        var verifyRecordResponse = new
        {
            success = true,
            result = new
            {
                id = TestRecordId,
                type = "TXT",
                name = $"_acme-challenge.{subdomain}",
                content = "test-key-authz"
            }
        };

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones?name={subdomain}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(emptyZoneResponse));

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones?name={TestDomain}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(zoneDiscoveryResponse));

        SetupHttpResponse(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(createRecordResponse));

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records/{TestRecordId}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(verifyRecordResponse));

        // Act
        await provider.DeployChallengeAsync(
            subdomain,
            "test-token",
            "test-key-authz",
            "Dns01",
            CancellationToken.None);

        // Assert - Verify we tried both subdomain and parent domain
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"zones?name={TestDomain}")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeployChallengeAsync_WithApiError_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        var errorResponse = new
        {
            success = false,
            result = (object?)null,
            errors = new[]
            {
                new { code = 1001, message = "Invalid API token" }
            }
        };

        SetupHttpResponse(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(errorResponse));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.DeployChallengeAsync(
                TestDomain,
                "test-token",
                "test-key-authz",
                "Dns01",
                CancellationToken.None));

        exception.Message.Should().Contain("Invalid API token");
    }

    [Fact]
    public async Task CleanupChallengeAsync_WithValidRequest_DeletesRecordSuccessfully()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        var listRecordsResponse = new
        {
            success = true,
            result = new[]
            {
                new
                {
                    id = TestRecordId,
                    type = "TXT",
                    name = $"_acme-challenge.{TestDomain}",
                    content = "test-key-authz",
                    ttl = 60,
                    proxied = false
                }
            }
        };

        var deleteResponse = new
        {
            success = true,
            result = new { id = TestRecordId }
        };

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records?name=_acme-challenge.{TestDomain}&type=TXT",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(listRecordsResponse));

        SetupHttpResponse(
            HttpMethod.Delete,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records/{TestRecordId}",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(deleteResponse));

        // Act
        await provider.CleanupChallengeAsync(
            TestDomain,
            "test-token",
            "test-key-authz",
            "Dns01",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.ToString().Contains($"/zones/{TestZoneId}/dns_records/{TestRecordId}")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CleanupChallengeAsync_WithHttp01Challenge_DoesNothing()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        // Act
        await provider.CleanupChallengeAsync(
            TestDomain,
            "test-token",
            "test-key-authz",
            "Http01",
            CancellationToken.None);

        // Assert
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CleanupChallengeAsync_WithMissingRecord_LogsWarningAndContinues()
    {
        // Arrange
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = TestApiToken,
            ZoneId = TestZoneId
        };

        var provider = new CloudflareDnsProvider(_httpClient, options, _mockLogger.Object);

        var emptyListResponse = new
        {
            success = true,
            result = new object[] { }
        };

        SetupHttpResponse(
            HttpMethod.Get,
            $"https://api.cloudflare.com/client/v4/zones/{TestZoneId}/dns_records?name=_acme-challenge.{TestDomain}&type=TXT",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(emptyListResponse));

        // Act
        await provider.CleanupChallengeAsync(
            TestDomain,
            "test-token",
            "test-key-authz",
            "Dns01",
            CancellationToken.None);

        // Assert - Should not throw, just log warning
        _mockHttpMessageHandler.Protected()
            .Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void CloudflareDnsProviderOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new CloudflareDnsProviderOptions();

        // Assert
        options.ApiToken.Should().Be(string.Empty);
        options.ZoneId.Should().BeNull();
        options.PropagationWaitSeconds.Should().Be(30);
    }

    [Fact]
    public void CloudflareDnsProviderOptions_CanSetCustomValues()
    {
        // Arrange & Act
        var options = new CloudflareDnsProviderOptions
        {
            ApiToken = "custom-token",
            ZoneId = "custom-zone-id",
            PropagationWaitSeconds = 60
        };

        // Assert
        options.ApiToken.Should().Be("custom-token");
        options.ZoneId.Should().Be("custom-zone-id");
        options.PropagationWaitSeconds.Should().Be(60);
    }

    private void SetupHttpResponse(HttpMethod method, string url, HttpStatusCode statusCode, string content)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri!.ToString().Contains(url.Replace("https://api.cloudflare.com/client/v4", ""))),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
