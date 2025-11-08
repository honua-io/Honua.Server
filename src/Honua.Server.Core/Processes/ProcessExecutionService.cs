// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Background service that executes process jobs asynchronously.
/// </summary>
public sealed class ProcessExecutionService : BackgroundService
{
    private readonly Channel<ProcessJob> _queue;
    private readonly ProcessJobStore _jobStore;
    private readonly CompletedProcessJobStore _completedJobStore;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogger<ProcessExecutionService> _logger;

    public ProcessExecutionService(
        ProcessJobStore jobStore,
        CompletedProcessJobStore completedJobStore,
        IProcessRegistry processRegistry,
        ILogger<ProcessExecutionService> logger)
    {
        _jobStore = jobStore;
        _completedJobStore = completedJobStore;
        _processRegistry = processRegistry;
        _logger = logger;

        // Bounded channel with capacity of 1000 jobs
        _queue = Channel.CreateBounded<ProcessJob>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Enqueues a job for async execution.
    /// </summary>
    public async ValueTask<bool> EnqueueAsync(ProcessJob job, CancellationToken cancellationToken = default)
    {
        var registered = await _jobStore.RegisterAsync(job, cancellationToken).ConfigureAwait(false);
        if (!registered)
        {
            return false;
        }

        try
        {
            await _queue.Writer.WriteAsync(job, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job {JobId} enqueue was cancelled", job.JobId);
            await _jobStore.UnregisterByIdAsync(job.JobId, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue job {JobId} to execution queue", job.JobId);
            await _jobStore.UnregisterByIdAsync(job.JobId, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Process execution service started");

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessJobAsync(job, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing job {JobId}", job.JobId);
                }
            }, stoppingToken);
        }

        _logger.LogInformation("Process execution service stopped");
    }

    private async Task ProcessJobAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        try
        {
            var process = _processRegistry.GetProcess(job.ProcessId);
            if (process is null)
            {
                job.MarkFailed($"Process '{job.ProcessId}' not found");
                _logger.LogWarning("Job {JobId} failed: process not found", job.JobId);
                await CompleteJobAsync(job, cancellationToken).ConfigureAwait(false);
                return;
            }

            job.MarkStarted();
            _logger.LogInformation("Starting job {JobId} for process {ProcessId}", job.JobId, job.ProcessId);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(job.Token, cancellationToken);

            var results = await process.ExecuteAsync(job.Inputs, job, linkedCts.Token).ConfigureAwait(false);

            if (linkedCts.Token.IsCancellationRequested)
            {
                job.MarkDismissed("Job was cancelled");
                _logger.LogInformation("Job {JobId} was cancelled", job.JobId);
            }
            else
            {
                job.MarkCompleted(results);
                _logger.LogInformation("Job {JobId} completed successfully", job.JobId);
            }
        }
        catch (OperationCanceledException)
        {
            job.MarkDismissed("Job was cancelled");
            _logger.LogInformation("Job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);
        }
        finally
        {
            await CompleteJobAsync(job, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CompleteJobAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        await _jobStore.UnregisterByIdAsync(job.JobId, cancellationToken).ConfigureAwait(false);

        // Store completed jobs for retrieval (keep for 24 hours)
        await _completedJobStore.AddAsync(job, cancellationToken).ConfigureAwait(false);
    }
}
