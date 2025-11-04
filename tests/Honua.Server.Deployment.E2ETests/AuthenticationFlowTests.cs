using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for authentication flows (QuickStart and Local/JWT).
/// </summary>
[Trait("Category", "Integration")]
public class AuthenticationFlowTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public AuthenticationFlowTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QuickStartMode_ShouldAllowUnauthenticatedAccess()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Access without authentication
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
    }

    [Fact]
    public async Task QuickStartMode_HealthChecks_ShouldBeAccessibleWithoutAuth()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act & Assert - All health endpoints should be accessible
        var startupResponse = await client.GetAsync("/healthz/startup");
        startupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var liveResponse = await client.GetAsync("/healthz/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var readyResponse = await client.GetAsync("/healthz/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LocalAuthMode_ShouldRequireAuthentication()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Try to access without authentication
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LocalAuthMode_Login_ShouldIssueJWTToken()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Login
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "TestAdmin123!"
        });

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();

        tokenResponse!.RootElement.TryGetProperty("token", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrEmpty();

        tokenResponse.RootElement.TryGetProperty("tokenType", out var tokenType).Should().BeTrue();
        tokenType.GetString().Should().Be("Bearer");

        tokenResponse.RootElement.TryGetProperty("expiresIn", out var expiresIn).Should().BeTrue();
        expiresIn.GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LocalAuthMode_WithValidToken_ShouldAccessProtectedResources()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
    }

    [Fact]
    public async Task LocalAuthMode_WithInvalidCredentials_ShouldRejectLogin()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Login with invalid password
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "WrongPassword123!"
        });

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LocalAuthMode_WithInvalidToken_ShouldRejectAccess()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LocalAuthMode_TokenExpiration_ShouldIndicateExpiry()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Login and check token expiration
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "TestAdmin123!"
        });

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var expiresIn = tokenResponse!.RootElement.GetProperty("expiresIn").GetInt32();

        // Assert - Token should have a reasonable expiration (e.g., 1 hour)
        expiresIn.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(86400); // Max 24 hours
    }

    [Fact]
    public async Task LocalAuthMode_HealthChecks_ShouldBeAccessibleWithoutAuth()
    {
        // Arrange - Health checks should always be accessible even in auth mode
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act & Assert - Health endpoints should not require authentication
        var liveResponse = await client.GetAsync("/healthz/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var readyResponse = await client.GetAsync("/healthz/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LocalAuthMode_AdminBootstrap_ShouldCreateDefaultAdminUser()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Try to login with bootstrapped admin credentials
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "TestAdmin123!"
        });

        // Assert - Should succeed because admin user was bootstrapped
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
