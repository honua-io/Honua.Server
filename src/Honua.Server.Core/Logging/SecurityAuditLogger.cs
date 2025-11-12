// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Logs an authorization failure for a resource.
    /// </summary>
    /// <param name="resourceType">Type of resource (e.g., "Share", "Dashboard")</param>
    /// <param name="resourceId">Identifier of the resource</param>
    /// <param name="userId">User who attempted the action (null for anonymous)</param>
    /// <param name="attemptedAction">Action that was attempted (e.g., "Update", "Delete")</param>
    /// <param name="reason">Reason for the failure (e.g., "Not owner", "Insufficient permissions")</param>
    /// <param name="remoteIp">Remote IP address of the request</param>
    /// <example>
    /// <code>
    /// // Log an authorization failure
    /// _auditLogger.LogAuthorizationFailure(
    ///     resourceType: "Share",
    ///     resourceId: token,
    ///     userId: User.Identity?.Name,
    ///     attemptedAction: "Update",
    ///     reason: "Not owner",
    ///     remoteIp: HttpContext.Connection.RemoteIpAddress?.ToString()
    /// );
    /// </code>
    /// </example>
    void LogAuthorizationFailure(
        string resourceType,
        string resourceId,
        string? userId,
        string attemptedAction,
        string reason,
        string? remoteIp = null);

    /// <summary>
    /// Logs an ownership validation failure.
    /// </summary>
    /// <param name="resourceType">Type of resource (e.g., "Share", "Dashboard")</param>
    /// <param name="resourceId">Identifier of the resource</param>
    /// <param name="userId">User who attempted the action (null for anonymous)</param>
    /// <param name="ownerId">User who owns the resource</param>
    /// <param name="attemptedAction">Action that was attempted</param>
    /// <param name="remoteIp">Remote IP address of the request</param>
    void LogOwnershipViolation(
        string resourceType,
        string resourceId,
        string? userId,
        string ownerId,
        string attemptedAction,
        string? remoteIp = null);

    /// <summary>
    /// Logs suspicious access patterns (e.g., token enumeration).
    /// </summary>
    /// <param name="activityType">Type of suspicious activity (e.g., "ShareTokenEnumeration")</param>
    /// <param name="userId">User involved in the activity (null for anonymous)</param>
    /// <param name="description">Description of the suspicious activity</param>
    /// <param name="metadata">Additional metadata about the activity</param>
    /// <param name="remoteIp">Remote IP address</param>
    /// <example>
    /// <code>
    /// // Log suspicious activity
    /// _auditLogger.LogSuspiciousActivity(
    ///     activityType: "ShareTokenEnumeration",
    ///     userId: User.Identity?.Name,
    ///     description: "Multiple failed share token lookups in short time",
    ///     metadata: new Dictionary&lt;string, object&gt; {
    ///         { "attemptCount", 10 },
    ///         { "timeWindowSeconds", 5 }
    ///     },
    ///     remoteIp: HttpContext.Connection.RemoteIpAddress?.ToString()
    /// );
    /// </code>
    /// </example>
    void LogSuspiciousActivity(
        string activityType,
        string? userId,
        string description,
        Dictionary<string, object>? metadata = null,
        string? remoteIp = null);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogInformation(
            "SECURITY_AUDIT: Login successful - Username={Username}, IP={IPAddress}, UserAgent={UserAgent}",
            username, ipAddress ?? "unknown", userAgent ?? "unknown");
    }

    public void LogLoginFailure(string username, string? ipAddress, string? userAgent, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _logger.LogWarning(
            "SECURITY_AUDIT: Login failed - Username={Username}, IP={IPAddress}, UserAgent={UserAgent}, Reason={Reason}",
            username, ipAddress ?? "unknown", userAgent ?? "unknown", reason);
    }

    public void LogAccountLockout(string username, string? ipAddress, DateTimeOffset lockedUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogWarning(
            "SECURITY_AUDIT: Account locked - Username={Username}, IP={IPAddress}, LockedUntil={LockedUntil:u}",
            username, ipAddress ?? "unknown", lockedUntil);
    }

    public void LogAdminOperation(string operation, string username, string? resourceType, string? resourceId, string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogInformation(
            "SECURITY_AUDIT: Admin operation - Operation={Operation}, Username={Username}, ResourceType={ResourceType}, ResourceId={ResourceId}, IP={IPAddress}",
            operation, username, resourceType ?? "N/A", resourceId ?? "N/A", ipAddress ?? "unknown");
    }

    public void LogDataAccess(string username, string operation, string resourceType, string? resourceId, string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);

        _logger.LogInformation(
            "SECURITY_AUDIT: Data access - Username={Username}, Operation={Operation}, ResourceType={ResourceType}, ResourceId={ResourceId}, IP={IPAddress}",
            username, operation, resourceType, resourceId ?? "N/A", ipAddress ?? "unknown");
    }

    public void LogConfigurationChange(string username, string configKey, string? oldValue, string? newValue, string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(configKey);

        var safeOldValue = SensitiveDataRedactor.Redact(oldValue);
        var safeNewValue = SensitiveDataRedactor.Redact(newValue);

        _logger.LogWarning(
            "SECURITY_AUDIT: Configuration changed - Username={Username}, ConfigKey={ConfigKey}, OldValue={OldValue}, NewValue={NewValue}, IP={IPAddress}",
            username, configKey, safeOldValue ?? "null", safeNewValue ?? "null", ipAddress ?? "unknown");
    }

    public void LogUnauthorizedAccess(string? username, string resource, string? ipAddress, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogWarning(
            "SECURITY_AUDIT: Password expired - Username={Username}, ExpiredOn={ExpiredOn:u}, IP={IPAddress}",
            username, expiresAt, ipAddress ?? "unknown");
    }

    public void LogPasswordExpiresSoon(string username, DateTimeOffset expiresAt, int daysUntilExpiration, string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogInformation(
            "SECURITY_AUDIT: Password expires soon - Username={Username}, ExpiresOn={ExpiresOn:u}, DaysRemaining={DaysRemaining}, IP={IPAddress}",
            username, expiresAt, daysUntilExpiration, ipAddress ?? "unknown");
    }

    public void LogPasswordChanged(string username, string? ipAddress, string? actorUsername)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        _logger.LogInformation(
            "SECURITY_AUDIT: Password changed - Username={Username}, Actor={Actor}, IP={IPAddress}",
            username, actorUsername ?? "self", ipAddress ?? "unknown");
    }

    public void LogAuthorizationFailure(
        string resourceType,
        string resourceId,
        string? userId,
        string attemptedAction,
        string reason,
        string? remoteIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptedAction);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _logger.LogWarning(
            "SECURITY_AUDIT: Authorization failure - UserId={UserId}, IP={IPAddress}, Action={Action}, ResourceType={ResourceType}, ResourceId={ResourceId}, Reason={Reason}",
            userId ?? "anonymous",
            remoteIp ?? "unknown",
            attemptedAction,
            resourceType,
            resourceId,
            reason);
    }

    public void LogOwnershipViolation(
        string resourceType,
        string resourceId,
        string? userId,
        string ownerId,
        string attemptedAction,
        string? remoteIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptedAction);

        _logger.LogWarning(
            "SECURITY_AUDIT: Ownership violation - UserId={UserId}, IP={IPAddress}, Action={Action}, ResourceType={ResourceType}, ResourceId={ResourceId}, OwnerId={OwnerId}",
            userId ?? "anonymous",
            remoteIp ?? "unknown",
            attemptedAction,
            resourceType,
            resourceId,
            ownerId);
    }

    public void LogSuspiciousActivity(
        string activityType,
        string? userId,
        string description,
        Dictionary<string, object>? metadata = null,
        string? remoteIp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var metadataString = metadata != null && metadata.Count > 0
            ? string.Join(", ", metadata.Select(kv => $"{kv.Key}={kv.Value}"))
            : "none";

        _logger.LogWarning(
            "SECURITY_AUDIT: Suspicious activity detected - ActivityType={ActivityType}, UserId={UserId}, IP={IPAddress}, Description={Description}, Metadata={Metadata}",
            activityType,
            userId ?? "anonymous",
            remoteIp ?? "unknown",
            description,
            metadataString);
    }
}
