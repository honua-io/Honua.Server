// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Integration.Azure.Models;

namespace Honua.Integration.Azure.ErrorHandling;

/// <summary>
/// Service for managing dead letter queue for failed IoT Hub messages
/// </summary>
public interface IDeadLetterQueueService
{
    /// <summary>
    /// Add a message to the dead letter queue
    /// </summary>
    Task AddToDeadLetterQueueAsync(DeadLetterMessage message, CancellationToken ct = default);

    /// <summary>
    /// Get all messages in the dead letter queue
    /// </summary>
    Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Get dead letter messages for a specific device
    /// </summary>
    Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesByDeviceAsync(
        string deviceId,
        CancellationToken ct = default);

    /// <summary>
    /// Retry processing a dead letter message
    /// </summary>
    Task<bool> RetryDeadLetterMessageAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Remove a message from the dead letter queue
    /// </summary>
    Task DeleteDeadLetterMessageAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Clear old dead letter messages
    /// </summary>
    Task PurgeOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default);
}
