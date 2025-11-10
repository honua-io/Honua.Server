// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public sealed class MetadataRegistry : IMetadataRegistry, IDisposable
{
    private readonly IMetadataProvider _provider;
    private readonly ILogger<MetadataRegistry>? _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private Task<MetadataSnapshot>? _snapshotTask;
    private CancellationTokenSource _changeTokenSource = new();
    private bool _disposed;

    public MetadataRegistry(IMetadataProvider provider, ILogger<MetadataRegistry>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger;
    }

    [Obsolete("Use GetSnapshotAsync() instead. This property uses blocking calls and will be removed in a future version.")]
    public MetadataSnapshot Snapshot => throw new NotSupportedException(
        "Synchronous Snapshot property removed due to deadlock risks. Use GetSnapshotAsync() instead.");

    public bool IsInitialized
    {
        get
        {
            var snapshotTask = Volatile.Read(ref _snapshotTask);
            return snapshotTask is not null && snapshotTask.IsCompletedSuccessfully;
        }
    }

    /// <summary>
    /// Attempts to retrieve the current metadata snapshot without blocking.
    /// </summary>
    /// <param name="snapshot">The materialized snapshot when available.</param>
    /// <returns><c>true</c> when a completed snapshot exists; otherwise <c>false</c>.</returns>
    public bool TryGetSnapshot(out MetadataSnapshot snapshot)
    {
        var snapshotTask = Volatile.Read(ref _snapshotTask);
        if (snapshotTask is not null && snapshotTask.IsCompletedSuccessfully)
        {
            snapshot = snapshotTask.Result;
            return true;
        }

        snapshot = null!;
        return false;
    }

    public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<MetadataSnapshot>(EnsureInitializedInternalAsync(cancellationToken));
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return EnsureInitializedInternalAsync(cancellationToken);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Metadata,
            "MetadataReload",
            [("metadata.operation", "Reload")],
            async activity =>
            {
                _logger?.LogInformation("Metadata reload started");

                await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "MetadataReload",
                        async () =>
                        {
                            var previous = Volatile.Read(ref _snapshotTask);
                            var loadTask = LoadAsync(cancellationToken);
                            Volatile.Write(ref _snapshotTask, loadTask);

                            try
                            {
                                var snapshot = await loadTask.ConfigureAwait(false);
                                SignalSnapshotChanged();

                                activity?.AddTag("metadata.service_count", snapshot.Services.Count);
                                activity?.AddTag("metadata.folder_count", snapshot.Folders.Count);

                                var layerCount = 0;
                                foreach (var service in snapshot.Services)
                                {
                                    layerCount += service.Layers.Count;
                                }
                                activity?.AddTag("metadata.layer_count", layerCount);

                                _logger?.LogInformation(
                                    "Metadata reload completed successfully: {ServiceCount} services, {FolderCount} folders, {LayerCount} layers",
                                    snapshot.Services.Count,
                                    snapshot.Folders.Count,
                                    layerCount);
                            }
                            catch (MetadataException)
                            {
                                // Re-throw domain exceptions as-is
                                Volatile.Write(ref _snapshotTask, previous);
                                throw;
                            }
                            catch (OperationCanceledException)
                            {
                                // Re-throw cancellation as-is
                                Volatile.Write(ref _snapshotTask, previous);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Volatile.Write(ref _snapshotTask, previous);
                                _logger?.LogError(ex, "Metadata reload failed: {Error}", ex.Message);
                                throw new MetadataException("Failed to reload metadata", "METADATA_RELOAD_FAILED", ex);
                            }
                        },
                        LogLevel.Information).ConfigureAwait(false);
                }
                finally
                {
                    _reloadLock.Release();
                }
            });
    }

    [Obsolete("Use UpdateAsync() instead. This method has been removed to prevent deadlocks.")]
    public void Update(MetadataSnapshot snapshot)
    {
        throw new NotSupportedException(
            "Synchronous Update() has been removed due to deadlock risks. " +
            "Use UpdateAsync() instead for safe async operation.");
    }

    public async Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(snapshot);

        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Metadata,
            "MetadataUpdate",
            [
                ("metadata.operation", "Update"),
                ("metadata.service_count", snapshot.Services.Count),
                ("metadata.folder_count", snapshot.Folders.Count)
            ],
            async activity =>
            {
                var layerCount = 0;
                foreach (var service in snapshot.Services)
                {
                    layerCount += service.Layers.Count;
                }
                activity?.AddTag("metadata.layer_count", layerCount);

                _logger?.LogInformation("Metadata update started: {ServiceCount} services, {FolderCount} folders, {LayerCount} layers",
                    snapshot.Services.Count, snapshot.Folders.Count, layerCount);

                await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "MetadataUpdate",
                        async () =>
                        {
                            var newTask = Task.FromResult(snapshot);
                            Volatile.Write(ref _snapshotTask, newTask);
                            SignalSnapshotChanged();

                            _logger?.LogInformation("Metadata update completed successfully");
                            await Task.CompletedTask.ConfigureAwait(false);
                        },
                        LogLevel.Information).ConfigureAwait(false);
                }
                catch (MetadataException)
                {
                    // Re-throw domain exceptions as-is
                    throw;
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation as-is
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Metadata update failed: {Error}", ex.Message);
                    throw new MetadataException("Failed to update metadata", "METADATA_UPDATE_FAILED", ex);
                }
                finally
                {
                    _reloadLock.Release();
                }
            });
    }

    private Task<MetadataSnapshot> EnsureInitializedInternalAsync(CancellationToken cancellationToken)
    {
        var snapshotTask = Volatile.Read(ref _snapshotTask);
        if (snapshotTask is not null)
        {
            return snapshotTask.WaitAsync(cancellationToken);
        }

        return InitializeSlowPathAsync(cancellationToken);
    }

    private async Task<MetadataSnapshot> InitializeSlowPathAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Metadata initialization started");

        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = Volatile.Read(ref _snapshotTask);
            if (existing is not null)
            {
                return await existing.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var loadTask = LoadAsync(cancellationToken);
            Volatile.Write(ref _snapshotTask, loadTask);

            try
            {
                var snapshot = await loadTask.ConfigureAwait(false);
                SignalSnapshotChanged();

                var layerCount = 0;
                foreach (var service in snapshot.Services)
                {
                    layerCount += service.Layers.Count;
                }

                _logger?.LogInformation(
                    "Metadata initialization completed: {ServiceCount} services, {FolderCount} folders, {LayerCount} layers",
                    snapshot.Services.Count,
                    snapshot.Folders.Count,
                    layerCount);

                return snapshot;
            }
            catch (MetadataException)
            {
                // Re-throw domain exceptions as-is
                Volatile.Write(ref _snapshotTask, null);
                throw;
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation as-is
                Volatile.Write(ref _snapshotTask, null);
                throw;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _snapshotTask, null);
                _logger?.LogError(ex, "Metadata initialization failed: {Error}", ex.Message);
                throw new MetadataException("Failed to initialize metadata", "METADATA_INIT_FAILED", ex);
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        return _provider.LoadAsync(cancellationToken);
    }

    public IChangeToken GetChangeToken()
    {
        var source = Volatile.Read(ref _changeTokenSource);
        return new CancellationChangeToken(source.Token);
    }

    private void SignalSnapshotChanged()
    {
        var newSource = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _changeTokenSource, newSource);
        if (previous is null)
        {
            return;
        }

        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancel and dispose the current change token source
        var currentSource = Interlocked.Exchange(ref _changeTokenSource, null!);
        if (currentSource is not null)
        {
            try
            {
                currentSource.Cancel();
            }
            catch
            {
                // Ignore cancellation exceptions during disposal
            }
            finally
            {
                currentSource.Dispose();
            }
        }

        // Dispose the reload lock
        _reloadLock?.Dispose();
    }
}
