// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// Represents changes between two versions of an entity
/// </summary>
public class ChangeSet
{
    public Guid EntityId { get; set; }
    public long FromVersion { get; set; }
    public long ToVersion { get; set; }
    public DateTimeOffset FromTimestamp { get; set; }
    public DateTimeOffset ToTimestamp { get; set; }
    public string? ChangedBy { get; set; }
    public string? CommitMessage { get; set; }
    public List<FieldChange> FieldChanges { get; set; } = new();

    /// <summary>
    /// Whether there are any changes
    /// </summary>
    public bool HasChanges => FieldChanges.Any();

    /// <summary>
    /// Number of fields changed
    /// </summary>
    public int ChangeCount => FieldChanges.Count;
}

/// <summary>
/// Represents a change to a single field
/// </summary>
public class FieldChange
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldPath { get; set; } = string.Empty; // JSON path for nested objects
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public ChangeType ChangeType { get; set; }

    public override string ToString()
    {
        return ChangeType switch
        {
            ChangeType.Added => $"{FieldName}: (added) -> {NewValue}",
            ChangeType.Removed => $"{FieldName}: {OldValue} -> (removed)",
            ChangeType.Modified => $"{FieldName}: {OldValue} -> {NewValue}",
            _ => $"{FieldName}: no change"
        };
    }
}

/// <summary>
/// Type of change to a field
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// No change
    /// </summary>
    None = 0,

    /// <summary>
    /// Field was added
    /// </summary>
    Added = 1,

    /// <summary>
    /// Field was removed
    /// </summary>
    Removed = 2,

    /// <summary>
    /// Field value was modified
    /// </summary>
    Modified = 3
}

/// <summary>
/// Represents the complete version history of an entity
/// </summary>
public class VersionHistory<T> where T : VersionedEntityBase
{
    public Guid EntityId { get; set; }
    public List<VersionNode<T>> Versions { get; set; } = new();
    public DateTimeOffset FirstVersionAt { get; set; }
    public DateTimeOffset LastVersionAt { get; set; }
    public int TotalVersions => Versions.Count;

    /// <summary>
    /// Get the current (latest) version
    /// </summary>
    public T? Current => Versions
        .Where(v => !v.Entity.IsDeleted)
        .MaxBy(v => v.Entity.Version)?.Entity;

    /// <summary>
    /// Get version at specific point in time
    /// </summary>
    public T? GetVersionAt(DateTimeOffset timestamp)
    {
        return Versions
            .Where(v => v.Entity.VersionCreatedAt <= timestamp)
            .MaxBy(v => v.Entity.VersionCreatedAt)?.Entity;
    }

    /// <summary>
    /// Get specific version number
    /// </summary>
    public T? GetVersion(long version)
    {
        return Versions.FirstOrDefault(v => v.Entity.Version == version)?.Entity;
    }
}

/// <summary>
/// Node in the version tree (supports branching)
/// </summary>
public class VersionNode<T> where T : VersionedEntityBase
{
    public T Entity { get; set; } = default!;
    public long? ParentVersion { get; set; }
    public List<long> ChildVersions { get; set; } = new();
    public string? Branch { get; set; }

    /// <summary>
    /// Whether this is a branch point (has multiple children)
    /// </summary>
    public bool IsBranchPoint => ChildVersions.Count > 1;

    /// <summary>
    /// Whether this is a merge point (has multiple parents)
    /// </summary>
    public bool IsMergePoint { get; set; }
}
