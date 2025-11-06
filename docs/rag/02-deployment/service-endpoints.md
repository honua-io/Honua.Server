# Honua Service Endpoints and API Reference

**Keywords**: ogc-api, ogc-api-features, wfs, wms, wmts, tiles-api, esri-rest, feature-service, map-service, image-service, geoservices, odata, stac, carto, endpoints, api-reference, service-endpoints, geospatial-api, spatial-services, web-services, rest-api, ogc-services

This document provides comprehensive reference for all Honua service endpoints, query parameters, request formats, and response structures. Use this guide to integrate client applications with Honua's multi-protocol geospatial services.

---

## Table of Contents

1. [Service Overview](#service-overview)
2. [Base URLs and Routing](#base-urls-and-routing)
3. [OGC API Features](#ogc-api-features)
4. [OGC WFS (Web Feature Service)](#ogc-wfs-web-feature-service)
5. [OGC WMS (Web Map Service)](#ogc-wms-web-map-service)
6. [OGC Tiles API](#ogc-tiles-api)
7. [Geoservices REST a.k.a. Esri REST Services](#esri-rest-services)
8. [OData v4 API](#odata-v4-api)
9. [STAC API](#stac-api)
10. [Carto API](#carto-api)
11. [OGC API Records](#ogc-api-records)
12. [Geometry Service](#geometry-service)
13. [Catalog and Discovery](#catalog-and-discovery)
14. [Authentication](#authentication)
15. [Administrative API](#administrative-api)
16. [Response Formats](#response-formats)
17. [Error Handling](#error-handling)

---

## Service Overview

Honua provides a comprehensive suite of geospatial service protocols:

### Available Protocols

| Protocol | Base Path | Purpose | Enabled By Default |
|----------|-----------|---------|-------------------|
| OGC API Features | `/ogc` | Modern RESTful feature access with CQL filtering | Yes |
| OGC WFS 2.0 | `/wfs` | Standards-based feature queries and transactions | Yes |
| OGC WMS 1.3.0 | `/wms` | Map image rendering and feature info | Yes |
| OGC Tiles API | `/ogc/collections/{id}/tiles` | Vector and raster tiles | Yes |
| Geoservices REST a.k.a. Esri REST | `/rest/services` | ArcGIS-compatible services | Yes |
| OData v4 | `/odata` | Entity framework queries with spatial support | Optional |
| STAC API | `/stac` | SpatioTemporal Asset Catalog | Optional |
| Carto API | `/carto` | SQL-based analytics | Optional |
| OGC Records | `/records` | Metadata catalog | Optional |
| Geometry Service | `/rest/services/Geometry/GeometryServer` | Spatial operations | Optional |

### Service Enablement

Configure service availability in `appsettings.json`:

```json
{
  "honua": {
    "services": {
      "wfs": { "enabled": true },
      "wms": { "enabled": true },
      "stac": { "enabled": false },
      "geometry": {
        "enabled": false,
        "enableGdalOperations": false
      }
    }
  }
}
```

### Authentication and Authorization

All endpoints respect Honua's authentication policies:

- **RequireViewer**: Read-only access (default for most endpoints)
- **RequireDataPublisher**: Create, update, delete operations
- **RequireAdministrator**: Administrative operations

See [Authentication](#authentication) section for token-based access.

---

## Base URLs and Routing

### Standard Base URL Pattern

```
{scheme}://{host}[:{port}][/{pathBase}]/{service}
```

Examples:
- `https://geo.example.com/ogc`
- `https://api.example.com:8443/honua/rest/services`
- `http://localhost:5000/wfs`

### Content Negotiation

Honua supports HTTP content negotiation via:

1. **Accept Header**: `Accept: application/geo+json`
2. **Query Parameter**: `?f=geojson` or `?format=json`

Supported formats include: `json`, `geojson`, `html`, `gml`, `xml`, `kml`, `kmz`, `csv`, `geopackage`, `shapefile`, `mvt`, `png`, `jpeg`, `webp`

---

## OGC API Features

Modern RESTful API for feature data following OGC API standards.

### Conformance Classes

Honua implements:
- Core (`http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core`)
- GeoJSON (`http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson`)
- HTML (`http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/html`)
- CQL2 Text (`http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql-text`)
- CQL2 JSON (`http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql2-json`)
- Transactions (Create, Update, Delete)

### Landing Page

**Endpoint**: `GET /ogc`

**Description**: Service landing page with catalog metadata and available links.

**Example Request**:
```bash
curl "https://localhost:5000/ogc" \
  -H "Accept: application/json"
```

**Response**:
```json
{
  "catalog": {
    "id": "honua-catalog",
    "title": "Honua Geospatial Services",
    "description": "Multi-protocol geospatial data services",
    "version": "1.0.0"
  },
  "services": [
    {
      "id": "transportation",
      "title": "Transportation Infrastructure",
      "folderId": "Public",
      "serviceType": "FeatureServer"
    }
  ],
  "links": [
    {
      "href": "https://localhost:5000/ogc",
      "rel": "self",
      "type": "application/json",
      "title": "OGC API landing"
    },
    {
      "href": "https://localhost:5000/ogc/conformance",
      "rel": "conformance",
      "type": "application/json",
      "title": "Conformance"
    },
    {
      "href": "https://localhost:5000/ogc/collections",
      "rel": "data",
      "type": "application/json",
      "title": "Collections"
    }
  ]
}
```

### Conformance Declaration

**Endpoint**: `GET /ogc/conformance`

**Example Request**:
```bash
curl "https://localhost:5000/ogc/conformance"
```

**Response**:
```json
{
  "conformsTo": [
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
    "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter-cql-text"
  ]
}
```

### Collections

**Endpoint**: `GET /ogc/collections`

**Query Parameters**:

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `limit` | integer | Maximum collections to return | `limit=20` |
| `f` | string | Response format | `f=json`, `f=html` |

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections?limit=10"
```

**Response**:
```json
{
  "collections": [
    {
      "id": "transportation::roads",
      "title": "Road Network",
      "description": "Primary and secondary road infrastructure",
      "itemType": "feature",
      "extent": {
        "spatial": {
          "bbox": [[-180, -90, 180, 90]],
          "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
        }
      },
      "crs": [
        "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        "http://www.opengis.net/def/crs/EPSG/0/3857"
      ],
      "storageCrs": "http://www.opengis.net/def/crs/EPSG/0/4326",
      "links": [
        {
          "href": "https://localhost:5000/ogc/collections/transportation::roads/items",
          "rel": "items",
          "type": "application/geo+json",
          "title": "Items"
        }
      ]
    }
  ],
  "links": [
    {
      "href": "https://localhost:5000/ogc/collections",
      "rel": "self",
      "type": "application/json"
    }
  ]
}
```

### Collection Details

**Endpoint**: `GET /ogc/collections/{collectionId}`

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads"
```

### Collection Items (Features)

**Endpoint**: `GET /ogc/collections/{collectionId}/items`

**Query Parameters**:

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `limit` | integer | Max features (default: 10, max: service-defined) | `limit=100` |
| `offset` | integer | Skip N features for pagination | `offset=50` |
| `bbox` | string | Bounding box filter (minx,miny,maxx,maxy) | `bbox=-122.5,45.4,-122.3,45.6` |
| `datetime` | string | Temporal filter (ISO 8601) | `datetime=2025-01-01T00:00:00Z/2025-12-31T23:59:59Z` |
| `filter` | string | CQL text filter expression | `filter=population > 50000` |
| `filter-lang` | string | Filter language (`cql-text`, `cql2-json`) | `filter-lang=cql-text` |
| `crs` | string | Response CRS | `crs=http://www.opengis.net/def/crs/EPSG/0/3857` |
| `f` | string | Output format | `f=geojson`, `f=geopackage`, `f=csv` |
| `properties` | string | Comma-separated property names | `properties=name,population` |
| `sortby` | string | Sort specification | `sortby=+name,-population` |

**CQL Filter Examples**:

Attribute filters:
```
filter=name = 'Main Street'
filter=population > 10000 AND area < 50
filter=status IN ('active', 'pending')
filter=name LIKE 'Highway%'
```

Spatial filters:
```
filter=S_INTERSECTS(geometry, POLYGON((-122.5 45.4, -122.3 45.4, -122.3 45.6, -122.5 45.6, -122.5 45.4)))
filter=S_WITHIN(geometry, POLYGON(...))
filter=S_CONTAINS(geometry, POINT(-122.4 45.5))
filter=S_DISTANCE(geometry, POINT(-122.4 45.5)) < 1000
```

Temporal filters:
```
filter=T_AFTER(timestamp, TIMESTAMP('2025-01-01T00:00:00Z'))
filter=T_DURING(timestamp, INTERVAL('2025-01-01', '2025-12-31'))
```

Combined filters:
```
filter=population > 50000 AND S_INTERSECTS(geometry, POLYGON(...))
```

**Example Requests**:

Basic query:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/items?limit=5&f=geojson"
```

Bounding box query:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/items?bbox=-122.5,45.4,-122.3,45.6&limit=100"
```

CQL2 text filter:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/items?filter=roadClass='Highway' AND lanes >= 4&filter-lang=cql-text"
```

Spatial intersection:
```bash
curl "https://localhost:5000/ogc/collections/parcels::parcels/items?filter=S_INTERSECTS(geometry,POLYGON((-122.5 45.4,-122.3 45.4,-122.3 45.6,-122.5 45.6,-122.5 45.4)))"
```

GeoPackage export:
```bash
curl -OJ "https://localhost:5000/ogc/collections/transportation::roads/items?f=geopackage"
```

CSV export:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/items?f=csv&limit=1000" -o roads.csv
```

Shapefile export:
```bash
curl -OJ "https://localhost:5000/ogc/collections/transportation::roads/items?f=shapefile"
```

**Response (GeoJSON)**:
```json
{
  "type": "FeatureCollection",
  "numberMatched": 1234,
  "numberReturned": 5,
  "features": [
    {
      "type": "Feature",
      "id": "road.1",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-122.4, 45.5], [-122.3, 45.6]]
      },
      "properties": {
        "name": "Main Street",
        "roadClass": "Arterial",
        "lanes": 4,
        "speed_limit": 35
      }
    }
  ],
  "links": [
    {
      "href": "https://localhost:5000/ogc/collections/transportation::roads/items?offset=5&limit=5",
      "rel": "next",
      "type": "application/geo+json"
    }
  ]
}
```

### Single Feature Retrieval

**Endpoint**: `GET /ogc/collections/{collectionId}/items/{featureId}`

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/items/road.123"
```

### Create Feature (POST)

**Endpoint**: `POST /ogc/collections/{collectionId}/items`

**Authorization**: RequireDataPublisher

**Content-Type**: `application/geo+json`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/ogc/collections/transportation::roads/items" \
  -H "Content-Type: application/geo+json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "type": "Feature",
    "geometry": {
      "type": "LineString",
      "coordinates": [[-122.4, 45.5], [-122.3, 45.6]]
    },
    "properties": {
      "name": "New Street",
      "roadClass": "Local",
      "lanes": 2
    }
  }'
```

**Response**: `201 Created` with `Location` header pointing to new feature.

### Update Feature (PUT)

**Endpoint**: `PUT /ogc/collections/{collectionId}/items/{featureId}`

**Authorization**: RequireDataPublisher

**Headers**:
- `Content-Type: application/geo+json`
- `If-Match: "etag-value"` (optimistic concurrency)

**Example Request**:
```bash
curl -X PUT "https://localhost:5000/ogc/collections/transportation::roads/items/road.123" \
  -H "Content-Type: application/geo+json" \
  -H "If-Match: \"abc123\"" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "type": "Feature",
    "id": "road.123",
    "geometry": {
      "type": "LineString",
      "coordinates": [[-122.4, 45.5], [-122.3, 45.6]]
    },
    "properties": {
      "name": "Main Street",
      "roadClass": "Arterial",
      "lanes": 6,
      "status": "Active"
    }
  }'
```

**Response**: `204 No Content` or `200 OK` with updated feature.

### Partial Update (PATCH)

**Endpoint**: `PATCH /ogc/collections/{collectionId}/items/{featureId}`

**Authorization**: RequireDataPublisher

**Example Request**:
```bash
curl -X PATCH "https://localhost:5000/ogc/collections/transportation::roads/items/road.123" \
  -H "Content-Type: application/geo+json" \
  -H "If-Match: \"abc123\"" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "type": "Feature",
    "id": "road.123",
    "properties": {
      "status": "Under Construction"
    }
  }'
```

### Delete Feature

**Endpoint**: `DELETE /ogc/collections/{collectionId}/items/{featureId}`

**Authorization**: RequireDataPublisher

**Headers**: `If-Match: "etag-value"`

**Example Request**:
```bash
curl -X DELETE "https://localhost:5000/ogc/collections/transportation::roads/items/road.123" \
  -H "If-Match: \"abc123\"" \
  -H "Authorization: Bearer {token}"
```

**Response**: `204 No Content`

### Collection Queryables

**Endpoint**: `GET /ogc/collections/{collectionId}/queryables`

Returns schema of queryable properties for CQL filtering.

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/queryables"
```

**Response**:
```json
{
  "$schema": "https://json-schema.org/draft/2019-09/schema",
  "type": "object",
  "properties": {
    "name": {
      "type": "string",
      "description": "Road name"
    },
    "roadClass": {
      "type": "string",
      "enum": ["Highway", "Arterial", "Collector", "Local"]
    },
    "lanes": {
      "type": "integer",
      "minimum": 1
    },
    "geometry": {
      "$ref": "https://geojson.org/schema/LineString.json"
    }
  }
}
```

---

## OGC WFS (Web Feature Service)

WFS 2.0 implementation with transactional support (WFS-T).

### GetCapabilities

**Endpoint**: `GET /wfs?service=WFS&request=GetCapabilities`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `service` | Yes | Must be "WFS" | `service=WFS` |
| `request` | Yes | Must be "GetCapabilities" | `request=GetCapabilities` |
| `version` | No | WFS version (default: 2.0.0) | `version=2.0.0` |

**Example Request**:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=GetCapabilities"
```

**Response**: XML capabilities document listing available feature types, supported operations, and service metadata.

### DescribeFeatureType

**Endpoint**: `GET /wfs?service=WFS&request=DescribeFeatureType&typeNames={typeNames}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `service` | Yes | Must be "WFS" | `service=WFS` |
| `request` | Yes | Must be "DescribeFeatureType" | `request=DescribeFeatureType` |
| `typeNames` | Yes | Feature type name(s) | `typeNames=transportation:roads` |
| `outputFormat` | No | Schema format (default: GML 3.2) | `outputFormat=application/gml+xml` |

**Example Request**:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=DescribeFeatureType&typeNames=transportation:roads"
```

**Response**: XML schema describing feature structure, attributes, and geometry type.

### GetFeature

**Endpoint**: `GET /wfs?service=WFS&request=GetFeature&typeNames={typeNames}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `service` | Yes | Must be "WFS" | `service=WFS` |
| `request` | Yes | Must be "GetFeature" | `request=GetFeature` |
| `typeNames` | Yes | Feature type(s) to query | `typeNames=transportation:roads` |
| `bbox` | No | Bounding box (minx,miny,maxx,maxy[,crs]) | `bbox=-122.5,45.4,-122.3,45.6` |
| `filter` | No | XML or CQL filter | See filter examples |
| `count` | No | Maximum features | `count=100` |
| `startIndex` | No | Starting index for pagination | `startIndex=50` |
| `outputFormat` | No | Response format | `outputFormat=application/geo+json` |
| `srsName` | No | Response CRS | `srsName=EPSG:3857` |
| `propertyName` | No | Properties to return | `propertyName=name,lanes` |
| `sortBy` | No | Sort specification | `sortBy=name A,lanes D` |

**Supported Output Formats**:
- `application/geo+json` - GeoJSON
- `application/gml+xml; version=3.2` - GML 3.2
- `application/json` - JSON

**Example Requests**:

Basic query:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=GetFeature&typeNames=transportation:roads&count=10&outputFormat=application/geo+json"
```

Bounding box query:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=GetFeature&typeNames=transportation:roads&bbox=-122.5,45.4,-122.3,45.6&srsName=EPSG:4326"
```

CQL filter:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=GetFeature&typeNames=transportation:roads&filter=<Filter><PropertyIsEqualTo><PropertyName>roadClass</PropertyName><Literal>Highway</Literal></PropertyIsEqualTo></Filter>"
```

### GetFeatureWithLock

**Endpoint**: `GET /wfs?service=WFS&request=GetFeatureWithLock&typeNames={typeNames}`

Queries features and acquires a lock for subsequent transactions.

**Example Request**:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=GetFeatureWithLock&typeNames=transportation:roads&filter=<Filter><PropertyIsEqualTo><PropertyName>id</PropertyName><Literal>road.123</Literal></PropertyIsEqualTo></Filter>"
```

**Response**: GML 3.2 with `lockId` attribute in transaction response.

### LockFeature

**Endpoint**: `GET /wfs?service=WFS&request=LockFeature&typeNames={typeNames}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `typeNames` | Yes | Feature types to lock | `typeNames=transportation:roads` |
| `lockAction` | No | ALL or SOME (default: ALL) | `lockAction=SOME` |
| `expiry` | No | Lock duration in minutes | `expiry=5` |

**Example Request**:
```bash
curl "https://localhost:5000/wfs?service=WFS&request=LockFeature&typeNames=transportation:roads&lockAction=ALL"
```

### Transaction (WFS-T)

**Endpoint**: `POST /wfs?service=WFS&request=Transaction`

**Content-Type**: `application/xml`

**Transaction Operations**:
- Insert: Add new features
- Update: Modify existing features
- Delete: Remove features

**Example Request (Insert)**:
```bash
curl -X POST "https://localhost:5000/wfs?service=WFS&request=Transaction" \
  -H "Content-Type: application/xml" \
  -H "Authorization: Bearer {token}" \
  --data-binary @- << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<wfs:Transaction service="WFS" version="2.0.0"
  xmlns:wfs="http://www.opengis.net/wfs/2.0"
  xmlns:gml="http://www.opengis.net/gml/3.2"
  xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Insert>
    <feature>
      <name>New Road</name>
      <geometry>
        <gml:LineString srsName="EPSG:4326">
          <gml:posList>-122.4 45.5 -122.3 45.6</gml:posList>
        </gml:LineString>
      </geometry>
    </feature>
  </wfs:Insert>
</wfs:Transaction>
EOF
```

**Example Request (Update with Lock)**:
```bash
curl -X POST "https://localhost:5000/wfs?service=WFS&request=Transaction" \
  -H "Content-Type: application/xml" \
  --data-binary @- << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<wfs:Transaction service="WFS" version="2.0.0" lockId="lock-abc123"
  xmlns:wfs="http://www.opengis.net/wfs/2.0"
  xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="transportation:roads">
    <wfs:Property>
      <wfs:ValueReference>status</wfs:ValueReference>
      <wfs:Value>Closed</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:ResourceId rid="road.123"/>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
EOF
```

**Response**: Transaction summary with inserted/updated/deleted counts.

---

## OGC WMS (Web Map Service)

WMS 1.3.0 implementation for rendering map images.

### GetCapabilities

**Endpoint**: `GET /wms?service=WMS&request=GetCapabilities`

**Example Request**:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetCapabilities"
```

**Response**: XML capabilities document with available layers, styles, CRS, and supported operations.

### GetMap

**Endpoint**: `GET /wms?service=WMS&request=GetMap&layers={layers}&bbox={bbox}&width={width}&height={height}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `service` | Yes | Must be "WMS" | `service=WMS` |
| `request` | Yes | Must be "GetMap" | `request=GetMap` |
| `version` | No | WMS version (default: 1.3.0) | `version=1.3.0` |
| `layers` | Yes | Layer name(s) `{serviceId}:{datasetId}` | `layers=transportation:roads-imagery` |
| `styles` | No | Style name(s) (empty = default) | `styles=,` |
| `crs` | Yes | Coordinate reference system | `crs=EPSG:3857` |
| `bbox` | Yes | Bounding box (minx,miny,maxx,maxy) | `bbox=-122.5,45.4,-122.3,45.6` |
| `width` | Yes | Image width in pixels | `width=1024` |
| `height` | Yes | Image height in pixels | `height=512` |
| `format` | Yes | Image format | `format=image/png` |
| `transparent` | No | Background transparency | `transparent=true` |
| `bgcolor` | No | Background color (hex) | `bgcolor=0xFFFFFF` |

**Supported Image Formats**:
- `image/png`
- `image/jpeg`
- `image/webp`

**CRS Support**:
- `EPSG:4326` (WGS84)
- `EPSG:3857` (Web Mercator)
- `CRS:84` (WGS84 lon/lat)
- Other EPSG codes supported via configuration

**Example Requests**:

Basic map request:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetMap&version=1.3.0&layers=transportation:roads-imagery&crs=EPSG:3857&bbox=-13644000,5698000,-13620000,5722000&width=1024&height=768&format=image/png" \
  --output map.png
```

Multiple layers:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetMap&layers=basemap:imagery,transportation:roads&styles=,default&crs=EPSG:4326&bbox=-122.6,45.5,-122.3,45.7&width=800&height=600&format=image/jpeg" \
  --output map.jpg
```

Transparent overlay:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetMap&layers=parcels:parcels-overlay&crs=EPSG:3857&bbox=-13644000,5698000,-13620000,5722000&width=1024&height=768&format=image/png&transparent=true" \
  --output overlay.png
```

### GetFeatureInfo

**Endpoint**: `GET /wms?service=WMS&request=GetFeatureInfo&query_layers={layers}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| All GetMap params | Yes | Same as GetMap | See GetMap |
| `query_layers` | Yes | Layers to query | `query_layers=transportation:roads` |
| `i` | Yes | X pixel coordinate | `i=256` |
| `j` | Yes | Y pixel coordinate | `j=256` |
| `info_format` | No | Response format | `info_format=application/json` |
| `feature_count` | No | Max features (default: 1) | `feature_count=10` |

**Supported Info Formats**:
- `application/json`
- `application/geo+json`
- `application/xml`
- `text/html`
- `text/plain`

**Example Request**:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetFeatureInfo&layers=transportation:roads&query_layers=transportation:roads&crs=EPSG:3857&bbox=-13644000,5698000,-13620000,5722000&width=512&height=512&i=256&j=256&info_format=application/json&feature_count=5"
```

**Response (JSON)**:
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": "road.123",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-122.4, 45.5], [-122.3, 45.6]]
      },
      "properties": {
        "name": "Main Street",
        "roadClass": "Arterial"
      }
    }
  ]
}
```

### DescribeLayer

**Endpoint**: `GET /wms?service=WMS&request=DescribeLayer&layers={layers}`

Returns layer metadata and associated WFS feature types.

**Example Request**:
```bash
curl "https://localhost:5000/wms?service=WMS&request=DescribeLayer&layers=transportation:roads-imagery"
```

### GetLegendGraphic

**Endpoint**: `GET /wms?service=WMS&request=GetLegendGraphic&layer={layer}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `layer` | Yes | Layer name | `layer=transportation:roads` |
| `format` | No | Image format (default: image/png) | `format=image/png` |
| `width` | No | Icon width | `width=20` |
| `height` | No | Icon height | `height=20` |

**Example Request**:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetLegendGraphic&layer=transportation:roads-imagery&width=32&height=32&format=image/png" \
  --output legend.png
```

---

## OGC Tiles API

Vector and raster tile services following OGC Tiles API specification.

### Tile Matrix Sets

**Endpoint**: `GET /ogc/tileMatrixSets`

Lists available tile matrix sets (tiling schemes).

**Example Request**:
```bash
curl "https://localhost:5000/ogc/tileMatrixSets"
```

**Response**:
```json
{
  "tileMatrixSets": [
    {
      "id": "WorldWebMercatorQuad",
      "title": "Web Mercator Quad",
      "uri": "http://www.opengis.net/def/tilematrixset/OGC/1.0/WorldWebMercatorQuad",
      "crs": "http://www.opengis.net/def/crs/EPSG/0/3857"
    },
    {
      "id": "WorldCRS84Quad",
      "title": "WGS84 Quad",
      "uri": "http://www.opengis.net/def/tilematrixset/OGC/1.0/WorldCRS84Quad",
      "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
    }
  ]
}
```

### Collection Tilesets

**Endpoint**: `GET /ogc/collections/{collectionId}/tiles`

Lists available tilesets for a collection.

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/tiles"
```

### Tileset Metadata

**Endpoint**: `GET /ogc/collections/{collectionId}/tiles/{tilesetId}`

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/tiles/roads-tiles"
```

### TileJSON

**Endpoint**: `GET /ogc/collections/{collectionId}/tiles/{tilesetId}/tilejson`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `tileMatrixSet` | No | Tile matrix set ID | `tileMatrixSet=WorldWebMercatorQuad` |

**Example Request**:
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/tiles/roads-tiles/tilejson?tileMatrixSet=WorldWebMercatorQuad"
```

**Response (TileJSON 3.0)**:
```json
{
  "tilejson": "3.0.0",
  "name": "Road Tiles",
  "description": "Vector tiles for road network",
  "scheme": "xyz",
  "format": "geojson",
  "minzoom": 0,
  "maxzoom": 14,
  "bounds": [-180, -85.0511, 180, 85.0511],
  "center": [-122.4, 45.5, 10],
  "tiles": [
    "https://localhost:5000/ogc/collections/transportation::roads/tiles/roads-tiles/WorldWebMercatorQuad/{z}/{y}/{x}?f=geojson"
  ],
  "vector_layers": [
    {
      "id": "roads",
      "description": "Road features"
    }
  ]
}
```

### Retrieve Tile

**Endpoint**: `GET /ogc/collections/{collectionId}/tiles/{tilesetId}/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `f` | No | Tile format | `f=mvt`, `f=geojson`, `f=png` |
| `format` | No | Alternative format parameter | `format=png` |

**Tile Formats**:
- **Vector**: `mvt` (Mapbox Vector Tiles), `pbf`, `geojson`
- **Raster**: `png`, `jpeg`, `webp`

**Example Requests**:

Vector tile (MVT):
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/tiles/roads-tiles/WorldWebMercatorQuad/14/2818/6538?f=mvt" \
  --output tile.mvt
```

Vector tile (GeoJSON):
```bash
curl "https://localhost:5000/ogc/collections/transportation::roads/tiles/roads-tiles/WorldWebMercatorQuad/10/177/409?f=geojson"
```

Raster tile (PNG):
```bash
curl "https://localhost:5000/ogc/collections/basemap::imagery/tiles/imagery-tiles/WorldWebMercatorQuad/12/711/1638?f=png" \
  --output tile.png
```

**Tile Coordinate System**:
- Scheme: XYZ (Web Mercator)
- Z: Zoom level (0-22)
- X: Column (0 to 2^z - 1)
- Y: Row (0 to 2^z - 1)

---

## Geoservices REST a.k.a. Esri REST Services

ArcGIS-compatible REST services for feature access, mapping, and editing.

### Services Directory

**Endpoint**: `GET /rest/services`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `f` | Format (`json`, `pjson`, `html`) | `f=pjson` |

**Example Request**:
```bash
curl "https://localhost:5000/rest/services?f=pjson"
```

**Response**:
```json
{
  "currentVersion": 10.81,
  "folders": ["Public", "Transportation", "Utilities"],
  "services": [
    {
      "name": "Public/Parcels",
      "type": "FeatureServer",
      "title": "Property Parcels",
      "url": "https://localhost:5000/rest/services/Public/Parcels/FeatureServer"
    },
    {
      "name": "Public/Parcels",
      "type": "MapServer",
      "title": "Property Parcels",
      "url": "https://localhost:5000/rest/services/Public/Parcels/MapServer"
    }
  ]
}
```

### Folder Listing

**Endpoint**: `GET /rest/services/{folderId}`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public?f=json"
```

### FeatureServer Metadata

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer?f=json"
```

**Response**:
```json
{
  "currentVersion": 10.81,
  "serviceDescription": "Property parcel data",
  "hasVersionedData": false,
  "supportsDisconnectedEditing": false,
  "supportedQueryFormats": "JSON, GeoJSON, PBF",
  "maxRecordCount": 1000,
  "capabilities": "Query,Create,Update,Delete,Editing",
  "layers": [
    {
      "id": 0,
      "name": "Parcels",
      "type": "Feature Layer",
      "geometryType": "esriGeometryPolygon"
    }
  ]
}
```

### Layer Metadata

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0?f=json"
```

**Response**:
```json
{
  "currentVersion": 10.81,
  "id": "0",
  "name": "Parcels",
  "type": "Feature Layer",
  "geometryType": "esriGeometryPolygon",
  "objectIdField": "OBJECTID",
  "globalIdField": "GlobalID",
  "hasAttachments": true,
  "supportsStatistics": true,
  "supportsAdvancedQueries": true,
  "supportsPagination": true,
  "maxRecordCount": 1000,
  "fields": [
    {
      "name": "OBJECTID",
      "type": "esriFieldTypeOID",
      "alias": "Object ID"
    },
    {
      "name": "PARCEL_ID",
      "type": "esriFieldTypeString",
      "alias": "Parcel ID",
      "length": 20
    },
    {
      "name": "OWNER_NAME",
      "type": "esriFieldTypeString",
      "alias": "Owner",
      "length": 100
    },
    {
      "name": "AREA_SQFT",
      "type": "esriFieldTypeDouble",
      "alias": "Area (sq ft)"
    }
  ],
  "relationships": [
    {
      "id": 1,
      "name": "Inspections",
      "cardinality": "esriRelCardinalityOneToMany",
      "role": "esriRelRoleOrigin",
      "relatedTableId": 1
    }
  ]
}
```

### Query Features

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/query`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `where` | SQL WHERE clause | `where=OWNER_NAME='Smith'` |
| `objectIds` | Comma-separated IDs | `objectIds=1,2,3` |
| `geometry` | Geometry for spatial filter | `geometry={"xmin":-122.5,"ymin":45.4,"xmax":-122.3,"ymax":45.6}` |
| `geometryType` | Geometry type | `geometryType=esriGeometryEnvelope` |
| `inSR` | Input spatial reference | `inSR=4326` |
| `spatialRel` | Spatial relationship | `spatialRel=esriSpatialRelIntersects` |
| `outFields` | Fields to return | `outFields=*` or `outFields=PARCEL_ID,OWNER_NAME` |
| `returnGeometry` | Include geometry | `returnGeometry=true` |
| `returnIdsOnly` | Return only IDs | `returnIdsOnly=false` |
| `returnCountOnly` | Return count only | `returnCountOnly=false` |
| `orderByFields` | Sort order | `orderByFields=OWNER_NAME ASC` |
| `resultOffset` | Pagination offset | `resultOffset=100` |
| `resultRecordCount` | Max records | `resultRecordCount=50` |
| `outSR` | Output spatial reference | `outSR=3857` |
| `f` | Format | `f=json`, `f=geojson`, `f=pbf` |

**Spatial Relationship Values**:
- `esriSpatialRelIntersects`
- `esriSpatialRelContains`
- `esriSpatialRelCrosses`
- `esriSpatialRelEnvelopeIntersects`
- `esriSpatialRelOverlaps`
- `esriSpatialRelTouches`
- `esriSpatialRelWithin`

**Example Requests**:

Basic query:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outFields=*&f=geojson"
```

Attribute filter:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=AREA_SQFT>5000 AND OWNER_NAME LIKE 'Smith%'&outFields=PARCEL_ID,OWNER_NAME,AREA_SQFT&f=json"
```

Spatial query with envelope:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?geometry=-122.5,45.4,-122.3,45.6&geometryType=esriGeometryEnvelope&spatialRel=esriSpatialRelIntersects&outFields=*&f=geojson"
```

Pagination:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outFields=*&resultOffset=0&resultRecordCount=100&orderByFields=PARCEL_ID&f=json"
```

Count only:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=AREA_SQFT>10000&returnCountOnly=true&f=json"
```

IDs only:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=OWNER_NAME='Smith'&returnIdsOnly=true&f=json"
```

PBF (Protocol Buffer) format:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outFields=*&f=pbf" \
  --output features.pbf
```

KML export:
```bash
curl -OJ "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&f=kml"
```

KMZ export:
```bash
curl -OJ "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&f=kmz"
```

**Response (GeoJSON)**:
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": 1,
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[-122.4, 45.5], [-122.3, 45.5], [-122.3, 45.6], [-122.4, 45.6], [-122.4, 45.5]]]
      },
      "properties": {
        "OBJECTID": 1,
        "PARCEL_ID": "P-001",
        "OWNER_NAME": "John Smith",
        "AREA_SQFT": 7500
      }
    }
  ]
}
```

### Query Related Records

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/queryRelatedRecords`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `objectIds` | Yes | Object IDs | `objectIds=1,2,3` |
| `relationshipId` | Yes | Relationship ID | `relationshipId=1` |
| `outFields` | No | Fields to return | `outFields=*` |
| `definitionExpression` | No | Filter related records | `definitionExpression=STATUS='Active'` |
| `returnGeometry` | No | Include geometry | `returnGeometry=true` |
| `f` | No | Format | `f=json` |

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/queryRelatedRecords?objectIds=1,2,3&relationshipId=1&outFields=*&f=json"
```

**Response**:
```json
{
  "relatedRecordGroups": [
    {
      "objectId": 1,
      "relatedRecords": [
        {
          "attributes": {
            "INSPECTION_ID": 101,
            "INSPECTION_DATE": 1704067200000,
            "STATUS": "Completed"
          }
        }
      ]
    }
  ]
}
```

### Apply Edits

**Endpoint**: `POST /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/applyEdits`

**Authorization**: RequireDataPublisher

**Content-Type**: `application/x-www-form-urlencoded` or `application/json`

**Request Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `adds` | Features to insert (JSON array) | See example |
| `updates` | Features to update (JSON array) | See example |
| `deletes` | Object IDs to delete (array or comma-separated) | `deletes=[1,2,3]` |
| `rollbackOnFailure` | Transaction rollback | `rollbackOnFailure=true` |
| `f` | Format | `f=json` |

**Example Request (Add Features)**:
```bash
curl -X POST "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/applyEdits" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "adds": [
      {
        "geometry": {
          "rings": [[[-122.4, 45.5], [-122.3, 45.5], [-122.3, 45.6], [-122.4, 45.6], [-122.4, 45.5]]]
        },
        "attributes": {
          "PARCEL_ID": "P-999",
          "OWNER_NAME": "Jane Doe",
          "AREA_SQFT": 8500
        }
      }
    ],
    "f": "json"
  }'
```

**Example Request (Update Features)**:
```bash
curl -X POST "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/applyEdits" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "updates": [
      {
        "attributes": {
          "OBJECTID": 1,
          "OWNER_NAME": "Updated Owner"
        }
      }
    ],
    "f": "json"
  }'
```

**Example Request (Delete Features)**:
```bash
curl -X POST "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/applyEdits" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "deletes": [5, 6, 7],
    "f": "json"
  }'
```

**Response**:
```json
{
  "addResults": [
    {
      "objectId": 999,
      "globalId": "{ABC-123}",
      "success": true
    }
  ],
  "updateResults": [],
  "deleteResults": []
}
```

### MapServer Metadata

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/MapServer`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public/Basemap/MapServer?f=json"
```

### MapServer Find

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/MapServer/find`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `searchText` | Yes | Text to search | `searchText=Main Street` |
| `searchFields` | No | Fields to search | `searchFields=name,description` |
| `layers` | No | Layers to search (`all`, `visible`, `top`, or IDs) | `layers=0,1,2` |
| `contains` | No | Contains vs exact match | `contains=true` |
| `returnGeometry` | No | Include geometry | `returnGeometry=true` |
| `f` | No | Format | `f=json` |

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Transportation/Roads/MapServer/find?searchText=Sunset&searchFields=name&layers=visible&f=json"
```

**Response**:
```json
{
  "results": [
    {
      "layerId": 0,
      "layerName": "Roads",
      "displayFieldName": "name",
      "foundFieldName": "name",
      "value": "Sunset Boulevard",
      "attributes": {
        "OBJECTID": 123,
        "name": "Sunset Boulevard",
        "roadClass": "Arterial"
      },
      "geometry": {
        "paths": [[[-122.4, 45.5], [-122.3, 45.6]]]
      }
    }
  ]
}
```

### ImageServer Metadata

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/ImageServer`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Basemap/Imagery/ImageServer?f=json"
```

**Response**:
```json
{
  "currentVersion": 10.81,
  "serviceDescription": "Aerial imagery",
  "name": "Imagery",
  "capabilities": "Image",
  "supportedImageFormatTypes": "PNG,JPEG,WEBP",
  "maxImageHeight": 4096,
  "maxImageWidth": 4096,
  "extent": {
    "xmin": -122.6,
    "ymin": 45.4,
    "xmax": -122.2,
    "ymax": 45.8,
    "spatialReference": { "wkid": 4326 }
  },
  "datasets": [
    {
      "id": "imagery-2025",
      "title": "2025 Imagery",
      "defaultStyleId": "rgb",
      "styleIds": ["rgb", "infrared", "ndvi"]
    }
  ]
}
```

### ImageServer Export

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/ImageServer/exportImage`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `bbox` | Yes | Bounding box (minx,miny,maxx,maxy) | `bbox=-122.5,45.4,-122.3,45.6` |
| `size` | Yes | Image size (width,height) | `size=1024,768` |
| `format` | No | Image format (default: png) | `format=jpeg` |
| `bboxSR` | No | Bbox spatial reference | `bboxSR=4326` |
| `imageSR` | No | Output spatial reference | `imageSR=3857` |
| `transparent` | No | Transparency | `transparent=true` |
| `rasterId` | No | Specific raster dataset | `rasterId=imagery-2025` |
| `styleId` | No | Style to apply | `styleId=infrared` |
| `f` | No | Format | `f=image` |

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Basemap/Imagery/ImageServer/exportImage?bbox=-122.5,45.4,-122.3,45.6&size=1024,768&format=png&bboxSR=4326" \
  --output imagery.png
```

### Attachments

**Query Attachments**:

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/{objectId}/queryAttachments`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/queryAttachments?f=json"
```

**Response**:
```json
{
  "attachmentInfos": [
    {
      "id": 1,
      "name": "photo.jpg",
      "size": 245678,
      "contentType": "image/jpeg"
    }
  ]
}
```

**Add Attachment**:

**Endpoint**: `POST /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/{objectId}/addAttachment`

**Content-Type**: `multipart/form-data`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/addAttachment" \
  -H "Authorization: Bearer {token}" \
  -F "attachment=@photo.jpg" \
  -F "f=json"
```

**Download Attachment**:

**Endpoint**: `GET /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/{objectId}/attachments/{attachmentId}`

**Example Request**:
```bash
curl -OJ "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/123/attachments/1"
```

**Delete Attachment**:

**Endpoint**: `POST /rest/services/{folderId}/{serviceId}/FeatureServer/{layerId}/deleteAttachments`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Public/Inspections/FeatureServer/0/deleteAttachments" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "objectIds": [123],
    "attachmentIds": [1],
    "f": "json"
  }'
```

---

## OData v4 API

OASIS OData 4.0 protocol with spatial extensions for entity queries.

### Service Root

**Endpoint**: `GET /odata`

**Example Request**:
```bash
curl "https://localhost:5000/odata"
```

**Response**:
```json
{
  "@odata.context": "https://localhost:5000/odata/$metadata",
  "value": [
    {
      "name": "Parcels",
      "kind": "EntitySet",
      "url": "Parcels"
    },
    {
      "name": "Roads",
      "kind": "EntitySet",
      "url": "Roads"
    }
  ]
}
```

### Service Metadata

**Endpoint**: `GET /odata/$metadata`

**Example Request**:
```bash
curl "https://localhost:5000/odata/\$metadata"
```

**Response**: EDMX (Entity Data Model) XML document.

### Query Entity Set

**Endpoint**: `GET /odata/{EntitySetName}`

**Query Options**:

| Option | Description | Example |
|--------|-------------|---------|
| `$select` | Properties to return | `$select=name,area` |
| `$filter` | Filter expression | `$filter=area gt 5000` |
| `$orderby` | Sort order | `$orderby=name desc` |
| `$top` | Limit results | `$top=10` |
| `$skip` | Skip results | `$skip=20` |
| `$count` | Include total count | `$count=true` |
| `$expand` | Expand related entities | `$expand=Inspections` |

**Example Requests**:

Basic query:
```bash
curl "https://localhost:5000/odata/Parcels?\$top=10&\$select=parcel_id,owner_name"
```

Filter query:
```bash
curl "https://localhost:5000/odata/Parcels?\$filter=area gt 5000 and owner_name eq 'Smith'"
```

Spatial filter (intersects):
```bash
curl "https://localhost:5000/odata/Parcels?\$filter=geo.intersects(geometry,geography'POLYGON((-122.5 45.4,-122.3 45.4,-122.3 45.6,-122.5 45.6,-122.5 45.4))')"
```

Spatial filter (distance):
```bash
curl "https://localhost:5000/odata/Roads?\$filter=geo.distance(geometry,geography'POINT(-122.4 45.5)') lt 1000"
```

Sort and pagination:
```bash
curl "https://localhost:5000/odata/Parcels?\$orderby=owner_name,area desc&\$skip=0&\$top=25"
```

Expand relationships:
```bash
curl "https://localhost:5000/odata/Parcels?\$expand=Inspections&\$filter=parcel_id eq 'P-001'"
```

**Response**:
```json
{
  "@odata.context": "https://localhost:5000/odata/$metadata#Parcels",
  "@odata.count": 1234,
  "value": [
    {
      "OBJECTID": 1,
      "parcel_id": "P-001",
      "owner_name": "John Smith",
      "area": 7500,
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[-122.4, 45.5], [-122.3, 45.5], [-122.3, 45.6], [-122.4, 45.6], [-122.4, 45.5]]]
      }
    }
  ]
}
```

### Entity Count

**Endpoint**: `GET /odata/{EntitySetName}/$count`

**Example Request**:
```bash
curl "https://localhost:5000/odata/Parcels/\$count"
```

**Response**: Integer count.

### Spatial Functions

Supported OData spatial functions:

| Function | Description | Example |
|----------|-------------|---------|
| `geo.intersects(p1, p2)` | Geometries intersect | `geo.intersects(geometry, geography'POINT(-122 45)')` |
| `geo.distance(p1, p2)` | Distance between geometries | `geo.distance(geometry, geography'POINT(-122 45)')` |
| `geo.length(p)` | Geometry length | `geo.length(geometry) gt 1000` |

---

## STAC API

SpatioTemporal Asset Catalog API for raster and imagery datasets.

### Landing Page

**Endpoint**: `GET /stac`

**Example Request**:
```bash
curl "https://localhost:5000/stac"
```

**Response**:
```json
{
  "type": "Catalog",
  "stac_version": "1.0.0",
  "id": "honua-catalog",
  "title": "Honua STAC Catalog",
  "description": "Geospatial asset catalog",
  "links": [
    {
      "rel": "self",
      "href": "https://localhost:5000/stac",
      "type": "application/json"
    },
    {
      "rel": "root",
      "href": "https://localhost:5000/stac",
      "type": "application/json"
    },
    {
      "rel": "conformance",
      "href": "https://localhost:5000/stac/conformance",
      "type": "application/json"
    },
    {
      "rel": "data",
      "href": "https://localhost:5000/stac/collections",
      "type": "application/json"
    },
    {
      "rel": "search",
      "href": "https://localhost:5000/stac/search",
      "type": "application/geo+json"
    }
  ]
}
```

### Conformance

**Endpoint**: `GET /stac/conformance`

**Example Request**:
```bash
curl "https://localhost:5000/stac/conformance"
```

### Collections

**Endpoint**: `GET /stac/collections`

**Example Request**:
```bash
curl "https://localhost:5000/stac/collections"
```

**Response**:
```json
{
  "collections": [
    {
      "id": "imagery-2025",
      "type": "Collection",
      "stac_version": "1.0.0",
      "title": "2025 Aerial Imagery",
      "description": "High-resolution aerial imagery",
      "license": "proprietary",
      "extent": {
        "spatial": {
          "bbox": [[-122.6, 45.4, -122.2, 45.8]]
        },
        "temporal": {
          "interval": [["2025-01-01T00:00:00Z", "2025-12-31T23:59:59Z"]]
        }
      },
      "links": [
        {
          "rel": "items",
          "href": "https://localhost:5000/stac/collections/imagery-2025/items",
          "type": "application/geo+json"
        }
      ]
    }
  ]
}
```

### Collection Detail

**Endpoint**: `GET /stac/collections/{collectionId}`

**Example Request**:
```bash
curl "https://localhost:5000/stac/collections/imagery-2025"
```

### Collection Items

**Endpoint**: `GET /stac/collections/{collectionId}/items`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `bbox` | Bounding box | `bbox=-122.5,45.4,-122.3,45.6` |
| `datetime` | Temporal filter | `datetime=2025-01-01/2025-12-31` |
| `limit` | Max items | `limit=10` |

**Example Request**:
```bash
curl "https://localhost:5000/stac/collections/imagery-2025/items?limit=10"
```

### Item Detail

**Endpoint**: `GET /stac/collections/{collectionId}/items/{itemId}`

**Example Request**:
```bash
curl "https://localhost:5000/stac/collections/imagery-2025/items/scene-2025-10-01"
```

**Response**:
```json
{
  "type": "Feature",
  "stac_version": "1.0.0",
  "id": "scene-2025-10-01",
  "geometry": {
    "type": "Polygon",
    "coordinates": [[[-122.5, 45.4], [-122.3, 45.4], [-122.3, 45.6], [-122.5, 45.6], [-122.5, 45.4]]]
  },
  "bbox": [-122.5, 45.4, -122.3, 45.6],
  "properties": {
    "datetime": "2025-10-01T12:00:00Z",
    "gsd": 0.3
  },
  "assets": {
    "visual": {
      "href": "https://localhost:5000/assets/scene-2025-10-01.tif",
      "type": "image/tiff; application=geotiff",
      "roles": ["data"]
    }
  },
  "links": [
    {
      "rel": "self",
      "href": "https://localhost:5000/stac/collections/imagery-2025/items/scene-2025-10-01"
    }
  ]
}
```

### Search (GET)

**Endpoint**: `GET /stac/search`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `bbox` | Bounding box | `bbox=-122.5,45.4,-122.3,45.6` |
| `datetime` | Temporal filter | `datetime=2025-01-01/2025-12-31` |
| `collections` | Collection IDs | `collections=imagery-2025,lidar-2025` |
| `limit` | Max results | `limit=10` |

**Example Request**:
```bash
curl "https://localhost:5000/stac/search?bbox=-122.5,45.4,-122.3,45.6&datetime=2025-01-01/2025-12-31&collections=imagery-2025&limit=10"
```

### Search (POST)

**Endpoint**: `POST /stac/search`

**Content-Type**: `application/json`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/stac/search" \
  -H "Content-Type: application/json" \
  -d '{
    "bbox": [-122.5, 45.4, -122.3, 45.6],
    "datetime": "2025-01-01T00:00:00Z/2025-12-31T23:59:59Z",
    "collections": ["imagery-2025"],
    "limit": 10
  }'
```

---

## Carto API

SQL-based analytics API compatible with Carto platform.

### Landing Page

**Endpoint**: `GET /carto`

**Example Request**:
```bash
curl "https://localhost:5000/carto"
```

### List Datasets

**Endpoint**: `GET /carto/api/v3/datasets`

**Example Request**:
```bash
curl "https://localhost:5000/carto/api/v3/datasets"
```

**Response**:
```json
{
  "datasets": [
    {
      "id": "transportation.roads",
      "name": "roads",
      "schema": "transportation",
      "title": "Road Network"
    }
  ]
}
```

### Dataset Detail

**Endpoint**: `GET /carto/api/v3/datasets/{datasetId}`

**Example Request**:
```bash
curl "https://localhost:5000/carto/api/v3/datasets/transportation.roads"
```

### Dataset Schema

**Endpoint**: `GET /carto/api/v3/datasets/{datasetId}/schema`

**Example Request**:
```bash
curl "https://localhost:5000/carto/api/v3/datasets/transportation.roads/schema"
```

**Response**:
```json
{
  "fields": [
    {
      "name": "name",
      "type": "string"
    },
    {
      "name": "lanes",
      "type": "integer"
    },
    {
      "name": "geometry",
      "type": "geometry"
    }
  ]
}
```

### SQL Query (GET)

**Endpoint**: `GET /carto/api/v3/sql?q={query}`

**Query Parameters**:

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `q` | Yes | SQL query | `q=SELECT * FROM roads LIMIT 5` |

**Example Requests**:

Basic SELECT:
```bash
curl "https://localhost:5000/carto/api/v3/sql?q=SELECT+name,lanes+FROM+transportation.roads+LIMIT+5"
```

Aggregation:
```bash
curl "https://localhost:5000/carto/api/v3/sql?q=SELECT+roadClass,COUNT(*)+AS+total+FROM+transportation.roads+GROUP+BY+roadClass"
```

WHERE clause:
```bash
curl "https://localhost:5000/carto/api/v3/sql?q=SELECT+*+FROM+transportation.roads+WHERE+lanes+>+4"
```

### SQL Query (POST)

**Endpoint**: `POST /carto/api/v3/sql`

**Content-Type**: `application/json`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/carto/api/v3/sql" \
  -H "Content-Type: application/json" \
  -d '{
    "q": "SELECT roadClass, COUNT(*) AS total, AVG(lanes) AS avg_lanes FROM transportation.roads GROUP BY roadClass ORDER BY total DESC"
  }'
```

**Response**:
```json
{
  "rows": [
    {
      "roadClass": "Local",
      "total": 5432,
      "avg_lanes": 2.1
    },
    {
      "roadClass": "Arterial",
      "total": 876,
      "avg_lanes": 4.3
    }
  ],
  "total_rows": 2,
  "time": 0.045
}
```

**Supported SQL Features**:
- SELECT with column aliases
- WHERE with comparison operators (`=`, `>`, `<`, `>=`, `<=`, `!=`)
- WHERE with IN and LIKE
- GROUP BY with aggregates (COUNT, SUM, AVG, MIN, MAX)
- ORDER BY (ASC/DESC)
- LIMIT and OFFSET

---

## OGC API Records

Metadata catalog API following OGC API Records specification.

### Landing Page

**Endpoint**: `GET /records`

**Example Request**:
```bash
curl "https://localhost:5000/records"
```

### List Collections

**Endpoint**: `GET /records/collections`

**Example Request**:
```bash
curl "https://localhost:5000/records/collections"
```

### Collection Detail

**Endpoint**: `GET /records/collections/{collectionId}`

**Example Request**:
```bash
curl "https://localhost:5000/records/collections/transportation"
```

### Records in Collection

**Endpoint**: `GET /records/collections/{collectionId}/items`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `limit` | Max records | `limit=20` |
| `q` | Text search | `q=road` |

**Example Request**:
```bash
curl "https://localhost:5000/records/collections/transportation/items?limit=5"
```

### Single Record

**Endpoint**: `GET /records/collections/{collectionId}/items/{recordId}`

**Example Request**:
```bash
curl "https://localhost:5000/records/collections/transportation/items/roads"
```

---

## Geometry Service

GeoServices REST compatible geometry service for spatial operations.

### Service Metadata

**Endpoint**: `GET /rest/services/Geometry/GeometryServer`

**Example Request**:
```bash
curl "https://localhost:5000/rest/services/Geometry/GeometryServer?f=json"
```

**Response**:
```json
{
  "currentVersion": 10.81,
  "serviceDescription": "Geometry Service for spatial operations",
  "operations": [
    "project",
    "buffer",
    "simplify",
    "union",
    "intersect",
    "difference",
    "distance",
    "areasAndLengths"
  ]
}
```

### Project

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/project`

**Request Body**:
```json
{
  "geometries": {
    "geometryType": "esriGeometryPoint",
    "geometries": [
      {"x": -122.4, "y": 45.5}
    ]
  },
  "inSR": 4326,
  "outSR": 3857,
  "f": "json"
}
```

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Geometry/GeometryServer/project" \
  -H "Content-Type: application/json" \
  -d '{
    "geometries": {
      "geometryType": "esriGeometryPoint",
      "geometries": [{"x": -122.4, "y": 45.5}]
    },
    "inSR": 4326,
    "outSR": 3857,
    "f": "json"
  }'
```

### Buffer

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/buffer`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Geometry/GeometryServer/buffer" \
  -H "Content-Type: application/json" \
  -d '{
    "geometries": {
      "geometryType": "esriGeometryPoint",
      "geometries": [{"x": -122.4, "y": 45.5}]
    },
    "sr": 4326,
    "distances": 1000,
    "unit": "meters",
    "unionResults": false,
    "f": "json"
  }'
```

### Simplify

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/simplify`

### Union

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/union`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Geometry/GeometryServer/union" \
  -H "Content-Type: application/json" \
  -d '{
    "geometries": {
      "geometryType": "esriGeometryPolygon",
      "geometries": [
        {"rings": [[[-122.5, 45.4], [-122.3, 45.4], [-122.3, 45.6], [-122.5, 45.6], [-122.5, 45.4]]]},
        {"rings": [[[-122.4, 45.5], [-122.2, 45.5], [-122.2, 45.7], [-122.4, 45.7], [-122.4, 45.5]]]}
      ]
    },
    "sr": 4326,
    "f": "json"
  }'
```

### Intersect

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/intersect`

### Difference

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/difference`

### Distance

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/distance`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Geometry/GeometryServer/distance" \
  -H "Content-Type: application/json" \
  -d '{
    "geometry1": {"x": -122.4, "y": 45.5},
    "geometry2": {"x": -122.3, "y": 45.6},
    "geometryType": "esriGeometryPoint",
    "sr": 4326,
    "distanceUnit": "meters",
    "geodesic": true,
    "f": "json"
  }'
```

### Areas and Lengths

**Endpoint**: `POST /rest/services/Geometry/GeometryServer/areasAndLengths`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/rest/services/Geometry/GeometryServer/areasAndLengths" \
  -H "Content-Type: application/json" \
  -d '{
    "polygons": [
      {"rings": [[[-122.5, 45.4], [-122.3, 45.4], [-122.3, 45.6], [-122.5, 45.6], [-122.5, 45.4]]]}
    ],
    "sr": 4326,
    "areaUnit": "square-meters",
    "lengthUnit": "meters",
    "f": "json"
  }'
```

---

## Catalog and Discovery

### Search Catalog

**Endpoint**: `GET /api/catalog`

**Query Parameters**:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `q` | Search query | `q=roads` |
| `group` | Group/folder filter | `group=Transportation` |
| `limit` | Max results | `limit=20` |

**Example Request**:
```bash
curl "https://localhost:5000/api/catalog?q=roads&group=Transportation"
```

**Response**:
```json
{
  "results": [
    {
      "serviceId": "transportation",
      "layerId": "roads",
      "title": "Road Network",
      "description": "Primary and secondary roads",
      "group": "Transportation",
      "keywords": ["roads", "infrastructure"]
    }
  ]
}
```

### Get Catalog Record

**Endpoint**: `GET /api/catalog/{serviceId}/{layerId}`

**Example Request**:
```bash
curl "https://localhost:5000/api/catalog/transportation/roads"
```

---

## Authentication

### Login (Local Mode)

**Endpoint**: `POST /api/auth/local/login`

**Content-Type**: `application/json`

**Request Body**:
```json
{
  "username": "admin",
  "password": "secretPassword"
}
```

**Example Request**:
```bash
curl -X POST "https://localhost:5000/api/auth/local/login" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "secretPassword"
  }'
