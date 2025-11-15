// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// PostgreSQL-based implementation of background job queue using database polling.
/// Suitable for Tier 1-2 deployments with low-to-medium throughput.
/// </summary>
/// <remarks>
/// Implementation approach:
///
/// 1. Enqueue: INSERT INTO background_jobs table
/// 2. Receive: SELECT ... WHERE status = 'pending' ORDER BY priority DESC, created_at FOR UPDATE SKIP LOCKED
/// 3. Complete: DELETE FROM background_jobs
/// 4. Abandon: UPDATE status = 'pending', visible_after = NOW() + interval
///
/// Advantages:
/// - No external dependencies (uses existing PostgreSQL database)
/// - Simple to deploy and operate
/// - Transactional guarantees
///
/// Disadvantages:
/// - Polling overhead (CPU/network load on database)
/// - Higher latency than event-driven message queues
/// - Less scalable than dedicated message queue systems
///
/// Suitable for:
/// - Tier 1-2 deployments
/// - Low-to-medium job throughput (< 100 jobs/second)
/// - Deployments without cloud infrastructure
/// </remarks>
public sealed class PostgresBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresBackgroundJobQueue> _logger;
    private readonly BackgroundJobsOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public PostgresBackgroundJobQueue(
        string connectionString,
        ILogger<PostgresBackgroundJobQueue> logger,
        IOptions<BackgroundJobsOptions> options)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<string> EnqueueAsync<TJob>(TJob job, CancellationToken cancellationToken = default)
        where TJob : class
    {
        return EnqueueAsync(job, new EnqueueOptions(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TJob>(
        TJob job,
        EnqueueOptions options,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var messageId = Guid.NewGuid().ToString("N");
            var jobType = typeof(TJob).Name;
            var payload = JsonSerializer.Serialize(job, JsonOptions);
            var visibleAfter = options.DelaySeconds.HasValue
                ? DateTimeOffset.UtcNow + options.DelaySeconds.Value
                : (DateTimeOffset?)null;

            var sql = @"
                INSERT INTO background_jobs (
                    message_id,
                    job_type,
                    payload,
                    status,
                    priority,
                    created_at,
                    visible_after,
                    delivery_count,
                    deduplication_id,
                    message_group_id,
                    attributes
                )
                VALUES (
                    @MessageId,
                    @JobType,
                    @Payload::jsonb,
                    'pending',
                    @Priority,
                    @CreatedAt,
                    @VisibleAfter,
                    0,
                    @DeduplicationId,
                    @MessageGroupId,
                    @Attributes::jsonb
                )";

            await connection.ExecuteAsync(
                sql,
                new
                {
                    MessageId = messageId,
                    JobType = jobType,
                    Payload = payload,
                    Priority = options.Priority,
                    CreatedAt = DateTimeOffset.UtcNow,
                    VisibleAfter = visibleAfter,
                    DeduplicationId = options.DeduplicationId,
                    MessageGroupId = options.MessageGroupId,
                    Attributes = options.Attributes != null
                        ? JsonSerializer.Serialize(options.Attributes, JsonOptions)
                        : null
                });

            _logger.LogDebug(
                "Enqueued job {JobType} with message ID {MessageId}, priority {Priority}",
                jobType,
                messageId,
                options.Priority);

            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing job of type {JobType}", typeof(TJob).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QueueMessage<TJob>>> ReceiveAsync<TJob>(
        int maxMessages = 1,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        if (maxMessages < 1 || maxMessages > 10)
            throw new ArgumentOutOfRangeException(nameof(maxMessages), "Must be between 1 and 10");

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var jobType = typeof(TJob).Name;

            // Use SELECT ... FOR UPDATE SKIP LOCKED for atomic dequeue
            // This prevents multiple workers from processing the same message
            var sql = @"
                UPDATE background_jobs
                SET
                    status = 'processing',
                    delivery_count = delivery_count + 1,
                    last_received_at = @Now,
                    visible_after = @VisibleAfter,
                    receipt_handle = @ReceiptHandle
                WHERE message_id IN (
                    SELECT message_id
                    FROM background_jobs
                    WHERE job_type = @JobType
                      AND status = 'pending'
                      AND (visible_after IS NULL OR visible_after <= @Now)
                    ORDER BY priority DESC, created_at ASC
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING
                    message_id,
                    job_type,
                    payload,
                    receipt_handle,
                    delivery_count,
                    created_at as enqueued_at,
                    visible_after,
                    attributes";

            var now = DateTimeOffset.UtcNow;
            var visibleAfter = now.AddSeconds(_options.VisibilityTimeoutSeconds);
            var receiptHandle = Guid.NewGuid().ToString("N");

            var rows = await connection.QueryAsync<BackgroundJobRow>(
                sql,
                new
                {
                    JobType = jobType,
                    Now = now,
                    VisibleAfter = visibleAfter,
                    ReceiptHandle = receiptHandle,
                    Limit = maxMessages
                });

            var messages = rows.Select(row =>
            {
                var body = JsonSerializer.Deserialize<TJob>(row.Payload, JsonOptions);
                if (body == null)
                    throw new InvalidOperationException($"Failed to deserialize job payload for message {row.MessageId}");

                var attributes = !string.IsNullOrWhiteSpace(row.Attributes)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(row.Attributes, JsonOptions)
                    : null;

                return new QueueMessage<TJob>
                {
                    MessageId = row.MessageId,
                    Body = body,
                    ReceiptHandle = row.ReceiptHandle,
                    DeliveryCount = row.DeliveryCount,
                    EnqueuedAt = row.EnqueuedAt,
                    VisibleAfter = row.VisibleAfter,
                    Attributes = attributes
                };
            }).ToList();

            _logger.LogDebug(
                "Received {Count} messages of type {JobType}",
                messages.Count,
                jobType);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages of type {JobType}", typeof(TJob).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
                DELETE FROM background_jobs
                WHERE message_id = @MessageId
                  AND status = 'processing'";

            var deleted = await connection.ExecuteAsync(sql, new { MessageId = messageId });

            if (deleted > 0)
            {
                _logger.LogDebug("Completed and deleted message {MessageId}", messageId);
            }
            else
            {
                _logger.LogWarning("Message {MessageId} not found or already completed", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing message {MessageId}", messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AbandonAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            // Calculate exponential backoff for retry
            var sql = @"
                UPDATE background_jobs
                SET
                    status = CASE
                        WHEN delivery_count >= @MaxRetries THEN 'failed'
                        ELSE 'pending'
                    END,
                    visible_after = @VisibleAfter,
                    receipt_handle = NULL
                WHERE message_id = @MessageId
                  AND status = 'processing'
                RETURNING delivery_count";

            var visibleAfter = DateTimeOffset.UtcNow.AddSeconds(
                CalculateExponentialBackoff(1)); // Will be recalculated based on delivery_count

            var deliveryCount = await connection.QuerySingleOrDefaultAsync<int?>(
                sql,
                new
                {
                    MessageId = messageId,
                    MaxRetries = _options.MaxRetries,
                    VisibleAfter = visibleAfter
                });

            if (deliveryCount.HasValue)
            {
                _logger.LogDebug(
                    "Abandoned message {MessageId} (delivery count: {DeliveryCount})",
                    messageId,
                    deliveryCount.Value);
            }
            else
            {
                _logger.LogWarning("Message {MessageId} not found or not in processing state", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abandoning message {MessageId}", messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetQueueDepthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
                SELECT COUNT(*)
                FROM background_jobs
                WHERE status = 'pending'
                  AND (visible_after IS NULL OR visible_after <= @Now)";

            var count = await connection.ExecuteScalarAsync<long>(
                sql,
                new { Now = DateTimeOffset.UtcNow });

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue depth");
            throw;
        }
    }

    /// <summary>
    /// Calculates exponential backoff delay for retry attempts.
    /// Uses formula: min(initialDelay * 2^(attempt-1), maxDelay)
    /// </summary>
    private static double CalculateExponentialBackoff(int deliveryCount)
    {
        const double initialDelaySeconds = 5.0;  // Start with 5 seconds
        const double maxDelaySeconds = 300.0;    // Cap at 5 minutes

        // Calculate: 5s, 10s, 20s, 40s, 80s, 160s, 300s (capped)
        var delaySeconds = Math.Min(
            initialDelaySeconds * Math.Pow(2, deliveryCount - 1),
            maxDelaySeconds);

        return delaySeconds;
    }

    /// <summary>
    /// Database row structure for background jobs
    /// </summary>
    private class BackgroundJobRow
    {
        public string MessageId { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string ReceiptHandle { get; set; } = string.Empty;
        public int DeliveryCount { get; set; }
        public DateTimeOffset EnqueuedAt { get; set; }
        public DateTimeOffset? VisibleAfter { get; set; }
        public string? Attributes { get; set; }
    }
}
