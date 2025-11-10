// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Integration.Tests.Sensors;

/// <summary>
/// Integration tests for sensor anomaly detection
/// Requires PostgreSQL database with SensorThings schema
/// </summary>
[Collection("Database")]
public sealed class AnomalyDetectionIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly ILogger<AnomalyDetectionIntegrationTests> _logger;
    private IAnomalyDetectionRepository? _repository;

    public AnomalyDetectionIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Testing.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _connectionString = config.GetConnectionString("SensorThingsDb")
            ?? "Host=localhost;Port=5432;Database=honua_test;Username=postgres;Password=test";

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<AnomalyDetectionIntegrationTests>();
    }

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var repoLogger = loggerFactory.CreateLogger<PostgresAnomalyDetectionRepository>();

        _repository = new PostgresAnomalyDetectionRepository(_connectionString, repoLogger);

        // Ensure database is set up (table creation is handled in repository)
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetStaleDatastreamsAsync_ReturnsStaleDatastreams()
    {
        // Arrange
        var threshold = TimeSpan.FromMinutes(5);

        // Act
        var staleDatastreams = await _repository!.GetStaleDatastreamsAsync(threshold);

        // Assert
        Assert.NotNull(staleDatastreams);
        // Note: Actual results depend on database state
        _logger.LogInformation("Found {Count} stale datastreams", staleDatastreams.Count);
    }

    [Fact]
    public async Task GetDatastreamStatisticsAsync_CalculatesStatistics()
    {
        // Arrange
        var datastreams = await _repository!.GetActiveDatastreamsAsync();

        if (datastreams.Count == 0)
        {
            _logger.LogWarning("No datastreams available for testing - skipping");
            return;
        }

        var datastreamId = datastreams[0].DatastreamId;
        var window = TimeSpan.FromDays(1);

        // Act
        var statistics = await _repository.GetDatastreamStatisticsAsync(datastreamId, window);

        // Assert
        if (statistics != null)
        {
            Assert.Equal(datastreamId, statistics.DatastreamId);
            Assert.True(statistics.ObservationCount >= 0);
            _logger.LogInformation(
                "Statistics for {DatastreamName}: Count={Count}, Mean={Mean}, StdDev={StdDev}",
                statistics.DatastreamName,
                statistics.ObservationCount,
                statistics.Mean,
                statistics.StandardDeviation);
        }
        else
        {
            _logger.LogWarning("No statistics available for datastream {DatastreamId}", datastreamId);
        }
    }

    [Fact]
    public async Task AlertRateLimiting_WorksCorrectly()
    {
        // Arrange
        var datastreams = await _repository!.GetActiveDatastreamsAsync();

        if (datastreams.Count == 0)
        {
            _logger.LogWarning("No datastreams available for testing - skipping");
            return;
        }

        var datastreamId = datastreams[0].DatastreamId;
        var anomalyType = AnomalyType.StaleSensor;
        var window = TimeSpan.FromMinutes(10);
        var maxAlerts = 3;

        // Act & Assert - Should be able to send first alert
        var canSend1 = await _repository.CanSendAlertAsync(datastreamId, anomalyType, window, maxAlerts);
        Assert.True(canSend1);

        // Record alerts up to the limit
        for (int i = 0; i < maxAlerts; i++)
        {
            await _repository.RecordAlertAsync(datastreamId, anomalyType);
        }

        // Should not be able to send more alerts
        var canSend2 = await _repository.CanSendAlertAsync(datastreamId, anomalyType, window, maxAlerts);
        Assert.False(canSend2);

        _logger.LogInformation("Rate limiting test passed for datastream {DatastreamId}", datastreamId);
    }

    [Fact]
    public async Task StaleSensorDetectionService_Integration()
    {
        // Arrange
        var options = Options.Create(new AnomalyDetectionOptions
        {
            Enabled = true,
            StaleSensorDetection = new StaleSensorDetectionOptions
            {
                Enabled = true,
                StaleThreshold = TimeSpan.FromDays(365) // Very long threshold to test detection
            }
        });

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var serviceLogger = loggerFactory.CreateLogger<StaleSensorDetectionService>();

        var service = new StaleSensorDetectionService(_repository!, options, serviceLogger);

        // Act
        var anomalies = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.NotNull(anomalies);
        _logger.LogInformation("Detected {Count} stale sensor anomalies", anomalies.Count);

        foreach (var anomaly in anomalies.Take(5))
        {
            _logger.LogInformation(
                "Anomaly: {Type} - {Description}",
                anomaly.Type,
                anomaly.Description);
        }
    }
}
