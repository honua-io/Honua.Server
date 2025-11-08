// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Integration.Tests.GeoETL.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Honua.Server.Integration.Tests.GeoETL;

/// <summary>
/// Integration tests for GDAL format support (GeoPackage, Shapefile, CSV, GPX, GML, etc.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "GeoETL")]
[Trait("Category", "GDAL")]
public class GdalFormatIntegrationTests : GeoEtlIntegrationTestBase
{
    [Fact]
    public async Task GeoPackage_ReadWriteRoundTrip_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10);
        var gpkgPath = GetOutputFilePath("test.gpkg");

        // Create workflow to write to GeoPackage
        var writeWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write to GeoPackage")
            .WithFileSource("source", geojson)
            .WithGeoPackageSink("sink", gpkgPath, "test_layer")
            .AddEdge("source", "sink")
            .Build();

        // Act - Write
        var writeRun = await ExecuteWorkflowAsync(writeWorkflow);

        // Assert - Write
        WorkflowAssertions.AssertWorkflowCompleted(writeRun);
        Assert.True(File.Exists(gpkgPath));

        // Create workflow to read from GeoPackage
        var readWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Read from GeoPackage")
            .WithGeoPackageSource("source", gpkgPath, "test_layer")
            .WithOutputSink("output")
            .AddEdge("source", "output")
            .Build();

        // Act - Read
        var readRun = await ExecuteWorkflowAsync(readWorkflow);

        // Assert - Read
        WorkflowAssertions.AssertWorkflowCompleted(readRun);
        WorkflowAssertions.AssertFeaturesProcessedAtLeast(readRun, 10);
    }

    [Fact]
    public async Task Shapefile_ReadWriteRoundTrip_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(5);
        var shpPath = GetOutputFilePath("test.shp");

        var writeWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write to Shapefile")
            .WithFileSource("source", geojson)
            .WithSink("sink", "shapefile", new Dictionary<string, object> { ["output_path"] = shpPath })
            .AddEdge("source", "sink")
            .Build();

        // Act - Write
        var writeRun = await ExecuteWorkflowAsync(writeWorkflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(writeRun);
    }

    [Fact]
    public async Task CsvWithWkt_ReadWriteRoundTrip_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10);
        var csvPath = GetOutputFilePath("test.csv");

        var writeWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write to CSV with WKT")
            .WithFileSource("source", geojson)
            .WithSink("sink", "csv_geometry", new Dictionary<string, object>
            {
                ["output_path"] = csvPath,
                ["geometry_format"] = "WKT"
            })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var writeRun = await ExecuteWorkflowAsync(writeWorkflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(writeRun);
        Assert.True(File.Exists(csvPath));
    }

    [Fact]
    public async Task CsvWithLatLon_ReadWrite_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10);
        var csvPath = GetOutputFilePath("points.csv");

        var writeWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write to CSV with Lat/Lon")
            .WithFileSource("source", geojson)
            .WithSink("sink", "csv_geometry", new Dictionary<string, object>
            {
                ["output_path"] = csvPath,
                ["geometry_format"] = "LatLon"
            })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var writeRun = await ExecuteWorkflowAsync(writeWorkflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(writeRun);
        Assert.True(File.Exists(csvPath));
    }

    [Fact]
    public async Task Gpx_WriteWaypoints_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(5);
        var gpxPath = GetOutputFilePath("waypoints.gpx");

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write GPX Waypoints")
            .WithFileSource("source", geojson)
            .WithSink("sink", "gpx", new Dictionary<string, object>
            {
                ["output_path"] = gpxPath,
                ["feature_type"] = "waypoints"
            })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.True(File.Exists(gpxPath));
    }

    [Fact]
    public async Task Gpx_WriteTracks_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromLineStrings(3);
        var gpxPath = GetOutputFilePath("tracks.gpx");

        var workflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write GPX Tracks")
            .WithFileSource("source", geojson)
            .WithSink("sink", "gpx", new Dictionary<string, object>
            {
                ["output_path"] = gpxPath,
                ["feature_type"] = "tracks"
            })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var run = await ExecuteWorkflowAsync(workflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run);
        Assert.True(File.Exists(gpxPath));
    }

    [Fact]
    public async Task Gml_ReadWriteRoundTrip_ShouldSucceed()
    {
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPolygons(5);
        var gmlPath = GetOutputFilePath("test.gml");

        var writeWorkflow = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Write to GML")
            .WithFileSource("source", geojson)
            .WithSink("sink", "gml", new Dictionary<string, object>
            {
                ["output_path"] = gmlPath
            })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var writeRun = await ExecuteWorkflowAsync(writeWorkflow);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(writeRun);
        Assert.True(File.Exists(gmlPath));
    }

    [Fact]
    public async Task MultiFormat_Pipeline_ShouldSucceed()
    {
        // Test: GeoJSON -> Buffer -> GeoPackage -> Read -> Shapefile
        // Arrange
        var geojson = FeatureGenerator.CreateGeoJsonFromPoints(10);
        var gpkgPath = GetOutputFilePath("intermediate.gpkg");
        var shpPath = GetOutputFilePath("final.shp");

        // Step 1: GeoJSON -> Buffer -> GeoPackage
        var step1 = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Step 1: Buffer to GeoPackage")
            .WithFileSource("source", geojson)
            .WithBuffer("buffer", 100)
            .WithGeoPackageSink("sink", gpkgPath, "buffered")
            .AddEdge("source", "buffer")
            .AddEdge("buffer", "sink")
            .Build();

        var run1 = await ExecuteWorkflowAsync(step1);
        WorkflowAssertions.AssertWorkflowCompleted(run1);

        // Step 2: GeoPackage -> Shapefile
        var step2 = WorkflowBuilder.Create(TestTenantId, TestUserId)
            .WithName("Step 2: GeoPackage to Shapefile")
            .WithGeoPackageSource("source", gpkgPath, "buffered")
            .WithSink("sink", "shapefile", new Dictionary<string, object> { ["output_path"] = shpPath })
            .AddEdge("source", "sink")
            .Build();

        // Act
        var run2 = await ExecuteWorkflowAsync(step2);

        // Assert
        WorkflowAssertions.AssertWorkflowCompleted(run2);
    }
}
