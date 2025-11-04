// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// Result of a merge operation
/// </summary>
public class MergeResult<T> where T : IVersionedEntity
{
    public bool Success { get; set; }
    public T? MergedEntity { get; set; }
    public List<MergeConflict> Conflicts { get; set; } = new();
    public List<FieldChange> AutoMergedChanges { get; set; } = new();
    public MergeStrategy Strategy { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether there are unresolved conflicts
    /// </summary>
    public bool HasConflicts => Conflicts.Any(c => !c.IsResolved);

    /// <summary>
    /// Whether all conflicts were auto-merged
    /// </summary>
    public bool IsAutoMerged => !HasConflicts;

    /// <summary>
    /// Summary of merge operation
    /// </summary>
    public string GetSummary()
    {
        if (!Success)
            return $"Merge failed: {ErrorMessage}";

        if (HasConflicts)
            return $"Merge incomplete: {Conflicts.Count(c => !c.IsResolved)} conflicts require manual resolution";

        return $"Merge successful: {AutoMergedChanges.Count} changes auto-merged";
    }
}

/// <summary>
/// Represents a conflict between two versions
/// </summary>
public class MergeConflict
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldPath { get; set; } = string.Empty;
    public object? BaseValue { get; set; }
    public object? CurrentValue { get; set; }
    public object? IncomingValue { get; set; }
    public ConflictType Type { get; set; }
    public bool IsResolved { get; set; }
    public object? ResolvedValue { get; set; }
    public ResolutionStrategy? ResolutionStrategy { get; set; }

    public override string ToString()
    {
        return Type switch
        {
            ConflictType.BothModified => $"{FieldName}: Both versions modified (base: {BaseValue}, current: {CurrentValue}, incoming: {IncomingValue})",
            ConflictType.ModifiedAndDeleted => $"{FieldName}: Modified in one version, deleted in another",
            ConflictType.DeletedAndModified => $"{FieldName}: Deleted in one version, modified in another",
            ConflictType.BothAdded => $"{FieldName}: Added in both versions with different values",
            _ => $"{FieldName}: Unknown conflict type"
        };
    }
}

/// <summary>
/// Type of merge conflict
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Both versions modified the same field
    /// </summary>
    BothModified,

    /// <summary>
    /// One version modified, other deleted
    /// </summary>
    ModifiedAndDeleted,

    /// <summary>
    /// One version deleted, other modified
    /// </summary>
    DeletedAndModified,

    /// <summary>
    /// Both versions added the same field with different values
    /// </summary>
    BothAdded,

    /// <summary>
    /// Type mismatch between versions
    /// </summary>
    TypeMismatch
}

/// <summary>
/// Strategy for resolving conflicts
/// </summary>
public enum ResolutionStrategy
{
    /// <summary>
    /// Use the current version's value
    /// </summary>
    UseOurs,

    /// <summary>
    /// Use the incoming version's value
    /// </summary>
    UseTheirs,

    /// <summary>
    /// Use the base version's value
    /// </summary>
    UseBase,

    /// <summary>
    /// Use a custom merged value
    /// </summary>
    Custom,

    /// <summary>
    /// Keep both values (for arrays/lists)
    /// </summary>
    KeepBoth,

    /// <summary>
    /// Manual resolution required
    /// </summary>
    Manual
}

/// <summary>
/// Strategy for merging changes
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// Automatically merge non-conflicting changes
    /// </summary>
    AutoMerge,

    /// <summary>
    /// On conflict, use current version (ours)
    /// </summary>
    Ours,

    /// <summary>
    /// On conflict, use incoming version (theirs)
    /// </summary>
    Theirs,

    /// <summary>
    /// Require manual resolution for all conflicts
    /// </summary>
    Manual,

    /// <summary>
    /// Use custom merge function
    /// </summary>
    Custom
}

/// <summary>
/// Request to merge two versions
/// </summary>
public class MergeRequest
{
    public Guid EntityId { get; set; }
    public long BaseVersion { get; set; }
    public long CurrentVersion { get; set; }
    public long IncomingVersion { get; set; }
    public MergeStrategy Strategy { get; set; } = MergeStrategy.AutoMerge;
    public Dictionary<string, ResolutionStrategy>? FieldResolutions { get; set; }
    public string? CommitMessage { get; set; }
    public string? MergedBy { get; set; }
}

/// <summary>
/// Request to rollback to a previous version
/// </summary>
public class RollbackRequest
{
    public Guid EntityId { get; set; }
    public long ToVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RolledBackBy { get; set; } = string.Empty;
    public bool CreateBranch { get; set; } // Whether to create a branch instead of overwriting
}

/// <summary>
/// Result of a rollback operation
/// </summary>
public class RollbackResult<T> where T : IVersionedEntity
{
    public bool Success { get; set; }
    public T? RestoredEntity { get; set; }
    public long NewVersion { get; set; }
    public string? ErrorMessage { get; set; }
}
