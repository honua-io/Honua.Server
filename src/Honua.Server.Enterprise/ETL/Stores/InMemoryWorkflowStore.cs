// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Stores;

/// <summary>
/// In-memory implementation of workflow store (for development/testing)
/// Production should use PostgreSQL-based store
/// </summary>
public class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly Dictionary<Guid, WorkflowDefinition> _workflows = new();
    private readonly Dictionary<Guid, WorkflowRun> _runs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<WorkflowDefinition> CreateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_workflows.ContainsKey(workflow.Id))
            {
                throw new InvalidOperationException($"Workflow {workflow.Id} already exists");
            }

            _workflows[workflow.Id] = workflow;
            return workflow;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<WorkflowDefinition?> GetWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        _workflows.TryGetValue(workflowId, out var workflow);
        return Task.FromResult(workflow);
    }

    public async Task<WorkflowDefinition> UpdateWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_workflows.ContainsKey(workflow.Id))
            {
                throw new InvalidOperationException($"Workflow {workflow.Id} not found");
            }

            workflow.UpdatedAt = DateTimeOffset.UtcNow;
            _workflows[workflow.Id] = workflow;
            return workflow;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_workflows.TryGetValue(workflowId, out var workflow))
            {
                workflow.IsDeleted = true;
                workflow.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<List<WorkflowDefinition>> ListWorkflowsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var workflows = _workflows.Values
            .Where(w => w.TenantId == tenantId && !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt)
            .ToList();

        return Task.FromResult(workflows);
    }

    public async Task<WorkflowRun> CreateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_runs.ContainsKey(run.Id))
            {
                throw new InvalidOperationException($"Workflow run {run.Id} already exists");
            }

            _runs[run.Id] = run;
            return run;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<WorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public async Task<WorkflowRun> UpdateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_runs.ContainsKey(run.Id))
            {
                throw new InvalidOperationException($"Workflow run {run.Id} not found");
            }

            _runs[run.Id] = run;
            return run;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<List<WorkflowRun>> ListRunsAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var runs = _runs.Values
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return Task.FromResult(runs);
    }

    public Task<List<WorkflowRun>> ListRunsByTenantAsync(Guid tenantId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var runs = _runs.Values
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(runs);
    }
}
