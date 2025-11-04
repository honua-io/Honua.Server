// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.Auth;

internal interface IRelationalAuthDialect
{
    string ProviderName { get; }
    IReadOnlyList<string> SchemaStatements { get; }
    string LimitClause(string parameterName);
}

internal abstract class RelationalAuthRepositoryBase : IAuthRepository
{
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ILogger _logger;
    private readonly AuthMetrics? _metrics;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly IRelationalAuthDialect _dialect;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    static RelationalAuthRepositoryBase()
    {
        RegisterTypeMap<BootstrapRow>();
        RegisterTypeMap<AuthUserRow>();
        RegisterTypeMap<AuthUserBasicRow>();
        RegisterTypeMap<AuditRow>();
    }

    private static void RegisterTypeMap<T>()
    {
        SqlMapper.SetTypeMap(typeof(T), new CustomPropertyTypeMap(
            typeof(T),
            (type, columnName) => type.GetProperty(ConvertToPropertyName(columnName), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)));
    }

    private static string ConvertToPropertyName(string columnName)
    {
        if (columnName.IsNullOrEmpty())
        {
            return columnName;
        }

        Span<char> buffer = stackalloc char[columnName.Length];
        var writeIndex = 0;
        var capitalize = true;

        foreach (var ch in columnName)
        {
            if (ch == '_')
            {
                capitalize = true;
                continue;
            }

            buffer[writeIndex++] = capitalize ? char.ToUpperInvariant(ch) : ch;
            capitalize = false;
        }

        return new string(buffer[..writeIndex]);
    }

