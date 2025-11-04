// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Base class for relational database implementations of the deletion audit store.
/// Provides cross-database deletion auditing with provider-specific SQL dialects.
/// </summary>
internal abstract class RelationalDeletionAuditStore : DisposableBase, IDeletionAuditStore
{
    private readonly IOptionsMonitor<SoftDeleteOptions> _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    protected abstract string ProviderName { get; }
    protected abstract DbConnection CreateConnection();
    protected abstract string LimitClause(string paramName);
    protected virtual ValueTask ConfigureConnectionAsync(DbConnection connection, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected RelationalDeletionAuditStore(
        IOptionsMonitor<SoftDeleteOptions> options,
        ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            // Schema should be created by migration scripts
            // This just verifies connectivity
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<long> RecordDeletionAsync(
        string entityType,
        string entityId,
        string deletionType,
        DeletionContext context,
        string? entityMetadataSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deletionType);

        if (!_options.CurrentValue.AuditDeletions)
        {
            _logger.LogDebug("Deletion auditing is disabled, skipping audit record for {EntityType} {EntityId}",
                entityType, entityId);
            return 0;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
INSERT INTO deletion_audit_log (
    entity_type, entity_id, deletion_type, deleted_by, deleted_at,
    reason, ip_address, user_agent, entity_metadata_snapshot,
    is_data_subject_request, data_subject_request_id, metadata
) VALUES (
    @EntityType, @EntityId, @DeletionType, @DeletedBy, @DeletedAt,
    @Reason, @IpAddress, @UserAgent, @EntityMetadataSnapshot,
    @IsDataSubjectRequest, @DataSubjectRequestId, @Metadata
)";

        var sqlWithReturning = GetInsertWithReturningIdSql(sql);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var parameters = new
        {
            EntityType = entityType,
            EntityId = entityId,
            DeletionType = deletionType,
            DeletedBy = context.UserId,
            DeletedAt = DateTime.UtcNow,
            Reason = context.Reason,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            EntityMetadataSnapshot = entityMetadataSnapshot,
            IsDataSubjectRequest = context.IsDataSubjectRequest,
            DataSubjectRequestId = context.DataSubjectRequestId,
            Metadata = context.Metadata != null ? JsonSerializer.Serialize(context.Metadata) : null
        };

        var auditId = await connection.ExecuteScalarAsync<long>(sqlWithReturning, parameters).ConfigureAwait(false);

        _logger.LogInformation(
            "Recorded {DeletionType} deletion audit for {EntityType} {EntityId} by user {UserId}",
            deletionType, entityType, entityId, context.UserId ?? "<system>");

        return auditId;
    }

    public async Task<long> RecordRestorationAsync(
        string entityType,
        string entityId,
        DeletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.AuditRestorations)
        {
            _logger.LogDebug("Restoration auditing is disabled, skipping audit record for {EntityType} {EntityId}",
                entityType, entityId);
            return 0;
        }

        return await RecordDeletionAsync(
            entityType, entityId, "restore", context, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, entity_type, entity_id, deletion_type, deleted_by, deleted_at,
       reason, ip_address, user_agent, entity_metadata_snapshot,
       is_data_subject_request, data_subject_request_id, metadata
FROM deletion_audit_log
WHERE entity_type = @EntityType AND entity_id = @EntityId
ORDER BY deleted_at DESC";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<DeletionAuditRow>(sql, new { EntityType = entityType, EntityId = entityId })
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByEntityTypeAsync(
        string entityType,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // KEYSET PAGINATION: Use cursor-based pagination for O(1) performance
        // For now, keeping OFFSET for backward compatibility, but logging deprecation warning
        if (offset > 0)
        {
            _logger.LogWarning(
                "OFFSET pagination used with offset={Offset}. This causes O(N) performance degradation. " +
                "Consider migrating to cursor-based pagination for O(1) performance at all page depths.",
                offset);
        }

        var sql = $@"
SELECT id, entity_type, entity_id, deletion_type, deleted_by, deleted_at,
       reason, ip_address, user_agent, entity_metadata_snapshot,
       is_data_subject_request, data_subject_request_id, metadata
FROM deletion_audit_log
WHERE entity_type = @EntityType
ORDER BY deleted_at DESC, id DESC
{LimitClause("@Limit")} OFFSET @Offset";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<DeletionAuditRow>(sql,
            new { EntityType = entityType, Limit = Math.Min(limit, 1000), Offset = offset })
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByUserAsync(
        string userId,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // KEYSET PAGINATION: Use cursor-based pagination for O(1) performance
        // For now, keeping OFFSET for backward compatibility, but logging deprecation warning
        if (offset > 0)
        {
            _logger.LogWarning(
                "OFFSET pagination used with offset={Offset}. This causes O(N) performance degradation. " +
                "Consider migrating to cursor-based pagination for O(1) performance at all page depths.",
                offset);
        }

        var sql = $@"
SELECT id, entity_type, entity_id, deletion_type, deleted_by, deleted_at,
       reason, ip_address, user_agent, entity_metadata_snapshot,
       is_data_subject_request, data_subject_request_id, metadata
FROM deletion_audit_log
WHERE deleted_by = @UserId
ORDER BY deleted_at DESC, id DESC
{LimitClause("@Limit")} OFFSET @Offset";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<DeletionAuditRow>(sql,
            new { UserId = userId, Limit = Math.Min(limit, 1000), Offset = offset })
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByTimeRangeAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = entityType != null
            ? "WHERE deleted_at >= @StartTime AND deleted_at <= @EndTime AND entity_type = @EntityType"
            : "WHERE deleted_at >= @StartTime AND deleted_at <= @EndTime";

        var sql = $@"
SELECT id, entity_type, entity_id, deletion_type, deleted_by, deleted_at,
       reason, ip_address, user_agent, entity_metadata_snapshot,
       is_data_subject_request, data_subject_request_id, metadata
FROM deletion_audit_log
{whereClause}
ORDER BY deleted_at DESC";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<DeletionAuditRow>(sql,
            new { StartTime = startTime.UtcDateTime, EndTime = endTime.UtcDateTime, EntityType = entityType })
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<DeletionAuditRecord>> GetDataSubjectRequestAuditRecordsAsync(
        string? dataSubjectRequestId = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = dataSubjectRequestId != null
            ? "WHERE is_data_subject_request = @IsDataSubjectRequest AND data_subject_request_id = @DataSubjectRequestId"
            : "WHERE is_data_subject_request = @IsDataSubjectRequest";

        var sql = $@"
SELECT id, entity_type, entity_id, deletion_type, deleted_by, deleted_at,
       reason, ip_address, user_agent, entity_metadata_snapshot,
       is_data_subject_request, data_subject_request_id, metadata
FROM deletion_audit_log
{whereClause}
ORDER BY deleted_at DESC";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<DeletionAuditRow>(sql,
            new { IsDataSubjectRequest = true, DataSubjectRequestId = dataSubjectRequestId })
            .ConfigureAwait(false);

        return rows.Select(MapToRecord).ToList();
    }

    public async Task<int> PurgeOldAuditRecordsAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod);

