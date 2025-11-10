// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Extensions;

/// <summary>
/// Extension methods for registering anomaly detection services
/// </summary>
public static class AnomalyDetectionServiceExtensions
{
    /// <summary>
    /// Adds sensor anomaly detection services to the service collection
    /// </summary>
    public static IServiceCollection AddSensorAnomalyDetection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<AnomalyDetectionOptions>(
            configuration.GetSection("Honua:Enterprise:SensorThings:AnomalyDetection"));

        // Register repository
        services.AddSingleton<IAnomalyDetectionRepository>(sp =>
        {
            var connectionString = configuration.GetConnectionString("SensorThingsDb")
                ?? throw new InvalidOperationException("SensorThingsDb connection string not configured");

            var logger = sp.GetRequiredService<ILogger<PostgresAnomalyDetectionRepository>>();
            return new PostgresAnomalyDetectionRepository(connectionString, logger);
        });

        // Register detection services
        services.AddSingleton<IStaleSensorDetectionService, StaleSensorDetectionService>();
        services.AddSingleton<IUnusualReadingDetectionService, UnusualReadingDetectionService>();
        services.AddSingleton<IAnomalyAlertService, AnomalyAlertService>();

        // Register HttpClient for webhook alerts
        services.AddHttpClient("AnomalyAlertWebhook");

        // Register background service
        services.AddSingleton<AnomalyDetectionBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<AnomalyDetectionBackgroundService>());

        // Register health check
        services.AddHealthChecks()
            .AddCheck<AnomalyDetectionHealthCheck>(
                "anomaly_detection",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "sensor", "anomaly", "background" });

        return services;
    }
}

/// <summary>
/// Health check for anomaly detection service
/// </summary>
public sealed class AnomalyDetectionHealthCheck : IHealthCheck
{
    private readonly AnomalyDetectionBackgroundService _backgroundService;
    private readonly ILogger<AnomalyDetectionHealthCheck> _logger;

    public AnomalyDetectionHealthCheck(
        AnomalyDetectionBackgroundService backgroundService,
        ILogger<AnomalyDetectionHealthCheck> logger)
    {
        _backgroundService = backgroundService ?? throw new ArgumentNullException(nameof(backgroundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = _backgroundService.GetHealthStatus();

            if (!status.IsHealthy)
            {
                var data = new Dictionary<string, object>
                {
                    ["last_check"] = status.LastCheckTime,
                    ["last_successful_check"] = status.LastSuccessfulCheckTime,
                    ["consecutive_failures"] = status.ConsecutiveFailures,
                    ["last_error"] = status.LastError ?? "unknown"
                };

                return Task.FromResult(
                    HealthCheckResult.Degraded(
                        $"Anomaly detection has {status.ConsecutiveFailures} consecutive failures. Last error: {status.LastError}",
                        data: data));
            }

            // Check if service is running (has checked recently)
            var timeSinceLastCheck = DateTime.UtcNow - status.LastCheckTime;
            if (timeSinceLastCheck > TimeSpan.FromMinutes(30))
            {
                var data = new Dictionary<string, object>
                {
                    ["last_check"] = status.LastCheckTime,
                    ["time_since_last_check_minutes"] = timeSinceLastCheck.TotalMinutes
                };

                return Task.FromResult(
                    HealthCheckResult.Degraded(
                        $"Anomaly detection has not run in {timeSinceLastCheck.TotalMinutes:F0} minutes",
                        data: data));
            }

            var healthData = new Dictionary<string, object>
            {
                ["last_check"] = status.LastCheckTime,
                ["last_successful_check"] = status.LastSuccessfulCheckTime,
                ["time_since_last_check_seconds"] = timeSinceLastCheck.TotalSeconds
            };

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Anomaly detection is running normally",
                    data: healthData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking anomaly detection health");
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Error checking anomaly detection health",
                    exception: ex));
        }
    }
}
