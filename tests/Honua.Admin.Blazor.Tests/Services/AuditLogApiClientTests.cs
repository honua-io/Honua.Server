// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for AuditLogApiClient.
/// </summary>
public class AuditLogApiClientTests
{
    [Fact]
    public async Task QueryAsync_Success_ReturnsAuditEvents()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            SearchText = "login",
            Category = "authentication",
            PageNumber = 1,
            PageSize = 25
        };

        var expectedResult = new AuditLogResult
        {
            Events = new List<AuditEvent>
            {
                new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Category = "authentication",
                    Action = "login",
                    UserId = "admin",
                    UserName = "Administrator",
                    ResourceType = "user",
                    ResourceId = "admin",
                    Status = "success",
                    IpAddress = "192.168.1.100",
                    UserAgent = "Mozilla/5.0",
                    RiskScore = 0
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 25
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/api/admin/audit/query", expectedResult);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.QueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Events[0].Action.Should().Be("login");
        result.Events[0].Status.Should().Be("success");
    }

    [Fact]
    public async Task GetByIdAsync_Success_ReturnsAuditEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var expectedEvent = new AuditEvent
        {
            Id = eventId,
            Timestamp = DateTimeOffset.UtcNow,
            Category = "data_modification",
            Action = "update",
            UserId = "admin",
            UserName = "Administrator",
            ResourceType = "layer",
            ResourceId = "test-layer",
            Status = "success",
            Description = "Updated layer properties",
            IpAddress = "192.168.1.100",
            Changes = new AuditChanges
            {
                Before = new Dictionary<string, object> { { "name", "Old Name" } },
                After = new Dictionary<string, object> { { "name", "New Name" } }
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson($"/api/admin/audit/{eventId}", expectedEvent);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.GetByIdAsync(eventId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(eventId);
        result.Action.Should().Be("update");
        result.ResourceType.Should().Be("layer");
        result.Changes.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_Success_ReturnsStatistics()
    {
        // Arrange
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        var expectedStats = new AuditLogStatistics
        {
            TotalEvents = 10000,
            SuccessfulEvents = 9500,
            FailedEvents = 500,
            UniqueUsers = 25,
            HighRiskEvents = 10,
            EventsByCategory = new Dictionary<string, int>
            {
                { "authentication", 3000 },
                { "data_modification", 5000 },
                { "admin_action", 2000 }
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson($"/api/admin/audit/statistics?startDate={startDate:O}&endDate={endDate:O}", expectedStats);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.GetStatisticsAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalEvents.Should().Be(10000);
        result.SuccessfulEvents.Should().Be(9500);
        result.UniqueUsers.Should().Be(25);
        result.EventsByCategory.Should().ContainKey("authentication");
    }

    [Fact]
    public async Task ExportToCsvAsync_Success_ReturnsBase64Csv()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            Category = "authentication",
            PageNumber = 1,
            PageSize = 1000
        };

        var expectedCsv = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("id,timestamp,action\n1,2024-01-01,login"));

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/api/admin/audit/export/csv", expectedCsv);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.ExportToCsvAsync(query);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(expectedCsv);
    }

    [Fact]
    public async Task ExportToJsonAsync_Success_ReturnsBase64Json()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            Category = "data_modification",
            PageNumber = 1,
            PageSize = 1000
        };

        var expectedJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"1\",\"action\":\"update\"}]"));

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/api/admin/audit/export/json", expectedJson);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.ExportToJsonAsync(query);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(expectedJson);
    }

    [Fact]
    public async Task ArchiveEventsAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var olderThan = DateTimeOffset.UtcNow.AddMonths(-6);

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson($"/api/admin/audit/archive?olderThan={olderThan:O}", new { archived = 5000 });

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var act = async () => await apiClient.ArchiveEventsAsync(olderThan);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task QueryAsync_WithFilters_SendsCorrectRequest()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            SearchText = "test",
            Category = "data_modification",
            Action = "update",
            ResourceType = "layer",
            UserId = "admin",
            Status = "success",
            StartDate = DateTimeOffset.UtcNow.AddDays(-7),
            EndDate = DateTimeOffset.UtcNow,
            MinRiskScore = 5,
            PageNumber = 2,
            PageSize = 50
        };

        var expectedResult = new AuditLogResult
        {
            Events = new List<AuditEvent>(),
            TotalCount = 0,
            PageNumber = 2,
            PageSize = 50
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/api/admin/audit/query", expectedResult);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var result = await apiClient.QueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task QueryAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var query = new AuditLogQuery();

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/api/admin/audit/query", HttpStatusCode.InternalServerError);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var act = async () => await apiClient.QueryAsync(query);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, $"/api/admin/audit/{eventId}", HttpStatusCode.NotFound);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var act = async () => await apiClient.GetByIdAsync(eventId);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ExportToCsvAsync_TooManyRecords_ThrowsHttpRequestException()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            PageSize = 100000 // Too many records
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/api/admin/audit/export/csv",
                HttpStatusCode.BadRequest, "Export limit exceeded");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new AuditLogApiClient(httpClient);

        // Act
        var act = async () => await apiClient.ExportToCsvAsync(query);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
