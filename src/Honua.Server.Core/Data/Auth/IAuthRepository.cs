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

    /// <summary>
    /// Retrieves user credentials by username for local authentication.
    /// </summary>
    /// <param name="username">The username to look up (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>User credentials including password hash and salt if found, null if username does not exist.</returns>
    ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user credentials by user ID.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>User credentials including password hash and salt if found, null if user ID does not exist.</returns>
    ValueTask<AuthUserCredentials?> GetCredentialsByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user account state after a failed login attempt.
    /// Increments failure count and optionally locks the account after excessive failures.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="failedAttempts">The new total count of consecutive failed login attempts.</param>
    /// <param name="failedAtUtc">The timestamp of the failed login attempt.</param>
    /// <param name="lockUser">Whether to lock the user account due to excessive failures.</param>
    /// <param name="auditContext">Optional audit context for tracking who made the change.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask UpdateLoginFailureAsync(string userId, int failedAttempts, DateTimeOffset failedAtUtc, bool lockUser, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user account state after a successful login.
    /// Resets failure count and updates last login timestamp.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="loginAtUtc">The timestamp of the successful login.</param>
    /// <param name="auditContext">Optional audit context for tracking who made the change.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask UpdateLoginSuccessAsync(string userId, DateTimeOffset loginAtUtc, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new local user account with password-based authentication.
    /// </summary>
    /// <param name="username">The unique username for the account.</param>
    /// <param name="email">The optional email address for the account.</param>
    /// <param name="passwordHash">The hashed password bytes (from IPasswordHasher).</param>
    /// <param name="salt">The salt bytes used during password hashing.</param>
    /// <param name="hashAlgorithm">The hashing algorithm name (e.g., "PBKDF2").</param>
    /// <param name="hashParameters">The algorithm parameters (e.g., iteration count, key length).</param>
    /// <param name="roles">The initial roles to assign to the user.</param>
    /// <param name="auditContext">Optional audit context for tracking who created the user.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The unique user ID of the newly created account.</returns>
    ValueTask<string> CreateLocalUserAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the password for an existing local user account.
    /// Used for password reset and password change operations.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="passwordHash">The new hashed password bytes (from IPasswordHasher).</param>
    /// <param name="salt">The new salt bytes used during password hashing.</param>
    /// <param name="hashAlgorithm">The hashing algorithm name (e.g., "PBKDF2").</param>
    /// <param name="hashParameters">The algorithm parameters (e.g., iteration count, key length).</param>
    /// <param name="auditContext">Optional audit context for tracking who changed the password.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask SetLocalUserPasswordAsync(string userId, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns roles to a user account, replacing any existing role assignments.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="roles">The roles to assign (e.g., "admin", "user", "viewer").</param>
    /// <param name="auditContext">Optional audit context for tracking who assigned the roles.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user account details including roles and status.
    /// </summary>
    /// <param name="username">The username to look up (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>User account details if found, null if username does not exist.</returns>
    ValueTask<AuthUser?> GetUserAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit records for a specific user.
    /// </summary>
    /// <param name="userId">The unique user identifier.</param>
    /// <param name="limit">Maximum number of records to return (default 100).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of audit records ordered by timestamp descending.</returns>
    ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit records for a specific action type across all users.
    /// </summary>
    /// <param name="action">The action type to filter by (e.g., "login", "password_change").</param>
    /// <param name="limit">Maximum number of records to return (default 100).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of audit records ordered by timestamp descending.</returns>
    ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsByActionAsync(string action, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent failed authentication attempts within a time window.
    /// Used for detecting brute-force attacks and suspicious activity.
    /// </summary>
    /// <param name="window">The time window to search (e.g., last 1 hour).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of failed authentication audit records ordered by timestamp descending.</returns>
    ValueTask<IReadOnlyList<AuditRecord>> GetRecentFailedAuthenticationsAsync(TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges old audit records beyond the retention period.
    /// Used for GDPR compliance and database maintenance.
    /// </summary>
    /// <param name="retentionPeriod">Records older than this period will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The number of audit records deleted.</returns>
    ValueTask<int> PurgeOldAuditRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

public sealed record AuthUser(string Id, string Username, string? Email, bool IsActive, bool IsLocked, IReadOnlyCollection<string> Roles);
