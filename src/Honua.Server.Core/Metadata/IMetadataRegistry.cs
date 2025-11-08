// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Central registry for accessing and managing server metadata configuration.
/// Provides access to service, layer, and server configuration with change notification support.
/// </summary>
/// <remarks>
/// The metadata registry is the primary source of configuration for the Honua Server.
/// It contains:
/// - Service definitions (WFS, WMS, WMTS, OGC API - Features, etc.)
/// - Layer configurations (schema, geometry type, fields, symbology)
/// - Server settings (CORS, authentication, caching, security)
/// - Connection strings and data source configurations
///
/// The registry supports hot-reloading of configuration changes without server restart.
/// All metadata access should go through this registry to ensure consistency.
///
/// Thread-safety: This interface is thread-safe. Snapshots are immutable and can be
/// safely cached and accessed from multiple threads.
/// </remarks>
public interface IMetadataRegistry
    {
        /// <summary>
        /// Gets the current metadata snapshot synchronously.
        /// </summary>
        /// <remarks>
        /// DEPRECATED: This property may block if metadata is not yet loaded.
        /// Use <see cref="GetSnapshotAsync"/> instead for better async/await support.
        /// </remarks>
        [Obsolete("Use GetSnapshotAsync() instead. This property uses blocking calls and will be removed in a future version.")]
        MetadataSnapshot Snapshot { get; }

        /// <summary>
        /// Gets a value indicating whether the metadata registry has been initialized.
        /// </summary>
        /// <value>
        /// <c>true</c> if metadata has been loaded from storage; otherwise, <c>false</c>.
        /// </value>
        bool IsInitialized { get; }

        /// <summary>
        /// Attempts to get the current metadata snapshot without blocking.
        /// </summary>
        /// <param name="snapshot">
        /// When this method returns, contains the metadata snapshot if available,
        /// or null if metadata is not yet loaded.
        /// </param>
        /// <returns>
        /// <c>true</c> if the snapshot was successfully retrieved; otherwise, <c>false</c>.
        /// </returns>
        bool TryGetSnapshot(out MetadataSnapshot snapshot);

        /// <summary>
        /// Gets the current metadata snapshot asynchronously.
        /// If metadata is not yet loaded, waits for initialization to complete.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The current metadata snapshot containing all service and server configuration.</returns>
        /// <exception cref="System.OperationCanceledException">Thrown when the operation is cancelled.</exception>
        ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures the metadata registry is initialized by loading configuration from storage.
        /// If already initialized, this method returns immediately without reloading.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the initialization operation.</returns>
        Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reloads metadata configuration from storage, discarding the current snapshot.
        /// Use this method to pick up configuration changes made outside the application.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the reload operation.</returns>
        /// <remarks>
        /// This triggers change notifications via <see cref="GetChangeToken"/>,
        /// allowing dependent services to react to configuration updates.
        /// </remarks>
        Task ReloadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the metadata registry with a new snapshot synchronously.
        /// </summary>
        /// <param name="snapshot">The new metadata snapshot to apply.</param>
        /// <remarks>
        /// DEPRECATED: This method uses synchronous operations.
        /// Use <see cref="UpdateAsync"/> instead for proper async/await support.
        /// </remarks>
        [Obsolete("Use UpdateAsync() instead. This method uses blocking calls and will be removed in a future version.")]
        void Update(MetadataSnapshot snapshot);

        /// <summary>
        /// Updates the metadata registry with a new snapshot and persists it to storage.
        /// Triggers change notifications to invalidate caches and update dependent services.
        /// </summary>
        /// <param name="snapshot">The new metadata snapshot to apply.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the update operation.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when snapshot is null.</exception>
        Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a change token that signals when metadata has been updated.
        /// Use this for cache invalidation and configuration-dependent service initialization.
        /// </summary>
        /// <returns>
        /// An <see cref="IChangeToken"/> that becomes active when metadata changes.
        /// Register callbacks to receive notifications of configuration updates.
        /// </returns>
        IChangeToken GetChangeToken();
    }
