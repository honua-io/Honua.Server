// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;

namespace Honua.Server.Core.Auth;

/// <summary>
/// Configuration options for token revocation.
/// </summary>
public sealed class TokenRevocationOptions
{
    public const string SectionName = "TokenRevocation";

    /// <summary>
    /// Interval between automatic cleanup operations.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to enable automatic cleanup of expired revocations.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Security posture for Redis failures.
    /// If true (recommended), authentication fails when Redis is unavailable (fail-closed).
    /// If false, authentication succeeds when Redis is unavailable (fail-open).
    /// </summary>
    public bool FailClosedOnRedisError { get; set; } = true;
}

/// <summary>
/// Redis-backed implementation of token revocation service.
/// Stores revoked token IDs with TTL matching JWT expiration for automatic cleanup.
/// </summary>
public sealed class RedisTokenRevocationService : ITokenRevocationService, IHealthCheck, IDisposable
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisTokenRevocationService> _logger;
    private readonly IOptions<TokenRevocationOptions> _options;
    private readonly Meter _meter;
    private readonly Counter<long> _revocationsCounter;
    private readonly Counter<long> _revocationChecksCounter;
    private readonly Counter<long> _cleanupCounter;
    private readonly Histogram<double> _operationDuration;

    private const string RevocationKeyPrefix = "revoked_token:";
    private const string UserRevocationKeyPrefix = "revoked_user:";
    private const string UserRevocationCheckPrefix = "user:";
    private const string RevocationMetadataKeyPrefix = "revocation_meta:";

    private static readonly ActivitySource ActivitySource = new("Honua.Auth.TokenRevocation");

    public RedisTokenRevocationService(
        IDistributedCache cache,
        ILogger<RedisTokenRevocationService> logger,
        IOptions<TokenRevocationOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize OpenTelemetry metrics
        _meter = new Meter("Honua.Auth.TokenRevocation", "1.0.0");
        _revocationsCounter = _meter.CreateCounter<long>("honua.auth.revocations.count",
            description: "Number of token revocations");
        _revocationChecksCounter = _meter.CreateCounter<long>("honua.auth.revocation_checks.count",
            description: "Number of token revocation checks");
        _cleanupCounter = _meter.CreateCounter<long>("honua.auth.revocation_cleanup.count",
            description: "Number of expired revocations cleaned up");
        _operationDuration = _meter.CreateHistogram<double>("honua.auth.revocation.duration",
            unit: "ms", description: "Duration of revocation operations");
    }

    private async Task<bool> CheckUserRevocationAsync(string tokenId, CancellationToken cancellationToken)
    {
        var payload = tokenId.AsSpan(UserRevocationCheckPrefix.Length);
        var separatorIndex = payload.IndexOf('|');
        if (separatorIndex <= 0)
        {
            _logger.LogWarning(
                "SECURITY_AUDIT: Invalid user revocation key format - TokenIdHash={TokenIdHash}",
                HashTokenIdForLogging(tokenId));
            return true;
        }

        var encodedUserId = payload[..separatorIndex].ToString();
        string userId;

        try
        {
            userId = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUserId));
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            _logger.LogWarning(
                ex,
                "SECURITY_AUDIT: Unable to decode user identifier from revocation key - TokenIdHash={TokenIdHash}",
                HashTokenIdForLogging(tokenId));
            return true;
        }

        var issuedAtPart = payload[(separatorIndex + 1)..].ToString();
        if (!long.TryParse(issuedAtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var issuedAtUnixSeconds))
        {
            _logger.LogWarning(
                "SECURITY_AUDIT: Invalid issued-at component in user revocation check key - UserIdHash={UserIdHash}",
                HashTokenIdForLogging(userId));
            return true;
        }

        var metadata = await _cache.GetStringAsync(GetUserRevocationKey(userId), cancellationToken).ConfigureAwait(false);
        if (metadata.IsNullOrEmpty())
        {
            _logger.LogDebug(
                "User revocation marker not found - UserIdHash={UserIdHash}",
                HashTokenIdForLogging(userId));
            return false;
        }

        var separator = metadata.IndexOf('|');
        var timestampPart = separator >= 0 ? metadata[..separator] : metadata;
        if (!DateTimeOffset.TryParse(
                timestampPart,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var revokedAt))
        {
            _logger.LogWarning(
                "SECURITY_AUDIT: Failed to parse user revocation timestamp - UserIdHash={UserIdHash}",
                HashTokenIdForLogging(userId));
            return true;
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixSeconds);
        var revoked = issuedAt <= revokedAt;

        _logger.LogDebug(
            "User revocation check - UserIdHash={UserIdHash}, IssuedAt={IssuedAt:o}, RevokedAt={RevokedAt:o}, Revoked={Revoked}",
            HashTokenIdForLogging(userId),
            issuedAt,
            revokedAt,
            revoked);

        return revoked;
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string tokenId, DateTimeOffset expiresAt, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await PerformanceMeasurement.MeasureAsync(
            "RevokeToken",
            async () =>
            {
                await ActivityScope.ExecuteAsync(
                    ActivitySource,
                    "RevokeToken",
                    [("token.id_hash", HashTokenIdForLogging(tokenId)), ("reason", reason)],
                    async activity =>
                    {
                        var key = GetRevocationKey(tokenId);
                        var metadataKey = GetRevocationMetadataKey(tokenId);
                        var ttl = expiresAt - DateTimeOffset.UtcNow;

                        // Don't revoke already-expired tokens
                        if (ttl <= TimeSpan.Zero)
                        {
                            _logger.LogWarning(
                                "SECURITY_AUDIT: Attempted to revoke expired token - TokenIdHash={TokenIdHash}, Reason={Reason}",
                                HashTokenIdForLogging(tokenId), reason);
                            return;
                        }

                        var options = new CacheOptionsBuilder()
                            .WithAbsoluteExpiration(expiresAt)
                            .BuildDistributed();

                        // Store revocation marker
                        await _cache.SetStringAsync(key, "revoked", options, cancellationToken).ConfigureAwait(false);

                        // Store metadata for audit trail (but don't include full token)
                        var metadata = $"{DateTimeOffset.UtcNow:O}|{reason}";
                        await _cache.SetStringAsync(metadataKey, metadata, options, cancellationToken).ConfigureAwait(false);

                        _revocationsCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));

                        _logger.LogWarning(
                            "SECURITY_AUDIT: Token revoked - TokenIdHash={TokenIdHash}, ExpiresAt={ExpiresAt:u}, Reason={Reason}",
                            HashTokenIdForLogging(tokenId), expiresAt, reason);
                    }).ConfigureAwait(false);
            },
            duration => _operationDuration.Record(duration.TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", "revoke_token"))
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// SECURITY: This implementation uses fail-closed behavior by default when Redis errors occur.
    ///
    /// Normal Operation (Redis Available):
    /// - For individual token checks: Queries Redis for "revoked_token:{tokenId}"
    /// - For user revocation checks: Compares token issue time against user revocation timestamp
    /// - Returns true if revoked, false if not found
    ///
    /// Error Handling (Redis Unavailable):
    /// When FailClosedOnRedisError = true (RECOMMENDED):
    /// - Catches all Redis exceptions (connection failures, timeouts, etc.)
    /// - Returns true (treats all tokens as revoked)
    /// - Logs error with SECURITY_AUDIT tag
    /// - Denies authentication to prevent accepting revoked tokens
    /// - Impact: Service remains secure but temporarily unavailable for auth
    ///
    /// When FailClosedOnRedisError = false (NOT RECOMMENDED):
    /// - Catches all Redis exceptions
    /// - Returns false (treats all tokens as valid)
    /// - Logs warning with SECURITY_AUDIT tag
    /// - Allows authentication to proceed
    /// - RISK: Revoked tokens (from logout, password reset, etc.) will be accepted
    /// - Only use in dev/test where uptime > security
    ///
    /// Implementation Details:
    /// - Token checks are wrapped in try/catch to handle Redis failures
    /// - Configuration value FailClosedOnRedisError controls behavior
    /// - All failures are logged with context for debugging
    /// - Performance metrics are recorded via OpenTelemetry
    ///
    /// Operational Guidance:
    /// - Production: Set FailClosedOnRedisError=true + Redis HA (cluster/sentinel)
    /// - Monitor: Set up alerts on Redis health check failures
    /// - Incident Response: Have runbook for Redis outage scenarios
    /// - Testing: Verify fail-closed behavior with chaos engineering
    /// </remarks>
    public async Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var scope = tokenId.StartsWith(UserRevocationCheckPrefix, StringComparison.Ordinal) ? "user" : "token";

        async Task<bool> ExecuteCheckAsync()
        {
            try
            {
                return await ActivityScope.ExecuteAsync(
                    ActivitySource,
                    "CheckTokenRevocation",
                    [("token.id_hash", HashTokenIdForLogging(tokenId))],
                    async activity =>
                    {
                        bool isRevoked;

                        if (scope == "user")
                        {
                            isRevoked = await CheckUserRevocationAsync(tokenId, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            var key = GetRevocationKey(tokenId);
                            var value = await _cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
                            isRevoked = !value.IsNullOrEmpty();
                        }

                        _revocationChecksCounter.Add(1,
                            new KeyValuePair<string, object?>("revoked", isRevoked),
                            new KeyValuePair<string, object?>("scope", scope));

                        activity.AddTag("revoked", isRevoked);
                        activity.AddTag("scope", scope);

                        return isRevoked;
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var failClosed = _options.Value.FailClosedOnRedisError;

                _logger.LogError(ex,
                    "Failed to check token revocation status - TokenIdHash={TokenIdHash}, FailClosed={FailClosed}",
                    HashTokenIdForLogging(tokenId), failClosed);

                if (failClosed)
                {
                    _logger.LogWarning(
                        "SECURITY_AUDIT: Denying authentication due to revocation check failure (fail-closed mode) - TokenOrUserHash={TokenIdHash}",
                        HashTokenIdForLogging(tokenId));
                    return true;
                }

                _logger.LogWarning(
                    "SECURITY_AUDIT: Allowing authentication despite revocation check failure (fail-open mode) - TokenOrUserHash={TokenIdHash}",
                    HashTokenIdForLogging(tokenId));
                return false;
            }
        }

        return await PerformanceMeasurement.MeasureAsync(
            "CheckTokenRevocation",
            ExecuteCheckAsync,
            (duration, _) => _operationDuration.Record(duration.TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", "check_revocation"))
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeAllUserTokensAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await PerformanceMeasurement.MeasureAsync(
            "RevokeAllUserTokens",
            async () =>
            {
                await ActivityScope.ExecuteAsync(
                    ActivitySource,
                    "RevokeAllUserTokens",
                    [("user.id_hash", HashTokenIdForLogging(userId)), ("reason", reason)],
                    async activity =>
                    {
                        var key = GetUserRevocationKey(userId);
                        var revocationTime = DateTimeOffset.UtcNow;

                        // Store a marker with a long TTL (1 year) to track user-level revocations
                        // Individual token checks will compare their issue time against this
                        var options = new CacheOptionsBuilder()
                            .WithAbsoluteExpiration(revocationTime.AddYears(1))
                            .BuildDistributed();

                        var metadata = $"{revocationTime:O}|{reason}";
                        await _cache.SetStringAsync(key, metadata, options, cancellationToken).ConfigureAwait(false);

                        _revocationsCounter.Add(1,
                            new KeyValuePair<string, object?>("reason", reason),
                            new KeyValuePair<string, object?>("scope", "user"));

                        _logger.LogWarning(
                            "SECURITY_AUDIT: All user tokens revoked - UserIdHash={UserIdHash}, Reason={Reason}",
                            HashTokenIdForLogging(userId), reason);
                    }).ConfigureAwait(false);
            },
            duration => _operationDuration.Record(duration.TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", "revoke_user_tokens"))
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        return PerformanceMeasurement.MeasureAsync(
            "CleanupExpiredRevocations",
            async () =>
            {
                return await ActivityScope.ExecuteAsync(
                    ActivitySource,
                    "CleanupExpiredRevocations",
                    async activity =>
                    {
                        // Redis automatically removes expired keys via TTL
                        // This method is primarily for compatibility and metrics reporting
                        // In a production system, you might scan and count expired keys for metrics

                        _logger.LogInformation("Token revocation cleanup completed (Redis handles TTL automatically)");

                        // Return 0 as Redis handles cleanup automatically
                        var cleanedCount = 0;

                        _cleanupCounter.Add(cleanedCount);
                        activity.AddTag("cleaned_count", cleanedCount);

                        return await Task.FromResult(cleanedCount).ConfigureAwait(false);
                    });
            },
            (duration, _) => _operationDuration.Record(duration.TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", "cleanup"))
        );
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test Redis connectivity by performing a simple operation
            var testKey = "revoked_token:health_check";
            var testValue = DateTimeOffset.UtcNow.ToString("O");

            var options = new CacheOptionsBuilder()
                .WithAbsoluteExpiration(TimeSpan.FromSeconds(10))
                .BuildDistributed();

            await _cache.SetStringAsync(testKey, testValue, options, cancellationToken).ConfigureAwait(false);

            var retrievedValue = await _cache.GetStringAsync(testKey, cancellationToken).ConfigureAwait(false);

            if (retrievedValue == testValue)
            {
                await _cache.RemoveAsync(testKey, cancellationToken).ConfigureAwait(false);
                return HealthCheckResult.Healthy("Token revocation service is operational");
            }

            return HealthCheckResult.Degraded("Token revocation service connectivity issue");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token revocation service health check failed - Redis unavailable, using fallback");
            return HealthCheckResult.Degraded("Token revocation service is using in-memory fallback (Redis unavailable)", ex);
        }
    }

    private static string GetRevocationKey(string tokenId) => $"{RevocationKeyPrefix}{tokenId}";
    private static string GetRevocationMetadataKey(string tokenId) => $"{RevocationMetadataKeyPrefix}{tokenId}";
    private static string GetUserRevocationKey(string userId) => $"{UserRevocationKeyPrefix}{userId}";

    /// <summary>
    /// Hashes token ID for safe logging (prevents token leakage in logs).
    /// Only logs first 8 chars of SHA256 hash for correlation.
    /// </summary>
    private static string HashTokenIdForLogging(string tokenId)
    {
        if (tokenId.IsNullOrEmpty())
            return "null";

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenId));
        var hashHex = Convert.ToHexString(hashBytes);
        return hashHex[..Math.Min(8, hashHex.Length)]; // First 8 chars only
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
