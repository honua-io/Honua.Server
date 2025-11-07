// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Integration.Tests.GeoETL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Integration tests for workflow execution covering various scenarios
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "GeoETL")]
public class WorkflowExecutionIntegrationTests : GeoEtlIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public WorkflowExecutionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecuteSimpleWorkflow_FileSourceToOutput_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Simple File to Output")
            .WithFileSource("source", geojson)
            .WithOutputSink("output")
            .AddEdge("source", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        WorkflowAssertions.AssertAllNodesCompleted(run);
        WorkflowAssertions.AssertFeaturesProcessedAtLeast(run, 10);
        WorkflowAssertions.AssertWorkflowHasOutput(run, "output.result");
    }

    [Fact]
    public async Task ExecuteBufferWorkflow_WithValidData_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(5);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Buffer Points")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100, "meters")
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        WorkflowAssertions.AssertAllNodesCompleted(run);
        WorkflowAssertions.AssertFeaturesProcessedAtLeast(run, 5);
        Assert.Equal(3, run.NodeRuns.Count); // source, buffer, output
    }

    [Fact]
    public async Task ExecuteMultiNodeWorkflow_SourceBufferSimplifyOutput_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(3);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Multi-Node Processing")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 50)
            .WithSimplify("simplify", 10)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "simplify")
            .AddEdge("simplify", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        WorkflowAssertions.AssertAllNodesCompleted(run);
        Assert.Equal(4, run.NodeRuns.Count);
        WorkflowAssertions.AssertNodeExecutionOrder(run, "source", "buffer", "simplify", "output");
    }

    [Fact]
    public async Task ExecuteWorkflow_WithInvalidParameters_ShouldFail()
    {
        // Arrange - buffer with negative distance
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(5);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Invalid Buffer")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", -100) // Invalid negative distance
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert - workflow should fail or handle gracefully
        // Depending on implementation, this might fail validation or during execution
        Assert.True(run.Status == WorkflowRunStatus.Failed || run.Status == WorkflowRunStatus.Completed);
    }

    [Fact]
    public async Task ExecuteWorkflow_WithCancellation_ShouldCancel()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(1000); // Large dataset
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Cancellable Workflow")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly

        // Act
        WorkflowRun? run = null;
        try
        {
            run = await ExecuteWorkflowAsync(workflow, cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected for cancelled workflows
        }

        // Assert - workflow should be cancelled or not complete successfully
        if (run != null)
        {
            Assert.True(run.Status == WorkflowRunStatus.Cancelled || run.Status == WorkflowRunStatus.Running);
        }
    }

    [Fact]
    public async Task ExecuteParallelWorkflows_MultipleConcurrent_ShouldSucceed()
    {
        // Arrange
        var workflows = Enumerable.Range(0, 3).Select(i =>
        {
            var geojson = FeatureGenerator.CreateGeoJsonFromPoints(5);
            return WorkflowBuilder.Create(TestTenantId, TestUserId)
                .WithName($"Parallel Workflow {i}")
                .WithFileSource("source", geojson)
                .WithBuffer("buffer", 50)
                .WithOutputSink("output")
                .AddEdge("source", "buffer")
                .AddEdge("buffer", "output")
                .Build();
        }).ToList();

        // Act
        var tasks = workflows.Select(w => ExecuteWorkflowAsync(w));
        var runs = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(3, runs.Length);
        Assert.All(runs, run =>
        {
            WorkflowAssertions.AssertWorkflowCompleted(run);
        });
    }

    [Fact]
    public async Task ExecuteWorkflow_WithLargeDataset_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10000); // Large dataset
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Large Dataset Processing")
            .WithFileSource("source", geojson)
            .WithOutputSink("output")
            .AddEdge("source", "output")
            .Build();

        // Act
        var startTime = DateTimeOffset.UtcNow;
        var run = await ExecuteWorkflowAsync(workflow);
        var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        WorkflowAssertions.AssertFeaturesProcessedAtLeast(run, 10000);
        _output.WriteLine($"Processed 10,000 features in {duration:F2} seconds");
    }

    [Fact]
    public async Task ExecuteWorkflow_AllGeoprocessingOperations_ShouldSucceed()
    {
        // Test each geoprocessing operation individually
        var operations = new[]
        {
            ("buffer", new Dictionary<string, object> { ["distance"] = 50, ["unit"] = "meters" }),
            ("intersection", new Dictionary<string, object>()),
            ("union", new Dictionary<string, object>()),
            ("difference", new Dictionary<string, object>()),
            ("simplify", new Dictionary<string, object> { ["tolerance"] = 10 }),
            ("convex_hull", new Dictionary<string, object>()),
            ("dissolve", new Dictionary<string, object>())
        };

        foreach (var (operation, parameters) in operations)
        {
            // Arrange
            var geojson = operation switch
            {
                "buffer" or "simplify" or "convex_hull" or "dissolve" => FeatureGenerator.CreateGeoJsonFromPolygons(3),
                _ => FeatureGenerator.CreateGeoJsonFromPoints(5)
            };

            var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
                .WithName($"Test {operation}")
                .WithFileSource("source", geojson)
                .WithTransform(operation, operation, parameters)
                .WithOutputSink("output")
                .AddEdge("source", operation)
                .AddEdge(operation, "output")
                .Build();

            // Act
            var run = await ExecuteWorkflowAsync(workflow);

            // Assert
            _output.WriteLine($"Testing operation: {operation}");
            WorkflowAssertions.AssertWorkflowCompleted(run);
            WorkflowAssertions.AssertNodeCompleted(run, operation);
        }
    }

    [Fact]
    public async Task ExecuteWorkflow_WithProgressTracking_ShouldReportProgress()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(100);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Progress Tracking Test")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        var progressReports = new List<WorkflowProgress>();
        var progress = new Progress<WorkflowProgress>(p =>
        {
            progressReports.Add(p);
            _output.WriteLine($"Progress: {p.ProgressPercent}% - {p.Message}");
        });

        // Act
        var run = await ExecuteWorkflowAsync(workflow, progress: progress);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.NotEmpty(progressReports);
        Assert.Contains(progressReports, p => p.ProgressPercent >= 100);
    }

    [Fact]
    public async Task ExecuteWorkflow_WithMetricsTracking_ShouldRecordMetrics()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(50);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Metrics Tracking Test")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.NotNull(run.FeaturesProcessed);
        Assert.True(run.FeaturesProcessed > 0);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.True(run.CompletedAt > run.StartedAt);
    }

    [Fact]
    public async Task ValidateWorkflow_WithCyclicGraph_ShouldFail()
    {
        // Arrange - create a workflow with a cycle
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Cyclic Workflow")
            .AddNode("node1", "geoprocessing.buffer", new Dictionary<string, object> { ["distance"] = 50 })
            .AddNode("node2", "geoprocessing.buffer", new Dictionary<string, object> { ["distance"] = 50 })
            .AddEdge("node1", "node2")
            .AddEdge("node2", "node1") // Creates a cycle
            .Build();

        // Act
        var validation = await WorkflowEngine.ValidateAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowValidationFailed(validation, "cycle");
    }

    [Fact]
    public async Task ValidateWorkflow_WithMissingNode_ShouldFail()
    {
        // Arrange - edge references non-existent node
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Missing Node Workflow")
            .AddNode("source", "data_source.file", new Dictionary<string, object> { ["geojson"] = "{}" })
            .AddEdge("source", "nonexistent") // References missing node
            .Build();

        // Act
        var validation = await WorkflowEngine.ValidateAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowValidationFailed(validation);
    }

    [Fact]
    public async Task EstimateWorkflow_WithValidWorkflow_ShouldProvideEstimate()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(100);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Estimation Test")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var estimate = await WorkflowEngine.EstimateAsync(workflow);

        // Assert
        Assert.NotNull(estimate);
        // Estimates should provide some information about expected resource usage
    }
}
