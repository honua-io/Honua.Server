// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Data.Auth;

public sealed record BootstrapState(bool IsCompleted, string? Mode);

public sealed record AuditRecord(
    long Id,
    string UserId,
    string Action,
    string? Details,
    string? OldValue,
    string? NewValue,
    string? ActorId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt);

public sealed record AuditContext(
    string? ActorId = null,
    string? IpAddress = null,
    string? UserAgent = null)
{
    public static readonly AuditContext Empty = new();
}

public sealed record AuthUserCredentials(
    string Id,
    string Username,
    string? Email,
    bool IsActive,
    bool IsLocked,
    bool IsServiceAccount,
    int FailedAttempts,
    DateTimeOffset? LastFailedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? PasswordChangedAt,
    DateTimeOffset? PasswordExpiresAt,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    string HashAlgorithm,
    string HashParameters,
    IReadOnlyCollection<string> Roles);

/// <summary>
/// Repository interface for authentication, user credential management, and security audit logging.
/// Provides methods for user authentication, password management, role assignment, and audit trail tracking.
/// </summary>
/// <remarks>
/// This interface is implemented for multiple database providers (PostgreSQL, MySQL, SQLite, SQL Server)
/// to support flexible deployment options. All operations include audit logging for security compliance.
/// </remarks>
public interface IAuthRepository
{
    /// <summary>
    /// Ensures the authentication database schema is initialized and ready for use.
    /// Creates necessary tables, indexes, and default data if they don't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current bootstrap state indicating whether initial administrator setup is complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="BootstrapState"/> indicating completion status and authentication mode.</returns>
    ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the bootstrap process as completed with the specified authentication mode.
    /// This prevents additional administrators from being created through the bootstrap flow.
    /// </summary>
    /// <param name="mode">The authentication mode used (e.g., "local", "oidc").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new local administrator account with username/password authentication.
    /// This is typically used during initial bootstrap or for creating the first admin user.
    /// </summary>
    /// <param name="username">The username for the administrator account.</param>
    /// <param name="email">The optional email address for the administrator.</param>
    /// <param name="passwordHash">The hashed password bytes.</param>
    /// <param name="salt">The password salt bytes.</param>
    /// <param name="hashAlgorithm">The name of the hashing algorithm used (e.g., "PBKDF2").</param>
    /// <param name="hashParameters">The algorithm parameters (e.g., iteration count, salt size).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask CreateLocalAdministratorAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new administrator account authenticated via OIDC (OpenID Connect).
    /// This is used when authentication is delegated to an external identity provider.
    /// </summary>
    /// <param name="subject">The OIDC subject identifier (unique user ID from identity provider).</param>
    /// <param name="username">The optional username from the identity provider.</param>
    /// <param name="email">The optional email address from the identity provider.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default);

    ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    ValueTask<AuthUserCredentials?> GetCredentialsByIdAsync(string userId, CancellationToken cancellationToken = default);

    ValueTask UpdateLoginFailureAsync(string userId, int failedAttempts, DateTimeOffset failedAtUtc, bool lockUser, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    ValueTask UpdateLoginSuccessAsync(string userId, DateTimeOffset loginAtUtc, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    ValueTask<string> CreateLocalUserAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    ValueTask SetLocalUserPasswordAsync(string userId, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    ValueTask AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    ValueTask<AuthUser?> GetUserAsync(string username, CancellationToken cancellationToken = default);

    // Audit log queries
    ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsByActionAsync(string action, int limit = 100, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AuditRecord>> GetRecentFailedAuthenticationsAsync(TimeSpan window, CancellationToken cancellationToken = default);

    ValueTask<int> PurgeOldAuditRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

public sealed record AuthUser(string Id, string Username, string? Email, bool IsActive, bool IsLocked, IReadOnlyCollection<string> Roles);
