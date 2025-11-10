// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Integration.Azure.Health;

/// <summary>
/// Health check for Azure IoT Hub consumer service
/// </summary>
public sealed class AzureIoTHubHealthCheck : IHealthCheck
{
    private readonly AzureIoTHubConsumerService _consumerService;

    public AzureIoTHubHealthCheck(AzureIoTHubConsumerService consumerService)
    {
        _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _consumerService.GetHealthStatus();

        if (!status.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"IoT Hub consumer is unhealthy: {status.LastError}",
                data: CreateHealthData(status)));
        }

        // Check if we've received messages recently
        if (status.LastMessageTime.HasValue)
        {
            var timeSinceLastMessage = DateTime.UtcNow - status.LastMessageTime.Value;

            // Warn if no messages in 5 minutes
            if (timeSinceLastMessage > TimeSpan.FromMinutes(5))
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"No messages received in {timeSinceLastMessage.TotalMinutes:F1} minutes",
                    data: CreateHealthData(status)));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "IoT Hub consumer is healthy",
            data: CreateHealthData(status)));
    }

    private static Dictionary<string, object> CreateHealthData(IoTHubConsumerHealthStatus status)
    {
        return new Dictionary<string, object>
        {
            ["isHealthy"] = status.IsHealthy,
            ["lastStartTime"] = status.LastStartTime?.ToString("O") ?? "N/A",
            ["lastMessageTime"] = status.LastMessageTime?.ToString("O") ?? "N/A",
            ["totalMessagesReceived"] = status.TotalMessagesReceived,
            ["totalMessagesProcessed"] = status.TotalMessagesProcessed,
            ["totalMessagesFailed"] = status.TotalMessagesFailed,
            ["totalObservationsCreated"] = status.TotalObservationsCreated,
            ["successRate"] = $"{status.SuccessRate:F2}%",
            ["consecutiveErrors"] = status.ConsecutiveErrors,
            ["timeSinceLastMessage"] = status.TimeSinceLastMessage?.ToString() ?? "N/A"
        };
    }
}
