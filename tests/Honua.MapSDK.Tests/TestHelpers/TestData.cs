using Honua.MapSDK.Models;

namespace Honua.MapSDK.Tests.TestHelpers;

/// <summary>
/// Sample test data for use across all tests
/// </summary>
public static class TestData
{
    /// <summary>
    /// Sample GeoJSON FeatureCollection with multiple features
    /// </summary>
    public static string SampleGeoJson => @"{
  ""type"": ""FeatureCollection"",
  ""features"": [
    {
      ""type"": ""Feature"",
      ""id"": ""feature-1"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-122.4194, 37.7749]
      },
      ""properties"": {
        ""name"": ""San Francisco"",
        ""population"": 873965,
        ""state"": ""California"",
        ""category"": ""city"",
        ""timestamp"": ""2024-01-01T00:00:00Z""
      }
    },
    {
      ""type"": ""Feature"",
      ""id"": ""feature-2"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-118.2437, 34.0522]
      },
      ""properties"": {
        ""name"": ""Los Angeles"",
        ""population"": 3979576,
        ""state"": ""California"",
        ""category"": ""city"",
        ""timestamp"": ""2024-01-02T00:00:00Z""
      }
    },
    {
      ""type"": ""Feature"",
      ""id"": ""feature-3"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-122.3321, 47.6062]
      },
      ""properties"": {
        ""name"": ""Seattle"",
        ""population"": 753675,
        ""state"": ""Washington"",
        ""category"": ""city"",
        ""timestamp"": ""2024-01-03T00:00:00Z""
      }
    },
    {
      ""type"": ""Feature"",
      ""id"": ""feature-4"",
      ""geometry"": {
        ""type"": ""Polygon"",
        ""coordinates"": [[
          [-122.5, 37.5],
          [-122.5, 38.0],
          [-122.0, 38.0],
          [-122.0, 37.5],
          [-122.5, 37.5]
        ]]
      },
      ""properties"": {
        ""name"": ""San Francisco Bay Area"",
        ""population"": 7753000,
        ""state"": ""California"",
        ""category"": ""region"",
        ""timestamp"": ""2024-01-04T00:00:00Z""
      }
    }
  ]
}";

    /// <summary>
    /// Sample polygon GeoJSON
    /// </summary>
    public static string SamplePolygonGeoJson => @"{
  ""type"": ""Feature"",
  ""geometry"": {
    ""type"": ""Polygon"",
    ""coordinates"": [[
      [-122.5, 37.5],
      [-122.5, 38.0],
      [-122.0, 38.0],
      [-122.0, 37.5],
      [-122.5, 37.5]
    ]]
  },
  ""properties"": {
    ""name"": ""Test Polygon""
  }
}";

    /// <summary>
    /// Sample bounds (west, south, east, north)
    /// </summary>
    public static double[] SampleBounds => new[] { -122.5, 37.5, -122.0, 38.0 };

    /// <summary>
    /// Sample center coordinates
    /// </summary>
    public static double[] SampleCenter => new[] { -122.4194, 37.7749 };

    /// <summary>
    /// Sample map configuration
    /// </summary>
    public static MapConfiguration SampleMapConfig => new()
    {
        Center = new[] { -122.4194, 37.7749 },
        Zoom = 10,
        MinZoom = 0,
        MaxZoom = 22,
        Bearing = 0,
        Pitch = 0,
        Style = "https://api.maptiler.com/maps/streets/style.json",
        Layers = new List<LayerConfiguration>
        {
            new()
            {
                Id = "cities-layer",
                Name = "Cities",
                Type = "circle",
                Source = new SourceConfiguration
                {
                    Type = "geojson",
                    Data = SampleGeoJson
                },
                Paint = new Dictionary<string, object>
                {
                    ["circle-radius"] = 8,
                    ["circle-color"] = "#007cbf"
                }
            }
        }
    };

    /// <summary>
    /// Sample feature properties as dictionary
    /// </summary>
    public static Dictionary<string, object> SampleFeatureProperties => new()
    {
        ["name"] = "San Francisco",
        ["population"] = 873965,
        ["state"] = "California",
        ["category"] = "city",
        ["timestamp"] = "2024-01-01T00:00:00Z"
    };

    /// <summary>
    /// Sample list data for DataGrid testing
    /// </summary>
    public static List<TestCity> SampleCities => new()
    {
        new TestCity { Id = 1, Name = "San Francisco", Population = 873965, State = "California", Lat = 37.7749, Lon = -122.4194 },
        new TestCity { Id = 2, Name = "Los Angeles", Population = 3979576, State = "California", Lat = 34.0522, Lon = -118.2437 },
        new TestCity { Id = 3, Name = "Seattle", Population = 753675, State = "Washington", Lat = 47.6062, Lon = -122.3321 },
        new TestCity { Id = 4, Name = "Portland", Population = 652503, State = "Oregon", Lat = 45.5155, Lon = -122.6793 },
        new TestCity { Id = 5, Name = "San Diego", Population = 1423851, State = "California", Lat = 32.7157, Lon = -117.1611 }
    };

    /// <summary>
    /// Sample time series data for timeline testing
    /// </summary>
    public static List<TimeSeriesPoint> SampleTimeSeriesData => new()
    {
        new TimeSeriesPoint { Timestamp = new DateTime(2024, 1, 1), Value = 100, Category = "A" },
        new TimeSeriesPoint { Timestamp = new DateTime(2024, 1, 2), Value = 150, Category = "A" },
        new TimeSeriesPoint { Timestamp = new DateTime(2024, 1, 3), Value = 200, Category = "B" },
        new TimeSeriesPoint { Timestamp = new DateTime(2024, 1, 4), Value = 175, Category = "B" },
        new TimeSeriesPoint { Timestamp = new DateTime(2024, 1, 5), Value = 225, Category = "C" }
    };

    /// <summary>
    /// Sample Nominatim geocoding response
    /// </summary>
    public static string SampleNominatimResponse => @"[
  {
    ""place_id"": 123456,
    ""licence"": ""Data © OpenStreetMap contributors"",
    ""lat"": ""37.7749295"",
    ""lon"": ""-122.4194155"",
    ""display_name"": ""San Francisco, California, United States"",
    ""address"": {
      ""city"": ""San Francisco"",
      ""state"": ""California"",
      ""country"": ""United States""
    },
    ""boundingbox"": [""37.6398299"", ""37.9298239"", ""-123.1738653"", ""-122.2815528""]
  }
]";

    /// <summary>
    /// Sample Mapbox geocoding response
    /// </summary>
    public static string SampleMapboxResponse => @"{
  ""type"": ""FeatureCollection"",
  ""features"": [
    {
      ""type"": ""Feature"",
      ""geometry"": {
        ""type"": ""Point"",
        ""coordinates"": [-122.4194, 37.7749]
      },
      ""properties"": {
        ""place_name"": ""San Francisco, California, United States""
      }
    }
  ]
}";
}

/// <summary>
/// Sample city model for testing
/// </summary>
public class TestCity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Population { get; set; }
    public string State { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
}

/// <summary>
/// Sample time series point for testing
/// </summary>
public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Category { get; set; } = string.Empty;
}
