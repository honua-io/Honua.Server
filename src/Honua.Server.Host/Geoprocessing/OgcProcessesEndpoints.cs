// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Geoprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Geoprocessing;

/// <summary>
/// OGC API - Processes endpoints (Priority 2 API surface)
/// Implements OGC API - Processes Part 1: Core standard
/// </summary>
public static class OgcProcessesEndpoints
{
    public static IEndpointRouteBuilder MapOgcProcessesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/processes")
            .WithTags("OGC API - Processes")
            .RequireAuthorization();

        // GET /processes - List available processes
        group.MapGet("", GetProcessList)
            .WithName("GetProcessList")
            .WithOpenApi(op =>
            {
                op.Summary = "List available processes";
                op.Description = "Returns a list of available geoprocessing operations";
                return op;
            });

        // GET /processes/{processId} - Get process description
        group.MapGet("{processId}", GetProcessDescription)
            .WithName("GetProcessDescription")
            .WithOpenApi(op =>
            {
                op.Summary = "Get process description";
                op.Description = "Returns detailed information about a specific process";
                return op;
            });

        // POST /processes/{processId}/execution - Execute process
        group.MapPost("{processId}/execution", ExecuteProcess)
            .WithName("ExecuteProcess")
            .WithOpenApi(op =>
            {
                op.Summary = "Execute a process";
                op.Description = "Executes a geoprocessing operation synchronously or asynchronously";
                return op;
            });

        // GET /jobs/{jobId} - Get job status
        group.MapGet("/jobs/{jobId}", GetJobStatus)
            .WithName("GetJobStatus")
            .WithOpenApi(op =>
            {
                op.Summary = "Get job status";
                op.Description = "Returns the status of an asynchronous job";
                return op;
            });

        // DELETE /jobs/{jobId} - Cancel job
        group.MapDelete("/jobs/{jobId}", CancelJob)
            .WithName("CancelJob")
            .WithOpenApi(op =>
            {
                op.Summary = "Cancel a job";
                op.Description = "Cancels a pending or running job";
                return op;
            });

        // GET /jobs/{jobId}/results - Get job results
        group.MapGet("/jobs/{jobId}/results", GetJobResults)
            .WithName("GetJobResults")
            .WithOpenApi(op =>
            {
                op.Summary = "Get job results";
                op.Description = "Returns the results of a completed job";
                return op;
            });

        // GET /jobs - List jobs
        group.MapGet("/jobs", ListJobs)
            .WithName("ListJobs")
            .WithOpenApi(op =>
            {
                op.Summary = "List jobs";
                op.Description = "Returns a list of jobs for the authenticated user";
                return op;
            });

