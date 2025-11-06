// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Dapper;
using Xunit;

namespace Honua.Server.Core.Tests.Repositories;

public class GeofenceAlertRepositoryTests
{
    private readonly Mock<IDbConnection> _mockConnection;
    private readonly GeofenceAlertRepository _repository;

    public GeofenceAlertRepositoryTests()
    {
        _mockConnection = new Mock<IDbConnection>();
        _repository = new GeofenceAlertRepository(
            _mockConnection.Object,
            NullLogger<GeofenceAlertRepository>.Instance);
    }

    [Fact]
    public async Task CreateCorrelationAsync_ValidCorrelation_ShouldReturnId()
    {
        // Arrange
        var correlation = new GeofenceAlertCorrelation
        {
            GeofenceEventId = Guid.NewGuid(),
            AlertFingerprint = "test-fingerprint",
            AlertHistoryId = 12345,
            AlertSeverity = "critical",
            AlertStatus = "active",
            WasSilenced = false,
            TenantId = "tenant-1"
        };

        _mockConnection.SetupDapperAsync(c => c.ExecuteScalarAsync<Guid>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(correlation.GeofenceEventId);

        // Act
        var result = await _repository.CreateCorrelationAsync(correlation);

        // Assert
        result.Should().Be(correlation.GeofenceEventId);
    }

    [Fact]
    public async Task GetCorrelationAsync_ExistingCorrelation_ShouldReturnCorrelation()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var expectedCorrelation = new GeofenceAlertCorrelation
        {
            GeofenceEventId = eventId,
            AlertFingerprint = "test-fingerprint",
            AlertSeverity = "high",
            AlertStatus = "active"
        };

        _mockConnection.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<GeofenceAlertCorrelation>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedCorrelation);

        // Act
        var result = await _repository.GetCorrelationAsync(eventId);

