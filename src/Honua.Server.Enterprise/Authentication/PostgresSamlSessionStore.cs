// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// PostgreSQL implementation of SAML session store
/// </summary>
public class PostgresSamlSessionStore : ISamlSessionStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresSamlSessionStore> _logger;

    public PostgresSamlSessionStore(
        string connectionString,
        ILogger<PostgresSamlSessionStore> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SamlAuthenticationSession> CreateSessionAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        string requestId,
        string? relayState,
        TimeSpan validityPeriod,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var session = new SamlAuthenticationSession
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            IdpConfigurationId = idpConfigurationId,
            RequestId = requestId,
            RelayState = relayState,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(validityPeriod),
            Consumed = false
        };

        const string sql = @"
            INSERT INTO saml_sessions (
                id,
                tenant_id,
                idp_configuration_id,
                request_id,
                relay_state,
                created_at,
                expires_at,
                consumed
            ) VALUES (
                @Id,
                @TenantId,
                @IdpConfigurationId,
                @RequestId,
                @RelayState,
                @CreatedAt,
                @ExpiresAt,
                @Consumed
            )";

        await connection.ExecuteAsync(sql, session);

        _logger.LogDebug(
            "Created SAML session {SessionId} for request {RequestId}",
            session.Id, requestId);

        return session;
    }

    public async Task<SamlAuthenticationSession?> GetSessionByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                idp_configuration_id,
                request_id,
                relay_state,
                created_at,
                expires_at,
                consumed
            FROM saml_sessions
            WHERE request_id = @RequestId
              AND expires_at > @Now";

        var session = await connection.QuerySingleOrDefaultAsync<SamlAuthenticationSession>(
            sql,
            new { RequestId = requestId, Now = DateTimeOffset.UtcNow });

        return session;
    }

    public async Task ConsumeSessionAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE saml_sessions
            SET consumed = true
            WHERE request_id = @RequestId";

        await connection.ExecuteAsync(sql, new { RequestId = requestId });

        _logger.LogDebug("Consumed SAML session for request {RequestId}", requestId);
    }

    public async Task<bool> TryConsumeSessionAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE saml_sessions
            SET consumed = true
            WHERE request_id = @RequestId
              AND consumed = false
              AND expires_at > @Now";

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { RequestId = requestId, Now = DateTimeOffset.UtcNow });

        if (rowsAffected > 0)
        {
            _logger.LogDebug("Successfully consumed SAML session for request {RequestId}", requestId);
            return true;
        }

        _logger.LogWarning(
            "Failed to consume SAML session for request {RequestId} - already consumed or expired",
            requestId);
        return false;
    }

    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            DELETE FROM saml_sessions
            WHERE expires_at < @Now";

        var deleted = await connection.ExecuteAsync(sql, new { Now = DateTimeOffset.UtcNow });

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired SAML sessions", deleted);
        }
    }
}
