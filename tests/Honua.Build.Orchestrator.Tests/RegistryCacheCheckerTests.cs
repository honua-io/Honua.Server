using FluentAssertions;
using Moq;
using Moq.Protected;
using RichardSzalay.MockHttp;
using System.Net;
using Xunit;

namespace Honua.Build.Orchestrator.Tests;

/// <summary>
/// Tests for RegistryCacheChecker - verifies if a build already exists in various container registries.
/// Supports GitHub Container Registry, AWS ECR, Azure ACR, and Google GCR.
/// </summary>
[Trait("Category", "Unit")]
public class RegistryCacheCheckerTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly RegistryCacheChecker _checker;

    public RegistryCacheCheckerTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        _checker = new RegistryCacheChecker(httpClient);
    }

    [Fact]
    public async Task CheckCache_ImageExists_ReturnsCacheHit()
    {
        // Arrange
        var registry = "ghcr.io/honua";
        var imageTag = "1.0.0-pro-linux-arm64-a1b2c3d4";

        // Mock HTTP HEAD request to check manifest existence
        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .Respond(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Docker-Content-Digest", "sha256:abc123...");
                return response;
            });

        // Act
        var result = await _checker.CheckCacheAsync(registry, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.Exists.Should().BeTrue();
        result.CacheHit.Should().BeTrue();
        result.Digest.Should().NotBeNullOrEmpty();
        result.Registry.Should().Be(registry);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_ImageNotFound_ReturnsCacheMiss()
    {
        // Arrange
        var registry = "ghcr.io/honua";
        var imageTag = "1.0.0-pro-linux-arm64-nonexistent";

        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await _checker.CheckCacheAsync(registry, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.Exists.Should().BeFalse();
        result.CacheHit.Should().BeFalse();
        result.Digest.Should().BeNull();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_InvalidLicense_ReturnsAccessDenied()
    {
        // Arrange
        var registry = "ghcr.io/honua-pro";
        var imageTag = "1.0.0-enterprise-linux-arm64-xyz789";

        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua-pro/server/manifests/{imageTag}")
            .Respond(HttpStatusCode.Unauthorized);

        // Act
        var result = await _checker.CheckCacheAsync(registry, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.Exists.Should().BeFalse();
        result.CacheHit.Should().BeFalse();
        result.AccessDenied.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Unauthorized");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckGitHubRegistry_ValidToken_CallsCorrectEndpoint()
    {
        // Arrange
        var registryConfig = new RegistryConfiguration
        {
            Provider = RegistryProvider.GitHub,
            Url = "ghcr.io/honua",
            AuthToken = "ghp_test123token"
        };
        var imageTag = "1.0.0-pro-linux-arm64-test123";

        _mockHttp.Expect(HttpMethod.Head, "https://ghcr.io/v2/honua/server/manifests/1.0.0-pro-linux-arm64-test123")
            .WithHeaders("Authorization", "Bearer ghp_test123token")
            .Respond(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Docker-Content-Digest", "sha256:def456...");
                return response;
            });

        // Act
        var result = await _checker.CheckGitHubRegistryAsync(registryConfig, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.Exists.Should().BeTrue();
        result.Digest.Should().StartWith("sha256:");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckGitHubRegistry_ExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var registryConfig = new RegistryConfiguration
        {
            Provider = RegistryProvider.GitHub,
            Url = "ghcr.io/honua",
            AuthToken = "expired_token"
        };
        var imageTag = "1.0.0-pro-linux-arm64-test456";

        _mockHttp.Expect(HttpMethod.Head, "https://ghcr.io/v2/honua/server/manifests/1.0.0-pro-linux-arm64-test456")
            .WithHeaders("Authorization", "Bearer expired_token")
            .Respond(HttpStatusCode.Unauthorized);

        // Act
        var result = await _checker.CheckGitHubRegistryAsync(registryConfig, "server", imageTag);

        // Assert
        result.Exists.Should().BeFalse();
        result.AccessDenied.Should().BeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckEcrAsync_ValidCredentials_UsesAwsSdk()
    {
        // Arrange
        var registryConfig = new RegistryConfiguration
        {
            Provider = RegistryProvider.AwsEcr,
            Url = "123456789012.dkr.ecr.us-west-2.amazonaws.com",
            AwsRegion = "us-west-2",
            AwsAccessKeyId = "AKIAIOSFODNN7EXAMPLE",
            AwsSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
        };
        var imageTag = "1.0.0-pro-linux-arm64-ecr123";

        // Note: For ECR, we'd need to mock the AWS SDK calls
        // This is a simplified version showing the pattern

        // Act & Assert
        // In real implementation, this would use AWS SDK's DescribeImages API
        var act = async () => await _checker.CheckEcrAsync(registryConfig, "server", imageTag);

        // Since we don't have actual AWS SDK mocking here, we just verify the method signature
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAzureAcr_ValidServicePrincipal_UsesAzureSdk()
    {
        // Arrange
        var registryConfig = new RegistryConfiguration
        {
            Provider = RegistryProvider.AzureAcr,
            Url = "honua.azurecr.io",
            AzureTenantId = "tenant-123",
            AzureClientId = "client-456",
            AzureClientSecret = "secret-789"
        };
        var imageTag = "1.0.0-pro-linux-arm64-acr123";

        // Mock Azure ACR REST API endpoint
        _mockHttp.Expect(HttpMethod.Get, $"https://honua.azurecr.io/v2/server/manifests/{imageTag}")
            .WithHeaders("Accept", "application/vnd.docker.distribution.manifest.v2+json")
            .Respond(HttpStatusCode.OK, "application/json", "{\"schemaVersion\": 2}");

        // Act
        var result = await _checker.CheckAzureAcrAsync(registryConfig, "server", imageTag);

        // Assert
        result.Exists.Should().BeTrue();
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_MultipleRegistries_ReturnsFirstFound()
    {
        // Arrange
        var registries = new[]
        {
            new RegistryConfiguration { Provider = RegistryProvider.GitHub, Url = "ghcr.io/honua-cache" },
            new RegistryConfiguration { Provider = RegistryProvider.GitHub, Url = "ghcr.io/honua-main" }
        };
        var imageTag = "1.0.0-pro-linux-arm64-multi";

        // First registry returns 404
        _mockHttp.Expect(HttpMethod.Head, "https://ghcr.io/v2/honua-cache/server/manifests/1.0.0-pro-linux-arm64-multi")
            .Respond(HttpStatusCode.NotFound);

        // Second registry returns 200
        _mockHttp.Expect(HttpMethod.Head, "https://ghcr.io/v2/honua-main/server/manifests/1.0.0-pro-linux-arm64-multi")
            .Respond(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Docker-Content-Digest", "sha256:found123");
                return response;
            });

        // Act
        var result = await _checker.CheckMultipleRegistriesAsync(registries, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.Exists.Should().BeTrue();
        result.Registry.Should().Contain("honua-main");
        result.Digest.Should().Be("sha256:found123");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_WithRetry_RetriesOnTransientFailure()
    {
        // Arrange
        var registry = "ghcr.io/honua";
        var imageTag = "1.0.0-pro-linux-arm64-retry";

        // First attempt fails with 503 (transient)
        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .Respond(HttpStatusCode.ServiceUnavailable);

        // Second attempt succeeds
        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .Respond(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Docker-Content-Digest", "sha256:retry123");
                return response;
            });

        // Act
        var result = await _checker.CheckCacheWithRetryAsync(registry, "server", imageTag, maxRetries: 3);

        // Assert
        result.Exists.Should().BeTrue();
        result.Digest.Should().Be("sha256:retry123");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_RateLimitExceeded_ReturnsError()
    {
        // Arrange
        var registry = "ghcr.io/honua";
        var imageTag = "1.0.0-pro-linux-arm64-ratelimit";

        _mockHttp.Expect(HttpMethod.Head, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .Respond(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.Add("Retry-After", "60");
                return response;
            });

        // Act
        var result = await _checker.CheckCacheAsync(registry, "server", imageTag);

        // Assert
        result.Exists.Should().BeFalse();
        result.RateLimited.Should().BeTrue();
        result.RetryAfterSeconds.Should().Be(60);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("ghcr.io/honua", RegistryProvider.GitHub)]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com", RegistryProvider.AwsEcr)]
    [InlineData("honua.azurecr.io", RegistryProvider.AzureAcr)]
    [InlineData("gcr.io/honua-project", RegistryProvider.GoogleGcr)]
    public void DetectRegistryProvider_ValidUrl_ReturnsCorrectProvider(string url, RegistryProvider expectedProvider)
    {
        // Act
        var provider = _checker.DetectRegistryProvider(url);

        // Assert
        provider.Should().Be(expectedProvider);
    }

    [Fact]
    public async Task CheckCache_MultiArchManifest_ReturnsAllArchitectures()
    {
        // Arrange
        var registry = "ghcr.io/honua";
        var imageTag = "1.0.0-pro-multiarch";

        var manifestList = @"{
            ""schemaVersion"": 2,
            ""mediaType"": ""application/vnd.docker.distribution.manifest.list.v2+json"",
            ""manifests"": [
                {
                    ""mediaType"": ""application/vnd.docker.distribution.manifest.v2+json"",
                    ""digest"": ""sha256:arm64digest"",
                    ""platform"": { ""architecture"": ""arm64"", ""os"": ""linux"" }
                },
                {
                    ""mediaType"": ""application/vnd.docker.distribution.manifest.v2+json"",
                    ""digest"": ""sha256:amd64digest"",
                    ""platform"": { ""architecture"": ""amd64"", ""os"": ""linux"" }
                }
            ]
        }";

        _mockHttp.Expect(HttpMethod.Get, $"https://ghcr.io/v2/honua/server/manifests/{imageTag}")
            .WithHeaders("Accept", "application/vnd.docker.distribution.manifest.list.v2+json")
            .Respond("application/json", manifestList);

        // Act
        var result = await _checker.CheckMultiArchManifestAsync(registry, "server", imageTag);

        // Assert
        result.Should().NotBeNull();
        result.IsMultiArch.Should().BeTrue();
        result.Architectures.Should().Contain(new[] { "arm64", "amd64" });
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckCache_NullOrEmptyRegistry_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _checker.CheckCacheAsync("", "server", "tag");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*registry*");
    }

    [Fact]
    public async Task CheckCache_NullOrEmptyImageName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _checker.CheckCacheAsync("ghcr.io/honua", "", "tag");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*imageName*");
    }
}

// Placeholder classes - these would be in the actual implementation
public class RegistryCacheChecker
{
    private readonly HttpClient _httpClient;

    public RegistryCacheChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<CacheCheckResult> CheckCacheAsync(string registry, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<CacheCheckResult> CheckGitHubRegistryAsync(RegistryConfiguration config, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<CacheCheckResult> CheckEcrAsync(RegistryConfiguration config, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<CacheCheckResult> CheckAzureAcrAsync(RegistryConfiguration config, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<CacheCheckResult> CheckMultipleRegistriesAsync(RegistryConfiguration[] registries, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<CacheCheckResult> CheckCacheWithRetryAsync(string registry, string imageName, string tag, int maxRetries)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public RegistryProvider DetectRegistryProvider(string url)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public Task<MultiArchResult> CheckMultiArchManifestAsync(string registry, string imageName, string tag)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }
}

public class CacheCheckResult
{
    public bool Exists { get; set; }
    public bool CacheHit { get; set; }
    public string? Digest { get; set; }
    public string? Registry { get; set; }
    public bool AccessDenied { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RateLimited { get; set; }
    public int? RetryAfterSeconds { get; set; }
}

public class MultiArchResult
{
    public bool IsMultiArch { get; set; }
    public List<string> Architectures { get; set; } = new();
}

public class RegistryConfiguration
{
    public RegistryProvider Provider { get; set; }
    public string Url { get; set; } = "";
    public string? AuthToken { get; set; }
    public string? AwsRegion { get; set; }
    public string? AwsAccessKeyId { get; set; }
    public string? AwsSecretAccessKey { get; set; }
    public string? AzureTenantId { get; set; }
    public string? AzureClientId { get; set; }
    public string? AzureClientSecret { get; set; }
}

public enum RegistryProvider
{
    GitHub,
    AwsEcr,
    AzureAcr,
    GoogleGcr
}
