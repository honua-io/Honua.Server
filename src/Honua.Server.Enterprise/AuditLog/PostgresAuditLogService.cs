// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.AuditLog;

/// <summary>
/// PostgreSQL implementation of audit log service with tamper-proof storage
/// </summary>
public class PostgresAuditLogService : IAuditLogService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresAuditLogService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Whitelist of allowed sort fields to prevent SQL injection in ORDER BY clauses
    /// </summary>
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "timestamp",
        "category",
        "action",
        "user_id",
        "user_identifier",
        "success",
        "resource_type",
        "resource_id",
        "ip_address",
        "status_code",
        "duration_ms",
        "risk_score",
        "created_at"
    };

    public PostgresAuditLogService(
        string connectionString,
        ILogger<PostgresAuditLogService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordAsync(AuditEvent @event, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO audit_events (
                id,
                tenant_id,
                timestamp,
                category,
                action,
                user_id,
                user_identifier,
                success,
                resource_type,
                resource_id,
                description,
                ip_address,
                user_agent,
                http_method,
                request_path,
                status_code,
                duration_ms,
                error_message,
                metadata,
                changes,
                session_id,
                trace_id,
                location,
                risk_score,
                tags
            ) VALUES (
                @Id,
                @TenantId,
                @Timestamp,
                @Category,
                @Action,
                @UserId,
                @UserIdentifier,
                @Success,
                @ResourceType,
                @ResourceId,
                @Description,
                @IpAddress,
                @UserAgent,
                @HttpMethod,
                @RequestPath,
                @StatusCode,
                @DurationMs,
                @ErrorMessage,
                @Metadata::jsonb,
                @Changes::jsonb,
                @SessionId,
                @TraceId,
                @Location,
                @RiskScore,
                @Tags
            )";

        await connection.ExecuteAsync(sql, new
        {
            @event.Id,
            @event.TenantId,
            @event.Timestamp,
            @event.Category,
            @event.Action,
            @event.UserId,
            @event.UserIdentifier,
            @event.Success,
            @event.ResourceType,
            @event.ResourceId,
            @event.Description,
            @event.IpAddress,
            @event.UserAgent,
            @event.HttpMethod,
            @event.RequestPath,
            @event.StatusCode,
            @event.DurationMs,
            @event.ErrorMessage,
            Metadata = @event.Metadata != null ? JsonSerializer.Serialize(@event.Metadata, _jsonOptions) : null,
            Changes = @event.Changes != null ? JsonSerializer.Serialize(@event.Changes, _jsonOptions) : null,
            @event.SessionId,
            @event.TraceId,
            @event.Location,
            @event.RiskScore,
            Tags = @event.Tags?.ToArray()
        });

        _logger.LogDebug(
            "Recorded audit event: {Category}.{Action} by {User} (Success: {Success})",
            @event.Category, @event.Action, @event.UserIdentifier ?? "system", @event.Success);
    }

    public async Task RecordBatchAsync(IEnumerable<AuditEvent> events, CancellationToken cancellationToken = default)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO audit_events (
                id, tenant_id, timestamp, category, action, user_id, user_identifier,
                success, resource_type, resource_id, description, ip_address, user_agent,
                http_method, request_path, status_code, duration_ms, error_message,
                metadata, changes, session_id, trace_id, location, risk_score, tags
            ) VALUES (
                @Id, @TenantId, @Timestamp, @Category, @Action, @UserId, @UserIdentifier,
                @Success, @ResourceType, @ResourceId, @Description, @IpAddress, @UserAgent,
                @HttpMethod, @RequestPath, @StatusCode, @DurationMs, @ErrorMessage,
                @Metadata::jsonb, @Changes::jsonb, @SessionId, @TraceId, @Location, @RiskScore, @Tags
            )";

        await connection.ExecuteAsync(sql, eventList.Select(e => new
        {
            e.Id,
            e.TenantId,
            e.Timestamp,
            e.Category,
            e.Action,
            e.UserId,
            e.UserIdentifier,
            e.Success,
            e.ResourceType,
            e.ResourceId,
            e.Description,
            e.IpAddress,
            e.UserAgent,
            e.HttpMethod,
            e.RequestPath,
            e.StatusCode,
            e.DurationMs,
            e.ErrorMessage,
            Metadata = e.Metadata != null ? JsonSerializer.Serialize(e.Metadata, _jsonOptions) : null,
            Changes = e.Changes != null ? JsonSerializer.Serialize(e.Changes, _jsonOptions) : null,
            e.SessionId,
            e.TraceId,
            e.Location,
            e.RiskScore,
            Tags = e.Tags?.ToArray()
        }));

        _logger.LogInformation("Recorded {Count} audit events in batch", eventList.Count);
    }

    public async Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Build WHERE clause
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // SECURITY: TenantId is now REQUIRED for tenant isolation
        if (!query.TenantId.HasValue)
        {
            _logger.LogError("QueryAsync called without TenantId - this is a security violation");
            throw new ArgumentException("TenantId is required for audit log queries", nameof(query));
        }

        conditions.Add("tenant_id = @TenantId");
        parameters.Add("TenantId", query.TenantId.Value);

        if (query.UserId.HasValue)
        {
            conditions.Add("user_id = @UserId");
            parameters.Add("UserId", query.UserId.Value);
        }

        if (!string.IsNullOrEmpty(query.UserIdentifier))
        {
            conditions.Add("user_identifier = @UserIdentifier");
            parameters.Add("UserIdentifier", query.UserIdentifier);
        }

        if (!string.IsNullOrEmpty(query.Category))
        {
            conditions.Add("category = @Category");
            parameters.Add("Category", query.Category);
        }

        if (!string.IsNullOrEmpty(query.Action))
        {
            conditions.Add("action = @Action");
            parameters.Add("Action", query.Action);
        }

        if (!string.IsNullOrEmpty(query.ResourceType))
        {
            conditions.Add("resource_type = @ResourceType");
            parameters.Add("ResourceType", query.ResourceType);
        }

        if (!string.IsNullOrEmpty(query.ResourceId))
        {
            conditions.Add("resource_id = @ResourceId");
            parameters.Add("ResourceId", query.ResourceId);
        }

        if (query.Success.HasValue)
        {
            conditions.Add("success = @Success");
            parameters.Add("Success", query.Success.Value);
        }

        if (!string.IsNullOrEmpty(query.IpAddress))
        {
            conditions.Add("ip_address = @IpAddress");
            parameters.Add("IpAddress", query.IpAddress);
        }

        if (query.StartTime.HasValue)
        {
            conditions.Add("timestamp >= @StartTime");
            parameters.Add("StartTime", query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            conditions.Add("timestamp <= @EndTime");
            parameters.Add("EndTime", query.EndTime.Value);
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            conditions.Add("(description ILIKE @SearchText OR metadata::text ILIKE @SearchText)");
            parameters.Add("SearchText", $"%{query.SearchText}%");
        }

        if (query.Tags != null && query.Tags.Count > 0)
        {
            conditions.Add("tags && @Tags");
            parameters.Add("Tags", query.Tags.ToArray());
        }

        if (query.MinRiskScore.HasValue)
        {
            conditions.Add("risk_score >= @MinRiskScore");
            parameters.Add("MinRiskScore", query.MinRiskScore.Value);
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        // Get total count
        var countSql = $"SELECT COUNT(*) FROM audit_events {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<long>(countSql, parameters);

        // Get paged results
        var offset = (query.Page - 1) * query.PageSize;
        var orderBy = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // Validate and sanitize sort field to prevent SQL injection
        var sortField = ValidateSortField(query.SortBy);

        var dataSql = $@"
            SELECT * FROM audit_events
            {whereClause}
            ORDER BY {sortField} {orderBy}
            LIMIT @PageSize OFFSET @Offset";

        parameters.Add("PageSize", Math.Min(query.PageSize, 1000));
        parameters.Add("Offset", offset);

        var rows = await connection.QueryAsync<dynamic>(dataSql, parameters);
        var events = rows.Select(MapToAuditEvent).ToList();

        return new AuditLogResult
        {
            Events = events,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<AuditEvent?> GetByIdAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // SECURITY: Add tenant filter to enforce tenant isolation
        const string sql = @"
            SELECT * FROM audit_events
            WHERE id = @EventId AND tenant_id = @TenantId";

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new
        {
            EventId = eventId,
            TenantId = tenantId
        });

        if (row != null)
        {
            _logger.LogDebug("Retrieved audit event {EventId} for tenant {TenantId}", eventId, tenantId);
        }
        else
        {
            _logger.LogWarning("Audit event {EventId} not found or does not belong to tenant {TenantId}", eventId, tenantId);
        }

        return row != null ? MapToAuditEvent(row) : null;
    }

    public async Task<AuditLogStatistics> GetStatisticsAsync(
        Guid? tenantId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var whereClause = tenantId.HasValue
            ? "WHERE tenant_id = @TenantId AND timestamp >= @StartTime AND timestamp <= @EndTime"
            : "WHERE timestamp >= @StartTime AND timestamp <= @EndTime";

        var parameters = new DynamicParameters();
        if (tenantId.HasValue) parameters.Add("TenantId", tenantId.Value);
        parameters.Add("StartTime", startTime);
        parameters.Add("EndTime", endTime);

        // Total events
        var totalSql = $"SELECT COUNT(*) FROM audit_events {whereClause}";
        var totalEvents = await connection.ExecuteScalarAsync<long>(totalSql, parameters);

        // Successful/failed events
        var successSql = $"SELECT COUNT(*) FROM audit_events {whereClause} AND success = true";
        var successfulEvents = await connection.ExecuteScalarAsync<long>(successSql, parameters);
        var failedEvents = totalEvents - successfulEvents;

        // Unique users
        var usersSql = $"SELECT COUNT(DISTINCT user_id) FROM audit_events {whereClause} AND user_id IS NOT NULL";
        var uniqueUsers = await connection.ExecuteScalarAsync<int>(usersSql, parameters);

        // Events by category
        var categorySql = $@"
            SELECT category, COUNT(*) as count
            FROM audit_events {whereClause}
            GROUP BY category";
        var categoryRows = await connection.QueryAsync<(string category, long count)>(categorySql, parameters);
        var eventsByCategory = categoryRows.ToDictionary(r => r.category, r => r.count);

        // Events by action
        var actionSql = $@"
            SELECT action, COUNT(*) as count
            FROM audit_events {whereClause}
            GROUP BY action
            ORDER BY count DESC
            LIMIT 20";
        var actionRows = await connection.QueryAsync<(string action, long count)>(actionSql, parameters);
        var eventsByAction = actionRows.ToDictionary(r => r.action, r => r.count);

        // High-risk events
        var riskSql = $"SELECT COUNT(*) FROM audit_events {whereClause} AND risk_score >= 80";
        var highRiskEvents = await connection.ExecuteScalarAsync<long>(riskSql, parameters);

        return new AuditLogStatistics
        {
            TotalEvents = totalEvents,
            SuccessfulEvents = successfulEvents,
            FailedEvents = failedEvents,
            UniqueUsers = uniqueUsers,
            EventsByCategory = eventsByCategory,
            EventsByAction = eventsByAction,
            HighRiskEvents = highRiskEvents
        };
    }

    public async Task<string> ExportToCsvAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        // Query all matching events (up to a reasonable limit)
        query.PageSize = 10000;
        var result = await QueryAsync(query, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Category,Action,User,Success,ResourceType,ResourceId,IpAddress,Description");

        foreach (var evt in result.Events)
        {
            csv.AppendLine($"{evt.Timestamp:yyyy-MM-dd HH:mm:ss},{CsvEscape(evt.Category)},{CsvEscape(evt.Action)}," +
                          $"{CsvEscape(evt.UserIdentifier)},{evt.Success},{CsvEscape(evt.ResourceType)}," +
                          $"{CsvEscape(evt.ResourceId)},{CsvEscape(evt.IpAddress)},{CsvEscape(evt.Description)}");
        }

        return csv.ToString();
    }

    public async Task<string> ExportToJsonAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        query.PageSize = 10000;
        var result = await QueryAsync(query, cancellationToken);

        return JsonSerializer.Serialize(result.Events, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public async Task<long> ArchiveEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO audit_events_archive
            SELECT * FROM audit_events
            WHERE timestamp < @OlderThan AND archived_at IS NULL";

        var archived = await connection.ExecuteAsync(sql, new { OlderThan = olderThan });

        if (archived > 0)
        {
            const string updateSql = @"
                UPDATE audit_events
                SET archived_at = @Now
                WHERE timestamp < @OlderThan AND archived_at IS NULL";

            await connection.ExecuteAsync(updateSql, new { OlderThan = olderThan, Now = DateTimeOffset.UtcNow });

            _logger.LogInformation("Archived {Count} audit events older than {Date}", archived, olderThan);
        }

        return archived;
    }

    public async Task<long> PurgeArchivedEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            DELETE FROM audit_events
            WHERE archived_at IS NOT NULL AND archived_at < @OlderThan";

        var purged = await connection.ExecuteAsync(sql, new { OlderThan = olderThan });

        if (purged > 0)
        {
            _logger.LogWarning("Purged {Count} archived audit events older than {Date}", purged, olderThan);
        }

        return purged;
    }

    private static AuditEvent MapToAuditEvent(dynamic row)
    {
        return new AuditEvent
        {
            Id = row.id,
            TenantId = row.tenant_id,
            Timestamp = row.timestamp,
            Category = row.category,
            Action = row.action,
            UserId = row.user_id,
            UserIdentifier = row.user_identifier,
            Success = row.success,
            ResourceType = row.resource_type,
            ResourceId = row.resource_id,
            Description = row.description,
            IpAddress = row.ip_address,
            UserAgent = row.user_agent,
            HttpMethod = row.http_method,
            RequestPath = row.request_path,
            StatusCode = row.status_code,
            DurationMs = row.duration_ms,
            ErrorMessage = row.error_message,
            Metadata = row.metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata)
                : null,
            Changes = row.changes != null
                ? JsonSerializer.Deserialize<AuditChanges>(row.changes)
                : null,
            SessionId = row.session_id,
            TraceId = row.trace_id,
            Location = row.location,
            RiskScore = row.risk_score,
            Tags = row.tags != null ? new List<string>(row.tags) : null
        };
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    /// <summary>
    /// Validates sort field against whitelist to prevent SQL injection
    /// </summary>
    /// <param name="sortBy">The sort field requested by the user</param>
    /// <returns>A validated sort field safe for use in SQL</returns>
    /// <exception cref="ArgumentException">Thrown when sort field is not in whitelist</exception>
    private string ValidateSortField(string sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            // Default to timestamp if not specified
            return "timestamp";
        }

        if (!AllowedSortFields.Contains(sortBy))
        {
            _logger.LogWarning(
                "Invalid sort field attempted: {SortBy}. This may be a SQL injection attempt.",
                sortBy);

            throw new ArgumentException(
                $"Invalid sort field '{sortBy}'. Allowed fields: {string.Join(", ", AllowedSortFields)}",
                nameof(sortBy));
        }

        return sortBy;
    }
}
