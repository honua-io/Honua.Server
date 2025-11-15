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
using Npgsql;
using Honua.Server.Enterprise.Data;

namespace Honua.Server.Enterprise.Geoprocessing.Webhooks;

/// <summary>
/// PostgreSQL-backed webhook delivery service with retry support
/// </summary>
public class PostgresWebhookDeliveryService : IWebhookDeliveryService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresWebhookDeliveryService> _logger;

    public PostgresWebhookDeliveryService(
        string connectionString,
        ILogger<PostgresWebhookDeliveryService> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> EnqueueAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        if (delivery == null) throw new ArgumentNullException(nameof(delivery));

        // Generate ID if not provided
        if (delivery.Id == Guid.Empty)
        {
            delivery.Id = Guid.NewGuid();
        }

        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            INSERT INTO webhook_deliveries (
                id, job_id, webhook_url, payload, headers,
                status, created_at, next_retry_at,
                attempt_count, max_attempts,
                tenant_id, process_id
            ) VALUES (
                @Id, @JobId, @WebhookUrl, @Payload::jsonb, @Headers::jsonb,
                @Status, @CreatedAt, @NextRetryAt,
                @AttemptCount, @MaxAttempts,
                @TenantId, @ProcessId
            )";

        await connection.ExecuteAsync(sql, new
        {
            delivery.Id,
            delivery.JobId,
            delivery.WebhookUrl,
            Payload = JsonSerializer.Serialize(delivery.Payload),
            Headers = delivery.Headers != null ? JsonSerializer.Serialize(delivery.Headers) : null,
            Status = delivery.Status.ToString().ToLowerInvariant(),
            delivery.CreatedAt,
            delivery.NextRetryAt,
            delivery.AttemptCount,
            delivery.MaxAttempts,
            delivery.TenantId,
            delivery.ProcessId
        });

        _logger.LogInformation(
            "Enqueued webhook delivery {DeliveryId} for job {JobId} to {WebhookUrl}",
            delivery.Id, delivery.JobId, delivery.WebhookUrl);

        return delivery.Id;
    }

    public async Task<WebhookDelivery?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = "SELECT * FROM dequeue_webhook_delivery()";

        var row = await connection.QuerySingleOrDefaultAsync(sql);

        if (row == null)
        {
            _logger.LogDebug("No pending webhook deliveries in queue");
            return null;
        }

        var delivery = new WebhookDelivery
        {
            Id = (Guid)row.id,
            JobId = (string)row.job_id,
            WebhookUrl = (string)row.webhook_url,
            Payload = JsonSerializer.Deserialize<Dictionary<string, object>>((string)row.payload)
                ?? new Dictionary<string, object>(),
            Headers = row.headers != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>((string)row.headers)
                : null,
            AttemptCount = (int)row.attempt_count,
            MaxAttempts = (int)row.max_attempts,
            Status = WebhookDeliveryStatus.Processing,
            TenantId = string.Empty, // Will be populated from full query if needed
            ProcessId = string.Empty  // Will be populated from full query if needed
        };

        _logger.LogInformation(
            "Dequeued webhook delivery {DeliveryId} for job {JobId} (attempt {Attempt}/{MaxAttempts})",
            delivery.Id, delivery.JobId, delivery.AttemptCount, delivery.MaxAttempts);

        return delivery;
    }

    public async Task RecordSuccessAsync(
        Guid deliveryId,
        int responseStatus,
        string? responseBody = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        // Truncate response body to avoid storing large payloads
        var truncatedBody = responseBody?.Length > 1000
            ? responseBody.Substring(0, 1000) + "... (truncated)"
            : responseBody;

        const string sql = "SELECT record_webhook_delivery_success(@Id, @ResponseStatus, @ResponseBody)";

        await connection.ExecuteAsync(sql, new
        {
            Id = deliveryId,
            ResponseStatus = responseStatus,
            ResponseBody = truncatedBody
        });

        _logger.LogInformation(
            "Recorded successful webhook delivery {DeliveryId} (HTTP {Status})",
            deliveryId, responseStatus);
    }

    public async Task RecordFailureAsync(
        Guid deliveryId,
        int? responseStatus,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        // Truncate error message
        var truncatedError = errorMessage?.Length > 2000
            ? errorMessage.Substring(0, 2000) + "... (truncated)"
            : errorMessage;

        const string sql = "SELECT record_webhook_delivery_failure(@Id, @ResponseStatus, @ErrorMessage)";

        await connection.ExecuteAsync(sql, new
        {
            Id = deliveryId,
            ResponseStatus = responseStatus,
            ErrorMessage = truncatedError
        });

        _logger.LogWarning(
            "Recorded failed webhook delivery {DeliveryId} (HTTP {Status}): {Error}",
            deliveryId, responseStatus, truncatedError);
    }

    public async Task<WebhookDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = "SELECT * FROM webhook_deliveries WHERE id = @Id";

        var row = await connection.QuerySingleOrDefaultAsync(sql, new { Id = deliveryId });

        if (row == null)
        {
            return null;
        }

        return MapToWebhookDelivery(row);
    }

    public async Task<WebhookDelivery[]> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT * FROM webhook_deliveries
            WHERE job_id = @JobId
            ORDER BY created_at DESC";

        var rows = await connection.QueryAsync(sql, new { JobId = jobId });

        return rows.Select(MapToWebhookDelivery).ToArray();
    }

    private static WebhookDelivery MapToWebhookDelivery(dynamic row)
    {
        var statusStr = (string)row.status;
        var status = Enum.Parse<WebhookDeliveryStatus>(statusStr, ignoreCase: true);

        return new WebhookDelivery
        {
            Id = (Guid)row.id,
            JobId = (string)row.job_id,
            WebhookUrl = (string)row.webhook_url,
            Payload = row.payload != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>((string)row.payload)
                    ?? new Dictionary<string, object>()
                : new Dictionary<string, object>(),
            Headers = row.headers != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>((string)row.headers)
                : null,
            Status = status,
            CreatedAt = (DateTimeOffset)row.created_at,
            NextRetryAt = (DateTimeOffset)row.next_retry_at,
            LastAttemptAt = row.last_attempt_at,
            CompletedAt = row.completed_at,
            AttemptCount = (int)row.attempt_count,
            MaxAttempts = (int)row.max_attempts,
            LastResponseStatus = row.last_response_status,
            LastResponseBody = row.last_response_body,
            LastErrorMessage = row.last_error_message,
            TenantId = (string)row.tenant_id,
            ProcessId = (string)row.process_id
        };
    }
}
