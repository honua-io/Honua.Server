// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Honua.Server.Enterprise.IoT.Azure.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.IoT.Azure.ErrorHandling;

/// <summary>
/// In-memory implementation of dead letter queue service
/// For production, consider using a persistent store (database, Azure Storage, etc.)
/// </summary>
public sealed class InMemoryDeadLetterQueueService : IDeadLetterQueueService
{
    private readonly ConcurrentDictionary<string, DeadLetterMessage> _deadLetterQueue = new();
    private readonly ILogger<InMemoryDeadLetterQueueService> _logger;

    public InMemoryDeadLetterQueueService(ILogger<InMemoryDeadLetterQueueService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task AddToDeadLetterQueueAsync(DeadLetterMessage message, CancellationToken ct = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _deadLetterQueue[message.Id] = message;

        _logger.LogWarning(
            "Message from device {DeviceId} added to dead letter queue. Reason: {Reason}",
            message.OriginalMessage.DeviceId,
            message.Error.Message);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
        int limit = 100,
        CancellationToken ct = default)
    {
        var messages = _deadLetterQueue.Values
            .OrderByDescending(m => m.DeadLetteredAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(messages);
    }

    public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesByDeviceAsync(
        string deviceId,
        CancellationToken ct = default)
    {
        var messages = _deadLetterQueue.Values
            .Where(m => m.OriginalMessage.DeviceId == deviceId)
            .OrderByDescending(m => m.DeadLetteredAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(messages);
    }

    public Task<bool> RetryDeadLetterMessageAsync(string messageId, CancellationToken ct = default)
    {
        // This would typically resubmit the message for processing
        // For now, just return false (not implemented in this version)
        _logger.LogInformation("Retry requested for dead letter message {MessageId}", messageId);
        return Task.FromResult(false);
    }

    public Task DeleteDeadLetterMessageAsync(string messageId, CancellationToken ct = default)
    {
        _deadLetterQueue.TryRemove(messageId, out _);
        _logger.LogInformation("Removed message {MessageId} from dead letter queue", messageId);
        return Task.CompletedTask;
    }

    public Task PurgeOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var oldMessages = _deadLetterQueue.Values
            .Where(m => m.DeadLetteredAt < cutoffTime)
            .ToList();

        foreach (var message in oldMessages)
        {
            _deadLetterQueue.TryRemove(message.Id, out _);
        }

        _logger.LogInformation("Purged {Count} old messages from dead letter queue", oldMessages.Count);

        return Task.CompletedTask;
    }
}
