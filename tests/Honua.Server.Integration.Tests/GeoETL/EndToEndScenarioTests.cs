// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Integration.Tests.GeoETL.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// End-to-end integration tests for real-world GeoETL scenarios
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "GeoETL")]
[Trait("Category", "E2E")]
public class EndToEndScenarioTests : GeoEtlIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public EndToEndScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Scenario_DataMigration_ShapefileToGeoPackage_ShouldSucceed()
    {
        // Scenario: Import Shapefile → Transform → Export to GeoPackage
        // This simulates a common data migration scenario

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(20);
        var shpPath = GetOutputFilePath("source.shp");
        var gpkgPath = GetOutputFilePath("migrated.gpkg");

        // Step 1: Create source shapefile (using GeoJSON as mock)
        var createSourceWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Create Source Shapefile")
            .WithFileSource("source", geojson)
            .WithSink("sink", "shapefile", new Dictionary<string, object> { ["output_path"] = shpPath })
            .AddEdge("source", "sink")
            .Build();

        await ExecuteWorkflowAsync(createSourceWorkflow);

        // Step 2: Migration workflow - Read Shapefile, Transform, Write to GeoPackage
        var migrationWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Shapefile to GeoPackage Migration")
            .WithSource("shapefile_source", "shapefile", new Dictionary<string, object> { ["file_path"] = shpPath })
            .WithSimplify("simplify", 5) // Simplify geometries
            .WithGeoPackageSink("gpkg_sink", gpkgPath, "migrated_data")
            .AddEdge("shapefile_source", "simplify")
            .AddEdge("simplify", "gpkg_sink")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(migrationWorkflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.True(File.Exists(gpkgPath), "GeoPackage output file should exist");
        _output.WriteLine($"Successfully migrated {run.FeaturesProcessed} features from Shapefile to GeoPackage");
    }

    [Fact]
    public async Task Scenario_BufferAnalysis_MultiStep_ShouldSucceed()
    {
        // Scenario: Load parcels → Buffer → Intersect with zones → Export
        // This simulates a spatial analysis workflow

        // Arrange
        var parcels = FeatureGenerator.CreateGeoJsonFromPolygons(15);
        var zones = FeatureGenerator.CreateGeoJsonFromPolygons(5);

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Buffer and Intersection Analysis")
            .WithDescription("Load parcels, create buffer zones, intersect with zoning boundaries")
            .WithFileSource("parcels", parcels)
            .WithFileSource("zones", zones)
            .WithBuffer("buffer_parcels", 30, "meters")
            .WithIntersection("intersect")
            .WithGeoJsonSink("export", GetOutputFilePath("analysis_result.geojson"))
            .AddEdge("parcels", "buffer_parcels")
            .AddEdge("buffer_parcels", "intersect")
            .AddEdge("zones", "intersect")
            .AddEdge("intersect", "export")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.Equal(5, run.NodeRuns.Count);
        _output.WriteLine($"Analysis complete. Processed {run.FeaturesProcessed} features in {(run.CompletedAt - run.StartedAt)?.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task Scenario_MultiFormatExport_ShouldSucceed()
    {
        // Scenario: Load data once, export to multiple formats
        // This simulates a data distribution workflow

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(50);
        var outputGeoJson = GetOutputFilePath("export.geojson");
        var outputGpkg = GetOutputFilePath("export.gpkg");
        var outputCsv = GetOutputFilePath("export.csv");

        // Create workflow with multiple output sinks
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Multi-Format Export")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 50) // Transform once
            .WithGeoJsonSink("export_geojson", outputGeoJson)
            .WithGeoPackageSink("export_gpkg", outputGpkg, "data")
            .WithSink("export_csv", "csv_geometry", new Dictionary<string, object>
            {
                ["output_path"] = outputCsv,
                ["geometry_format"] = "WKT"
            })
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "export_geojson")
            .AddEdge("buffer", "export_gpkg")
            .AddEdge("buffer", "export_csv")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.True(File.Exists(outputGeoJson), "GeoJSON export should exist");
        Assert.True(File.Exists(outputGpkg), "GeoPackage export should exist");
        Assert.True(File.Exists(outputCsv), "CSV export should exist");
        _output.WriteLine("Successfully exported to 3 different formats");
    }

    [Fact]
    public async Task Scenario_ComplexPipeline_AllNodeTypes_ShouldSucceed()
    {
        // Scenario: Comprehensive workflow using multiple node types
        // This tests the full capability of the GeoETL system

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(10);
        var outputPath = GetOutputFilePath("complex_result.gpkg");

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Complex Multi-Step Pipeline")
            .WithCategory("Analysis")
            .WithTag("complex")
            .WithTag("multi-step")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithSimplify("simplify", 10)
            .WithTransform("convex_hull", "convex_hull")
            .WithGeoPackageSink("output", outputPath, "result")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "simplify")
            .AddEdge("simplify", "convex_hull")
            .AddEdge("convex_hull", "output")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.Equal(5, run.NodeRuns.Count);
        WorkflowAssertions.AssertNodeExecutionOrder(run, "source", "buffer", "simplify", "convex_hull", "output");
        Assert.True(File.Exists(outputPath));
        _output.WriteLine($"Complex pipeline completed in {(run.CompletedAt - run.StartedAt)?.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task Scenario_DataQuality_ValidationPipeline_ShouldSucceed()
    {
        // Scenario: Load data → Validate → Repair → Export
        // This simulates a data quality workflow

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(20);
        var outputPath = GetOutputFilePath("validated.geojson");

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Data Quality Pipeline")
            .WithDescription("Validate and repair geometries")
            .WithFileSource("source", geojson)
            .WithSimplify("repair", 1) // Simplify to repair invalid geometries
            .WithGeoJsonSink("export", outputPath)
            .AddEdge("source", "repair")
            .AddEdge("repair", "export")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.True(File.Exists(outputPath));
        _output.WriteLine($"Validated and repaired {run.FeaturesProcessed} features");
    }

    [Fact]
    public async Task Scenario_LargeScaleProcessing_10kFeatures_ShouldComplete()
    {
        // Scenario: Process large dataset efficiently
        // This tests performance and scalability

        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10000);
        var outputPath = GetOutputFilePath("large_dataset.gpkg");

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Large Scale Processing")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 25)
            .WithGeoPackageSink("export", outputPath, "large_data")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "export")
            .Build();

        // Act
        var startTime = System.DateTimeOffset.UtcNow;
        var run = await ExecuteWorkflowAsync(workflow);
        var duration = (System.DateTimeOffset.UtcNow - startTime).TotalSeconds;

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        WorkflowAssertions.AssertFeaturesProcessedAtLeast(run, 10000);
        Assert.True(File.Exists(outputPath));
        _output.WriteLine($"Processed 10,000 features in {duration:F2} seconds ({10000 / duration:F0} features/sec)");

        // Performance assertion - should process at reasonable speed
        Assert.True(duration < 60, $"Processing took {duration}s, expected < 60s");
    }

    [Fact]
    public async Task Scenario_IncrementalProcessing_MultipleRuns_ShouldSucceed()
    {
        // Scenario: Run the same workflow multiple times with different data
        // This tests workflow reusability

        // Arrange
        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Reusable Buffer Workflow")
            .WithParameter("buffer_distance", "number", 50, required: true)
            .WithFileSource("source", FeatureGenerator.CreateGeoJsonFromPoints(10))
            .WithBuffer("buffer", 50)
            .WithOutputSink("output")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "output")
            .Build();

        // Save workflow
        var savedWorkflow = await WorkflowStore.CreateWorkflowAsync(workflow, TestUserId);

        // Act - Run multiple times
        var runs = new List<Enterprise.ETL.Models.WorkflowRun>();
        for (int i = 0; i < 3; i++)
        {
            var run = await ExecuteWorkflowAsync(savedWorkflow);
            runs.Add(run);
        }

        // Assert
        Assert.Equal(3, runs.Count);
        Assert.All(runs, run => WorkflowAssertions.AssertWorkflowCompleted(run));
        _output.WriteLine($"Successfully executed workflow 3 times");
    }
}
