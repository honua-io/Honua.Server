// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.JSInterop;
using System.Text.Json;

namespace Honua.Admin.Blazor.Services;

/// <summary>
/// Service for managing user onboarding progress and checklist items
/// </summary>
public class OnboardingService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<OnboardingService> _logger;
    private const string StorageKey = "honua-onboarding-progress";

    public OnboardingService(IJSRuntime jsRuntime, ILogger<OnboardingService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when onboarding progress changes
    /// </summary>
    public event Func<Task>? OnProgressChanged;

    /// <summary>
    /// Get the current onboarding progress
    /// </summary>
    public async Task<OnboardingProgress> GetProgressAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);

            if (string.IsNullOrEmpty(json))
            {
                return CreateDefaultProgress();
            }

            var progress = JsonSerializer.Deserialize<OnboardingProgress>(json);
            return progress ?? CreateDefaultProgress();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get onboarding progress");
            return CreateDefaultProgress();
        }
    }

    /// <summary>
    /// Save onboarding progress
    /// </summary>
    public async Task SaveProgressAsync(OnboardingProgress progress)
    {
        try
        {
            var json = JsonSerializer.Serialize(progress);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);

            _logger.LogInformation("Saved onboarding progress: {Completed}/{Total} items completed",
                progress.CompletedItemsCount, progress.TotalItemsCount);

            if (OnProgressChanged != null)
            {
                await OnProgressChanged.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save onboarding progress");
        }
    }

    /// <summary>
    /// Mark a checklist item as completed
    /// </summary>
    public async Task CompleteItemAsync(string itemId)
    {
        var progress = await GetProgressAsync();
        var item = progress.ChecklistItems.FirstOrDefault(i => i.Id == itemId);

        if (item != null && !item.IsCompleted)
        {
            item.IsCompleted = true;
            item.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Completed onboarding item: {ItemId}", itemId);

            await SaveProgressAsync(progress);

            // Check if all items are completed
            if (progress.IsFullyCompleted)
            {
                _logger.LogInformation("User has completed all onboarding items!");
            }
        }
    }

    /// <summary>
    /// Mark a checklist item as incomplete
    /// </summary>
    public async Task UncompleteItemAsync(string itemId)
    {
        var progress = await GetProgressAsync();
        var item = progress.ChecklistItems.FirstOrDefault(i => i.Id == itemId);

        if (item != null && item.IsCompleted)
        {
            item.IsCompleted = false;
            item.CompletedAt = null;

            _logger.LogInformation("Uncompleted onboarding item: {ItemId}", itemId);

            await SaveProgressAsync(progress);
        }
    }

    /// <summary>
    /// Reset all onboarding progress
    /// </summary>
    public async Task ResetProgressAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            _logger.LogInformation("Reset onboarding progress");

            if (OnProgressChanged != null)
            {
                await OnProgressChanged.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset onboarding progress");
        }
    }

    /// <summary>
    /// Dismiss the onboarding checklist
    /// </summary>
    public async Task DismissChecklistAsync()
    {
        var progress = await GetProgressAsync();
        progress.IsDismissed = true;
        await SaveProgressAsync(progress);
    }

    /// <summary>
    /// Show the onboarding checklist again
    /// </summary>
    public async Task ShowChecklistAsync()
    {
        var progress = await GetProgressAsync();
        progress.IsDismissed = false;
        await SaveProgressAsync(progress);
    }

    /// <summary>
    /// Check if user should see onboarding
    /// </summary>
    public async Task<bool> ShouldShowOnboardingAsync()
    {
        var progress = await GetProgressAsync();
        return !progress.IsDismissed && !progress.IsFullyCompleted;
    }

    /// <summary>
    /// Create default onboarding progress
    /// </summary>
    private OnboardingProgress CreateDefaultProgress()
    {
        return new OnboardingProgress
        {
            ChecklistItems = new List<OnboardingChecklistItem>
            {
                new()
                {
                    Id = "complete-welcome-tour",
                    Title = "Complete Welcome Tour",
                    Description = "Take a tour of the Honua platform",
                    Icon = "TourIcon",
                    TourId = "welcome-tour",
                    Category = "Getting Started"
                },
                new()
                {
                    Id = "create-first-service",
                    Title = "Create Your First Service",
                    Description = "Set up a spatial data service",
                    Icon = "LayersIcon",
                    ActionUrl = "/services",
                    Category = "Getting Started"
                },
                new()
                {
                    Id = "upload-data",
                    Title = "Upload Spatial Data",
                    Description = "Import your first dataset",
                    Icon = "CloudUploadIcon",
                    ActionUrl = "/import",
                    TourId = "data-upload-tour",
                    Category = "Data Management"
                },
                new()
                {
                    Id = "create-map",
                    Title = "Create a Map",
                    Description = "Build an interactive map visualization",
                    Icon = "MapIcon",
                    ActionUrl = "/maps",
                    TourId = "map-creation-tour",
                    Category = "Visualization"
                },
                new()
                {
                    Id = "create-dashboard",
                    Title = "Build a Dashboard",
                    Description = "Create a dashboard with widgets",
                    Icon = "DashboardIcon",
                    ActionUrl = "/",
                    TourId = "dashboard-tour",
                    Category = "Visualization"
                },
                new()
                {
                    Id = "invite-team-member",
                    Title = "Invite Team Members",
                    Description = "Collaborate with your team",
                    Icon = "PeopleIcon",
                    ActionUrl = "/users",
                    Category = "Collaboration"
                },
                new()
                {
                    Id = "explore-api",
                    Title = "Explore the API",
                    Description = "Learn how to access your data via API",
                    Icon = "CodeIcon",
                    ActionUrl = "/services",
                    Category = "Advanced"
                }
            },
            StartedAt = DateTimeOffset.UtcNow,
            IsDismissed = false
        };
    }
}

/// <summary>
/// User's onboarding progress
/// </summary>
public class OnboardingProgress
{
    public List<OnboardingChecklistItem> ChecklistItems { get; set; } = new();
    public DateTimeOffset StartedAt { get; set; }
    public bool IsDismissed { get; set; }

    public int CompletedItemsCount => ChecklistItems.Count(i => i.IsCompleted);
    public int TotalItemsCount => ChecklistItems.Count;
    public int ProgressPercentage => TotalItemsCount > 0
        ? (int)Math.Round((double)CompletedItemsCount / TotalItemsCount * 100)
        : 0;
    public bool IsFullyCompleted => CompletedItemsCount == TotalItemsCount;
}

/// <summary>
/// A single item in the onboarding checklist
/// </summary>
public class OnboardingChecklistItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? TourId { get; set; }
    public string Category { get; set; } = "General";
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
