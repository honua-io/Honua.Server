---
tags: [esri, arcgis, geoservices, rest-api, feature-server, map-server, image-server, compatibility]
category: api-reference
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# Esri GeoServices REST API Complete Reference

Comprehensive guide to Honua's Esri ArcGIS REST API compatibility layer for FeatureServer, MapServer, and ImageServer.

## Table of Contents
- [Overview](#overview)
- [Service Directory](#service-directory)
- [FeatureServer API](#featureserver-api)
- [MapServer API](#mapserver-api)
- [ImageServer API](#imageserver-api)
- [GeometryServer API](#geometryserver-api)
- [Query Operations](#query-operations)
- [Editing Operations](#editing-operations)
- [Attachments](#attachments)
- [Output Formats](#output-formats)
- [Client Integration](#client-integration)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua implements Esri's GeoServices REST API specification, providing compatibility with ArcGIS clients and applications.

### Why Esri Compatibility?

- **Legacy systems**: Integrate with existing ArcGIS infrastructure
- **Client apps**: Use ArcGIS Online, ArcGIS Pro, Field Maps
- **Developer tools**: Esri JavaScript API, Android/iOS SDKs
- **Industry standard**: Widely adopted in enterprise GIS

### API Version

Honua implements GeoServices REST API **version 10.81**.

### Base URL Structure

```
http://localhost:5000/rest/services/{folder}/{service}/{serverType}
```

**Server Types:**
- `FeatureServer`: Vector features (query, edit, attachments)
- `MapServer`: Rendered maps and feature queries
- `ImageServer`: Raster data access

**Example URLs:**
```
http://localhost:5000/rest/services/Public/Countries/FeatureServer
http://localhost:5000/rest/services/Public/Countries/MapServer
http://localhost:5000/rest/services/Rasters/Elevation/ImageServer
```

## Service Directory

The service directory provides browsable HTML and JSON endpoints.

### Root Directory

**Request:**
```bash
curl http://localhost:5000/rest/services?f=json
```

**Response:**
```json
{
  "currentVersion": 10.81,
  "folders": ["Public", "Rasters"],
  "services": []
}
```

### Folder Contents

**Request:**
```bash
curl http://localhost:5000/rest/services/Public?f=json
```

**Response:**
```json
{
  "currentVersion": 10.81,
  "folders": [],
  "services": [
    {
      "name": "Public/Countries",
      "type": "FeatureServer"
    },
    {
      "name": "Public/Countries",
      "type": "MapServer"
    }
  ]
}
```

## FeatureServer API

FeatureServer provides access to vector features with full CRUD operations.

### Service Metadata

**Request:**
```bash
curl http://localhost:5000/rest/services/Public/Countries/FeatureServer?f=json
```

**Response:**
```json
{
  "currentVersion": 10.81,
  "serviceDescription": "World Countries",
  "hasVersionedData": false,
  "supportsDisconnectedEditing": false,
  "hasStaticData": false,
  "maxRecordCount": 1000,
  "supportedQueryFormats": "JSON, geoJSON",
  "capabilities": "Query,Create,Update,Delete,Uploads,Editing",
  "layers": [
    {
      "id": 0,
      "name": "Countries",
      "type": "Feature Layer",
      "geometryType": "esriGeometryPolygon"
    }
  ],
  "tables": [],
  "enableZDefaults": false,
  "allowGeometryUpdates": true,
  "units": "esriDecimalDegrees"
}
```

### Layer Metadata

**Request:**
```bash
curl http://localhost:5000/rest/services/Public/Countries/FeatureServer/0?f=json
```

**Response:**
```json
{
  "currentVersion": 10.81,
  "id": 0,
  "name": "Countries",
  "type": "Feature Layer",
  "displayField": "name",
  "description": "",
  "copyrightText": "",
  "defaultVisibility": true,
  "editingInfo": {
    "lastEditDate": 1697500000000
  },
  "relationships": [],
  "isDataVersioned": false,
  "supportsRollbackOnFailureParameter": true,
  "supportsStatistics": true,
  "supportsAdvancedQueries": true,
  "geometryType": "esriGeometryPolygon",
  "minScale": 0,
  "maxScale": 0,
  "extent": {
    "xmin": -180,
    "ymin": -90,
    "xmax": 180,
    "ymax": 90,
    "spatialReference": {"wkid": 4326}
  },
  "drawingInfo": {
    "renderer": {
      "type": "simple",
      "symbol": {
        "type": "esriSFS",
        "style": "esriSFSSolid",
        "color": [200, 200, 200, 255]
      }
    }
  },
  "fields": [
    {
      "name": "objectid",
      "type": "esriFieldTypeOID",
      "alias": "Object ID",
      "sqlType": "sqlTypeOther"
    },
    {
      "name": "name",
      "type": "esriFieldTypeString",
      "alias": "Country Name",
      "length": 255,
      "sqlType": "sqlTypeVarchar"
    },
    {
      "name": "population",
      "type": "esriFieldTypeInteger",
      "alias": "Population",
      "sqlType": "sqlTypeInteger"
    }
  ]
}
```

### Query Features

**Basic Query:**
```bash
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query?where=1=1&outFields=*&f=json"
```

**Spatial Query:**
```bash
curl -G "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query" \
  --data-urlencode "geometry={\"xmin\":-180,\"ymin\":-90,\"xmax\":0,\"ymax\":90}" \
  --data-urlencode "geometryType=esriGeometryEnvelope" \
  --data-urlencode "spatialRel=esriSpatialRelIntersects" \
  --data-urlencode "outFields=*" \
  --data-urlencode "f=json"
```

**Attribute Query:**
```bash
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query?where=population>10000000&outFields=name,population&f=json"
```

**Query with Statistics:**
```bash
curl -G "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query" \
  --data-urlencode "where=1=1" \
  --data-urlencode "outStatistics=[{\"statisticType\":\"sum\",\"onStatisticField\":\"population\",\"outStatisticFieldName\":\"total_pop\"}]" \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "objectIdFieldName": "objectid",
  "globalIdFieldName": "",
  "geometryType": "esriGeometryPolygon",
  "spatialReference": {"wkid": 4326},
  "fields": [...],
  "features": [
    {
      "attributes": {
        "objectid": 1,
        "name": "United States",
        "population": 331000000
      },
      "geometry": {
        "rings": [[[...]]]
      }
    }
  ]
}
```

### Query Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `where` | SQL WHERE clause | `population > 1000000` |
| `objectIds` | Comma-separated IDs | `1,2,3,4,5` |
| `geometry` | Spatial filter | `{"xmin":...}` |
| `geometryType` | Geometry type | `esriGeometryEnvelope` |
| `spatialRel` | Spatial relationship | `esriSpatialRelIntersects` |
| `outFields` | Fields to return | `*` or `name,pop` |
| `returnGeometry` | Include geometry | `true` (default) |
| `returnCountOnly` | Count only | `false` |
| `returnIdsOnly` | IDs only | `false` |
| `outSR` | Output spatial reference | `4326` |
| `resultRecordCount` | Max records | `1000` |
| `resultOffset` | Pagination offset | `0` |
| `orderByFields` | Sort order | `name ASC` |
| `f` | Output format | `json`, `geojson` |

### Add Features

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/addFeatures" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode 'features=[{
    "attributes": {
      "name": "New Country",
      "population": 1000000
    },
    "geometry": {
      "rings": [[[-100, 30], [-100, 31], [-99, 31], [-99, 30], [-100, 30]]]
    }
  }]' \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "addResults": [
    {
      "objectId": 123,
      "globalId": null,
      "success": true
    }
  ]
}
```

### Update Features

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/updateFeatures" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode 'features=[{
    "attributes": {
      "objectid": 123,
      "population": 1500000
    }
  }]' \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "updateResults": [
    {
      "objectId": 123,
      "globalId": null,
      "success": true
    }
  ]
}
```

### Delete Features

**By ObjectIds:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/deleteFeatures" \
  --data "objectIds=123,124,125" \
  --data "f=json"
```

**By Where Clause:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/deleteFeatures" \
  --data "where=population<1000" \
  --data "f=json"
```

**Response:**
```json
{
  "deleteResults": [
    {"objectId": 123, "globalId": null, "success": true},
    {"objectId": 124, "globalId": null, "success": true}
  ]
}
```

### Apply Edits (Batch)

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/applyEdits" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode 'adds=[{"attributes":{"name":"Country A"},"geometry":{...}}]' \
  --data-urlencode 'updates=[{"attributes":{"objectid":1,"population":2000000}}]' \
  --data-urlencode 'deletes=[2,3,4]' \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "addResults": [...],
  "updateResults": [...],
  "deleteResults": [...]
}
```

## MapServer API

MapServer provides rendered map images and feature queries.

### Export Map

**Request:**
```bash
curl -G "http://localhost:5000/rest/services/Public/Countries/MapServer/export" \
  --data-urlencode "bbox=-180,-90,180,90" \
  --data-urlencode "size=800,400" \
  --data-urlencode "dpi=96" \
  --data-urlencode "imageSR=4326" \
  --data-urlencode "bboxSR=4326" \
  --data-urlencode "format=png" \
  --data-urlencode "transparent=true" \
  --data-urlencode "f=image"
```

**Returns:** PNG image

**Parameters:**
- `bbox`: Bounding box (xmin,ymin,xmax,ymax)
- `size`: Image size (width,height)
- `dpi`: Dots per inch
- `imageSR`: Output spatial reference
- `format`: `png`, `jpg`, `png8`, `png24`, `png32`
- `layers`: Layer visibility (e.g., `show:0,1,2`)

### Identify Features

**Request:**
```bash
curl -G "http://localhost:5000/rest/services/Public/Countries/MapServer/identify" \
  --data-urlencode "geometry={\"x\":-100,\"y\":40}" \
  --data-urlencode "geometryType=esriGeometryPoint" \
  --data-urlencode "sr=4326" \
  --data-urlencode "layers=all:0" \
  --data-urlencode "tolerance=5" \
  --data-urlencode "mapExtent=-180,-90,180,90" \
  --data-urlencode "imageDisplay=800,600,96" \
  --data-urlencode "returnGeometry=true" \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "results": [
    {
      "layerId": 0,
      "layerName": "Countries",
      "value": "1",
      "displayFieldName": "name",
      "attributes": {
        "objectid": 1,
        "name": "United States",
        "population": 331000000
      },
      "geometryType": "esriGeometryPolygon",
      "geometry": {...}
    }
  ]
}
```

### Find

**Search by attribute:**
```bash
curl -G "http://localhost:5000/rest/services/Public/Countries/MapServer/find" \
  --data-urlencode "searchText=United" \
  --data-urlencode "contains=true" \
  --data-urlencode "searchFields=name" \
  --data-urlencode "layers=0" \
  --data-urlencode "returnGeometry=false" \
  --data-urlencode "f=json"
```

## ImageServer API

ImageServer provides access to raster datasets.

### Service Info

**Request:**
```bash
curl http://localhost:5000/rest/services/Rasters/Elevation/ImageServer?f=json
```

**Response:**
```json
{
  "currentVersion": 10.81,
  "serviceDescription": "Elevation Data",
  "name": "Elevation",
  "description": "Digital Elevation Model",
  "extent": {
    "xmin": -180,
    "ymin": -90,
    "xmax": 180,
    "ymax": 90,
    "spatialReference": {"wkid": 4326}
  },
  "pixelSizeX": 0.0027777777777778,
  "pixelSizeY": 0.0027777777777778,
  "bandCount": 1,
  "pixelType": "F32",
  "minPixelSize": 0,
  "maxPixelSize": 0,
  "copyrightText": "",
  "serviceDataType": "esriImageServiceDataTypeElevation",
  "minValues": [0],
  "maxValues": [8850],
  "meanValues": [450],
  "stdvValues": [300],
  "objectIdField": "",
  "fields": [],
  "capabilities": "Image,Metadata,Mensuration"
}
```

### Export Image

**Request:**
```bash
curl -G "http://localhost:5000/rest/services/Rasters/Elevation/ImageServer/exportImage" \
  --data-urlencode "bbox=-120,35,-115,40" \
  --data-urlencode "size=512,512" \
  --data-urlencode "imageSR=4326" \
  --data-urlencode "bboxSR=4326" \
  --data-urlencode "format=tiff" \
  --data-urlencode "pixelType=F32" \
  --data-urlencode "noData=0" \
  --data-urlencode "interpolation=RSP_BilinearInterpolation" \
  --data-urlencode "f=image"
```

**Returns:** GeoTIFF file

### Identify (Sample)

**Request:**
```bash
curl -G "http://localhost:5000/rest/services/Rasters/Elevation/ImageServer/identify" \
  --data-urlencode "geometry={\"x\":-120,\"y\":38}" \
  --data-urlencode "geometryType=esriGeometryPoint" \
  --data-urlencode "returnGeometry=false" \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "objectId": -1,
  "name": "Pixel",
  "value": "1250.5",
  "location": {
    "x": -120,
    "y": 38,
    "spatialReference": {"wkid": 4326}
  },
  "properties": {
    "Values": [1250.5]
  }
}
```

## GeometryServer API

GeometryServer provides geometry operations.

### Project

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Geometry/GeometryServer/project" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode 'geometries={"geometryType":"esriGeometryPoint","geometries":[{"x":-118.15,"y":33.80}]}' \
  --data-urlencode "inSR=4326" \
  --data-urlencode "outSR=3857" \
  --data-urlencode "f=json"
```

**Response:**
```json
{
  "geometries": [
    {
      "x": -13149614.849955,
      "y": 4021262.595804
    }
  ]
}
```

### Buffer

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Geometry/GeometryServer/buffer" \
  --data-urlencode 'geometries={"geometryType":"esriGeometryPoint","geometries":[{"x":-118.15,"y":33.80}]}' \
  --data-urlencode "inSR=4326" \
  --data-urlencode "distances=100" \
  --data-urlencode "unit=9001" \
  --data-urlencode "outSR=4326" \
  --data-urlencode "f=json"
```

## Query Operations

### Supported Operators

**Comparison:**
- `=`, `<>`, `>`, `>=`, `<`, `<=`
- `IS NULL`, `IS NOT NULL`

**Logical:**
- `AND`, `OR`, `NOT`

**String:**
- `LIKE` with wildcards (`%`, `_`)
- `IN` (value list)

**Spatial:**
- `esriSpatialRelIntersects`
- `esriSpatialRelContains`
- `esriSpatialRelWithin`
- `esriSpatialRelTouches`
- `esriSpatialRelOverlaps`
- `esriSpatialRelCrosses`

### Query Examples

**Complex WHERE:**
```bash
where=population > 1000000 AND name LIKE 'United%' AND status = 'active'
```

**IN Clause:**
```bash
where=country_code IN ('US', 'CA', 'MX')
```

**Date Query:**
```bash
where=last_update > DATE '2025-01-01'
```

**Spatial + Attribute:**
```bash
geometry={"xmin":-120,"ymin":30,"xmax":-100,"ymax":40}&geometryType=esriGeometryEnvelope&where=population>1000000
```

## Editing Operations

### Transaction Support

Honua supports transactional edits with rollback on failure.

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/applyEdits" \
  --data-urlencode 'adds=[...]' \
  --data-urlencode 'updates=[...]' \
  --data-urlencode 'deletes=[...]' \
  --data-urlencode "rollbackOnFailure=true" \
  --data-urlencode "f=json"
```

If any operation fails, all changes are rolled back.

### Global IDs

Honua supports GlobalID tracking for offline editing:

```json
{
  "attributes": {
    "objectid": 123,
    "globalId": "{12345678-1234-1234-1234-123456789012}",
    "name": "Feature"
  }
}
```

## Attachments

FeatureServer supports file attachments.

### Query Attachments

**Request:**
```bash
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/123/attachments?f=json"
```

**Response:**
```json
{
  "attachmentInfos": [
    {
      "id": 1,
      "contentType": "image/jpeg",
      "size": 45678,
      "name": "photo.jpg"
    }
  ]
}
```

### Add Attachment

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/123/addAttachment" \
  -F "attachment=@photo.jpg" \
  -F "f=json"
```

**Response:**
```json
{
  "addAttachmentResult": {
    "objectId": 123,
    "attachmentId": 1,
    "globalId": null,
    "success": true
  }
}
```

### Download Attachment

**Request:**
```bash
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/123/attachments/1" \
  -o photo.jpg
```

### Delete Attachment

**Request:**
```bash
curl -X POST "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/123/deleteAttachments" \
  --data "attachmentIds=1" \
  --data "f=json"
```

## Output Formats

### Supported Formats

| Format | Parameter | Content Type | Use Case |
|--------|-----------|--------------|----------|
| JSON | `f=json` | `application/json` | Default, web apps |
| GeoJSON | `f=geojson` | `application/geo+json` | Modern web mapping |
| PGeoJSON | `f=pgeojson` | `application/geo+json` | Compact GeoJSON |
| HTML | `f=html` | `text/html` | Browser viewing |
| Image | `f=image` | `image/png` | Map export |

### GeoJSON Output

**Request:**
```bash
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query?where=1=1&outFields=*&f=geojson"
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": 1,
      "properties": {
        "name": "United States",
        "population": 331000000
      },
      "geometry": {
        "type": "MultiPolygon",
        "coordinates": [[[[...]]]
      }
    }
  ]
}
```

## Client Integration

### ArcGIS JavaScript API

```javascript
require([
  "esri/layers/FeatureLayer",
  "esri/Map",
  "esri/views/MapView"
], function(FeatureLayer, Map, MapView) {

  const featureLayer = new FeatureLayer({
    url: "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0",
    outFields: ["*"],
    popupTemplate: {
      title: "{name}",
      content: "Population: {population}"
    }
  });

  const map = new Map({
    basemap: "topo-vector",
    layers: [featureLayer]
  });

  const view = new MapView({
    container: "viewDiv",
    map: map,
    center: [-100, 40],
    zoom: 4
  });
});
```

### ArcGIS Pro

1. Add Data → Add Data from Path
2. Enter URL: `http://localhost:5000/rest/services/Public/Countries/FeatureServer`
3. Select layer and add to map

### Esri Leaflet

```javascript
const map = L.map('map').setView([40, -100], 4);

L.esri.featureLayer({
  url: 'http://localhost:5000/rest/services/Public/Countries/FeatureServer/0',
  style: function() {
    return { color: '#70ca49', weight: 2 };
  }
}).addTo(map);
```

### Field Maps / Survey123

Configure feature service URL in app settings:
```
http://localhost:5000/rest/services/Public/Survey/FeatureServer
```

## Troubleshooting

### Issue: Service Not Found

**Symptoms:** 404 error on service URL.

**Solutions:**
1. Verify folder and service names in `metadata.yaml`
2. Check service is published
3. Verify URL structure: `/rest/services/{folder}/{service}/{serverType}`

```bash
# List all services
curl http://localhost:5000/rest/services?f=json
```

### Issue: Empty Query Results

**Symptoms:** Query returns no features.

**Solutions:**
1. Check `where` clause syntax
2. Verify spatial filter geometry
3. Check `outSR` matches data CRS
4. Verify `resultRecordCount` isn't too low

```bash
# Debug: Get count
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer/0/query?where=1=1&returnCountOnly=true&f=json"
```

### Issue: Edit Fails

**Symptoms:** addFeatures/updateFeatures returns success:false.

**Solutions:**
1. Verify user has Editor role
2. Check required fields are provided
3. Validate geometry format
4. Check for constraint violations

```bash
# Check editing capabilities
curl "http://localhost:5000/rest/services/Public/Countries/FeatureServer?f=json" | grep capabilities
```

### Issue: Attachment Upload Fails

**Symptoms:** 400/500 error on attachment upload.

**Solutions:**
1. Verify feature exists
2. Check file size limits
3. Validate content type
4. Ensure storage is configured

```bash
# Check max file size
honua config show | grep maxBodySize
```

## Related Documentation

- [OGC API Features](./03-01-ogc-api-features.md) - Modern REST API
- [Export Formats](./03-04-export-formats.md) - Output format details
- [Authentication](./02-02-authentication-setup.md) - API security
- [Configuration](./02-01-configuration-reference.md) - Service setup

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**Esri Compatibility**: GeoServices REST API 10.81