```

**Response**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-10-05T12:00:00Z",
  "user": {
    "username": "admin",
    "roles": ["Administrator", "DataPublisher", "Viewer"]
  }
}
```

### Get Current User

**Endpoint**: `GET /api/auth/user`

**Headers**: `Authorization: Bearer {token}`

**Example Request**:
```bash
curl "https://localhost:5000/api/auth/user" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Response**:
```json
{
  "username": "admin",
  "email": "admin@example.com",
  "roles": ["Administrator"]
}
```

### Logout

**Endpoint**: `POST /api/auth/logout`

Client-side token invalidation (server does not track sessions).

---

## Administrative API

All admin endpoints require `Administrator` role.

### Reload Metadata

**Endpoint**: `POST /admin/metadata/reload`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/reload" \
  -H "Authorization: Bearer {token}"
```

### Validate Metadata

**Endpoint**: `POST /admin/metadata/validate`

**Content-Type**: `application/json`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/validate" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d @metadata.json
```

### Apply Metadata

**Endpoint**: `POST /admin/metadata/apply`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/apply" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d @metadata.json
```

### Metadata Diff

**Endpoint**: `POST /admin/metadata/diff`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/diff" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d @new-metadata.json
```

### List Snapshots

**Endpoint**: `GET /admin/metadata/snapshots`

**Example Request**:
```bash
curl "https://localhost:5000/admin/metadata/snapshots" \
  -H "Authorization: Bearer {token}"
