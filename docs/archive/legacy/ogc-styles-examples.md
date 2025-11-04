# OGC Styles CRUD Examples

This document provides examples for using the OGC Styles CRUD endpoints.

## Endpoints

### Create Style
```
POST /ogc/styles
Authorization: Bearer {token}
Content-Type: application/json
```

### Update Style
```
PUT /ogc/styles/{styleId}
Authorization: Bearer {token}
Content-Type: application/json
```

### Delete Style
```
DELETE /ogc/styles/{styleId}
Authorization: Bearer {token}
```

### Get Style History
```
GET /ogc/styles/{styleId}/history
```

### Get Style Version
```
GET /ogc/styles/{styleId}/versions/{version}
GET /ogc/styles/{styleId}/versions/{version}?f=sld
```

### Validate Style
```
POST /ogc/styles/validate
Content-Type: application/json | application/vnd.ogc.sld+xml | application/vnd.mapbox-style+json
```

## Example 1: Simple Polygon Style (JSON)

```json
{
  "id": "water-bodies",
  "title": "Water Bodies Style",
  "format": "legacy",
  "geometryType": "polygon",
  "renderer": "simple",
  "simple": {
    "fillColor": "#4A90E2",
    "strokeColor": "#1F364D",
    "strokeWidth": 1.5,
    "opacity": 0.8
  }
}
```

## Example 2: UniqueValue Style for Land Use (JSON)

```json
{
  "id": "land-use-classification",
  "title": "Land Use Classification",
  "format": "legacy",
  "geometryType": "polygon",
  "renderer": "uniqueValue",
  "uniqueValue": {
    "field": "land_use_type",
    "defaultSymbol": {
      "fillColor": "#CCCCCC",
      "strokeColor": "#666666",
      "strokeWidth": 1.0
    },
    "classes": [
      {
        "value": "residential",
        "symbol": {
          "fillColor": "#FFFF00",
          "strokeColor": "#CC9900",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "commercial",
        "symbol": {
          "fillColor": "#FF0000",
          "strokeColor": "#990000",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "industrial",
        "symbol": {
          "fillColor": "#800080",
          "strokeColor": "#400040",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "agricultural",
        "symbol": {
          "fillColor": "#00FF00",
          "strokeColor": "#006600",
          "strokeWidth": 1.0
        }
      }
    ]
  }
}
```

## Example 3: Point Style for Points of Interest (JSON)

```json
{
  "id": "poi-markers",
  "title": "Points of Interest Markers",
  "format": "legacy",
  "geometryType": "point",
  "renderer": "simple",
  "simple": {
    "fillColor": "#E74C3C",
    "strokeColor": "#C0392B",
    "strokeWidth": 2.0,
    "size": 16,
    "opacity": 0.9
  }
}
```

## Example 4: Line Style for Roads (JSON)

```json
{
  "id": "road-network",
  "title": "Road Network Style",
  "format": "legacy",
  "geometryType": "line",
  "renderer": "uniqueValue",
  "uniqueValue": {
    "field": "road_class",
    "defaultSymbol": {
      "strokeColor": "#CCCCCC",
      "strokeWidth": 1.0
    },
    "classes": [
      {
        "value": "highway",
        "symbol": {
          "strokeColor": "#E67E22",
          "strokeWidth": 4.0
        }
      },
      {
        "value": "arterial",
        "symbol": {
          "strokeColor": "#F39C12",
          "strokeWidth": 3.0
        }
      },
      {
        "value": "local",
        "symbol": {
          "strokeColor": "#95A5A6",
          "strokeWidth": 1.5
        }
      }
    ]
  }
}
```

## Example 5: SLD XML Style

```xml
<?xml version="1.0" encoding="UTF-8"?>
<StyledLayerDescriptor version="1.0.0"
    xmlns="http://www.opengis.net/sld"
    xmlns:ogc="http://www.opengis.net/ogc"
    xmlns:xlink="http://www.w3.org/1999/xlink">
  <NamedLayer>
    <Name>Parks and Recreation</Name>
    <UserStyle>
      <Name>parks-style</Name>
      <Title>Parks and Recreation Areas</Title>
      <FeatureTypeStyle>
        <Rule>
          <Name>default-park-style</Name>
          <Title>Park Areas</Title>
          <PolygonSymbolizer>
            <Fill>
              <CssParameter name="fill">#90EE90</CssParameter>
              <CssParameter name="fill-opacity">0.7</CssParameter>
            </Fill>
            <Stroke>
              <CssParameter name="stroke">#228B22</CssParameter>
              <CssParameter name="stroke-width">2</CssParameter>
              <CssParameter name="stroke-opacity">1.0</CssParameter>
            </Stroke>
          </PolygonSymbolizer>
        </Rule>
      </FeatureTypeStyle>
    </UserStyle>
  </NamedLayer>
</StyledLayerDescriptor>
```

## Example 6: Mapbox Style JSON

```json
{
  "version": 8,
  "name": "Honua Transportation Network",
  "sources": {
    "roads": {
      "type": "vector",
      "url": "https://example.com/data/roads"
    }
  },
  "layers": [
    {
      "id": "highways",
      "type": "line",
      "source": "roads",
      "source-layer": "roads",
      "filter": ["==", "road_class", "highway"],
      "paint": {
        "line-color": "#E67E22",
        "line-width": 4,
        "line-opacity": 0.9
      }
    },
    {
      "id": "arterials",
      "type": "line",
      "source": "roads",
      "source-layer": "roads",
      "filter": ["==", "road_class", "arterial"],
      "paint": {
        "line-color": "#F39C12",
        "line-width": 3,
        "line-opacity": 0.85
      }
    },
    {
      "id": "local-roads",
      "type": "line",
      "source": "roads",
      "source-layer": "roads",
      "filter": ["==", "road_class", "local"],
      "paint": {
        "line-color": "#95A5A6",
        "line-width": 1.5,
        "line-opacity": 0.8
      }
    }
  ]
}
```

