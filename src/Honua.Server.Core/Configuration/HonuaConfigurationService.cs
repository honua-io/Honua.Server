// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Service for accessing and updating Honua configuration with change notifications.
/// Provides thread-safe access to configuration and notifies consumers of configuration updates.
/// </summary>
public interface IHonuaConfigurationService
{
    /// <summary>
    /// Gets the current configuration instance.
    /// This property is thread-safe and always returns the latest configuration.
    /// </summary>
    HonuaConfiguration Current { get; }

    /// <summary>
    /// Gets a change token that is triggered when configuration is updated via <see cref="Update"/>.
    /// Consumers can register callbacks to be notified when configuration changes.
    /// </summary>
    /// <returns>A change token that fires on configuration updates.</returns>
    IChangeToken GetChangeToken();

    /// <summary>
    /// Updates the configuration and triggers change notifications to all registered consumers.
    /// This operation is thread-safe and atomic.
    /// </summary>
    /// <param name="configuration">The new configuration instance to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    void Update(HonuaConfiguration configuration);
}

public sealed class HonuaConfigurationService : DisposableBase, IHonuaConfigurationService
{
    private readonly object _syncRoot = new();
    private HonuaConfiguration _current;
    private CancellationTokenSource _changeTokenSource = new();

    public HonuaConfigurationService(HonuaConfiguration configuration)
    {
        _current = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public HonuaConfiguration Current
    {
        get
        {
            ThrowIfDisposed();
            return Volatile.Read(ref _current);
        }
    }

    public IChangeToken GetChangeToken()
    {
        ThrowIfDisposed();
        var source = Volatile.Read(ref _changeTokenSource);
        return new CancellationChangeToken(source.Token);
    }

    public void Update(HonuaConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        ThrowIfDisposed();

        CancellationTokenSource? previous;
        lock (_syncRoot)
        {
            Volatile.Write(ref _current, configuration);
            var newSource = new CancellationTokenSource();
            previous = Interlocked.Exchange(ref _changeTokenSource, newSource);
        }

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

    protected override void DisposeCore()
    {
        var source = Interlocked.Exchange(ref _changeTokenSource, new CancellationTokenSource());
        source.Cancel();
        source.Dispose();
    }
}
