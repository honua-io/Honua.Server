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

public interface IAuthRepository
{
    ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default);

    ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default);

    ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default);

    ValueTask CreateLocalAdministratorAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, CancellationToken cancellationToken = default);

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
