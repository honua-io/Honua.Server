// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin REST API endpoints for Site Reliability Engineering (SRE) features including
/// SLO tracking, error budget monitoring, and deployment policy recommendations.
/// </summary>
public static class SreEndpoints
{
    /// <summary>
    /// Maps SRE administration endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAdminSreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/sre")
            .WithTags("Admin - SRE & SLO Management")
            .WithOpenApi()
            .RequireAuthorization(AdminAuthorizationPolicies.RequireServerAdministrator);

        // SLO endpoints
        group.MapGet("/slos", GetAllSlos)
            .WithName("GetAllSlos")
            .WithSummary("Get all configured SLOs and their compliance status")
            .WithDescription("Returns a list of all SLOs with current compliance metrics, error budgets, and violation status.");

        group.MapGet("/slos/{sloName}", GetSloDetails)
            .WithName("GetSloDetails")
            .WithSummary("Get detailed metrics for a specific SLO")
            .WithDescription("Returns comprehensive metrics for a single SLO including compliance history, error budget, and event statistics.");

        // Error budget endpoints
        group.MapGet("/error-budgets", GetErrorBudgets)
            .WithName("GetErrorBudgets")
            .WithSummary("Get error budget status for all SLOs")
            .WithDescription("Returns current error budget status including remaining budget, allowed errors, and status (Healthy/Warning/Critical/Exhausted).");

        group.MapGet("/error-budgets/{sloName}", GetErrorBudget)
            .WithName("GetErrorBudget")
            .WithSummary("Get error budget status for a specific SLO")
            .WithDescription("Returns detailed error budget information for a single SLO.");

        // Deployment policy endpoint
        group.MapGet("/deployment-policy", GetDeploymentPolicy)
            .WithName("GetDeploymentPolicy")
            .WithSummary("Get deployment policy recommendations based on error budgets")
            .WithDescription("Returns deployment policy recommendations (Normal/Cautious/Restricted/Halt) based on current error budget status.");

        // Configuration endpoint
        group.MapGet("/config", GetSreConfig)
            .WithName("GetSreConfig")
            .WithSummary("Get current SRE configuration")
            .WithDescription("Returns the current SRE configuration including enabled SLOs, thresholds, and evaluation settings.");

