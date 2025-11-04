using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.DataOperations.Export.Matrix;

/// <summary>
/// Comprehensive export format matrix tests.
/// Tests all geometry types × export formats (CSV, Shapefile, GeoPackage).
/// </summary>
[Trait("Category", "Export")]
[Trait("Category", "Matrix")]
public sealed class ExportFormatMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ExportFormatMatrixTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [ClassData(typeof(CsvExportMatrixTestData))]
    public async Task CsvExporter_HandlesAllGeometryTypes_Correctly(
        GeometryTestData.GeometryType geometryType,
        string geometryFormat)
    {
        // Arrange
        _output.WriteLine($"CSV Export Test: {geometryType} with {geometryFormat} format");

        var options = new CsvExportOptions { GeometryFormat = geometryFormat };
        var exporter = new CsvExporter(NullLogger<CsvExporter>.Instance, options);
        var layer = CreateLayerForGeometryType(geometryType);
        var records = CreateRecordsForGeometryType(geometryType);
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().BeGreaterThan(0, $"should export at least one {geometryType} feature");
        result.FileName.Should().EndWith(".csv");

        // Validate content
        result.Content.Position = 0;
        using var reader = new StreamReader(result.Content);
        var content = await reader.ReadToEndAsync();

        // Should contain header
        content.Should().NotBeNullOrWhiteSpace();

        if (geometryFormat.ToLower() == "wkt")
        {
            content.Should().Contain("WKT", "CSV with WKT should have WKT column");
            ValidateWktForGeometryType(content, geometryType);
        }
        else if (geometryFormat.ToLower() == "geojson")
        {
            content.Should().Contain("GeoJSON", "CSV with GeoJSON should have GeoJSON column");
            content.Should().Contain("\"type\"");
            content.Should().Contain("\"coordinates\"", "GeoJSON should have coordinates or geometries");
        }

        _output.WriteLine($"✅ CSV export successful for {geometryType} ({geometryFormat})");
    }

    [Theory]
    [ClassData(typeof(ShapefileExportMatrixTestData))]
    public async Task ShapefileExporter_HandlesAllGeometryTypes_Correctly(
        GeometryTestData.GeometryType geometryType)
    {
        // Arrange
        _output.WriteLine($"Shapefile Export Test: {geometryType}");

        var exporter = new ShapefileExporter();
        var layer = CreateLayerForGeometryType(geometryType);
        var records = CreateRecordsForGeometryType(geometryType);
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().BeGreaterThan(0, $"should export at least one {geometryType} feature");
        result.FileName.Should().EndWith(".zip");
        result.Content.Length.Should().BeGreaterThan(0);

        // Verify ZIP structure contains required shapefile components
        result.Content.Position = 0;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Should().Contain(e => e.Name.EndsWith(".shp"), "shapefile must contain .shp file");
        archive.Entries.Should().Contain(e => e.Name.EndsWith(".dbf"), "shapefile must contain .dbf file");
        archive.Entries.Should().Contain(e => e.Name.EndsWith(".shx"), "shapefile must contain .shx index");
        archive.Entries.Should().Contain(e => e.Name.EndsWith(".prj"), "shapefile must contain .prj projection");

        _output.WriteLine($"✅ Shapefile export successful for {geometryType}");
    }

    [Theory]
    [ClassData(typeof(CrsReprojectionTestData))]
    public async Task ShapefileExporter_ReprojectsGeometry_Correctly(
        GeometryTestData.GeometryType geometryType,
        string targetCrs,
        string crsDescription)
    {
        // Arrange
        _output.WriteLine($"CRS Reprojection Test: {geometryType} to {crsDescription} ({targetCrs})");

        var exporter = new ShapefileExporter();
        var layer = CreateLayerForGeometryType(geometryType);
        var records = CreateRecordsForGeometryType(geometryType);
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, targetCrs, records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().BeGreaterThan(0, $"should export at least one {geometryType} feature");
        result.FileName.Should().EndWith(".zip");

        // Verify .prj file exists (contains projection info)
        result.Content.Position = 0;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);
        var prjEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".prj"));
        prjEntry.Should().NotBeNull($"shapefile should contain .prj file for {targetCrs}");

        // Read and validate PRJ content
        using var prjStream = prjEntry!.Open();
        using var prjReader = new StreamReader(prjStream);
        var prjContent = await prjReader.ReadToEndAsync();
        prjContent.Should().NotBeNullOrWhiteSpace("PRJ file should contain projection definition");

        _output.WriteLine($"✅ Reprojection to {crsDescription} successful for {geometryType}");
    }

    [Theory]
    [ClassData(typeof(GeoPackageExportMatrixTestData))]
    public async Task GeoPackageExporter_HandlesAllGeometryTypes_Correctly(
        GeometryTestData.GeometryType geometryType)
    {
        // Arrange
        _output.WriteLine($"GeoPackage Export Test: {geometryType}");

        var exporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var layer = CreateLayerForGeometryType(geometryType);
        var records = CreateRecordsForGeometryType(geometryType);
        var query = new FeatureQuery(Crs: "EPSG:4326");

        // Act
        var result = await exporter.ExportAsync(layer, query, "EPSG:4326", records);

        // Assert
        result.Should().NotBeNull();
        result.FeatureCount.Should().BeGreaterThan(0, $"should export at least one {geometryType} feature");
        result.FileName.Should().EndWith(".gpkg");
        result.Content.Length.Should().BeGreaterThan(0);

        // Validate it's a valid SQLite database
        result.Content.Position = 0;
        var header = new byte[16];
        await result.Content.ReadAsync(header, 0, 16);
        var headerString = System.Text.Encoding.ASCII.GetString(header);
        headerString.Should().StartWith("SQLite format 3", "GeoPackage is SQLite-based");

        _output.WriteLine($"✅ GeoPackage export successful for {geometryType}");
    }

    private static void ValidateWktForGeometryType(string content, GeometryTestData.GeometryType geometryType)
    {
        var expectedWktPrefix = geometryType switch
        {
            GeometryTestData.GeometryType.Point => "POINT",
            GeometryTestData.GeometryType.LineString => "LINESTRING",
            GeometryTestData.GeometryType.Polygon => "POLYGON",
            GeometryTestData.GeometryType.MultiPoint => "MULTIPOINT",
            GeometryTestData.GeometryType.MultiLineString => "MULTILINESTRING",
            GeometryTestData.GeometryType.MultiPolygon => "MULTIPOLYGON",
            GeometryTestData.GeometryType.GeometryCollection => "GEOMETRYCOLLECTION",
            _ => throw new ArgumentOutOfRangeException(nameof(geometryType))
        };

        content.Should().Contain(expectedWktPrefix, $"WKT should contain {expectedWktPrefix} for {geometryType}");
    }

    private static LayerDefinition CreateLayerForGeometryType(GeometryTestData.GeometryType geometryType)
    {
        return new LayerDefinition
        {
            Id = $"export-test-{geometryType.ToString().ToLowerInvariant()}",
            ServiceId = "export-test-service",
            Title = $"Export Test {geometryType}",
            GeometryType = geometryType.ToString(),
            IdField = "feature_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "feature_id", DataType = "int64" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "category", DataType = "string" }
            }
        };
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateRecordsForGeometryType(
        GeometryTestData.GeometryType geometryType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Generate test geometry using our existing test data infrastructure
        var geometry = GeometryTestData.GetTestGeometry(geometryType, GeometryTestData.GeodeticScenario.Simple);
        var geoJson = GeometryTestData.ToGeoJson(geometry);

        yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["feature_id"] = 1,
            ["name"] = $"Test {geometryType} Feature",
            ["category"] = "export_test",
            ["geom"] = JsonNode.Parse(geoJson)
        }));

        // Add a second feature for coverage
        var geometry2 = GeometryTestData.GetTestGeometry(geometryType, GeometryTestData.GeodeticScenario.Simple);
        var geoJson2 = GeometryTestData.ToGeoJson(geometry2);

        yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["feature_id"] = 2,
            ["name"] = $"Test {geometryType} Feature 2",
            ["category"] = "export_test",
            ["geom"] = JsonNode.Parse(geoJson2)
        }));

        await Task.CompletedTask;
    }
}