## Example 7: CartoCSS Style

```css
/* Water bodies styling */
#water_bodies {
  polygon-fill: #4A90E2;
  polygon-opacity: 0.8;
  line-color: #1F364D;
  line-width: 1.5;
}

/* Conditional styling based on attribute */
#water_bodies[type='lake'] {
  polygon-fill: #3498DB;
}

#water_bodies[type='river'] {
  polygon-fill: #5DADE2;
  line-width: 2.0;
}

/* Scale-dependent styling */
#water_bodies[@scale > 50000] {
  polygon-opacity: 0.6;
  line-width: 1.0;
}
```

## Usage Examples

### Creating a Style via cURL

```bash
curl -X POST https://example.com/ogc/styles \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "custom-water-style",
    "title": "Custom Water Bodies",
    "format": "legacy",
    "geometryType": "polygon",
    "renderer": "simple",
    "simple": {
      "fillColor": "#4A90E2",
      "strokeColor": "#1F364D",
      "strokeWidth": 1.5,
      "opacity": 0.8
    }
  }'
```

### Updating a Style

```bash
curl -X PUT https://example.com/ogc/styles/custom-water-style \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "custom-water-style",
    "title": "Updated Water Bodies",
    "format": "legacy",
    "geometryType": "polygon",
    "renderer": "simple",
    "simple": {
      "fillColor": "#3498DB",
      "strokeColor": "#2C3E50",
      "strokeWidth": 2.0,
      "opacity": 0.9
    }
  }'
```

### Deleting a Style

```bash
curl -X DELETE https://example.com/ogc/styles/custom-water-style \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Getting Style History

```bash
curl https://example.com/ogc/styles/custom-water-style/history
```

### Getting a Specific Version

```bash
# Get as JSON
curl https://example.com/ogc/styles/custom-water-style/versions/1

# Get as SLD XML
curl https://example.com/ogc/styles/custom-water-style/versions/1?f=sld
```

### Validating a Style

```bash
# Validate JSON style
curl -X POST https://example.com/ogc/styles/validate \
  -H "Content-Type: application/json" \
  -d '{
    "id": "test-style",
    "format": "legacy",
    "geometryType": "polygon",
    "renderer": "simple",
    "simple": {
      "fillColor": "#4A90E2"
    }
  }'

# Validate SLD XML
curl -X POST https://example.com/ogc/styles/validate \
  -H "Content-Type: application/vnd.ogc.sld+xml" \
  --data-binary @style.sld
```

## Response Examples

### Successful Creation (201 Created)

```json
{
  "id": "custom-water-style",
  "title": "Custom Water Bodies",
  "format": "legacy",
  "geometryType": "polygon",
  "renderer": "simple",
  "warnings": null,
  "links": [
    {
      "href": "https://example.com/ogc/styles/custom-water-style",
      "rel": "self",
      "type": "application/json",
      "title": "Custom Water Bodies"
    }
  ]
}
```

### Style History Response (200 OK)

```json
{
  "styleId": "custom-water-style",
  "totalVersions": 3,
  "versions": [
    {
      "version": 1,
      "createdAt": "2025-10-18T10:00:00Z",
      "createdBy": "admin",
      "changeDescription": "Initial creation",
      "links": [
        {
          "href": "https://example.com/ogc/styles/custom-water-style/versions/1",
          "rel": "version",
          "type": "application/json",
          "title": "Version 1"
        }
      ]
    },
    {
      "version": 2,
      "createdAt": "2025-10-18T11:30:00Z",
      "createdBy": "admin",
      "changeDescription": "Updated",
      "links": [
        {
          "href": "https://example.com/ogc/styles/custom-water-style/versions/2",
          "rel": "version",
          "type": "application/json",
          "title": "Version 2"
        }
      ]
    },
    {
      "version": 3,
      "createdAt": "2025-10-18T14:15:00Z",
      "createdBy": "admin",
      "changeDescription": "Updated",
      "links": [
        {
          "href": "https://example.com/ogc/styles/custom-water-style/versions/3",
          "rel": "version",
          "type": "application/json",
          "title": "Version 3"
        }
      ]
    }
  ],
  "links": [
    {
      "href": "https://example.com/ogc/styles/custom-water-style/history",
      "rel": "self",
      "type": "application/json",
      "title": "Version History"
    },
    {
      "href": "https://example.com/ogc/styles/custom-water-style",
      "rel": "current",
      "type": "application/json",
      "title": "Current Version"
    }
  ]
}
```

### Validation Response (200 OK)

```json
{
  "valid": true,
  "errors": [],
  "warnings": [
    "Simple symbol has no stroke color. It may not render visibly."
  ],
  "summary": "0 error(s), 1 warning(s)"
}
```

## Authentication

All write operations (POST, PUT, DELETE) require the `RequireDataPublisher` authorization policy. Read operations (GET) require the `RequireViewer` policy.

Example with Bearer token:
```bash
curl -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  https://example.com/ogc/styles
```

## Notes

1. **Versioning**: Every create and update operation creates a new version in the history
2. **Soft Delete**: Delete operations preserve history but remove the current version
3. **Format Support**:
   - `legacy`: Internal Honua format (JSON)
   - `sld`: OGC Styled Layer Descriptor (XML)
   - `mapbox`: Mapbox Style Specification (JSON)
   - `cartocss`: CartoCSS (CSS-like syntax)
4. **Validation**: Use the `/styles/validate` endpoint to check styles before creating them
5. **OpenTelemetry**: All operations emit metrics for monitoring and observability
