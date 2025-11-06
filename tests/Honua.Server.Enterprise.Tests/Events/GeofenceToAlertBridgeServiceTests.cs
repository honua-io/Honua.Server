// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

public class GeofenceToAlertBridgeServiceTests
{
    private readonly Mock<IGeofenceAlertRepository> _mockRepository;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly GeofenceToAlertBridgeService _service;
    private readonly GeometryFactory _geometryFactory;
    private const string AlertReceiverBaseUrl = "http://localhost:5001";

    public GeofenceToAlertBridgeServiceTests()
    {
        _mockRepository = new Mock<IGeofenceAlertRepository>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Setup HTTP client factory
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(AlertReceiverBaseUrl)
        };
        _mockHttpClientFactory.Setup(f => f.CreateClient("AlertReceiver")).Returns(httpClient);

        _service = new GeofenceToAlertBridgeService(
            _mockRepository.Object,
            _mockHttpClientFactory.Object,
            NullLogger<GeofenceToAlertBridgeService>.Instance,
            AlertReceiverBaseUrl);
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_WithSilencingRule_ShouldNotGenerateAlert()
    {
        // Arrange
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "entity-1",
            "Restricted Zone A",
            Guid.NewGuid());

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);

        // Assert
        _mockRepository.Verify(r => r.FindMatchingRulesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c => c.WasSilenced == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_NoMatchingRules_ShouldNotGenerateAlert()
    {
        // Arrange
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "entity-1",
            "Test Zone",
            Guid.NewGuid());

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule>());

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);

        // Assert
        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.IsAny<GeofenceAlertCorrelation>(), It.IsAny<CancellationToken>()), Times.Never);

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_WithMatchingRule_ShouldGenerateAlert()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "vehicle-123",
            "Restricted Zone A",
            geofenceId);

        var alertRule = new GeofenceAlertRule
        {
            Id = ruleId,
            Name = "Restricted Entry Alert",
            AlertSeverity = "critical",
            AlertNameTemplate = "{entity_id} entered {geofence_name}",
            AlertDescriptionTemplate = "Entity {entity_id} entered restricted geofence {geofence_name} at {event_time}",
            NotificationChannelIds = new List<long> { 1, 2 },
            DeduplicationWindowMinutes = 60
        };

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                geofenceId, "Restricted Zone A", "vehicle-123", null, "enter", null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { alertRule });

        // Setup HTTP response
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"id\": 12345}")
            });

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);

        // Allow async processing to complete
        await Task.Delay(100);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/api/alerts")),
            ItExpr.IsAny<CancellationToken>());

        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c =>
                c.GeofenceEventId == geofenceEvent.Id &&
                c.AlertSeverity == "critical" &&
                c.WasSilenced == false &&
                c.NotificationChannelIds != null &&
                c.NotificationChannelIds.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_TemplateSubstitution_ShouldReplaceAllPlaceholders()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Exit,
            "truck-456",
            "Loading Zone 1",
            geofenceId,
            entityType: "delivery-truck",
            dwellTimeSeconds: 1800); // 30 minutes

        var alertRule = new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = "Dwell Time Alert",
            AlertSeverity = "medium",
            AlertNameTemplate = "{entity_type} {entity_id} overstayed in {geofence_name}",
            AlertDescriptionTemplate = "Entity {entity_id} exited {geofence_name} after {dwell_time_minutes} minutes (dwell: {dwell_time} seconds)",
            DeduplicationWindowMinutes = 60
        };

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { alertRule });

        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);
        await Task.Delay(100); // Allow async processing

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();

        content.Should().Contain("delivery-truck truck-456 overstayed in Loading Zone 1");
        content.Should().Contain("exited Loading Zone 1 after 30 minutes");
        content.Should().Contain("dwell: 1800 seconds");
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_MultipleMatchingRules_ShouldGenerateMultipleAlerts()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "vehicle-789",
            "Restricted Zone",
            geofenceId);

        var rule1 = new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = "Critical Alert",
            AlertSeverity = "critical",
            AlertNameTemplate = "Critical: {entity_id} entered {geofence_name}",
            NotificationChannelIds = new List<long> { 1 }, // PagerDuty
            DeduplicationWindowMinutes = 30
        };

        var rule2 = new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = "Info Alert",
            AlertSeverity = "info",
            AlertNameTemplate = "Info: {entity_id} entered {geofence_name}",
            NotificationChannelIds = new List<long> { 2 }, // Slack
            DeduplicationWindowMinutes = 60
        };

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { rule1, rule2 });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);
        await Task.Delay(200); // Allow async processing

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c => c.AlertSeverity == "critical"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c => c.AlertSeverity == "info"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_WithCustomLabels_ShouldIncludeLabelsInAlert()
    {
        // Arrange
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "entity-1",
            "Test Zone",
            Guid.NewGuid());

        var alertRule = new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Alert",
            AlertSeverity = "medium",
            AlertNameTemplate = "Test Alert",
            AlertLabels = new Dictionary<string, string>
            {
                ["priority"] = "high",
                ["team"] = "security",
                ["environment"] = "production"
            },
            DeduplicationWindowMinutes = 60
        };

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { alertRule });

        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);
        await Task.Delay(100);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();

        content.Should().Contain("\"priority\":\"high\"");
        content.Should().Contain("\"team\":\"security\"");
        content.Should().Contain("\"environment\":\"production\"");
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_HttpClientFailure_ShouldLogError()
    {
        // Arrange
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "entity-1",
            "Test Zone",
            Guid.NewGuid());

        var alertRule = new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Alert",
            AlertSeverity = "critical",
            AlertNameTemplate = "Test",
            DeduplicationWindowMinutes = 60
        };

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { alertRule });

        // Simulate HTTP failure
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert - should not throw
        await _service.ProcessGeofenceEventAsync(geofenceEvent);
        await Task.Delay(100);

        // Verify correlation was NOT created (since alert failed)
        _mockRepository.Verify(r => r.CreateCorrelationAsync(
            It.IsAny<GeofenceAlertCorrelation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessGeofenceEventAsync_WithTenantId_ShouldPassTenantToRepository()
    {
        // Arrange
        var tenantId = "tenant-123";
        var geofenceEvent = CreateGeofenceEvent(
            GeofenceEventType.Enter,
            "entity-1",
            "Test Zone",
            Guid.NewGuid(),
            tenantId: tenantId);

        _mockRepository.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.FindMatchingRulesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
                tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule>());

        // Act
        await _service.ProcessGeofenceEventAsync(geofenceEvent);

        // Assert
        _mockRepository.Verify(r => r.ShouldSilenceAlertAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTimeOffset>(), tenantId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.FindMatchingRulesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(),
            tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private GeofenceEvent CreateGeofenceEvent(
        GeofenceEventType eventType,
        string entityId,
        string geofenceName,
        Guid geofenceId,
        string? entityType = null,
        int? dwellTimeSeconds = null,
        string? tenantId = null)
    {
        return new GeofenceEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EventTime = DateTime.UtcNow,
            GeofenceId = geofenceId,
            GeofenceName = geofenceName,
            EntityId = entityId,
            EntityType = entityType,
            Location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8)),
            DwellTimeSeconds = dwellTimeSeconds,
            TenantId = tenantId,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
