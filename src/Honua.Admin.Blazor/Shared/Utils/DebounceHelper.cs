// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Timers;

namespace Honua.Admin.Blazor.Shared.Utils;

/// <summary>
/// Helper class for debouncing operations like search inputs.
/// Delays execution of an action until after a specified delay has passed since the last invocation.
/// </summary>
public class DebounceHelper : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private Func<Task>? _action;

    /// <summary>
    /// Creates a new debounce helper with the specified delay.
    /// </summary>
    /// <param name="delayMilliseconds">Delay in milliseconds before executing the action</param>
    public DebounceHelper(int delayMilliseconds = 300)
    {
        _timer = new System.Timers.Timer(delayMilliseconds);
        _timer.AutoReset = false;
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>
    /// Debounces the specified action. The action will be executed after the delay period
    /// has passed without any new calls to Debounce.
    /// </summary>
    /// <param name="action">The action to debounce</param>
    public void Debounce(Func<Task> action)
    {
        _action = action;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// Debounces the specified synchronous action.
    /// </summary>
    /// <param name="action">The action to debounce</param>
    public void Debounce(Action action)
    {
        Debounce(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Cancels any pending debounced action.
    /// </summary>
    public void Cancel()
    {
        _timer.Stop();
        _action = null;
    }

    /// <summary>
    /// Immediately executes any pending action without waiting for the delay.
    /// </summary>
    public async Task FlushAsync()
    {
        _timer.Stop();
        if (_action != null)
        {
            await _action();
            _action = null;
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_action != null)
        {
            try
            {
                await _action();
            }
            catch
            {
                // Swallow exceptions to prevent crashes
            }
            finally
            {
                _action = null;
            }
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
        _action = null;
    }
}

/// <summary>
/// Extension methods for debouncing operations.
/// </summary>
public static class DebounceExtensions
{
    /// <summary>
    /// Creates a debounced version of the specified action.
    /// </summary>
    /// <param name="action">The action to debounce</param>
    /// <param name="delayMilliseconds">Delay in milliseconds</param>
    /// <returns>A debounce helper configured with the action</returns>
    public static DebounceHelper CreateDebouncer(this Func<Task> action, int delayMilliseconds = 300)
    {
        return new DebounceHelper(delayMilliseconds);
    }

    /// <summary>
    /// Creates a debounced version of the specified synchronous action.
    /// </summary>
    /// <param name="action">The action to debounce</param>
    /// <param name="delayMilliseconds">Delay in milliseconds</param>
    /// <returns>A debounce helper configured with the action</returns>
    public static DebounceHelper CreateDebouncer(this Action action, int delayMilliseconds = 300)
    {
        return new DebounceHelper(delayMilliseconds);
    }
}
