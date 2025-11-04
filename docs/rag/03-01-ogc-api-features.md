---
tags: [ogc, api, features, geojson, rest, filtering, cql2, spatial-queries, crud]
category: api-reference
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# OGC API Features Complete Guide

Comprehensive guide to Honua's OGC API Features implementation with working examples.

## Table of Contents
- [Overview](#overview)
- [Base Endpoints](#base-endpoints)
- [Landing Page](#landing-page)
- [Conformance](#conformance)
- [Collections](#collections)
- [Features (Items)](#features-items)
- [Querying Features](#querying-features)
- [Filtering](#filtering)
- [Coordinate Reference Systems](#coordinate-reference-systems)
- [Output Formats](#output-formats)
- [Creating Features](#creating-features)
- [Updating Features](#updating-features)
- [Deleting Features](#deleting-features)
- [Queryables](#queryables)
- [Styles](#styles)
- [Search Across Collections](#search-across-collections)
- [Error Handling](#error-handling)
- [Related Documentation](#related-documentation)

## Overview

OGC API Features is a modern, RESTful replacement for WFS (Web Feature Service). Honua implements OGC API - Features - Part 1: Core, Part 2: CRS, Part 3: Filtering, and Part 4: Create, Replace, Update, Delete.

### Key Features

- **RESTful**: Standard HTTP methods (GET, POST, PUT, PATCH, DELETE)
- **Multiple formats**: GeoJSON, HTML, GeoPackage, Shapefile, KML, CSV
- **CQL2 filtering**: Advanced spatial and attribute queries
- **Multiple CRS**: Support for various coordinate systems
- **CRUD operations**: Full create, read, update, delete support
- **Pagination**: Efficient handling of large datasets

### Base URL Structure

```
http://localhost:5000/ogc/
```

All OGC API Features endpoints are under the `/ogc` path.

## Base Endpoints

### API Overview

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ogc` | GET | Landing page |
| `/ogc/conformance` | GET | Conformance classes |
| `/ogc/collections` | GET | List all collections |
| `/ogc/collections/{collectionId}` | GET | Collection metadata |
| `/ogc/collections/{collectionId}/items` | GET | Query features |
| `/ogc/collections/{collectionId}/items/{featureId}` | GET | Get single feature |
| `/ogc/collections/{collectionId}/items` | POST | Create feature(s) |
| `/ogc/collections/{collectionId}/items/{featureId}` | PUT | Replace feature |
| `/ogc/collections/{collectionId}/items/{featureId}` | PATCH | Update feature |
| `/ogc/collections/{collectionId}/items/{featureId}` | DELETE | Delete feature |

## Landing Page

The landing page provides links to all available resources.

### Request

```bash
curl http://localhost:5000/ogc
```

### Response

```json
{
  "title": "Honua Geospatial Server",
  "description": "OGC-compliant geospatial server",
  "links": [
    {
      "href": "http://localhost:5000/ogc",
      "rel": "self",
      "type": "application/json",
      "title": "This document"
    },
    {
      "href": "http://localhost:5000/ogc/conformance",
      "rel": "conformance",
      "type": "application/json",
      "title": "Conformance declaration"
    },
    {
      "href": "http://localhost:5000/ogc/collections",
      "rel": "data",
      "type": "application/json",
      "title": "Collections"
    }
  ]
}
```

## Conformance

Lists OGC conformance classes implemented by the server.

### Request

```bash
curl http://localhost:5000/ogc/conformance
```

### Response

```json
{
  "conformsTo": [
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/html",
    "http://www.opengis.net/spec/ogcapi-features-2/1.0/conf/crs",
    "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter",
    "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/features-filter",
    "http://www.opengis.net/spec/ogcapi-features-4/1.0/conf/create-replace-delete"
  ]
}
```

## Collections

Collections represent feature types (layers) available for querying.

### List All Collections

```bash
curl http://localhost:5000/ogc/collections
```

### Response

```json
{
  "links": [
    {
      "href": "http://localhost:5000/ogc/collections",
      "rel": "self",
      "type": "application/json"
    }
  ],
  "collections": [
    {
      "id": "cities",
      "title": "World Cities",
      "description": "Major cities around the world",
      "extent": {
        "spatial": {
          "bbox": [[-180, -90, 180, 90]],
          "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
        }
      },
      "links": [
        {
          "href": "http://localhost:5000/ogc/collections/cities",
          "rel": "self",
          "type": "application/json"
        },
        {
          "href": "http://localhost:5000/ogc/collections/cities/items",
          "rel": "items",
          "type": "application/geo+json"
        }
      ]
    }
  ]
}
```

### Get Single Collection

```bash
curl http://localhost:5000/ogc/collections/cities
```

### Response

```json
{
  "id": "cities",
  "title": "World Cities",
  "description": "Major cities around the world",
  "extent": {
    "spatial": {
      "bbox": [[-180, -90, 180, 90]],
      "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
    }
  },
  "itemType": "feature",
  "crs": [
    "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
    "http://www.opengis.net/def/crs/EPSG/0/4326",
    "http://www.opengis.net/def/crs/EPSG/0/3857"
  ],
  "storageCrs": "http://www.opengis.net/def/crs/EPSG/0/4326",
  "links": [
    {
      "href": "http://localhost:5000/ogc/collections/cities/items",
      "rel": "items",
      "type": "application/geo+json",
      "title": "Features"
    },
    {
      "href": "http://localhost:5000/ogc/collections/cities/queryables",
      "rel": "queryables",
      "type": "application/schema+json",
      "title": "Queryable properties"
    }
  ]
}
```

## Features (Items)

Query features from a collection.

### Get All Features

```bash
curl http://localhost:5000/ogc/collections/cities/items
```

### Response

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": "1",
      "geometry": {
        "type": "Point",
        "coordinates": [-73.935242, 40.730610]
      },
      "properties": {
        "name": "New York",
        "population": 8336817,
        "country": "USA"
      }
    }
  ],
  "timeStamp": "2025-10-15T12:00:00Z",
  "numberMatched": 1234,
  "numberReturned": 10,
  "links": [
    {
      "href": "http://localhost:5000/ogc/collections/cities/items",
      "rel": "self",
      "type": "application/geo+json"
    },
    {
      "href": "http://localhost:5000/ogc/collections/cities/items?offset=10",
      "rel": "next",
      "type": "application/geo+json"
    }
  ]
}
```

### Get Single Feature

```bash
curl http://localhost:5000/ogc/collections/cities/items/1
```

### Response

```json
{
  "type": "Feature",
  "id": "1",
  "geometry": {
    "type": "Point",
    "coordinates": [-73.935242, 40.730610]
  },
  "properties": {
    "name": "New York",
    "population": 8336817,
    "country": "USA"
  },
  "links": [
    {
      "href": "http://localhost:5000/ogc/collections/cities/items/1",
      "rel": "self",
      "type": "application/geo+json"
    }
  ]
}
```

## Querying Features

### Pagination

Use `limit` and `offset` parameters.

```bash
# Get 10 features
curl "http://localhost:5000/ogc/collections/cities/items?limit=10"

# Get next page (skip first 10)
curl "http://localhost:5000/ogc/collections/cities/items?limit=10&offset=10"
```

**Parameters:**
- `limit`: Max features to return (default: 100, max: 1000)
- `offset`: Number of features to skip (default: 0)

### Bounding Box Filter

Spatial filter using bbox parameter.

```bash
# New York City area (minLon, minLat, maxLon, maxLat)
curl "http://localhost:5000/ogc/collections/cities/items?bbox=-74.05,40.68,-73.90,40.80"
```

**Format:** `bbox=minLon,minLat,maxLon,maxLat` (WGS 84)

**With CRS:**
```bash
# Web Mercator bbox
curl "http://localhost:5000/ogc/collections/cities/items?bbox=-8238310,4970241,-8226310,4982241&bbox-crs=http://www.opengis.net/def/crs/EPSG/0/3857"
```

### Property Selection

Select specific properties to return.

```bash
# Return only name and population
curl "http://localhost:5000/ogc/collections/cities/items?properties=name,population"
```

### Datetime Filter

Filter by temporal extent.

```bash
# Features from specific date
curl "http://localhost:5000/ogc/collections/events/items?datetime=2025-10-15"

# Date range
curl "http://localhost:5000/ogc/collections/events/items?datetime=2025-10-01/2025-10-31"

# Open-ended (after date)
curl "http://localhost:5000/ogc/collections/events/items?datetime=2025-10-01/.."

# Open-ended (before date)
curl "http://localhost:5000/ogc/collections/events/items?datetime=../2025-10-31"
```

### Sorting

Order results by property values.

```bash
# Sort by population ascending
curl "http://localhost:5000/ogc/collections/cities/items?sortby=population"

# Sort descending (prefix with -)
curl "http://localhost:5000/ogc/collections/cities/items?sortby=-population"

# Multiple sort fields
curl "http://localhost:5000/ogc/collections/cities/items?sortby=country,-population"
```

## Filtering

Advanced filtering using CQL (Common Query Language).

### CQL Text Format

```bash
# Simple property filter
curl "http://localhost:5000/ogc/collections/cities/items?filter=population > 1000000&filter-lang=cql-text"

# Multiple conditions with AND
curl "http://localhost:5000/ogc/collections/cities/items?filter=population > 1000000 AND country = 'USA'&filter-lang=cql-text"

# OR condition
curl "http://localhost:5000/ogc/collections/cities/items?filter=country = 'USA' OR country = 'Canada'&filter-lang=cql-text"

# IN operator
curl "http://localhost:5000/ogc/collections/cities/items?filter=country IN ('USA', 'Canada', 'Mexico')&filter-lang=cql-text"

# LIKE operator (pattern matching)
curl "http://localhost:5000/ogc/collections/cities/items?filter=name LIKE 'New%'&filter-lang=cql-text"
```

### CQL2 JSON Format

```bash
curl -X POST http://localhost:5000/ogc/collections/cities/items \
  -H "Content-Type: application/json" \
  -d '{
    "collections": ["cities"],
    "filter": {
      "op": "and",
      "args": [
        {
          "op": ">",
          "args": [{"property": "population"}, 1000000]
        },
        {
          "op": "=",
          "args": [{"property": "country"}, "USA"]
        }
      ]
    },
    "filter-lang": "cql2-json"
  }'
```

### Spatial Filters

```bash
# INTERSECTS - geometry intersects bbox
curl "http://localhost:5000/ogc/collections/parcels/items?filter=INTERSECTS(geometry, ENVELOPE(-122.5, -122.3, 37.9, 37.7))&filter-lang=cql-text"

# WITHIN - geometry is within bbox
curl "http://localhost:5000/ogc/collections/parcels/items?filter=WITHIN(geometry, ENVELOPE(-122.5, -122.3, 37.9, 37.7))&filter-lang=cql-text"

# CONTAINS - geometry contains point
curl "http://localhost:5000/ogc/collections/zones/items?filter=CONTAINS(geometry, POINT(-122.4, 37.8))&filter-lang=cql-text"

# CROSSES, OVERLAPS, TOUCHES also supported
```

### Numeric Comparisons

```bash
# Greater than
curl "http://localhost:5000/ogc/collections/cities/items?filter=population > 5000000&filter-lang=cql-text"

# Less than or equal
curl "http://localhost:5000/ogc/collections/cities/items?filter=elevation <= 100&filter-lang=cql-text"

# Between (range)
curl "http://localhost:5000/ogc/collections/cities/items?filter=population BETWEEN 1000000 AND 5000000&filter-lang=cql-text"
```

### String Comparisons

```bash
# Case-sensitive equality
curl "http://localhost:5000/ogc/collections/cities/items?filter=country = 'USA'&filter-lang=cql-text"

# Pattern matching
curl "http://localhost:5000/ogc/collections/cities/items?filter=name LIKE 'San%'&filter-lang=cql-text"

# Case-insensitive (use LOWER or UPPER)
curl "http://localhost:5000/ogc/collections/cities/items?filter=LOWER(country) = 'usa'&filter-lang=cql-text"
```

### Null Checks

```bash
# IS NULL
curl "http://localhost:5000/ogc/collections/cities/items?filter=elevation IS NULL&filter-lang=cql-text"

# IS NOT NULL
curl "http://localhost:5000/ogc/collections/cities/items?filter=elevation IS NOT NULL&filter-lang=cql-text"
```

## Coordinate Reference Systems

### Request Specific CRS

```bash
# Request features in Web Mercator (EPSG:3857)
curl "http://localhost:5000/ogc/collections/cities/items?crs=http://www.opengis.net/def/crs/EPSG/0/3857"

# Short form (EPSG code)
curl "http://localhost:5000/ogc/collections/cities/items?crs=EPSG:3857"
```

### Response Headers

The server returns the CRS in response headers:

```
Content-Crs: <http://www.opengis.net/def/crs/EPSG/0/3857>
```

### Supported CRS

Common coordinate systems:
- CRS84 (WGS 84 lon/lat): `http://www.opengis.net/def/crs/OGC/1.3/CRS84`
- EPSG:4326 (WGS 84): `http://www.opengis.net/def/crs/EPSG/0/4326`
- EPSG:3857 (Web Mercator): `http://www.opengis.net/def/crs/EPSG/0/3857`

## Output Formats

Request different output formats using the `f` parameter or `Accept` header.

### GeoJSON (Default)

```bash
curl "http://localhost:5000/ogc/collections/cities/items?f=json"
# OR
curl -H "Accept: application/geo+json" http://localhost:5000/ogc/collections/cities/items
```

### HTML

```bash
curl "http://localhost:5000/ogc/collections/cities/items?f=html"
# OR
curl -H "Accept: text/html" http://localhost:5000/ogc/collections/cities/items
```

### GeoPackage

```bash
# Download as GeoPackage file
curl "http://localhost:5000/ogc/collections/cities/items?f=gpkg" -o cities.gpkg

# With filtering
curl "http://localhost:5000/ogc/collections/cities/items?filter=population > 1000000&f=gpkg" -o large_cities.gpkg
```

### Shapefile

```bash
# Download as Shapefile (ZIP archive)
curl "http://localhost:5000/ogc/collections/cities/items?f=shp" -o cities.zip
```

### KML/KMZ

```bash
# KML (XML format)
curl "http://localhost:5000/ogc/collections/cities/items?f=kml" -o cities.kml

# KMZ (compressed)
curl "http://localhost:5000/ogc/collections/cities/items?f=kmz" -o cities.kmz
```

**Note:** KML/KMZ only supports CRS84 (WGS 84 lon/lat).

### FlatGeobuf

```bash
# High-performance binary format
curl "http://localhost:5000/ogc/collections/cities/items?f=fgb" -o cities.fgb
```

### GeoArrow

```bash
# Apache Arrow columnar format with geometry
curl "http://localhost:5000/ogc/collections/cities/items?f=arrow" -o cities.arrow
```

### CSV

```bash
# CSV with WKT geometry
curl "http://localhost:5000/ogc/collections/cities/items?f=csv" -o cities.csv
```

## Creating Features

Create new features using POST requests.

### Create Single Feature

```bash
curl -X POST http://localhost:5000/ogc/collections/cities/items \
  -H "Content-Type: application/geo+json" \
  -d '{
    "type": "Feature",
    "geometry": {
      "type": "Point",
      "coordinates": [-122.4194, 37.7749]
    },
    "properties": {
      "name": "San Francisco",
      "population": 883305,
      "country": "USA"
    }
  }'
```

### Response

```json
{
  "type": "Feature",
  "id": "123",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "name": "San Francisco",
    "population": 883305,
    "country": "USA"
  }
}
```

**Response Headers:**
```
HTTP/1.1 201 Created
Location: http://localhost:5000/ogc/collections/cities/items/123
ETag: "abc123"
```

### Create Multiple Features

```bash
curl -X POST http://localhost:5000/ogc/collections/cities/items \
  -H "Content-Type: application/geo+json" \
  -d '{
    "type": "FeatureCollection",
    "features": [
      {
        "type": "Feature",
        "geometry": {
          "type": "Point",
          "coordinates": [-122.4194, 37.7749]
        },
        "properties": {
          "name": "San Francisco",
          "population": 883305
        }
      },
      {
        "type": "Feature",
        "geometry": {
          "type": "Point",
          "coordinates": [-118.2437, 34.0522]
        },
        "properties": {
          "name": "Los Angeles",
          "population": 3979576
        }
      }
    ]
  }'
```

## Updating Features

Update existing features using PUT or PATCH.

### Replace Feature (PUT)

Replaces entire feature.

```bash
curl -X PUT http://localhost:5000/ogc/collections/cities/items/123 \
  -H "Content-Type: application/geo+json" \
  -d '{
    "type": "Feature",
    "geometry": {
      "type": "Point",
      "coordinates": [-122.4194, 37.7749]
    },
    "properties": {
      "name": "San Francisco",
      "population": 900000,
      "country": "USA",
      "state": "California"
    }
  }'
```

### Partial Update (PATCH)

Updates only specified properties.

```bash
curl -X PATCH http://localhost:5000/ogc/collections/cities/items/123 \
  -H "Content-Type: application/geo+json" \
  -d '{
    "type": "Feature",
    "properties": {
      "population": 900000
    }
  }'
```

### Conditional Updates (ETag)

Use ETags to prevent conflicts.

```bash
# Get current ETag
curl -I http://localhost:5000/ogc/collections/cities/items/123

# Update with If-Match header
curl -X PUT http://localhost:5000/ogc/collections/cities/items/123 \
  -H "Content-Type: application/geo+json" \
  -H "If-Match: \"abc123\"" \
  -d '{...}'
```

**Response on conflict:**
```
HTTP/1.1 412 Precondition Failed
```

## Deleting Features

Delete features using DELETE method.

### Delete Single Feature

```bash
curl -X DELETE http://localhost:5000/ogc/collections/cities/items/123
```

**Response:**
```
HTTP/1.1 204 No Content
```

### Conditional Delete

```bash
curl -X DELETE http://localhost:5000/ogc/collections/cities/items/123 \
  -H "If-Match: \"abc123\""
```

## Queryables

Discover queryable properties for a collection.

### Request

```bash
curl http://localhost:5000/ogc/collections/cities/queryables
```

### Response

```json
{
  "$schema": "https://json-schema.org/draft/2019-09/schema",
  "$id": "http://localhost:5000/ogc/collections/cities/queryables",
  "type": "object",
  "title": "World Cities",
  "properties": {
    "name": {
      "type": "string",
      "title": "City Name"
    },
    "population": {
      "type": "integer",
      "title": "Population"
    },
    "country": {
      "type": "string",
      "title": "Country"
    },
    "geometry": {
      "$ref": "https://geojson.org/schema/Geometry.json"
    }
  }
}
```

## Styles

Access styling information for collections.

### List Styles for Collection

```bash
curl http://localhost:5000/ogc/collections/cities/styles
```

### Response

```json
{
  "collectionId": "cities",
  "defaultStyle": "population-heatmap",
  "styles": [
    {
      "id": "population-heatmap",
      "title": "Population Heatmap",
      "isDefault": true,
      "geometryType": "point",
      "renderer": "simple",
      "links": [
        {
          "href": "http://localhost:5000/ogc/collections/cities/styles/population-heatmap",
          "rel": "stylesheet",
          "type": "application/vnd.ogc.sld+xml"
        }
      ]
    }
  ]
}
```

### Get Style Definition

```bash
# JSON format
curl http://localhost:5000/ogc/collections/cities/styles/population-heatmap

# SLD format
curl "http://localhost:5000/ogc/collections/cities/styles/population-heatmap?f=sld"
```

## Search Across Collections

Query multiple collections simultaneously.

### GET Request

```bash
curl "http://localhost:5000/ogc/search?collections=cities,towns&filter=population > 100000&filter-lang=cql-text"
```

### POST Request

```bash
curl -X POST http://localhost:5000/ogc/search \
  -H "Content-Type: application/json" \
  -d '{
    "collections": ["cities", "towns"],
    "filter": {
      "op": ">",
      "args": [{"property": "population"}, 100000]
    },
    "filter-lang": "cql2-json",
    "limit": 50
  }'
```

## Error Handling

Honua returns RFC 7807 Problem Details for errors.

### Not Found (404)

```bash
curl http://localhost:5000/ogc/collections/nonexistent/items
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Collection 'nonexistent' was not found."
}
```

### Bad Request (400)

```bash
curl "http://localhost:5000/ogc/collections/cities/items?filter=invalid syntax&filter-lang=cql-text"
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid CQL2 filter expression.",
  "instance": "filter"
}
```

### Rate Limit (429)

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Retry after 60 seconds."
}
```

## Performance Tips

### 1. Use Pagination Efficiently

```bash
# BAD: Requesting all features
curl "http://localhost:5000/ogc/collections/cities/items?limit=1000000"

# GOOD: Use reasonable page sizes
curl "http://localhost:5000/ogc/collections/cities/items?limit=100"
```

### 2. Select Only Needed Properties

```bash
# BAD: Return all properties
curl "http://localhost:5000/ogc/collections/cities/items"

# GOOD: Select specific properties
curl "http://localhost:5000/ogc/collections/cities/items?properties=name,population"
```

### 3. Use Spatial Filters

```bash
# BAD: Query entire dataset
curl "http://localhost:5000/ogc/collections/parcels/items"

# GOOD: Filter by bbox
curl "http://localhost:5000/ogc/collections/parcels/items?bbox=-122.5,-122.3,37.7,37.9"
```

### 4. Leverage Export Formats

```bash
# BAD: Process GeoJSON in client
curl "http://localhost:5000/ogc/collections/cities/items" | process_geojson

# GOOD: Use optimized format
curl "http://localhost:5000/ogc/collections/cities/items?f=gpkg" -o cities.gpkg
```

## Complete Example Workflows

### Workflow 1: Query Large Datasets

```bash
# Step 1: Get collection metadata
curl http://localhost:5000/ogc/collections/buildings

# Step 2: Query queryables to understand schema
curl http://localhost:5000/ogc/collections/buildings/queryables

# Step 3: Query with spatial and attribute filters
curl "http://localhost:5000/ogc/collections/buildings/items?\
bbox=-122.5,-122.3,37.7,37.9&\
filter=building_type='residential' AND floors > 2&\
filter-lang=cql-text&\
limit=100&\
properties=address,floors,year_built"

# Step 4: Export results
curl "http://localhost:5000/ogc/collections/buildings/items?\
bbox=-122.5,-122.3,37.7,37.9&\
filter=building_type='residential' AND floors > 2&\
filter-lang=cql-text&\
f=gpkg" -o residential_buildings.gpkg
```

### Workflow 2: Create and Update Features

```bash
# Step 1: Create new feature
curl -X POST http://localhost:5000/ogc/collections/pois/items \
  -H "Content-Type: application/geo+json" \
  -d '{
    "type": "Feature",
    "geometry": {"type": "Point", "coordinates": [-122.4, 37.8]},
    "properties": {"name": "New POI", "category": "restaurant"}
  }'

# Response includes ID and ETag
# Location: http://localhost:5000/ogc/collections/pois/items/456
# ETag: "xyz789"

# Step 2: Update the feature
curl -X PATCH http://localhost:5000/ogc/collections/pois/items/456 \
  -H "Content-Type: application/geo+json" \
  -H "If-Match: \"xyz789\"" \
  -d '{
    "type": "Feature",
    "properties": {"rating": 4.5}
  }'

# Step 3: Verify update
curl http://localhost:5000/ogc/collections/pois/items/456
```

## Related Documentation

- [Architecture Overview](01-01-architecture-overview.md) - System architecture
- [Configuration Reference](02-01-configuration-reference.md) - Configuration options
- [Docker Deployment](04-01-docker-deployment.md) - Deployment guide
- [Common Issues](05-02-common-issues.md) - Troubleshooting

## Keywords for Search

OGC API Features, REST API, GeoJSON, features, items, collections, filtering, CQL2, spatial queries, CRUD, create features, update features, delete features, pagination, bbox, coordinate systems, CRS, output formats, GeoPackage, Shapefile, KML, performance, queryables, styles

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Covers**: Honua Server 1.0.0-rc1
