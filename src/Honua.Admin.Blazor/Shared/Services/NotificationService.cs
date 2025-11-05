// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using MudBlazor;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Wrapper around MudBlazor's ISnackbar for consistent toast notifications.
/// Scoped service - one instance per user session (Blazor circuit).
/// </summary>
public class NotificationService
{
    private readonly ISnackbar _snackbar;

    public NotificationService(ISnackbar snackbar)
    {
        _snackbar = snackbar;
    }

    /// <summary>
    /// Show a success message (green toast).
    /// </summary>
    public void Success(string message, string? title = null)
    {
        var msg = title != null ? $"{title}: {message}" : message;
        _snackbar.Add(msg, Severity.Success, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = true;
        });
    }

    /// <summary>
    /// Show an error message (red toast).
    /// </summary>
    public void Error(string message, string? title = null)
    {
        var msg = title != null ? $"{title}: {message}" : message;
        _snackbar.Add(msg, Severity.Error, config =>
        {
            config.VisibleStateDuration = 5000;
            config.ShowCloseIcon = true;
        });
    }

    /// <summary>
    /// Show a warning message (orange toast).
    /// </summary>
    public void Warning(string message, string? title = null)
    {
        var msg = title != null ? $"{title}: {message}" : message;
        _snackbar.Add(msg, Severity.Warning, config =>
        {
            config.VisibleStateDuration = 4000;
            config.ShowCloseIcon = true;
        });
    }

    /// <summary>
    /// Show an info message (blue toast).
    /// </summary>
    public void Info(string message, string? title = null)
    {
        var msg = title != null ? $"{title}: {message}" : message;
        _snackbar.Add(msg, Severity.Info, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = true;
        });
    }

    /// <summary>
    /// Clear all active notifications.
    /// </summary>
    public void Clear()
    {
        _snackbar.Clear();
    }
}
