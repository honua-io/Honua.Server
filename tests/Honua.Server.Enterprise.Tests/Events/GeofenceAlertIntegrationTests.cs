// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

/// <summary>
/// Integration tests for the geofence-alert system integration.
/// Tests the complete flow from geofence events to alert generation.
/// </summary>
public class GeofenceAlertIntegrationTests
{
    private readonly Mock<IGeofenceRepository> _mockGeofenceRepo;
    private readonly Mock<IEntityStateRepository> _mockStateRepo;
    private readonly Mock<IGeofenceEventRepository> _mockEventRepo;
    private readonly Mock<IGeofenceAlertRepository> _mockAlertRepo;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly GeometryFactory _geometryFactory;
    private readonly GeofenceEvaluationService _evaluationService;
    private readonly GeofenceToAlertBridgeService _bridgeService;

    public GeofenceAlertIntegrationTests()
    {
        _mockGeofenceRepo = new Mock<IGeofenceRepository>();
        _mockStateRepo = new Mock<IEntityStateRepository>();
        _mockEventRepo = new Mock<IGeofenceEventRepository>();
        _mockAlertRepo = new Mock<IGeofenceAlertRepository>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Setup HTTP client
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        _mockHttpClientFactory.Setup(f => f.CreateClient("AlertReceiver")).Returns(httpClient);

        // Create bridge service
        _bridgeService = new GeofenceToAlertBridgeService(
            _mockAlertRepo.Object,
            _mockHttpClientFactory.Object,
            NullLogger<GeofenceToAlertBridgeService>.Instance,
            "http://localhost:5001");

        // Create evaluation service with bridge service
        _evaluationService = new GeofenceEvaluationService(
            _mockGeofenceRepo.Object,
            _mockStateRepo.Object,
            _mockEventRepo.Object,
            NullLogger<GeofenceEvaluationService>.Instance,
            _bridgeService);
    }

    [Fact]
    public async Task GeofenceEntry_WithMatchingAlertRule_ShouldGenerateAlert()
    {
        // Arrange
        var entityId = "vehicle-123";
        var geofenceId = Guid.NewGuid();
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;

        var geofence = CreateGeofence(geofenceId, "Restricted Zone A");
        var alertRule = CreateAlertRule(
            "Restricted Entry Alert",
            geofenceId: geofenceId,
            severity: "critical");

        // Setup geofence repository
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        // Setup state repository (first entry, no previous state)
        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(
                entityId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Setup event repository
        _mockEventRepo.Setup(r => r.CreateBatchAsync(
                It.IsAny<List<GeofenceEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<GeofenceEvent> events, CancellationToken _) => events);

        // Setup alert repository
        _mockAlertRepo.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAlertRepo.Setup(r => r.FindMatchingRulesAsync(
                geofenceId, It.IsAny<string>(), entityId, It.IsAny<string>(),
                "enter", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        var result = await _evaluationService.EvaluateLocationAsync(
            entityId, location, eventTime);

        // Allow async alert processing to complete
        await Task.Delay(200);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events[0].EventType.Should().Be(GeofenceEventType.Enter);

        // Verify alert was sent
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/api/alerts")),
            ItExpr.IsAny<CancellationToken>());

        // Verify correlation was created
        _mockAlertRepo.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c =>
                c.AlertSeverity == "critical" &&
                c.WasSilenced == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeofenceExit_WithDwellTimeRule_ShouldGenerateAlert()
    {
        // Arrange
        var entityId = "truck-456";
        var geofenceId = Guid.NewGuid();
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.0, 37.5)); // Outside
        var enteredAt = DateTime.UtcNow.AddMinutes(-35); // 35 minutes ago
        var eventTime = DateTime.UtcNow;

        var geofence = CreateGeofence(geofenceId, "Loading Zone");
        var alertRule = CreateAlertRule(
            "Overstay Alert",
            geofenceId: geofenceId,
            severity: "medium",
            eventTypes: new List<string> { "exit" },
            minDwellTimeSeconds: 1800); // 30 minutes

        // Setup existing state (entity was inside)
        var existingState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = enteredAt,
            LastUpdated = enteredAt
        };

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>()); // Outside all geofences

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(
                entityId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { existingState });

        _mockEventRepo.Setup(r => r.CreateBatchAsync(
                It.IsAny<List<GeofenceEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<GeofenceEvent> events, CancellationToken _) => events);