        return endpoints;
    }

    private static async Task<IResult> GetProcessList(
        [FromServices] IProcessRegistry registry,
        [FromServices] ILogger<IProcessRegistry> logger,
        HttpContext context,
        CancellationToken ct)
    {
        try
        {
            var processes = await registry.ListProcessesAsync(ct);

            var response = new OgcProcessList
            {
                Processes = processes.Select(p => new OgcProcessSummary
                {
                    Id = p.Id,
                    Version = p.Version,
                    Title = p.Title,
                    Description = p.Description,
                    Keywords = p.Keywords,
                    Links = new List<OgcLink>
                    {
                        new()
                        {
                            Href = $"{GetBaseUrl(context)}/processes/{p.Id}",
                            Rel = "self",
                            Type = "application/json",
                            Title = $"{p.Title} process description"
                        },
                        new()
                        {
                            Href = $"{GetBaseUrl(context)}/processes/{p.Id}/execution",
                            Rel = "execute",
                            Type = "application/json",
                            Title = $"Execute {p.Title}"
                        }
                    }
                }).ToList(),
                Links = new List<OgcLink>
                {
                    new()
                    {
                        Href = $"{GetBaseUrl(context)}/processes",
                        Rel = "self",
                        Type = "application/json",
                        Title = "This document"
                    }
                }
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list processes");
            return Results.Problem("Failed to list processes", statusCode: 500);
        }
    }

    private static async Task<IResult> GetProcessDescription(
        string processId,
        [FromServices] IProcessRegistry registry,
        [FromServices] ILogger<IProcessRegistry> logger,
        HttpContext context,
        CancellationToken ct)
    {
        try
        {
            var process = await registry.GetProcessAsync(processId, ct);

            if (process == null)
            {
                return Results.NotFound(new { error = $"Process '{processId}' not found" });
            }

            var response = new OgcProcessDescription
            {
                Id = process.Id,
                Version = process.Version,
                Title = process.Title,
                Description = process.Description,
                Keywords = process.Keywords,
                Inputs = process.Inputs.ToDictionary(
                    input => input.Name,
                    input => new OgcInputDescription
                    {
                        Title = input.Title ?? input.Name,
                        Description = input.Description,
                        Schema = new OgcInputSchema
                        {
                            Type = input.Type,
                            Format = GetFormat(input),
                            MinValue = input.MinValue,
                            MaxValue = input.MaxValue,
                            Enum = input.AllowedValues,
                            Required = input.Required,
                            Default = input.DefaultValue
                        }
                    }),
                Outputs = new Dictionary<string, OgcOutputDescription>
                {
                    ["result"] = new()
                    {
                        Title = "Process result",
                        Description = process.Output?.Description ?? "Geoprocessing result",
                        Schema = new OgcOutputSchema
                        {
                            Type = process.Output?.Type ?? "object",
                            ContentMediaType = GetMediaType(process.OutputFormats.FirstOrDefault() ?? "geojson")
                        }
                    }
                },
                Links = new List<OgcLink>
                {
                    new()
                    {
                        Href = $"{GetBaseUrl(context)}/processes/{processId}",
                        Rel = "self",
                        Type = "application/json",
                        Title = "This document"
                    },
                    new()
                    {
                        Href = $"{GetBaseUrl(context)}/processes/{processId}/execution",
                        Rel = "execute",
                        Type = "application/json",
                        Title = $"Execute {process.Title}"
                    }
                }
            };

            // Add documentation links from process definition
            foreach (var link in process.Links)
            {
                response.Links.Add(new OgcLink
                {
                    Href = link.Href,
                    Rel = link.Rel,
                    Type = link.Type,
                    Title = link.Title
                });
            }

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get process description for {ProcessId}", processId);
            return Results.Problem("Failed to get process description", statusCode: 500);
        }
    }

    private static async Task<IResult> ExecuteProcess(
        string processId,
        [FromBody] OgcExecuteRequest request,
        [FromServices] IControlPlane controlPlane,
        [FromServices] IProcessRegistry registry,
        [FromServices] ILogger<IControlPlane> logger,
        HttpContext context,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            // Verify process exists
            var process = await registry.GetProcessAsync(processId, ct);
            if (process == null)
            {
                return Results.NotFound(new { error = $"Process '{processId}' not found" });
            }

            // Extract user info
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? throw new UnauthorizedAccessException("User ID not found"));
            var userEmail = user.FindFirstValue("email");

            // Build execution request
            var execRequest = new ProcessExecutionRequest
            {
                ProcessId = processId,
                TenantId = tenantId,
                UserId = userId,
                UserEmail = userEmail,
                Inputs = request.Inputs ?? new Dictionary<string, object>(),
                Mode = DetermineExecutionMode(request.Response),
                ResponseFormat = GetResponseFormat(request.Response),
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = "OGC API",
                    ["user_agent"] = context.Request.Headers.UserAgent.ToString(),
                    ["ip_address"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                }
            };

            // Admission control
            var admission = await controlPlane.AdmitAsync(execRequest, ct);

            if (!admission.Admitted)
            {
                return Results.BadRequest(new
                {
                    type = "http://honua.io/errors/admission-denied",
                    title = "Execution request denied",
                    status = 400,
                    detail = string.Join("; ", admission.DenialReasons)
                });
            }

            // Execute based on mode
            if (admission.ExecutionMode == ExecutionMode.Sync)
            {
                // Synchronous execution - return result immediately
                var result = await controlPlane.ExecuteInlineAsync(admission, ct);

                return Results.Ok(new OgcExecuteResponse
                {
                    JobId = result.JobId,
                    Status = "successful",
                    Result = result.Output
                });
            }
            else
            {
                // Asynchronous execution - enqueue and return job ID
                var run = await controlPlane.EnqueueAsync(admission, ct);

                context.Response.Headers.Location = $"{GetBaseUrl(context)}/processes/jobs/{run.JobId}";

                return Results.Accepted($"/processes/jobs/{run.JobId}", new OgcStatusInfo
                {
                    JobId = run.JobId,
                    ProcessId = run.ProcessId,
                    Status = MapStatus(run.Status),
                    Created = run.CreatedAt,
                    Started = run.StartedAt,
                    Progress = run.Progress,
                    Message = run.ProgressMessage,
                    Links = new List<OgcLink>
                    {
                        new()
                        {
                            Href = $"{GetBaseUrl(context)}/processes/jobs/{run.JobId}",
                            Rel = "status",
                            Type = "application/json",
                            Title = "Job status"
                        },
                        new()
                        {
                            Href = $"{GetBaseUrl(context)}/processes/jobs/{run.JobId}/results",
                            Rel = "results",
                            Type = "application/json",
                            Title = "Job results"
                        }
                    }
                });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized access attempt");
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute process {ProcessId}", processId);
            return Results.Problem("Failed to execute process", statusCode: 500);
        }
    }

    private static async Task<IResult> GetJobStatus(
        string jobId,
        [FromServices] IControlPlane controlPlane,
        [FromServices] ILogger<IControlPlane> logger,
        HttpContext context,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            // SECURITY: Get tenant ID first for isolation
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));

            // Retrieve with tenant isolation enforced at the data layer
            var run = await controlPlane.GetJobStatusAsync(jobId, tenantId, ct);

            if (run == null)
            {
                return Results.NotFound(new { error = $"Job '{jobId}' not found" });
            }

            var response = new OgcStatusInfo
            {
                JobId = run.JobId,
                ProcessId = run.ProcessId,
                Status = MapStatus(run.Status),
                Created = run.CreatedAt,
                Started = run.StartedAt,
                Finished = run.CompletedAt,
                Progress = run.Progress,
                Message = run.ProgressMessage,
                Links = new List<OgcLink>
                {
                    new()
                    {
                        Href = $"{GetBaseUrl(context)}/processes/jobs/{jobId}",
                        Rel = "self",
                        Type = "application/json",
                        Title = "This document"
                    }
                }
            };

            if (run.Status == ProcessRunStatus.Completed)
            {
                response.Links.Add(new OgcLink
                {
                    Href = $"{GetBaseUrl(context)}/processes/jobs/{jobId}/results",
                    Rel = "results",
                    Type = "application/json",
                    Title = "Job results"
                });
            }

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job status for {JobId}", jobId);
            return Results.Problem("Failed to get job status", statusCode: 500);
        }
    }

    private static async Task<IResult> CancelJob(
        string jobId,
        [FromServices] IControlPlane controlPlane,
        [FromServices] ILogger<IControlPlane> logger,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            // SECURITY: Get tenant ID first for isolation
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));

            // Cancel with tenant isolation enforced at the data layer
            var cancelled = await controlPlane.CancelJobAsync(jobId, tenantId, "Cancelled by user", ct);

            if (cancelled)
            {
                return Results.Ok(new { message = "Job cancelled successfully" });
            }
            else
            {
                return Results.BadRequest(new { error = "Job cannot be cancelled (may be already completed)" });
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return Results.Problem("Failed to cancel job", statusCode: 500);
        }
    }

    private static async Task<IResult> GetJobResults(
        string jobId,
        [FromServices] IControlPlane controlPlane,
        [FromServices] ILogger<IControlPlane> logger,
        HttpContext context,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            // SECURITY: Get tenant ID first for isolation
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));

            // Retrieve with tenant isolation enforced at the data layer
            var run = await controlPlane.GetJobStatusAsync(jobId, tenantId, ct);

            if (run == null)
            {
                return Results.NotFound(new { error = $"Job '{jobId}' not found" });
            }

            if (run.Status != ProcessRunStatus.Completed)
            {
                return Results.BadRequest(new { error = $"Job is not completed yet (status: {run.Status})" });
            }

            // If output URL is set, redirect to it
            if (!string.IsNullOrEmpty(run.OutputUrl))
            {
                return Results.Redirect(run.OutputUrl);
            }

            // Return inline result
            return Results.Ok(run.Output);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job results for {JobId}", jobId);
            return Results.Problem("Failed to get job results", statusCode: 500);
        }
    }

    private static async Task<IResult> ListJobs(
        [FromServices] IControlPlane controlPlane,
        [FromServices] ILogger<IControlPlane> logger,
        HttpContext context,
        ClaimsPrincipal user,
        [FromQuery] string? processId,
        [FromQuery] string? status,
        CancellationToken ct,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        try
        {
            // SECURITY: Get tenant ID for isolation
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));

            var query = new ProcessRunQuery
            {
                TenantId = tenantId,
                ProcessId = processId,
                Status = status != null ? Enum.Parse<ProcessRunStatus>(status, true) : null,
                Limit = Math.Min(limit, 1000),
                Offset = offset
            };

            // Query with tenant isolation (isSystemAdmin = false)
            var result = await controlPlane.QueryRunsAsync(query, isSystemAdmin: false, ct);

            var response = new OgcJobList
            {
                Jobs = result.Runs.Select(run => new OgcJobSummary
                {
                    JobId = run.JobId,
                    ProcessId = run.ProcessId,
                    Status = MapStatus(run.Status),
                    Created = run.CreatedAt,
                    Started = run.StartedAt,
                    Finished = run.CompletedAt,
                    Progress = run.Progress,
                    Links = new List<OgcLink>
                    {
                        new()
                        {
                            Href = $"{GetBaseUrl(context)}/processes/jobs/{run.JobId}",
                            Rel = "status",
                            Type = "application/json",
                            Title = "Job status"
                        }
                    }
                }).ToList(),
                Links = new List<OgcLink>
                {
                    new()
                    {
                        Href = $"{GetBaseUrl(context)}/processes/jobs?limit={limit}&offset={offset}",
                        Rel = "self",
                        Type = "application/json",
                        Title = "This document"
                    }
                }
            };

            // Add pagination links
            if (result.TotalCount > offset + limit)
            {
                response.Links.Add(new OgcLink
                {
                    Href = $"{GetBaseUrl(context)}/processes/jobs?limit={limit}&offset={offset + limit}",
                    Rel = "next",
                    Type = "application/json",
                    Title = "Next page"
                });
            }

            if (offset > 0)
            {
                response.Links.Add(new OgcLink
                {
                    Href = $"{GetBaseUrl(context)}/processes/jobs?limit={limit}&offset={Math.Max(0, offset - limit)}",
                    Rel = "prev",
                    Type = "application/json",
                    Title = "Previous page"
                });
            }

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list jobs");
            return Results.Problem("Failed to list jobs", statusCode: 500);
        }
    }

    // Helper methods

    private static string GetBaseUrl(HttpContext context)
    {
        return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    }

    private static string MapStatus(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Pending => "accepted",
            ProcessRunStatus.Running => "running",
            ProcessRunStatus.Completed => "successful",
            ProcessRunStatus.Failed => "failed",
            ProcessRunStatus.Cancelled => "dismissed",
            ProcessRunStatus.Timeout => "failed",
            _ => "unknown"
        };
    }

    private static ExecutionMode DetermineExecutionMode(string? response)
    {
        return response?.ToLowerInvariant() switch
        {
            "document" => ExecutionMode.Async,
            "raw" => ExecutionMode.Sync,
            _ => ExecutionMode.Auto
        };
    }

    private static string GetResponseFormat(string? response)
    {
        // Parse response preference from OGC format
        return "geojson"; // Default
    }

    private static string? GetFormat(ProcessParameter input)
    {
        return input.Type switch
        {
            "geometry" => "geometry",
            "number" => "double",
            "string" => "string",
            "boolean" => "boolean",
            _ => null
        };
    }

    private static string GetMediaType(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "geojson" => "application/geo+json",
            "json" => "application/json",
            "shapefile" => "application/x-shapefile",
            "geoparquet" => "application/vnd.apache.parquet",
            _ => "application/octet-stream"
        };
    }
}

