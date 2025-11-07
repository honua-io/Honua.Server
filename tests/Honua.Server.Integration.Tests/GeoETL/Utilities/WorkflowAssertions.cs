// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.ETL.Models;
using System.Linq;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoETL.Utilities;

/// <summary>
/// Custom assertions for workflow testing
/// </summary>
public static class WorkflowAssertions
{
    public static void AssertWorkflowCompleted(WorkflowRun run)
    {
        Assert.NotNull(run);
        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Null(run.ErrorMessage);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.True(run.CompletedAt > run.StartedAt);
    }

    public static void AssertWorkflowFailed(WorkflowRun run, string? expectedErrorSubstring = null)
    {
        Assert.NotNull(run);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.NotNull(run.ErrorMessage);

        if (expectedErrorSubstring != null)
        {
            Assert.Contains(expectedErrorSubstring, run.ErrorMessage);
        }
    }

    public static void AssertAllNodesCompleted(WorkflowRun run)
    {
        Assert.NotNull(run.NodeRuns);
        Assert.All(run.NodeRuns, nodeRun =>
        {
            Assert.Equal(NodeRunStatus.Completed, nodeRun.Status);
            Assert.Null(nodeRun.ErrorMessage);
        });
    }

    public static void AssertNodeCompleted(WorkflowRun run, string nodeId)
    {
        var nodeRun = run.NodeRuns.FirstOrDefault(n => n.NodeId == nodeId);
        Assert.NotNull(nodeRun);
        Assert.Equal(NodeRunStatus.Completed, nodeRun.Status);
        Assert.Null(nodeRun.ErrorMessage);
    }

    public static void AssertNodeFailed(WorkflowRun run, string nodeId)
    {
        var nodeRun = run.NodeRuns.FirstOrDefault(n => n.NodeId == nodeId);
        Assert.NotNull(nodeRun);
        Assert.Equal(NodeRunStatus.Failed, nodeRun.Status);
        Assert.NotNull(nodeRun.ErrorMessage);
    }

    public static void AssertFeaturesProcessed(WorkflowRun run, long expectedCount)
    {
        Assert.NotNull(run.FeaturesProcessed);
        Assert.Equal(expectedCount, run.FeaturesProcessed);
    }

    public static void AssertFeaturesProcessedAtLeast(WorkflowRun run, long minCount)
    {
        Assert.NotNull(run.FeaturesProcessed);
        Assert.True(run.FeaturesProcessed >= minCount, $"Expected at least {minCount} features, got {run.FeaturesProcessed}");
    }

    public static void AssertWorkflowHasOutput(WorkflowRun run, string key)
    {
        Assert.NotNull(run.State);
        Assert.True(run.State.ContainsKey(key), $"Workflow state missing expected key: {key}");
    }

    public static void AssertExecutionTimeWithin(WorkflowRun run, long maxMilliseconds)
    {
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);

        var duration = (run.CompletedAt.Value - run.StartedAt.Value).TotalMilliseconds;
        Assert.True(duration <= maxMilliseconds, $"Execution took {duration}ms, expected <= {maxMilliseconds}ms");
    }

    public static void AssertNodeExecutionOrder(WorkflowRun run, params string[] expectedOrder)
    {
        Assert.Equal(expectedOrder.Length, run.NodeRuns.Count);

        for (int i = 0; i < expectedOrder.Length; i++)
        {
            var nodeRun = run.NodeRuns.FirstOrDefault(n => n.NodeId == expectedOrder[i]);
            Assert.NotNull(nodeRun);

            if (i > 0)
            {
                var previousNodeRun = run.NodeRuns.FirstOrDefault(n => n.NodeId == expectedOrder[i - 1]);
                Assert.NotNull(previousNodeRun);
                Assert.True(nodeRun.StartedAt >= previousNodeRun.CompletedAt,
                    $"Node {expectedOrder[i]} started before {expectedOrder[i - 1]} completed");
            }
        }
    }

    public static void AssertWorkflowValidationSuccess(WorkflowValidationResult validation)
    {
        Assert.NotNull(validation);
        Assert.True(validation.IsValid, $"Validation failed: {string.Join(", ", validation.Errors)}");
        Assert.Empty(validation.Errors);
    }

    public static void AssertWorkflowValidationFailed(WorkflowValidationResult validation, string? expectedErrorSubstring = null)
    {
        Assert.NotNull(validation);
        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);

        if (expectedErrorSubstring != null)
        {
            Assert.Contains(validation.Errors, e => e.Contains(expectedErrorSubstring));
        }
    }
}
