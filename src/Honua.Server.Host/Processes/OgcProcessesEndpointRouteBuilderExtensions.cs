// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Processes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Processes;

/// <summary>
/// Extension methods for mapping OGC API - Processes endpoints.
/// </summary>
public static class OgcProcessesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps OGC API - Processes endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapOgcProcesses(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/processes")
            .WithTags("OGC API - Processes");

        // Process discovery
        group.MapGet("/", OgcProcessesHandlers.GetProcesses)
            .WithName("GetProcesses")
            .WithSummary("Get list of available processes")
            .Produces<object>(StatusCodes.Status200OK);

        group.MapGet("/{processId}", OgcProcessesHandlers.GetProcess)
            .WithName("GetProcess")
            .WithSummary("Get process description")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        // Process execution
        group.MapPost("/{processId}/execution", OgcProcessesHandlers.ExecuteProcess)
            .WithName("ExecuteProcess")
            .WithSummary("Execute a process")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        // Preview endpoints
        group.MapPost("/{processId}/preview", OgcProcessesPreviewHandlers.ExecutePreview)
            .WithName("ExecutePreview")
            .WithSummary("Execute a process in preview mode for quick feedback")
            .WithDescription("Executes a process with optimizations for quick preview. Results are limited and may be simplified.")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        group.MapPost("/{processId}/validate", OgcProcessesPreviewHandlers.ValidatePreviewInputs)
            .WithName("ValidatePreviewInputs")
            .WithSummary("Validate process inputs without executing")
            .WithDescription("Validates inputs and returns potential errors and warnings")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        // Job management
        var jobsGroup = endpoints.MapGroup("/jobs")
            .WithTags("OGC API - Processes - Jobs");

        jobsGroup.MapGet("/", OgcProcessesHandlers.GetJobs)
            .WithName("GetJobs")
            .WithSummary("Get list of jobs")
            .Produces<object>(StatusCodes.Status200OK);

        jobsGroup.MapGet("/{jobId}", OgcProcessesHandlers.GetJobStatus)
            .WithName("GetJobStatus")
            .WithSummary("Get job status")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        jobsGroup.MapGet("/{jobId}/results", OgcProcessesHandlers.GetJobResults)
            .WithName("GetJobResults")
            .WithSummary("Get job results")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status404NotFound)
;

        jobsGroup.MapDelete("/{jobId}", OgcProcessesHandlers.DismissJob)
            .WithName("DismissJob")
            .WithSummary("Dismiss (cancel) a job")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status410Gone)
;

        return endpoints;
    }
}
