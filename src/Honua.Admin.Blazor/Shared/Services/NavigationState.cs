// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using MudBlazor;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Manages current folder navigation and breadcrumbs across the Admin UI.
/// Scoped service - one instance per user session (Blazor circuit).
/// </summary>
public class NavigationState
{
    private string? _currentFolderId;
    private readonly List<BreadcrumbItem> _breadcrumbs = new();

    /// <summary>
    /// Event raised when navigation state changes (folder, breadcrumbs).
    /// Components should subscribe to trigger re-renders.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Current folder ID being viewed, or null for root.
    /// </summary>
    public string? CurrentFolderId => _currentFolderId;

    /// <summary>
    /// Breadcrumb trail for current location.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs => _breadcrumbs;

    /// <summary>
    /// Navigate to a specific folder.
    /// </summary>
    public void NavigateToFolder(string? folderId, string? folderName = null)
    {
        _currentFolderId = folderId;

        if (folderId is not null && folderName is not null)
        {
            _breadcrumbs.Add(new BreadcrumbItem(folderName, $"/folders/{folderId}", icon: Icons.Material.Filled.Folder));
        }
        else
        {
            // Root folder
            _breadcrumbs.Clear();
            _breadcrumbs.Add(new BreadcrumbItem("Home", "/", icon: Icons.Material.Filled.Home));
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Navigate up one level in folder hierarchy.
    /// </summary>
    public void NavigateUp()
    {
        if (_breadcrumbs.Count > 1)
        {
            _breadcrumbs.RemoveAt(_breadcrumbs.Count - 1);

            var lastBreadcrumb = _breadcrumbs.LastOrDefault();
            _currentFolderId = lastBreadcrumb?.Href?.Split('/').Last();

            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Set breadcrumbs for a specific page (not folder navigation).
    /// </summary>
    public void SetBreadcrumbs(params BreadcrumbItem[] items)
    {
        _breadcrumbs.Clear();
        _breadcrumbs.Add(new BreadcrumbItem("Home", "/", icon: Icons.Material.Filled.Home));
        _breadcrumbs.AddRange(items);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clear all breadcrumbs and return to root.
    /// </summary>
    public void Reset()
    {
        _currentFolderId = null;
        _breadcrumbs.Clear();
        _breadcrumbs.Add(new BreadcrumbItem("Home", "/", icon: Icons.Material.Filled.Home));
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
