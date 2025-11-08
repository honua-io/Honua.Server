// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Cronos;

namespace Honua.Server.Enterprise.ETL.Scheduling;

/// <summary>
/// Represents a scheduled execution plan for a workflow
/// </summary>
public class WorkflowSchedule
{
    /// <summary>
    /// Unique schedule identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Workflow definition ID to execute
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Tenant ID (for multi-tenant isolation)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Schedule display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Schedule description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Cron expression (e.g., "0 0 * * *" for daily at midnight)
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Timezone for schedule execution (IANA timezone identifier)
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Whether this schedule is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Schedule status
    /// </summary>
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;

    /// <summary>
    /// Next scheduled run time
    /// </summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>
    /// Last actual run time
    /// </summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>
    /// Status of the last run
    /// </summary>
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// ID of the last workflow run
    /// </summary>
    public Guid? LastRunId { get; set; }

    /// <summary>
    /// Parameter values to pass to workflow executions
    /// </summary>
    public Dictionary<string, object>? ParameterValues { get; set; }

    /// <summary>
    /// Maximum number of concurrent executions allowed (0 = no limit)
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 1;

    /// <summary>
    /// Number of retry attempts on failure
    /// </summary>
    public int RetryAttempts { get; set; } = 0;

    /// <summary>
    /// Retry delay in minutes
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Schedule expiration date (null = never expires)
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Notification configuration
    /// </summary>
    public ScheduleNotificationConfig? NotificationConfig { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// When schedule was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When schedule was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// User who created the schedule
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the schedule
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Calculate the next run time based on the cron expression
    /// </summary>
    public DateTimeOffset? CalculateNextRun(DateTimeOffset? from = null)
    {
        try
        {
            var cronExpr = Cronos.CronExpression.Parse(CronExpression);
            var baseTime = from ?? DateTimeOffset.UtcNow;

            // Convert to specified timezone
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            var localTime = TimeZoneInfo.ConvertTime(baseTime, timezone);

            var nextOccurrence = cronExpr.GetNextOccurrence(localTime.DateTime, timezone);

            if (nextOccurrence.HasValue)
            {
                return new DateTimeOffset(nextOccurrence.Value, timezone.GetUtcOffset(nextOccurrence.Value));
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the next N execution times
    /// </summary>
    public List<DateTimeOffset> GetNextExecutions(int count = 5)
    {
        var executions = new List<DateTimeOffset>();
        var currentTime = DateTimeOffset.UtcNow;

        try
        {
            var cronExpr = Cronos.CronExpression.Parse(CronExpression);
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            var localTime = TimeZoneInfo.ConvertTime(currentTime, timezone);

            var current = localTime.DateTime;
            for (int i = 0; i < count; i++)
            {
                var next = cronExpr.GetNextOccurrence(current, timezone);
                if (!next.HasValue)
                    break;

                executions.Add(new DateTimeOffset(next.Value, timezone.GetUtcOffset(next.Value)));
                current = next.Value;
            }
        }
        catch
        {
            // Return empty list on error
        }

        return executions;
    }

    /// <summary>
    /// Validate the cron expression
    /// </summary>
    public bool IsValidCronExpression()
    {
        try
        {
            Cronos.CronExpression.Parse(CronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if the schedule is currently active
    /// </summary>
    public bool IsActive()
    {
        if (!Enabled)
            return false;

        if (Status != ScheduleStatus.Active)
            return false;

        if (ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value)
            return false;

        return true;
    }
}

/// <summary>
/// Schedule status enumeration
/// </summary>
public enum ScheduleStatus
{
    /// <summary>
    /// Schedule is active and will execute
    /// </summary>
    Active,

    /// <summary>
    /// Schedule is paused by user
    /// </summary>
    Paused,

    /// <summary>
    /// Schedule has expired
    /// </summary>
    Expired,

    /// <summary>
    /// Schedule is in error state
    /// </summary>
    Error
}

/// <summary>
/// Notification configuration for scheduled workflows
/// </summary>
public class ScheduleNotificationConfig
{
    /// <summary>
    /// Whether to send notifications on success
    /// </summary>
    public bool NotifyOnSuccess { get; set; } = false;

    /// <summary>
    /// Whether to send notifications on failure
    /// </summary>
    public bool NotifyOnFailure { get; set; } = true;

    /// <summary>
    /// Email addresses to notify
    /// </summary>
    public List<string> EmailAddresses { get; set; } = new();

    /// <summary>
    /// Webhook URLs to call
    /// </summary>
    public List<string> WebhookUrls { get; set; } = new();

    /// <summary>
    /// Slack webhook URL
    /// </summary>
    public string? SlackWebhookUrl { get; set; }

    /// <summary>
    /// Microsoft Teams webhook URL
    /// </summary>
    public string? TeamsWebhookUrl { get; set; }
}

/// <summary>
/// Represents a single execution of a scheduled workflow
/// </summary>
public class ScheduleExecution
{
    /// <summary>
    /// Unique execution identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Schedule ID
    /// </summary>
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid? WorkflowRunId { get; set; }

    /// <summary>
    /// Scheduled execution time
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// Actual execution time
    /// </summary>
    public DateTimeOffset? ExecutedAt { get; set; }

    /// <summary>
    /// Completion time
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Execution status
    /// </summary>
    public ScheduleExecutionStatus Status { get; set; } = ScheduleExecutionStatus.Pending;

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Whether this execution was skipped due to concurrent execution limit
    /// </summary>
    public bool Skipped { get; set; } = false;

    /// <summary>
    /// Reason for skipping (if applicable)
    /// </summary>
    public string? SkipReason { get; set; }
}

/// <summary>
/// Schedule execution status
/// </summary>
public enum ScheduleExecutionStatus
{
    /// <summary>
    /// Execution is pending
    /// </summary>
    Pending,

    /// <summary>
    /// Execution is running
    /// </summary>
    Running,

    /// <summary>
    /// Execution completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Execution failed
    /// </summary>
    Failed,

    /// <summary>
    /// Execution was skipped
    /// </summary>
    Skipped
}
