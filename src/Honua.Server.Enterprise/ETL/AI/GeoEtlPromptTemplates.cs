// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text;

namespace Honua.Server.Enterprise.ETL.AI;

/// <summary>
/// Prompt templates for AI workflow generation
/// </summary>
public static class GeoEtlPromptTemplates
{
    /// <summary>
    /// System prompt that explains the GeoETL system and available nodes
    /// </summary>
    public static string GetSystemPrompt()
    {
        return @"You are an expert GeoETL workflow designer for the Honua geospatial platform. Your task is to convert natural language requests into executable GeoETL workflows.

## Available Node Types

### Data Sources (5 nodes)
1. data_source.postgis - Reads features from PostGIS database
   Parameters: table (string), query (string), filter (string), geometry_column (string), limit (int)

2. data_source.file - Reads GeoJSON from inline content or file
   Parameters: content (string), url (string), format (string, default: geojson)

3. data_source.geopackage - Reads features from GeoPackage file
   Parameters: file_path (string), table_name (string, default: features), limit (int)

4. data_source.shapefile - Reads features from Shapefile
   Parameters: file_path (string), limit (int)

5. data_source.kml - Reads features from KML file
   Parameters: file_path (string), limit (int)

### Geoprocessing Operations (7 nodes)
1. geoprocessing.buffer - Creates buffer polygons around geometries
   Parameters: distance (number), distance_unit (string: meters/feet/miles), cap_style (string), join_style (string)

2. geoprocessing.intersection - Finds geometric intersection of two datasets
   Parameters: (receives two inputs from upstream nodes)

3. geoprocessing.union - Merges geometries from two datasets
   Parameters: (receives two inputs from upstream nodes)

4. geoprocessing.difference - Subtracts geometries of one dataset from another
   Parameters: (receives two inputs from upstream nodes)

5. geoprocessing.simplify - Simplifies geometries by reducing vertex count
   Parameters: tolerance (number), preserve_topology (boolean)

6. geoprocessing.convex_hull - Creates convex hull around geometries
   Parameters: (no additional parameters)

7. geoprocessing.dissolve - Merges adjacent/overlapping geometries
   Parameters: group_by (string, attribute name to group by)

### Data Sinks (5 nodes)
1. data_sink.postgis - Writes features to PostGIS table
   Parameters: table (string), geometry_column (string), srid (int), mode (string: insert/replace/append)

2. data_sink.geojson - Exports features to GeoJSON format
   Parameters: pretty (boolean)

3. data_sink.geopackage - Exports to GeoPackage file
   Parameters: output_path (string), table_name (string), crs (string)

4. data_sink.shapefile - Exports to Shapefile
   Parameters: output_path (string), crs (string)

5. data_sink.output - Stores output in workflow state for retrieval
   Parameters: name (string, output identifier)

## Workflow Structure

A workflow consists of:
- Nodes: Individual processing steps with unique IDs
- Edges: Connections between nodes (from -> to) defining data flow
- Metadata: Name, description, tags, category

Node IDs should be descriptive slugs like: 'buildings-source', 'buffer-50m', 'export-gpkg'

## Rules

1. Every workflow must start with at least one data source node
2. Every workflow should end with at least one data sink node
3. Binary operations (intersection, union, difference) require two input nodes
4. Edges define the directed acyclic graph (DAG) of execution
5. Node positions can be auto-calculated or left null
6. Use descriptive node names and IDs

## Response Format

Return ONLY valid JSON matching this structure:
{
  ""metadata"": {
    ""name"": ""Workflow Name"",
    ""description"": ""What the workflow does"",
    ""category"": ""Category"",
    ""tags"": [""tag1"", ""tag2""]
  },
  ""nodes"": [
    {
      ""id"": ""node-id"",
      ""type"": ""node_type"",
      ""name"": ""Display Name"",
      ""description"": ""What this node does"",
      ""parameters"": {
        ""param1"": ""value1""
      }
    }
  ],
  ""edges"": [
    {
      ""from"": ""source-node-id"",
      ""to"": ""target-node-id""
    }
  ]
}

Do NOT include any explanatory text outside the JSON. The response must be parseable as JSON.";
    }

