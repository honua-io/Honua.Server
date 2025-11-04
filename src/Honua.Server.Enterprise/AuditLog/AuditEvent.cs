// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Honua.Server.Enterprise.AuditLog;

/// <summary>
/// Represents an immutable audit log event
/// </summary>
public class AuditEvent
{
    /// <summary>
    /// Unique identifier for this audit event
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant ID (for multi-tenant isolation)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Timestamp when the event occurred (UTC)
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Event category (e.g., "authentication", "data.access", "admin.action")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific action performed (e.g., "login", "create", "update", "delete")
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// User who performed the action (ID)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// User who performed the action (email/username)
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Resource type affected (e.g., "collection", "user", "tenant")
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Resource ID affected
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Human-readable description of the event
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent (browser/client)
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE)
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Request path/endpoint
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Request duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Error message (if Success = false)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata (JSON object)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Changes made (before/after values for updates)
    /// </summary>
    public AuditChanges? Changes { get; set; }

    /// <summary>
    /// Session ID (for correlating events in same session)
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Trace ID (for distributed tracing correlation)
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Geographic location (country/region)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Risk score (0-100, for anomaly detection)
    /// </summary>
    public int? RiskScore { get; set; }

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Represents changes made during an update operation
/// </summary>
public class AuditChanges
{
    /// <summary>
    /// Previous values (before the change)
    /// </summary>
    public Dictionary<string, object?>? Before { get; set; }

    /// <summary>
    /// New values (after the change)
    /// </summary>
    public Dictionary<string, object?>? After { get; set; }

    /// <summary>
    /// Fields that were changed
    /// </summary>
    public List<string>? ChangedFields { get; set; }
}

/// <summary>
/// Audit event category constants
/// </summary>
public static class AuditCategory
{
    public const string Authentication = "authentication";
    public const string Authorization = "authorization";
    public const string DataAccess = "data.access";
    public const string DataModification = "data.modification";
    public const string AdminAction = "admin.action";
    public const string SystemEvent = "system.event";
    public const string SecurityEvent = "security.event";
    public const string ApiRequest = "api.request";
    public const string Configuration = "configuration";
    public const string Compliance = "compliance";
}

/// <summary>
/// Audit action constants
/// </summary>
public static class AuditAction
{
    // Authentication actions
    public const string Login = "login";
    public const string Logout = "logout";
    public const string LoginFailed = "login.failed";
    public const string PasswordReset = "password.reset";
    public const string MfaEnabled = "mfa.enabled";
    public const string MfaDisabled = "mfa.disabled";
    public const string SamlLogin = "saml.login";

    // Authorization actions
    public const string AccessGranted = "access.granted";
    public const string AccessDenied = "access.denied";
    public const string PermissionChanged = "permission.changed";
    public const string RoleAssigned = "role.assigned";
    public const string RoleRevoked = "role.revoked";

    // Data actions
    public const string Create = "create";
    public const string Read = "read";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Export = "export";
    public const string Import = "import";
    public const string Download = "download";
    public const string Upload = "upload";

    // Admin actions
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantDeleted = "tenant.deleted";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string UserSuspended = "user.suspended";
    public const string UserReactivated = "user.reactivated";

    // System actions
    public const string ConfigurationChanged = "config.changed";
    public const string ServiceStarted = "service.started";
    public const string ServiceStopped = "service.stopped";
    public const string MigrationApplied = "migration.applied";
    public const string BackupCreated = "backup.created";
    public const string BackupRestored = "backup.restored";

    // Security actions
    public const string SecurityPolicyChanged = "security.policy.changed";
    public const string SuspiciousActivity = "suspicious.activity";
    public const string ApiKeyCreated = "apikey.created";
    public const string ApiKeyRevoked = "apikey.revoked";
    public const string CertificateRenewed = "certificate.renewed";
}

/// <summary>
/// Query parameters for searching audit logs
/// </summary>
public class AuditLogQuery
{
    /// <summary>
    /// Filter by tenant ID
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Filter by user identifier (email/username)
    /// </summary>
    public string? UserIdentifier { get; set; }

    /// <summary>
    /// Filter by category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Filter by action
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filter by resource type
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Filter by resource ID
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Filter by success/failure
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Filter by IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Start timestamp (inclusive)
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// End timestamp (inclusive)
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Search text (searches description and metadata)
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Filter by tags
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Minimum risk score
    /// </summary>
    public int? MinRiskScore { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size (max 1000)
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "timestamp";

    /// <summary>
    /// Sort direction (asc/desc)
    /// </summary>
    public string SortDirection { get; set; } = "desc";
}

/// <summary>
/// Paged result of audit events
/// </summary>
public class AuditLogResult
{
    /// <summary>
    /// Audit events in this page
    /// </summary>
    public List<AuditEvent> Events { get; set; } = new();

    /// <summary>
    /// Total count of matching events
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there are more pages
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Audit log statistics
/// </summary>
public class AuditLogStatistics
{
    /// <summary>
    /// Total events in period
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Successful events
    /// </summary>
    public long SuccessfulEvents { get; set; }

    /// <summary>
    /// Failed events
    /// </summary>
    public long FailedEvents { get; set; }

    /// <summary>
    /// Unique users
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Events by category
    /// </summary>
    public Dictionary<string, long> EventsByCategory { get; set; } = new();

    /// <summary>
    /// Events by action
    /// </summary>
    public Dictionary<string, long> EventsByAction { get; set; } = new();

    /// <summary>
    /// High-risk events (risk score >= 80)
    /// </summary>
    public long HighRiskEvents { get; set; }

    /// <summary>
    /// Events over time (hourly/daily buckets)
    /// </summary>
    public Dictionary<DateTimeOffset, long> EventsOverTime { get; set; } = new();
}
