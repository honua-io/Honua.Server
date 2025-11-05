// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Represents an audit log event.
/// </summary>
public sealed class AuditEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public Guid? UserId { get; set; }

    [JsonPropertyName("userIdentifier")]
    public string? UserIdentifier { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("httpMethod")]
    public string? HttpMethod { get; set; }

    [JsonPropertyName("requestPath")]
    public string? RequestPath { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("changes")]
    public AuditChanges? Changes { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("riskScore")]
    public int? RiskScore { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Represents changes made during an update operation.
/// </summary>
public sealed class AuditChanges
{
    [JsonPropertyName("before")]
    public Dictionary<string, object?>? Before { get; set; }

    [JsonPropertyName("after")]
    public Dictionary<string, object?>? After { get; set; }

    [JsonPropertyName("changedFields")]
    public List<string>? ChangedFields { get; set; }
}

/// <summary>
/// Query parameters for searching audit logs.
/// </summary>
public sealed class AuditLogQuery
{
    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("userId")]
    public Guid? UserId { get; set; }

    [JsonPropertyName("userIdentifier")]
    public string? UserIdentifier { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("success")]
    public bool? Success { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("searchText")]
    public string? SearchText { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("minRiskScore")]
    public int? MinRiskScore { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 100;

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "timestamp";

    [JsonPropertyName("sortDirection")]
    public string SortDirection { get; set; } = "desc";
}

/// <summary>
/// Paged result of audit events.
/// </summary>
public sealed class AuditLogResult
{
    [JsonPropertyName("events")]
    public List<AuditEvent> Events { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Audit log statistics.
/// </summary>
public sealed class AuditLogStatistics
{
    [JsonPropertyName("totalEvents")]
    public long TotalEvents { get; set; }

    [JsonPropertyName("successfulEvents")]
    public long SuccessfulEvents { get; set; }

    [JsonPropertyName("failedEvents")]
    public long FailedEvents { get; set; }

    [JsonPropertyName("uniqueUsers")]
    public int UniqueUsers { get; set; }

    [JsonPropertyName("eventsByCategory")]
    public Dictionary<string, long> EventsByCategory { get; set; } = new();

    [JsonPropertyName("eventsByAction")]
    public Dictionary<string, long> EventsByAction { get; set; } = new();

    [JsonPropertyName("highRiskEvents")]
    public long HighRiskEvents { get; set; }

    [JsonPropertyName("eventsOverTime")]
    public Dictionary<string, long> EventsOverTime { get; set; } = new();
}

/// <summary>
/// Predefined filter options for audit log viewer.
/// </summary>
public static class AuditFilterOptions
{
    public static readonly List<string> Categories = new()
    {
        "authentication",
        "authorization",
        "data.access",
        "data.modification",
        "admin.action",
        "system.event",
        "security.event",
        "api.request",
        "configuration",
        "compliance"
    };

    public static readonly List<string> Actions = new()
    {
        "login",
        "logout",
        "create",
        "read",
        "update",
        "delete",
        "export",
        "import",
        "access.granted",
        "access.denied"
    };

    public static readonly List<string> ResourceTypes = new()
    {
        "service",
        "layer",
        "folder",
        "user",
        "tenant",
        "snapshot",
        "import_job"
    };
}
