// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Integration.Tests.GeoETL.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Integration tests for PostgreSQL workflow storage operations
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "GeoETL")]
public class WorkflowStorageIntegrationTests : GeoEtlIntegrationTestBase
{
    [Fact]
    public async Task CreateWorkflow_WithValidData_ShouldPersistToDatabase()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);

        // Act
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Assert
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(TestTenantId, created.TenantId);
        Assert.Equal(TestUserId, created.CreatedBy);
        Assert.NotEqual(default(DateTimeOffset), created.CreatedAt);
        Assert.Equal("Simple Workflow", created.Metadata.Name);
    }

    [Fact]
    public async Task GetWorkflow_WithValidId_ShouldReturnWorkflow()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Act
        var retrieved = await WorkflowStore.GetWorkflowAsync(created.Id, TestTenantId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(created.Metadata.Name, retrieved.Metadata.Name);
        Assert.Equal(created.Nodes.Count, retrieved.Nodes.Count);
        Assert.Equal(created.Edges.Count, retrieved.Edges.Count);
    }

    [Fact]
    public async Task UpdateWorkflow_WithModifiedData_ShouldPersistChanges()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Modify workflow
        created.Metadata.Name = "Updated Workflow";
        created.Metadata.Description = "This workflow was updated";
        created.UpdatedBy = TestUserId;

        // Act
        var updated = await WorkflowStore.UpdateWorkflowAsync(created, TestUserId);

        // Assert
        Assert.Equal("Updated Workflow", updated.Metadata.Name);
        Assert.Equal("This workflow was updated", updated.Metadata.Description);
        Assert.Equal(TestUserId, updated.UpdatedBy);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
    }

    [Fact]
    public async Task DeleteWorkflow_WithValidId_ShouldSoftDelete()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Act
        await WorkflowStore.DeleteWorkflowAsync(created.Id, TestUserId);

        // Assert
        var retrieved = await WorkflowStore.GetWorkflowAsync(created.Id, TestTenantId);
        Assert.Null(retrieved); // Soft deleted workflows should not be returned
    }

    [Fact]
    public async Task ListWorkflows_ForTenant_ShouldReturnOnlyTenantWorkflows()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var workflow1 = WorkflowBuilder.CreateSimple(tenant1Id, TestUserId);
        var workflow2 = WorkflowBuilder.CreateSimple(tenant1Id, TestUserId);
        var workflow3 = WorkflowBuilder.CreateSimple(tenant2Id, TestUserId);

        await WorkflowStore.CreateWorkflowAsync(workflow1, TestUserId);
        await WorkflowStore.CreateWorkflowAsync(workflow2, TestUserId);
        await WorkflowStore.CreateWorkflowAsync(workflow3, TestUserId);

        // Act
        var tenant1Workflows = await WorkflowStore.ListWorkflowsAsync(tenant1Id);

        // Assert
        Assert.Equal(2, tenant1Workflows.Count);
        Assert.All(tenant1Workflows, w => Assert.Equal(tenant1Id, w.TenantId));
    }

    [Fact]
    public async Task CreateWorkflowRun_WithValidData_ShouldPersistRun()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        var run = new WorkflowRun
        {
            WorkflowId = created.Id,
            TenantId = TestTenantId,
            TriggeredBy = TestUserId,
            Status = WorkflowRunStatus.Pending
        };

        // Act
        var createdRun = await WorkflowStore.CreateRunAsync(run, TestUserId);

        // Assert
        Assert.NotEqual(Guid.Empty, createdRun.Id);
        Assert.Equal(created.Id, createdRun.WorkflowId);
        Assert.Equal(TestTenantId, createdRun.TenantId);
        Assert.Equal(WorkflowRunStatus.Pending, createdRun.Status);
    }

    [Fact]
    public async Task UpdateWorkflowRun_WithCompletedStatus_ShouldUpdateMetrics()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        var run = new WorkflowRun
        {
            WorkflowId = created.Id,
            TenantId = TestTenantId,
            TriggeredBy = TestUserId,
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        var createdRun = await WorkflowStore.CreateRunAsync(run, TestUserId);

        // Update to completed
        createdRun.Status = WorkflowRunStatus.Completed;
        createdRun.CompletedAt = DateTimeOffset.UtcNow;
        createdRun.FeaturesProcessed = 100;
        createdRun.BytesRead = 1024;
        createdRun.BytesWritten = 2048;

        // Act
        var updatedRun = await WorkflowStore.UpdateRunAsync(createdRun, TestUserId);

        // Assert
        Assert.Equal(WorkflowRunStatus.Completed, updatedRun.Status);
        Assert.NotNull(updatedRun.CompletedAt);
        Assert.Equal(100, updatedRun.FeaturesProcessed);
        Assert.Equal(1024, updatedRun.BytesRead);
        Assert.Equal(2048, updatedRun.BytesWritten);
    }

    [Fact]
    public async Task ListRunsByWorkflow_WithMultipleRuns_ShouldReturnAllRuns()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Create multiple runs
        for (int i = 0; i < 5; i++)
        {
            var run = new WorkflowRun
            {
                WorkflowId = created.Id,
                TenantId = TestTenantId,
                TriggeredBy = TestUserId,
                Status = i % 2 == 0 ? WorkflowRunStatus.Completed : WorkflowRunStatus.Failed
            };
            await WorkflowStore.CreateRunAsync(run, TestUserId);
        }

        // Act
        var runs = await WorkflowStore.ListRunsAsync(created.Id);

        // Assert
        Assert.Equal(5, runs.Count);
        Assert.All(runs, r => Assert.Equal(created.Id, r.WorkflowId));
    }

    [Fact]
    public async Task ListRunsByTenant_WithMultipleWorkflows_ShouldReturnAllTenantRuns()
    {
        // Arrange
        var workflow1 = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        var workflow2 = WorkflowBuilder.CreateBufferWorkflow(TestTenantId, TestUserId);

        var created1 = await WorkflowStore.CreateWorkflowAsync(workflow1, TestUserId);
        var created2 = await WorkflowStore.CreateWorkflowAsync(workflow2, TestUserId);

        var run1 = new WorkflowRun { WorkflowId = created1.Id, TenantId = TestTenantId, TriggeredBy = TestUserId };
        var run2 = new WorkflowRun { WorkflowId = created2.Id, TenantId = TestTenantId, TriggeredBy = TestUserId };

        await WorkflowStore.CreateRunAsync(run1, TestUserId);
        await WorkflowStore.CreateRunAsync(run2, TestUserId);

        // Act
        var runs = await WorkflowStore.ListRunsByTenantAsync(TestTenantId);

        // Assert
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(TestTenantId, r.TenantId));
    }

    [Fact]
    public async Task ConcurrentWorkflowCreation_ShouldHandleCorrectly()
    {
        // Arrange
        var workflows = Enumerable.Range(0, 10).Select(i =>
            WorkflowBuilder.Create(TestTenantId, TestUserId)
                .WithName($"Concurrent Workflow {i}")
                .WithFileSource("source", "{}")
                .WithOutputSink("output")
                .AddEdge("source", "output")
                .Build()
        ).ToList();

        // Act
        var tasks = workflows.Select(w => WorkflowStore.CreateWorkflowAsync(w, TestUserId));
        var created = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, created.Length);
        Assert.Equal(10, created.Select(w => w.Id).Distinct().Count()); // All unique IDs
    }

    [Fact]
    public async Task GetWorkflow_FromDifferentTenant_ShouldReturnNull()
    {
        // Arrange - tenant isolation test
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var workflow = WorkflowBuilder.CreateSimple(tenant1Id, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Act - try to access from different tenant
        var retrieved = await WorkflowStore.GetWorkflowAsync(created.Id, tenant2Id);

        // Assert
        Assert.Null(retrieved); // Should not return workflow from different tenant
    }

    [Fact]
    public async Task CreateWorkflowRun_WithNodeRuns_ShouldPersistAll()
    {
        // Arrange
        var workflow = WorkflowBuilder.CreateBufferWorkflow(TestTenantId, TestUserId);
        var created = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        var run = new WorkflowRun
        {
            WorkflowId = created.Id,
            TenantId = TestTenantId,
            TriggeredBy = TestUserId,
            Status = WorkflowRunStatus.Running,
            NodeRuns = new System.Collections.Generic.List<NodeRun>
            {
                new NodeRun
                {
                    NodeId = "source",
                    NodeType = "data_source.file",
                    Status = NodeRunStatus.Completed,
                    FeaturesProcessed = 10
                },
                new NodeRun
                {
                    NodeId = "buffer",
                    NodeType = "geoprocessing.buffer",
                    Status = NodeRunStatus.Running
                }
            }
        };

        // Act
        var createdRun = await WorkflowStore.CreateRunAsync(run, TestUserId);

        // Assert
        Assert.Equal(2, createdRun.NodeRuns.Count);
        Assert.Contains(createdRun.NodeRuns, nr => nr.NodeId == "source" && nr.Status == NodeRunStatus.Completed);
        Assert.Contains(createdRun.NodeRuns, nr => nr.NodeId == "buffer" && nr.Status == NodeRunStatus.Running);
    }

    [Fact]
    public async Task QueryWorkflows_ByPublishedStatus_ShouldFilterCorrectly()
    {
        // Arrange
        var workflow1 = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        workflow1.IsPublished = true;

        var workflow2 = WorkflowBuilder.CreateSimple(TestTenantId, TestUserId);
        workflow2.IsPublished = false;

        await WorkflowStore.CreateWorkflowAsync(workflow1, TestUserId);
        await WorkflowStore.CreateWorkflowAsync(workflow2, TestUserId);

        // Act
        var allWorkflows = await WorkflowStore.ListWorkflowsAsync(TestTenantId);
        var publishedWorkflows = allWorkflows.Where(w => w.IsPublished).ToList();

        // Assert
        Assert.Equal(2, allWorkflows.Count);
        Assert.Single(publishedWorkflows);
    }
}
