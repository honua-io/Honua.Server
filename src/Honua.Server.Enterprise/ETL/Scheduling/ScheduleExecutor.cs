// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Scheduling;

/// <summary>
/// Background service that executes scheduled workflows
/// </summary>
public class ScheduleExecutor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleExecutor> _logger;
    private readonly TimeSpan _checkInterval;

    public ScheduleExecutor(
        IServiceProvider serviceProvider,
        ILogger<ScheduleExecutor> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = TimeSpan.FromMinutes(1); // Check every minute
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Schedule Executor starting...");

        // Wait a bit on startup to let the application fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled workflows");
            }

            // Wait for next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Schedule Executor stopping...");
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var scheduleStore = scope.ServiceProvider.GetRequiredService<IWorkflowScheduleStore>();
        var workflowStore = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

        var now = DateTimeOffset.UtcNow;

        // Get all schedules due for execution
        var dueSchedules = await scheduleStore.GetDueSchedulesAsync(now, cancellationToken);

        if (dueSchedules.Count > 0)
        {
            _logger.LogInformation("Found {Count} schedules due for execution", dueSchedules.Count);
        }

        foreach (var schedule in dueSchedules)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessScheduleAsync(schedule, scheduleStore, workflowStore, workflowEngine, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing schedule {ScheduleId} ({ScheduleName})",
                    schedule.Id, schedule.Name);

                // Update schedule status to error
                try
                {
                    await scheduleStore.UpdateScheduleStatusAsync(schedule.Id, ScheduleStatus.Error, cancellationToken);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Error updating schedule status to error for {ScheduleId}", schedule.Id);
                }
            }
        }
    }

    private async Task ProcessScheduleAsync(
        WorkflowSchedule schedule,
        IWorkflowScheduleStore scheduleStore,
        IWorkflowStore workflowStore,
        IWorkflowEngine workflowEngine,
        CancellationToken cancellationToken)
    {
        // Try to acquire distributed lock
        var lockAcquired = await scheduleStore.AcquireScheduleLockAsync(
            schedule.Id,
            TimeSpan.FromMinutes(5),
            cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogDebug("Could not acquire lock for schedule {ScheduleId}, skipping (likely being processed by another instance)",
                schedule.Id);
            return;
        }

        try
        {
            // Check concurrent execution limit
            if (schedule.MaxConcurrentExecutions > 0)
            {
                var runningCount = await scheduleStore.GetRunningExecutionCountAsync(schedule.Id, cancellationToken);

                if (runningCount >= schedule.MaxConcurrentExecutions)
                {
                    _logger.LogInformation(
                        "Schedule {ScheduleId} ({ScheduleName}) has {RunningCount} executions running (max: {MaxConcurrent}), skipping",
                        schedule.Id, schedule.Name, runningCount, schedule.MaxConcurrentExecutions);

                    // Record skipped execution
                    var skippedExecution = new ScheduleExecution
                    {
                        ScheduleId = schedule.Id,
                        ScheduledAt = schedule.NextRunAt ?? DateTimeOffset.UtcNow,
                        Status = ScheduleExecutionStatus.Skipped,
                        Skipped = true,
                        SkipReason = $"Max concurrent executions ({schedule.MaxConcurrentExecutions}) reached"
                    };

                    await scheduleStore.CreateExecutionAsync(skippedExecution, cancellationToken);

                    // Calculate next run time and update schedule
                    var nextRun = schedule.CalculateNextRun(schedule.NextRunAt);
                    await scheduleStore.UpdateNextRunTimeAsync(schedule.Id, nextRun, cancellationToken);

                    return;
                }
            }

            // Create execution record
            var execution = new ScheduleExecution
            {
                ScheduleId = schedule.Id,
                ScheduledAt = schedule.NextRunAt ?? DateTimeOffset.UtcNow,
                ExecutedAt = DateTimeOffset.UtcNow,
                Status = ScheduleExecutionStatus.Running
            };

            execution = await scheduleStore.CreateExecutionAsync(execution, cancellationToken);

            _logger.LogInformation("Executing scheduled workflow {WorkflowId} from schedule {ScheduleId} ({ScheduleName})",
                schedule.WorkflowId, schedule.Id, schedule.Name);

            // Get workflow definition
            var workflow = await workflowStore.GetWorkflowAsync(schedule.WorkflowId, cancellationToken);

            if (workflow == null)
            {
                _logger.LogError("Workflow {WorkflowId} not found for schedule {ScheduleId}", schedule.WorkflowId, schedule.Id);

                execution.Status = ScheduleExecutionStatus.Failed;
                execution.CompletedAt = DateTimeOffset.UtcNow;
                execution.ErrorMessage = "Workflow not found";

                await scheduleStore.UpdateExecutionAsync(execution, cancellationToken);

                // Update schedule status to error
                await scheduleStore.UpdateScheduleStatusAsync(schedule.Id, ScheduleStatus.Error, cancellationToken);

                return;
            }

            // Execute workflow
            WorkflowRun? run = null;
            try
            {
                var executionOptions = new WorkflowExecutionOptions
                {
                    TenantId = schedule.TenantId,
                    UserId = schedule.CreatedBy, // Use schedule creator as execution user
                    TriggerType = WorkflowTriggerType.Scheduled,
                    ParameterValues = schedule.ParameterValues
                };

                run = await workflowEngine.ExecuteAsync(workflow, executionOptions, cancellationToken);

                // Update execution with workflow run ID
                execution.WorkflowRunId = run.Id;
                execution.Status = run.Status switch
                {
                    WorkflowRunStatus.Completed => ScheduleExecutionStatus.Completed,
                    WorkflowRunStatus.Failed => ScheduleExecutionStatus.Failed,
                    WorkflowRunStatus.Cancelled => ScheduleExecutionStatus.Failed,
                    WorkflowRunStatus.Timeout => ScheduleExecutionStatus.Failed,
                    _ => ScheduleExecutionStatus.Running
                };
                execution.CompletedAt = run.CompletedAt;
                execution.ErrorMessage = run.ErrorMessage;

                await scheduleStore.UpdateExecutionAsync(execution, cancellationToken);

                // Update schedule last run information
                await scheduleStore.UpdateLastRunAsync(
                    schedule.Id,
                    run.Id,
                    run.Status.ToString(),
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                _logger.LogInformation(
                    "Scheduled workflow execution completed: Schedule {ScheduleId}, Run {RunId}, Status {Status}",
                    schedule.Id, run.Id, run.Status);

                // Handle notifications
                await SendNotificationsAsync(schedule, run, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled workflow {WorkflowId} from schedule {ScheduleId}",
                    schedule.WorkflowId, schedule.Id);

                execution.Status = ScheduleExecutionStatus.Failed;
                execution.CompletedAt = DateTimeOffset.UtcNow;
                execution.ErrorMessage = ex.Message;

                await scheduleStore.UpdateExecutionAsync(execution, cancellationToken);

                // Update schedule last run
                if (run != null)
                {
                    await scheduleStore.UpdateLastRunAsync(
                        schedule.Id,
                        run.Id,
                        "failed",
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                }

                // Handle retry logic
                if (schedule.RetryAttempts > 0 && execution.RetryCount < schedule.RetryAttempts)
                {
                    _logger.LogInformation(
                        "Scheduling retry {RetryCount}/{MaxRetries} for schedule {ScheduleId} in {DelayMinutes} minutes",
                        execution.RetryCount + 1, schedule.RetryAttempts, schedule.Id, schedule.RetryDelayMinutes);

                    // Schedule retry
                    var retryTime = DateTimeOffset.UtcNow.AddMinutes(schedule.RetryDelayMinutes);
                    await scheduleStore.UpdateNextRunTimeAsync(schedule.Id, retryTime, cancellationToken);

                    // Create retry execution record
                    var retryExecution = new ScheduleExecution
                    {
                        ScheduleId = schedule.Id,
                        ScheduledAt = retryTime,
                        Status = ScheduleExecutionStatus.Pending,
                        RetryCount = execution.RetryCount + 1
                    };
                    await scheduleStore.CreateExecutionAsync(retryExecution, cancellationToken);

                    return; // Don't calculate next normal run yet
                }

                // Send failure notifications
                await SendNotificationsAsync(schedule, run, cancellationToken);
            }

            // Calculate and update next run time
            var nextRunTime = schedule.CalculateNextRun(schedule.NextRunAt);

            if (nextRunTime.HasValue)
            {
                await scheduleStore.UpdateNextRunTimeAsync(schedule.Id, nextRunTime.Value, cancellationToken);

                _logger.LogInformation(
                    "Next run for schedule {ScheduleId} ({ScheduleName}) scheduled for {NextRun}",
                    schedule.Id, schedule.Name, nextRunTime.Value);
            }
            else
            {
                // No more runs, mark as expired
                await scheduleStore.UpdateScheduleStatusAsync(schedule.Id, ScheduleStatus.Expired, cancellationToken);

                _logger.LogInformation(
                    "Schedule {ScheduleId} ({ScheduleName}) has no more runs, marking as expired",
                    schedule.Id, schedule.Name);
            }
        }
        finally
        {
            // Release the lock
            await scheduleStore.ReleaseScheduleLockAsync(schedule.Id, cancellationToken);
        }
    }

    private async Task SendNotificationsAsync(
        WorkflowSchedule schedule,
        WorkflowRun? run,
        CancellationToken cancellationToken)
    {
        if (schedule.NotificationConfig == null || run == null)
            return;

        var config = schedule.NotificationConfig;
        var shouldNotify = (run.Status == WorkflowRunStatus.Completed && config.NotifyOnSuccess) ||
                          (run.Status == WorkflowRunStatus.Failed && config.NotifyOnFailure);

        if (!shouldNotify)
            return;

        try
        {
            // TODO: Implement actual notification sending
            // This is a placeholder for future notification integration

            _logger.LogInformation(
                "Notification triggered for schedule {ScheduleId}, run {RunId}, status {Status}",
                schedule.Id, run.Id, run.Status);

            // Email notifications
            if (config.EmailAddresses?.Count > 0)
            {
                _logger.LogDebug("Would send email notifications to: {Emails}",
                    string.Join(", ", config.EmailAddresses));
            }

            // Webhook notifications
            if (config.WebhookUrls?.Count > 0)
            {
                _logger.LogDebug("Would call webhooks: {Webhooks}",
                    string.Join(", ", config.WebhookUrls));
            }

            // Slack notifications
            if (!string.IsNullOrEmpty(config.SlackWebhookUrl))
            {
                _logger.LogDebug("Would send Slack notification to: {Url}", config.SlackWebhookUrl);
            }

            // Teams notifications
            if (!string.IsNullOrEmpty(config.TeamsWebhookUrl))
            {
                _logger.LogDebug("Would send Teams notification to: {Url}", config.TeamsWebhookUrl);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications for schedule {ScheduleId}", schedule.Id);
        }
    }
}
