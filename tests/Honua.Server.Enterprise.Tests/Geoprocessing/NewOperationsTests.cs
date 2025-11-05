using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Operations;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

/// <summary>
/// Tests for newly implemented geoprocessing operations
/// </summary>
public class NewOperationsTests
{
    [Fact]
    public async Task IntersectionOperation_OverlappingPolygons_ShouldReturnIntersection()
    {
        // Arrange
        var operation = new IntersectionOperation();
        var parameters = new Dictionary<string, object>();
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input1",
                Type = "wkt",
                Source = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"
            },
            new()
            {
                Name = "input2",
                Type = "wkt",
                Source = "POLYGON((5 5, 15 5, 15 15, 5 15, 5 5))"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("geojson");
        result.Data.Should().ContainKey("count");
        result.FeaturesProcessed.Should().BeGreaterThan(0);

        // The intersection should be a 5x5 square
        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry.Should().NotBeNull();
        geometry!.Area.Should().BeApproximately(25.0, 0.1);
    }

    [Fact]
    public async Task IntersectionOperation_NonOverlappingPolygons_ShouldReturnEmpty()
    {
        // Arrange
        var operation = new IntersectionOperation();
        var parameters = new Dictionary<string, object>();
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input1",
                Type = "wkt",
                Source = "POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))"
            },
            new()
            {
                Name = "input2",
                Type = "wkt",
                Source = "POLYGON((10 10, 15 10, 15 15, 10 15, 10 10))"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("count");
        result.Data["count"].Should().Be(0); // No intersection
    }

    [Fact]
    public async Task UnionOperation_MultiplePolygons_ShouldMerge()
    {
        // Arrange
        var operation = new UnionOperation();
        var parameters = new Dictionary<string, object>();
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input1",
                Type = "wkt",
                Source = "POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))"
            },
            new()
            {
                Name = "input2",
                Type = "wkt",
                Source = "POLYGON((3 3, 8 3, 8 8, 3 8, 3 3))"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("geojson");
        result.Data.Should().ContainKey("input_count");
        result.Data["input_count"].Should().Be(2);

        // The union should be larger than either individual polygon
        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry.Should().NotBeNull();
        geometry!.Area.Should().BeGreaterThan(25.0);
    }

    [Fact]
    public async Task DifferenceOperation_SubtractPolygon_ShouldRemoveOverlap()
    {
        // Arrange
        var operation = new DifferenceOperation();
        var parameters = new Dictionary<string, object>();
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "wkt",
                Source = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"
            },
            new()
            {
                Name = "subtract",
                Type = "wkt",
                Source = "POLYGON((5 5, 15 5, 15 15, 5 15, 5 5))"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("geojson");
        result.Data.Should().ContainKey("count");

        // The result should be smaller than the original
        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry.Should().NotBeNull();
        geometry!.Area.Should().BeLessThan(100.0);
        geometry.Area.Should().BeApproximately(75.0, 0.1); // 100 - 25 (overlap)
    }

    [Fact]
    public async Task SimplifyOperation_ComplexLineString_ShouldReduceVertices()
    {
        // Arrange
        var operation = new SimplifyOperation();
        var parameters = new Dictionary<string, object>
        {
            ["tolerance"] = 1.0,
            ["preserve_topology"] = true
        };
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "wkt",
                Source = "LINESTRING(0 0, 1 0.1, 2 0, 3 0.1, 4 0, 5 0.1, 6 0)"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("original_vertices");
        result.Data.Should().ContainKey("simplified_vertices");
        result.Data.Should().ContainKey("reduction_percent");

        var originalVertices = Convert.ToInt32(result.Data["original_vertices"]);
        var simplifiedVertices = Convert.ToInt32(result.Data["simplified_vertices"]);

        simplifiedVertices.Should().BeLessThan(originalVertices);
        result.Data["reduction_percent"].Should().NotBe(0);
    }