```

### Create Snapshot

**Endpoint**: `POST /admin/metadata/snapshots`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/snapshots" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "label": "backup-2025-10-04",
    "notes": "Pre-migration backup"
  }'
```

### Restore Snapshot

**Endpoint**: `POST /admin/metadata/snapshots/{label}/restore`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/metadata/snapshots/backup-2025-10-04/restore" \
  -H "Authorization: Bearer {token}"
```

### Create Migration Job

**Endpoint**: `POST /admin/migrations/jobs`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/migrations/jobs" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceUrl": "https://services.arcgis.com/xyz/FeatureServer",
    "targetProvider": "postgis",
    "targetConnectionString": "Host=localhost;Database=gis;Username=postgres",
    "includeLayers": [0, 1, 2]
  }'
```

### List Migration Jobs

**Endpoint**: `GET /admin/migrations/jobs`

**Example Request**:
```bash
curl "https://localhost:5000/admin/migrations/jobs" \
  -H "Authorization: Bearer {token}"
```

### Get Migration Status

**Endpoint**: `GET /admin/migrations/jobs/{jobId}`

**Example Request**:
```bash
curl "https://localhost:5000/admin/migrations/jobs/migration-abc123" \
  -H "Authorization: Bearer {token}"
```

### Create Tile Cache Preseed Job

