// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Data;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Queue.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Events.Queue.Repositories;

/// <summary>
/// PostgreSQL implementation of geofence event queue repository
/// </summary>
public class PostgresGeofenceEventQueueRepository : IGeofenceEventQueueRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresGeofenceEventQueueRepository> _logger;
    private readonly WKBReader _wkbReader;

    public PostgresGeofenceEventQueueRepository(
        string connectionString,
        ILogger<PostgresGeofenceEventQueueRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wkbReader = new WKBReader();
    }

    public async Task<Guid> EnqueueEventAsync(
        GeofenceEvent geofenceEvent,
        List<string>? deliveryTargets = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        // Generate fingerprint for deduplication
        var fingerprint = GenerateFingerprint(geofenceEvent);

        // Use geofence_id as partition key for FIFO ordering per geofence
        var partitionKey = geofenceEvent.GeofenceId.ToString();

        // Default delivery targets
        deliveryTargets ??= new List<string> { "signalr" };

        var queueId = await connection.QuerySingleAsync<Guid>(
            "SELECT honua_enqueue_geofence_event(@GeofenceEventId, @PartitionKey, @Fingerprint, @DeliveryTargets::jsonb, @Priority, @TenantId)",
            new
            {
                GeofenceEventId = geofenceEvent.Id,
                PartitionKey = partitionKey,
                Fingerprint = fingerprint,
                DeliveryTargets = JsonSerializer.Serialize(deliveryTargets),
                Priority = priority,
                geofenceEvent.TenantId
            });

        _logger.LogDebug(
            "Enqueued geofence event {EventId} with queue ID {QueueId} (fingerprint: {Fingerprint})",
            geofenceEvent.Id,
            queueId,
            fingerprint);

        return queueId;
    }

    public async Task<List<GeofenceEventQueueItem>> PollPendingEventsAsync(
        int batchSize = 10,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<QueueItemDto>(
            "SELECT * FROM honua_poll_pending_events(@BatchSize, @TenantId)",
            new { BatchSize = batchSize, TenantId = tenantId });

        var queueItems = results.Select(dto => new GeofenceEventQueueItem
        {
            Id = dto.QueueId,
            GeofenceEventId = dto.GeofenceEventId,
            PartitionKey = dto.PartitionKey,
            DeliveryTargets = JsonSerializer.Deserialize<List<string>>(dto.DeliveryTargets) ?? new List<string>(),
            AttemptCount = dto.AttemptCount
        }).ToList();

        _logger.LogDebug("Polled {Count} pending events for delivery", queueItems.Count);

        return queueItems;
    }

    public async Task MarkEventDeliveredAsync(
        Guid queueItemId,
        string target,
        int recipientCount,
        int latencyMs,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "SELECT honua_mark_event_delivered(@QueueId, @Target, @RecipientCount, @LatencyMs, @Metadata::jsonb)",
            new
            {
                QueueId = queueItemId,
                Target = target,
                RecipientCount = recipientCount,
                LatencyMs = latencyMs,
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
            });

        _logger.LogInformation(
            "Marked event {QueueId} as delivered to {Target} ({RecipientCount} recipients, {LatencyMs}ms)",
            queueItemId,
            target,
            recipientCount,
            latencyMs);
    }

    public async Task MarkEventFailedAsync(
        Guid queueItemId,
        string target,
        string errorMessage,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "SELECT honua_mark_event_failed(@QueueId, @Target, @ErrorMessage, @Metadata::jsonb)",
            new
            {
                QueueId = queueItemId,
                Target = target,
                ErrorMessage = errorMessage,
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
            });

        _logger.LogWarning(
            "Marked event {QueueId} as failed for {Target}: {ErrorMessage}",
            queueItemId,
            target,
            errorMessage);
    }

    public async Task<QueueMetrics> GetQueueMetricsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var metrics = await connection.QuerySingleAsync<QueueMetrics>(
            "SELECT * FROM honua_get_queue_metrics(@TenantId)",
            new { TenantId = tenantId });

        return metrics;
    }

    public async Task<List<GeofenceEventDeliveryLog>> GetDeliveryLogAsync(
        Guid queueItemId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                id, queue_item_id, attempt_number, target, status,
                recipient_count, latency_ms, error_message, metadata, attempted_at
            FROM geofence_event_delivery_log
            WHERE queue_item_id = @QueueItemId
            ORDER BY attempted_at DESC";

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<DeliveryLogDto>(sql, new { QueueItemId = queueItemId });

        return results.Select(dto => new GeofenceEventDeliveryLog
        {
            Id = dto.Id,
            QueueItemId = dto.QueueItemId,
            AttemptNumber = dto.AttemptNumber,
            Target = dto.Target,
            Status = Enum.Parse<DeliveryStatus>(dto.Status, ignoreCase: true),
            RecipientCount = dto.RecipientCount,
            LatencyMs = dto.LatencyMs,
            ErrorMessage = dto.ErrorMessage,
            Metadata = string.IsNullOrEmpty(dto.Metadata)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Metadata),
            AttemptedAt = dto.AttemptedAt
        }).ToList();
    }

    public async Task<List<GeofenceEventQueueItem>> GetDeadLetterQueueAsync(
        int limit = 100,
        int offset = 0,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                id, geofence_event_id, status, priority, partition_key,
                fingerprint, attempt_count, max_attempts, next_attempt_at,
                retry_delay_seconds, delivery_targets, delivery_results,
                last_error, tenant_id, created_at, updated_at, completed_at
            FROM geofence_event_queue
            WHERE status = 'dlq'";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        sql += " ORDER BY created_at DESC LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<QueueItemFullDto>(
            sql,
            new { TenantId = tenantId, Limit = limit, Offset = offset });

        return results.Select(MapToQueueItem).ToList();
    }

    public async Task<int> CleanupCompletedItemsAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var deletedCount = await connection.ExecuteScalarAsync<int>(
            "SELECT honua_cleanup_completed_queue_items(@RetentionDays)",
            new { RetentionDays = retentionDays });

        _logger.LogInformation(
            "Cleaned up {Count} completed queue items older than {Days} days",
            deletedCount,
            retentionDays);

        return deletedCount;
    }

    public async Task<List<GeofenceEvent>> ReplayEventsAsync(
        EventReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var eventTypes = request.EventTypes?.Select(t => t.ToString().ToLowerInvariant()).ToArray();

        var results = await connection.QueryAsync<ReplayEventDto>(
            @"SELECT * FROM honua_replay_geofence_events(
                @EntityId, @GeofenceId, @StartTime, @EndTime, @EventTypes, @TenantId)",
            new
            {
                request.EntityId,
                request.GeofenceId,
                request.StartTime,
                request.EndTime,
                EventTypes = eventTypes,
                request.TenantId
            });

        return results.Select(dto => new GeofenceEvent
        {
            Id = dto.EventId,
            EventType = Enum.Parse<GeofenceEventType>(dto.EventType, ignoreCase: true),
            EventTime = dto.EventTime,
            GeofenceId = dto.GeofenceId,
            GeofenceName = dto.GeofenceName,
            EntityId = dto.EntityId,
            EntityType = dto.EntityType,
            Location = ParseGeoJsonPoint(dto.LocationGeoJson),
            Properties = dto.Properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Properties.ToString())
                : null,
            DwellTimeSeconds = dto.DwellTimeSeconds
        }).ToList();
    }

    private Point ParseGeoJsonPoint(string geoJson)
    {
        using var doc = JsonDocument.Parse(geoJson);
        var coordinates = doc.RootElement.GetProperty("coordinates");
        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        return geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
    }

    private string GenerateFingerprint(GeofenceEvent geofenceEvent)
    {
        // Generate SHA-256 hash of event key properties for deduplication
        var fingerprintData = $"{geofenceEvent.EventType}|{geofenceEvent.EntityId}|{geofenceEvent.GeofenceId}|{geofenceEvent.EventTime:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private GeofenceEventQueueItem MapToQueueItem(QueueItemFullDto dto)
    {
        return new GeofenceEventQueueItem
        {
            Id = dto.Id,
            GeofenceEventId = dto.GeofenceEventId,
            Status = Enum.Parse<QueueItemStatus>(dto.Status, ignoreCase: true),
            Priority = dto.Priority,
            PartitionKey = dto.PartitionKey,
            Fingerprint = dto.Fingerprint,
            AttemptCount = dto.AttemptCount,
            MaxAttempts = dto.MaxAttempts,
            NextAttemptAt = dto.NextAttemptAt,
            RetryDelaySeconds = dto.RetryDelaySeconds,
            DeliveryTargets = JsonSerializer.Deserialize<List<string>>(dto.DeliveryTargets) ?? new List<string>(),
            DeliveryResults = string.IsNullOrEmpty(dto.DeliveryResults)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, DeliveryResult>>(dto.DeliveryResults),
            LastError = dto.LastError,
            TenantId = dto.TenantId,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            CompletedAt = dto.CompletedAt
        };
    }

    // DTOs for database mapping
    private class QueueItemDto
    {
        public Guid QueueId { get; set; }
        public Guid GeofenceEventId { get; set; }
        public string PartitionKey { get; set; } = string.Empty;
        public string DeliveryTargets { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
    }

    private class QueueItemFullDto
    {
        public Guid Id { get; set; }
        public Guid GeofenceEventId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string PartitionKey { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime NextAttemptAt { get; set; }
        public int RetryDelaySeconds { get; set; }
        public string DeliveryTargets { get; set; } = string.Empty;
        public string? DeliveryResults { get; set; }
        public string? LastError { get; set; }
        public string? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    private class DeliveryLogDto
    {
        public Guid Id { get; set; }
        public Guid QueueItemId { get; set; }
        public int AttemptNumber { get; set; }
        public string Target { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? RecipientCount { get; set; }
        public int? LatencyMs { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Metadata { get; set; }
        public DateTime AttemptedAt { get; set; }
    }

    private class ReplayEventDto
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime EventTime { get; set; }
        public Guid GeofenceId { get; set; }
        public string GeofenceName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string LocationGeoJson { get; set; } = string.Empty;
        public object? Properties { get; set; }
        public int? DwellTimeSeconds { get; set; }
    }
}
