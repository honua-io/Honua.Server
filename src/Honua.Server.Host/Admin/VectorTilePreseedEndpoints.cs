// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.VectorTiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Extension methods for mapping vector tile preseed administration endpoints.
/// </summary>
internal static class VectorTilePreseedEndpoints
{
    /// <summary>
    /// Maps all vector tile preseed administration endpoints for job management.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Enqueuing new preseed jobs to pre-generate vector tiles
    /// - Listing all active and completed preseed jobs
    /// - Retrieving details of specific jobs including progress
    /// - Cancelling running preseed jobs
    /// </remarks>
    /// <example>
    /// Example request to enqueue a preseed job:
    /// <code>
    /// POST /admin/vector-cache/jobs
    /// {
    ///   "collectionIds": ["parcels", "roads"],
    ///   "minZoom": 0,
    ///   "maxZoom": 14,
    ///   "format": "mvt"
    /// }
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapVectorTilePreseed(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/vector-cache/jobs")
            .RequireAuthorization("RequireAdministrator")
            .WithTags("Vector Tile Preseed");

        group.MapPost("", EnqueueJob)
            .Produces<VectorTilePreseedJobSnapshot>(StatusCodes.Status202Accepted)
            .WithName("EnqueueVectorTilePreseedJob")
            .WithDescription("Enqueue a vector tile preseed job");

        group.MapGet("", ListJobs)
            .Produces<VectorTilePreseedJobSnapshot[]>()
            .WithName("ListVectorTilePreseedJobs")
            .WithDescription("List all vector tile preseed jobs");

        group.MapGet("/{jobId:guid}", GetJob)
            .Produces<VectorTilePreseedJobSnapshot>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetVectorTilePreseedJob")
            .WithDescription("Get a specific vector tile preseed job");

        group.MapDelete("/{jobId:guid}", CancelJob)
            .Produces<VectorTilePreseedJobSnapshot>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("CancelVectorTilePreseedJob")
            .WithDescription("Cancel a running vector tile preseed job");

        return app;
    }

