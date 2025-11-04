// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// Service for managing versioned data with git-like capabilities
/// </summary>
public interface IVersioningService<T> where T : VersionedEntityBase
{
    /// <summary>
    /// Create a new versioned entity
    /// </summary>
    Task<T> CreateAsync(T entity, string createdBy, string? commitMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an entity (creates new version)
    /// </summary>
    Task<T> UpdateAsync(T entity, string updatedBy, string? commitMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete an entity (creates new version marked as deleted)
    /// </summary>
    Task<T> DeleteAsync(Guid id, string deletedBy, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current (latest) version of an entity
    /// </summary>
    Task<T?> GetCurrentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific version of an entity
    /// </summary>
    Task<T?> GetVersionAsync(Guid id, long version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entity as it was at a specific point in time
    /// </summary>
    Task<T?> GetAtTimestampAsync(Guid id, DateTimeOffset timestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get complete version history for an entity
    /// </summary>
    Task<VersionHistory<T>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get changes between two versions
    /// </summary>
    Task<ChangeSet> GetChangesAsync(Guid id, long fromVersion, long toVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare current version with a specific version
    /// </summary>
    Task<ChangeSet> CompareWithCurrentAsync(Guid id, long compareVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect conflicts between versions
    /// </summary>
    Task<List<MergeConflict>> DetectConflictsAsync(Guid id, long baseVersion, long currentVersion, long incomingVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge two versions of an entity
    /// </summary>
    Task<MergeResult<T>> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to a previous version
    /// </summary>
    Task<RollbackResult<T>> RollbackAsync(RollbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a branch from a specific version
    /// </summary>
    Task<T> CreateBranchAsync(Guid id, long fromVersion, string branchName, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all branches for an entity
    /// </summary>
    Task<List<string>> GetBranchesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the version tree (for visualizing branches and merges)
    /// </summary>
    Task<VersionTree<T>> GetVersionTreeAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Version tree structure for visualizing history
/// </summary>
public class VersionTree<T> where T : VersionedEntityBase
{
    public Guid EntityId { get; set; }
    public List<VersionNode<T>> Nodes { get; set; } = new();
    public Dictionary<string, List<VersionNode<T>>> Branches { get; set; } = new();

    /// <summary>
    /// Get the root version (first version)
    /// </summary>
    public VersionNode<T>? Root => Nodes.FirstOrDefault(n => n.ParentVersion == null);

    /// <summary>
    /// Get all leaf nodes (versions with no children)
    /// </summary>
    public List<VersionNode<T>> Leaves => Nodes.Where(n => n.ChildVersions.Count == 0).ToList();
}