    [Fact]
    public async Task SimplifyOperation_InvalidTolerance_ShouldFail()
    {
        // Arrange
        var operation = new SimplifyOperation();
        var parameters = new Dictionary<string, object>
        {
            ["tolerance"] = -1.0
        };
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "wkt",
                Source = "LINESTRING(0 0, 10 0)"
            }
        };

        // Act
        var validation = operation.Validate(parameters, inputs);

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("greater than 0"));
    }

    [Fact]
    public async Task ConvexHullOperation_MultiPoint_ShouldReturnConvexPolygon()
    {
        // Arrange
        var operation = new ConvexHullOperation();
        var parameters = new Dictionary<string, object>
        {
            ["per_feature"] = false
        };
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "wkt",
                Source = "MULTIPOINT((0 0), (10 0), (10 10), (0 10), (5 5))"
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("geojson");
        result.Data.Should().ContainKey("geometry_type");

        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry.Should().NotBeNull();

        // Convex hull of a square with center point should be the square
        geometry!.Area.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public async Task ConvexHullOperation_PerFeatureMode_ShouldReturnMultipleHulls()
    {
        // Arrange
        var operation = new ConvexHullOperation();
        var parameters = new Dictionary<string, object>
        {
            ["per_feature"] = true
        };

        // Create a geometry collection with two separate point clusters
        var factory = GeometryFactory.Default;
        var point1 = factory.CreatePoint(new Coordinate(0, 0));
        var point2 = factory.CreatePoint(new Coordinate(10, 10));
        var collection = factory.CreateGeometryCollection(new Geometry[] { point1, point2 });

        var writer = new GeoJsonWriter();
        var geoJson = writer.Write(collection);

        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "geojson",
                Source = geoJson
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("count");
        result.Data["count"].Should().Be(2); // One hull per feature
    }

    [Fact]
    public async Task DissolveOperation_OverlappingPolygons_ShouldMerge()
    {
        // Arrange
        var operation = new DissolveOperation();
        var parameters = new Dictionary<string, object>();

        // Create three overlapping squares
        var factory = GeometryFactory.Default;
        var poly1 = (Polygon)new WKTReader().Read("POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))");
        var poly2 = (Polygon)new WKTReader().Read("POLYGON((3 0, 8 0, 8 5, 3 5, 3 0))");
        var poly3 = (Polygon)new WKTReader().Read("POLYGON((6 0, 11 0, 11 5, 6 5, 6 0))");
        var collection = factory.CreateGeometryCollection(new Geometry[] { poly1, poly2, poly3 });

        var writer = new GeoJsonWriter();
        var geoJson = writer.Write(collection);

        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "geojson",
                Source = geoJson
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("geojson");
        result.Data.Should().ContainKey("input_count");
        result.Data.Should().ContainKey("output_count");
        result.Data.Should().ContainKey("reduction_percent");

        result.Data["input_count"].Should().Be(3);
        result.Data["output_count"].Should().Be(1); // All merged into one

        // Verify the result is a single polygon
        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry.Should().NotBeNull();
        geometry.Should().BeOfType<Polygon>();
    }

    [Fact]
    public async Task DissolveOperation_AdjacentPolygons_ShouldRemoveInternalBoundaries()
    {
        // Arrange
        var operation = new DissolveOperation();
        var parameters = new Dictionary<string, object>();

        // Create two adjacent squares (sharing an edge)
        var factory = GeometryFactory.Default;
        var poly1 = (Polygon)new WKTReader().Read("POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))");
        var poly2 = (Polygon)new WKTReader().Read("POLYGON((5 0, 10 0, 10 5, 5 5, 5 0))");
        var collection = factory.CreateGeometryCollection(new Geometry[] { poly1, poly2 });

        var writer = new GeoJsonWriter();
        var geoJson = writer.Write(collection);

        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "geojson",
                Source = geoJson
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data["input_count"].Should().Be(2);
        result.Data["output_count"].Should().Be(1); // Merged into one rectangle

        var reader = new GeoJsonReader();
        var geometry = reader.Read<Geometry>(result.Data["geojson"].ToString()!);
        geometry!.Area.Should().BeApproximately(50.0, 0.1); // Combined area
    }

    [Fact]
    public async Task DissolveOperation_DisconnectedPolygons_ShouldReturnMultiPolygon()
    {
        // Arrange
        var operation = new DissolveOperation();
        var parameters = new Dictionary<string, object>();

        // Create two separated squares
        var factory = GeometryFactory.Default;
        var poly1 = (Polygon)new WKTReader().Read("POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))");
        var poly2 = (Polygon)new WKTReader().Read("POLYGON((10 10, 15 10, 15 15, 10 15, 10 10))");
        var collection = factory.CreateGeometryCollection(new Geometry[] { poly1, poly2 });

        var writer = new GeoJsonWriter();
        var geoJson = writer.Write(collection);

        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "geojson",
                Source = geoJson
            }
        };

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs);

        // Assert
        result.Success.Should().BeTrue();
        result.Data["input_count"].Should().Be(2);
        result.Data["output_count"].Should().Be(2); // Still two separate features
    }

    [Fact]
    public async Task DissolveOperation_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var operation = new DissolveOperation();
        var parameters = new Dictionary<string, object>();
        var inputs = new List<GeoprocessingInput>
        {
            new()
            {
                Name = "input",
                Type = "wkt",
                Source = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"
            }
        };

        var progressReports = new List<GeoprocessingProgress>();
        var progress = new Progress<GeoprocessingProgress>(p => progressReports.Add(p));

        // Act
        var result = await operation.ExecuteAsync(parameters, inputs, progress);

        // Assert
        result.Success.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.ProgressPercent == 0 || p.ProgressPercent == 10);
        progressReports.Should().Contain(p => p.ProgressPercent == 100);
    }

    [Theory]
    [InlineData("intersection", 2)] // Needs 2 inputs
    [InlineData("union", 1)]        // Needs at least 1 input
    [InlineData("difference", 2)]   // Needs 2 inputs
    [InlineData("simplify", 1)]     // Needs 1 input
    [InlineData("convex_hull", 1)]  // Needs 1 input
    [InlineData("dissolve", 1)]     // Needs 1 input
    public void Validate_RequiredInputs_ShouldEnforceCount(string operationType, int requiredInputs)
    {
        // Arrange
        IGeoprocessingOperation operation = operationType switch
        {
            "intersection" => new IntersectionOperation(),
            "union" => new UnionOperation(),
            "difference" => new DifferenceOperation(),
            "simplify" => new SimplifyOperation(),
            "convex_hull" => new ConvexHullOperation(),
            "dissolve" => new DissolveOperation(),
            _ => throw new ArgumentException($"Unknown operation: {operationType}")
        };

        var parameters = new Dictionary<string, object>();
        var insufficientInputs = new List<GeoprocessingInput>();

        // Act
        var result = operation.Validate(parameters, insufficientInputs);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.Contains("input") ||
            e.Contains("required") ||
            e.Contains("geometries"));
    }
}
