// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.AuditLog;

/// <summary>
/// Service for recording and querying audit log events
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records an audit event (write-only, tamper-proof)
    /// </summary>
    /// <param name="event">The audit event to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordAsync(AuditEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records multiple audit events in a batch
    /// </summary>
    /// <param name="events">The audit events to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordBatchAsync(IEnumerable<AuditEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries audit events with filtering and pagination
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of audit events</returns>
    Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific audit event by ID with tenant isolation
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="tenantId">Tenant ID for isolation (required)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit event if found and belongs to tenant</returns>
    Task<AuditEvent?> GetByIdAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit log statistics for a time period
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter</param>
    /// <param name="startTime">Start of time period</param>
    /// <param name="endTime">End of time period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit log statistics</returns>
    Task<AuditLogStatistics> GetStatisticsAsync(
        Guid? tenantId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit events to CSV format
    /// </summary>
    /// <param name="query">Query parameters for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV content as string</returns>
    Task<string> ExportToCsvAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit events to JSON format
    /// </summary>
    /// <param name="query">Query parameters for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON content as string</returns>
    Task<string> ExportToJsonAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives old audit events (for compliance retention)
    /// </summary>
    /// <param name="olderThan">Archive events older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of events archived</returns>
    Task<long> ArchiveEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges archived audit events (for data retention compliance)
    /// </summary>
    /// <param name="olderThan">Purge events older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of events purged</returns>
    Task<long> PurgeArchivedEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builder for creating audit events fluently
/// </summary>
public class AuditEventBuilder
{
    private readonly AuditEvent _event = new();

    public static AuditEventBuilder Create() => new();

    public AuditEventBuilder WithTenant(Guid tenantId)
    {
        _event.TenantId = tenantId;
        return this;
    }

    public AuditEventBuilder WithCategory(string category)
    {
        _event.Category = category;
        return this;
    }

    public AuditEventBuilder WithAction(string action)
    {
        _event.Action = action;
        return this;
    }

    public AuditEventBuilder WithUser(Guid userId, string userIdentifier)
    {
        _event.UserId = userId;
        _event.UserIdentifier = userIdentifier;
        return this;
    }

    public AuditEventBuilder WithResource(string resourceType, string resourceId)
    {
        _event.ResourceType = resourceType;
        _event.ResourceId = resourceId;
        return this;
    }

    public AuditEventBuilder WithDescription(string description)
    {
        _event.Description = description;
        return this;
    }

    public AuditEventBuilder WithSuccess(bool success)
    {
        _event.Success = success;
        return this;
    }

    public AuditEventBuilder WithError(string errorMessage)
    {
        _event.Success = false;
        _event.ErrorMessage = errorMessage;
        return this;
    }

    public AuditEventBuilder WithHttpContext(string method, string path, int statusCode, long durationMs)
    {
        _event.HttpMethod = method;
        _event.RequestPath = path;
        _event.StatusCode = statusCode;
        _event.DurationMs = durationMs;
        return this;
    }

    public AuditEventBuilder WithIpAddress(string ipAddress)
    {
        _event.IpAddress = ipAddress;
        return this;
    }

    public AuditEventBuilder WithUserAgent(string userAgent)
    {
        _event.UserAgent = userAgent;
        return this;
    }

    public AuditEventBuilder WithSessionId(string sessionId)
    {
        _event.SessionId = sessionId;
        return this;
    }

    public AuditEventBuilder WithTraceId(string traceId)
    {
        _event.TraceId = traceId;
        return this;
    }

    public AuditEventBuilder WithMetadata(string key, object value)
    {
        _event.Metadata ??= new Dictionary<string, object>();
        _event.Metadata[key] = value;
        return this;
    }

    public AuditEventBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        _event.Metadata = metadata;
        return this;
    }

    public AuditEventBuilder WithChanges(Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        _event.Changes = new AuditChanges
        {
            Before = before,
            After = after,
            ChangedFields = new List<string>()
        };

        // Determine changed fields
        foreach (var key in before.Keys)
        {
            if (!after.ContainsKey(key) || !Equals(before[key], after[key]))
            {
                _event.Changes.ChangedFields.Add(key);
            }
        }

        foreach (var key in after.Keys)
        {
            if (!before.ContainsKey(key) && !_event.Changes.ChangedFields.Contains(key))
            {
                _event.Changes.ChangedFields.Add(key);
            }
        }

        return this;
    }

    public AuditEventBuilder WithLocation(string location)
    {
        _event.Location = location;
        return this;
    }

    public AuditEventBuilder WithRiskScore(int riskScore)
    {
        _event.RiskScore = riskScore;
        return this;
    }

    public AuditEventBuilder WithTags(params string[] tags)
    {
        _event.Tags = new List<string>(tags);
        return this;
    }

    public AuditEvent Build() => _event;
}
