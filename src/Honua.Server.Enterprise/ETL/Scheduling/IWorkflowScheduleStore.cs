// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.ETL.Scheduling;

/// <summary>
/// Storage interface for workflow schedules and schedule executions
/// </summary>
public interface IWorkflowScheduleStore
{
    // Schedule CRUD operations

    /// <summary>
    /// Create a new workflow schedule
    /// </summary>
    Task<WorkflowSchedule> CreateScheduleAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a schedule by ID
    /// </summary>
    Task<WorkflowSchedule?> GetScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing schedule
    /// </summary>
    Task<WorkflowSchedule> UpdateScheduleAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a schedule
    /// </summary>
    Task DeleteScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all schedules for a tenant
    /// </summary>
    Task<List<WorkflowSchedule>> ListSchedulesAsync(
        Guid tenantId,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List schedules for a specific workflow
    /// </summary>
    Task<List<WorkflowSchedule>> ListSchedulesByWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get schedules that are due for execution
    /// </summary>
    Task<List<WorkflowSchedule>> GetDueSchedulesAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the next run time for a schedule
    /// </summary>
    Task UpdateNextRunTimeAsync(
        Guid scheduleId,
        DateTimeOffset? nextRunAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update schedule status
    /// </summary>
    Task UpdateScheduleStatusAsync(
        Guid scheduleId,
        ScheduleStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record the last run information for a schedule
    /// </summary>
    Task UpdateLastRunAsync(
        Guid scheduleId,
        Guid runId,
        string status,
        DateTimeOffset runAt,
        CancellationToken cancellationToken = default);

    // Schedule Execution operations

    /// <summary>
    /// Create a schedule execution record
    /// </summary>
    Task<ScheduleExecution> CreateExecutionAsync(
        ScheduleExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a schedule execution record
    /// </summary>
    Task<ScheduleExecution> UpdateExecutionAsync(
        ScheduleExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution history for a schedule
    /// </summary>
    Task<List<ScheduleExecution>> GetExecutionHistoryAsync(
        Guid scheduleId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of currently running executions for a schedule
    /// </summary>
    Task<int> GetRunningExecutionCountAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquire a distributed lock for schedule execution
    /// </summary>
    Task<bool> AcquireScheduleLockAsync(
        Guid scheduleId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a distributed lock for schedule execution
    /// </summary>
    Task ReleaseScheduleLockAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);
}