**Endpoint**: `POST /admin/raster-cache/jobs`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/raster-cache/jobs" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "datasetIds": ["imagery-basemap"],
    "maxZoom": 14,
    "bbox": [-122.6, 45.4, -122.2, 45.8]
  }'
```

### Purge Tile Cache

**Endpoint**: `POST /admin/raster-cache/datasets/purge`

**Example Request**:
```bash
curl -X POST "https://localhost:5000/admin/raster-cache/datasets/purge" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "datasetIds": ["imagery-basemap"]
  }'
```

---

## Response Formats

### Format Specification

Formats can be requested via:
1. Query parameter: `?f=geojson` or `?format=json`
2. Accept header: `Accept: application/geo+json`
3. File extension (where supported): `/items.geojson`

### Supported Formats by Service

| Format | MIME Type | OGC API | WFS | WMS | Geoservices REST a.k.a. Esri REST |
|--------|-----------|---------|-----|-----|-----------|
| JSON | application/json | Yes | Yes | Info | Yes |
| GeoJSON | application/geo+json | Yes | Yes | Info | Yes |
| JSON-FG | application/vnd.ogc.fg+json | Yes | No | No | No |
| GML 3.2 | application/gml+xml | No | Yes | No | No |
| HTML | text/html | Yes | No | Info | Yes |
| KML | application/vnd.google-earth.kml+xml | Yes | No | No | Yes |
| KMZ | application/vnd.google-earth.kmz | Yes | No | No | Yes |
| CSV | text/csv | Yes | No | No | No |
| GeoPackage | application/geopackage+sqlite3 | Yes | No | No | No |
| Shapefile | application/x-shapefile | Yes | No | No | No |
| MVT | application/vnd.mapbox-vector-tile | Tiles | No | No | No |
| PNG | image/png | Tiles | No | Yes | Yes |
| JPEG | image/jpeg | Tiles | No | Yes | Yes |
| WebP | image/webp | Tiles | No | Yes | Yes |
| PBF | application/x-protobuf | No | No | No | Yes |

### Content Type Headers

Standard MIME types:
```
Content-Type: application/geo+json; charset=utf-8
Content-Type: application/json; charset=utf-8
Content-Type: text/html; charset=utf-8
Content-Type: application/gml+xml; version=3.2
Content-Type: image/png
```

---

## Error Handling

### HTTP Status Codes

| Code | Meaning | Usage |
|------|---------|-------|
| 200 | OK | Successful request |
| 201 | Created | Feature created |
| 204 | No Content | Successful delete/update |
| 400 | Bad Request | Invalid parameters or syntax |
| 401 | Unauthorized | Missing or invalid authentication |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Optimistic concurrency failure |
| 412 | Precondition Failed | If-Match header mismatch |
| 500 | Internal Server Error | Server-side error |
| 501 | Not Implemented | Feature not enabled |

### Error Response Format

**OGC API / JSON**:
```json
{
  "type": "https://honua.io/errors/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "The 'bbox' parameter must contain 4 coordinates",
  "instance": "/ogc/collections/roads/items"
}
```

**Geoservices REST a.k.a. Esri REST**:
```json
{
  "error": {
    "code": 400,
    "message": "Invalid where clause",
    "details": ["Syntax error near 'SELET'"]
  }
}
```

**WFS/WMS XML**:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<ExceptionReport version="2.0.0" xmlns="http://www.opengis.net/ows/2.0">
  <Exception exceptionCode="InvalidParameterValue" locator="bbox">
    <ExceptionText>Bounding box must contain 4 values</ExceptionText>
  </Exception>
</ExceptionReport>
```

