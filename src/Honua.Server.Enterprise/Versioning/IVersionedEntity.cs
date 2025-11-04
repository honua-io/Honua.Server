// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// Interface for entities that support version tracking
/// </summary>
public interface IVersionedEntity
{
    /// <summary>
    /// Unique identifier for the entity (stable across versions)
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Version number (increments with each change)
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Hash of the entity content (for detecting changes)
    /// </summary>
    string ContentHash { get; }

    /// <summary>
    /// When this version was created
    /// </summary>
    DateTimeOffset VersionCreatedAt { get; }

    /// <summary>
    /// User who created this version
    /// </summary>
    string? VersionCreatedBy { get; }

    /// <summary>
    /// Parent version (for branching/merging)
    /// </summary>
    long? ParentVersion { get; }

    /// <summary>
    /// Branch name (null for main branch)
    /// </summary>
    string? Branch { get; }

    /// <summary>
    /// Commit message describing the changes
    /// </summary>
    string? CommitMessage { get; }

    /// <summary>
    /// Whether this version is deleted
    /// </summary>
    bool IsDeleted { get; }
}

/// <summary>
/// Base class for versioned entities
/// </summary>
public abstract class VersionedEntityBase : IVersionedEntity
{
    public Guid Id { get; set; }
    public long Version { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset VersionCreatedAt { get; set; }
    public string? VersionCreatedBy { get; set; }
    public long? ParentVersion { get; set; }
    public string? Branch { get; set; }
    public string? CommitMessage { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Calculate content hash for change detection
    /// Override this to customize which fields are included in the hash
    /// </summary>
    public abstract string CalculateContentHash();

    /// <summary>
    /// Update the content hash
    /// </summary>
    public void UpdateContentHash()
    {
        ContentHash = CalculateContentHash();
    }
}
