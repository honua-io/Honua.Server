// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Honua.Admin.Blazor.Shared.Components;

/// <summary>
/// Base class for forms with auto-save draft functionality
/// </summary>
public abstract class AutoSaveFormBase<T> : ComponentBase, IAsyncDisposable where T : class, new()
{
    [Inject] protected DraftStorageService DraftStorage { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;

    protected T Model { get; set; } = new();
    protected bool HasUnsavedChanges { get; set; }
    protected DateTime? LastSaved { get; set; }

    private Timer? _autoSaveTimer;
    private string _draftKey = string.Empty;
    private DotNetObjectReference<AutoSaveFormBase<T>>? _dotNetRef;
    private bool _isNavigating = false;

    /// <summary>
    /// Override to provide a unique key for draft storage
    /// </summary>
    protected abstract string GetDraftKey();

    /// <summary>
    /// Override to perform the actual save operation
    /// </summary>
    protected abstract Task PerformSaveAsync();

    /// <summary>
    /// Override to determine if draft should be loaded (default: true)
    /// </summary>
    protected virtual bool ShouldLoadDraft() => true;

    /// <summary>
    /// Override to customize auto-save interval in milliseconds (default: 30000 = 30 seconds)
    /// </summary>
    protected virtual int AutoSaveIntervalMs => 30000;

    protected override async Task OnInitializedAsync()
    {
        _draftKey = GetDraftKey();

        // Try to load draft if enabled
        if (ShouldLoadDraft())
        {
            var draft = await DraftStorage.LoadDraftAsync<T>(_draftKey);
            if (draft != null)
            {
                var shouldRestore = await ShowRestoreDraftDialogAsync();
                if (shouldRestore)
                {
                    Model = draft;
                    Snackbar.Add("Draft restored successfully", Severity.Info);
                    StateHasChanged();
                }
                else
                {
                    await DraftStorage.DeleteDraftAsync(_draftKey);
                }
            }
        }

        // Start auto-save timer
        _autoSaveTimer = new Timer(AutoSaveIntervalMs);
        _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveAsync();
        _autoSaveTimer.Start();

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Create reference to this component for JS interop
                _dotNetRef = DotNetObjectReference.Create(this);

                // Enable navigation warning
                await JSRuntime.InvokeVoidAsync("AutoSave.enableNavigationWarning", _dotNetRef);
            }
            catch (Exception ex)
            {
                // Silently fail - navigation warning is an enhancement
                Console.WriteLine($"Failed to enable navigation warning: {ex.Message}");
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    /// <summary>
    /// Call this when model changes to mark as dirty and trigger auto-save
    /// </summary>
    protected void OnModelChanged()
    {
        HasUnsavedChanges = true;
        StateHasChanged();
    }

    /// <summary>
    /// Performs auto-save to localStorage
    /// </summary>
    private async Task AutoSaveAsync()
    {
        if (!HasUnsavedChanges) return;

        try
        {
            await DraftStorage.SaveDraftAsync(_draftKey, Model);
            LastSaved = DateTime.Now;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Silently fail - auto-save is not critical
            Console.WriteLine($"Auto-save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this when performing actual save to clear draft
    /// </summary>
    protected async Task SaveAsync()
    {
        try
        {
            // Perform actual save
            await PerformSaveAsync();

            // Clear draft
            await DraftStorage.DeleteDraftAsync(_draftKey);
            HasUnsavedChanges = false;
            LastSaved = null;

            // Disable navigation warning since we're about to navigate
            _isNavigating = true;
        }
        catch
        {
            // Re-throw to let the caller handle the error
            throw;
        }
    }

    /// <summary>
    /// Shows confirmation dialog to restore draft
    /// </summary>
    private async Task<bool> ShowRestoreDraftDialogAsync()
    {
        var parameters = new DialogParameters
        {
            { "ContentText", "A draft was found from a previous session. Would you like to restore it?" },
            { "ButtonText", "Restore" },
            { "Color", Color.Primary }
        };

        var dialog = await DialogService.ShowAsync<Components.Shared.ConfirmDialog>("Restore Draft", parameters);
        var result = await dialog.Result;
        return !result.Canceled;
    }

    /// <summary>
    /// JavaScript callback for beforeunload event
    /// </summary>
    [JSInvokable]
    public string OnBeforeUnload()
    {
        // Don't show warning if we're in the process of saving/navigating
        if (_isNavigating) return string.Empty;

        return HasUnsavedChanges
            ? "You have unsaved changes. Are you sure you want to leave?"
            : string.Empty;
    }

    /// <summary>
    /// Helper to format time since last save
    /// </summary>
    protected string GetTimeSince(DateTime savedTime)
    {
        var elapsed = DateTime.Now - savedTime;

        if (elapsed.TotalSeconds < 60)
            return "just now";
        else if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        else if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        else
            return $"{(int)elapsed.TotalDays}d ago";
    }

    /// <summary>
    /// Renders the draft status indicator
    /// </summary>
    protected RenderFragment RenderDraftStatus() => builder =>
    {
        if (LastSaved.HasValue)
        {
            builder.OpenComponent<MudChip<string>>(0);
            builder.AddAttribute(1, "Size", Size.Small);
            builder.AddAttribute(2, "Color", Color.Info);
            builder.AddAttribute(3, "Icon", Icons.Material.Filled.CloudDone);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.AddContent(0, $"Auto-saved {GetTimeSince(LastSaved.Value)}");
            }));
            builder.CloseComponent();
        }
        else if (HasUnsavedChanges)
        {
            builder.OpenComponent<MudChip<string>>(0);
            builder.AddAttribute(1, "Size", Size.Small);
            builder.AddAttribute(2, "Color", Color.Warning);
            builder.AddAttribute(3, "Icon", Icons.Material.Filled.CloudOff);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
            {
                b.AddContent(0, "Unsaved changes");
            }));
            builder.CloseComponent();
        }
    };

    public virtual async ValueTask DisposeAsync()
    {
        // Stop auto-save timer
        if (_autoSaveTimer != null)
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Dispose();
        }

        // Disable navigation warning
        try
        {
            await JSRuntime.InvokeVoidAsync("AutoSave.disableNavigationWarning");
        }
        catch
        {
            // Ignore - component may be disposed
        }

        // Dispose DotNetObjectReference
        _dotNetRef?.Dispose();

        GC.SuppressFinalize(this);
    }
}
