// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Maps.AI;

/// <summary>
/// Prompt templates for AI map generation
/// </summary>
public static class MapGenerationPromptTemplates
{
    /// <summary>
    /// System prompt that explains the Honua mapping system and capabilities
    /// </summary>
    public static string GetSystemPrompt()
    {
        return @"You are an expert map designer for the Honua geospatial platform. Your task is to convert natural language requests into complete, interactive map configurations that can be rendered using MapLibre GL JS.

## Map Capabilities

Honua supports rich, interactive web maps with the following features:

### Layer Types
1. **Vector Layers** - Point, line, and polygon features from PostGIS, GeoJSON, or WFS
2. **Raster Layers** - Imagery and raster tiles
3. **3D Layers** - Extruded buildings and terrain
4. **Heatmaps** - Density visualizations
5. **Cluster Layers** - Clustered point features
6. **Symbol Layers** - Icons and markers

### Data Sources
- **PostGIS**: grpc://api.honua.io/table_name
- **GeoJSON**: geojson://url or inline GeoJSON
- **WFS**: wfs://server/layer_name
- **WMS**: wms://server?layers=layer_name
- **Vector Tiles**: vtiles://server/{z}/{x}/{y}

### Styling Options
- Fill colors with opacity
- Line colors and widths
- Circle markers with radius and color
- Data-driven styling based on attributes
- Heatmap gradients
- 3D extrusion heights

### Spatial Analysis
When users request spatial operations (e.g., ""within X miles"", ""intersecting"", ""buffer""), describe the operation in the explanation and note that it will be performed by the backend.

### Common Patterns

**Point Analysis**: Show points with optional clustering, heatmaps, or circle markers
**Proximity Search**: Layers with filters for distance-based queries
**Multi-layer Comparison**: Multiple layers with different styling to show relationships
**Temporal Data**: Layers with time-based filters (if temporal fields exist)
**Thematic Maps**: Data-driven styling based on attribute values (choropleth, graduated symbols)

## Map Configuration Structure

Return ONLY valid JSON matching this structure:

{
  ""name"": ""Map Title"",
  ""description"": ""Brief description of what the map shows"",
  ""settings"": {
    ""style"": ""maplibre://honua/streets"",
    ""center"": [longitude, latitude],
    ""zoom"": 12,
    ""pitch"": 0,
    ""bearing"": 0,
    ""projection"": ""mercator""
  },
  ""layers"": [
    {
      ""name"": ""Layer Name"",
      ""type"": ""Vector|Raster|ThreeD|Heatmap|Cluster|Line|Fill|Symbol"",
      ""source"": ""grpc://api.honua.io/table_name"",
      ""visible"": true,
      ""opacity"": 1.0,
      ""style"": {
        ""fillColor"": ""#3388ff"",
        ""fillOpacity"": 0.6,
        ""lineColor"": ""#000000"",
        ""lineWidth"": 2,
        ""circleRadius"": 8,
        ""circleColor"": ""#ff0000""
      },
      ""popupTemplate"": ""<h3>{name}</h3><p>{description}</p>""
    }
  ],
  ""controls"": [
    {
      ""type"": ""Navigation|Scale|Fullscreen|Legend|Search|Measure|Draw"",
      ""position"": ""top-right"",
      ""visible"": true
    }
  ],
  ""filters"": {
    ""allowSpatial"": true,
    ""allowAttribute"": true,
    ""allowTemporal"": false,
    ""availableFilters"": [
      {
        ""field"": ""field_name"",
        ""label"": ""Display Label"",
        ""type"": ""Text|Number|Date|Select|Range|Boolean""
      }
    ]
  }
}

## Style Presets

Use these base map styles:
- ""maplibre://honua/streets"" - Standard street map
- ""maplibre://honua/satellite"" - Satellite imagery
- ""maplibre://honua/dark"" - Dark theme
- ""maplibre://honua/light"" - Light theme
- ""maplibre://honua/outdoors"" - Topographic/terrain

## Color Guidelines

Use clear, accessible colors:
- Blues (#0066cc, #3388ff, #00ccff) for water, general features
- Greens (#00cc66, #66cc00, #009933) for vegetation, parks
- Reds (#ff0000, #cc0000, #ff3333) for warnings, fires, important points
- Yellows (#ffcc00, #ffff00, #ff9900) for caution, highlights
- Purples (#9933ff, #cc00cc, #6600cc) for special features
- Grays (#666666, #999999, #cccccc) for neutral features

## Rules

1. Always include at least one layer
2. Set appropriate zoom levels based on data type (e.g., city: 11-14, country: 4-7)
3. Center the map on the relevant geographic area
4. Include controls that make sense for the use case
5. Add popup templates to show feature information
6. For spatial queries, note them in the explanation but let the backend handle them
7. Use appropriate layer types (e.g., Heatmap for density, Cluster for many points)
8. Include filters for interactive exploration when appropriate

## Response Format

Return ONLY valid JSON. Do NOT include any explanatory text outside the JSON.
The response must be parseable as JSON.";
    }

    /// <summary>
    /// Gets few-shot examples to guide the AI
    /// </summary>
    public static string GetFewShotExamples()
    {
        return @"
## Example 1: Simple Point Map

User: ""Show me all schools in San Francisco""

Response:
{
  ""name"": ""San Francisco Schools"",
  ""description"": ""Map showing all school locations in San Francisco"",
  ""settings"": {
    ""style"": ""maplibre://honua/streets"",
    ""center"": [-122.4194, 37.7749],
    ""zoom"": 12,
    ""pitch"": 0,
    ""bearing"": 0,
    ""projection"": ""mercator""
  },
  ""layers"": [
    {
      ""name"": ""Schools"",
      ""type"": ""Symbol"",
      ""source"": ""grpc://api.honua.io/schools"",
      ""visible"": true,
      ""opacity"": 1.0,
      ""style"": {
        ""circleRadius"": 8,
        ""circleColor"": ""#3388ff"",
        ""lineColor"": ""#ffffff"",
        ""lineWidth"": 2
      },
      ""popupTemplate"": ""<h3>{name}</h3><p>Type: {school_type}</p><p>Grades: {grade_range}</p>""
    }
  ],
  ""controls"": [
    {
      ""type"": ""Navigation"",
      ""position"": ""top-right"",
      ""visible"": true
    },
    {
      ""type"": ""Search"",
      ""position"": ""top-left"",
      ""visible"": true
    },
    {
      ""type"": ""Legend"",
      ""position"": ""bottom-right"",
      ""visible"": true
    }
  ],
  ""filters"": {
    ""allowSpatial"": true,
    ""allowAttribute"": true,
    ""allowTemporal"": false,
    ""availableFilters"": [
      {
        ""field"": ""school_type"",
        ""label"": ""School Type"",
        ""type"": ""Select""
      },
      {
        ""field"": ""enrollment"",
        ""label"": ""Enrollment"",
        ""type"": ""Range""
      }
    ]
  }
}

## Example 2: Proximity Analysis with Multiple Layers

User: ""Show me all schools within 2 miles of industrial zones""

Response:
{
  ""name"": ""Schools Near Industrial Zones"",
  ""description"": ""Shows schools located within 2 miles of industrial zones. The proximity analysis will be performed on the server."",
  ""settings"": {
    ""style"": ""maplibre://honua/light"",
    ""center"": [-118.2437, 34.0522],
    ""zoom"": 11,
    ""pitch"": 0,
    ""bearing"": 0,
    ""projection"": ""mercator""
  },
  ""layers"": [
    {
      ""name"": ""Industrial Zones"",
      ""type"": ""Fill"",
      ""source"": ""grpc://api.honua.io/industrial_zones"",
      ""visible"": true,
      ""opacity"": 0.8,
      ""style"": {
        ""fillColor"": ""#666666"",
        ""fillOpacity"": 0.4,
        ""lineColor"": ""#333333"",
        ""lineWidth"": 2
      },
      ""popupTemplate"": ""<h3>Industrial Zone</h3><p>Type: {zone_type}</p>""
    },
    {
      ""name"": ""Schools Within 2 Miles"",
      ""type"": ""Symbol"",
      ""source"": ""grpc://api.honua.io/schools?spatial_filter=buffer_intersect:industrial_zones:2:miles"",
      ""visible"": true,
      ""opacity"": 1.0,
      ""style"": {
        ""circleRadius"": 10,
        ""circleColor"": ""#ff3333"",
        ""lineColor"": ""#ffffff"",
        ""lineWidth"": 2
      },
      ""popupTemplate"": ""<h3>{name}</h3><p>Distance to nearest industrial zone: {distance_miles} miles</p><p>Students: {enrollment}</p>""
    }
  ],
  ""controls"": [
    {
      ""type"": ""Navigation"",
      ""position"": ""top-right"",
      ""visible"": true
    },
    {
      ""type"": ""Measure"",
      ""position"": ""top-left"",
      ""visible"": true
    },
    {
      ""type"": ""Legend"",
      ""position"": ""bottom-right"",
      ""visible"": true
    }
  ],
  ""filters"": {
    ""allowSpatial"": true,
    ""allowAttribute"": true,
    ""allowTemporal"": false,
    ""availableFilters"": [
      {
        ""field"": ""distance_miles"",
        ""label"": ""Distance (miles)"",
        ""type"": ""Range""
      }
    ]
  }
}

## Example 3: Heatmap Visualization

User: ""Create a heatmap of crime incidents in the last month""

Response:
{
  ""name"": ""Crime Incident Heatmap"",
  ""description"": ""Density heatmap showing crime incidents from the last 30 days"",
  ""settings"": {
    ""style"": ""maplibre://honua/dark"",
    ""center"": [-122.4194, 37.7749],
    ""zoom"": 12,
    ""pitch"": 0,
    ""bearing"": 0,
    ""projection"": ""mercator""
  },
  ""layers"": [
    {
      ""name"": ""Crime Heatmap"",
      ""type"": ""Heatmap"",
      ""source"": ""grpc://api.honua.io/crime_incidents?time_filter=last_30_days"",
      ""visible"": true,
      ""opacity"": 0.8,
      ""style"": {
        ""heatmap"": {
          ""radius"": 30,
          ""intensity"": 1.0,
          ""colorRamp"": [""#0000ff"", ""#00ff00"", ""#ffff00"", ""#ff9900"", ""#ff0000""]
        }
      }
    },
    {
      ""name"": ""Crime Points"",
      ""type"": ""Symbol"",
      ""source"": ""grpc://api.honua.io/crime_incidents?time_filter=last_30_days"",
      ""visible"": false,
      ""opacity"": 1.0,
      ""minZoom"": 14,
      ""style"": {
        ""circleRadius"": 6,
        ""circleColor"": ""#ff3333"",
        ""lineColor"": ""#ffffff"",
        ""lineWidth"": 1
      },
      ""popupTemplate"": ""<h3>{incident_type}</h3><p>Date: {incident_date}</p><p>Location: {location_desc}</p>""
    }
  ],
  ""controls"": [
    {
      ""type"": ""Navigation"",
      ""position"": ""top-right"",
      ""visible"": true
    },
    {
      ""type"": ""Legend"",
      ""position"": ""bottom-right"",
      ""visible"": true
    }
  ],
  ""filters"": {
    ""allowSpatial"": true,
    ""allowAttribute"": true,
    ""allowTemporal"": true,
    ""availableFilters"": [
      {
        ""field"": ""incident_date"",
        ""label"": ""Incident Date"",
        ""type"": ""Date""
      },
      {
        ""field"": ""incident_type"",
        ""label"": ""Crime Type"",
        ""type"": ""Select""
      }
    ]
  }
}

## Example 4: 3D Buildings Map

User: ""Show downtown buildings in 3D""

Response:
{
  ""name"": ""Downtown 3D Buildings"",
  ""description"": ""3D visualization of downtown buildings with heights"",
  ""settings"": {
    ""style"": ""maplibre://honua/streets"",
    ""center"": [-73.9857, 40.7484],
    ""zoom"": 15,
    ""pitch"": 60,
    ""bearing"": -20,
    ""projection"": ""mercator""
  },
  ""layers"": [
    {
      ""name"": ""3D Buildings"",
      ""type"": ""ThreeD"",
      ""source"": ""grpc://api.honua.io/buildings"",
      ""visible"": true,
      ""opacity"": 0.9,
      ""style"": {
        ""fillColor"": ""#aaaaaa"",
        ""fillOpacity"": 0.9,
        ""lineColor"": ""#666666"",
        ""lineWidth"": 1,
        ""extrusionHeight"": ""{height}""
      },
      ""popupTemplate"": ""<h3>{name}</h3><p>Height: {height} feet</p><p>Year Built: {year_built}</p><p>Use: {building_use}</p>""
    }
  ],
  ""controls"": [
    {
      ""type"": ""Navigation"",
      ""position"": ""top-right"",
      ""visible"": true
    },
    {
      ""type"": ""Fullscreen"",
      ""position"": ""top-right"",
      ""visible"": true
    }
  ],
  ""filters"": {
    ""allowSpatial"": false,
    ""allowAttribute"": true,
    ""allowTemporal"": false,
    ""availableFilters"": [
      {
        ""field"": ""height"",
        ""label"": ""Building Height (ft)"",
        ""type"": ""Range""
      },
      {
        ""field"": ""building_use"",
        ""label"": ""Building Use"",
        ""type"": ""Select""
      }
    ]
  }
}";
    }

    /// <summary>
    /// Formats the user prompt
    /// </summary>
    public static string FormatUserPrompt(string userRequest)
    {
        return $@"Generate a map configuration for the following request:

""{userRequest}""

Remember: Return ONLY valid JSON, no additional text or explanation.";
    }

    /// <summary>
    /// Gets example prompts for users
    /// </summary>
    public static List<string> GetExamplePrompts()
    {
        return new List<string>
        {
            "Show me all parks in Seattle",
            "Show me all schools within 2 miles of industrial zones",
            "Create a heatmap of traffic accidents in the last 6 months",
            "Show downtown buildings in 3D",
            "Map all fire stations and hospitals with a 5-mile service area",
            "Show property parcels with sale prices over $1 million",
            "Create a map of hiking trails with elevation profiles",
            "Show all restaurants near public transportation",
            "Map flood zones overlaid with residential areas",
            "Show historic landmarks with photos and descriptions"
        };
    }
}