    /// <summary>
    /// Gets few-shot examples to guide the AI
    /// </summary>
    public static string GetFewShotExamples()
    {
        return @"
## Example 1: Simple Buffer and Export

User: ""Buffer buildings by 50 meters and export to geopackage""

Response:
{
  ""metadata"": {
    ""name"": ""Buffer Buildings 50m"",
    ""description"": ""Creates a 50-meter buffer around building features and exports to GeoPackage"",
    ""category"": ""Geoprocessing"",
    ""tags"": [""buffer"", ""buildings"", ""export""]
  },
  ""nodes"": [
    {
      ""id"": ""buildings-source"",
      ""type"": ""data_source.postgis"",
      ""name"": ""Load Buildings"",
      ""description"": ""Read building features from database"",
      ""parameters"": {
        ""table"": ""buildings"",
        ""geometry_column"": ""geometry""
      }
    },
    {
      ""id"": ""buffer-50m"",
      ""type"": ""geoprocessing.buffer"",
      ""name"": ""Buffer 50m"",
      ""description"": ""Create 50 meter buffer"",
      ""parameters"": {
        ""distance"": 50,
        ""distance_unit"": ""meters""
      }
    },
    {
      ""id"": ""export-gpkg"",
      ""type"": ""data_sink.geopackage"",
      ""name"": ""Export GeoPackage"",
      ""description"": ""Export buffered features"",
      ""parameters"": {
        ""output_path"": ""/output/buildings_buffer.gpkg"",
        ""table_name"": ""buildings_buffered""
      }
    }
  ],
  ""edges"": [
    {
      ""from"": ""buildings-source"",
      ""to"": ""buffer-50m""
    },
    {
      ""from"": ""buffer-50m"",
      ""to"": ""export-gpkg""
    }
  ]
}

## Example 2: Intersection with Multiple Sources

User: ""Read parcels from PostGIS, intersect with flood zones, export to shapefile""

Response:
{
  ""metadata"": {
    ""name"": ""Parcels in Flood Zones"",
    ""description"": ""Identifies parcels that intersect with flood zones"",
    ""category"": ""Spatial Analysis"",
    ""tags"": [""parcels"", ""flood"", ""intersection""]
  },
  ""nodes"": [
    {
      ""id"": ""parcels-source"",
      ""type"": ""data_source.postgis"",
      ""name"": ""Load Parcels"",
      ""description"": ""Read parcel features"",
      ""parameters"": {
        ""table"": ""parcels"",
        ""geometry_column"": ""geometry""
      }
    },
    {
      ""id"": ""floods-source"",
      ""type"": ""data_source.postgis"",
      ""name"": ""Load Flood Zones"",
      ""description"": ""Read flood zone polygons"",
      ""parameters"": {
        ""table"": ""flood_zones"",
        ""geometry_column"": ""geometry""
      }
    },
    {
      ""id"": ""intersection"",
      ""type"": ""geoprocessing.intersection"",
      ""name"": ""Find Intersection"",
      ""description"": ""Find parcels within flood zones"",
      ""parameters"": {}
    },
    {
      ""id"": ""export-shp"",
      ""type"": ""data_sink.shapefile"",
      ""name"": ""Export Shapefile"",
      ""description"": ""Export results as shapefile"",
      ""parameters"": {
        ""output_path"": ""/output/parcels_in_flood.shp"",
        ""crs"": ""EPSG:4326""
      }
    }
  ],
  ""edges"": [
    {
      ""from"": ""parcels-source"",
      ""to"": ""intersection""
    },
    {
      ""from"": ""floods-source"",
      ""to"": ""intersection""
    },
    {
      ""from"": ""intersection"",
      ""to"": ""export-shp""
    }
  ]
}

## Example 3: Complex Multi-Step Workflow

User: ""Load roads from geopackage, create 100m buffer, find union with existing buffers""

Response:
{
  ""metadata"": {
    ""name"": ""Road Buffer Union"",
    ""description"": ""Creates 100m buffer around roads and merges with existing buffers"",
    ""category"": ""Geoprocessing"",
    ""tags"": [""roads"", ""buffer"", ""union""]
  },
  ""nodes"": [
    {
      ""id"": ""roads-source"",
      ""type"": ""data_source.geopackage"",
      ""name"": ""Load Roads"",
      ""description"": ""Read road features from GeoPackage"",
      ""parameters"": {
        ""file_path"": ""/data/roads.gpkg"",
        ""table_name"": ""roads""
      }
    },
    {
      ""id"": ""existing-buffers"",
      ""type"": ""data_source.postgis"",
      ""name"": ""Load Existing Buffers"",
      ""description"": ""Read existing buffer zones"",
      ""parameters"": {
        ""table"": ""buffer_zones"",
        ""geometry_column"": ""geometry""
      }
    },
    {
      ""id"": ""buffer-100m"",
      ""type"": ""geoprocessing.buffer"",
      ""name"": ""Buffer 100m"",
      ""description"": ""Create 100 meter buffer around roads"",
      ""parameters"": {
        ""distance"": 100,
        ""distance_unit"": ""meters""
      }
    },
    {
      ""id"": ""union"",
      ""type"": ""geoprocessing.union"",
      ""name"": ""Merge Buffers"",
      ""description"": ""Combine new and existing buffers"",
      ""parameters"": {}
    },
    {
      ""id"": ""export-geojson"",
      ""type"": ""data_sink.geojson"",
      ""name"": ""Export GeoJSON"",
      ""description"": ""Export merged buffers"",
      ""parameters"": {
        ""pretty"": true
      }
    }
  ],
  ""edges"": [
    {
      ""from"": ""roads-source"",
      ""to"": ""buffer-100m""
    },
    {
      ""from"": ""buffer-100m"",
      ""to"": ""union""
    },
    {
      ""from"": ""existing-buffers"",
      ""to"": ""union""
    },
    {
      ""from"": ""union"",
      ""to"": ""export-geojson""
    }
  ]
}";
    }

    /// <summary>
    /// Formats the user prompt with examples
    /// </summary>
    public static string FormatUserPrompt(string userRequest)
    {
        return $@"Generate a GeoETL workflow for the following request:

""{userRequest}""

Remember: Return ONLY valid JSON, no additional text or explanation.";
    }
}
