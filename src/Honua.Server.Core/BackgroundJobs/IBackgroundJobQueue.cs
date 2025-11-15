// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Abstraction for background job queue systems.
/// Supports pluggable implementations: PostgreSQL polling, AWS SQS, Azure Service Bus, RabbitMQ.
/// </summary>
/// <remarks>
/// This interface provides a unified abstraction over different message queue backends:
///
/// Tier 1-2 (PostgreSQL polling):
/// - Simple implementation using database polling
/// - No external dependencies
/// - Suitable for low-throughput workloads
///
/// Tier 3 (Message Queue - SQS, Service Bus, RabbitMQ):
/// - Event-driven processing with long polling
/// - Better scalability and throughput
/// - Native retry and dead-letter queue support
/// - Visibility timeout prevents duplicate processing
/// </remarks>
public interface IBackgroundJobQueue
{
    /// <summary>
    /// Enqueues a job for background processing.
    /// </summary>
    /// <typeparam name="TJob">Job payload type</typeparam>
    /// <param name="job">Job to enqueue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Unique message identifier</returns>
    Task<string> EnqueueAsync<TJob>(TJob job, CancellationToken cancellationToken = default)
        where TJob : class;

    /// <summary>
    /// Enqueues a job with additional options (delay, priority, deduplication).
    /// </summary>
    /// <typeparam name="TJob">Job payload type</typeparam>
    /// <param name="job">Job to enqueue</param>
    /// <param name="options">Enqueue options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Unique message identifier</returns>
    Task<string> EnqueueAsync<TJob>(TJob job, EnqueueOptions options, CancellationToken cancellationToken = default)
        where TJob : class;

    /// <summary>
    /// Receives jobs from the queue for processing.
    /// For message queues: uses long polling to wait for messages.
    /// For database polling: returns pending jobs immediately or empty list.
    /// </summary>
    /// <typeparam name="TJob">Job payload type</typeparam>
    /// <param name="maxMessages">Maximum number of messages to receive (1-10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of queue messages</returns>
    Task<IEnumerable<QueueMessage<TJob>>> ReceiveAsync<TJob>(
        int maxMessages = 1,
        CancellationToken cancellationToken = default)
        where TJob : class;

    /// <summary>
    /// Marks a message as successfully processed and removes it from the queue.
    /// </summary>
    /// <param name="messageId">Message identifier from ReceiveAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CompleteAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a message to the queue for retry (transient failure).
    /// Message becomes visible again after visibility timeout.
    /// </summary>
    /// <param name="messageId">Message identifier from ReceiveAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AbandonAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approximate queue depth (pending messages).
    /// Used for monitoring and autoscaling decisions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approximate number of pending messages</returns>
    Task<long> GetQueueDepthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Message received from the queue
/// </summary>
/// <typeparam name="TJob">Job payload type</typeparam>
public class QueueMessage<TJob>
    where TJob : class
{
    /// <summary>Unique message identifier (for Complete/Abandon)</summary>
    public required string MessageId { get; init; }

    /// <summary>Job payload</summary>
    public required TJob Body { get; init; }

    /// <summary>Receipt handle (provider-specific, used for deletion)</summary>
    public required string ReceiptHandle { get; init; }

    /// <summary>Number of times this message has been received (delivery count)</summary>
    public int DeliveryCount { get; init; } = 1;

    /// <summary>When message was first enqueued</summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When message becomes visible again if not completed</summary>
    public DateTimeOffset? VisibleAfter { get; init; }

    /// <summary>Custom attributes/metadata</summary>
    public Dictionary<string, string>? Attributes { get; init; }
}

/// <summary>
/// Options for enqueuing jobs
/// </summary>
public class EnqueueOptions
{
    /// <summary>Delay before message becomes visible (default: none)</summary>
    public TimeSpan? DelaySeconds { get; init; }

    /// <summary>Message priority (1-10, higher = more urgent, default: 5)</summary>
    public int Priority { get; init; } = 5;

    /// <summary>Deduplication ID for exactly-once delivery (SQS FIFO queues)</summary>
    public string? DeduplicationId { get; init; }

    /// <summary>Message group ID for FIFO ordering (SQS FIFO queues)</summary>
    public string? MessageGroupId { get; init; }

    /// <summary>Custom attributes/metadata</summary>
    public Dictionary<string, string>? Attributes { get; init; }
}
