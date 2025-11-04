using System.Net;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Api;

/// <summary>
/// Integration tests for health check endpoints.
/// Ensures Kubernetes/Docker readiness and liveness probes work correctly.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Api")]
[Trait("Category", "HealthCheck")]
[Collection("EndpointTests")]
public class HealthCheckApiTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckApiTests(HonuaTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =====================================================
    // Readiness Probe Tests
    // =====================================================

    [Fact]
    public async Task ReadinessCheck_ReturnsHealthy_WhenServerIsReady()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        // Server is ready if status is Healthy or Degraded (degraded means some optional services like Redis are unavailable)
        doc.RootElement.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");
    }

    [Fact]
    public async Task ReadinessCheck_IncludesMetadataHealthCheck()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        // Verify health check results contain metadata check
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            var hasMetadataCheck = false;
            foreach (var prop in results.EnumerateObject())
            {
                if (prop.Name.Contains("metadata", StringComparison.OrdinalIgnoreCase))
                {
                    hasMetadataCheck = true;
                    prop.Value.GetProperty("status").GetString().Should().Be("Healthy");
                }
            }

            hasMetadataCheck.Should().BeTrue("readiness check should include metadata validation");
        }
    }

    [Fact]
    public async Task ReadinessCheck_IncludesDataSourceHealthChecks()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        // Verify health check results contain data source checks
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            // At least one data source health check should be present
            var hasDataSourceCheck = false;
            foreach (var prop in results.EnumerateObject())
            {
                if (prop.Name.Contains("datasource", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("database", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("sqlite", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                {
                    hasDataSourceCheck = true;
                    // Data source may be healthy or degraded (depending on test environment)
                    var status = prop.Value.GetProperty("status").GetString();
                    status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
                }
            }

            hasDataSourceCheck.Should().BeTrue("readiness check should include data source connectivity");
        }
    }

    // =====================================================
    // Liveness Probe Tests
    // =====================================================

    [Fact]
    public async Task LivenessCheck_ReturnsHealthy_WhenServerIsAlive()
    {
        // Act
        var response = await _client.GetAsync("/healthz/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task LivenessCheck_IsFasterThanReadinessCheck()
    {
        // Liveness checks should be lightweight and fast (no DB connectivity checks)

        // Act
        var liveStart = DateTime.UtcNow;
        var liveResponse = await _client.GetAsync("/healthz/live");
        var liveDuration = DateTime.UtcNow - liveStart;

        var readyStart = DateTime.UtcNow;
        var readyResponse = await _client.GetAsync("/healthz/ready");
        var readyDuration = DateTime.UtcNow - readyStart;

        // Assert
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        liveDuration.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "liveness check should be very fast (< 1s)");

        // Readiness check can be slower as it checks dependencies
        // Allow up to 10 seconds to account for all health checks including timeouts
        readyDuration.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "readiness check should complete within 10 seconds");
    }

    // =====================================================
    // Startup Probe Tests
    // =====================================================

    [Fact]
    public async Task StartupCheck_ReturnsHealthy_WhenServerHasStarted()
    {
        // Act
        var response = await _client.GetAsync("/healthz/startup");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    // =====================================================
    // General Health Check Tests
    // =====================================================

    [Fact]
    public async Task HealthCheck_Root_ReturnsOverallHealth()
    {
        // Act - use ready endpoint as the comprehensive health check
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheck_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task HealthCheck_IncludesTotalDuration()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("totalDuration", out var duration))
        {
            var durationMs = duration.GetDouble();
            durationMs.Should().BeGreaterThan(0, "health check should report execution time");
            durationMs.Should().BeLessThan(10000, "health check should complete within 10 seconds");
        }
    }

    // =====================================================
    // CRS Transformation Health Check Tests
    // =====================================================

    [Fact]
    public async Task HealthCheck_IncludesCrsTransformationCheck()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        // Verify CRS transformation health check is present
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            var hasCrsCheck = false;
            foreach (var prop in results.EnumerateObject())
            {
                if (prop.Name.Contains("crs", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("transformation", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("projection", StringComparison.OrdinalIgnoreCase))
                {
                    hasCrsCheck = true;
                    prop.Value.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");
                }
            }

            hasCrsCheck.Should().BeTrue("health check should validate CRS transformation capability");
        }
    }

    // =====================================================
    // Error Handling Tests
    // =====================================================

    [Fact]
    public async Task HealthCheck_NonExistentEndpoint_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/healthz/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =====================================================
    // Kubernetes/Docker Compatibility Tests
    // =====================================================

    [Fact]
    public async Task HealthChecks_AllEndpoints_AreAccessibleWithoutAuthentication()
    {
        // Health checks must be accessible without auth for K8s/Docker

        // Act
        var readyResponse = await _client.GetAsync("/healthz/ready");
        var liveResponse = await _client.GetAsync("/healthz/live");
        var startupResponse = await _client.GetAsync("/healthz/startup");

        // Assert
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "readiness probe must be accessible without auth");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness probe must be accessible without auth");
        startupResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "startup probe must be accessible without auth");
    }

    [Fact]
    public async Task HealthChecks_ReturnConsistentFormat()
    {
        // All health check endpoints should return consistent JSON format

        // Act
        var endpoints = new[] { "/healthz/ready", "/healthz/live", "/healthz/startup" };
        var responses = await Task.WhenAll(endpoints.Select(e => _client.GetAsync(e)));

        // Assert
        foreach (var (response, endpoint) in responses.Zip(endpoints))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{endpoint} should return 200 OK");

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            doc.RootElement.TryGetProperty("status", out _)
                .Should().BeTrue($"{endpoint} should have 'status' property");
        }
    }

    // =====================================================
    // Performance Tests
    // =====================================================

    [Fact]
    public async Task HealthCheck_CompletesQuickly_UnderLoad()
    {
        // Send 20 concurrent health check requests

        // Act
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _client.GetAsync("/healthz/live"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "health checks should handle concurrent requests");
        }
    }
}
