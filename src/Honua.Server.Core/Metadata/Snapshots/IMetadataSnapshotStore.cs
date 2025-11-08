// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Metadata.Snapshots;

/// <summary>
/// Manages metadata snapshots for point-in-time backup and restore operations.
/// Provides functionality to create, list, retrieve, and restore metadata snapshots.
/// </summary>
public interface IMetadataSnapshotStore
{
    /// <summary>
    /// Creates a new metadata snapshot based on the provided request.
    /// </summary>
    /// <param name="request">The snapshot creation request containing label and metadata details</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A descriptor containing metadata about the created snapshot</returns>
    Task<MetadataSnapshotDescriptor> CreateAsync(MetadataSnapshotRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of all available metadata snapshots.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A read-only list of snapshot descriptors ordered by creation time</returns>
    Task<IReadOnlyList<MetadataSnapshotDescriptor>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed information about a specific metadata snapshot.
    /// </summary>
    /// <param name="label">The unique label identifying the snapshot</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The snapshot details if found, otherwise null</returns>
    Task<MetadataSnapshotDetails?> GetAsync(string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores metadata from a snapshot to the current system state.
    /// </summary>
    /// <param name="label">The unique label identifying the snapshot to restore</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task representing the asynchronous restore operation</returns>
    Task RestoreAsync(string label, CancellationToken cancellationToken = default);
}
