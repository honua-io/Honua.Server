// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Timers;

namespace Honua.Admin.Blazor.Shared.Helpers;

/// <summary>
/// Helper class for debouncing search operations to improve performance.
/// Delays execution of search actions until after a specified delay period has elapsed
/// since the last invocation.
/// </summary>
public class SearchDebouncer : IDisposable
{
    private System.Timers.Timer? _debounceTimer;
    private readonly int _delayMs;
    private Action? _pendingAction;
    private readonly SynchronizationContext? _synchronizationContext;

    /// <summary>
    /// Initializes a new instance of SearchDebouncer with the specified delay.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before executing the action. Default is 300ms.</param>
    public SearchDebouncer(int delayMs = 300)
    {
        _delayMs = delayMs;
        _synchronizationContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Debounces the specified action. The action will only be executed after the delay period
    /// has elapsed without any new calls to Debounce.
    /// </summary>
    /// <param name="action">The action to execute after the debounce period.</param>
    public void Debounce(Action action)
    {
        _pendingAction = action;

        // Dispose existing timer if any
        _debounceTimer?.Dispose();

        // Create new timer
        _debounceTimer = new System.Timers.Timer(_delayMs);
        _debounceTimer.Elapsed += OnTimerElapsed;
        _debounceTimer.AutoReset = false;
        _debounceTimer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Unsubscribe event handler before disposing to prevent memory leak
        if (_debounceTimer != null)
        {
            _debounceTimer.Elapsed -= OnTimerElapsed;
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }

        if (_pendingAction != null)
        {
            var actionToExecute = _pendingAction;
            _pendingAction = null;

            // Execute on the original synchronization context (UI thread for Blazor)
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Post(_ => actionToExecute(), null);
            }
            else
            {
                actionToExecute();
            }
        }
    }

    /// <summary>
    /// Disposes the debouncer and any pending timers.
    /// </summary>
    public void Dispose()
    {
        if (_debounceTimer != null)
        {
            _debounceTimer.Elapsed -= OnTimerElapsed;
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }
        _pendingAction = null;
    }
}
