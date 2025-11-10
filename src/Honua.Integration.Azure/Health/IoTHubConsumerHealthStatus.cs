// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Integration.Azure.Health;

/// <summary>
/// Health status for the Azure IoT Hub consumer service
/// </summary>
public sealed class IoTHubConsumerHealthStatus
{
    /// <summary>
    /// Whether the service is healthy
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// When the service last started
    /// </summary>
    public DateTime? LastStartTime { get; set; }

    /// <summary>
    /// When the last message was received
    /// </summary>
    public DateTime? LastMessageTime { get; set; }

    /// <summary>
    /// Total messages received
    /// </summary>
    public long TotalMessagesReceived { get; set; }

    /// <summary>
    /// Total messages successfully processed
    /// </summary>
    public long TotalMessagesProcessed { get; set; }

    /// <summary>
    /// Total messages that failed processing
    /// </summary>
    public long TotalMessagesFailed { get; set; }

    /// <summary>
    /// Total observations created
    /// </summary>
    public long TotalObservationsCreated { get; set; }

    /// <summary>
    /// Number of consecutive errors
    /// </summary>
    public int ConsecutiveErrors { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Get success rate percentage
    /// </summary>
    public double SuccessRate =>
        TotalMessagesReceived > 0
            ? (double)TotalMessagesProcessed / TotalMessagesReceived * 100
            : 0;

    /// <summary>
    /// Time since last message
    /// </summary>
    public TimeSpan? TimeSinceLastMessage =>
        LastMessageTime.HasValue
            ? DateTime.UtcNow - LastMessageTime.Value
            : null;
}
