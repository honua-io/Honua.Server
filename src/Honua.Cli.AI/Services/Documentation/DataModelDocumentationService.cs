// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Documentation;

/// <summary>
/// Service for documenting data models and schemas.
/// </summary>
public sealed class DataModelDocumentationService
{
    /// <summary>
    /// Generates comprehensive data model documentation including schemas and relationships.
    /// </summary>
    /// <param name="schemaInfo">Schema information as JSON</param>
    /// <returns>JSON containing markdown documentation and generation tools</returns>
    public string DocumentDataModel(string schemaInfo)
    {
        var documentation = @"# Data Model Documentation

## Collections Overview

| Collection ID | Title | Geometry Type | CRS | Description |
|--------------|-------|---------------|-----|-------------|
| buildings | Municipal Buildings | Polygon | EPSG:4326 | Building footprints and attributes |
| roads | Road Network | LineString | EPSG:4326 | Street centerlines and classifications |
| parcels | Land Parcels | Polygon | EPSG:4326 | Property boundaries and ownership |

## Buildings Collection

### Schema

| Field Name | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| id | integer | Yes | Unique identifier | 12345 |
| name | string | No | Building name | ""City Hall"" |
| address | string | Yes | Street address | ""1 Main St"" |
| height | float | No | Height in meters | 45.5 |
| floors | integer | No | Number of floors | 10 |
| year_built | integer | No | Construction year | 1985 |
| use_type | string | Yes | Building use | ""commercial"" |
| geom | geometry | Yes | Building footprint | Polygon |

### Geometry Type
- **Type:** Polygon
- **Coordinate System:** EPSG:4326 (WGS84)
- **Dimensions:** 2D (X, Y)

### Use Type Codes

| Code | Description |
|------|-------------|
| residential | Residential building |
| commercial | Commercial/office building |
| industrial | Industrial/warehouse |
| institutional | School, hospital, government |
| mixed | Mixed-use development |

### Extent
- **Spatial:** [-122.5, 37.7, -122.3, 37.9] (San Francisco)
- **Temporal:** 1900-01-01 to Present

### Data Quality
- **Positional Accuracy:** ±2 meters
- **Completeness:** 98% of structures
- **Update Frequency:** Quarterly
- **Last Updated:** 2024-03-15

## Roads Collection

### Schema

| Field Name | Type | Required | Description |
|-----------|------|----------|-------------|
| id | integer | Yes | Unique identifier |
| name | string | Yes | Street name |
| type | string | Yes | Road classification |
| lanes | integer | No | Number of lanes |
| speed_limit | integer | No | Speed limit (mph) |
| surface | string | No | Pavement type |
| geom | geometry | Yes | Centerline |

### Road Type Classifications

- **highway:** Major highways and freeways
- **arterial:** Primary urban routes
- **collector:** Collector roads
- **local:** Local/residential streets
- **alley:** Alleys and service roads

## Relationships

### Building-to-Parcel

Buildings are spatially related to parcels through:

```sql
SELECT
    b.id AS building_id,
    b.name AS building_name,
    p.id AS parcel_id,
    p.apn AS parcel_number
FROM buildings b
JOIN parcels p ON ST_Within(ST_Centroid(b.geom), p.geom);
```

### Roads-to-Buildings

Find buildings along a road:

```sql
SELECT
    r.name AS road_name,
    b.id AS building_id,
    b.address
FROM roads r
JOIN buildings b ON ST_DWithin(b.geom::geography, r.geom::geography, 50)
WHERE r.id = 123;
```

## API Access

### Query Buildings

```bash
# All buildings
curl 'https://api.example.com/collections/buildings/items'

# Buildings by use type
curl 'https://api.example.com/collections/buildings/items?filter=use_type=''commercial'''

# Tall buildings
curl 'https://api.example.com/collections/buildings/items?filter=height>50'
```

### Query Roads

```bash
# All roads
curl 'https://api.example.com/collections/roads/items'

# Highways only
curl 'https://api.example.com/collections/roads/items?filter=type=''highway'''
```

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.2 | 2024-03-15 | Added 'year_built' field to buildings |
| 1.1 | 2024-01-10 | Updated road classifications |
| 1.0 | 2023-06-01 | Initial schema |

";

        return JsonSerializer.Serialize(new
        {
            markdownDocumentation = documentation,
            generationTools = new[]
            {
                "schemaSpy - Database schema visualization",
                "PostGIS - \\d+ tablename for schema info",
                "JSONSchema - Generate JSON Schema from database",
                "Markdown tables - Manual or auto-generated documentation"
            },
            automation = new
            {
                postgisQuery = @"
-- Generate schema documentation from PostGIS
SELECT
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'buildings'
ORDER BY ordinal_position;",

                jsonSchemaGeneration = "Convert database schema to JSON Schema for API documentation"
            }
        }, CliJsonOptions.Indented);
    }
}
