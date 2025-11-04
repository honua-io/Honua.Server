// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Rollback;

/// <summary>
/// Manages snapshots for rollback functionality.
/// Creates point-in-time snapshots before dangerous operations.
/// </summary>
public interface ISnapshotManager
{
    /// <summary>
    /// Creates a snapshot of the current state before executing a plan.
    /// </summary>
    Task<string> CreateSnapshotAsync(
        string workspacePath,
        string planId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a snapshot, reverting to the state when it was created.
    /// </summary>
    Task RestoreSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot when it's no longer needed.
    /// </summary>
    Task DeleteSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available snapshots.
    /// </summary>
    Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific snapshot.
    /// </summary>
    Task<Snapshot?> GetSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a point-in-time snapshot.
/// </summary>
public sealed class Snapshot
{
    /// <summary>
    /// Unique identifier for this snapshot.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Plan that triggered this snapshot.
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// Workspace path that was snapshotted.
    /// </summary>
    public required string WorkspacePath { get; init; }

    /// <summary>
    /// When the snapshot was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Size of the snapshot in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Type of snapshot (filesystem, database dump, etc.).
    /// </summary>
    public SnapshotType Type { get; init; }

    /// <summary>
    /// Storage location of the snapshot.
    /// </summary>
    public required string StoragePath { get; init; }

    /// <summary>
    /// Metadata about what was snapshotted.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Whether this snapshot has been restored.
    /// </summary>
    public bool IsRestored { get; set; }

    /// <summary>
    /// When the snapshot was restored (if applicable).
    /// </summary>
    public DateTime? RestoredAt { get; set; }
}

public enum SnapshotType
{
    /// <summary>
    /// Configuration file backup.
    /// </summary>
    Configuration,

    /// <summary>
    /// Database schema dump.
    /// </summary>
    DatabaseSchema,

    /// <summary>
    /// Full database backup.
    /// </summary>
    DatabaseFull,

    /// <summary>
    /// File system backup.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Combined snapshot of multiple components.
    /// </summary>
    Combined
}
