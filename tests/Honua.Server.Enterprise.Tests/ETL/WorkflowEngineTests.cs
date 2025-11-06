// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

[Trait("Category", "Unit")]
public sealed class WorkflowEngineTests : IAsyncDisposable
{
    private readonly InMemoryWorkflowStore _store;
    private readonly WorkflowNodeRegistry _registry;
    private readonly WorkflowEngine _engine;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public WorkflowEngineTests()
    {
        _store = new InMemoryWorkflowStore();
        _registry = new WorkflowNodeRegistry();

        // Register test nodes
        _registry.RegisterNode("test.source", new TestSourceNode());
        _registry.RegisterNode("test.transform", new TestTransformNode());
        _registry.RegisterNode("test.sink", new TestSinkNode());
        _registry.RegisterNode("test.fail", new TestFailNode());

        _engine = new WorkflowEngine(_store, _registry, NullLogger<WorkflowEngine>.Instance);
    }

    #region DAG Validation Tests

    [Fact]
    public async Task ValidateAsync_ValidLinearWorkflow_Succeeds()
    {
        var workflow = CreateLinearWorkflow();

        var result = await _engine.ValidateAsync(workflow);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.DagValidation);
        Assert.True(result.DagValidation.IsValid);
        Assert.Equal(3, result.DagValidation.ExecutionOrder!.Count);
    }

    [Fact]
    public async Task ValidateAsync_WorkflowWithCycle_Fails()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Cyclic Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" },
                new() { Id = "node2", Type = "test.transform" },
                new() { Id = "node3", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "node1", To = "node2" },
                new() { From = "node2", To = "node3" },
                new() { From = "node3", To = "node1" } // Creates cycle
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("cycle", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.DagValidation);
        Assert.False(result.DagValidation.IsValid);
        Assert.NotNull(result.DagValidation.Cycles);
        Assert.NotEmpty(result.DagValidation.Cycles);
    }

    [Fact]
    public async Task ValidateAsync_WorkflowWithMissingNodeReference_Fails()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Missing Node Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" },
                new() { Id = "node2", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "node1", To = "nonexistent" } // References missing node
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Missing node references", result.Errors[0]);
        Assert.NotNull(result.DagValidation);
        Assert.False(result.DagValidation.IsValid);
        Assert.NotNull(result.DagValidation.MissingNodes);
        Assert.Contains("nonexistent", result.DagValidation.MissingNodes);
    }

    [Fact]
    public async Task ValidateAsync_DisconnectedNodes_ProducesWarning()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Disconnected Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" },
                new() { Id = "node2", Type = "test.sink" },
                new() { Id = "isolated", Type = "test.transform" } // Not connected
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "node1", To = "node2" }
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.True(result.IsValid); // Valid, just a warning
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Disconnected nodes", result.Warnings[0]);
        Assert.NotNull(result.DagValidation);
        Assert.NotNull(result.DagValidation.DisconnectedNodes);
        Assert.Contains("isolated", result.DagValidation.DisconnectedNodes);
    }

    [Fact]
    public async Task ValidateAsync_ComplexDAG_CorrectTopologicalSort()
    {
        // Create a diamond-shaped DAG:
        //     node1
        //    /     \
        // node2   node3
        //    \     /
        //     node4
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Diamond DAG" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" },
                new() { Id = "node2", Type = "test.transform" },
                new() { Id = "node3", Type = "test.transform" },
                new() { Id = "node4", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "node1", To = "node2" },
                new() { From = "node1", To = "node3" },
                new() { From = "node2", To = "node4" },
                new() { From = "node3", To = "node4" }
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.DagValidation?.ExecutionOrder);

        var order = result.DagValidation.ExecutionOrder;
        Assert.Equal(4, order.Count);
        Assert.Equal("node1", order[0]); // Source must be first
        Assert.Equal("node4", order[3]); // Sink must be last

        // node2 and node3 can be in any order, but must be after node1 and before node4
        var node2Index = order.IndexOf("node2");
        var node3Index = order.IndexOf("node3");
        Assert.True(node2Index > 0 && node2Index < 3);
        Assert.True(node3Index > 0 && node3Index < 3);
    }

    [Fact]
    public async Task ValidateAsync_UnknownNodeType_Fails()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Unknown Node Type" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "unknown.type" }
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Unknown node type", result.Errors[0]);
    }

    [Fact]
    public async Task ValidateAsync_RequiredParameterMissing_Fails()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Missing Required Parameter" },
            Parameters = new Dictionary<string, WorkflowParameter>
            {
                ["requiredParam"] = new() { Name = "requiredParam", Required = true }
            },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" }
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Required parameter 'requiredParam' is missing", result.Errors[0]);
    }

    [Fact]
    public async Task ValidateAsync_NodeValidationFails_IncludedInResult()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Node Validation Fail" },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "node1",
                    Type = "test.source",
                    Parameters = new Dictionary<string, object>
                    {
                        ["shouldFail"] = true
                    }
                }
            }
        };

        var result = await _engine.ValidateAsync(workflow);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.NodeErrors);
        Assert.True(result.NodeErrors.ContainsKey("node1"));
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_ValidWorkflow_Succeeds()
    {
        var workflow = CreateLinearWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId,
            TriggerType = WorkflowTriggerType.Manual
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.Equal(3, run.NodeRuns.Count);
        Assert.All(run.NodeRuns, nr => Assert.Equal(NodeRunStatus.Completed, nr.Status));
    }

    [Fact]
    public async Task ExecuteAsync_NodeFails_WorkflowFails()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Fail Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "source", Type = "test.source" },
                new() { Id = "fail", Type = "test.fail" },
                new() { Id = "sink", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "fail" },
                new() { From = "fail", To = "sink" }
            }
        };
        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.NotNull(run.ErrorMessage);
        Assert.Contains("Intentional failure", run.ErrorMessage);

        // Source should succeed, fail node should fail, sink should not execute
        Assert.Equal(2, run.NodeRuns.Count);
        Assert.Equal(NodeRunStatus.Completed, run.NodeRuns[0].Status);
        Assert.Equal(NodeRunStatus.Failed, run.NodeRuns[1].Status);
    }

    [Fact]
    public async Task ExecuteAsync_ContinueOnError_ExecutesRemainingNodes()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Continue On Error" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "source", Type = "test.source" },
                new() { Id = "fail", Type = "test.fail" },
                new() { Id = "sink", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "fail" },
                new() { From = "fail", To = "sink" }
            }
        };
        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId,
            ContinueOnError = true
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Equal(3, run.NodeRuns.Count);
        Assert.Equal(NodeRunStatus.Completed, run.NodeRuns[0].Status);
        Assert.Equal(NodeRunStatus.Failed, run.NodeRuns[1].Status);
        Assert.Equal(NodeRunStatus.Completed, run.NodeRuns[2].Status);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_StopsExecution()
    {
        var workflow = CreateLinearWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options, cts.Token);

        Assert.Equal(WorkflowRunStatus.Cancelled, run.Status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidWorkflow_FailsValidation()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Invalid Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "unknown.type" }
            }
        };
        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Contains("validation failed", run.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_TracksProgressCorrectly()
    {
        var workflow = CreateLinearWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var progressReports = new List<WorkflowProgress>();
        var progress = new Progress<WorkflowProgress>(p => progressReports.Add(p));

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId,
            ProgressCallback = progress
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.NotEmpty(progressReports);

        // Should have progress reports for each node
        Assert.True(progressReports.Count >= 3);

        // Final progress should be 100%
        var lastProgress = progressReports.Last();
        Assert.Equal(100, lastProgress.ProgressPercent);
    }

    [Fact]
    public async Task ExecuteAsync_NodeOutputPassedToDownstreamNodes()
    {
        var workflow = CreateLinearWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);

        // Each node should have output
        Assert.All(run.NodeRuns, nr => Assert.NotNull(nr.Output));
    }

    #endregion

    #region Estimate Tests

    [Fact]
    public async Task EstimateAsync_ValidWorkflow_ReturnsEstimate()
    {
        var workflow = CreateLinearWorkflow();

        var estimate = await _engine.EstimateAsync(workflow);

        Assert.NotNull(estimate);
        Assert.True(estimate.TotalDurationSeconds > 0);
        Assert.True(estimate.PeakMemoryMB > 0);
        Assert.Equal(3, estimate.NodeEstimates.Count);
        Assert.NotNull(estimate.CriticalPath);
        Assert.Equal(3, estimate.CriticalPath.Count);
    }

    [Fact]
    public async Task EstimateAsync_InvalidDAG_ReturnsEmptyEstimate()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata { Name = "Invalid DAG" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "node1", Type = "test.source" },
                new() { Id = "node2", Type = "test.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "node1", To = "node2" },
                new() { From = "node2", To = "node1" } // Cycle
            }
        };

        var estimate = await _engine.EstimateAsync(workflow);

        Assert.NotNull(estimate);
        Assert.Equal(0, estimate.TotalDurationSeconds);
        Assert.Empty(estimate.NodeEstimates);
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task CancelAsync_RunningWorkflow_Cancels()
    {
        var workflow = CreateLinearWorkflow();
        await _store.CreateWorkflowAsync(workflow);

        var runId = Guid.NewGuid();
        var run = new WorkflowRun
        {
            Id = runId,
            WorkflowId = workflow.Id,
            TenantId = _tenantId,
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        await _store.CreateRunAsync(run);

        await _engine.CancelAsync(runId);

        var updatedRun = await _store.GetRunAsync(runId);
        Assert.NotNull(updatedRun);
        Assert.Equal(WorkflowRunStatus.Cancelled, updatedRun.Status);
        Assert.NotNull(updatedRun.CompletedAt);
    }

    [Fact]
    public async Task CancelAsync_NonExistentRun_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.CancelAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelAsync_CompletedRun_DoesNothing()
    {
        var runId = Guid.NewGuid();
        var run = new WorkflowRun
        {
            Id = runId,
            WorkflowId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = WorkflowRunStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };
        await _store.CreateRunAsync(run);

        await _engine.CancelAsync(runId);

        var updatedRun = await _store.GetRunAsync(runId);
        Assert.NotNull(updatedRun);
        Assert.Equal(WorkflowRunStatus.Completed, updatedRun.Status); // Should remain completed
    }

    #endregion

    #region Status Tests

    [Fact]
    public async Task GetRunStatusAsync_ExistingRun_ReturnsRun()
    {
        var run = new WorkflowRun
        {
            WorkflowId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = WorkflowRunStatus.Running
        };
        await _store.CreateRunAsync(run);

        var result = await _engine.GetRunStatusAsync(run.Id);

        Assert.NotNull(result);
        Assert.Equal(run.Id, result.Id);
        Assert.Equal(WorkflowRunStatus.Running, result.Status);
    }

    [Fact]
    public async Task GetRunStatusAsync_NonExistentRun_ReturnsNull()
    {
        var result = await _engine.GetRunStatusAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

    private WorkflowDefinition CreateLinearWorkflow()
    {
        return new WorkflowDefinition
        {
            TenantId = _tenantId,
            Metadata = new WorkflowMetadata
            {
                Name = "Linear Test Workflow",
                Description = "Simple linear workflow for testing"
            },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "source", Type = "test.source", Name = "Test Source" },
                new() { Id = "transform", Type = "test.transform", Name = "Test Transform" },
                new() { Id = "sink", Type = "test.sink", Name = "Test Sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "transform" },
                new() { From = "transform", To = "sink" }
            }
        };
    }

    #endregion

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

#region Test Node Implementations

internal class TestSourceNode : WorkflowNodeBase
{
    public TestSourceNode() : base(NullLogger<TestSourceNode>.Instance) { }

    public override string NodeType => "test.source";
    public override string DisplayName => "Test Source";
    public override string Description => "Test data source node";

    public override Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        if (nodeDefinition.Parameters.TryGetValue("shouldFail", out var shouldFail) &&
            shouldFail is bool fail && fail)
        {
            return Task.FromResult(NodeValidationResult.Failure("Validation failed as requested"));
        }

        return Task.FromResult(NodeValidationResult.Success());
    }

    public override Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NodeExecutionResult
        {
            Success = true,
            Data = new Dictionary<string, object>
            {
                ["features"] = new List<object> { new { id = 1, name = "Test" } },
                ["count"] = 1
            },
            FeaturesProcessed = 1
        });
    }
}

