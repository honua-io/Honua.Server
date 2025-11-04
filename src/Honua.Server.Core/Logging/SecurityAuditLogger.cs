// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Logging;

/// <summary>
/// Centralized security audit logging service for tracking security-relevant events.
/// </summary>
public interface ISecurityAuditLogger
{
    void LogLoginSuccess(string username, string? ipAddress, string? userAgent);
    void LogLoginFailure(string username, string? ipAddress, string? userAgent, string reason);
    void LogAccountLockout(string username, string? ipAddress, DateTimeOffset lockedUntil);
    void LogAdminOperation(string operation, string username, string? resourceType, string? resourceId, string? ipAddress);
    void LogDataAccess(string username, string operation, string resourceType, string? resourceId, string? ipAddress);
    void LogConfigurationChange(string username, string configKey, string? oldValue, string? newValue, string? ipAddress);
    void LogUnauthorizedAccess(string? username, string resource, string? ipAddress, string reason);
    void LogSuspiciousActivity(string activityType, string? username, string? ipAddress, string details);
    void LogApiKeyValidationFailure(string partialKey, string? ipAddress);
    void LogApiKeyAuthentication(string keyName, string? ipAddress);
    void LogPasswordExpired(string username, DateTimeOffset expiresAt, string? ipAddress);
    void LogPasswordExpiresSoon(string username, DateTimeOffset expiresAt, int daysUntilExpiration, string? ipAddress);
    void LogPasswordChanged(string username, string? ipAddress, string? actorUsername);
}

public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;

    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogLoginSuccess(string username, string? ipAddress, string? userAgent)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: Login successful - Username={Username}, IP={IPAddress}, UserAgent={UserAgent}",
            username, ipAddress ?? "unknown", userAgent ?? "unknown");
    }

    public void LogLoginFailure(string username, string? ipAddress, string? userAgent, string reason)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Login failed - Username={Username}, IP={IPAddress}, UserAgent={UserAgent}, Reason={Reason}",
            username, ipAddress ?? "unknown", userAgent ?? "unknown", reason);
    }

    public void LogAccountLockout(string username, string? ipAddress, DateTimeOffset lockedUntil)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Account locked - Username={Username}, IP={IPAddress}, LockedUntil={LockedUntil:u}",
            username, ipAddress ?? "unknown", lockedUntil);
    }

    public void LogAdminOperation(string operation, string username, string? resourceType, string? resourceId, string? ipAddress)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: Admin operation - Operation={Operation}, Username={Username}, ResourceType={ResourceType}, ResourceId={ResourceId}, IP={IPAddress}",
            operation, username, resourceType ?? "N/A", resourceId ?? "N/A", ipAddress ?? "unknown");
    }

    public void LogDataAccess(string username, string operation, string resourceType, string? resourceId, string? ipAddress)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: Data access - Username={Username}, Operation={Operation}, ResourceType={ResourceType}, ResourceId={ResourceId}, IP={IPAddress}",
            username, operation, resourceType, resourceId ?? "N/A", ipAddress ?? "unknown");
    }

    public void LogConfigurationChange(string username, string configKey, string? oldValue, string? newValue, string? ipAddress)
    {
        var safeOldValue = SensitiveDataRedactor.Redact(oldValue);
        var safeNewValue = SensitiveDataRedactor.Redact(newValue);

        _logger.LogWarning(
            "SECURITY_AUDIT: Configuration changed - Username={Username}, ConfigKey={ConfigKey}, OldValue={OldValue}, NewValue={NewValue}, IP={IPAddress}",
            username, configKey, safeOldValue ?? "null", safeNewValue ?? "null", ipAddress ?? "unknown");
    }

    public void LogUnauthorizedAccess(string? username, string resource, string? ipAddress, string reason)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Unauthorized access attempt - Username={Username}, Resource={Resource}, IP={IPAddress}, Reason={Reason}",
            username ?? "anonymous", resource, ipAddress ?? "unknown", reason);
    }

    public void LogSuspiciousActivity(string activityType, string? username, string? ipAddress, string details)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Suspicious activity detected - ActivityType={ActivityType}, Username={Username}, IP={IPAddress}, Details={Details}",
            activityType, username ?? "anonymous", ipAddress ?? "unknown", details);
    }

    public void LogApiKeyValidationFailure(string partialKey, string? ipAddress)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Invalid API key attempted - PartialKey={PartialKey}, IP={IPAddress}",
            partialKey, ipAddress ?? "unknown");
    }

    public void LogApiKeyAuthentication(string keyName, string? ipAddress)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: API key authentication successful - KeyName={KeyName}, IP={IPAddress}",
            keyName, ipAddress ?? "unknown");
    }

    public void LogPasswordExpired(string username, DateTimeOffset expiresAt, string? ipAddress)
    {
        _logger.LogWarning(
            "SECURITY_AUDIT: Password expired - Username={Username}, ExpiredOn={ExpiredOn:u}, IP={IPAddress}",
            username, expiresAt, ipAddress ?? "unknown");
    }

    public void LogPasswordExpiresSoon(string username, DateTimeOffset expiresAt, int daysUntilExpiration, string? ipAddress)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: Password expires soon - Username={Username}, ExpiresOn={ExpiresOn:u}, DaysRemaining={DaysRemaining}, IP={IPAddress}",
            username, expiresAt, daysUntilExpiration, ipAddress ?? "unknown");
    }

    public void LogPasswordChanged(string username, string? ipAddress, string? actorUsername)
    {
        _logger.LogInformation(
            "SECURITY_AUDIT: Password changed - Username={Username}, Actor={Actor}, IP={IPAddress}",
            username, actorUsername ?? "self", ipAddress ?? "unknown");
    }
}