    private static async Task<IResult> EnqueueJob(
        VectorTilePreseedRequest request,
        [FromServices] IVectorTilePreseedService preseedService,
        [FromServices] ILogger<IVectorTilePreseedService> logger,
        [FromServices] ISecurityAuditLogger auditLogger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await preseedService.EnqueueAsync(request, cancellationToken);

            // Audit log successful job enqueue
            auditLogger.LogAdminOperation(
                operation: "enqueue_vector_preseed_job",
                username: httpContext.User.Identity?.Name ?? "anonymous",
                resourceType: "vector_tile_preseed",
                resourceId: snapshot.JobId.ToString(),
                ipAddress: httpContext.Connection.RemoteIpAddress?.ToString());

            return Results.Accepted($"/admin/vector-cache/jobs/{snapshot.JobId}", snapshot);
        }
        catch (InvalidOperationException ex)
        {
            // Log the detailed exception internally
            logger.LogWarning(ex,
                "Failed to enqueue vector tile preseed job - ServiceId={ServiceId}, LayerId={LayerId}, MinZoom={MinZoom}, MaxZoom={MaxZoom}",
                request.ServiceId, request.LayerId, request.MinZoom, request.MaxZoom);

            // Return sanitized error - only safe validation messages
            var sanitizedError = SanitizeValidationError(ex.Message);
            return ApiErrorResponse.Json.BadRequestResult(sanitizedError);
        }
        catch (ArgumentException ex)
        {
            // Log validation errors
            logger.LogWarning(ex, "Invalid vector tile preseed request");

            return ApiErrorResponse.Json.BadRequestResult("Invalid request parameters. Please check your input and try again.");
        }
        catch (Exception ex)
        {
            // Log unexpected errors with full details
            logger.LogError(ex, "Unexpected error enqueueing vector tile preseed job");

            // Return generic error to client
            return Results.Problem(
                detail: "An error occurred while enqueueing the preseed job",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ListJobs(
        [FromServices] IVectorTilePreseedService preseedService,
        CancellationToken cancellationToken)
    {
        var jobs = await preseedService.ListJobsAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetJob(
        Guid jobId,
        [FromServices] IVectorTilePreseedService preseedService,
        CancellationToken cancellationToken)
    {
        var snapshot = await preseedService.TryGetJobAsync(jobId, cancellationToken).ConfigureAwait(false);

        return snapshot is not null
            ? Results.Ok(snapshot)
            : GeoservicesRESTErrorHelper.NotFoundWithMessage("Job not found");
    }

    private static async Task<IResult> CancelJob(
        Guid jobId,
        [FromServices] IVectorTilePreseedService preseedService,
        [FromServices] ILogger<IVectorTilePreseedService> logger,
        [FromServices] ISecurityAuditLogger auditLogger,
        HttpContext httpContext)
    {
        try
        {
            var snapshot = await preseedService.CancelAsync(jobId, "User requested cancellation");
            if (snapshot == null)
            {
                return GeoservicesRESTErrorHelper.NotFoundWithMessage("Job not found");
            }

            // Audit log the cancellation
            auditLogger.LogAdminOperation(
                operation: "cancel_vector_preseed_job",
                username: httpContext.User.Identity?.Name ?? "anonymous",
                resourceType: "vector_tile_preseed",
                resourceId: jobId.ToString(),
                ipAddress: httpContext.Connection.RemoteIpAddress?.ToString());

            logger.LogInformation("Vector tile preseed job {JobId} cancelled by user", jobId);

            return Results.Ok(snapshot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling vector tile preseed job {JobId}", jobId);
            return Results.Problem(
                detail: "An error occurred while cancelling the job",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Sanitizes validation error messages to prevent information leakage.
    /// Returns safe user-facing messages while filtering out internal details.
    /// </summary>
    /// <remarks>
    /// This method uses a two-stage sanitization approach:
    /// <list type="number">
    /// <item><description>First checks if the message matches known safe patterns (whitelist)</description></item>
    /// <item><description>Then applies comprehensive sanitization to remove sensitive data patterns</description></item>
    /// </list>
    /// Removes potentially sensitive information including:
    /// <list type="bullet">
    /// <item><description>File paths (C:\, /home/, /var/)</description></item>
    /// <item><description>Connection strings (Server=, Database=, Password=)</description></item>
    /// <item><description>SQL statements (SELECT, INSERT, UPDATE, DELETE)</description></item>
    /// <item><description>Stack trace fragments (lines starting with "at ")</description></item>
    /// </list>
    /// </remarks>
    private static string SanitizeValidationError(string errorMessage)
    {
        if (errorMessage.IsNullOrWhiteSpace())
        {
            return "The request could not be processed. Please check your parameters and try again.";
        }

        // List of safe error message patterns that can be returned to users
        var safePatterns = new[]
        {
            "Maximum zoom level",
            "Maximum concurrent jobs",
            "Maximum jobs per service/layer",
            "Rate limit exceeded",
            "Requested tile count exceeds maximum"
        };

        // Check if the error message starts with any safe pattern
        bool isSafePattern = false;
        foreach (var pattern in safePatterns)
        {
            if (errorMessage.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                isSafePattern = true;
                break;
            }
        }

        // If not a safe pattern, return generic message immediately
        if (!isSafePattern)
        {
            return "The request could not be processed. Please check your parameters and try again.";
        }

        // Even for safe patterns, apply comprehensive sanitization to remove any sensitive data
        var sanitized = errorMessage;

        // Remove file paths
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[A-Za-z]:\\[^\s]+|/(?:home|var|usr|opt)/[^\s]+",
            "[PATH_REDACTED]");

        // Remove potential connection strings
        if (sanitized.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase))
        {
            return "The request could not be processed. Please check your parameters and try again.";
        }

        // Remove potential SQL
        if (sanitized.Contains("SELECT ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("INSERT ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("DELETE ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("DROP ", StringComparison.OrdinalIgnoreCase) ||
            sanitized.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
        {
            return "The request could not be processed. Please check your parameters and try again.";
        }

        // Remove stack trace fragments (lines starting with "at ")
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"\s+at\s+[^\n]+",
            "");

        // If sanitization changed the message significantly, return generic message
        if (sanitized.IsNullOrWhiteSpace() || sanitized.Contains("[PATH_REDACTED]"))
        {
            return "The request could not be processed. Please check your parameters and try again.";
        }

        return sanitized.Trim();
    }

    /// <summary>
    /// Alias for MapVectorTilePreseed for backward compatibility.
    /// </summary>
    public static IEndpointRouteBuilder MapVectorTilePreseedEndpoints(this IEndpointRouteBuilder app) =>
        MapVectorTilePreseed(app);
}
