// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.JSInterop;
using System.Text.Json;

namespace Honua.Admin.Blazor.Services;

/// <summary>
/// Service for managing interactive guided tours in the Honua Admin UI
/// </summary>
public class TourService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<TourService> _logger;
    private DotNetObjectReference<TourService>? _dotNetReference;
    private readonly Dictionary<string, Action> _completionCallbacks = new();

    public TourService(IJSRuntime jsRuntime, ILogger<TourService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when a tour is completed
    /// </summary>
    public event Func<string, Task>? OnTourCompleted;

    /// <summary>
    /// Start a tour with the given configuration
    /// </summary>
    public async Task StartTourAsync(string tourId, TourConfiguration config)
    {
        try
        {
            _logger.LogInformation("Starting tour: {TourId}", tourId);

            // Ensure DotNet reference is set for callbacks
            _dotNetReference ??= DotNetObjectReference.Create(this);

            // Create tour config JSON
            var tourConfig = new
            {
                id = tourId,
                steps = config.Steps.Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    text = s.Text,
                    attachTo = s.AttachTo != null ? new
                    {
                        element = s.AttachTo.Element,
                        on = s.AttachTo.Position.ToString().ToLower()
                    } : null,
                    beforeShow = s.BeforeShowFunction,
                    buttons = s.CustomButtons,
                    options = s.Options
                }).ToArray(),
                defaultStepOptions = config.DefaultStepOptions,
                useModalOverlay = config.UseModalOverlay,
                onComplete = config.OnCompleteFunction,
                onCancel = config.OnCancelFunction
            };

            var tourConfigJson = JsonSerializer.Serialize(tourConfig);

            await _jsRuntime.InvokeVoidAsync("HonuaTours.startTour", tourId, JsonDocument.Parse(tourConfigJson).RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start tour: {TourId}", tourId);
            throw;
        }
    }

    /// <summary>
    /// Cancel the currently active tour
    /// </summary>
    public async Task CancelActiveTourAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("HonuaTours.cancelActiveTour");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel active tour");
        }
    }

    /// <summary>
    /// Check if a tour has been completed
    /// </summary>
    public async Task<bool> IsTourCompletedAsync(string tourId)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("HonuaTours.isTourCompleted", tourId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check tour completion status: {TourId}", tourId);
            return false;
        }
    }

    /// <summary>
    /// Reset a tour's completion status
    /// </summary>
    public async Task ResetTourAsync(string tourId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("HonuaTours.resetTour", tourId);
            _logger.LogInformation("Reset tour: {TourId}", tourId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset tour: {TourId}", tourId);
        }
    }

    /// <summary>
    /// Reset all tours
    /// </summary>
    public async Task ResetAllToursAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("HonuaTours.resetAllTours");
            _logger.LogInformation("Reset all tours");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset all tours");
        }
    }

    /// <summary>
    /// Get list of completed tours
    /// </summary>
    public async Task<List<string>> GetCompletedToursAsync()
    {
        try
        {
            var completed = await _jsRuntime.InvokeAsync<string[]>("HonuaTours.getCompletedTours");
            return completed?.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get completed tours");
            return new List<string>();
        }
    }

    /// <summary>
    /// Get tour progress statistics
    /// </summary>
    public async Task<TourProgress> GetTourProgressAsync()
    {
        try
        {
            var progress = await _jsRuntime.InvokeAsync<JsonElement>("HonuaTours.getTourProgress");
            return new TourProgress
            {
                Completed = progress.GetProperty("completed").GetInt32(),
                Total = progress.GetProperty("total").GetInt32(),
                Percentage = progress.GetProperty("percentage").GetInt32()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tour progress");
            return new TourProgress { Completed = 0, Total = 0, Percentage = 0 };
        }
    }

    /// <summary>
    /// Register a callback for when a tour is completed
    /// </summary>
    public void RegisterCompletionCallback(string tourId, Action callback)
    {
        _completionCallbacks[tourId] = callback;
    }

    /// <summary>
    /// JavaScript callback when a tour is completed
    /// </summary>
    [JSInvokable("OnTourCompleted")]
    public async Task HandleTourCompletedAsync(string tourId)
    {
        _logger.LogInformation("Tour completed: {TourId}", tourId);

        // Execute registered callback
        if (_completionCallbacks.TryGetValue(tourId, out var callback))
        {
            callback();
        }

        // Raise event
        if (OnTourCompleted != null)
        {
            await OnTourCompleted.Invoke(tourId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetReference?.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration for a guided tour
/// </summary>
public class TourConfiguration
{
    public List<TourStep> Steps { get; set; } = new();
    public bool UseModalOverlay { get; set; } = true;
    public Dictionary<string, object>? DefaultStepOptions { get; set; }
    public string? OnCompleteFunction { get; set; }
    public string? OnCancelFunction { get; set; }
}

/// <summary>
/// A single step in a guided tour
/// </summary>
public class TourStep
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public TourStepAttachment? AttachTo { get; set; }
    public string? BeforeShowFunction { get; set; }
    public List<TourButton>? CustomButtons { get; set; }
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// Attachment configuration for a tour step
/// </summary>
public class TourStepAttachment
{
    public string Element { get; set; } = string.Empty;
    public TourStepPosition Position { get; set; } = TourStepPosition.Bottom;
}

/// <summary>
/// Position of tour popover relative to target element
/// </summary>
public enum TourStepPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Auto
}

/// <summary>
/// Custom button for a tour step
/// </summary>
public class TourButton
{
    public string Text { get; set; } = string.Empty;
    public string? Classes { get; set; }
    public string? Action { get; set; }
}

/// <summary>
/// Tour progress information
/// </summary>
public class TourProgress
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public int Percentage { get; set; }
}
