# Geometry Loading Guide for Geoprocessing Operations

This guide explains how to load geometries from various sources in geoprocessing operations.

## Supported Input Types

The geoprocessing operations now support four input types:

1. **WKT** (Well-Known Text)
2. **GeoJSON** (including FeatureCollections)
3. **Collection** (Database tables/layers)
4. **URL** (Remote data sources)

## Usage Examples

### 1. WKT Input

Load a single geometry from Well-Known Text format:

```json
{
  "operation": "buffer",
  "inputs": [
    {
      "name": "input",
      "type": "wkt",
      "source": "POINT(-122.4194 37.7749)"
    }
  ],
  "parameters": {
    "distance": 1000,
    "units": "meters"
  }
}
```

### 2. GeoJSON Input

#### Single Geometry

```json
{
  "operation": "buffer",
  "inputs": [
    {
      "name": "input",
      "type": "geojson",
      "source": "{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]}"
    }
  ],
  "parameters": {
    "distance": 1000
  }
}
```

#### FeatureCollection

The loader automatically extracts all features from a FeatureCollection:

```json
{
  "operation": "union",
  "inputs": [
    {
      "name": "input",
      "type": "geojson",
      "source": "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]},\"properties\":{}},{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[-122.4295,37.7849]},\"properties\":{}}]}"
    }
  ],
  "parameters": {}
}
```

#### GeometryCollection

GeometryCollections are automatically decomposed into individual geometries:

```json
{
  "operation": "dissolve",
  "inputs": [
    {
      "name": "input",
      "type": "geojson",
      "source": "{\"type\":\"GeometryCollection\",\"geometries\":[{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]},{\"type\":\"Point\",\"coordinates\":[-122.4295,37.7849]}]}"
    }
  ],
  "parameters": {}
}
```

### 3. Collection Input (Database)

Load geometries from a PostgreSQL/PostGIS table or collection:

```json
{
  "operation": "buffer",
  "inputs": [
    {
      "name": "input",
      "type": "collection",
      "source": "public.cities",
      "filter": "population > 1000000",
      "parameters": {
        "connectionString": "Host=localhost;Database=geodata;Username=user;Password=pass",
        "geometryColumn": "geom",
        "maxFeatures": 5000
      }
    }
  ],
  "parameters": {
    "distance": 5000,
    "units": "meters"
  }
}
```

#### Collection Parameters

- **connectionString** (required): PostgreSQL connection string. Can also be set via the `GEOPROCESSING_CONNECTION_STRING` environment variable.
- **geometryColumn** (optional): Name of the geometry column. Default: `"geometry"`.
- **maxFeatures** (optional): Maximum number of features to load. Default: `10000`.
- **filter** (optional): CQL-like WHERE clause to filter features. Example: `"population > 1000000 AND city_type = 'capital'"`.

#### Schema-Qualified Table Names

Use schema.table format for schema-qualified table names:

```json
{
  "type": "collection",
  "source": "gis_schema.road_network",
  "parameters": {
    "connectionString": "..."
  }
}
```

### 4. URL Input

Load geometries from a remote URL (HTTP/HTTPS):

```json
{
  "operation": "convex_hull",
  "inputs": [
    {
      "name": "input",
      "type": "url",
      "source": "https://api.example.com/features/123/geometry.geojson"
    }
  ],
  "parameters": {}
}
```

The URL loader:
- Accepts both HTTP and HTTPS URLs
- Automatically detects format (GeoJSON or WKT) based on content-type header or content
- Has a 30-second timeout
- Supports both single geometries and FeatureCollections

#### Supported URL Formats

- **GeoJSON**: `https://example.com/data.geojson`
- **WKT**: `https://example.com/data.wkt`
- **APIs**: `https://api.example.com/v1/features?format=geojson`

## Error Handling

The loader provides detailed error messages for common issues:

### Invalid Input

```
Input type cannot be null or empty
Input source cannot be null or empty
```

### Format Errors

```
Invalid WKT format: Expected token 'POINT' but got 'INVALID'
Invalid GeoJSON format: Unexpected character at position 42
```

### Connection Errors

```
Connection string required for collection input. Provide via input.Parameters['connectionString'] or GEOPROCESSING_CONNECTION_STRING environment variable
Database error loading from collection 'cities': relation "cities" does not exist
```

