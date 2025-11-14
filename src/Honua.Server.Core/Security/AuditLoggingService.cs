// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Security;

/// <summary>
/// Represents an audit event category.
/// </summary>
public enum AuditEventCategory
{
    /// <summary>Authentication events (login, logout, password changes)</summary>
    Authentication,
    /// <summary>Authorization events (access granted/denied)</summary>
    Authorization,
    /// <summary>Administrative actions</summary>
    Administration,
    /// <summary>Data access and modifications</summary>
    DataAccess,
    /// <summary>Configuration changes</summary>
    Configuration,
    /// <summary>Security events (violations, suspicious activity)</summary>
    Security,
    /// <summary>User management events</summary>
    UserManagement
}

/// <summary>
/// Represents the result of an audited action.
/// </summary>
public enum AuditEventResult
{
    /// <summary>Action completed successfully</summary>
    Success,
    /// <summary>Action failed</summary>
    Failure,
    /// <summary>Action was denied due to authorization</summary>
    Denied
}

/// <summary>
/// Represents an audit log entry with comprehensive tracking information.
/// </summary>
public sealed record AuditLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    AuditEventCategory Category,
    string Action,
    AuditEventResult Result,
    string? UserId,
    string? Username,
    string? TenantId,
    string? IpAddress,
    string? UserAgent,
    string? ResourceType,
    string? ResourceId,
    string? Details,
    Dictionary<string, object>? AdditionalData = null);

/// <summary>
/// Service for comprehensive audit logging with SOC 2, ISO 27001, and GDPR compliance.
/// Provides structured logging of security-relevant events for compliance and forensics.
/// </summary>
public interface IAuditLoggingService
{
    /// <summary>
    /// Logs an audit event with full context information.
    /// </summary>
    Task LogEventAsync(
        AuditEventCategory category,
        string action,
        AuditEventResult result,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a successful administrative action.
    /// </summary>
    Task LogAdminActionAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a failed administrative action.
    /// </summary>
    Task LogAdminActionFailureAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an authorization denial event.
    /// </summary>
    Task LogAuthorizationDeniedAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a security violation event.
    /// </summary>
    Task LogSecurityViolationAsync(
        string violationType,
        string details,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a data access event.
    /// </summary>
    Task LogDataAccessAsync(
        string resourceType,
        string resourceId,
        string operation,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of IAuditLoggingService using structured logging.
/// </summary>
public sealed class AuditLoggingService : IAuditLoggingService
{
    private readonly IUserIdentityService _userIdentityService;
    private readonly ILogger<AuditLoggingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditLoggingService(
        IUserIdentityService userIdentityService,
        ILogger<AuditLoggingService> logger)
    {
        _userIdentityService = userIdentityService ?? throw new ArgumentNullException(nameof(userIdentityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task LogEventAsync(
        AuditEventCategory category,
        string action,
        AuditEventResult result,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        var userIdentity = _userIdentityService.GetCurrentUserIdentity();

        var entry = new AuditLogEntry(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            Category: category,
            Action: action,
            Result: result,
            UserId: userIdentity?.UserId,
            Username: userIdentity?.Username ?? userIdentity?.Email,
            TenantId: userIdentity?.TenantId,
            IpAddress: null, // Can be extended to capture from HttpContext
            UserAgent: null, // Can be extended to capture from HttpContext
            ResourceType: resourceType,
            ResourceId: resourceId,
            Details: details,
            AdditionalData: additionalData);

        LogAuditEntry(entry);
        return Task.CompletedTask;
    }

    public Task LogAdminActionAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            AuditEventCategory.Administration,
            action,
            AuditEventResult.Success,
            resourceType,
            resourceId,
            details,
            additionalData,
            cancellationToken);
    }

    public Task LogAdminActionFailureAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? details = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var additionalData = new Dictionary<string, object>();
        if (exception != null)
        {
            additionalData["exceptionType"] = exception.GetType().Name;
            additionalData["exceptionMessage"] = exception.Message;
            additionalData["stackTrace"] = exception.StackTrace ?? string.Empty;
        }

        var fullDetails = details;
        if (exception != null && !string.IsNullOrEmpty(details))
        {
            fullDetails = $"{details}: {exception.Message}";
        }
        else if (exception != null)
        {
            fullDetails = exception.Message;
        }

        return LogEventAsync(
            AuditEventCategory.Administration,
            action,
            AuditEventResult.Failure,
            resourceType,
            resourceId,
            fullDetails,
            additionalData,
            cancellationToken);
    }

    public Task LogAuthorizationDeniedAsync(
        string action,
        string? resourceType = null,
        string? resourceId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var additionalData = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(reason))
        {
            additionalData["denialReason"] = reason;
        }

        return LogEventAsync(
            AuditEventCategory.Authorization,
            action,
            AuditEventResult.Denied,
            resourceType,
            resourceId,
            $"Authorization denied: {reason ?? "Access forbidden"}",
            additionalData,
            cancellationToken);
    }

    public Task LogSecurityViolationAsync(
        string violationType,
        string details,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            AuditEventCategory.Security,
            $"SecurityViolation.{violationType}",
            AuditEventResult.Denied,
            "SecurityEvent",
            null,
            details,
            additionalData,
            cancellationToken);
    }

    public Task LogDataAccessAsync(
        string resourceType,
        string resourceId,
        string operation,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            AuditEventCategory.DataAccess,
            operation,
            AuditEventResult.Success,
            resourceType,
            resourceId,
            null,
            additionalData,
            cancellationToken);
    }

    private void LogAuditEntry(AuditLogEntry entry)
    {
        // Use structured logging with all fields
        // This ensures that log aggregation systems (Splunk, ELK, etc.) can properly index and query
        var logLevel = entry.Result == AuditEventResult.Denied || entry.Category == AuditEventCategory.Security
            ? LogLevel.Warning
            : LogLevel.Information;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["AuditId"] = entry.Id,
            ["AuditTimestamp"] = entry.Timestamp,
            ["AuditCategory"] = entry.Category.ToString(),
            ["AuditAction"] = entry.Action,
            ["AuditResult"] = entry.Result.ToString(),
            ["UserId"] = entry.UserId,
            ["Username"] = entry.Username,
            ["TenantId"] = entry.TenantId,
            ["ResourceType"] = entry.ResourceType,
            ["ResourceId"] = entry.ResourceId
        });

        var message = FormatAuditMessage(entry);
        _logger.Log(logLevel, message);

        // Also log as structured JSON for easier parsing
        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        _logger.Log(logLevel, "[AUDIT] {AuditJson}", json);
    }

    private static string FormatAuditMessage(AuditLogEntry entry)
    {
        var parts = new List<string>
        {
            $"[AUDIT {entry.Category}]",
            $"Action: {entry.Action}",
            $"Result: {entry.Result}",
            $"User: {entry.Username ?? entry.UserId ?? "anonymous"}"
        };

        if (!string.IsNullOrEmpty(entry.TenantId))
        {
            parts.Add($"Tenant: {entry.TenantId}");
        }

        if (!string.IsNullOrEmpty(entry.ResourceType) && !string.IsNullOrEmpty(entry.ResourceId))
        {
            parts.Add($"Resource: {entry.ResourceType}/{entry.ResourceId}");
        }
        else if (!string.IsNullOrEmpty(entry.ResourceType))
        {
            parts.Add($"Resource: {entry.ResourceType}");
        }

        if (!string.IsNullOrEmpty(entry.Details))
        {
            parts.Add($"Details: {entry.Details}");
        }

        return string.Join(" | ", parts);
    }
}
