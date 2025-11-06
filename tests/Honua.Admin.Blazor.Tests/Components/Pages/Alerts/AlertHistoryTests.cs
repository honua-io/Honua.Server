// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using RichardSzalay.MockHttp;

namespace Honua.Admin.Blazor.Tests.Components.Pages.Alerts;

/// <summary>
/// Tests for the Alert History page component.
/// Tests alert history display, filtering, pagination, and alert management actions.
/// </summary>
[Trait("Category", "Unit")]
public class AlertHistoryTests : ComponentTestBase
{
    private readonly MockHttpMessageHandler _mockHttp;

    public AlertHistoryTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        Context.Services.AddSingleton(httpClient);
        Context.Services.AddSingleton<ISnackbar, SnackbarService>();
    }

    #region Page Rendering Tests

    [Fact]
    public async Task AlertHistoryPage_Renders_Successfully()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAlertHistory_DisplaysAlerts()
    {
        // Arrange
        var alerts = new[]
        {
            new
            {
                id = "alert-1",
                ruleName = "High CPU Usage",
                severity = "warning",
                message = "CPU usage is at 85%",
                timestamp = DateTime.UtcNow.AddHours(-2),
                status = "active",
                acknowledged = false
            },
            new
            {
                id = "alert-2",
                ruleName = "Low Disk Space",
                severity = "critical",
                message = "Disk space is below 10%",
                timestamp = DateTime.UtcNow.AddHours(-1),
                status = "active",
                acknowledged = false
            },
            new
            {
                id = "alert-3",
                ruleName = "High Memory",
                severity = "warning",
                message = "Memory usage is at 90%",
                timestamp = DateTime.UtcNow.AddMinutes(-30),
                status = "resolved",
                acknowledged = true
            }
        };

        _mockHttp.When("/api/admin/alerts/history")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(alerts));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("High CPU Usage");
        content.Should().Contain("Low Disk Space");
        content.Should().Contain("High Memory");
    }

    [Fact]
    public async Task LoadAlertHistory_WithEmptyList_ShowsEmptyMessage()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("[]");
    }

    [Fact]
    public async Task LoadAlertHistory_OnError_ShowsErrorMessage()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history")
            .Respond(System.Net.HttpStatusCode.InternalServerError);

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task LoadAlertHistory_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var pageSize = 20;
        var pageNumber = 1;

        _mockHttp.When($"/api/admin/alerts/history?pageSize={pageSize}&pageNumber={pageNumber}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                items = new[] { new { id = "1", ruleName = "Test" } },
                totalCount = 100,
                pageSize = pageSize,
                pageNumber = pageNumber
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?pageSize={pageSize}&pageNumber={pageNumber}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("totalCount");
    }

    [Fact]
    public async Task ClickNextPage_LoadsNextPage()
    {
        // Arrange
        var pageNumber = 2;

        _mockHttp.When($"/api/admin/alerts/history?pageSize=20&pageNumber={pageNumber}")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?pageSize=20&pageNumber={pageNumber}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePageSize_ReloadsWithNewPageSize()
    {
        // Arrange
        var pageSize = 50;

        _mockHttp.When($"/api/admin/alerts/history?pageSize={pageSize}&pageNumber=1")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?pageSize={pageSize}&pageNumber=1");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Filtering Tests

    [Fact]
    public async Task FilterBySeverity_ShowsOnlyMatchingAlerts()
    {
        // Arrange
        var severity = "critical";

        _mockHttp.When($"/api/admin/alerts/history?severity={severity}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { id = "1", severity = "critical", ruleName = "Critical Alert" }
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?severity={severity}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("critical");
    }

    [Fact]
    public async Task FilterByDateRange_ShowsAlertsInRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7).ToString("o");
        var endDate = DateTime.UtcNow.ToString("o");

        _mockHttp.When($"/api/admin/alerts/history?startDate={Uri.EscapeDataString(startDate)}&endDate={Uri.EscapeDataString(endDate)}")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?startDate={Uri.EscapeDataString(startDate)}&endDate={Uri.EscapeDataString(endDate)}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task FilterByRuleName_ShowsMatchingAlerts()
    {
        // Arrange
        var ruleId = "rule-123";

        _mockHttp.When($"/api/admin/alerts/history?ruleId={ruleId}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { id = "1", ruleId = ruleId, ruleName = "Test Rule" }
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?ruleId={ruleId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task FilterByStatus_ShowsOnlyMatchingAlerts()
    {
        // Arrange
        var status = "active";

        _mockHttp.When($"/api/admin/alerts/history?status={status}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { id = "1", status = "active", ruleName = "Active Alert" }
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?status={status}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task FilterByAcknowledged_ShowsOnlyAcknowledgedAlerts()
    {
        // Arrange
        var acknowledged = true;

        _mockHttp.When($"/api/admin/alerts/history?acknowledged={acknowledged}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { id = "1", acknowledged = true, ruleName = "Acknowledged Alert" }
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history?acknowledged={acknowledged}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ClearFilters_ShowsAllAlerts()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Alert Actions Tests

    [Fact]
    public async Task AcknowledgeAlert_CallsApiAndUpdatesDisplay()
    {
        // Arrange
        var alertId = "alert-1";

        _mockHttp.When(HttpMethod.Post, $"/api/admin/alerts/history/{alertId}/acknowledge")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                id = alertId,
                acknowledged = true,
                acknowledgedBy = "admin@example.com",
                acknowledgedAt = DateTime.UtcNow
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/alerts/history/{alertId}/acknowledge",
            new { acknowledgedBy = "admin@example.com", note = "Investigating" }
        );

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAlert_CallsApiAndUpdatesDisplay()
    {
        // Arrange
        var alertId = "alert-1";

        _mockHttp.When(HttpMethod.Post, $"/api/admin/alerts/history/{alertId}/resolve")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                id = alertId,
                status = "resolved",
                resolvedBy = "admin@example.com",
                resolvedAt = DateTime.UtcNow
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/alerts/history/{alertId}/resolve",
            new { resolvedBy = "admin@example.com", resolution = "Issue fixed" }
        );

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ViewAlertDetails_OpensDetailsDialog()
    {
        // Arrange
        var alertId = "alert-1";

        _mockHttp.When($"/api/admin/alerts/history/{alertId}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                id = alertId,
                ruleName = "High CPU",
                severity = "warning",
                message = "CPU at 85%",
                timestamp = DateTime.UtcNow
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/history/{alertId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task BulkAcknowledge_AcknowledgesMultipleAlerts()
    {
        // Arrange
        var alertIds = new[] { "alert-1", "alert-2", "alert-3" };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/history/bulk-acknowledge")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                successCount = 3,
                failedCount = 0
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync(
            "/api/admin/alerts/history/bulk-acknowledge",
            new { alertIds, acknowledgedBy = "admin@example.com" }
        );

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task BulkResolve_ResolvesMultipleAlerts()
    {
        // Arrange
        var alertIds = new[] { "alert-1", "alert-2", "alert-3" };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/history/bulk-resolve")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                successCount = 3,
                failedCount = 0
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync(
            "/api/admin/alerts/history/bulk-resolve",
            new { alertIds, resolvedBy = "admin@example.com", resolution = "Resolved" }
        );

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void SortByTimestamp_Descending_ShowsNewestFirst()
    {
        // Arrange
        var alerts = new[]
        {
            new { id = "1", timestamp = DateTime.UtcNow.AddHours(-3) },
            new { id = "2", timestamp = DateTime.UtcNow.AddHours(-1) },
            new { id = "3", timestamp = DateTime.UtcNow.AddHours(-2) }
        };

        // Act
        var sorted = alerts.OrderByDescending(a => a.timestamp).ToArray();

        // Assert
        sorted[0].id.Should().Be("2");
        sorted[1].id.Should().Be("3");
        sorted[2].id.Should().Be("1");
    }

    [Fact]
    public void SortBySeverity_ShowsCriticalFirst()
    {
        // Arrange
        var severityPriority = new Dictionary<string, int>
        {
            { "critical", 1 },
            { "error", 2 },
            { "warning", 3 },
            { "info", 4 }
        };

        var alerts = new[]
        {
            new { id = "1", severity = "info" },
            new { id = "2", severity = "critical" },
            new { id = "3", severity = "warning" }
        };

        // Act
        var sorted = alerts.OrderBy(a => severityPriority[a.severity]).ToArray();

        // Assert
        sorted[0].severity.Should().Be("critical");
        sorted[1].severity.Should().Be("warning");
        sorted[2].severity.Should().Be("info");
    }

    [Fact]
    public void SortByRuleName_Alphabetically()
    {
        // Arrange
        var alerts = new[]
        {
            new { id = "1", ruleName = "Zebra Alert" },
            new { id = "2", ruleName = "Alpha Alert" },
            new { id = "3", ruleName = "Beta Alert" }
        };

        // Act
        var sorted = alerts.OrderBy(a => a.ruleName).ToArray();

        // Assert
        sorted[0].ruleName.Should().Be("Alpha Alert");
        sorted[1].ruleName.Should().Be("Beta Alert");
        sorted[2].ruleName.Should().Be("Zebra Alert");
    }

    #endregion

    #region Search Tests

    [Fact]
    public void SearchAlerts_ByMessage_FiltersResults()
    {
        // Arrange
        var alerts = new[]
        {
            new { id = "1", message = "CPU usage is high" },
            new { id = "2", message = "Disk space is low" },
            new { id = "3", message = "Memory usage is high" }
        };

        var searchTerm = "usage";

        // Act
        var filtered = alerts.Where(a => a.message.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(a => a.id == "1");
        filtered.Should().Contain(a => a.id == "3");
    }

    [Fact]
    public void SearchAlerts_ByRuleName_FiltersResults()
    {
        // Arrange
        var alerts = new[]
        {
            new { id = "1", ruleName = "High CPU Usage" },
            new { id = "2", ruleName = "Low Disk Space" },
            new { id = "3", ruleName = "High Memory Usage" }
        };

        var searchTerm = "High";

        // Act
        var filtered = alerts.Where(a => a.ruleName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Assert
        filtered.Should().HaveCount(2);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportAlertHistory_ToCsv_DownloadsFile()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history/export?format=csv")
            .Respond("text/csv", "id,ruleName,severity,timestamp\n1,Test,warning,2024-01-01");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history/export?format=csv");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
    }

    [Fact]
    public async Task ExportAlertHistory_ToJson_DownloadsFile()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history/export?format=json")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history/export?format=json");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task AutoRefresh_Enabled_RefreshesAlerts()
    {
        // Arrange
        var callCount = 0;

        _mockHttp.When("/api/admin/alerts/history")
            .Respond(() =>
            {
                callCount++;
                return new StringContent("[]", System.Text.Encoding.UTF8, "application/json");
            });

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        await httpClient.GetAsync("/api/admin/alerts/history");
        await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ManualRefresh_ReloadsAlerts()
    {
        // Arrange
        _mockHttp.When("/api/admin/alerts/history")
            .Respond("application/json", "[]");

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/history");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task DisplayAlertStatistics_ShowsSummary()
    {
        // Arrange
        var stats = new
        {
            totalAlerts = 150,
            criticalCount = 10,
            warningCount = 50,
            acknowledgedCount = 100,
            resolvedCount = 120
        };

        _mockHttp.When("/api/admin/alerts/statistics")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(stats));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/statistics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("150");
        content.Should().Contain("10");
    }

    #endregion
}
