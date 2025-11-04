// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Auth;

/// <summary>
/// Service for managing JWT token revocation (blacklisting).
/// Prevents compromised tokens from being used after logout or security incidents.
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// Revokes a specific token by its JWT ID (jti claim).
    /// </summary>
    /// <param name="tokenId">The JWT ID (jti claim) of the token to revoke.</param>
    /// <param name="expiresAt">When the token naturally expires (for automatic cleanup).</param>
    /// <param name="reason">The reason for revocation (for audit logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeTokenAsync(string tokenId, DateTimeOffset expiresAt, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a token has been revoked.
    /// </summary>
    /// <param name="tokenId">The JWT ID (jti claim) to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token is revoked, false otherwise.</returns>
    Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all tokens for a specific user.
    /// Useful when a user's account is compromised or they reset their password.
    /// </summary>
    /// <param name="userId">The user ID (sub claim) whose tokens should be revoked.</param>
    /// <param name="reason">The reason for revocation (for audit logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeAllUserTokensAsync(string userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes expired token revocations from storage.
    /// Should be called periodically to prevent unbounded growth.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of expired revocations that were cleaned up.</returns>
    Task<int> CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default);
}
