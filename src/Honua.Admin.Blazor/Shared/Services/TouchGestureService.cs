// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing touch gestures on mobile devices.
/// Provides pull-to-refresh, swipe-to-delete, and swipe navigation functionality.
/// Scoped service - one instance per user session (Blazor circuit).
/// </summary>
public class TouchGestureService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<TouchGestureService> _logger;
    private readonly List<string> _activeGestures = new();
    private bool _disposed = false;

    public TouchGestureService(IJSRuntime jsRuntime, ILogger<TouchGestureService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Initialize pull-to-refresh gesture on an element.
    /// </summary>
    /// <param name="elementId">ID of the container element</param>
    /// <param name="dotNetHelper">DotNetObjectReference for callbacks</param>
    /// <returns>Gesture ID for cleanup</returns>
    public async Task<string?> InitializePullToRefreshAsync(string elementId, DotNetObjectReference<object> dotNetHelper)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<GestureResult>(
                "TouchGestures.initPullToRefresh",
                elementId,
                dotNetHelper
            );

            if (result?.Id != null)
            {
                _activeGestures.Add(result.Id);
                return result.Id;
            }

            return null;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Error initializing pull-to-refresh gesture for element {ElementId}", elementId);
            return null;
        }
    }

    /// <summary>
    /// Initialize swipe-to-delete gesture on a table row.
    /// </summary>
    /// <param name="rowId">ID of the row element</param>
    /// <param name="dotNetHelper">DotNetObjectReference for callbacks</param>
    /// <param name="itemId">ID of the item to delete</param>
    /// <returns>Gesture ID for cleanup</returns>
    public async Task<string?> InitializeSwipeToDeleteAsync(string rowId, DotNetObjectReference<object> dotNetHelper, string itemId)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<GestureResult>(
                "TouchGestures.initSwipeToDelete",
                rowId,
                dotNetHelper,
                itemId
            );

            if (result?.Id != null)
            {
                _activeGestures.Add(result.Id);
                return result.Id;
            }

            return null;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Error initializing swipe-to-delete gesture for row {RowId}, item {ItemId}", rowId, itemId);
            return null;
        }
    }

    /// <summary>
    /// Initialize swipe navigation (swipe right to go back).
    /// </summary>
    /// <param name="elementId">ID of the container element</param>
    /// <param name="dotNetHelper">DotNetObjectReference for callbacks</param>
    /// <returns>Gesture ID for cleanup</returns>
    public async Task<string?> InitializeSwipeNavigationAsync(string elementId, DotNetObjectReference<object> dotNetHelper)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<GestureResult>(
                "TouchGestures.initSwipeNavigation",
                elementId,
                dotNetHelper
            );

            if (result?.Id != null)
            {
                _activeGestures.Add(result.Id);
                return result.Id;
            }

            return null;
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Error initializing swipe navigation gesture for element {ElementId}", elementId);
            return null;
        }
    }

    /// <summary>
    /// Dispose a specific gesture handler.
    /// </summary>
    /// <param name="gestureId">Gesture ID to dispose</param>
    public async Task DisposeGestureAsync(string gestureId)
    {
        if (string.IsNullOrEmpty(gestureId)) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("TouchGestures.dispose", gestureId);
            _activeGestures.Remove(gestureId);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Error disposing gesture {GestureId}", gestureId);
        }
    }

    /// <summary>
    /// Dispose all active gesture handlers.
    /// </summary>
    public async Task DisposeAllGesturesAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("TouchGestures.disposeAll");
            _activeGestures.Clear();
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Error disposing all gestures");
        }
    }

    /// <summary>
    /// Dispose the service and cleanup all gesture handlers.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await DisposeAllGesturesAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Result object returned from JavaScript gesture initialization.
    /// </summary>
    private class GestureResult
    {
        public string? Id { get; set; }
    }
}

/// <summary>
/// Base class for components that use touch gestures.
/// Provides callback methods that can be invoked from JavaScript.
/// </summary>
public abstract class TouchGestureComponentBase : IDisposable
{
    private DotNetObjectReference<TouchGestureComponentBase>? _dotNetRef;

    /// <summary>
    /// Get a DotNetObjectReference for JavaScript callbacks.
    /// </summary>
    protected DotNetObjectReference<TouchGestureComponentBase> GetDotNetReference()
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);
        return _dotNetRef;
    }

    /// <summary>
    /// Called when pull-to-refresh is triggered.
    /// Override this method to handle refresh logic.
    /// </summary>
    [JSInvokable]
    public virtual Task OnPullToRefresh()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when swipe-to-delete is triggered.
    /// Override this method to handle delete logic.
    /// </summary>
    /// <param name="itemId">ID of the item to delete</param>
    [JSInvokable]
    public virtual Task OnSwipeDelete(string itemId)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when swipe back navigation is triggered.
    /// Override this method to handle navigation logic.
    /// </summary>
    [JSInvokable]
    public virtual Task OnSwipeBack()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose the DotNetObjectReference.
    /// </summary>
    public virtual void Dispose()
    {
        _dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