        // Assert
        result.Should().NotBeNull();
        result!.GeofenceEventId.Should().Be(eventId);
        result.AlertFingerprint.Should().Be("test-fingerprint");
    }

    [Fact]
    public async Task GetCorrelationAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        _mockConnection.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<GeofenceAlertCorrelation>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync((GeofenceAlertCorrelation?)null);

        // Act
        var result = await _repository.GetCorrelationAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateCorrelationStatusAsync_ValidUpdate_ShouldExecute()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var newStatus = "resolved";
        var alertHistoryId = 12345L;

        _mockConnection.SetupDapperAsync(c => c.ExecuteAsync(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(1);

        // Act
        await _repository.UpdateCorrelationStatusAsync(eventId, newStatus, alertHistoryId);

        // Assert
        _mockConnection.Verify(c => c.ExecuteAsync(
            It.Is<CommandDefinition>(cmd =>
                cmd.CommandText.Contains("UPDATE geofence_alert_correlation"))),
            Times.Once);
    }

    [Fact]
    public async Task CreateAlertRuleAsync_ValidRule_ShouldReturnId()
    {
        // Arrange
        var rule = new GeofenceAlertRule
        {
            Name = "Test Rule",
            Description = "Test Description",
            Enabled = true,
            GeofenceNamePattern = "Restricted.*",
            EventTypes = new List<string> { "enter", "exit" },
            AlertSeverity = "critical",
            AlertNameTemplate = "{entity_id} entered {geofence_name}",
            DeduplicationWindowMinutes = 60
        };

        _mockConnection.SetupDapperAsync(c => c.ExecuteAsync(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(1);

        // Act
        var result = await _repository.CreateAlertRuleAsync(rule);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAlertRuleAsync_ExistingRule_ShouldReturnRule()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var expectedRule = new GeofenceAlertRule
        {
            Id = ruleId,
            Name = "Test Rule",
            AlertSeverity = "medium",
            Enabled = true
        };

        _mockConnection.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<GeofenceAlertRule>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedRule);

        // Act
        var result = await _repository.GetAlertRuleAsync(ruleId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(ruleId);
        result.Name.Should().Be("Test Rule");
    }

    [Fact]
    public async Task GetAlertRulesAsync_WithTenantFilter_ShouldReturnFilteredRules()
    {
        // Arrange
        var tenantId = "tenant-1";
        var expectedRules = new List<GeofenceAlertRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Rule 1", TenantId = tenantId },
            new() { Id = Guid.NewGuid(), Name = "Rule 2", TenantId = tenantId }
        };

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<GeofenceAlertRule>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _repository.GetAlertRulesAsync(tenantId, enabledOnly: false);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.TenantId.Should().Be(tenantId));
    }

    [Fact]
    public async Task GetAlertRulesAsync_EnabledOnly_ShouldFilterByEnabled()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Enabled Rule", Enabled = true }
        };

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<GeofenceAlertRule>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _repository.GetAlertRulesAsync(null, enabledOnly: true);

        // Assert
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(r => r.Enabled.Should().BeTrue());
    }

    [Fact]
    public async Task DeleteAlertRuleAsync_ValidId_ShouldExecuteDelete()
    {
        // Arrange
        var ruleId = Guid.NewGuid();

        _mockConnection.SetupDapperAsync(c => c.ExecuteAsync(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(1);

        // Act
        await _repository.DeleteAlertRuleAsync(ruleId);

        // Assert
        _mockConnection.Verify(c => c.ExecuteAsync(
            It.Is<CommandDefinition>(cmd =>
                cmd.CommandText.Contains("DELETE FROM geofence_alert_rules"))),
            Times.Once);
    }

    [Fact]
    public async Task CreateSilencingRuleAsync_ValidRule_ShouldReturnId()
    {
        // Arrange
        var rule = new GeofenceAlertSilencingRule
        {
            Name = "Maintenance Window",
            Enabled = true,
            GeofenceNamePattern = "Zone-A.*",
            EventTypes = new List<string> { "enter", "exit" },
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddHours(8)
        };

        _mockConnection.SetupDapperAsync(c => c.ExecuteAsync(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(1);

        // Act
        var result = await _repository.CreateSilencingRuleAsync(rule);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSilencingRuleAsync_WithRecurringSchedule_ShouldSerializeSchedule()
    {
        // Arrange
        var rule = new GeofenceAlertSilencingRule
        {
            Name = "Business Hours Silencing",
            Enabled = true,
            RecurringSchedule = new RecurringSchedule
            {
                Days = new List<int> { 1, 2, 3, 4, 5 }, // Monday-Friday
                StartHour = 9,
                EndHour = 17
            }
        };

        _mockConnection.SetupDapperAsync(c => c.ExecuteAsync(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(1);

        // Act
        var result = await _repository.CreateSilencingRuleAsync(rule);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSilencingRulesAsync_EnabledOnly_ShouldFilterByEnabled()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertSilencingRule>
        {
            new() { Id = Guid.NewGuid(), Name = "Active Silencing", Enabled = true }
        };

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<GeofenceAlertSilencingRule>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _repository.GetSilencingRulesAsync(null, enabledOnly: true);

        // Assert
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(r => r.Enabled.Should().BeTrue());
    }

    [Fact]
    public async Task ShouldSilenceAlertAsync_WithMatchingRule_ShouldReturnTrue()
    {
        // Arrange
        _mockConnection.SetupDapperAsync(c => c.ExecuteScalarAsync<bool>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(true);

        // Act
        var result = await _repository.ShouldSilenceAlertAsync(
            Guid.NewGuid(),
            "Restricted Zone",
            "entity-1",
            "enter",
            DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSilenceAlertAsync_NoMatchingRule_ShouldReturnFalse()
    {
        // Arrange
        _mockConnection.SetupDapperAsync(c => c.ExecuteScalarAsync<bool>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(false);

        // Act
        var result = await _repository.ShouldSilenceAlertAsync(
            Guid.NewGuid(),
            "Normal Zone",
            "entity-1",
            "enter",
            DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FindMatchingRulesAsync_WithMatchingCriteria_ShouldReturnRules()
    {
        // Arrange
        var expectedRules = new List<GeofenceAlertRule>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Matching Rule",
                AlertSeverity = "critical",
                EventTypes = new List<string> { "enter" }
            }
        };

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<GeofenceAlertRule>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _repository.FindMatchingRulesAsync(
            Guid.NewGuid(),
            "Restricted Zone",
            "vehicle-123",
            "vehicle",
            "enter",
            null);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Matching Rule");
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ShouldReturnActiveAlerts()
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

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<ActiveGeofenceAlert>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _repository.GetActiveAlertsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].AlertStatus.Should().Be("active");
    }

    [Fact]
    public async Task GetActiveAlertsAsync_WithTenantFilter_ShouldFilterByTenant()
    {
        // Arrange
        var tenantId = "tenant-1";
        var expectedAlerts = new List<ActiveGeofenceAlert>
        {
            new()
            {
                GeofenceEventId = Guid.NewGuid(),
                TenantId = tenantId,
                AlertStatus = "active"
            }
        };

        _mockConnection.SetupDapperAsync(c => c.QueryAsync<ActiveGeofenceAlert>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _repository.GetActiveAlertsAsync(tenantId);

        // Assert
        result.Should().HaveCount(1);
        result[0].TenantId.Should().Be(tenantId);
    }
}
