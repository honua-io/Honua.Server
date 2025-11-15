// <copyright file="AlertDeadLetterQueueService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Data;
using System.Text.Json;
using Dapper;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Interface for the alert delivery dead letter queue service.
/// </summary>
public interface IAlertDeadLetterQueueService
{
    /// <summary>
    /// Enqueues a failed alert delivery to the DLQ.
    /// </summary>
    /// <param name="webhook">The alert webhook that failed to deliver.</param>
    /// <param name="channel">The target channel that failed.</param>
    /// <param name="error">The exception that caused the failure.</param>
    /// <param name="severity">The alert severity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the enqueued failure.</returns>
    Task<long> EnqueueAsync(
        AlertManagerWebhook webhook,
        string channel,
        Exception error,
        string severity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending alerts ready for retry.
    /// </summary>
    /// <param name="limit">Maximum number of alerts to retrieve.</param>
    /// <param name="maxRetryCount">Maximum retry count to consider (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of failed deliveries ready for retry.</returns>
    Task<IReadOnlyList<AlertDeliveryFailure>> GetPendingRetriesAsync(
        int limit = 50,
        int maxRetryCount = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a failed delivery as resolved after successful retry.
    /// </summary>
    /// <param name="id">The failure ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkResolvedAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a failed delivery after max retries.
    /// </summary>
    /// <param name="id">The failure ID.</param>
    /// <param name="reason">Reason for abandonment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AbandonAsync(long id, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates retry state after an attempt.
    /// </summary>
    /// <param name="id">The failure ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateRetryStateAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about failed deliveries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary with statistics by channel and status.</returns>
    Task<Dictionary<string, object>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old resolved/abandoned failures based on retention policy.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain (default: 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records deleted.</returns>
    Task<int> CleanupOldFailuresAsync(int retentionDays = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL-based dead letter queue service for failed alert deliveries.
/// Implements retry logic with exponential backoff and tracks delivery failures.
/// </summary>
public sealed class AlertDeadLetterQueueService : IAlertDeadLetterQueueService
{
    private readonly IAlertReceiverDbConnectionFactory connectionFactory;
    private readonly ILogger<AlertDeadLetterQueueService> logger;

    private static readonly object SchemaLock = new();
    private static volatile bool schemaInitialized;

    private const string EnsureSchemaSql = @"
CREATE TABLE IF NOT EXISTS alert_delivery_failures (
    id BIGSERIAL PRIMARY KEY,
    alert_fingerprint TEXT NOT NULL,
    alert_payload JSONB NOT NULL,
    target_channel TEXT NOT NULL,
    error_message TEXT NOT NULL,
    error_details JSONB NULL,
    retry_count INTEGER NOT NULL DEFAULT 0,
    failed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_retry_at TIMESTAMPTZ NULL,
    next_retry_at TIMESTAMPTZ NULL,
    resolved_at TIMESTAMPTZ NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    severity TEXT NOT NULL,
    CONSTRAINT chk_status CHECK (status IN ('pending', 'retrying', 'resolved', 'abandoned'))
);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_fingerprint ON alert_delivery_failures(alert_fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_status ON alert_delivery_failures(status);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_next_retry ON alert_delivery_failures(next_retry_at)
    WHERE status = 'pending' AND next_retry_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_failed_at ON alert_delivery_failures(failed_at);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_channel_status ON alert_delivery_failures(target_channel, status);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_payload ON alert_delivery_failures USING GIN(alert_payload);
";

    public AlertDeadLetterQueueService(
        IAlertReceiverDbConnectionFactory connectionFactory,
        ILogger<AlertDeadLetterQueueService> logger)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<long> EnqueueAsync(
        AlertManagerWebhook webhook,
        string channel,
        Exception error,
        string severity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var fingerprint = webhook.GroupKey ?? $"{webhook.Receiver}:{webhook.Status}";
        var payloadJson = JsonSerializer.Serialize(webhook);
        var errorDetailsJson = AlertDeliveryFailure.CreateErrorDetailsJson(error);

        var failure = new AlertDeliveryFailure
        {
            AlertFingerprint = fingerprint,
            AlertPayloadJson = payloadJson,
            TargetChannel = channel,
            ErrorMessage = error.Message,
            ErrorDetailsJson = errorDetailsJson,
            RetryCount = 0,
            FailedAt = DateTimeOffset.UtcNow,
            Status = AlertDeliveryFailureStatus.Pending,
            Severity = severity,
        };

        failure.NextRetryAt = failure.CalculateNextRetryTime();

        const string sql = @"
INSERT INTO alert_delivery_failures (
    alert_fingerprint,
    alert_payload,
    target_channel,
    error_message,
    error_details,
    retry_count,
    failed_at,
    next_retry_at,
    status,
    severity
) VALUES (
    @AlertFingerprint,
    CAST(@AlertPayloadJson AS jsonb),
    @TargetChannel,
    @ErrorMessage,
    CAST(@ErrorDetailsJson AS jsonb),
    @RetryCount,
    @FailedAt,
    @NextRetryAt,
    @Status,
    @Severity
) RETURNING id;";

        var id = await connection.ExecuteScalarAsync<long>(sql, new
        {
            failure.AlertFingerprint,
            failure.AlertPayloadJson,
            failure.TargetChannel,
            failure.ErrorMessage,
            failure.ErrorDetailsJson,
            failure.RetryCount,
            failure.FailedAt,
            failure.NextRetryAt,
            Status = failure.Status.ToString().ToLowerInvariant(),
            failure.Severity,
        }).ConfigureAwait(false);

        this.logger.LogWarning(
            "Alert delivery failed - enqueued to DLQ. ID: {Id}, Channel: {Channel}, Fingerprint: {Fingerprint}, Error: {Error}",
            id,
            channel,
            fingerprint,
            error.Message);

        return id;
    }

    public async Task<IReadOnlyList<AlertDeliveryFailure>> GetPendingRetriesAsync(
        int limit = 50,
        int maxRetryCount = 5,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT
    id,
    alert_fingerprint,
    alert_payload AS alert_payload_json,
    target_channel,
    error_message,
    error_details AS error_details_json,
    retry_count,
    failed_at,
    last_retry_at,
    next_retry_at,
    resolved_at,
    status,
    severity
FROM alert_delivery_failures
WHERE status = 'pending'
  AND next_retry_at IS NOT NULL
  AND next_retry_at <= @Now
  AND retry_count < @MaxRetryCount
ORDER BY severity DESC, next_retry_at ASC
LIMIT @Limit;";

        var rows = await connection.QueryAsync(sql, new
        {
            Now = DateTimeOffset.UtcNow,
            MaxRetryCount = maxRetryCount,
            Limit = limit,
        }).ConfigureAwait(false);

        var failures = rows.Select(row => new AlertDeliveryFailure
        {
            Id = row.id,
            AlertFingerprint = row.alert_fingerprint,
            AlertPayloadJson = row.alert_payload_json,
            TargetChannel = row.target_channel,
            ErrorMessage = row.error_message,
            ErrorDetailsJson = row.error_details_json,
            RetryCount = row.retry_count,
            FailedAt = row.failed_at,
            LastRetryAt = row.last_retry_at,
            NextRetryAt = row.next_retry_at,
            ResolvedAt = row.resolved_at,
            Status = Enum.Parse<AlertDeliveryFailureStatus>(row.status, ignoreCase: true),
            Severity = row.severity,
        }).ToList();

        return failures;
    }

    public async Task MarkResolvedAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE alert_delivery_failures
SET status = 'resolved',
    resolved_at = @ResolvedAt
WHERE id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            ResolvedAt = DateTimeOffset.UtcNow,
        }).ConfigureAwait(false);

        this.logger.LogInformation("Alert delivery {Id} successfully resolved after retry", id);
    }

    public async Task AbandonAsync(long id, string? reason = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE alert_delivery_failures
SET status = 'abandoned',
    resolved_at = @ResolvedAt,
    error_message = CASE
        WHEN @Reason IS NOT NULL THEN error_message || ' | Abandoned: ' || @Reason
        ELSE error_message || ' | Abandoned: Max retries exceeded'
    END
WHERE id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            ResolvedAt = DateTimeOffset.UtcNow,
            Reason = reason,
        }).ConfigureAwait(false);

        this.logger.LogWarning("Alert delivery {Id} abandoned. Reason: {Reason}", id, reason ?? "Max retries exceeded");
    }

    public async Task UpdateRetryStateAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Get current state
        const string selectSql = @"
SELECT retry_count FROM alert_delivery_failures WHERE id = @Id;";

        var retryCount = await connection.ExecuteScalarAsync<int>(selectSql, new { Id = id }).ConfigureAwait(false);

        // Calculate next retry time using exponential backoff
        var newRetryCount = retryCount + 1;
        var baseDelayMinutes = 5;
        var delayMinutes = baseDelayMinutes * Math.Pow(2, newRetryCount);
        var maxDelayMinutes = 120;
        var actualDelay = Math.Min(delayMinutes, maxDelayMinutes);
        var nextRetryAt = DateTimeOffset.UtcNow.AddMinutes(actualDelay);

        const string updateSql = @"
UPDATE alert_delivery_failures
SET retry_count = @RetryCount,
    last_retry_at = @LastRetryAt,
    next_retry_at = @NextRetryAt,
    status = 'pending'
WHERE id = @Id;";

        await connection.ExecuteAsync(updateSql, new
        {
            Id = id,
            RetryCount = newRetryCount,
            LastRetryAt = DateTimeOffset.UtcNow,
            NextRetryAt = nextRetryAt,
        }).ConfigureAwait(false);

        this.logger.LogInformation(
            "Alert delivery {Id} retry state updated. RetryCount: {RetryCount}, NextRetry: {NextRetry}",
            id,
            newRetryCount,
            nextRetryAt);
    }

