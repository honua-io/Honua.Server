using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Geoservices.GeometryService;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Geometry;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class EsriGeometrySerializerTests
{
    private readonly EsriGeometrySerializer _serializer = new();
    private readonly GeometryFactory _factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void DeserializeGeometries_ShouldParsePoints()
    {
        var payload = JsonNode.Parse("""
        {
          "geometryType": "esriGeometryPoint",
          "geometries": [
            { "x": -122.42, "y": 45.52 },
            { "x": -123.01, "y": 46.11, "z": 12.5 }
          ]
        }
        """)!;

        var geometries = _serializer.DeserializeGeometries(payload, "esriGeometryPoint", 4326);

        geometries.Should().HaveCount(2);
        var firstPoint = Assert.IsType<Point>(geometries[0]);
        firstPoint.Coordinate.X.Should().BeApproximately(-122.42, 1e-9);
        firstPoint.Coordinate.Y.Should().BeApproximately(45.52, 1e-9);
        firstPoint.SRID.Should().Be(4326);

        var secondPoint = Assert.IsType<Point>(geometries[1]);
        secondPoint.Coordinate.Z.Should().BeApproximately(12.5, 1e-9);
    }

    [Fact]
    public void SerializeGeometries_ShouldEmitExpectedStructure()
    {
        var pointA = _factory.CreatePoint(new Coordinate(-100.5, 43.2));
        pointA.SRID = 3857;
        var pointB = _factory.CreatePoint(new CoordinateZ(-100.75, 43.01, 5));
        pointB.SRID = 3857;

        var payload = _serializer.SerializeGeometries(new[] { pointA, pointB }, "esriGeometryPoint", 3857);

        payload["geometryType"]!.GetValue<string>().Should().Be("esriGeometryPoint");
        payload["spatialReference"]!.AsObject()["wkid"]!.GetValue<int>().Should().Be(3857);

        var geometries = payload["geometries"]!.AsArray();
        geometries.Should().HaveCount(2);
        geometries[0]!.AsObject()["x"]!.GetValue<double>().Should().BeApproximately(-100.5, 1e-9);
        geometries[1]!.AsObject().Should().ContainKey("z");
    }

    [Fact]
    public void DeserializePolygon_ShouldProducePolygonWithHole()
    {
        var payload = JsonNode.Parse("""
        {
          "geometryType": "esriGeometryPolygon",
          "geometries": [
            {
              "rings": [
                [
                  [0, 0], [10, 0], [10, 10], [0, 10], [0, 0]
                ],
                [
                  [2, 2], [2, 4], [4, 4], [4, 2], [2, 2]
                ]
              ]
            }
          ]
        }
        """)!;

        var geometries = _serializer.DeserializeGeometries(payload, "esriGeometryPolygon", 4326);

        geometries.Should().HaveCount(1);
        geometries[0].Should().BeOfType<Polygon>();
        var polygon = (Polygon)geometries[0];
        polygon.SRID.Should().Be(4326);
        polygon.NumInteriorRings.Should().Be(1);
        polygon.Area.Should().BeApproximately(96, 1e-6);
    }
}
