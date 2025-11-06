using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.MapSDK.Models;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services.BookmarkStorage;

/// <summary>
/// LocalStorage-based bookmark storage implementation
/// Stores bookmarks in browser's localStorage for persistence across sessions
/// </summary>
public class LocalStorageBookmarkStorage : IBookmarkStorage
{
    private const string BookmarksKey = "honua.bookmarks";
    private const string FoldersKey = "honua.bookmark-folders";

    private readonly IJSRuntime _jsRuntime;
    private List<Bookmark>? _cachedBookmarks;
    private List<BookmarkFolder>? _cachedFolders;

    public LocalStorageBookmarkStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<Bookmark>> GetAllAsync()
    {
        if (_cachedBookmarks != null)
            return _cachedBookmarks;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", BookmarksKey);
            if (string.IsNullOrEmpty(json))
            {
                _cachedBookmarks = new List<Bookmark>();
                return _cachedBookmarks;
            }

            _cachedBookmarks = JsonSerializer.Deserialize<List<Bookmark>>(json) ?? new List<Bookmark>();
            return _cachedBookmarks;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading bookmarks: {ex.Message}");
            _cachedBookmarks = new List<Bookmark>();
            return _cachedBookmarks;
        }
    }

    public async Task<Bookmark?> GetByIdAsync(string id)
    {
        var bookmarks = await GetAllAsync();
        return bookmarks.FirstOrDefault(b => b.Id == id);
    }

    public async Task<string> SaveAsync(Bookmark bookmark)
    {
        var bookmarks = await GetAllAsync();

        var existing = bookmarks.FirstOrDefault(b => b.Id == bookmark.Id);
        if (existing != null)
        {
            // Update existing
            var index = bookmarks.IndexOf(existing);
            bookmarks[index] = bookmark;
        }
        else
        {
            // Add new
            if (string.IsNullOrEmpty(bookmark.Id))
            {
                bookmark.Id = Guid.NewGuid().ToString("N");
            }
            bookmarks.Add(bookmark);
        }

        await SaveBookmarksAsync(bookmarks);
        return bookmark.Id;
    }

    public async Task DeleteAsync(string id)
    {
        var bookmarks = await GetAllAsync();
        var bookmark = bookmarks.FirstOrDefault(b => b.Id == id);
        if (bookmark != null)
        {
            bookmarks.Remove(bookmark);
            await SaveBookmarksAsync(bookmarks);
        }
    }

    public async Task<List<Bookmark>> SearchAsync(string query)
    {
        var bookmarks = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query))
            return bookmarks;

        var lowerQuery = query.ToLowerInvariant();

        return bookmarks.Where(b =>
            (b.Name?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
            (b.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
            b.Tags.Any(t => t.ToLowerInvariant().Contains(lowerQuery))
        ).ToList();
    }

    public async Task<List<Bookmark>> GetByFolderAsync(string? folderId)
    {
        var bookmarks = await GetAllAsync();
        return bookmarks.Where(b => b.FolderId == folderId).ToList();
    }

    public async Task<List<BookmarkFolder>> GetFoldersAsync()
    {
        if (_cachedFolders != null)
            return _cachedFolders;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", FoldersKey);
            if (string.IsNullOrEmpty(json))
            {
                _cachedFolders = new List<BookmarkFolder>();
                return _cachedFolders;
            }

            _cachedFolders = JsonSerializer.Deserialize<List<BookmarkFolder>>(json) ?? new List<BookmarkFolder>();
            return _cachedFolders;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading folders: {ex.Message}");
            _cachedFolders = new List<BookmarkFolder>();
            return _cachedFolders;
        }
    }

    public async Task<string> SaveFolderAsync(BookmarkFolder folder)
    {
        var folders = await GetFoldersAsync();

        var existing = folders.FirstOrDefault(f => f.Id == folder.Id);
        if (existing != null)
        {
            var index = folders.IndexOf(existing);
            folders[index] = folder;
        }
        else
        {
            if (string.IsNullOrEmpty(folder.Id))
            {
                folder.Id = Guid.NewGuid().ToString("N");
            }
            folders.Add(folder);
        }

        await SaveFoldersAsync(folders);
        return folder.Id;
    }

    public async Task DeleteFolderAsync(string id, bool deleteBookmarks = false)
    {
        var folders = await GetFoldersAsync();
        var folder = folders.FirstOrDefault(f => f.Id == id);
        if (folder != null)
        {
            folders.Remove(folder);
            await SaveFoldersAsync(folders);
        }

        if (deleteBookmarks)
        {
            var bookmarks = await GetAllAsync();
            var toDelete = bookmarks.Where(b => b.FolderId == id).ToList();
            foreach (var bookmark in toDelete)
            {
                bookmarks.Remove(bookmark);
            }
            await SaveBookmarksAsync(bookmarks);
        }
        else
        {
            // Move bookmarks to no folder
            var bookmarks = await GetAllAsync();
            var toMove = bookmarks.Where(b => b.FolderId == id).ToList();
            foreach (var bookmark in toMove)
            {
                bookmark.FolderId = null;
            }
            await SaveBookmarksAsync(bookmarks);
        }
    }

    public async Task<string> ExportAsync()
    {
        var bookmarks = await GetAllAsync();
        var folders = await GetFoldersAsync();

        var export = new
        {
            bookmarks,
            folders,
            exportedAt = DateTime.UtcNow,
            version = "1.0"
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task ImportAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("bookmarks", out var bookmarksElement))
            {
                var bookmarks = JsonSerializer.Deserialize<List<Bookmark>>(bookmarksElement.GetRawText());
                if (bookmarks != null)
                {
                    var existing = await GetAllAsync();
                    existing.AddRange(bookmarks);
                    await SaveBookmarksAsync(existing);
                }
            }

            if (root.TryGetProperty("folders", out var foldersElement))
            {
                var folders = JsonSerializer.Deserialize<List<BookmarkFolder>>(foldersElement.GetRawText());
                if (folders != null)
                {
                    var existing = await GetFoldersAsync();
                    existing.AddRange(folders);
                    await SaveFoldersAsync(existing);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error importing bookmarks: {ex.Message}");
            throw;
        }
    }

    private async Task SaveBookmarksAsync(List<Bookmark> bookmarks)
    {
        _cachedBookmarks = bookmarks;
        var json = JsonSerializer.Serialize(bookmarks);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", BookmarksKey, json);
    }

    private async Task SaveFoldersAsync(List<BookmarkFolder> folders)
    {
        _cachedFolders = folders;
        var json = JsonSerializer.Serialize(folders);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", FoldersKey, json);
    }
}