    public async Task<Dictionary<string, object>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT
    COUNT(*) as total_failures,
    COUNT(*) FILTER (WHERE status = 'pending') as pending,
    COUNT(*) FILTER (WHERE status = 'retrying') as retrying,
    COUNT(*) FILTER (WHERE status = 'resolved') as resolved,
    COUNT(*) FILTER (WHERE status = 'abandoned') as abandoned,
    target_channel,
    severity
FROM alert_delivery_failures
WHERE failed_at > @Since
GROUP BY target_channel, severity;";

        var rows = await connection.QueryAsync(sql, new
        {
            Since = DateTimeOffset.UtcNow.AddDays(-7),
        }).ConfigureAwait(false);

        var stats = new Dictionary<string, object>
        {
            ["by_channel"] = rows.GroupBy(r => (string)r.target_channel)
                .ToDictionary(g => g.Key, g => g.Sum(r => (int)r.total_failures)),
            ["by_status"] = new Dictionary<string, int>
            {
                ["pending"] = rows.Sum(r => (int)r.pending),
                ["retrying"] = rows.Sum(r => (int)r.retrying),
                ["resolved"] = rows.Sum(r => (int)r.resolved),
                ["abandoned"] = rows.Sum(r => (int)r.abandoned),
            },
            ["total"] = rows.Sum(r => (int)r.total_failures),
        };

        return stats;
    }

    public async Task<int> CleanupOldFailuresAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        const string sql = @"
DELETE FROM alert_delivery_failures
WHERE status IN ('resolved', 'abandoned')
  AND (resolved_at < @CutoffDate OR (resolved_at IS NULL AND failed_at < @CutoffDate));";

        var deletedCount = await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate }).ConfigureAwait(false);

        this.logger.LogInformation(
            "Cleaned up {Count} old alert delivery failures older than {Days} days",
            deletedCount,
            retentionDays);

        return deletedCount;
    }

    private async Task<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = this.connectionFactory.CreateConnection();
        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            throw new InvalidOperationException("Alert DLQ requires DbConnection-compatible factory.");
        }

        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        this.EnsureSchema(dbConnection);
        return dbConnection;
    }

    private void EnsureSchema(IDbConnection connection)
    {
        if (schemaInitialized)
        {
            return;
        }

        lock (SchemaLock)
        {
            if (schemaInitialized)
            {
                return;
            }

            connection.Execute(EnsureSchemaSql);
            schemaInitialized = true;
            this.logger.LogInformation("Alert delivery DLQ schema verified.");
        }
    }
}
