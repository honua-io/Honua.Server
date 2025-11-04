using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Comprehensive test data generator for all OGC geometry types with geodetic considerations.
/// Ensures consistent test data across all provider × API × format tests.
/// </summary>
public static class GeometryTestData
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    /// <summary>
    /// All supported geometry types for comprehensive matrix testing.
    /// </summary>
    public enum GeometryType
    {
        Point,
        LineString,
        Polygon,
        MultiPoint,
        MultiLineString,
        MultiPolygon,
        GeometryCollection
    }

    /// <summary>
    /// Test scenarios covering different geodetic edge cases.
    /// </summary>
    public enum GeodeticScenario
    {
        /// <summary>Simple coordinates in Portland, Oregon area</summary>
        Simple,

        /// <summary>Crosses antimeridian (180° longitude)</summary>
        AntimeridianCrossing,

        /// <summary>Near North Pole</summary>
        NorthPole,

        /// <summary>Near South Pole</summary>
        SouthPole,

        /// <summary>Spans multiple hemispheres</summary>
        GlobalExtent,

        /// <summary>Very small features (sub-meter precision)</summary>
        HighPrecision,

        /// <summary>Features with holes (donuts)</summary>
        WithHoles
    }

    /// <summary>
    /// Get test geometry for a specific type and geodetic scenario.
    /// </summary>
    public static NetTopologySuite.Geometries.Geometry GetTestGeometry(GeometryType type, GeodeticScenario scenario)
    {
        return type switch
        {
            GeometryType.Point => CreatePoint(scenario),
            GeometryType.LineString => CreateLineString(scenario),
            GeometryType.Polygon => CreatePolygon(scenario),
            GeometryType.MultiPoint => CreateMultiPoint(scenario),
            GeometryType.MultiLineString => CreateMultiLineString(scenario),
            GeometryType.MultiPolygon => CreateMultiPolygon(scenario),
            GeometryType.GeometryCollection => CreateGeometryCollection(scenario),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    /// <summary>
    /// Get expected feature attributes for testing.
    /// </summary>
    public static Dictionary<string, object?> GetTestAttributes(GeometryType type, int featureId)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["feature_id"] = featureId,
            ["name"] = $"{type} Feature {featureId}",
            ["geometry_type"] = type.ToString(),
            ["category"] = "test_data",
            ["priority"] = featureId % 3 + 1,
            ["active"] = featureId % 2 == 0,
            ["created_at"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(featureId),
            ["measurement"] = 123.45 * featureId,
            ["description"] = $"Test feature for {type} geometry validation"
        };
    }

    // Point geometries
    private static Point CreatePoint(GeodeticScenario scenario)
    {
        return scenario switch
        {
            GeodeticScenario.Simple => Factory.CreatePoint(new Coordinate(-122.4194, 45.5231)),
            GeodeticScenario.AntimeridianCrossing => Factory.CreatePoint(new Coordinate(179.9999, 0.0)),
            GeodeticScenario.NorthPole => Factory.CreatePoint(new Coordinate(0.0, 89.9)),
            GeodeticScenario.SouthPole => Factory.CreatePoint(new Coordinate(0.0, -89.9)),
            GeodeticScenario.HighPrecision => Factory.CreatePoint(new Coordinate(-122.419412345678, 45.523123456789)),
            _ => Factory.CreatePoint(new Coordinate(-122.4194, 45.5231))
        };
    }

    // LineString geometries
    private static LineString CreateLineString(GeodeticScenario scenario)
    {
        Coordinate[] coords = scenario switch
        {
            GeodeticScenario.Simple => new[]
            {
                new Coordinate(-122.4, 45.5),
                new Coordinate(-122.3, 45.6),
                new Coordinate(-122.2, 45.7)
            },
            GeodeticScenario.AntimeridianCrossing => new[]
            {
                new Coordinate(179.0, 0.0),
                new Coordinate(-179.0, 0.0) // Crosses antimeridian
            },
            GeodeticScenario.NorthPole => new[]
            {
                new Coordinate(-180.0, 85.0),
                new Coordinate(0.0, 89.5),
                new Coordinate(180.0, 85.0)
            },
            GeodeticScenario.SouthPole => new[]
            {
                new Coordinate(-180.0, -85.0),
                new Coordinate(0.0, -89.5),
                new Coordinate(180.0, -85.0)
            },
            GeodeticScenario.GlobalExtent => new[]
            {
                new Coordinate(-179.0, -85.0),
                new Coordinate(0.0, 0.0),
                new Coordinate(179.0, 85.0)
            },
            GeodeticScenario.HighPrecision => new[]
            {
                new Coordinate(-122.400000001, 45.500000001),
                new Coordinate(-122.400000002, 45.500000002),
                new Coordinate(-122.400000003, 45.500000003)
            },
            _ => new[]
            {
                new Coordinate(-122.4, 45.5),
                new Coordinate(-122.3, 45.6)
            }
        };

        return Factory.CreateLineString(coords);
    }

    // Polygon geometries
    private static Polygon CreatePolygon(GeodeticScenario scenario)
    {
        return scenario switch
        {
            GeodeticScenario.Simple => CreateSimplePolygon(),
            GeodeticScenario.WithHoles => CreatePolygonWithHole(),
            GeodeticScenario.AntimeridianCrossing => CreateAntimeridianPolygon(),
            GeodeticScenario.NorthPole => CreateNorthPolePolygon(),
            GeodeticScenario.SouthPole => CreateSouthPolePolygon(),
            GeodeticScenario.GlobalExtent => CreateGlobalExtentPolygon(),
            GeodeticScenario.HighPrecision => CreateHighPrecisionPolygon(),
            _ => CreateSimplePolygon()
        };
    }

    private static Polygon CreateSimplePolygon()
    {
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.3, 45.7),
            new Coordinate(-122.5, 45.7),
            new Coordinate(-122.5, 45.5) // Close ring
        });

        return Factory.CreatePolygon(shell);
    }

    private static Polygon CreatePolygonWithHole()
    {
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.3, 45.7),
            new Coordinate(-122.5, 45.7),
            new Coordinate(-122.5, 45.5)
        });

        var hole = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-122.45, 45.55),
            new Coordinate(-122.35, 45.55),
            new Coordinate(-122.35, 45.65),
            new Coordinate(-122.45, 45.65),
            new Coordinate(-122.45, 45.55)
        });

        return Factory.CreatePolygon(shell, new[] { hole });
    }

    private static Polygon CreateAntimeridianPolygon()
    {
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(179.0, -10.0),
            new Coordinate(-179.0, -10.0),
            new Coordinate(-179.0, 10.0),
            new Coordinate(179.0, 10.0),
            new Coordinate(179.0, -10.0)
        });

        return Factory.CreatePolygon(shell);
    }

    private static Polygon CreateHighPrecisionPolygon()
    {
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-122.400000001, 45.500000001),
            new Coordinate(-122.400000002, 45.500000001),
            new Coordinate(-122.400000002, 45.500000002),
            new Coordinate(-122.400000001, 45.500000002),
            new Coordinate(-122.400000001, 45.500000001)
        });

        return Factory.CreatePolygon(shell);
    }

    private static Polygon CreateNorthPolePolygon()
    {
        // Triangle polygon near North Pole
        // Tests coordinate systems at high latitudes where meridians converge
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-120.0, 85.0),
            new Coordinate(0.0, 85.0),
            new Coordinate(120.0, 85.0),
            new Coordinate(-120.0, 85.0) // Close ring
        });

        return Factory.CreatePolygon(shell);
    }

    private static Polygon CreateSouthPolePolygon()
    {
        // Triangle polygon near South Pole
        // Tests coordinate systems at high southern latitudes
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-120.0, -85.0),
            new Coordinate(0.0, -85.0),
            new Coordinate(120.0, -85.0),
            new Coordinate(-120.0, -85.0) // Close ring
        });

        return Factory.CreatePolygon(shell);
    }

    private static Polygon CreateGlobalExtentPolygon()
    {
        // Large polygon spanning multiple hemispheres
        // Tests ability to handle worldwide/continental scale features
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(-170.0, -80.0), // Near South Pole, west
            new Coordinate(170.0, -80.0),  // Near South Pole, east
            new Coordinate(170.0, 80.0),   // Near North Pole, east
            new Coordinate(-170.0, 80.0),  // Near North Pole, west
            new Coordinate(-170.0, -80.0)  // Close ring
        });

        return Factory.CreatePolygon(shell);
    }

    // MultiPoint geometries
    private static MultiPoint CreateMultiPoint(GeodeticScenario scenario)
    {
        Point[] points = scenario switch
        {
            GeodeticScenario.Simple => new[]
            {
                Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
                Factory.CreatePoint(new Coordinate(-122.3, 45.6)),
                Factory.CreatePoint(new Coordinate(-122.2, 45.7))
            },
            GeodeticScenario.GlobalExtent => new[]
            {
                Factory.CreatePoint(new Coordinate(-179.0, -85.0)),
                Factory.CreatePoint(new Coordinate(0.0, 0.0)),
                Factory.CreatePoint(new Coordinate(179.0, 85.0))
            },
            _ => new[]
            {
                Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
                Factory.CreatePoint(new Coordinate(-122.3, 45.6))
            }
        };

        return Factory.CreateMultiPoint(points);
    }

    // MultiLineString geometries
    private static MultiLineString CreateMultiLineString(GeodeticScenario scenario)
    {
        LineString[] lines = scenario switch
        {
            GeodeticScenario.Simple => new[]
            {
                Factory.CreateLineString(new[]
                {
                    new Coordinate(-122.5, 45.5),
                    new Coordinate(-122.4, 45.6)
                }),
                Factory.CreateLineString(new[]
                {
                    new Coordinate(-122.3, 45.5),
                    new Coordinate(-122.2, 45.6)
                })
            },
            _ => new[]
            {
                Factory.CreateLineString(new[]
                {
                    new Coordinate(-122.5, 45.5),
                    new Coordinate(-122.4, 45.6)
                })
            }
        };

        return Factory.CreateMultiLineString(lines);
    }

    // MultiPolygon geometries
    private static MultiPolygon CreateMultiPolygon(GeodeticScenario scenario)
    {
        Polygon[] polygons = scenario switch
        {
            GeodeticScenario.Simple => new[]
            {
                CreateSimplePolygonAt(-122.5, 45.5),
                CreateSimplePolygonAt(-122.3, 45.7)
            },
            _ => new[]
            {
                CreateSimplePolygonAt(-122.5, 45.5)
            }
        };

        return Factory.CreateMultiPolygon(polygons);
    }

    private static Polygon CreateSimplePolygonAt(double lon, double lat)
    {
        var shell = Factory.CreateLinearRing(new[]
        {
            new Coordinate(lon, lat),
            new Coordinate(lon + 0.1, lat),
            new Coordinate(lon + 0.1, lat + 0.1),
            new Coordinate(lon, lat + 0.1),
            new Coordinate(lon, lat)
        });

        return Factory.CreatePolygon(shell);
    }

    // GeometryCollection
    private static GeometryCollection CreateGeometryCollection(GeodeticScenario scenario)
    {
        NetTopologySuite.Geometries.Geometry[] geometries = new NetTopologySuite.Geometries.Geometry[]
        {
            CreatePoint(GeodeticScenario.Simple),
            CreateLineString(GeodeticScenario.Simple),
            CreatePolygon(GeodeticScenario.Simple)
        };

        return Factory.CreateGeometryCollection(geometries);
    }

    /// <summary>
    /// Get WKT representation for database seeding.
    /// </summary>
    public static string ToWkt(NetTopologySuite.Geometries.Geometry geometry)
    {
        var writer = new WKTWriter();
        return writer.Write(geometry);
    }

    /// <summary>
    /// Get GeoJSON representation for validation.
    /// </summary>
    public static string ToGeoJson(NetTopologySuite.Geometries.Geometry geometry)
    {
        var writer = new GeoJsonWriter();
        return writer.Write(geometry);
    }

    /// <summary>
    /// Get all geometry type × geodetic scenario combinations for comprehensive testing.
    /// </summary>
    public static IEnumerable<(GeometryType Type, GeodeticScenario Scenario)> GetAllTestCombinations()
    {
        var geometryTypes = Enum.GetValues<GeometryType>();
        var scenarios = Enum.GetValues<GeodeticScenario>();

        foreach (var type in geometryTypes)
        {
            foreach (var scenario in scenarios)
            {
                // Skip incompatible combinations
                if (type == GeometryType.Point && scenario == GeodeticScenario.WithHoles)
                    continue;
                if (type == GeometryType.LineString && scenario == GeodeticScenario.WithHoles)
                    continue;

                yield return (type, scenario);
            }
        }
    }

    /// <summary>
    /// Get essential test combinations for quick smoke tests.
    /// Includes basic geometry types + critical geodetic edge cases.
    /// </summary>
    public static IEnumerable<(GeometryType Type, GeodeticScenario Scenario)> GetEssentialCombinations()
    {
        // All 7 basic geometry types with Simple scenario
        yield return (GeometryType.Point, GeodeticScenario.Simple);
        yield return (GeometryType.LineString, GeodeticScenario.Simple);
        yield return (GeometryType.Polygon, GeodeticScenario.Simple);
        yield return (GeometryType.MultiPoint, GeodeticScenario.Simple);
        yield return (GeometryType.MultiLineString, GeodeticScenario.Simple);
        yield return (GeometryType.MultiPolygon, GeodeticScenario.Simple);
        yield return (GeometryType.GeometryCollection, GeodeticScenario.Simple);

        // Critical geodetic edge cases (Option 1)

        // Antimeridian crossing - critical for Pacific Ocean applications
        yield return (GeometryType.LineString, GeodeticScenario.AntimeridianCrossing);
        yield return (GeometryType.Polygon, GeodeticScenario.AntimeridianCrossing);

        // Polar regions - critical for global coverage
        yield return (GeometryType.Point, GeodeticScenario.NorthPole);
        yield return (GeometryType.LineString, GeodeticScenario.NorthPole);
        yield return (GeometryType.Polygon, GeodeticScenario.NorthPole);

        // Polygon with holes - common GIS pattern
        yield return (GeometryType.Polygon, GeodeticScenario.WithHoles);

        // High precision - validate coordinate accuracy
        yield return (GeometryType.Point, GeodeticScenario.HighPrecision);

        // Global extent - validate hemisphere spanning
        yield return (GeometryType.LineString, GeodeticScenario.GlobalExtent);
        yield return (GeometryType.Polygon, GeodeticScenario.GlobalExtent);
    }
}
