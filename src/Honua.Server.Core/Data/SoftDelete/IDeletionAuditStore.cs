// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Repository interface for storing and querying deletion audit records.
/// Provides a complete audit trail for all soft and hard delete operations.
/// </summary>
public interface IDeletionAuditStore
{
    /// <summary>
    /// Records a deletion audit entry.
    /// </summary>
    /// <param name="entityType">Type of entity deleted (e.g., "Feature", "StacCollection").</param>
    /// <param name="entityId">Unique identifier of the deleted entity.</param>
    /// <param name="deletionType">Type of deletion: "soft" or "hard".</param>
    /// <param name="context">Context information about the deletion operation.</param>
    /// <param name="entityMetadataSnapshot">Optional JSON snapshot of entity metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created audit record.</returns>
    Task<long> RecordDeletionAsync(
        string entityType,
        string entityId,
        string deletionType,
        DeletionContext context,
        string? entityMetadataSnapshot = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a restoration audit entry when a soft-deleted entity is restored.
    /// </summary>
    Task<long> RecordRestorationAsync(
        string entityType,
        string entityId,
        DeletionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deletion audit records for a specific entity.
    /// </summary>
    Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all deletion audit records for a specific entity type.
    /// </summary>
    Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByEntityTypeAsync(
        string entityType,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deletion audit records for a specific user (who performed deletions).
    /// </summary>
    Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByUserAsync(
        string userId,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deletion audit records within a time range.
    /// </summary>
    Task<IReadOnlyList<DeletionAuditRecord>> GetAuditRecordsByTimeRangeAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? entityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all data subject request deletion audit records for GDPR compliance.
    /// </summary>
    Task<IReadOnlyList<DeletionAuditRecord>> GetDataSubjectRequestAuditRecordsAsync(
        string? dataSubjectRequestId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges old audit records based on retention policy.
    /// </summary>
    /// <param name="retentionPeriod">Audit records older than this will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of audit records purged.</returns>
    Task<int> PurgeOldAuditRecordsAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default);
}