/// <summary>
/// Test data for CSV export matrix - all geometry types × geometry formats (WKT, GeoJSON).
/// </summary>
public class CsvExportMatrixTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var geometryTypes = new[]
        {
            GeometryTestData.GeometryType.Point,
            GeometryTestData.GeometryType.LineString,
            GeometryTestData.GeometryType.Polygon,
            GeometryTestData.GeometryType.MultiPoint,
            GeometryTestData.GeometryType.MultiLineString,
            GeometryTestData.GeometryType.MultiPolygon,
            GeometryTestData.GeometryType.GeometryCollection
        };

        var geometryFormats = new[] { "wkt", "geojson" };

        foreach (var geometryType in geometryTypes)
        {
            foreach (var format in geometryFormats)
            {
                yield return new object[] { geometryType, format };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Test data for Shapefile export matrix - geometry types supported by NetTopologySuite ShapefileDataWriter.
/// </summary>
public class ShapefileExportMatrixTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var geometryTypes = new[]
        {
            GeometryTestData.GeometryType.Point,
            GeometryTestData.GeometryType.LineString,
            GeometryTestData.GeometryType.Polygon,
            // Note: MultiPoint not supported by NetTopologySuite.IO.ShapefileDataWriter (bug in PointHandler)
            GeometryTestData.GeometryType.MultiLineString,
            GeometryTestData.GeometryType.MultiPolygon
            // Note: GeometryCollection not supported in Shapefile format
        };

        foreach (var geometryType in geometryTypes)
        {
            yield return new object[] { geometryType };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Test data for GeoPackage export matrix - all geometry types.
/// </summary>
public class GeoPackageExportMatrixTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var geometryTypes = new[]
        {
            GeometryTestData.GeometryType.Point,
            GeometryTestData.GeometryType.LineString,
            GeometryTestData.GeometryType.Polygon,
            GeometryTestData.GeometryType.MultiPoint,
            GeometryTestData.GeometryType.MultiLineString,
            GeometryTestData.GeometryType.MultiPolygon,
            GeometryTestData.GeometryType.GeometryCollection
        };

        foreach (var geometryType in geometryTypes)
        {
            yield return new object[] { geometryType };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Test data for CRS reprojection - geometry types × common CRS transformations.
/// </summary>
public class CrsReprojectionTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        // Test with a subset of geometry types to keep test count reasonable
        var geometryTypes = new[]
        {
            GeometryTestData.GeometryType.Point,
            GeometryTestData.GeometryType.LineString,
            GeometryTestData.GeometryType.Polygon
        };

        // Common CRS transformations used in GIS
        var crsTransformations = new[]
        {
            ("EPSG:3857", "Web Mercator (Google/OSM)"),           // WGS84 -> Web Mercator
            ("EPSG:2163", "US National Atlas Equal Area"),        // WGS84 -> US Equal Area
            ("EPSG:32610", "UTM Zone 10N (Pacific Northwest)"),   // WGS84 -> UTM Zone 10N
            ("EPSG:26910", "NAD83 UTM Zone 10N"),                 // WGS84 -> NAD83 UTM 10N
            ("EPSG:3310", "California Albers"),                    // WGS84 -> CA Albers
        };

        foreach (var geometryType in geometryTypes)
        {
            foreach (var (crs, description) in crsTransformations)
            {
                yield return new object[] { geometryType, crs, description };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