### Common Error Scenarios

**Missing Required Parameter**:
```bash
curl "https://localhost:5000/wms?service=WMS&request=GetMap&layers=roads"
# Error: Missing required parameter 'bbox'
```

**Invalid CQL Syntax**:
```bash
curl "https://localhost:5000/ogc/collections/roads/items?filter=population >"
# Error: CQL syntax error - expected value after '>'
```

**Invalid Spatial Reference**:
```bash
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outSR=99999"
# Error: Unsupported spatial reference: 99999
```

**Unauthorized**:
```bash
curl -X POST "https://localhost:5000/ogc/collections/roads/items" -d '{...}'
# Error 401: Authorization required for this operation
```

**Optimistic Concurrency Failure**:
```bash
curl -X PUT "https://localhost:5000/ogc/collections/roads/items/123" \
  -H "If-Match: \"old-etag\"" -d '{...}'
# Error 409: Resource has been modified by another user
```

---

## Performance Optimization

### Pagination Best Practices

**OGC API Features**:
```bash
# Start with reasonable page size
curl "https://localhost:5000/ogc/collections/roads/items?limit=100"

# Follow 'next' links for subsequent pages
curl "https://localhost:5000/ogc/collections/roads/items?limit=100&offset=100"
```

**Geoservices REST a.k.a. Esri REST**:
```bash
# Use resultOffset and resultRecordCount
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&resultOffset=0&resultRecordCount=100"
```