// OGC API - Processes data models

public record OgcProcessList
{
    public List<OgcProcessSummary> Processes { get; init; } = new();
    public List<OgcLink> Links { get; init; } = new();
}

public record OgcProcessSummary
{
    public required string Id { get; init; }
    public string Version { get; init; } = "1.0.0";
    public required string Title { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = new();
    public List<OgcLink> Links { get; init; } = new();
}

public record OgcProcessDescription : OgcProcessSummary
{
    public Dictionary<string, OgcInputDescription> Inputs { get; init; } = new();
    public Dictionary<string, OgcOutputDescription> Outputs { get; init; } = new();
}

public record OgcInputDescription
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required OgcInputSchema Schema { get; init; }
}

public record OgcInputSchema
{
    public required string Type { get; init; }
    public string? Format { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public List<object>? Enum { get; init; }
    public bool Required { get; init; }
    public object? Default { get; init; }
}

public record OgcOutputDescription
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required OgcOutputSchema Schema { get; init; }
}

public record OgcOutputSchema
{
    public required string Type { get; init; }
    public string? ContentMediaType { get; init; }
}

public record OgcExecuteRequest
{
    public Dictionary<string, object>? Inputs { get; init; }
    public string? Response { get; init; } // "document" or "raw"
}

public record OgcExecuteResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    public Dictionary<string, object>? Result { get; init; }
}

public record OgcStatusInfo
{
    public required string JobId { get; init; }
    public required string ProcessId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? Started { get; init; }
    public DateTimeOffset? Finished { get; init; }
    public int Progress { get; init; }
    public string? Message { get; init; }
    public List<OgcLink> Links { get; init; } = new();
}

public record OgcJobList
{
    public List<OgcJobSummary> Jobs { get; init; } = new();
    public List<OgcLink> Links { get; init; } = new();
}

public record OgcJobSummary
{
    public required string JobId { get; init; }
    public required string ProcessId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? Started { get; init; }
    public DateTimeOffset? Finished { get; init; }
    public int Progress { get; init; }
    public List<OgcLink> Links { get; init; } = new();
}

public record OgcLink
{
    public required string Href { get; init; }
    public required string Rel { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
}