        return group;
    }

    /// <summary>
    /// Gets all configured SLOs and their compliance status.
    /// </summary>
    private static async Task<IResult> GetAllSlos(
        [FromServices] ISliMetrics sliMetrics,
        [FromServices] IErrorBudgetTracker errorBudgetTracker,
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetAllSlos",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            if (!sreOptions.Enabled)
            {
                return Results.Ok(new
                {
                    enabled = false,
                    message = "SRE features are not enabled",
                    slos = Array.Empty<object>()
                });
            }

            var window = TimeSpan.FromDays(sreOptions.RollingWindowDays);
            var statistics = sliMetrics.GetAllStatistics(window);
            var errorBudgets = errorBudgetTracker.GetAllErrorBudgets();

            var slos = sreOptions.Slos
                .Where(kvp => kvp.Value.Enabled)
                .Select(kvp =>
                {
                    var sloName = kvp.Key;
                    var sloConfig = kvp.Value;
                    var stat = statistics.FirstOrDefault(s => s.Name == sloName);
                    var budget = errorBudgets.FirstOrDefault(b => b.SloName == sloName);

                    return new
                    {
                        name = sloName,
                        type = sloConfig.Type.ToString(),
                        target = sloConfig.Target,
                        description = sloConfig.Description,
                        thresholdMs = sloConfig.ThresholdMs,
                        compliance = new
                        {
                            actual = stat?.ActualSli ?? 0.0,
                            isMet = stat != null && stat.ActualSli >= sloConfig.Target,
                            margin = stat != null ? stat.ActualSli - sloConfig.Target : 0.0,
                            totalEvents = stat?.TotalEvents ?? 0,
                            goodEvents = stat?.GoodEvents ?? 0,
                            badEvents = stat?.BadEvents ?? 0
                        },
                        errorBudget = budget != null ? new
                        {
                            status = budget.Status.ToString(),
                            remaining = budget.BudgetRemaining,
                            remainingErrors = budget.RemainingErrors,
                            allowedErrors = budget.AllowedErrors
                        } : null,
                        windowDays = sreOptions.RollingWindowDays
                    };
                })
                .ToList();

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: "SLOs",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["sloCount"] = slos.Count
                });

            return Results.Ok(new
            {
                enabled = true,
                rollingWindowDays = sreOptions.RollingWindowDays,
                evaluationIntervalMinutes = sreOptions.EvaluationIntervalMinutes,
                slos = slos
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetAllSlos",
                resourceType: "SRE",
                resourceId: "SLOs",
                details: "Failed to retrieve SLOs",
                exception: ex);

            logger.LogError(ex, "Error retrieving SLOs");
            return Results.Problem("Failed to retrieve SLOs", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets detailed metrics for a specific SLO.
    /// </summary>
    private static async Task<IResult> GetSloDetails(
        string sloName,
        [FromServices] ISliMetrics sliMetrics,
        [FromServices] IErrorBudgetTracker errorBudgetTracker,
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetSloDetails",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            if (!sreOptions.Enabled)
            {
                return Results.BadRequest(new { error = "SRE features are not enabled" });
            }

            if (!sreOptions.Slos.TryGetValue(sloName, out var sloConfig) || !sloConfig.Enabled)
            {
                return Results.NotFound(new { error = $"SLO '{sloName}' not found or not enabled" });
            }

            var window = TimeSpan.FromDays(sreOptions.RollingWindowDays);
            var statistics = sliMetrics.GetStatistics(sloName, window);
            var errorBudget = errorBudgetTracker.GetErrorBudget(sloName);

            if (statistics == null)
            {
                return Results.Ok(new
                {
                    name = sloName,
                    type = sloConfig.Type.ToString(),
                    target = sloConfig.Target,
                    description = sloConfig.Description,
                    thresholdMs = sloConfig.ThresholdMs,
                    includeEndpoints = sloConfig.IncludeEndpoints,
                    excludeEndpoints = sloConfig.ExcludeEndpoints,
                    message = "No data available yet",
                    windowDays = sreOptions.RollingWindowDays
                });
            }

            var isMet = statistics.ActualSli >= sloConfig.Target;
            var margin = statistics.ActualSli - sloConfig.Target;

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: $"SLO:{sloName}",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["sloName"] = sloName,
                    ["actualSli"] = statistics.ActualSli,
                    ["isMet"] = isMet
                });

            return Results.Ok(new
            {
                name = sloName,
                type = sloConfig.Type.ToString(),
                target = sloConfig.Target,
                description = sloConfig.Description,
                thresholdMs = sloConfig.ThresholdMs,
                includeEndpoints = sloConfig.IncludeEndpoints,
                excludeEndpoints = sloConfig.ExcludeEndpoints,
                compliance = new
                {
                    actual = statistics.ActualSli,
                    isMet = isMet,
                    margin = margin,
                    totalEvents = statistics.TotalEvents,
                    goodEvents = statistics.GoodEvents,
                    badEvents = statistics.BadEvents,
                    windowStart = statistics.WindowStart,
                    windowEnd = statistics.WindowEnd
                },
                errorBudget = errorBudget != null ? new
                {
                    status = errorBudget.Status.ToString(),
                    remaining = errorBudget.BudgetRemaining,
                    remainingErrors = errorBudget.RemainingErrors,
                    allowedErrors = errorBudget.AllowedErrors,
                    failedRequests = errorBudget.FailedRequests,
                    totalRequests = errorBudget.TotalRequests
                } : null,
                windowDays = sreOptions.RollingWindowDays
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetSloDetails",
                resourceType: "SRE",
                resourceId: $"SLO:{sloName}",
                details: "Failed to retrieve SLO details",
                exception: ex);

            logger.LogError(ex, "Error retrieving SLO details for {SloName}", sloName);
            return Results.Problem("Failed to retrieve SLO details", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets error budget status for all SLOs.
    /// </summary>
    private static async Task<IResult> GetErrorBudgets(
        [FromServices] IErrorBudgetTracker errorBudgetTracker,
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetErrorBudgets",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            if (!sreOptions.Enabled)
            {
                return Results.Ok(new
                {
                    enabled = false,
                    message = "SRE features are not enabled",
                    errorBudgets = Array.Empty<object>()
                });
            }

            var budgets = errorBudgetTracker.GetAllErrorBudgets();

            var result = budgets.Select(b => new
            {
                sloName = b.SloName,
                target = b.Target,
                status = b.Status.ToString(),
                budgetRemaining = b.BudgetRemaining,
                remainingErrors = b.RemainingErrors,
                allowedErrors = b.AllowedErrors,
                failedRequests = b.FailedRequests,
                totalRequests = b.TotalRequests,
                actualSli = b.ActualSli,
                sloMet = b.SloMet,
                windowDays = b.WindowDays
            }).ToList();

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: "ErrorBudgets",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["budgetCount"] = result.Count
                });

            return Results.Ok(new
            {
                enabled = true,
                thresholds = new
                {
                    warning = sreOptions.ErrorBudgetThresholds.WarningThreshold,
                    critical = sreOptions.ErrorBudgetThresholds.CriticalThreshold
                },
                errorBudgets = result
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetErrorBudgets",
                resourceType: "SRE",
                resourceId: "ErrorBudgets",
                details: "Failed to retrieve error budgets",
                exception: ex);

            logger.LogError(ex, "Error retrieving error budgets");
            return Results.Problem("Failed to retrieve error budgets", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets error budget status for a specific SLO.
    /// </summary>
    private static async Task<IResult> GetErrorBudget(
        string sloName,
        [FromServices] IErrorBudgetTracker errorBudgetTracker,
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetErrorBudget",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            if (!sreOptions.Enabled)
            {
                return Results.BadRequest(new { error = "SRE features are not enabled" });
            }

            var budget = errorBudgetTracker.GetErrorBudget(sloName);

            if (budget == null)
            {
                return Results.NotFound(new { error = $"Error budget for SLO '{sloName}' not found" });
            }

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: $"ErrorBudget:{sloName}",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["sloName"] = sloName,
                    ["status"] = budget.Status.ToString(),
                    ["budgetRemaining"] = budget.BudgetRemaining
                });

            return Results.Ok(new
            {
                sloName = budget.SloName,
                target = budget.Target,
                status = budget.Status.ToString(),
                budgetRemaining = budget.BudgetRemaining,
                remainingErrors = budget.RemainingErrors,
                allowedErrors = budget.AllowedErrors,
                failedRequests = budget.FailedRequests,
                totalRequests = budget.TotalRequests,
                actualSli = budget.ActualSli,
                sloMet = budget.SloMet,
                windowDays = budget.WindowDays
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetErrorBudget",
                resourceType: "SRE",
                resourceId: $"ErrorBudget:{sloName}",
                details: "Failed to retrieve error budget",
                exception: ex);

            logger.LogError(ex, "Error retrieving error budget for {SloName}", sloName);
            return Results.Problem("Failed to retrieve error budget", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets deployment policy recommendations based on error budgets.
    /// </summary>
    private static async Task<IResult> GetDeploymentPolicy(
        [FromServices] IErrorBudgetTracker errorBudgetTracker,
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetDeploymentPolicy",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            if (!sreOptions.Enabled)
            {
                return Results.Ok(new
                {
                    enabled = false,
                    canDeploy = true,
                    recommendation = "Normal",
                    message = "SRE features are not enabled. Normal deployment velocity approved."
                });
            }

            var policy = errorBudgetTracker.GetDeploymentPolicy();

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: "DeploymentPolicy",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["recommendation"] = policy.Recommendation.ToString(),
                    ["canDeploy"] = policy.CanDeploy
                });

            return Results.Ok(new
            {
                enabled = true,
                canDeploy = policy.CanDeploy,
                recommendation = policy.Recommendation.ToString(),
                reason = policy.Reason,
                details = policy.Details,
                affectedSlos = policy.AffectedSlos
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetDeploymentPolicy",
                resourceType: "SRE",
                resourceId: "DeploymentPolicy",
                details: "Failed to retrieve deployment policy",
                exception: ex);

            logger.LogError(ex, "Error retrieving deployment policy");
            return Results.Problem("Failed to retrieve deployment policy", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets the current SRE configuration.
    /// </summary>
    private static async Task<IResult> GetSreConfig(
        [FromServices] IOptions<SreOptions> options,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetSreConfig",
                    reason: "User not authenticated");
                return Results.Unauthorized();
            }

            var sreOptions = options.Value;

            var slos = sreOptions.Slos.Select(kvp => new
            {
                name = kvp.Key,
                enabled = kvp.Value.Enabled,
                type = kvp.Value.Type.ToString(),
                target = kvp.Value.Target,
                thresholdMs = kvp.Value.ThresholdMs,
                description = kvp.Value.Description,
                includeEndpoints = kvp.Value.IncludeEndpoints,
                excludeEndpoints = kvp.Value.ExcludeEndpoints
            }).ToList();

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "SRE",
                resourceId: "Configuration",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["enabled"] = sreOptions.Enabled,
                    ["sloCount"] = slos.Count
                });

            return Results.Ok(new
            {
                enabled = sreOptions.Enabled,
                rollingWindowDays = sreOptions.RollingWindowDays,
                evaluationIntervalMinutes = sreOptions.EvaluationIntervalMinutes,
                errorBudgetThresholds = new
                {
                    warning = sreOptions.ErrorBudgetThresholds.WarningThreshold,
                    critical = sreOptions.ErrorBudgetThresholds.CriticalThreshold
                },
                slos = slos
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetSreConfig",
                resourceType: "SRE",
                resourceId: "Configuration",
                details: "Failed to retrieve SRE configuration",
                exception: ex);

            logger.LogError(ex, "Error retrieving SRE configuration");
            return Results.Problem("Failed to retrieve SRE configuration", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
