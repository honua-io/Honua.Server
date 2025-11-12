// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Models;

namespace Honua.Server.Core.Data.Sharing;

/// <summary>
/// Repository interface for managing share tokens and comments
/// </summary>
public interface IShareRepository
{
    /// <summary>
    /// Ensures the sharing database schema is initialized
    /// </summary>
    ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new share token
    /// </summary>
    ValueTask<ShareToken> CreateShareTokenAsync(ShareToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a share token by its token value
    /// </summary>
    ValueTask<ShareToken?> GetShareTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all share tokens for a specific map
    /// </summary>
    ValueTask<IReadOnlyList<ShareToken>> GetShareTokensByMapIdAsync(string mapId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a share token
    /// </summary>
    ValueTask UpdateShareTokenAsync(ShareToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a share token
    /// </summary>
    ValueTask DeactivateShareTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the access count for a share token
    /// </summary>
    ValueTask IncrementAccessCountAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired share tokens
    /// </summary>
    ValueTask<int> DeleteExpiredTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new comment on a shared map
    /// </summary>
    ValueTask<ShareComment> CreateCommentAsync(ShareComment comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comments for a specific share token
    /// </summary>
    ValueTask<IReadOnlyList<ShareComment>> GetCommentsByTokenAsync(string token, bool includeUnapproved = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comments for a specific map
    /// </summary>
    ValueTask<IReadOnlyList<ShareComment>> GetCommentsByMapIdAsync(string mapId, bool includeUnapproved = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a comment
    /// </summary>
    ValueTask ApproveCommentAsync(string commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a comment
    /// </summary>
    ValueTask DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending comments awaiting moderation
    /// </summary>
    ValueTask<IReadOnlyList<ShareComment>> GetPendingCommentsAsync(int limit = 100, CancellationToken cancellationToken = default);
}
