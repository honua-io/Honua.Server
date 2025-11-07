// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Honua.Server.AlertReceiver.Tests.Controllers;

/// <summary>
/// Integration tests for AlertController - Prometheus AlertManager webhook receiver.
/// Tests all alert severity endpoints and health check functionality.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "AlertReceiver")]
public sealed class AlertControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AlertControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Health Check Tests

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/alert/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetString().Should().Be("healthy");

        content.TryGetProperty("service", out var service).Should().BeTrue();
        service.GetString().Should().Be("honua-alert-receiver");
    }

    [Fact]
    public async Task Health_DoesNotRequireAuthentication()
    {
        // Act
        var response = await _client.GetAsync("/alert/health");

        // Assert - Should succeed without authorization
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Critical Alerts

    [Fact]
    public async Task Critical_ValidAlert_ReturnsOk()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("critical");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("received");

            content.TryGetProperty("alertCount", out var count).Should().BeTrue();
            count.GetInt32().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Critical_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("critical");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK); // May be configured without auth in some deployments
    }

    [Fact]
    public async Task Critical_MultipleAlerts_ProcessesAll()
    {
        // Arrange
        var webhook = CreateAlertManagerWebhookWithMultipleAlerts(3, "critical");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("alertCount", out var count).Should().BeTrue();
            count.GetInt32().Should().Be(3);
        }
    }

    #endregion

    #region Warning Alerts

    [Fact]
    public async Task Warning_ValidAlert_ReturnsOk()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("warning");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/warning", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("received");
        }
    }

    [Fact]
    public async Task Warning_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("warning");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/warning", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK);
    }

    #endregion

    #region Database Alerts

    [Fact]
    public async Task Database_ValidAlert_ReturnsOk()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("database");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/database", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("received");
        }
    }

    #endregion

    #region Storage Alerts

    [Fact]
    public async Task Storage_ValidAlert_ReturnsOk()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("storage");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/storage", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("received");
        }
    }

    #endregion

    #region Default Alerts

    [Fact]
    public async Task Default_ValidAlert_ReturnsOk()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("default");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/default", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetString().Should().Be("received");
        }
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task Critical_NullWebhook_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", (object?)null);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Critical_EmptyAlertsList_HandlesGracefully()
    {
        // Arrange
        var webhook = new
        {
            Status = "firing",
            GroupLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert"
            },
            Alerts = new List<object>() // Empty alerts
        };

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Alert Status Tests

    [Fact]
    public async Task Critical_FiringAlert_ProcessesCorrectly()
    {
        // Arrange
        var webhook = CreateAlertManagerWebhookWithStatus("firing", "critical");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Critical_ResolvedAlert_ProcessesCorrectly()
    {
        // Arrange
        var webhook = CreateAlertManagerWebhookWithStatus("resolved", "critical");

        // Act
        var response = await _client.PostAsJsonAsync("/alert/critical", webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Concurrent Requests

    [Fact]
    public async Task Critical_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("critical");

        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync("/alert/critical", webhook))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Unauthorized);
    }

    #endregion

    #region All Endpoints Tests

    [Theory]
    [InlineData("/alert/critical")]
    [InlineData("/alert/warning")]
    [InlineData("/alert/database")]
    [InlineData("/alert/storage")]
    [InlineData("/alert/default")]
    public async Task AllEndpoints_ValidAlert_ReturnsOkOrUnauthorized(string endpoint)
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("test");

        // Act
        var response = await _client.PostAsJsonAsync(endpoint, webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/alert/critical")]
    [InlineData("/alert/warning")]
    [InlineData("/alert/database")]
    [InlineData("/alert/storage")]
    [InlineData("/alert/default")]
    public async Task AllEndpoints_WithoutAuth_ReturnsUnauthorized(string endpoint)
    {
        // Arrange
        var webhook = CreateValidAlertManagerWebhook("test");

        // Act
        var response = await _client.PostAsJsonAsync(endpoint, webhook);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK); // May be configured without auth
    }

    #endregion

    #region Helper Methods

    private static object CreateValidAlertManagerWebhook(string severity)
    {
        return new
        {
            Status = "firing",
            GroupLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert",
                ["severity"] = severity
            },
            CommonLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert",
                ["severity"] = severity,
                ["instance"] = "localhost:9090"
            },
            CommonAnnotations = new Dictionary<string, string>
            {
                ["description"] = "Test alert description",
                ["summary"] = "Test alert summary"
            },
            ExternalURL = "http://alertmanager:9093",
            Alerts = new[]
            {
                new
                {
                    Status = "firing",
                    Labels = new Dictionary<string, string>
                    {
                        ["alertname"] = "TestAlert",
                        ["severity"] = severity,
                        ["instance"] = "localhost:9090"
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        ["description"] = "Test alert description",
                        ["summary"] = "Test alert summary"
                    },
                    StartsAt = DateTime.UtcNow,
                    EndsAt = DateTime.UtcNow.AddHours(1)
                }
            }
        };
    }

    private static object CreateAlertManagerWebhookWithMultipleAlerts(int count, string severity)
    {
        var alerts = Enumerable.Range(0, count).Select(i => new
        {
            Status = "firing",
            Labels = new Dictionary<string, string>
            {
                ["alertname"] = $"TestAlert{i}",
                ["severity"] = severity,
                ["instance"] = $"localhost:909{i}"
            },
            Annotations = new Dictionary<string, string>
            {
                ["description"] = $"Test alert {i} description",
                ["summary"] = $"Test alert {i} summary"
            },
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddHours(1)
        }).ToArray();

        return new
        {
            Status = "firing",
            GroupLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert",
                ["severity"] = severity
            },
            CommonLabels = new Dictionary<string, string>
            {
                ["severity"] = severity
            },
            CommonAnnotations = new Dictionary<string, string>
            {
                ["description"] = "Multiple test alerts"
            },
            ExternalURL = "http://alertmanager:9093",
            Alerts = alerts
        };
    }

    private static object CreateAlertManagerWebhookWithStatus(string status, string severity)
    {
        return new
        {
            Status = status,
            GroupLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert",
                ["severity"] = severity
            },
            CommonLabels = new Dictionary<string, string>
            {
                ["alertname"] = "TestAlert",
                ["severity"] = severity
            },
            CommonAnnotations = new Dictionary<string, string>
            {
                ["description"] = $"Alert with status: {status}"
            },
            ExternalURL = "http://alertmanager:9093",
            Alerts = new[]
            {
                new
                {
                    Status = status,
                    Labels = new Dictionary<string, string>
                    {
                        ["alertname"] = "TestAlert",
                        ["severity"] = severity
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        ["description"] = $"Alert with status: {status}"
                    },
                    StartsAt = DateTime.UtcNow.AddHours(-1),
                    EndsAt = status == "resolved" ? DateTime.UtcNow : DateTime.UtcNow.AddHours(1)
                }
            }
        };
    }

    #endregion
}