internal class TestTransformNode : WorkflowNodeBase
{
    public TestTransformNode() : base(NullLogger<TestTransformNode>.Instance) { }

    public override string NodeType => "test.transform";
    public override string DisplayName => "Test Transform";
    public override string Description => "Test transformation node";

    public override Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var inputCount = context.Inputs.Values.Sum(i =>
            i.Data.TryGetValue("count", out var c) ? Convert.ToInt32(c) : 0);

        return Task.FromResult(new NodeExecutionResult
        {
            Success = true,
            Data = new Dictionary<string, object>
            {
                ["features"] = new List<object>(),
                ["count"] = inputCount,
                ["transformed"] = true
            },
            FeaturesProcessed = inputCount
        });
    }
}

internal class TestSinkNode : WorkflowNodeBase
{
    public TestSinkNode() : base(NullLogger<TestSinkNode>.Instance) { }

    public override string NodeType => "test.sink";
    public override string DisplayName => "Test Sink";
    public override string Description => "Test data sink node";

    public override Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var inputCount = context.Inputs.Values.Sum(i =>
            i.Data.TryGetValue("count", out var c) ? Convert.ToInt32(c) : 0);

        return Task.FromResult(new NodeExecutionResult
        {
            Success = true,
            Data = new Dictionary<string, object>
            {
                ["written"] = inputCount
            },
            FeaturesProcessed = inputCount
        });
    }
}

internal class TestFailNode : WorkflowNodeBase
{
    public TestFailNode() : base(NullLogger<TestFailNode>.Instance) { }

    public override string NodeType => "test.fail";
    public override string DisplayName => "Test Fail";
    public override string Description => "Test node that always fails";

    public override Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NodeExecutionResult.Fail("Intentional failure for testing"));
    }
}

#endregion
