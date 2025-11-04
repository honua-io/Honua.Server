using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Logging;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Integration tests for CSRF protection middleware.
/// Validates that state-changing operations are protected against CSRF attacks.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Security")]
[Trait("Category", "Integration")]
public sealed class CsrfProtectionTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly Mock<ISecurityAuditLogger> _mockAuditLogger;

    public CsrfProtectionTests()
    {
        _mockAuditLogger = new Mock<ISecurityAuditLogger>();

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        // Add antiforgery services
                        services.AddAntiforgery(options =>
                        {
                            options.HeaderName = "X-CSRF-Token";
                            options.Cookie.Name = "__Host-X-CSRF-Token";
                            options.Cookie.HttpOnly = true;
                            options.Cookie.SameSite = SameSiteMode.Strict;
                            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                        });

                        // Add mock security audit logger
                        services.AddSingleton(_mockAuditLogger.Object);

                        // Add logging
                        services.AddLogging();
                    })
                    .Configure(app =>
                    {
                        // Add CSRF validation middleware
                        app.UseCsrfValidation(new CsrfProtectionOptions
                        {
                            Enabled = true,
                            ExcludedPaths = new[] { "/healthz", "/metrics" }
                        });

                        // Add test endpoints
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // GET endpoint (safe method - no CSRF required)
                            endpoints.MapGet("/api/data", () => Results.Ok(new { message = "GET succeeded" }));

                            // POST endpoint (requires CSRF)
                            endpoints.MapPost("/api/data", () => Results.Ok(new { message = "POST succeeded" }));

                            // PUT endpoint (requires CSRF)
                            endpoints.MapPut("/api/data/{id}", (int id) => Results.Ok(new { message = $"PUT {id} succeeded" }));

                            // DELETE endpoint (requires CSRF)
                            endpoints.MapDelete("/api/data/{id}", (int id) => Results.Ok(new { message = $"DELETE {id} succeeded" }));

                            // PATCH endpoint (requires CSRF)
                            endpoints.MapMethods("/api/data/{id}", new[] { "PATCH" }, (int id) => Results.Ok(new { message = $"PATCH {id} succeeded" }));

                            // Health check endpoint (excluded from CSRF)
                            endpoints.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

                            // CSRF token endpoint
                            endpoints.MapGet("/api/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
                            {
                                var tokens = antiforgery.GetAndStoreTokens(context);
                                return Results.Ok(new { token = tokens.RequestToken });
                            });
                        });
                    });
            })
            .Build();

        _host.Start();
        _client = _host.GetTestClient();
    }

    [Fact]
    public async Task GetRequest_WithoutCsrfToken_Succeeds()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/data");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("GET succeeded");
    }

    [Fact]
    public async Task PostRequest_WithoutCsrfToken_Returns403()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("CSRF Token Validation Failed");

        // Verify audit log was called
        _mockAuditLogger.Verify(
            x => x.LogSecurityEvent(
                "csrf_validation_failure",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task PostRequest_WithValidCsrfToken_Succeeds()
    {
        // Arrange - Get CSRF token
        var tokenResponse = await _client.GetAsync("/api/csrf-token");
        tokenResponse.EnsureSuccessStatusCode();

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        var tokenJson = System.Text.Json.JsonDocument.Parse(tokenContent);
        var csrfToken = tokenJson.RootElement.GetProperty("token").GetString();

        // Extract CSRF cookie from response
        var cookies = tokenResponse.Headers.GetValues("Set-Cookie");

        // Act - Send POST with CSRF token
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Headers.Add("X-CSRF-Token", csrfToken);

        // Add cookie to request
        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("POST succeeded");
    }

    [Fact]
    public async Task PutRequest_WithoutCsrfToken_Returns403()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/data/123");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRequest_WithoutCsrfToken_Returns403()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/data/123");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchRequest_WithoutCsrfToken_Returns403()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/data/123");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRequest_WithApiKey_BypassesCsrfValidation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Headers.Add("X-API-Key", "test-api-key-12345");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("POST succeeded");

        // Verify no audit log for CSRF failure
        _mockAuditLogger.Verify(
            x => x.LogSecurityEvent(
                "csrf_validation_failure",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task PostRequest_WithApiKeyQueryParam_BypassesCsrfValidation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data?api_key=test-key");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheckEndpoint_ExcludedFromCsrfValidation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/healthz");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Should not return 403 (CSRF failure), but might return 405 (Method Not Allowed)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRequest_WithInvalidCsrfToken_Returns403()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Headers.Add("X-CSRF-Token", "invalid-token-12345");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("CSRF Token Validation Failed");
    }

    [Fact]
    public async Task PostRequest_WithExpiredCsrfToken_Returns403()
    {
        // This test simulates an expired token scenario
        // In practice, antiforgery tokens expire after a certain period

        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Use a token from a different session (simulates expired token)
        request.Headers.Add("X-CSRF-Token", "CfDJ8Expired-Token-Simulation");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CsrfTokenEndpoint_GeneratesValidToken()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/csrf-token");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("token");

        // Verify cookie was set
        response.Headers.TryGetValues("Set-Cookie", out var cookies);
        cookies.Should().NotBeNull();
        cookies.Should().Contain(c => c.Contains("__Host-X-CSRF-Token"));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeHttpMethods_DoNotRequireCsrfToken(string method)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), "/api/data");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Safe methods should not return 403 for missing CSRF token
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MultipleRequests_WithSameToken_AllSucceed()
    {
        // Arrange - Get CSRF token
        var tokenResponse = await _client.GetAsync("/api/csrf-token");
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        var tokenJson = System.Text.Json.JsonDocument.Parse(tokenContent);
        var csrfToken = tokenJson.RootElement.GetProperty("token").GetString();
        var cookies = tokenResponse.Headers.GetValues("Set-Cookie");

        // Act - Send multiple POST requests with same token
        var results = new List<HttpResponseMessage>();
        for (int i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            request.Headers.Add("X-CSRF-Token", csrfToken);

            foreach (var cookie in cookies)
            {
                request.Headers.Add("Cookie", cookie.Split(';')[0]);
            }

            var response = await _client.SendAsync(request);
            results.Add(response);
        }

        // Assert - All requests should succeed
        results.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task CsrfProtection_ReturnsProperProblemDetails()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = System.Text.Json.JsonDocument.Parse(content);

        problemDetails.RootElement.GetProperty("title").GetString().Should().Be("CSRF Token Validation Failed");
        problemDetails.RootElement.GetProperty("status").GetInt32().Should().Be(403);
        problemDetails.RootElement.GetProperty("detail").GetString().Should().Contain("CSRF token");
        problemDetails.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _host?.Dispose();
    }
}

/// <summary>
/// Unit tests for CSRF protection configuration and options.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class CsrfProtectionOptionsTests
{
    [Fact]
    public void DefaultOptions_HasExpectedValues()
    {
        // Arrange & Act
        var options = CsrfProtectionOptions.Default;

        // Assert
        options.Enabled.Should().BeTrue();
        options.ExcludedPaths.Should().Contain("/healthz");
        options.ExcludedPaths.Should().Contain("/metrics");
        options.ExcludedPaths.Should().Contain("/swagger");
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        // Arrange & Act
        var options = new CsrfProtectionOptions
        {
            Enabled = false,
            ExcludedPaths = new[] { "/custom-path" }
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.ExcludedPaths.Should().HaveCount(1);
        options.ExcludedPaths.Should().Contain("/custom-path");
    }
}
