// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Store for managing SAML authentication sessions
/// </summary>
public interface ISamlSessionStore
{
    /// <summary>
    /// Creates a new authentication session
    /// </summary>
    Task<SamlAuthenticationSession> CreateSessionAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        string requestId,
        string? relayState,
        TimeSpan validityPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by request ID
    /// </summary>
    Task<SamlAuthenticationSession?> GetSessionByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a session as consumed
    /// </summary>
    Task ConsumeSessionAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically tries to consume a session (returns false if already consumed)
    /// </summary>
    Task<bool> TryConsumeSessionAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired sessions
    /// </summary>
    Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
