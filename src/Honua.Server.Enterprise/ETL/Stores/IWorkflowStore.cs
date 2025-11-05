// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Stores;

/// <summary>
/// Storage interface for workflows and workflow runs
/// </summary>
public interface IWorkflowStore
{
    // Workflow Definition operations
    Task<WorkflowDefinition> CreateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> GetWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition> UpdateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    Task DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<List<WorkflowDefinition>> ListWorkflowsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // Workflow Run operations
    Task<WorkflowRun> CreateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default);
    Task<WorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<WorkflowRun> UpdateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default);
    Task<List<WorkflowRun>> ListRunsAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<List<WorkflowRun>> ListRunsByTenantAsync(Guid tenantId, int limit = 100, CancellationToken cancellationToken = default);
}
