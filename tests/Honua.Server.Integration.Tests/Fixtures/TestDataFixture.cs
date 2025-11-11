// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Integration.Tests.Fixtures;

/// <summary>
/// Provides sample geospatial test data for integration tests.
/// This includes test geometries, GeoJSON features, and coordinate systems.
/// </summary>
public static class TestDataFixture
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    /// <summary>
    /// Sample point geometry (Honolulu, Hawaii).
    /// </summary>
    public static Point SamplePoint => GeometryFactory.CreatePoint(new Coordinate(-157.8583, 21.3099));

    /// <summary>
    /// Sample polygon geometry (bounding box around Oahu).
    /// </summary>
    public static Polygon SamplePolygon
    {
        get
        {
            var coordinates = new[]
            {
                new Coordinate(-158.3, 21.2),
                new Coordinate(-157.6, 21.2),
                new Coordinate(-157.6, 21.7),
                new Coordinate(-158.3, 21.7),
                new Coordinate(-158.3, 21.2)
            };
            return GeometryFactory.CreatePolygon(coordinates);
        }
    }

    /// <summary>
    /// Sample line string geometry (path across Oahu).
    /// </summary>
    public static LineString SampleLineString
    {
        get
        {
            var coordinates = new[]
            {
                new Coordinate(-158.0, 21.3),
                new Coordinate(-157.9, 21.4),
                new Coordinate(-157.8, 21.5),
                new Coordinate(-157.7, 21.6)
            };
            return GeometryFactory.CreateLineString(coordinates);
        }
    }

    /// <summary>
    /// Sample multi-point geometry (multiple locations on Oahu).
    /// </summary>
    public static MultiPoint SampleMultiPoint
    {
        get
        {
            var points = new[]
            {
                GeometryFactory.CreatePoint(new Coordinate(-157.8583, 21.3099)),
                GeometryFactory.CreatePoint(new Coordinate(-157.9, 21.4)),
                GeometryFactory.CreatePoint(new Coordinate(-158.0, 21.5))
            };
            return GeometryFactory.CreateMultiPoint(points);
        }
    }

    /// <summary>
    /// Sample bounding box coordinates [minX, minY, maxX, maxY].
    /// </summary>
    public static double[] SampleBbox => new[] { -158.3, 21.2, -157.6, 21.7 };

    /// <summary>
    /// Sample GeoJSON feature with properties.
    /// </summary>
    public static string SampleGeoJsonFeature => @"{
  ""type"": ""Feature"",
  ""geometry"": {
    ""type"": ""Point"",
    ""coordinates"": [-157.8583, 21.3099]
  },
  ""properties"": {
    ""name"": ""Honolulu"",
    ""population"": 345064,
    ""state"": ""Hawaii"",
    ""elevation"": 6
  }
}";

    /// <summary>
    /// Sample GeoJSON FeatureCollection.
    /// </summary>
    public static string SampleGeoJsonFeatureCollection => @"{
  ""type"": ""FeatureCollection"",
  ""features"": [
    {
      ""type"": ""Feature"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-157.8583, 21.3099]
      },
      ""properties"": {
        ""name"": ""Honolulu"",
        ""population"": 345064
      }
    },
    {
      ""type"": ""Feature"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-155.5828, 19.5429]
      },
      ""properties"": {
        ""name"": ""Hilo"",
        ""population"": 44186
      }
    }
  ]
}";

    /// <summary>
    /// Sample attribute data for feature testing.
    /// </summary>
    public static Dictionary<string, object> SampleAttributes => new()
    {
        ["name"] = "Test Feature",
        ["description"] = "A test feature for integration testing",
        ["category"] = "Test",
        ["value"] = 42,
        ["active"] = true,
        ["created_date"] = "2025-01-01T00:00:00Z"
    };

    /// <summary>
    /// Sample ISO 8601 datetime strings for temporal filtering.
    /// </summary>
    public static string SampleDateTimeStart => "2024-01-01T00:00:00Z";
    public static string SampleDateTimeEnd => "2024-12-31T23:59:59Z";
    public static string SampleDateTimeInterval => "2024-01-01T00:00:00Z/2024-12-31T23:59:59Z";

    /// <summary>
    /// Sample STAC collection ID for testing.
    /// </summary>
    public static string SampleStacCollectionId => "test-collection";

    /// <summary>
    /// Sample STAC item ID for testing.
    /// </summary>
    public static string SampleStacItemId => "test-item-001";
}
