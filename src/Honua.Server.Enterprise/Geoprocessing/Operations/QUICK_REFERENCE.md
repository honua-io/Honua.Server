# Geometry Loading Quick Reference

## Input Types

| Type | Description | Example Source |
|------|-------------|----------------|
| `wkt` | Well-Known Text | `"POINT(-122.4194 37.7749)"` |
| `geojson` | GeoJSON geometry or FeatureCollection | `"{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]}"` |
| `collection` | Database table/layer | `"public.buildings"` |
| `url` | Remote HTTP/HTTPS endpoint | `"https://api.example.com/data.geojson"` |

## Quick Examples

### WKT Input
```json
{
  "type": "wkt",
  "source": "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"
}
```

### GeoJSON Input
```json
{
  "type": "geojson",
  "source": "{\"type\":\"Point\",\"coordinates\":[0,0]}"
}
```

### Collection Input
```json
{
  "type": "collection",
  "source": "schema.tablename",
  "filter": "property > 100",
  "parameters": {
    "connectionString": "Host=localhost;Database=db;...",
    "geometryColumn": "geom",
    "maxFeatures": 5000
  }
}
```

### URL Input
```json
{
  "type": "url",
  "source": "https://example.com/features.geojson"
}
```

## Collection Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `connectionString` | Yes* | env var | PostgreSQL connection string |
| `geometryColumn` | No | `"geometry"` | Name of geometry column |
| `maxFeatures` | No | `10000` | Maximum features to load |

*Can use `GEOPROCESSING_CONNECTION_STRING` environment variable

## Common Patterns

### Load from database with filter
```json
{
  "type": "collection",
  "source": "cities",
  "filter": "population > 1000000 AND country = 'USA'"
}
```

### Load FeatureCollection from URL
```json
{
  "type": "url",
  "source": "https://api.example.com/features?bbox=-180,-90,180,90&format=geojson"
}
```

### Mixed input types in one operation
```json
{
  "operation": "union",
  "inputs": [
    { "type": "geojson", "source": "{...}" },
    { "type": "collection", "source": "parcels" },
    { "type": "url", "source": "https://..." }
  ]
}
```

## Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| "Input type cannot be null or empty" | Missing `type` field | Add `type` to input |
| "Input source cannot be null or empty" | Missing `source` field | Add `source` to input |
| "Input type 'X' is not supported" | Unknown type | Use: wkt, geojson, collection, url |
| "Connection string required" | Missing DB connection | Add to parameters or env var |
| "Invalid URL format" | Malformed URL | Check URL syntax |
| "Only HTTP and HTTPS URLs are supported" | Wrong URL scheme | Use http:// or https:// |
| "Invalid WKT format" | Bad WKT syntax | Validate WKT string |
| "Invalid GeoJSON format" | Bad JSON syntax | Validate JSON string |

## Performance Tips

1. **Use filters** to reduce data volume from collections
2. **Set maxFeatures** appropriately for your use case
3. **Prefer direct GeoJSON** over URL for small datasets
4. **Use spatial indexes** on geometry columns in database
5. **Cache frequently accessed** remote URLs locally

## Security Notes

- Table names are validated and quoted automatically
- Filters are sanitized to prevent SQL injection
- Only HTTP/HTTPS URLs are allowed (no file://, ftp://, etc.)
- 30-second timeout on URL requests

## Code Example

```csharp
// Using GeometryLoader directly
var input = new GeoprocessingInput
{
    Type = "collection",
    Source = "public.buildings",
    Filter = "height > 50",
    Parameters = new Dictionary<string, object>
    {
        { "connectionString", "Host=localhost;Database=gis;..." },
        { "geometryColumn", "geom" }
    }
};

var geometries = await GeometryLoader.LoadGeometriesAsync(input, cancellationToken);
```

## See Also

- **Full Documentation:** `GEOMETRY_LOADING_GUIDE.md`
- **Unit Tests:** `GeometryLoaderTests.cs`
- **Implementation:** `GeometryLoader.cs`