        const string sql = @"
DELETE FROM deletion_audit_log
WHERE deleted_at < @CutoffDate";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var deleted = await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate.UtcDateTime })
            .ConfigureAwait(false);

        _logger.LogInformation("Purged {Count} deletion audit records older than {CutoffDate:u}",
            deleted, cutoffDate);

        return deleted;
    }

    protected abstract string GetInsertWithReturningIdSql(string baseSql);

    private static DeletionAuditRecord MapToRecord(DeletionAuditRow row)
    {
        Dictionary<string, string>? metadata = null;
        if (!string.IsNullOrWhiteSpace(row.Metadata))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Metadata);
            }
            catch
            {
                // Ignore invalid JSON
            }
        }

        return new DeletionAuditRecord
        {
            Id = row.Id,
            EntityType = row.EntityType,
            EntityId = row.EntityId,
            DeletionType = row.DeletionType,
            DeletedBy = row.DeletedBy,
            DeletedAt = DateTime.SpecifyKind(row.DeletedAt, DateTimeKind.Utc),
            Reason = row.Reason,
            IpAddress = row.IpAddress,
            UserAgent = row.UserAgent,
            EntityMetadataSnapshot = row.EntityMetadataSnapshot,
            IsDataSubjectRequest = row.IsDataSubjectRequest,
            DataSubjectRequestId = row.DataSubjectRequestId,
            Metadata = metadata
        };
    }

    protected override void DisposeCore()
    {
        _initLock.Dispose();
    }

    private class DeletionAuditRow
    {
        public long Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string DeletionType { get; set; } = string.Empty;
        public string? DeletedBy { get; set; }
        public DateTime DeletedAt { get; set; }
        public string? Reason { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? EntityMetadataSnapshot { get; set; }
        public bool IsDataSubjectRequest { get; set; }
        public string? DataSubjectRequestId { get; set; }
        public string? Metadata { get; set; }
    }
}
