// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for authentication and authorization operations.
/// Tracks login attempts, token validations, permission checks, and security events.
/// </summary>
public interface ISecurityMetrics
{
    void RecordLoginAttempt(string authMethod, bool success, string? username = null);
    void RecordLoginFailure(string authMethod, string failureReason, string? username = null);
    void RecordTokenValidation(string tokenType, bool success, TimeSpan? tokenAge = null);
    void RecordTokenRefresh(string tokenType, bool success);
    void RecordAuthorizationCheck(string resource, string permission, bool granted);
    void RecordAuthorizationDenied(string resource, string permission, string? reason = null);
    void RecordSessionCreated(TimeSpan? sessionDuration = null);
    void RecordSessionTerminated(string terminationReason, TimeSpan sessionDuration);
    void RecordSecurityEvent(string eventType, string severity);
    void RecordApiKeyUsage(string apiKeyId, string? endpoint = null);
}

/// <summary>
/// Implementation of security metrics using OpenTelemetry.
/// </summary>
public sealed class SecurityMetrics : ISecurityMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _loginAttempts;
    private readonly Counter<long> _loginFailures;
    private readonly Counter<long> _tokenValidations;
    private readonly Counter<long> _tokenRefreshes;
    private readonly Counter<long> _authorizationChecks;
    private readonly Counter<long> _authorizationDenials;
    private readonly Counter<long> _sessionsCreated;
    private readonly Counter<long> _sessionsTerminated;
    private readonly Counter<long> _securityEvents;
    private readonly Counter<long> _apiKeyUsage;
    private readonly Histogram<double> _sessionDuration;
    private readonly Histogram<double> _tokenAge;

    public SecurityMetrics()
    {
        _meter = new Meter("Honua.Server.Security", "1.0.0");

        _loginAttempts = _meter.CreateCounter<long>(
            "honua.security.login_attempts",
            unit: "{attempt}",
            description: "Number of login attempts by authentication method");

        _loginFailures = _meter.CreateCounter<long>(
            "honua.security.login_failures",
            unit: "{failure}",
            description: "Number of failed login attempts by reason");

        _tokenValidations = _meter.CreateCounter<long>(
            "honua.security.token_validations",
            unit: "{validation}",
            description: "Number of token validation attempts");

        _tokenRefreshes = _meter.CreateCounter<long>(
            "honua.security.token_refreshes",
            unit: "{refresh}",
            description: "Number of token refresh operations");

        _authorizationChecks = _meter.CreateCounter<long>(
            "honua.security.authorization_checks",
            unit: "{check}",
            description: "Number of authorization checks performed");

        _authorizationDenials = _meter.CreateCounter<long>(
            "honua.security.authorization_denials",
            unit: "{denial}",
            description: "Number of authorization denials by resource and reason");

        _sessionsCreated = _meter.CreateCounter<long>(
            "honua.security.sessions_created",
            unit: "{session}",
            description: "Number of user sessions created");

        _sessionsTerminated = _meter.CreateCounter<long>(
            "honua.security.sessions_terminated",
            unit: "{session}",
            description: "Number of user sessions terminated");

        _securityEvents = _meter.CreateCounter<long>(
            "honua.security.events",
            unit: "{event}",
            description: "Number of security events by type and severity");

        _apiKeyUsage = _meter.CreateCounter<long>(
            "honua.security.api_key_usage",
            unit: "{request}",
            description: "Number of API requests authenticated via API key");

        _sessionDuration = _meter.CreateHistogram<double>(
            "honua.security.session_duration",
            unit: "ms",
            description: "User session duration");

        _tokenAge = _meter.CreateHistogram<double>(
            "honua.security.token_age",
            unit: "ms",
            description: "Age of validated tokens");
    }

    public void RecordLoginAttempt(string authMethod, bool success, string? username = null)
    {
        _loginAttempts.Add(1,
            new("auth.method", NormalizeAuthMethod(authMethod)),
            new("success", success.ToString()),
            new("username.provided", (username != null).ToString()));

        if (!success)
        {
            RecordLoginFailure(authMethod, "authentication_failed", username);
        }
    }

    public void RecordLoginFailure(string authMethod, string failureReason, string? username = null)
    {
        _loginFailures.Add(1,
            new("auth.method", NormalizeAuthMethod(authMethod)),
            new("failure.reason", NormalizeFailureReason(failureReason)),
            new("username.provided", (username != null).ToString()));

        // Record as a security event
        RecordSecurityEvent("login_failure", "warning");
    }

    public void RecordTokenValidation(string tokenType, bool success, TimeSpan? tokenAge = null)
    {
        _tokenValidations.Add(1,
            new("token.type", NormalizeTokenType(tokenType)),
            new("success", success.ToString()));

        if (tokenAge.HasValue)
        {
            _tokenAge.Record(tokenAge.Value.TotalMilliseconds,
                new("token.type", NormalizeTokenType(tokenType)),
                new("validation.result", success ? "success" : "failure"));
        }
    }

    public void RecordTokenRefresh(string tokenType, bool success)
    {
        _tokenRefreshes.Add(1,
            new("token.type", NormalizeTokenType(tokenType)),
            new("success", success.ToString()));

        if (!success)
        {
            RecordSecurityEvent("token_refresh_failure", "warning");
        }
    }

    public void RecordAuthorizationCheck(string resource, string permission, bool granted)
    {
        _authorizationChecks.Add(1,
            new("resource.type", NormalizeResource(resource)),
            new("permission", NormalizePermission(permission)),
            new("granted", granted.ToString()));

        if (!granted)
        {
            RecordAuthorizationDenied(resource, permission);
        }
    }

    public void RecordAuthorizationDenied(string resource, string permission, string? reason = null)
    {
        _authorizationDenials.Add(1,
            new("resource.type", NormalizeResource(resource)),
            new("permission", NormalizePermission(permission)),
            new("denial.reason", Normalize(reason)));

        // Record as a security event
        RecordSecurityEvent("authorization_denied", "info");
    }

    public void RecordSessionCreated(TimeSpan? sessionDuration = null)
    {
        _sessionsCreated.Add(1);
    }

    public void RecordSessionTerminated(string terminationReason, TimeSpan sessionDuration)
    {
        _sessionsTerminated.Add(1,
            new KeyValuePair<string, object?>[] { new("termination.reason", NormalizeTerminationReason(terminationReason)) });

        _sessionDuration.Record(sessionDuration.TotalMilliseconds,
            new("termination.reason", NormalizeTerminationReason(terminationReason)),
            new("duration.bucket", GetSessionDurationBucket(sessionDuration)));
    }

    public void RecordSecurityEvent(string eventType, string severity)
    {
        _securityEvents.Add(1,
            new("event.type", NormalizeEventType(eventType)),
            new("severity", NormalizeSeverity(severity)));
    }

    public void RecordApiKeyUsage(string apiKeyId, string? endpoint = null)
    {
        _apiKeyUsage.Add(1,
            new("api_key.id", MaskApiKeyId(apiKeyId)),
            new("endpoint.category", NormalizeEndpoint(endpoint)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => value.IsNullOrWhiteSpace() ? "unknown" : value;

    private static string NormalizeAuthMethod(string? method)
    {
        if (method.IsNullOrWhiteSpace())
            return "unknown";

        return method.ToLowerInvariant() switch
        {
            var m when m.Contains("oauth") => "oauth",
            var m when m.Contains("oidc") || m.Contains("openid") => "oidc",
            var m when m.Contains("basic") => "basic",
            var m when m.Contains("bearer") => "bearer",
            var m when m.Contains("api") && m.Contains("key") => "api_key",
            var m when m.Contains("certificate") || m.Contains("mtls") => "certificate",
            var m when m.Contains("anonymous") => "anonymous",
            _ => method.ToLowerInvariant()
        };
    }

    private static string NormalizeTokenType(string? tokenType)
    {
        if (tokenType.IsNullOrWhiteSpace())
            return "unknown";

        return tokenType.ToLowerInvariant() switch
        {
            "jwt" or "access_token" => "jwt",
            "refresh_token" => "refresh",
            "id_token" => "id_token",
            "api_key" => "api_key",
            _ => tokenType.ToLowerInvariant()
        };
    }

    private static string NormalizeFailureReason(string? reason)
    {
        if (reason.IsNullOrWhiteSpace())
            return "unknown";

        return reason.ToLowerInvariant() switch
        {
            var r when r.Contains("credential") => "invalid_credentials",
            var r when r.Contains("password") => "invalid_password",
            var r when r.Contains("username") || r.Contains("user") => "invalid_username",
            var r when r.Contains("token") => "invalid_token",
            var r when r.Contains("expired") => "expired",
            var r when r.Contains("locked") => "account_locked",
            var r when r.Contains("disabled") => "account_disabled",
            var r when r.Contains("mfa") || r.Contains("2fa") => "mfa_failure",
            _ => reason.ToLowerInvariant()
        };
    }

    private static string NormalizeResource(string? resource)
    {
        if (resource.IsNullOrWhiteSpace())
            return "unknown";

        return resource.ToLowerInvariant() switch
        {
            var r when r.Contains("layer") => "layer",
            var r when r.Contains("service") => "service",
            var r when r.Contains("dataset") => "dataset",
            var r when r.Contains("tile") => "tile",
            var r when r.Contains("feature") => "feature",
            var r when r.Contains("metadata") => "metadata",
            var r when r.Contains("admin") => "admin",
            _ => resource.ToLowerInvariant()
        };
    }

    private static string NormalizePermission(string? permission)
    {
        if (permission.IsNullOrWhiteSpace())
            return "unknown";

        return permission.ToLowerInvariant() switch
        {
            "read" or "view" or "get" => "read",
            "write" or "create" or "post" => "write",
            "update" or "put" or "patch" => "update",
            "delete" or "remove" => "delete",
            "admin" or "manage" => "admin",
            _ => permission.ToLowerInvariant()
        };
    }

    private static string NormalizeTerminationReason(string? reason)
    {
        if (reason.IsNullOrWhiteSpace())
            return "unknown";

        return reason.ToLowerInvariant() switch
        {
            var r when r.Contains("logout") => "logout",
            var r when r.Contains("timeout") || r.Contains("expir") => "timeout",
            var r when r.Contains("revoked") => "revoked",
            var r when r.Contains("error") => "error",
            _ => reason.ToLowerInvariant()
        };
    }

    private static string NormalizeEventType(string? eventType)
    {
        if (eventType.IsNullOrWhiteSpace())
            return "unknown";

        return eventType.ToLowerInvariant();
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (severity.IsNullOrWhiteSpace())
            return "info";

        return severity.ToLowerInvariant() switch
        {
            "critical" or "crit" => "critical",
            "error" or "err" => "error",
            "warning" or "warn" => "warning",
            "info" or "information" => "info",
            "debug" => "debug",
            _ => "info"
        };
    }

    private static string NormalizeEndpoint(string? endpoint)
    {
        if (endpoint.IsNullOrWhiteSpace())
            return "unknown";

        return endpoint.ToLowerInvariant() switch
        {
            var e when e.Contains("/wfs") => "wfs",
            var e when e.Contains("/wms") => "wms",
            var e when e.Contains("/ogc") => "ogc",
            var e when e.Contains("/stac") => "stac",
            var e when e.Contains("/admin") => "admin",
            var e when e.Contains("/tile") => "tiles",
            _ => "other"
        };
    }

    private static string MaskApiKeyId(string? apiKeyId)
    {
        if (apiKeyId.IsNullOrWhiteSpace())
            return "unknown";

        // Show only first 8 characters for tracking purposes
        return apiKeyId.Length > 8 ? apiKeyId.Substring(0, 8) + "..." : apiKeyId;
    }

    private static string GetSessionDurationBucket(TimeSpan duration)
    {
        var minutes = duration.TotalMinutes;
        return minutes switch
        {
            < 5 => "very_short",
            < 30 => "short",
            < 120 => "normal",
            < 480 => "long",
            _ => "very_long"
        };
    }
}
