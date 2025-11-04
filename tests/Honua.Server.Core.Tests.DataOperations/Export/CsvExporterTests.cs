using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Export;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class CsvExporterTests
{
    [Fact]
    public async Task ExportAsync_ShouldGenerateCsvWithHeader()
    {
        // Arrange
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().Be(2);
        result.FileName.Should().EndWith(".csv");

        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("WKT");
        content.Should().Contain("road_id");
        content.Should().Contain("name");
        content.Should().Contain("POINT");
        content.Should().Contain("Main Street");
    }

    [Fact]
    public async Task ExportAsync_ShouldRespectGeoJsonGeometryFormat()
    {
        // Arrange
        var options = new CsvExportOptions { GeometryFormat = "geojson" };
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance, options);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("GeoJSON");
        content.Should().Contain("\"type\"");
        content.Should().Contain("\"coordinates\"");
    }

    [Fact]
    public async Task ExportAsync_ShouldRespectMaxFeaturesLimit()
    {
        // Arrange
        var options = new CsvExportOptions { MaxFeatures = 1 };
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance, options);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act & Assert
        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
        {
            await exporter.ExportAsync(layer, query, records);
        });
    }

    [Fact]
    public async Task ExportAsync_ShouldExcludeGeometryWhenConfigured()
    {
        // Arrange
        var options = new CsvExportOptions { IncludeGeometry = false };
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance, options);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();

        content.Should().NotContain("WKT");
        content.Should().NotContain("POINT");
        content.Should().Contain("road_id");
        content.Should().Contain("name");
    }

    [Fact]
    public async Task ExportAsync_ShouldUseCustomDelimiter()
    {
        // Arrange
        var options = new CsvExportOptions { Delimiter = ";" };
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance, options);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var firstLine = await reader.ReadLineAsync();

        firstLine.Should().Contain(";");
        firstLine.Should().NotContain(",");
    }

    [Fact]
    public async Task ExportAsync_ShouldHandleNullValues()
    {
        // Arrange
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance);
        var layer = CreateTestLayer();
        var records = CreateTestRecordsWithNulls();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().Be(1);

        // Verify null handling in content
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Verify header exists
        lines[0].Should().Contain("road_id");
        lines[0].Should().Contain("name");

        // Verify data line contains road_id but handles null name appropriately
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        lines[1].Should().Contain("1"); // road_id
        // Null values should be represented as empty or specific null representation
        content.Should().Contain("active"); // status field should still be present
    }

    [Fact]
    public async Task ExportAsync_ShouldExportAllFeaturesWithCorrectData()
    {
        // Arrange
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Verify we have header + 2 data rows
        lines.Should().HaveCountGreaterThanOrEqualTo(3);

        // Verify header contains all expected columns
        var header = lines[0];
        header.Should().Contain("road_id");
        header.Should().Contain("name");
        header.Should().Contain("status");

        // Verify first feature data (road_id=1, Main Street, active)
        var firstRow = lines[1];
        firstRow.Should().Contain("1");
        firstRow.Should().Contain("Main Street");
        firstRow.Should().Contain("active");

        // Verify second feature data (road_id=2, Oak Avenue, planned)
        var secondRow = lines[2];
        secondRow.Should().Contain("2");
        secondRow.Should().Contain("Oak Avenue");
        secondRow.Should().Contain("planned");

        // Verify geometry is exported
        content.Should().Contain("POINT");
        content.Should().Contain("-122.5");
        content.Should().Contain("45.5");
    }

    private static LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "status", DataType = "string" }
            }
        };
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateTestRecords([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = "Main Street",
            ["status"] = "active",
            ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-122.5,45.5]}")
        }));

        yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["road_id"] = 2,
            ["name"] = "Oak Avenue",
            ["status"] = "planned",
            ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-122.6,45.6]}")
        }));

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateTestRecordsWithNulls([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = null,
            ["status"] = "active",
            ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-122.5,45.5]}")
        }));

        await Task.CompletedTask;
    }
}
