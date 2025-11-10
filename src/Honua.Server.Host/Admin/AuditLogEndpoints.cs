// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.AuditLog;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin API endpoints for audit log management
/// </summary>
public static class AuditLogEndpoints
{
    /// <summary>
    /// Maps audit log admin endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/audit")
            .RequireAuthorization("RequireAdministrator"); // Requires admin role

        // Query audit events
        group.MapPost("/query", QueryAuditEventsAsync)
            .WithName("QueryAuditEvents")
            .WithOpenApi(op =>
            {
                op.Summary = "Query audit log events";
                op.Description = "Search and filter audit log events with pagination";
                return op;
            });

        // Get specific audit event
        group.MapGet("/{eventId:guid}", GetAuditEventAsync)
            .WithName("GetAuditEvent")
            .WithOpenApi(op =>
            {
                op.Summary = "Get audit event by ID";
                op.Description = "Retrieves a specific audit event";
                return op;
            });

        // Get audit log statistics
        group.MapGet("/statistics", GetStatisticsAsync)
            .WithName("GetAuditStatistics")
            .WithOpenApi(op =>
            {
                op.Summary = "Get audit log statistics";
                op.Description = "Returns statistics for a time period";
                return op;
            });

        // Export to CSV
        group.MapPost("/export/csv", ExportToCsvAsync)
            .WithName("ExportAuditLogCsv")
            .WithOpenApi(op =>
            {
                op.Summary = "Export audit log to CSV";
                op.Description = "Exports filtered audit events to CSV format";
                return op;
            });

        // Export to JSON
        group.MapPost("/export/json", ExportToJsonAsync)
            .WithName("ExportAuditLogJson")
            .WithOpenApi(op =>
            {
                op.Summary = "Export audit log to JSON";
                op.Description = "Exports filtered audit events to JSON format";
                return op;
            });

        // Archive old events (admin action)
        group.MapPost("/archive", ArchiveEventsAsync)
            .WithName("ArchiveAuditEvents")
            .WithOpenApi(op =>
            {
                op.Summary = "Archive old audit events";
                op.Description = "Archives events older than specified date";
                return op;
            });

