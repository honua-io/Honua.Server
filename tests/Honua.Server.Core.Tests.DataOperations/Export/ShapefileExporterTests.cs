using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Export;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ShapefileExporterTests
{
    [Fact]
    public async Task ExportAsync_ShouldGenerateZippedShapefile()
    {
        // Arrange
        var exporter = new ShapefileExporter();
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().Be(2);
        result.FileName.Should().EndWith(".zip");
        result.Content.Length.Should().BeGreaterThan(0);

        // Verify ZIP structure
        result.Content.Position = 0;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);

        // Verify all required shapefile components exist
        var shpEntry = archive.Entries.Should().ContainSingle(e => e.Name.EndsWith(".shp")).Subject;
        var dbfEntry = archive.Entries.Should().ContainSingle(e => e.Name.EndsWith(".dbf")).Subject;
        var shxEntry = archive.Entries.Should().ContainSingle(e => e.Name.EndsWith(".shx")).Subject;

        // Verify file sizes are non-zero (contains actual data)
        shpEntry.Length.Should().BeGreaterThan(0, "SHP file should contain geometry data");
        dbfEntry.Length.Should().BeGreaterThan(0, "DBF file should contain attribute data");
        shxEntry.Length.Should().BeGreaterThan(0, "SHX file should contain index data");

        // Verify .prj file exists for coordinate system
        archive.Entries.Should().Contain(e => e.Name.EndsWith(".prj"), "PRJ file should define coordinate system");
    }

    [Fact]
    public async Task ExportAsync_ShouldRespectMaxFeaturesLimit()
    {
        // Arrange
        var options = new ShapefileExportOptions { MaxFeatures = 1 };
        var exporter = new ShapefileExporter(options);
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act & Assert
        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
        {
            await exporter.ExportAsync(layer, query, "EPSG:4326", records);
        });
    }

    [Fact]
    public async Task ExportAsync_ShouldHandleEmptyRecords()
    {
        // Arrange
        var exporter = new ShapefileExporter();
        var layer = CreateTestLayer();
        var records = CreateEmptyRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().Be(0);
    }

    [Fact]
    public async Task ExportAsync_ShouldIncludeCorrectGeometryType()
    {
        // Arrange
        var exporter = new ShapefileExporter();
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Content.Position = 0;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);

        // Read .shp file header to verify geometry type
        var shpEntry = archive.Entries.First(e => e.Name.EndsWith(".shp"));
        using var shpStream = shpEntry.Open();
        using var shpReader = new BinaryReader(shpStream);

        // Skip file code (4 bytes) and unused bytes (20 bytes)
        shpReader.ReadBytes(24);

        // Read file length (4 bytes, big-endian)
        var fileLengthBytes = shpReader.ReadBytes(4);
        Array.Reverse(fileLengthBytes);
        var fileLength = BitConverter.ToInt32(fileLengthBytes, 0);
        fileLength.Should().BeGreaterThan(0);

        // Read version (4 bytes, little-endian)
        var version = shpReader.ReadInt32();
        version.Should().Be(1000); // Shapefile version

        // Read shape type (4 bytes, little-endian)
        var shapeType = shpReader.ReadInt32();
        shapeType.Should().Be(1, "Shape type 1 represents Point geometry");
    }

    [Fact]
    public async Task ExportAsync_ShouldIncludeAttributesInDbf()
    {
        // Arrange
        var exporter = new ShapefileExporter();
        var layer = CreateTestLayer();
        var records = CreateTestRecords();
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Content.Position = 0;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);

        // Verify DBF contains attribute data
        var dbfEntry = archive.Entries.First(e => e.Name.EndsWith(".dbf"));
        using var dbfStream = dbfEntry.Open();
        using var dbfReader = new BinaryReader(dbfStream);

        // Read DBF header
        var dbfVersion = dbfReader.ReadByte();
        dbfVersion.Should().BeGreaterThan(0, "DBF should have valid version");

        // Skip date (3 bytes)
        dbfReader.ReadBytes(3);

        // Read record count (4 bytes, little-endian)
        var recordCount = dbfReader.ReadInt32();
        recordCount.Should().Be(2, "Should have 2 feature records");

        // Read header length (2 bytes)
        var headerLength = dbfReader.ReadInt16();
        headerLength.Should().BeGreaterThan(0);

        // Read record length (2 bytes)
        var recordLength = dbfReader.ReadInt16();
        recordLength.Should().BeGreaterThan(0, "Each record should have non-zero length for attributes");
    }

    private static LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            Id = "test-shapefile-layer",
            ServiceId = "test-service",
            Title = "Test Shapefile Layer",
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

    private static async IAsyncEnumerable<FeatureRecord> CreateEmptyRecords([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
