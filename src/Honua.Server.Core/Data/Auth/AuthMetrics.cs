// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Data.Auth;

/// <summary>
/// Metrics for authentication and authorization operations.
/// </summary>
public sealed class AuthMetrics
{
    private readonly Counter<long> _loginSuccessCounter;
    private readonly Counter<long> _loginFailureCounter;
    private readonly Counter<long> _accountLockedCounter;
    private readonly Counter<long> _passwordChangedCounter;
    private readonly Counter<long> _rolesChangedCounter;
    private readonly Counter<long> _userCreatedCounter;
    private readonly Counter<long> _passwordExpiredCounter;
    private readonly Counter<long> _passwordExpiresSoonCounter;
    private readonly Histogram<double> _failedAttemptsHistogram;
    private readonly Histogram<double> _daysUntilPasswordExpirationHistogram;

    public AuthMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Auth");

        _loginSuccessCounter = meter.CreateCounter<long>(
            "auth.login.success",
            description: "Number of successful login attempts");

        _loginFailureCounter = meter.CreateCounter<long>(
            "auth.login.failure",
            description: "Number of failed login attempts");

        _accountLockedCounter = meter.CreateCounter<long>(
            "auth.account.locked",
            description: "Number of account lockouts");

        _passwordChangedCounter = meter.CreateCounter<long>(
            "auth.password.changed",
            description: "Number of password changes");

        _rolesChangedCounter = meter.CreateCounter<long>(
            "auth.roles.changed",
            description: "Number of role assignment changes");

        _userCreatedCounter = meter.CreateCounter<long>(
            "auth.user.created",
            description: "Number of users created");

        _failedAttemptsHistogram = meter.CreateHistogram<double>(
            "auth.failed_attempts.count",
            description: "Distribution of failed login attempts before lockout");

        _passwordExpiredCounter = meter.CreateCounter<long>(
            "auth.password.expired",
            description: "Number of authentication attempts with expired passwords");

        _passwordExpiresSoonCounter = meter.CreateCounter<long>(
            "auth.password.expires_soon",
            description: "Number of successful authentications with password expiring soon warnings");

        _daysUntilPasswordExpirationHistogram = meter.CreateHistogram<double>(
            "auth.password.days_until_expiration",
            description: "Distribution of days until password expiration for users with warnings");
    }

    public void RecordLoginSuccess(string? ipAddress = null)
    {
        _loginSuccessCounter.Add(1, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
    }

    public void RecordLoginFailure(int attemptNumber, string? ipAddress = null)
    {
        _loginFailureCounter.Add(1, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
        _failedAttemptsHistogram.Record(attemptNumber, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
    }

    public void RecordAccountLocked(int failedAttempts, string? ipAddress = null)
    {
        _accountLockedCounter.Add(1, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
        _failedAttemptsHistogram.Record(failedAttempts, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
    }

    public void RecordPasswordChanged(string? actorId = null)
    {
        _passwordChangedCounter.Add(1, new[] { new KeyValuePair<string, object?>("actor_id", actorId ?? "self") });
    }

    public void RecordRolesChanged(string? actorId = null)
    {
        _rolesChangedCounter.Add(1, new[] { new KeyValuePair<string, object?>("actor_id", actorId ?? "system") });
    }

    public void RecordUserCreated(string? actorId = null)
    {
        _userCreatedCounter.Add(1, new[] { new KeyValuePair<string, object?>("actor_id", actorId ?? "system") });
    }

    public void RecordPasswordExpired(string? ipAddress = null)
    {
        _passwordExpiredCounter.Add(1, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
    }

    public void RecordPasswordExpiresSoon(int daysUntilExpiration, string? ipAddress = null)
    {
        _passwordExpiresSoonCounter.Add(1, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
        _daysUntilPasswordExpirationHistogram.Record(daysUntilExpiration, new[] { new KeyValuePair<string, object?>("ip_address", ipAddress ?? "unknown") });
    }
}
