// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Represents an audit record for a deletion operation (soft or hard delete).
/// Provides a complete audit trail for data deletion compliance (GDPR, SOC2, etc).
/// </summary>
public sealed record DeletionAuditRecord
{
    /// <summary>
    /// Unique identifier for this audit record.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The type of entity that was deleted (e.g., "Feature", "StacCollection", "AuthUser").
    /// </summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>
    /// The unique identifier of the deleted entity.
    /// </summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    /// The type of deletion: "soft" or "hard".
    /// </summary>
    public string DeletionType { get; init; } = string.Empty;

    /// <summary>
    /// The user ID who performed the deletion.
    /// </summary>
    public string? DeletedBy { get; init; }

    /// <summary>
    /// The timestamp when the deletion occurred.
    /// </summary>
    public DateTimeOffset DeletedAt { get; init; }

    /// <summary>
    /// Optional reason for the deletion.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// IP address from which the deletion was performed.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User agent string of the client that performed the deletion.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Snapshot of entity metadata at the time of deletion (JSON).
    /// Used for audit purposes and potential recovery.
    /// </summary>
    public string? EntityMetadataSnapshot { get; init; }

    /// <summary>
    /// Additional context or metadata about the deletion operation.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// For GDPR compliance: indicates if deletion was part of a data subject request.
    /// </summary>
    public bool IsDataSubjectRequest { get; init; }

    /// <summary>
    /// For GDPR compliance: the data subject request ID if applicable.
    /// </summary>
    public string? DataSubjectRequestId { get; init; }
}

/// <summary>
/// Context information for a deletion operation.
/// Used to populate audit records with actor and request information.
/// </summary>
public sealed record DeletionContext
{
    /// <summary>
    /// The user ID performing the deletion.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Optional reason for the deletion.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// IP address from which the deletion is being performed.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User agent string of the client performing the deletion.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// For GDPR compliance: indicates if deletion is part of a data subject request.
    /// </summary>
    public bool IsDataSubjectRequest { get; init; }

    /// <summary>
    /// For GDPR compliance: the data subject request ID if applicable.
    /// </summary>
    public string? DataSubjectRequestId { get; init; }

    /// <summary>
    /// Additional metadata about the deletion operation.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets an empty deletion context.
    /// </summary>
    public static readonly DeletionContext Empty = new();
}
