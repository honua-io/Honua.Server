// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Custom IChangeToken implementation for configuration file changes.
/// Thread-safe callback registration and triggering.
/// </summary>
public sealed class ConfigurationChangeToken : IChangeToken
{
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private bool _hasChanged;

    /// <summary>
    /// Gets a value that indicates if a change has occurred.
    /// </summary>
    public bool HasChanged
    {
        get
        {
            lock (_lock)
            {
                return _hasChanged;
            }
        }
    }

    /// <summary>
    /// Gets a value that indicates if this token will proactively raise callbacks.
    /// Always returns true since this token supports active change notifications.
    /// </summary>
    public bool ActiveChangeCallbacks => true;

    /// <summary>
    /// Registers a callback that will be invoked when the change token is triggered.
    /// </summary>
    /// <param name="callback">The callback to invoke when the token is triggered.</param>
    /// <param name="state">State to be passed to the callback.</param>
    /// <returns>A disposable that can be used to unregister the callback.</returns>
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        return _cts.Token.Register(callback, state);
    }

    /// <summary>
    /// Triggers the change token and invokes all registered callbacks.
    /// This method is thread-safe and can be called multiple times, but only the first call will trigger callbacks.
    /// </summary>
    public void OnChange()
    {
        lock (_lock)
        {
            if (_hasChanged)
            {
                return; // Already triggered
            }

            _hasChanged = true;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Token was already disposed, ignore
        }
    }

    /// <summary>
    /// Disposes the change token and releases all resources.
    /// </summary>
    public void Dispose()
    {
        _cts.Dispose();
    }
}
