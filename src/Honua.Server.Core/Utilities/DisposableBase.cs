// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Abstract base class that provides a standard implementation of the disposable pattern
/// (IDisposable and IAsyncDisposable).
///
/// This eliminates 500-800 lines of duplicate disposal pattern code across 40+ classes
/// that individually implement ObjectDisposedException checking and resource cleanup.
///
/// USAGE:
/// 1. Inherit from DisposableBase
/// 2. Remove _disposed field and Dispose/DisposeAsync methods
/// 3. Call ThrowIfDisposed() at the start of public methods
/// 4. Override DisposeCore() for synchronous cleanup
/// 5. Override DisposeAsyncCore() for asynchronous cleanup (optional)
///
/// EXAMPLE MIGRATION (saves ~15-20 lines per class):
///
/// BEFORE:
///   public class MyCache : IDisposable
///   {
///       private volatile bool _disposed;
///
///       public void GetValue() {
///           ObjectDisposedException.ThrowIf(_disposed, this);
///           // ... work
///       }
///
///       public void Dispose() {
///           if (_disposed) return;
///           _disposed = true;
///           _lock.Dispose();
///       }
///   }
///
/// AFTER:
///   public class MyCache : DisposableBase
///   {
///       public void GetValue() {
///           ThrowIfDisposed();
///           // ... work
///       }
///
///       protected override void DisposeCore() {
///           _lock.Dispose();
///       }
///   }
/// </summary>
public abstract class DisposableBase : IDisposable, IAsyncDisposable
{
    private volatile bool _disposed;

    /// <summary>
    /// Gets a value indicating whether the object has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Throws ObjectDisposedException if the object has been disposed.
    /// Call this at the start of public methods to ensure the object is still usable.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Override this method to implement synchronous resource cleanup.
    /// Called by Dispose() when disposing.
    /// </summary>
    protected virtual void DisposeCore()
    {
    }

    /// <summary>
    /// Override this method to implement asynchronous resource cleanup.
    /// Optional - only override if you have async-only resources.
    /// The default implementation does nothing (async-only operations default to DisposeCore).
    /// </summary>
    protected virtual ValueTask DisposeCoreAsync()
    {
        return default;
    }

    /// <summary>
    /// Disposes the object synchronously.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCore();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Dispose(true);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the object asynchronously.
    /// Calls both async and sync disposal methods.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            Dispose(true);
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
