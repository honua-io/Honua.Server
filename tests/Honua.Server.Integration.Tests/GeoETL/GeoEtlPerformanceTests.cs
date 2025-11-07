// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Integration.Tests.GeoETL.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Performance and throughput tests for GeoETL system
/// </summary>
[Trait("Category", "Performance")]
[Trait("Category", "GeoETL")]
public class GeoEtlPerformanceTests : GeoEtlIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public GeoEtlPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task ExecuteWorkflow_WithVariousDatasetSizes_ShouldScaleLinearly(int featureCount)
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(featureCount);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName($"Performance Test - {featureCount} features")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 50)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var run = await ExecuteWorkflowAsync(workflow);
        stopwatch.Stop();

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        var throughput = featureCount / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Features: {featureCount}, Time: {stopwatch.Elapsed.TotalSeconds:F2}s, Throughput: {throughput:F0} features/sec");

        // Performance assertions
        Assert.True(stopwatch.Elapsed.TotalSeconds < 60, $"Processing {featureCount} features took too long: {stopwatch.Elapsed.TotalSeconds}s");
    }

    [Fact]
    public async Task ParallelWorkflowExecution_Throughput_ShouldHandleMultipleWorkflows()
    {
        // Arrange - Create 10 workflows
        var workflows = Enumerable.Range(0, 10).Select(i =>
        {
            var geojson = FeatureGenerator.CreateGeoJsonFromPoints(100);
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
        var stopwatch = Stopwatch.StartNew();
        var tasks = workflows.Select(w => ExecuteWorkflowAsync(w));
        var runs = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.Equal(10, runs.Length);
        Assert.All(runs, run => WorkflowAssertions.AssertWorkflowCompleted(run));

        var totalFeatures = runs.Sum(r => r.FeaturesProcessed ?? 0);
        var throughput = totalFeatures / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Executed 10 workflows in parallel");
        _output.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Total features: {totalFeatures}");
        _output.WriteLine($"Throughput: {throughput:F0} features/sec");
    }

    [Fact]
    public async Task WorkflowValidation_Performance_ShouldBeFast()
    {
        // Arrange
        var workflows = Enumerable.Range(0, 100).Select(i =>
            WorkflowBuilder.CreateBufferWorkflow(TestTenantId, TestUserId)
        ).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var validations = new List<Enterprise.ETL.Models.WorkflowValidationResult>();
        foreach (var workflow in workflows)
        {
            var validation = await WorkflowEngine.ValidateAsync(workflow);
            validations.Add(validation);
        }
        stopwatch.Stop();

        // Assert
        Assert.Equal(100, validations.Count);
        Assert.All(validations, v => Assert.True(v.IsValid));

        var avgValidationTime = stopwatch.Elapsed.TotalMilliseconds / 100;
        _output.WriteLine($"Validated 100 workflows in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"Average validation time: {avgValidationTime:F2}ms");

        Assert.True(avgValidationTime < 100, $"Average validation time too slow: {avgValidationTime}ms");
    }

    [Fact]
    public async Task DatabaseOperations_Performance_ShouldHandleHighLoad()
    {
        // Test database operation performance

        // Arrange - Create workflows
        var workflows = Enumerable.Range(0, 50).Select(i =>
            WorkflowBuilder.Create(TestTenantId, TestUserId)
                .WithName($"DB Test Workflow {i}")
                .WithFileSource("source", "{}")
                .WithOutputSink("output")
                .AddEdge("source", "output")
                .Build()
        ).ToList();

        // Act - Create workflows
        var stopwatch = Stopwatch.StartNew();
        var tasks = workflows.Select(w => WorkflowStore.CreateWorkflowAsync(w, TestUserId));
        var created = await Task.WhenAll(tasks);
        stopwatch.Stop();

        var createTime = stopwatch.Elapsed.TotalMilliseconds;

        // Act - Read workflows
        stopwatch.Restart();
        var readTasks = created.Select(w => WorkflowStore.GetWorkflowAsync(w.Id, TestTenantId));
        var read = await Task.WhenAll(readTasks);
        stopwatch.Stop();

        var readTime = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        Assert.Equal(50, created.Length);
        Assert.Equal(50, read.Length);

        _output.WriteLine($"Created 50 workflows in {createTime:F0}ms ({createTime / 50:F2}ms avg)");
        _output.WriteLine($"Read 50 workflows in {readTime:F0}ms ({readTime / 50:F2}ms avg)");

        Assert.True(createTime / 50 < 100, "Average create time too slow");
        Assert.True(readTime / 50 < 50, "Average read time too slow");
    }

    [Fact]
    public async Task ComplexWorkflow_Performance_ShouldCompleteInReasonableTime()
    {
        // Test a complex multi-step workflow

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(1000);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Complex Performance Test")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer1", 50)
            .WithSimplify("simplify", 5)
            .WithTransform("convex_hull", "convex_hull")
            .WithBuffer("buffer2", 25)
            .WithOutputSink("output")
            .AddEdge("source", "buffer1")
            .AddEdge("buffer1", "simplify")
            .AddEdge("simplify", "convex_hull")
            .AddEdge("convex_hull", "buffer2")
            .AddEdge("buffer2", "output")
            .Build();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var run = await ExecuteWorkflowAsync(workflow);
        stopwatch.Stop();

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);

        _output.WriteLine($"Complex workflow (5 nodes, 1000 features) completed in {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Features processed: {run.FeaturesProcessed}");

        Assert.True(stopwatch.Elapsed.TotalSeconds < 30, $"Complex workflow took too long: {stopwatch.Elapsed.TotalSeconds}s");
    }

    [Fact]
    public async Task MemoryUsage_LargeDataset_ShouldStayWithinLimits()
    {
        // Test memory efficiency with large dataset

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(50000);
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Memory Test - 50k features")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 25)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Act
        var beforeMemory = GC.GetTotalMemory(true);
        var run = await ExecuteWorkflowAsync(workflow);
        var afterMemory = GC.GetTotalMemory(false);

        var memoryUsedMB = (afterMemory - beforeMemory) / (1024.0 * 1024.0);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);

        _output.WriteLine($"Memory used: {memoryUsedMB:F2} MB for 50,000 features");

        // Memory should be reasonable (< 500MB for 50k features)
        Assert.True(memoryUsedMB < 500, $"Memory usage too high: {memoryUsedMB:F2} MB");
    }
}