### Field Selection

**OGC API**:
```bash
# Request only needed properties
curl "https://localhost:5000/ogc/collections/roads/items?properties=name,lanes&limit=100"
```

**Geoservices REST a.k.a. Esri REST**:
```bash
# Use outFields parameter
curl "https://localhost:5000/rest/services/Public/Parcels/FeatureServer/0/query?where=1=1&outFields=PARCEL_ID,OWNER_NAME"
```

**OData**:
```bash
# Use $select
curl "https://localhost:5000/odata/Parcels?\$select=parcel_id,owner_name&\$top=100"
```

### Spatial Indexing

Ensure spatial filters leverage database indexes:

```bash
# Good - Uses spatial index
curl "https://localhost:5000/ogc/collections/parcels/items?bbox=-122.5,45.4,-122.3,45.6"

# Better - Smaller bbox for focused queries
curl "https://localhost:5000/ogc/collections/parcels/items?bbox=-122.45,45.52,-122.43,45.54"
```

### Compression

Enable HTTP compression:
```bash
curl "https://localhost:5000/ogc/collections/roads/items?limit=1000" \
  -H "Accept-Encoding: gzip"
```

---

This comprehensive API reference enables developers to integrate with all Honua service protocols. For implementation examples and client code, see the [Integration Guides](/docs/rag/03-integration/).

For metadata configuration to enable these services, refer to [Metadata Configuration](/docs/rag/01-getting-started/metadata-configuration.md).

For deployment and service configuration, see [Deployment Guide](/docs/rag/02-deployment/).
