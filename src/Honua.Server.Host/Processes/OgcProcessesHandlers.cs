// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Processes;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Processes;

/// <summary>
/// OGC API - Processes handlers.
/// </summary>
internal static class OgcProcessesHandlers
{
    /// <summary>
    /// Gets the process list.
    /// </summary>
    public static Task<IResult> GetProcesses(
        HttpRequest request,
        [FromServices] IProcessRegistry processRegistry,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(processRegistry);

        var processes = processRegistry.GetAllProcesses();
        var summaries = processes.Select(p => new
        {
            id = p.Id,
            version = p.Version,
            title = p.Title,
            description = p.Description,
            keywords = p.Keywords,
            jobControlOptions = p.JobControlOptions,
            links = BuildProcessLinks(request, p.Id)
        }).ToList();

        var response = new
        {
            processes = summaries,
            links = new[]
            {
                BuildLink(request, "/processes", "self", "application/json", "Process list"),
                BuildLink(request, "/", "alternate", "application/json", "Landing page")
            }
        };

        return Task.FromResult(Results.Ok(response));
    }

    /// <summary>
    /// Gets a process description.
    /// </summary>
    public static Task<IResult> GetProcess(
        string processId,
        HttpRequest request,
        [FromServices] IProcessRegistry processRegistry,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(processRegistry);

        var process = processRegistry.GetProcess(processId);
        if (process is null)
        {
            return Task.FromResult(Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-process",
                title = "Process not found",
                detail = $"The process '{processId}' does not exist.",
                status = 404
            }));
        }

        var description = process.Description;
        var response = new
        {
            id = description.Id,
            version = description.Version,
            title = description.Title,
            description = description.Description,
            keywords = description.Keywords,
            jobControlOptions = description.JobControlOptions,
            outputTransmission = description.OutputTransmission,
            inputs = description.Inputs,
            outputs = description.Outputs,
            links = BuildProcessLinks(request, processId)
        };

        return Task.FromResult(Results.Ok(response));
    }

    /// <summary>
    /// Executes a process.
    /// </summary>
    public static async Task<IResult> ExecuteProcess(
        string processId,
        HttpRequest request,
        ExecuteRequest executeRequest,
        [FromServices] IProcessRegistry processRegistry,
        [FromServices] ProcessExecutionService executionService,
        [FromServices] Core.Processes.ProcessJobStore jobStore,
        [FromServices] CompletedProcessJobStore completedJobStore,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(processRegistry);
        Guard.NotNull(executionService);

        var process = processRegistry.GetProcess(processId);
        if (process is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-process",
                title = "Process not found",
                detail = $"The process '{processId}' does not exist.",
                status = 404
            });
        }

        // Determine execution mode from Prefer header
        var preferHeader = request.Headers["Prefer"].ToString();
        var isAsync = !preferHeader.Contains("respond-async", StringComparison.OrdinalIgnoreCase) ||
                      preferHeader.Contains("respond-async", StringComparison.OrdinalIgnoreCase);

        // Create job
        var jobId = Guid.NewGuid().ToString();
        var job = new ProcessJob(jobId, processId, executeRequest.Inputs);

        if (isAsync)
        {
            // Async execution
            var enqueued = await executionService.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
            if (!enqueued)
            {
                return Results.Problem(
                    "Failed to enqueue job",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Execution failed");
            }

            var status = job.GetStatus();
            var statusResponse = BuildStatusResponse(request, status);

            return Results.Created($"/jobs/{jobId}", statusResponse);
        }
        else
        {
            // Sync execution
            try
            {
                job.MarkStarted();
                var results = await process.ExecuteAsync(executeRequest.Inputs, job, cancellationToken).ConfigureAwait(false);
                job.MarkCompleted(results);

                // Store for retrieval
                await completedJobStore.AddAsync(job, cancellationToken).ConfigureAwait(false);

                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                job.MarkFailed(ex.Message);
                return Results.Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Execution failed");
            }
            finally
            {
                job.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets job status.
    /// </summary>
    public static async Task<IResult> GetJobStatus(
        string jobId,
        HttpRequest request,
        [FromServices] Core.Processes.ProcessJobStore jobStore,
        [FromServices] CompletedProcessJobStore completedJobStore,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(jobStore);
        Guard.NotNull(completedJobStore);

        // Check active jobs first
        var job = await jobStore.GetJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            // Check completed jobs
            job = await completedJobStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        }

        if (job is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-job",
                title = "Job not found",
                detail = $"The job '{jobId}' does not exist.",
                status = 404
            });
        }

        var status = job.GetStatus();
        var response = BuildStatusResponse(request, status);

        return Results.Ok(response);
    }

    /// <summary>
    /// Gets job results.
    /// </summary>
    public static async Task<IResult> GetJobResults(
        string jobId,
        HttpRequest request,
        [FromServices] Core.Processes.ProcessJobStore jobStore,
        [FromServices] CompletedProcessJobStore completedJobStore,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(jobStore);
        Guard.NotNull(completedJobStore);

        // Check active jobs first
        var job = await jobStore.GetJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            // Check completed jobs
            job = await completedJobStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        }

        if (job is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-job",
                title = "Job not found",
                detail = $"The job '{jobId}' does not exist.",
                status = 404
            });
        }

        var status = job.GetStatus();

        // Results only available if job is successful
        if (status.Status != JobStatus.Successful)
        {
            return Results.Problem(
                $"Job is not yet complete. Current status: {status.Status}",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Results not available");
        }

        var results = job.GetResults();
        return Results.Ok(results);
    }

    /// <summary>
    /// Dismisses (cancels) a job.
    /// </summary>
    public static async Task<IResult> DismissJob(
        string jobId,
        HttpRequest request,
        [FromServices] Core.Processes.ProcessJobStore jobStore,
        [FromServices] CompletedProcessJobStore completedJobStore,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(jobStore);

        // Check active jobs
        var job = await jobStore.GetJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            // Check if already completed
            job = await completedJobStore.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (job is not null && job.IsTerminal)
            {
                // Already completed - return 410 Gone
                return Results.StatusCode(StatusCodes.Status410Gone);
            }

            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-job",
                title = "Job not found",
                detail = $"The job '{jobId}' does not exist.",
                status = 404
            });
        }

        // Cancel the job
        job.RequestCancellation();

        var status = job.GetStatus();
        var response = BuildStatusResponse(request, status);

        return Results.Ok(response);
    }

    /// <summary>
    /// Gets the list of jobs.
    /// </summary>
    public static async Task<IResult> GetJobs(
        HttpRequest request,
        Core.Processes.ProcessJobStore jobStore,
        CompletedProcessJobStore completedJobStore,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNull(jobStore);
        Guard.NotNull(completedJobStore);

        var activeJobs = await jobStore.GetActiveJobsAsync(cancellationToken).ConfigureAwait(false);
        var completedJobIds = await completedJobStore.GetAllJobIdsAsync(cancellationToken).ConfigureAwait(false);

        var jobs = new List<object>();

        foreach (var job in activeJobs)
        {
            var status = job.GetStatus();
            jobs.Add(BuildStatusSummary(request, status));
        }

        foreach (var completedJobId in completedJobIds.Take(100)) // Limit to 100 most recent
        {
            var job = await completedJobStore.GetAsync(completedJobId, cancellationToken).ConfigureAwait(false);
            if (job is not null)
            {
                var status = job.GetStatus();
                jobs.Add(BuildStatusSummary(request, status));
            }
        }

        var response = new
        {
            jobs,
            links = new[]
            {
                BuildLink(request, "/jobs", "self", "application/json", "Job list")
            }
        };

        return Results.Ok(response);
    }

    private static object BuildStatusResponse(HttpRequest request, StatusInfo status)
    {
        var links = new List<ProcessLink>
        {
            new ProcessLink
            {
                Href = BuildUrl(request, $"/jobs/{status.JobId}"),
                Rel = "self",
                Type = "application/json",
                Title = "Job status"
            }
        };

        if (status.Status == JobStatus.Successful)
        {
            links.Add(new ProcessLink
            {
                Href = BuildUrl(request, $"/jobs/{status.JobId}/results"),
                Rel = "results",
                Type = "application/json",
                Title = "Job results"
            });
        }

        return new
        {
            jobID = status.JobId,
            processID = status.ProcessId,
            type = status.Type,
            status = status.Status.ToString().ToLowerInvariant(),
            message = status.Message,
            created = status.Created,
            started = status.Started,
            finished = status.Finished,
            updated = status.Updated,
            progress = status.Progress,
            links
        };
    }

    private static object BuildStatusSummary(HttpRequest request, StatusInfo status)
    {
        return new
        {
            jobID = status.JobId,
            processID = status.ProcessId,
            status = status.Status.ToString().ToLowerInvariant(),
            created = status.Created,
            finished = status.Finished,
            links = new[]
            {
                new ProcessLink
                {
                    Href = BuildUrl(request, $"/jobs/{status.JobId}"),
                    Rel = "self",
                    Type = "application/json",
                    Title = "Job status"
                }
            }
        };
    }

    private static List<ProcessLink> BuildProcessLinks(HttpRequest request, string processId)
    {
        return new List<ProcessLink>
        {
            new ProcessLink
            {
                Href = BuildUrl(request, $"/processes/{processId}"),
                Rel = "self",
                Type = "application/json",
                Title = "Process description"
            },
            new ProcessLink
            {
                Href = BuildUrl(request, $"/processes/{processId}/execution"),
                Rel = "http://www.opengis.net/def/rel/ogc/1.0/execute",
                Type = "application/json",
                Title = "Execute process"
            }
        };
    }

    private static ProcessLink BuildLink(HttpRequest request, string path, string rel, string type, string title)
    {
        return new ProcessLink
        {
            Href = BuildUrl(request, path),
            Rel = rel,
            Type = type,
            Title = title
        };
    }

    private static string BuildUrl(HttpRequest request, string path)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value;
        return $"{scheme}://{host}{path}";
    }
}
