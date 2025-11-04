// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Documentation;

/// <summary>
/// Service for creating user-facing guides and tutorials.
/// </summary>
public sealed class UserGuideService
{
    /// <summary>
    /// Creates comprehensive user guides including quick start, developer guide, and troubleshooting.
    /// </summary>
    /// <param name="deploymentInfo">Deployment information as JSON</param>
    /// <returns>JSON containing various user guide sections</returns>
    public string CreateUserGuide(string deploymentInfo = "{\"environment\":\"development\",\"url\":\"http://localhost:5000\"}")
    {
        var userGuide = new
        {
            quickStartGuide = @"# Honua Geospatial API - Quick Start Guide

## Getting Started

### 1. Access the API

The Honua API is available at: https://api.example.com

No authentication is required for read-only access to public datasets.

### 2. Explore Available Data

List all available collections:

```bash
curl https://api.example.com/collections
```

### 3. Query Features

Get features from a collection:

```bash
curl 'https://api.example.com/collections/buildings/items?limit=10'
```

### 4. Filter by Location

Use bounding box to filter by area:

```bash
curl 'https://api.example.com/collections/buildings/items?bbox=-122.5,37.7,-122.3,37.9&limit=100'
```

## Common Use Cases

### Finding Features Near a Location

```bash
# Get buildings within bounding box around San Francisco
curl 'https://api.example.com/collections/buildings/items?bbox=-122.45,37.75,-122.35,37.85'
```

### Pagination

```bash
# Get first 50 results
curl 'https://api.example.com/collections/buildings/items?limit=50'

# Get next 50 results
curl 'https://api.example.com/collections/buildings/items?limit=50&offset=50'
```

### Filtering by Attributes

```bash
# Buildings taller than 100 meters (using CQL filter)
curl 'https://api.example.com/collections/buildings/items?filter=height>100'
```

## Response Format

All responses are in GeoJSON format:

```json
{
  ""type"": ""FeatureCollection"",
  ""features"": [
    {
      ""type"": ""Feature"",
      ""id"": 1,
      ""geometry"": {
        ""type"": ""Polygon"",
        ""coordinates"": [...]
      },
      ""properties"": {
        ""name"": ""Building Name"",
        ""height"": 150
      }
    }
  ],
  ""links"": [
    {""rel"": ""next"", ""href"": ""...""}
  ]
}
```

## Integration Examples

### JavaScript (Fetch API)

```javascript
async function getFeatures(collectionId, bbox) {
    const url = `https://api.example.com/collections/${collectionId}/items?bbox=${bbox}`;
    const response = await fetch(url);
    const geojson = await response.json();
    return geojson;
}
```

### Python (requests)

```python
import requests

def get_features(collection_id, bbox):
    url = f'https://api.example.com/collections/{collection_id}/items'
    params = {'bbox': bbox, 'limit': 100}
    response = requests.get(url, params=params)
    return response.json()
```

### QGIS

1. Layer > Add Layer > Add WFS Layer
2. New Connection
3. URL: https://api.example.com
4. Version: OGC API Features
5. Connect and select layers

## Support

- API Documentation: https://api.example.com/api-docs
- GitHub: https://github.com/your-org/honua
- Email: support@example.com

",
            developerGuide = @"# Developer Guide

## Authentication

For write access, include bearer token:

```bash
curl -H ""Authorization: Bearer YOUR_TOKEN"" https://api.example.com/collections
```

## Rate Limiting

- Free tier: 100 requests/minute
- Authenticated: 1000 requests/minute
- Enterprise: Contact for custom limits

Rate limit headers in response:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1640000000
```

## Error Handling

Errors return standard HTTP status codes:

- 400: Bad Request (invalid parameters)
- 401: Unauthorized (missing/invalid token)
- 404: Not Found (collection doesn't exist)
- 429: Too Many Requests (rate limit exceeded)
- 500: Internal Server Error

Error response format:
```json
{
  ""code"": ""InvalidParameter"",
  ""description"": ""bbox parameter must have 4 values""
}
```

## Best Practices

1. **Use pagination** - Always specify limit to avoid large responses
2. **Cache responses** - Respect Cache-Control headers
3. **Handle rate limits** - Implement exponential backoff
4. **Validate GeoJSON** - Check response structure before processing
5. **Use bounding box** - Filter spatially when possible

## Advanced Features

### CRS Support

Request features in specific coordinate system:

```bash
curl -H ""Accept-Crs: http://www.opengis.net/def/crs/EPSG/0/3857"" \
     https://api.example.com/collections/buildings/items
```

### Property Selection

Request specific properties only:

```bash
curl 'https://api.example.com/collections/buildings/items?properties=name,height'
```

",
            troubleshootingGuide = @"# Troubleshooting Guide

## Common Issues

### Issue: Empty Response

**Symptom:** API returns empty features array

**Causes:**
- Bounding box doesn't intersect with data
- Filters exclude all features
- Collection has no data

**Solutions:**
```bash
# Check collection extent
curl https://api.example.com/collections/buildings | jq '.extent'

# Try query without filters
curl 'https://api.example.com/collections/buildings/items?limit=10'

# Verify data exists
curl 'https://api.example.com/collections/buildings/items?resulttype=hits'
```

### Issue: Slow Responses

**Symptom:** Requests take > 5 seconds

**Causes:**
- Large bounding box
- No spatial filtering
- High limit value

**Solutions:**
- Reduce bbox size
- Always use limit parameter
- Use offset for pagination
- Filter by attributes first

### Issue: Invalid GeoJSON

**Symptom:** Geometry errors in client

**Causes:**
- Coordinate order confusion (lon,lat vs lat,lon)
- CRS mismatch

**Solutions:**
- OGC API uses lon,lat order (x,y)
- Check Content-Crs header
- Validate with geojson.io

## Getting Help

1. Check API status: https://status.example.com
2. Review documentation: https://api.example.com/api-docs
3. Search GitHub issues: https://github.com/your-org/honua/issues
4. Contact support: support@example.com
"
        };

        return JsonSerializer.Serialize(userGuide, CliJsonOptions.Indented);
    }
}