    protected RelationalAuthRepositoryBase(
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ILogger logger,
        AuthMetrics? metrics,
        ResiliencePipeline retryPipeline,
        IRelationalAuthDialect dialect)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
        _retryPipeline = retryPipeline ?? throw new ArgumentNullException(nameof(retryPipeline));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    }

    protected abstract DbConnection CreateConnection();

    public async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = CreateConnection();
            await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

            foreach (var statement in _dialect.SchemaStatements)
            {
                if (statement.IsNullOrWhiteSpace())
                {
                    continue;
                }

                await ExecuteNonQueryAsync(connection, statement, null, null, cancellationToken).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"SELECT completed_at, mode FROM auth_bootstrap_state WHERE id = 1;";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var row = await QuerySingleOrDefaultAsync<BootstrapRow>(connection, sql, null, null, cancellationToken).ConfigureAwait(false);

        if (row is null || row.CompletedAt is null)
        {
            return new BootstrapState(false, null);
        }

        return new BootstrapState(true, row.Mode);
    }

    public async ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"UPDATE auth_bootstrap_state SET completed_at = @CompletedAt, mode = @Mode WHERE id = 1;";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        await ExecuteNonQueryAsync(connection, sql, new { CompletedAt = now.UtcDateTime, Mode = mode }, null, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CreateLocalAdministratorAsync(
        string username,
        string? email,
        byte[] passwordHash,
        byte[] salt,
        string hashAlgorithm,
        string hashParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashParameters);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        try
        {
            var userId = Guid.NewGuid().ToString("D");
            var now = DateTimeOffset.UtcNow;
            var localOptions = _options.CurrentValue.Local;
            var expirationPeriod = localOptions.PasswordExpiration.ExpirationPeriod;
            var expiresAt = localOptions.PasswordExpiration.Enabled ? now.Add(expirationPeriod) : (DateTimeOffset?)null;

            const string insertUserSql = @"INSERT INTO auth_users
                (id, subject, username, email, password_hash, password_salt, hash_algorithm, hash_parameters,
                 is_active, is_locked, is_service_account, failed_attempts, created_at, updated_at, password_changed_at, password_expires_at)
                VALUES (@Id, NULL, @Username, @Email, @PasswordHash, @PasswordSalt, @HashAlgorithm, @HashParameters,
                        @IsActive, @IsLocked, @IsServiceAccount, 0, @CreatedAt, @UpdatedAt, @PasswordChangedAt, @PasswordExpiresAt);";

            await ExecuteNonQueryAsync(connection, insertUserSql, new
            {
                Id = userId,
                Username = username,
                Email = email.IsNullOrWhiteSpace() ? null : email,
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                HashAlgorithm = hashAlgorithm,
                HashParameters = hashParameters,
                IsActive = true,
                IsLocked = false,
                IsServiceAccount = false,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                PasswordChangedAt = now.UtcDateTime,
                PasswordExpiresAt = expiresAt?.UtcDateTime
            }, transaction, cancellationToken).ConfigureAwait(false);

            await AssignRolesInternalAsync(connection, transaction, userId, new[] { "administrator" }, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        try
        {
            var userId = Guid.NewGuid().ToString("D");
            var now = DateTimeOffset.UtcNow;

            const string insertUserSql = @"INSERT INTO auth_users
                (id, subject, username, email, is_active, is_locked, is_service_account, failed_attempts, created_at, updated_at)
                VALUES (@Id, @Subject, @Username, @Email, @IsActive, @IsLocked, @IsServiceAccount, 0, @CreatedAt, @UpdatedAt);";

            await ExecuteNonQueryAsync(connection, insertUserSql, new
            {
                Id = userId,
                Subject = subject,
                Username = username.IsNullOrWhiteSpace() ? null : username,
                Email = email.IsNullOrWhiteSpace() ? null : email,
                IsActive = true,
                IsLocked = false,
                IsServiceAccount = false,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime
            }, transaction, cancellationToken).ConfigureAwait(false);

            await AssignRolesInternalAsync(connection, transaction, userId, new[] { "administrator" }, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"SELECT id, username, email, is_active, is_locked, is_service_account, failed_attempts,
                            last_failed_at, last_login_at, password_changed_at, password_expires_at,
                            password_hash, password_salt, hash_algorithm, hash_parameters
                     FROM auth_users
                     WHERE LOWER(username) = LOWER(@Username)";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var row = await QuerySingleOrDefaultAsync<AuthUserRow>(connection, sql, new { Username = username }, null, cancellationToken).ConfigureAwait(false);

        if (row is null || row.PasswordHash is null || row.PasswordSalt is null || row.HashAlgorithm.IsNullOrWhiteSpace())
        {
            return null;
        }

        var roles = await QueryAsync<string>(connection,
            @"SELECT role_id FROM auth_user_roles WHERE user_id = @UserId",
            new { UserId = row.Id }, null, cancellationToken).ConfigureAwait(false);

        return new AuthUserCredentials(
            row.Id,
            row.Username ?? string.Empty,
            row.Email,
            row.IsActive,
            row.IsLocked,
            row.IsServiceAccount,
            row.FailedAttempts,
            ToDateTimeOffset(row.LastFailedAt),
            ToDateTimeOffset(row.LastLoginAt),
            ToDateTimeOffset(row.PasswordChangedAt),
            ToDateTimeOffset(row.PasswordExpiresAt),
            row.PasswordHash,
            row.PasswordSalt,
            row.HashAlgorithm ?? string.Empty,
            row.HashParameters ?? string.Empty,
            roles.ToArray());
    }

    public async ValueTask<AuthUserCredentials?> GetCredentialsByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"SELECT id, username, email, is_active, is_locked, is_service_account, failed_attempts,
                                    last_failed_at, last_login_at, password_changed_at, password_expires_at,
                                    password_hash, password_salt, hash_algorithm, hash_parameters
                             FROM auth_users
                             WHERE id = @UserId";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var row = await QuerySingleOrDefaultAsync<AuthUserRow>(connection, sql, new { UserId = userId }, null, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var roles = await QueryAsync<string>(connection,
            @"SELECT role_id FROM auth_user_roles WHERE user_id = @UserId",
            new { UserId = row.Id }, null, cancellationToken).ConfigureAwait(false);

        return new AuthUserCredentials(
            row.Id,
            row.Username ?? string.Empty,
            row.Email,
            row.IsActive,
            row.IsLocked,
            row.IsServiceAccount,
            row.FailedAttempts,
            ToDateTimeOffset(row.LastFailedAt),
            ToDateTimeOffset(row.LastLoginAt),
            ToDateTimeOffset(row.PasswordChangedAt),
            ToDateTimeOffset(row.PasswordExpiresAt),
            row.PasswordHash ?? Array.Empty<byte>(),
            row.PasswordSalt ?? Array.Empty<byte>(),
            row.HashAlgorithm ?? string.Empty,
            row.HashParameters ?? string.Empty,
            roles.ToArray());
    }

    public async ValueTask UpdateLoginFailureAsync(
        string userId,
        int failedAttempts,
        DateTimeOffset failedAtUtc,
        bool lockUser,
        AuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"UPDATE auth_users
SET failed_attempts = @FailedAttempts,
    last_failed_at = @LastFailedAt,
    is_locked = @IsLocked,
    updated_at = @UpdatedAt
WHERE id = @UserId;";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, sql, new
        {
            FailedAttempts = failedAttempts,
            LastFailedAt = failedAtUtc.UtcDateTime,
            IsLocked = lockUser,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId
        }, null, cancellationToken).ConfigureAwait(false);

        await WriteAuditRecordAsync(connection, null, userId,
            lockUser ? "account_locked" : "login_failed",
            lockUser ? "Account locked due to repeated failed login attempts." : "Login attempt failed.",
            null,
            null,
            auditContext,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpdateLoginSuccessAsync(
        string userId,
        DateTimeOffset loginAtUtc,
        AuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"UPDATE auth_users
SET failed_attempts = 0,
    last_login_at = @LastLoginAt,
    updated_at = @UpdatedAt
WHERE id = @UserId;";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        await ExecuteNonQueryAsync(connection, sql, new
        {
            LastLoginAt = loginAtUtc.UtcDateTime,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId
        }, null, cancellationToken).ConfigureAwait(false);

        await WriteAuditRecordAsync(connection, null, userId,
            "login_success",
            "Successful authentication.",
            null,
            null,
            auditContext,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string> CreateLocalUserAsync(
        string username,
        string? email,
        byte[] passwordHash,
        byte[] salt,
        string hashAlgorithm,
        string hashParameters,
        IReadOnlyCollection<string> roles,
        AuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashParameters);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        try
        {
            var userId = Guid.NewGuid().ToString("D");
            var now = DateTimeOffset.UtcNow;
            var localOptions = _options.CurrentValue.Local;
            var expirationPeriod = localOptions.PasswordExpiration.ExpirationPeriod;
            var expiresAt = localOptions.PasswordExpiration.Enabled ? now.Add(expirationPeriod) : (DateTimeOffset?)null;

            const string insertUserSql = @"INSERT INTO auth_users
                (id, subject, username, email, password_hash, password_salt, hash_algorithm, hash_parameters,
                 is_active, is_locked, is_service_account, failed_attempts, created_at, updated_at, password_changed_at, password_expires_at)
                VALUES (@Id, NULL, @Username, @Email, @PasswordHash, @PasswordSalt, @HashAlgorithm, @HashParameters,
                        @IsActive, @IsLocked, @IsServiceAccount, 0, @CreatedAt, @UpdatedAt, @PasswordChangedAt, @PasswordExpiresAt);";

            await ExecuteNonQueryAsync(connection, insertUserSql, new
            {
                Id = userId,
                Username = username,
                Email = email.IsNullOrWhiteSpace() ? null : email,
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                HashAlgorithm = hashAlgorithm,
                HashParameters = hashParameters,
                IsActive = true,
                IsLocked = false,
                IsServiceAccount = false,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                PasswordChangedAt = now.UtcDateTime,
                PasswordExpiresAt = expiresAt?.UtcDateTime
            }, transaction, cancellationToken).ConfigureAwait(false);

            await AssignRolesInternalAsync(connection, transaction, userId, roles, cancellationToken).ConfigureAwait(false);

            await WriteAuditRecordAsync(connection, transaction, userId,
                "user_created",
                "Local user account created.",
                null,
                string.Join(", ", roles),
                auditContext,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _metrics?.RecordUserCreated(auditContext?.ActorId);

            return userId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask SetLocalUserPasswordAsync(
        string userId,
        byte[] passwordHash,
        byte[] salt,
        string hashAlgorithm,
        string hashParameters,
        AuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashParameters);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTimeOffset.UtcNow;
            var localOptions = _options.CurrentValue.Local;
            var expirationPeriod = localOptions.PasswordExpiration.ExpirationPeriod;
            var expiresAt = localOptions.PasswordExpiration.Enabled ? now.Add(expirationPeriod) : (DateTimeOffset?)null;

            const string sql = @"UPDATE auth_users
SET password_hash = @PasswordHash,
    password_salt = @PasswordSalt,
    hash_algorithm = @HashAlgorithm,
    hash_parameters = @HashParameters,
    password_changed_at = @PasswordChangedAt,
    password_expires_at = @PasswordExpiresAt,
    failed_attempts = 0,
    is_locked = 0,
    updated_at = @UpdatedAt
WHERE id = @UserId;";

            var affected = await ExecuteNonQueryAsync(connection, sql, new
            {
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                HashAlgorithm = hashAlgorithm,
                HashParameters = hashParameters,
                PasswordChangedAt = now.UtcDateTime,
                PasswordExpiresAt = expiresAt?.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                UserId = userId
            }, transaction, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new InvalidOperationException($"Failed to update password for user '{userId}': user not found.");
            }

            await WriteAuditRecordAsync(connection, transaction, userId,
                "password_changed",
                "User password updated.",
                null,
                null,
                auditContext,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _metrics?.RecordPasswordChanged(auditContext?.ActorId);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        roles ??= Array.Empty<string>();

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        try
        {
            var existingRoles = await QueryAsync<string>(connection,
                "SELECT role_id FROM auth_user_roles WHERE user_id = @UserId",
                new { UserId = userId }, transaction, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection,
                "DELETE FROM auth_user_roles WHERE user_id = @UserId;",
                new { UserId = userId }, transaction, cancellationToken).ConfigureAwait(false);

            await AssignRolesInternalAsync(connection, transaction, userId, roles, cancellationToken).ConfigureAwait(false);

            var oldValue = existingRoles.Any() ? string.Join(", ", existingRoles) : "none";
            var newValue = roles.Any() ? string.Join(", ", roles) : "none";

            await WriteAuditRecordAsync(connection, transaction, userId,
                "roles_changed",
                "User role assignments updated.",
                oldValue,
                newValue,
                auditContext,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _metrics?.RecordRolesChanged(auditContext?.ActorId);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<AuthUser?> GetUserAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"SELECT id, username, email, is_active, is_locked
FROM auth_users
WHERE LOWER(username) = LOWER(@Username);";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var row = await QuerySingleOrDefaultAsync<AuthUserBasicRow>(connection, sql, new { Username = username }, null, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var roles = await QueryAsync<string>(connection,
            "SELECT role_id FROM auth_user_roles WHERE user_id = @UserId",
            new { UserId = row.Id }, null, cancellationToken).ConfigureAwait(false);

        return new AuthUser(
            row.Id,
            row.Username ?? string.Empty,
            row.Email,
            row.IsActive,
            row.IsLocked,
            roles.ToArray());
    }

    public async ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var baseSql = @"SELECT id, user_id, action, details, old_value, new_value, actor_id, ip_address, user_agent, occurred_at
FROM auth_credentials_audit
WHERE user_id = @UserId
ORDER BY occurred_at DESC";

        var sql = $"{baseSql} {_dialect.LimitClause("@Limit")}";

        var parameters = new { UserId = userId, Limit = Math.Min(limit, 1000) };

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await QueryAsync<AuditRow>(connection, sql, parameters, null, cancellationToken).ConfigureAwait(false);
        return rows.Select(ConvertAuditRow).ToArray();
    }

    public async ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsByActionAsync(string action, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var baseSql = @"SELECT id, user_id, action, details, old_value, new_value, actor_id, ip_address, user_agent, occurred_at
FROM auth_credentials_audit
WHERE action = @Action
ORDER BY occurred_at DESC";

        var sql = $"{baseSql} {_dialect.LimitClause("@Limit")}";

        var parameters = new { Action = action, Limit = Math.Min(limit, 1000) };

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await QueryAsync<AuditRow>(connection, sql, parameters, null, cancellationToken).ConfigureAwait(false);
        return rows.Select(ConvertAuditRow).ToArray();
    }

    public async ValueTask<IReadOnlyList<AuditRecord>> GetRecentFailedAuthenticationsAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = DateTimeOffset.UtcNow.Subtract(window);

        const string sql = @"SELECT id, user_id, action, details, old_value, new_value, actor_id, ip_address, user_agent, occurred_at
FROM auth_credentials_audit
WHERE action IN ('login_failed', 'account_locked')
  AND occurred_at >= @Cutoff
ORDER BY occurred_at DESC";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var rows = await QueryAsync<AuditRow>(connection, sql, new { Cutoff = cutoff.UtcDateTime }, null, cancellationToken).ConfigureAwait(false);
        return rows.Select(ConvertAuditRow).ToArray();
    }

    public async ValueTask<int> PurgeOldAuditRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod);

        const string sql = @"DELETE FROM auth_credentials_audit
WHERE occurred_at < @Cutoff;";

        await using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var deleted = await ExecuteNonQueryAsync(connection, sql, new { Cutoff = cutoff.UtcDateTime }, null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Purged {Count} audit records older than {Cutoff:u}", deleted, cutoff);

        return deleted;
    }

    private async Task AssignRolesInternalAsync(DbConnection connection, DbTransaction? transaction, string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        if (roles is null || roles.Count == 0)
        {
            return;
        }

        const string insertRoleSql = @"INSERT INTO auth_user_roles (user_id, role_id, granted_at)
SELECT @UserId, @RoleId, @GrantedAt
WHERE NOT EXISTS (
    SELECT 1 FROM auth_user_roles WHERE user_id = @UserId AND role_id = @RoleId
);";

        var now = DateTimeOffset.UtcNow.UtcDateTime;

        foreach (var role in roles.Where(r => r.HasValue()).Select(r => r.Trim().ToLowerInvariant()).Distinct())
        {
            await ExecuteNonQueryAsync(connection, insertRoleSql, new
            {
                UserId = userId,
                RoleId = role,
                GrantedAt = now
            }, transaction, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteAuditRecordAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string userId,
        string action,
        string? details,
        string? oldValue,
        string? newValue,
        AuditContext? auditContext,
        CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO auth_credentials_audit
            (user_id, action, details, old_value, new_value, actor_id, ip_address, user_agent, occurred_at)
            VALUES (@UserId, @Action, @Details, @OldValue, @NewValue, @ActorId, @IpAddress, @UserAgent, @OccurredAt);";

        await ExecuteNonQueryAsync(connection, sql, new
        {
            UserId = userId,
            Action = action,
            Details = details,
            OldValue = oldValue,
            NewValue = newValue,
            ActorId = auditContext?.ActorId,
            IpAddress = auditContext?.IpAddress,
            UserAgent = auditContext?.UserAgent,
            OccurredAt = DateTime.UtcNow
        }, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task OpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await _retryPipeline.ExecuteAsync(async ct =>
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);

        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task ConfigureConnectionAsync(DbConnection connection, CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<DbTransaction> BeginTransactionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        return await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        object? parameters,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return await _retryPipeline.ExecuteAsync(async ct =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: ct);
            return await connection.ExecuteAsync(command).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> QuerySingleOrDefaultAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return await _retryPipeline.ExecuteAsync(async ct =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: ct);
            return await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return await _retryPipeline.ExecuteAsync(async ct =>
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: ct);
            return await connection.QueryAsync<T>(command).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }

    private static AuditRecord ConvertAuditRow(AuditRow row)
    {
        return new AuditRecord(
            row.Id,
            row.UserId,
            row.Action,
            row.Details,
            row.OldValue,
            row.NewValue,
            row.ActorId,
            row.IpAddress,
            row.UserAgent,
            ToDateTimeOffset(row.OccurredAt) ?? DateTimeOffset.UtcNow);
    }

    private sealed class BootstrapRow
    {
        public DateTime? CompletedAt { get; init; }
        public string? Mode { get; init; }
    }

    private sealed class AuthUserRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public string Id { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.Column("username")]
        public string? Username { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("email")]
        public string? Email { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_active")]
        public bool IsActive { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_locked")]
        public bool IsLocked { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_service_account")]
        public bool IsServiceAccount { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("failed_attempts")]
        public int FailedAttempts { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("last_failed_at")]
        public DateTime? LastFailedAt { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("password_changed_at")]
        public DateTime? PasswordChangedAt { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("password_expires_at")]
        public DateTime? PasswordExpiresAt { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("password_hash")]
        public byte[]? PasswordHash { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("password_salt")]
        public byte[]? PasswordSalt { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("hash_algorithm")]
        public string? HashAlgorithm { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("hash_parameters")]
        public string? HashParameters { get; set; }
    }

    private sealed class AuthUserBasicRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public string Id { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.Column("username")]
        public string? Username { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("email")]
        public string? Email { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_active")]
        public bool IsActive { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("is_locked")]
        public bool IsLocked { get; set; }
    }

    private sealed class AuditRow
    {
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public long Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.Column("action")]
        public string Action { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.Column("details")]
        public string? Details { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("old_value")]
        public string? OldValue { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("new_value")]
        public string? NewValue { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("actor_id")]
        public string? ActorId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("ip_address")]
        public string? IpAddress { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("user_agent")]
        public string? UserAgent { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("occurred_at")]
        public DateTime? OccurredAt { get; set; }
    }
}
