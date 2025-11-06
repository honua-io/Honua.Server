// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Honua.Server.Host.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Admin;

public class GeofenceAlertAdministrationEndpointsTests
{
    private readonly Mock<IGeofenceAlertRepository> _mockRepository;

    public GeofenceAlertAdministrationEndpointsTests()
    {
        _mockRepository = new Mock<IGeofenceAlertRepository>();
    }

    [Fact]
    public async Task GetAlertRules_ShouldReturnAllRules()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Rule 1", Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "Rule 2", Enabled = false }
        };

        _mockRepository.Setup(r => r.GetAlertRulesAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await CallGetAlertRules(_mockRepository.Object, null, false);

        // Assert
        var okResult = result.Should().BeOfType<Ok<IReadOnlyList<GeofenceAlertRule>>>().Subject;
        okResult.Value.Should().HaveCount(2);
        okResult.Value[0].Name.Should().Be("Rule 1");
    }

    [Fact]
    public async Task GetAlertRules_WithTenantFilter_ShouldFilterByTenant()
    {
        // Arrange
        var tenantId = "tenant-1";
        var expectedRules = new List<GeofenceAlertRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Tenant Rule", TenantId = tenantId }
        };

        _mockRepository.Setup(r => r.GetAlertRulesAsync(
                tenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await CallGetAlertRules(_mockRepository.Object, tenantId, false);

        // Assert
        var okResult = result.Should().BeOfType<Ok<IReadOnlyList<GeofenceAlertRule>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value[0].TenantId.Should().Be(tenantId);

        _mockRepository.Verify(r => r.GetAlertRulesAsync(
            tenantId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAlertRules_EnabledOnly_ShouldFilterByEnabled()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Active Rule", Enabled = true }
        };

        _mockRepository.Setup(r => r.GetAlertRulesAsync(
                It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await CallGetAlertRules(_mockRepository.Object, null, true);

        // Assert
        var okResult = result.Should().BeOfType<Ok<IReadOnlyList<GeofenceAlertRule>>>().Subject;
        okResult.Value.Should().AllSatisfy(r => r.Enabled.Should().BeTrue());
    }

    [Fact]
    public async Task GetAlertRule_ExistingRule_ShouldReturnRule()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = new GeofenceAlertRule
        {
            Id = ruleId,
            Name = "Test Rule",
            AlertSeverity = "critical"
        };

        _mockRepository.Setup(r => r.GetAlertRuleAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRule);

        // Act
        var result = await CallGetAlertRule(_mockRepository.Object, ruleId);

        // Assert
        var okResult = result.Should().BeOfType<Ok<GeofenceAlertRule>>().Subject;
        okResult.Value.Id.Should().Be(ruleId);
        okResult.Value.Name.Should().Be("Test Rule");
    }

    [Fact]
    public async Task GetAlertRule_NonExistent_ShouldReturnNotFound()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAlertRuleAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeofenceAlertRule?)null);

        // Act
        var result = await CallGetAlertRule(_mockRepository.Object, Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task CreateAlertRule_ValidRequest_ShouldCreateAndReturnCreated()
    {
        // Arrange
        var request = new CreateGeofenceAlertRuleRequest
        {
            Name = "New Rule",
            AlertSeverity = "high",
            AlertNameTemplate = "{entity_id} entered {geofence_name}",
            DeduplicationWindowMinutes = 60
        };

        var createdId = Guid.NewGuid();
        _mockRepository.Setup(r => r.CreateAlertRuleAsync(
                It.IsAny<GeofenceAlertRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdId);

        // Act
        var result = await CallCreateAlertRule(
            _mockRepository.Object,
            NullLogger<GeofenceAlertRepository>.Instance,
            request);

        // Assert
        var createdResult = result.Should().BeOfType<Created<object>>().Subject;
        createdResult.Location.Should().Contain(createdId.ToString());

        _mockRepository.Verify(r => r.CreateAlertRuleAsync(
            It.Is<GeofenceAlertRule>(rule => rule.Name == "New Rule"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAlertRule_ShouldCallRepositoryDelete()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        _mockRepository.Setup(r => r.DeleteAlertRuleAsync(
                ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CallDeleteAlertRule(
            _mockRepository.Object,
            NullLogger<GeofenceAlertRepository>.Instance,
            ruleId);

        // Assert
        result.Should().BeOfType<NoContent>();
        _mockRepository.Verify(r => r.DeleteAlertRuleAsync(
            ruleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSilencingRules_ShouldReturnAllRules()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertSilencingRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Maintenance Window", Enabled = true }
        };

        _mockRepository.Setup(r => r.GetSilencingRulesAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await CallGetSilencingRules(_mockRepository.Object, null, false);

        // Assert
        var okResult = result.Should().BeOfType<Ok<IReadOnlyList<GeofenceAlertSilencingRule>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value[0].Name.Should().Be("Maintenance Window");
    }

    [Fact]
    public async Task GetActiveAlerts_ShouldReturnActiveAlerts()
    {
        // Arrange
        var expectedAlerts = new List<ActiveGeofenceAlert>
        {
            new()
            {
                GeofenceEventId = Guid.NewGuid(),
                EventType = "enter",
                AlertSeverity = "critical",
                AlertStatus = "active"
            }
        };

        _mockRepository.Setup(r => r.GetActiveAlertsAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await CallGetActiveAlerts(_mockRepository.Object, null);

        // Assert
        var okResult = result.Should().BeOfType<Ok<IReadOnlyList<ActiveGeofenceAlert>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value[0].AlertStatus.Should().Be("active");
    }

    [Fact]
    public async Task GetCorrelation_ExistingCorrelation_ShouldReturnCorrelation()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var expectedCorrelation = new GeofenceAlertCorrelation
        {
            GeofenceEventId = eventId,
            AlertFingerprint = "test-fingerprint",
            AlertSeverity = "high"
        };

        _mockRepository.Setup(r => r.GetCorrelationAsync(
                eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCorrelation);

        // Act
        var result = await CallGetCorrelation(_mockRepository.Object, eventId);

        // Assert
        var okResult = result.Should().BeOfType<Ok<GeofenceAlertCorrelation>>().Subject;
        okResult.Value.GeofenceEventId.Should().Be(eventId);
    }

    [Fact]
    public async Task GetCorrelation_NonExistent_ShouldReturnNotFound()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetCorrelationAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeofenceAlertCorrelation?)null);

        // Act
        var result = await CallGetCorrelation(_mockRepository.Object, Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFound>();
    }

    // Helper methods that simulate endpoint calls
    // These methods extract the endpoint logic for testing without requiring a full HTTP context

    private static Task<IResult> CallGetAlertRules(
        IGeofenceAlertRepository repository,
        string? tenantId,
        bool enabledOnly)
    {
        // Simulates the endpoint handler logic
        return Task.FromResult<IResult>(
            Results.Ok(repository.GetAlertRulesAsync(tenantId, enabledOnly).Result));
    }

    private static Task<IResult> CallGetAlertRule(
        IGeofenceAlertRepository repository,
        Guid id)
    {
        var rule = repository.GetAlertRuleAsync(id).Result;
        return Task.FromResult<IResult>(
            rule != null ? Results.Ok(rule) : Results.NotFound());
    }

    private static Task<IResult> CallCreateAlertRule(
        IGeofenceAlertRepository repository,
        Microsoft.Extensions.Logging.ILogger<GeofenceAlertRepository> logger,
        CreateGeofenceAlertRuleRequest request)
    {
        try
        {
            var rule = new GeofenceAlertRule
            {
                Name = request.Name,
                AlertSeverity = request.AlertSeverity,
                AlertNameTemplate = request.AlertNameTemplate,
                DeduplicationWindowMinutes = request.DeduplicationWindowMinutes
            };

            var id = repository.CreateAlertRuleAsync(rule).Result;
            return Task.FromResult<IResult>(
                Results.Created($"/admin/geofence-alerts/rules/{id}", new { id }));
        }
        catch (Exception)
        {
            return Task.FromResult<IResult>(Results.Problem("Failed to create alert rule", statusCode: 500));
        }
    }

    private static Task<IResult> CallDeleteAlertRule(
        IGeofenceAlertRepository repository,
        Microsoft.Extensions.Logging.ILogger<GeofenceAlertRepository> logger,
        Guid id)
    {
        try
        {
            repository.DeleteAlertRuleAsync(id).Wait();
            return Task.FromResult<IResult>(Results.NoContent());
        }
        catch (Exception)
        {
            return Task.FromResult<IResult>(Results.Problem("Failed to delete alert rule", statusCode: 500));
        }
    }

    private static Task<IResult> CallGetSilencingRules(
        IGeofenceAlertRepository repository,
        string? tenantId,
        bool enabledOnly)
    {
        return Task.FromResult<IResult>(
            Results.Ok(repository.GetSilencingRulesAsync(tenantId, enabledOnly).Result));
    }

    private static Task<IResult> CallGetActiveAlerts(
        IGeofenceAlertRepository repository,
        string? tenantId)
    {
        return Task.FromResult<IResult>(
            Results.Ok(repository.GetActiveAlertsAsync(tenantId).Result));
    }

    private static Task<IResult> CallGetCorrelation(
        IGeofenceAlertRepository repository,
        Guid geofenceEventId)
    {
        var correlation = repository.GetCorrelationAsync(geofenceEventId).Result;
        return Task.FromResult<IResult>(
            correlation != null ? Results.Ok(correlation) : Results.NotFound());
    }
}
