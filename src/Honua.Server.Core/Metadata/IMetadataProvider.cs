// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Provides read-only access to metadata configuration.
/// </summary>
public interface IMetadataProvider
{
    /// <summary>
    /// Loads the current metadata snapshot.
    /// </summary>
    Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether this provider supports change notifications for hot-reload.
    /// </summary>
    bool SupportsChangeNotifications { get; }

    /// <summary>
    /// Event raised when metadata changes externally (for hot-reload).
    /// Only raised if SupportsChangeNotifications is true.
    /// </summary>
    event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
}

/// <summary>
/// Provides read-write access to metadata configuration with optional versioning support.
/// </summary>
public interface IMutableMetadataProvider : IMetadataProvider
{
    /// <summary>
    /// Saves a complete metadata snapshot, replacing the current state.
    /// </summary>
    Task SaveAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a single layer definition in metadata.
    /// </summary>
    Task UpdateLayerAsync(LayerDefinition layer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether this provider supports versioning/rollback.
    /// </summary>
    bool SupportsVersioning { get; }

    /// <summary>
    /// Creates a version of current metadata state (for rollback).
    /// Only supported if SupportsVersioning is true.
    /// </summary>
    Task<MetadataVersion> CreateVersionAsync(string? label = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores metadata from a previously created version.
    /// Only supported if SupportsVersioning is true.
    /// </summary>
    Task RestoreVersionAsync(string versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available versions.
    /// Only supported if SupportsVersioning is true.
    /// </summary>
    Task<IReadOnlyList<MetadataVersion>> ListVersionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Event args for metadata change notifications.
/// </summary>
public sealed class MetadataChangedEventArgs : EventArgs
{
    public MetadataChangedEventArgs()
    {
    }

    public MetadataChangedEventArgs(string? source)
    {
        Source = source;
    }

    /// <summary>
    /// Optional source of the change (e.g., "file-watcher", "database-notify", "redis-pubsub").
    /// </summary>
    public string? Source { get; }
}

/// <summary>
/// Represents a point-in-time version of metadata for rollback purposes.
/// </summary>
public sealed record MetadataVersion(
    string Id,
    DateTimeOffset CreatedAt,
    string? Label,
    long? SizeBytes,
    string? Checksum);

/// <summary>
/// Extends IMetadataProvider with runtime reload capability for GitOps deployments.
/// </summary>
public interface IReloadableMetadataProvider : IMetadataProvider
{
    /// <summary>
    /// Reloads metadata from the underlying source without restarting the server.
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
