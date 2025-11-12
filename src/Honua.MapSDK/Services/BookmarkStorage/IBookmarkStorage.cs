// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.MapSDK.Models;

namespace Honua.MapSDK.Services.BookmarkStorage;

/// <summary>
/// Interface for bookmark storage providers
/// Allows different storage backends (localStorage, API, database, etc.)
/// </summary>
public interface IBookmarkStorage
{
    /// <summary>
    /// Get all bookmarks
    /// </summary>
    Task<List<Bookmark>> GetAllAsync();

    /// <summary>
    /// Get a specific bookmark by ID
    /// </summary>
    Task<Bookmark?> GetByIdAsync(string id);

    /// <summary>
    /// Save a bookmark (create or update)
    /// </summary>
    /// <returns>The ID of the saved bookmark</returns>
    Task<string> SaveAsync(Bookmark bookmark);

    /// <summary>
    /// Delete a bookmark
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Search bookmarks by query string
    /// Searches name, description, and tags
    /// </summary>
    Task<List<Bookmark>> SearchAsync(string query);

    /// <summary>
    /// Get bookmarks in a specific folder
    /// </summary>
    Task<List<Bookmark>> GetByFolderAsync(string? folderId);

    /// <summary>
    /// Get all folders
    /// </summary>
    Task<List<BookmarkFolder>> GetFoldersAsync();

    /// <summary>
    /// Save a folder (create or update)
    /// </summary>
    Task<string> SaveFolderAsync(BookmarkFolder folder);

    /// <summary>
    /// Delete a folder and optionally its bookmarks
    /// </summary>
    Task DeleteFolderAsync(string id, bool deleteBookmarks = false);

    /// <summary>
    /// Export all bookmarks and folders as JSON
    /// </summary>
    Task<string> ExportAsync();

    /// <summary>
    /// Import bookmarks and folders from JSON
    /// </summary>
    Task ImportAsync(string json);
}
