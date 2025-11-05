// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Tracks unsaved changes across form components to prevent accidental navigation.
/// Scoped service - one instance per user session (Blazor circuit).
/// </summary>
public class EditorState
{
    private readonly Dictionary<string, bool> _unsavedChanges = new();

    /// <summary>
    /// Event raised when editor state changes (dirty/clean).
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Check if a specific editor has unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges(string editorId)
        => _unsavedChanges.GetValueOrDefault(editorId, false);

    /// <summary>
    /// Check if ANY editor has unsaved changes.
    /// </summary>
    public bool HasAnyUnsavedChanges()
        => _unsavedChanges.Any(kvp => kvp.Value);

    /// <summary>
    /// Mark an editor as having unsaved changes (dirty).
    /// </summary>
    public void MarkDirty(string editorId)
    {
        _unsavedChanges[editorId] = true;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Mark an editor as having no unsaved changes (clean).
    /// </summary>
    public void MarkClean(string editorId)
    {
        _unsavedChanges.Remove(editorId);
        OnChange?.Invoke();
    }

    /// <summary>
    /// Clear all unsaved changes tracking.
    /// </summary>
    public void Reset()
    {
        _unsavedChanges.Clear();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Show confirmation dialog if there are unsaved changes.
    /// Returns true if navigation should proceed, false if canceled.
    /// </summary>
    public async Task<bool> ConfirmNavigationAsync(IDialogService dialogService, string? editorId = null)
    {
        bool hasChanges = editorId != null
            ? HasUnsavedChanges(editorId)
            : HasAnyUnsavedChanges();

        if (!hasChanges)
            return true;

        var parameters = new DialogParameters
        {
            { "ContentText", "You have unsaved changes. Are you sure you want to leave? Your changes will be lost." },
            { "ButtonText", "Leave" },
            { "Color", Color.Warning }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ConfirmDialog>("Unsaved Changes", parameters, options);
        var result = await dialog.Result;

        return !result.Canceled;
    }
}

/// <summary>
/// Simple confirmation dialog component.
/// </summary>
public class ConfirmDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string ContentText { get; set; } = "Are you sure?";

    [Parameter]
    public string ButtonText { get; set; } = "Confirm";

    [Parameter]
    public Color Color { get; set; } = Color.Primary;

    private void Cancel() => MudDialog.Cancel();
    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