### URL Errors

```
Invalid URL format: not-a-valid-url
Only HTTP and HTTPS URLs are supported. Provided: ftp
HTTP error fetching from URL 'https://example.com/data.json': 404 Not Found
Request to URL 'https://slow-api.com/data' timed out after 30 seconds
```

## Security Considerations

### SQL Injection Prevention

The collection loader implements several security measures:

1. **Table name validation**: Only alphanumeric characters, underscores, dots, and hyphens are allowed
2. **Automatic quoting**: Table names are automatically quoted to prevent injection
3. **Filter sanitization**: Dangerous SQL keywords (DROP, DELETE, etc.) are blocked
4. **Parameterized queries**: Uses Dapper for safe parameter binding

### URL Safety

- Only HTTP and HTTPS schemes are allowed
- 30-second timeout prevents hanging requests
- No redirect following to prevent SSRF attacks

## Performance Considerations

### Collection Loading

- Default limit: 10,000 features
- Use `filter` parameter to reduce data volume
- Create spatial indexes on geometry columns for better performance
- Consider chunking large datasets into multiple jobs

### URL Loading

- 30-second timeout for remote requests
- No retry logic - ensure reliable endpoints
- Consider caching frequently accessed URLs

## Best Practices

1. **Use the most specific input type**: Direct WKT/GeoJSON is faster than collection/URL loading
2. **Limit feature count**: Use filters and maxFeatures to control memory usage
3. **Validate inputs**: Check geometry validity before processing
4. **Use environment variables**: Store connection strings in environment variables, not in job definitions
5. **Monitor timeouts**: Large collections or slow URLs may timeout
6. **Handle errors gracefully**: Wrap operations in try-catch and provide meaningful feedback

## Examples by Operation

### Buffer with Collection Input

```json
{
  "operation": "buffer",
  "inputs": [
    {
      "type": "collection",
      "source": "infrastructure.buildings",
      "filter": "height > 50",
      "parameters": {
        "connectionString": "${DB_CONNECTION}",
        "geometryColumn": "geom"
      }
    }
  ],
  "parameters": {
    "distance": 10,
    "units": "meters",
    "dissolve": true
  }
}
```

### Intersection with URL Inputs

```json
{
  "operation": "intersection",
  "inputs": [
    {
      "type": "url",
      "source": "https://api.example.com/boundaries/city.geojson"
    },
    {
      "type": "url",
      "source": "https://api.example.com/zones/commercial.geojson"
    }
  ],
  "parameters": {}
}
```

### Union with Mixed Inputs

```json
{
  "operation": "union",
  "inputs": [
    {
      "type": "geojson",
      "source": "{...inline GeoJSON...}"
    },
    {
      "type": "collection",
      "source": "public.parcels",
      "parameters": {
        "connectionString": "${DB_CONNECTION}"
      }
    },
    {
      "type": "url",
      "source": "https://api.example.com/additional-features.geojson"
    }
  ],
  "parameters": {}
}
```

## Implementation Details

The geometry loading functionality is implemented in `/src/Honua.Server.Enterprise/Geoprocessing/Operations/GeometryLoader.cs` as a shared static helper class used by all geoprocessing operations.

### Key Features

- **Extensible**: Easy to add new input types
- **Consistent**: All operations use the same loader
- **Robust**: Comprehensive error handling and validation
- **Efficient**: Minimal overhead, streams large datasets
- **Secure**: Built-in SQL injection and SSRF protection

## Troubleshooting

### "No geometries found in collection"

- Verify the table/collection exists
- Check the geometry column name
- Verify the filter doesn't exclude all features
- Ensure geometries are not empty/null

### "Unable to parse response from URL as GeoJSON or WKT"

- Verify the URL returns valid geometry data
- Check the content-type header
- Inspect the response content for format issues

### "Connection string required for collection input"

- Set the connection string in input.Parameters["connectionString"]
- Or set the GEOPROCESSING_CONNECTION_STRING environment variable

## Future Enhancements

Potential future additions:

- Support for WFS (Web Feature Service)
- Support for file uploads (Shapefile, GeoPackage, KML)
- Streaming support for very large datasets
- Geometry caching for repeated loads
- Advanced CQL parser for complex filters
- Support for other databases (MySQL, SQL Server, Oracle)
