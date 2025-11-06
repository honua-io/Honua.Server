// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

[Trait("Category", "Unit")]
public sealed class InMemoryWorkflowStoreTests
{
    private readonly InMemoryWorkflowStore _store;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public InMemoryWorkflowStoreTests()
    {
        _store = new InMemoryWorkflowStore();
    }

    #region Workflow CRUD Tests

    [Fact]
    public async Task CreateWorkflowAsync_ValidWorkflow_Succeeds()
    {
        var workflow = CreateTestWorkflow();

        var result = await _store.CreateWorkflowAsync(workflow);

        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.Equal(workflow.Metadata.Name, result.Metadata.Name);
    }

    [Fact]
    public async Task CreateWorkflowAsync_DuplicateId_Throws()
    {
        var workflow = CreateTestWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.CreateWorkflowAsync(workflow));
    }

    [Fact]
    public async Task GetWorkflowAsync_ExistingWorkflow_ReturnsWorkflow()
    {
        var workflow = CreateTestWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var result = await _store.GetWorkflowAsync(workflow.Id);

        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.Equal(workflow.Metadata.Name, result.Metadata.Name);
    }

    [Fact]
    public async Task GetWorkflowAsync_NonExistentWorkflow_ReturnsNull()
    {
        var result = await _store.GetWorkflowAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateWorkflowAsync_ExistingWorkflow_Updates()
    {
        var workflow = CreateTestWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        workflow.Metadata.Name = "Updated Name";
        workflow.Metadata.Description = "Updated Description";

        var result = await _store.UpdateWorkflowAsync(workflow);

        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Metadata.Name);
        Assert.Equal("Updated Description", result.Metadata.Description);
        Assert.True(result.UpdatedAt > workflow.CreatedAt);
    }

    [Fact]
    public async Task UpdateWorkflowAsync_NonExistentWorkflow_Throws()
    {
        var workflow = CreateTestWorkflow();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.UpdateWorkflowAsync(workflow));
    }

    [Fact]
    public async Task DeleteWorkflowAsync_ExistingWorkflow_MarksAsDeleted()
    {
        var workflow = CreateTestWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        await _store.DeleteWorkflowAsync(workflow.Id);

        var result = await _store.GetWorkflowAsync(workflow.Id);
        Assert.NotNull(result);
        Assert.True(result.IsDeleted);
        Assert.True(result.UpdatedAt > workflow.CreatedAt);
    }

    [Fact]
    public async Task DeleteWorkflowAsync_NonExistentWorkflow_DoesNotThrow()
    {
        await _store.DeleteWorkflowAsync(Guid.NewGuid());
        // Should not throw
    }

    [Fact]
    public async Task ListWorkflowsAsync_FiltersByTenant()
    {
        var workflow1 = CreateTestWorkflow();
        workflow1.TenantId = _tenantId;
        await _store.CreateWorkflowAsync(workflow1);

        var workflow2 = CreateTestWorkflow();
        workflow2.TenantId = Guid.NewGuid(); // Different tenant
        await _store.CreateWorkflowAsync(workflow2);

        var results = await _store.ListWorkflowsAsync(_tenantId);

        Assert.Single(results);
        Assert.Equal(workflow1.Id, results[0].Id);
    }

    [Fact]
    public async Task ListWorkflowsAsync_ExcludesDeletedWorkflows()
    {
        var workflow1 = CreateTestWorkflow();
        workflow1.TenantId = _tenantId;
        await _store.CreateWorkflowAsync(workflow1);

        var workflow2 = CreateTestWorkflow();
        workflow2.TenantId = _tenantId;
        await _store.CreateWorkflowAsync(workflow2);

        await _store.DeleteWorkflowAsync(workflow2.Id);

        var results = await _store.ListWorkflowsAsync(_tenantId);

        Assert.Single(results);
        Assert.Equal(workflow1.Id, results[0].Id);
    }

    [Fact]
    public async Task ListWorkflowsAsync_OrdersByCreatedAtDescending()
    {
        var workflow1 = CreateTestWorkflow();
        workflow1.TenantId = _tenantId;
        workflow1.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _store.CreateWorkflowAsync(workflow1);

        var workflow2 = CreateTestWorkflow();
        workflow2.TenantId = _tenantId;
        workflow2.CreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateWorkflowAsync(workflow2);

        var workflow3 = CreateTestWorkflow();
        workflow3.TenantId = _tenantId;
        workflow3.CreatedAt = DateTimeOffset.UtcNow;
        await _store.CreateWorkflowAsync(workflow3);

        var results = await _store.ListWorkflowsAsync(_tenantId);

        Assert.Equal(3, results.Count);
        Assert.Equal(workflow3.Id, results[0].Id);
        Assert.Equal(workflow2.Id, results[1].Id);
        Assert.Equal(workflow1.Id, results[2].Id);
    }

    #endregion

    #region Workflow Run CRUD Tests

    [Fact]
    public async Task CreateRunAsync_ValidRun_Succeeds()
    {
        var run = CreateTestRun();

        var result = await _store.CreateRunAsync(run);

        Assert.NotNull(result);
        Assert.Equal(run.Id, result.Id);
        Assert.Equal(run.WorkflowId, result.WorkflowId);
    }

    [Fact]
    public async Task CreateRunAsync_DuplicateId_Throws()
    {
        var run = CreateTestRun();
        await _store.CreateRunAsync(run);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.CreateRunAsync(run));
    }

    [Fact]
    public async Task GetRunAsync_ExistingRun_ReturnsRun()
    {
        var run = CreateTestRun();
        await _store.CreateRunAsync(run);

        var result = await _store.GetRunAsync(run.Id);

        Assert.NotNull(result);
        Assert.Equal(run.Id, result.Id);
        Assert.Equal(run.Status, result.Status);
    }

    [Fact]
    public async Task GetRunAsync_NonExistentRun_ReturnsNull()
    {
        var result = await _store.GetRunAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateRunAsync_ExistingRun_Updates()
    {
        var run = CreateTestRun();
        await _store.CreateRunAsync(run);

        run.Status = WorkflowRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;

        var result = await _store.UpdateRunAsync(run);

        Assert.NotNull(result);
        Assert.Equal(WorkflowRunStatus.Running, result.Status);
        Assert.NotNull(result.StartedAt);
    }

    [Fact]
    public async Task UpdateRunAsync_NonExistentRun_Throws()
    {
        var run = CreateTestRun();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.UpdateRunAsync(run));
    }

    [Fact]
    public async Task ListRunsAsync_FiltersByWorkflowId()
    {
        var workflowId = Guid.NewGuid();

        var run1 = CreateTestRun();
        run1.WorkflowId = workflowId;
        await _store.CreateRunAsync(run1);

        var run2 = CreateTestRun();
        run2.WorkflowId = Guid.NewGuid(); // Different workflow
        await _store.CreateRunAsync(run2);

        var results = await _store.ListRunsAsync(workflowId);

        Assert.Single(results);
        Assert.Equal(run1.Id, results[0].Id);
    }

    [Fact]
    public async Task ListRunsAsync_OrdersByCreatedAtDescending()
    {
        var workflowId = Guid.NewGuid();

        var run1 = CreateTestRun();
        run1.WorkflowId = workflowId;
        run1.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _store.CreateRunAsync(run1);

        var run2 = CreateTestRun();
        run2.WorkflowId = workflowId;
        run2.CreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateRunAsync(run2);

        var run3 = CreateTestRun();
        run3.WorkflowId = workflowId;
        run3.CreatedAt = DateTimeOffset.UtcNow;
        await _store.CreateRunAsync(run3);

        var results = await _store.ListRunsAsync(workflowId);

        Assert.Equal(3, results.Count);
        Assert.Equal(run3.Id, results[0].Id);
        Assert.Equal(run2.Id, results[1].Id);
        Assert.Equal(run1.Id, results[2].Id);
    }

    [Fact]
    public async Task ListRunsByTenantAsync_FiltersByTenant()
    {
        var run1 = CreateTestRun();
        run1.TenantId = _tenantId;
        await _store.CreateRunAsync(run1);

        var run2 = CreateTestRun();
        run2.TenantId = Guid.NewGuid(); // Different tenant
        await _store.CreateRunAsync(run2);

        var results = await _store.ListRunsByTenantAsync(_tenantId);

        Assert.Single(results);
        Assert.Equal(run1.Id, results[0].Id);
    }

    [Fact]
    public async Task ListRunsByTenantAsync_RespectsLimit()
    {
        var run1 = CreateTestRun();
        run1.TenantId = _tenantId;
        run1.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _store.CreateRunAsync(run1);

        var run2 = CreateTestRun();
        run2.TenantId = _tenantId;
        run2.CreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateRunAsync(run2);

        var run3 = CreateTestRun();
        run3.TenantId = _tenantId;
        run3.CreatedAt = DateTimeOffset.UtcNow;
        await _store.CreateRunAsync(run3);

        var results = await _store.ListRunsByTenantAsync(_tenantId, limit: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(run3.Id, results[0].Id); // Most recent
        Assert.Equal(run2.Id, results[1].Id);
    }

    [Fact]
    public async Task ListRunsByTenantAsync_OrdersByCreatedAtDescending()
    {
        var run1 = CreateTestRun();
        run1.TenantId = _tenantId;
        run1.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _store.CreateRunAsync(run1);

        var run2 = CreateTestRun();
        run2.TenantId = _tenantId;
        run2.CreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await _store.CreateRunAsync(run2);

        var run3 = CreateTestRun();
        run3.TenantId = _tenantId;
        run3.CreatedAt = DateTimeOffset.UtcNow;
        await _store.CreateRunAsync(run3);

        var results = await _store.ListRunsByTenantAsync(_tenantId);

        Assert.Equal(3, results.Count);
        Assert.Equal(run3.Id, results[0].Id);
        Assert.Equal(run2.Id, results[1].Id);
        Assert.Equal(run1.Id, results[2].Id);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentCreates_DoNotCorruptState()
    {
        var workflows = Enumerable.Range(0, 10)
            .Select(_ => CreateTestWorkflow())
            .ToList();

        var tasks = workflows.Select(w => _store.CreateWorkflowAsync(w));
        await Task.WhenAll(tasks);

        var results = await _store.ListWorkflowsAsync(_tenantId);
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task ConcurrentUpdates_DoNotCorruptState()
    {
        var workflow = CreateTestWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var tasks = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var w = new WorkflowDefinition
                {
                    Id = workflow.Id,
                    TenantId = workflow.TenantId,
                    Metadata = new WorkflowMetadata { Name = $"Update {i}" }
                };
                return _store.UpdateWorkflowAsync(w);
            });

        await Task.WhenAll(tasks);

        var result = await _store.GetWorkflowAsync(workflow.Id);
        Assert.NotNull(result);
        Assert.Contains("Update", result.Metadata.Name);
    }

    #endregion

    #region Helper Methods

    private WorkflowDefinition CreateTestWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata
            {
                Name = "Test Workflow",
                Description = "Test workflow for unit tests"
            },
            CreatedBy = _userId
        };
    }

    private WorkflowRun CreateTestRun()
    {
        return new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = WorkflowRunStatus.Pending,
            TriggeredBy = _userId,
            TriggerType = WorkflowTriggerType.Manual
        };
    }

    #endregion
}
