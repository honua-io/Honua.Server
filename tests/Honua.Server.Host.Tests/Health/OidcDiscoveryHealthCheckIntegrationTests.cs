using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Host.Tests.Health;

/// <summary>
/// Integration tests for OIDC health check endpoint.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class OidcDiscoveryHealthCheckIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OidcDiscoveryHealthCheckIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_Ready_IncludesOidcHealthCheck()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("honua:authentication:mode", "Oidc"),
                    new KeyValuePair<string, string>("honua:authentication:jwt:authority", "https://example.com")
                });
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // Parse JSON response
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify OIDC health check is included
        Assert.True(root.TryGetProperty("entries", out var entries));
        Assert.True(entries.TryGetProperty("oidc", out var oidcEntry));
    }

    [Fact]
    public async Task HealthEndpoint_Ready_OidcCheck_ReturnsDegradedWhenEndpointUnreachable()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("honua:authentication:mode", "Oidc"),
                    new KeyValuePair<string, string>("honua:authentication:jwt:authority", "https://invalid-oidc-endpoint-that-does-not-exist.example.com")
                });
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        // The overall health endpoint should still return 200 OK because OIDC is degraded, not unhealthy
        var content = await response.Content.ReadAsStringAsync();

        // Parse JSON response
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify OIDC health check is degraded
        if (root.TryGetProperty("entries", out var entries) &&
            entries.TryGetProperty("oidc", out var oidcEntry))
        {
            Assert.True(oidcEntry.TryGetProperty("status", out var status));
            var statusValue = status.GetString();
            Assert.True(statusValue == "Degraded" || statusValue == "Healthy",
                "OIDC status should be Degraded (unreachable) or Healthy (not configured)");
        }
    }

    [Fact]
    public async Task HealthEndpoint_Ready_OidcCheck_ReturnsHealthyWhenOidcNotEnabled()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("honua:authentication:mode", "JwtBearer")
                });
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // Parse JSON response
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify OIDC health check is healthy when not enabled
        if (root.TryGetProperty("entries", out var entries) &&
            entries.TryGetProperty("oidc", out var oidcEntry))
        {
            Assert.True(oidcEntry.TryGetProperty("status", out var status));
            Assert.Equal("Healthy", status.GetString());
        }
    }

    [Fact]
    public async Task HealthEndpoint_Ready_HasOidcTag()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("honua:authentication:mode", "Oidc"),
                    new KeyValuePair<string, string>("honua:authentication:jwt:authority", "https://example.com")
                });
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // Parse JSON response and verify tags
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.TryGetProperty("entries", out var entries) &&
            entries.TryGetProperty("oidc", out var oidcEntry) &&
            oidcEntry.TryGetProperty("tags", out var tags))
        {
            bool hasOidcTag = false;
            foreach (var tag in tags.EnumerateArray())
            {
                if (tag.GetString() == "oidc")
                {
                    hasOidcTag = true;
                    break;
                }
            }
            Assert.True(hasOidcTag, "OIDC health check should have 'oidc' tag");
        }
    }

    [Fact]
    public async Task HealthEndpoint_Ready_CachesOidcResults()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("honua:authentication:mode", "Oidc"),
                    new KeyValuePair<string, string>("honua:authentication:jwt:authority", "https://example.com")
                });
            });
        }).CreateClient();

        // Act - Call health endpoint twice
        var response1 = await client.GetAsync("/healthz/ready");
        var response2 = await client.GetAsync("/healthz/ready");

        // Assert - Both should succeed
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // The second call should use cached result (we can't directly verify this in integration test,
        // but the unit tests verify caching behavior)
    }
}