        _mockAlertRepo.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAlertRepo.Setup(r => r.FindMatchingRulesAsync(
                geofenceId, "Loading Zone", entityId, It.IsAny<string>(),
                "exit", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { alertRule });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        var result = await _evaluationService.EvaluateLocationAsync(
            entityId, location, eventTime);

        await Task.Delay(200);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events[0].EventType.Should().Be(GeofenceEventType.Exit);
        result.Events[0].DwellTimeSeconds.Should().BeGreaterThan(1800);

        _mockAlertRepo.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c => c.AlertSeverity == "medium"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeofenceEntry_WithSilencingRule_ShouldNotGenerateAlert()
    {
        // Arrange
        var entityId = "vehicle-789";
        var geofenceId = Guid.NewGuid();
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;

        var geofence = CreateGeofence(geofenceId, "Construction Zone");
        var alertRule = CreateAlertRule("Entry Alert", geofenceId: geofenceId);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(
                entityId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        _mockEventRepo.Setup(r => r.CreateBatchAsync(
                It.IsAny<List<GeofenceEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<GeofenceEvent> events, CancellationToken _) => events);

        // Alert is silenced
        _mockAlertRepo.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _evaluationService.EvaluateLocationAsync(
            entityId, location, eventTime);

        await Task.Delay(200);

        // Assert
        result.Events.Should().HaveCount(1);

        // Verify alert was NOT sent
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // Verify silenced correlation was created
        _mockAlertRepo.Verify(r => r.CreateCorrelationAsync(
            It.Is<GeofenceAlertCorrelation>(c => c.WasSilenced == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeofenceEntry_WithMultipleRules_ShouldGenerateMultipleAlerts()
    {
        // Arrange
        var entityId = "emergency-vehicle-1";
        var geofenceId = Guid.NewGuid();
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;

        var geofence = CreateGeofence(geofenceId, "Emergency Zone");
        var criticalRule = CreateAlertRule("Critical Alert", geofenceId: geofenceId, severity: "critical");
        var infoRule = CreateAlertRule("Info Alert", geofenceId: geofenceId, severity: "info");

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(
                entityId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        _mockEventRepo.Setup(r => r.CreateBatchAsync(
                It.IsAny<List<GeofenceEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<GeofenceEvent> events, CancellationToken _) => events);

        _mockAlertRepo.Setup(r => r.ShouldSilenceAlertAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockAlertRepo.Setup(r => r.FindMatchingRulesAsync(
                geofenceId, It.IsAny<string>(), entityId, It.IsAny<string>(),
                "enter", It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeofenceAlertRule> { criticalRule, infoRule });

        _mockAlertRepo.Setup(r => r.CreateCorrelationAsync(
                It.IsAny<GeofenceAlertCorrelation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Guid.NewGuid());

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        var result = await _evaluationService.EvaluateLocationAsync(
            entityId, location, eventTime);

        await Task.Delay(300);

        // Assert
        result.Events.Should().HaveCount(1);

        // Verify two alerts were sent
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // Verify two correlations were created
        _mockAlertRepo.Verify(r => r.CreateCorrelationAsync(
            It.IsAny<GeofenceAlertCorrelation>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private Geofence CreateGeofence(Guid id, string name)
    {
        var coordinates = new[]
        {
            new Coordinate(-122.5, 37.7),
            new Coordinate(-122.3, 37.7),
            new Coordinate(-122.3, 37.9),
            new Coordinate(-122.5, 37.9),
            new Coordinate(-122.5, 37.7)
        };

        return new Geofence
        {
            Id = id,
            Name = name,
            Description = $"Test geofence {name}",
            Geometry = _geometryFactory.CreatePolygon(coordinates),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true
        };
    }

    private GeofenceAlertRule CreateAlertRule(
        string name,
        Guid? geofenceId = null,
        string severity = "medium",
        List<string>? eventTypes = null,
        int? minDwellTimeSeconds = null)
    {
        return new GeofenceAlertRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            Enabled = true,
            GeofenceId = geofenceId,
            EventTypes = eventTypes ?? new List<string> { "enter" },
            AlertSeverity = severity,
            AlertNameTemplate = "{entity_id} triggered {geofence_name}",
            AlertDescriptionTemplate = "Entity {entity_id} {event_type} geofence {geofence_name}",
            MinDwellTimeSeconds = minDwellTimeSeconds,
            DeduplicationWindowMinutes = 60
        };
    }
}
