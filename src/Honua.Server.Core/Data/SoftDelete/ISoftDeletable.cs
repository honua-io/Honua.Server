// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// Marker interface for entities that support soft delete functionality.
/// Soft delete marks records as deleted without physically removing them from the database.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indicates whether this entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// The timestamp when this entity was soft-deleted, or null if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAt { get; }

    /// <summary>
    /// The user ID who soft-deleted this entity, or null if not deleted.
    /// </summary>
    string? DeletedBy { get; }
}