        return endpoints;
    }

    private static async Task<IResult> QueryAuditEventsAsync(
        HttpContext context,
        [FromBody] AuditLogQuery query,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ITenantProvider? tenantProvider,
        [FromServices] ILogger<IAuditLogService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // If user is not super admin, restrict to their tenant
            if (!context.User.IsInRole("SuperAdmin") && tenantProvider != null)
            {
                var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
                if (tenant != null && Guid.TryParse(tenant.TenantId, out var tenantGuid))
                {
                    query.TenantId = tenantGuid;
                }
            }

            var result = await auditLogService.QueryAsync(query, cancellationToken);

            logger.LogInformation(
                "Admin {User} queried audit log: {Count} events returned",
                context.User.Identity?.Name,
                result.Events.Count);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying audit log");
            return Results.Problem(
                title: "Query Error",
                detail: "Failed to query audit log",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetAuditEventAsync(
        HttpContext context,
        Guid eventId,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ITenantProvider? tenantProvider,
        [FromServices] ILogger<IAuditLogService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // SECURITY: Get tenant ID for isolation
            Guid tenantId;
            if (!context.User.IsInRole("SuperAdmin") && tenantProvider != null)
            {
                var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
                if (tenant == null || !Guid.TryParse(tenant.TenantId, out tenantId))
                {
                    logger.LogWarning("Unable to determine tenant for audit event retrieval");
                    return Results.Forbid();
                }
            }
            else
            {
                // For super admin, require tenant ID in query string or headers
                logger.LogError("SuperAdmin access to audit events requires explicit tenant ID");
                return Results.BadRequest(new { error = "TenantId required for audit event retrieval" });
            }

            // Retrieve with tenant isolation enforced at the data layer
            var auditEvent = await auditLogService.GetByIdAsync(eventId, tenantId, cancellationToken);

            if (auditEvent == null)
            {
                return Results.NotFound(new { error = "Audit event not found" });
            }

            return Results.Ok(auditEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving audit event {EventId}", eventId);
            return Results.Problem(
                title: "Retrieval Error",
                detail: "Failed to retrieve audit event",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetStatisticsAsync(
        HttpContext context,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ITenantProvider? tenantProvider,
        [FromServices] ILogger<IAuditLogService> logger,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? tenantId = null;

            // If not super admin, restrict to their tenant
            if (!context.User.IsInRole("SuperAdmin") && tenantProvider != null)
            {
                var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
                if (tenant != null && Guid.TryParse(tenant.TenantId, out var tenantGuid))
                {
                    tenantId = tenantGuid;
                }
            }

            var start = startTime ?? DateTimeOffset.UtcNow.AddDays(-30);
            var end = endTime ?? DateTimeOffset.UtcNow;

            var statistics = await auditLogService.GetStatisticsAsync(
                tenantId,
                start,
                end,
                cancellationToken);

            logger.LogInformation(
                "Admin {User} retrieved audit statistics for period {Start} to {End}",
                context.User.Identity?.Name,
                start,
                end);

            return Results.Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving audit statistics");
            return Results.Problem(
                title: "Statistics Error",
                detail: "Failed to retrieve audit statistics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ExportToCsvAsync(
        HttpContext context,
        [FromBody] AuditLogQuery query,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ITenantProvider? tenantProvider,
        [FromServices] ILogger<IAuditLogService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Restrict to tenant if not super admin
            if (!context.User.IsInRole("SuperAdmin") && tenantProvider != null)
            {
                var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
                if (tenant != null && Guid.TryParse(tenant.TenantId, out var tenantGuid))
                {
                    query.TenantId = tenantGuid;
                }
            }

            var csv = await auditLogService.ExportToCsvAsync(query, cancellationToken);

            logger.LogInformation(
                "Admin {User} exported audit log to CSV",
                context.User.Identity?.Name);

            // Record admin action
            await auditLogService.RecordAsync(AuditEventBuilder.Create()
                .WithCategory(AuditCategory.AdminAction)
                .WithAction(AuditAction.Export)
                .WithUser(Guid.Parse(context.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                         context.User.Identity?.Name ?? "unknown")
                .WithResource("audit_log", "csv_export")
                .WithDescription("Exported audit log to CSV")
                .Build(), cancellationToken);

            var fileName = $"audit_log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting audit log to CSV");
            return Results.Problem(
                title: "Export Error",
                detail: "Failed to export audit log",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ExportToJsonAsync(
        HttpContext context,
        [FromBody] AuditLogQuery query,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ITenantProvider? tenantProvider,
        [FromServices] ILogger<IAuditLogService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Restrict to tenant if not super admin
            if (!context.User.IsInRole("SuperAdmin") && tenantProvider != null)
            {
                var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
                if (tenant != null && Guid.TryParse(tenant.TenantId, out var tenantGuid))
                {
                    query.TenantId = tenantGuid;
                }
            }

            var json = await auditLogService.ExportToJsonAsync(query, cancellationToken);

            logger.LogInformation(
                "Admin {User} exported audit log to JSON",
                context.User.Identity?.Name);

            // Record admin action
            await auditLogService.RecordAsync(AuditEventBuilder.Create()
                .WithCategory(AuditCategory.AdminAction)
                .WithAction(AuditAction.Export)
                .WithUser(Guid.Parse(context.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                         context.User.Identity?.Name ?? "unknown")
                .WithResource("audit_log", "json_export")
                .WithDescription("Exported audit log to JSON")
                .Build(), cancellationToken);

            var fileName = $"audit_log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting audit log to JSON");
            return Results.Problem(
                title: "Export Error",
                detail: "Failed to export audit log",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ArchiveEventsAsync(
        HttpContext context,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ILogger<IAuditLogService> logger,
        int olderThanDays = 90,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
            var archived = await auditLogService.ArchiveEventsAsync(cutoffDate, cancellationToken);

            logger.LogInformation(
                "Admin {User} archived {Count} audit events older than {Days} days",
                context.User.Identity?.Name,
                archived,
                olderThanDays);

            // Record admin action
            await auditLogService.RecordAsync(AuditEventBuilder.Create()
                .WithCategory(AuditCategory.AdminAction)
                .WithAction("archive")
                .WithUser(Guid.Parse(context.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                         context.User.Identity?.Name ?? "unknown")
                .WithResource("audit_log", "archival")
                .WithDescription($"Archived {archived} events older than {olderThanDays} days")
                .WithMetadata("archived_count", archived)
                .WithMetadata("cutoff_days", olderThanDays)
                .Build(), cancellationToken);

            return Results.Ok(new
            {
                success = true,
                archivedCount = archived,
                cutoffDate,
                message = $"Archived {archived} audit events"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error archiving audit events");
            return Results.Problem(
                title: "Archive Error",
                detail: "Failed to archive audit events",
                statusCode: 500);
        }
    }
}
