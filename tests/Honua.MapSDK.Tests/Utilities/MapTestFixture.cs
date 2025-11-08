// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;

namespace Honua.MapSDK.Tests.Utilities;

/// <summary>
/// Shared test fixtures and test data for map component tests.
/// Provides common test scenarios and data builders.
/// </summary>
public static class MapTestFixture
{
    /// <summary>
    /// Create a basic map configuration for testing
    /// </summary>
    public static MapConfiguration CreateBasicMapConfiguration(string name = "Test Map")
    {
        return new MapConfiguration
        {
            Name = name,
            Settings = new MapSettings
            {
                Style = "https://demotiles.maplibre.org/style.json",
                Center = new[] { -122.4194, 37.7749 }, // San Francisco
                Zoom = 12,
                Bearing = 0,
                Pitch = 0,
                Projection = "mercator"
            },
            Layers = new List<LayerConfiguration>(),
            Controls = new List<ControlConfiguration>()
        };
    }

    /// <summary>
    /// Create a map configuration with layers
    /// </summary>
    public static MapConfiguration CreateMapConfigurationWithLayers()
    {
        var config = CreateBasicMapConfiguration();
        config.Layers.Add(new LayerConfiguration
        {
            Id = "parcels",
            Name = "Parcels",
            Type = LayerType.Vector,
            Source = "grpc://api.honua.io/parcels",
            Visible = true,
            Opacity = 0.8,
            Style = new LayerStyle
            {
                FillColor = "#3388ff",
                FillOpacity = 0.5,
                LineColor = "#ffffff",
                LineWidth = 2
            }
        });

        config.Layers.Add(new LayerConfiguration
        {
            Id = "buildings",
            Name = "Buildings",
            Type = LayerType.ThreeD,
            Source = "grpc://api.honua.io/buildings",
            Visible = true,
            Opacity = 1.0,
            Style = new LayerStyle
            {
                FillColor = "#888888",
                ExtrusionHeight = new { property = "height" }
            }
        });

        return config;
    }

    /// <summary>
    /// Create a map configuration with controls
    /// </summary>
    public static MapConfiguration CreateMapConfigurationWithControls()
    {
        var config = CreateBasicMapConfiguration();
        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.Navigation,
            Position = "top-right",
            Visible = true
        });

        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.Scale,
            Position = "bottom-left",
            Visible = true
        });

        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.Search,
            Position = "top-left",
            Visible = true
        });

        return config;
    }

    /// <summary>
    /// Create a complex map configuration for testing
    /// </summary>
    public static MapConfiguration CreateComplexMapConfiguration()
    {
        var config = CreateMapConfigurationWithLayers();

        // Add controls
        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.Navigation,
            Position = "top-right",
            Visible = true
        });

        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.LayerList,
            Position = "top-left",
            Visible = true
        });

        // Add filters
        config.Filters = new FilterConfiguration
        {
            AllowSpatial = true,
            AllowAttribute = true,
            AllowTemporal = true,
            AvailableFilters = new List<FilterDefinition>
            {
                new FilterDefinition
                {
                    Field = "zoning",
                    Label = "Zoning Type",
                    Type = FilterFieldType.Select,
                    Options = new[] { "Residential", "Commercial", "Industrial" }
                },
                new FilterDefinition
                {
                    Field = "year_built",
                    Label = "Year Built",
                    Type = FilterFieldType.Range,
                    DefaultValue = new { min = 1900, max = 2025 }
                }
            }
        };

        // Add metadata
        config.Metadata["author"] = "Test User";
        config.Metadata["version"] = "1.0";
        config.Metadata["tags"] = new[] { "test", "parcels", "buildings" };

        return config;
    }

    /// <summary>
    /// Create test viewport bounds
    /// </summary>
    public static double[] CreateTestBounds()
    {
        // San Francisco Bay Area bounds [west, south, east, north]
        return new[] { -122.5, 37.7, -122.3, 37.8 };
    }

    /// <summary>
    /// Create test center coordinates
    /// </summary>
    public static double[] CreateTestCenter()
    {
        // San Francisco coordinates [longitude, latitude]
        return new[] { -122.4194, 37.7749 };
    }

    /// <summary>
    /// Create test geometry (GeoJSON polygon)
    /// </summary>
    public static object CreateTestPolygonGeometry()
    {
        return new
        {
            type = "Polygon",
            coordinates = new[]
            {
                new[]
                {
                    new[] { -122.42, 37.77 },
                    new[] { -122.41, 37.77 },
                    new[] { -122.41, 37.78 },
                    new[] { -122.42, 37.78 },
                    new[] { -122.42, 37.77 }
                }
            }
        };
    }

    /// <summary>
    /// Create test geometry (GeoJSON point)
    /// </summary>
    public static object CreateTestPointGeometry(double lon = -122.4194, double lat = 37.7749)
    {
        return new
        {
            type = "Point",
            coordinates = new[] { lon, lat }
        };
    }

    /// <summary>
    /// Create test feature properties
    /// </summary>
    public static Dictionary<string, object> CreateTestFeatureProperties()
    {
        return new Dictionary<string, object>
        {
            ["id"] = "parcel-123",
            ["address"] = "123 Market St",
            ["zoning"] = "Commercial",
            ["area_sqft"] = 5000,
            ["year_built"] = 1920,
            ["owner"] = "Test Owner LLC"
        };
    }

    /// <summary>
    /// Create test route coordinates
    /// </summary>
    public static List<double[]> CreateTestRouteCoordinates()
    {
        return new List<double[]>
        {
            new[] { -122.4194, 37.7749 },  // Start: San Francisco
            new[] { -122.4089, 37.7835 },  // Waypoint 1
            new[] { -122.3959, 37.7916 },  // Waypoint 2
            new[] { -122.3892, 37.8044 }   // End: North Beach
        };
    }

    /// <summary>
    /// Create test encoded polyline (Google Maps format)
    /// </summary>
    public static string CreateTestEncodedPolyline()
    {
        // Simplified encoded polyline for testing
        return "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
    }
}
