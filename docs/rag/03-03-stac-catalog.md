---
tags: [stac, catalog, metadata, search, spatiotemporal, assets, collections, items]
category: api-reference
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# STAC (SpatioTemporal Asset Catalog) Implementation Guide

Complete guide to Honua's STAC 1.0.0 API implementation for catalog discovery and spatiotemporal search.

## Table of Contents
- [Overview](#overview)
- [STAC Endpoints](#stac-endpoints)
- [Root Catalog](#root-catalog)
- [Collections](#collections)
- [Items](#items)
- [Search API](#search-api)
- [Extensions](#extensions)
- [COG and Zarr Assets](#cog-and-zarr-assets)
- [Client Integration](#client-integration)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

STAC (SpatioTemporal Asset Catalog) is a specification for describing geospatial information, making it searchable and queryable.

### What is STAC?

STAC provides:
- **Collections**: Groups of related items
- **Items**: Individual spatiotemporal assets (scenes, tiles, datasets)
- **Assets**: Files (COG, Zarr, metadata)
- **Search**: Spatiotemporal queries

### STAC Version

Honua implements **STAC 1.0.0** with these conformance classes:
- STAC API - Core
- STAC API - Collections
- STAC API - Features (Items)
- STAC API - Item Search
- OGC API - Features

### Base URL

```
http://localhost:5000/stac
```

## STAC Endpoints

### API Overview

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/stac` | GET | Root catalog |
| `/stac/conformance` | GET | Conformance classes |
| `/stac/collections` | GET | List collections |
| `/stac/collections/{collectionId}` | GET | Collection details |
| `/stac/collections/{collectionId}/items` | GET | Collection items |
| `/stac/collections/{collectionId}/items/{itemId}` | GET | Single item |
| `/stac/search` | GET, POST | Search items |

## Root Catalog

The root catalog provides links to all STAC resources.

### Request

```bash
curl http://localhost:5000/stac
```

### Response

```json
{
  "stac_version": "1.0.0",
  "type": "Catalog",
  "id": "honua-catalog",
  "title": "Honua Geospatial Server",
  "description": "STAC API for geospatial data discovery",
  "links": [
    {
      "rel": "self",
      "type": "application/json",
      "href": "http://localhost:5000/stac"
    },
    {
      "rel": "root",
      "type": "application/json",
      "href": "http://localhost:5000/stac"
    },
    {
      "rel": "conformance",
      "type": "application/json",
      "href": "http://localhost:5000/stac/conformance"
    },
    {
      "rel": "data",
      "type": "application/json",
      "href": "http://localhost:5000/stac/collections"
    },
    {
      "rel": "search",
      "type": "application/geo+json",
      "href": "http://localhost:5000/stac/search",
      "method": "GET"
    },
    {
      "rel": "search",
      "type": "application/geo+json",
      "href": "http://localhost:5000/stac/search",
      "method": "POST"
    }
  ]
}
```

### Conformance

```bash
curl http://localhost:5000/stac/conformance
```

```json
{
  "conformsTo": [
    "https://api.stacspec.org/v1.0.0/core",
    "https://api.stacspec.org/v1.0.0/collections",
    "https://api.stacspec.org/v1.0.0/item-search",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson"
  ]
}
```

## Collections

Collections group related STAC items.

### List Collections

**Request:**
```bash
curl http://localhost:5000/stac/collections
```

**Response:**
```json
{
  "collections": [
    {
      "stac_version": "1.0.0",
      "type": "Collection",
      "id": "elevation",
      "title": "Global Elevation",
      "description": "Digital Elevation Model at 30m resolution",
      "license": "CC-BY-4.0",
      "extent": {
        "spatial": {
          "bbox": [[-180, -90, 180, 90]]
        },
        "temporal": {
          "interval": [["2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z"]]
        }
      },
      "links": [
        {
          "rel": "self",
          "type": "application/json",
          "href": "http://localhost:5000/stac/collections/elevation"
        },
        {
          "rel": "items",
          "type": "application/geo+json",
          "href": "http://localhost:5000/stac/collections/elevation/items"
        },
        {
          "rel": "root",
          "type": "application/json",
          "href": "http://localhost:5000/stac"
        }
      ],
      "keywords": ["elevation", "dem", "terrain"],
      "providers": [
        {
          "name": "Honua",
          "roles": ["host"],
          "url": "https://honua.io"
        }
      ]
    }
  ],
  "links": [
    {
      "rel": "self",
      "type": "application/json",
      "href": "http://localhost:5000/stac/collections"
    },
    {
      "rel": "root",
      "type": "application/json",
      "href": "http://localhost:5000/stac"
    }
  ]
}
```

### Get Collection

**Request:**
```bash
curl http://localhost:5000/stac/collections/elevation
```

**Response:** Single collection object with full metadata.

## Items

Items represent individual spatiotemporal assets within a collection.

### List Items

**Request:**
```bash
curl http://localhost:5000/stac/collections/elevation/items
```

**With Pagination:**
```bash
curl "http://localhost:5000/stac/collections/elevation/items?limit=10&page=2"
```

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "stac_version": "1.0.0",
      "stac_extensions": [
        "https://stac-extensions.github.io/projection/v1.0.0/schema.json"
      ],
      "type": "Feature",
      "id": "elevation_tile_001",
      "collection": "elevation",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-180, -90],
          [-180, 90],
          [180, 90],
          [180, -90],
          [-180, -90]
        ]]
      },
      "bbox": [-180, -90, 180, 90],
      "properties": {
        "datetime": "2024-01-15T00:00:00Z",
        "created": "2024-01-15T10:30:00Z",
        "updated": "2024-01-15T10:30:00Z",
        "proj:epsg": 4326,
        "proj:bbox": [-180, -90, 180, 90],
        "proj:shape": [21600, 43200],
        "proj:transform": [0.008333333, 0, -180, 0, -0.008333333, 90]
      },
      "assets": {
        "data": {
          "href": "https://storage.honua.io/elevation/tile_001.tif",
          "type": "image/tiff; application=geotiff; profile=cloud-optimized",
          "roles": ["data"],
          "title": "Elevation COG",
          "proj:epsg": 4326
        },
        "thumbnail": {
          "href": "https://storage.honua.io/elevation/tile_001_thumb.png",
          "type": "image/png",
          "roles": ["thumbnail"],
          "title": "Thumbnail"
        }
      },
      "links": [
        {
          "rel": "self",
          "type": "application/geo+json",
          "href": "http://localhost:5000/stac/collections/elevation/items/elevation_tile_001"
        },
        {
          "rel": "collection",
          "type": "application/json",
          "href": "http://localhost:5000/stac/collections/elevation"
        },
        {
          "rel": "root",
          "type": "application/json",
          "href": "http://localhost:5000/stac"
        }
      ]
    }
  ],
  "links": [
    {
      "rel": "self",
      "type": "application/geo+json",
      "href": "http://localhost:5000/stac/collections/elevation/items"
    },
    {
      "rel": "next",
      "type": "application/geo+json",
      "href": "http://localhost:5000/stac/collections/elevation/items?page=2"
    }
  ]
}
```

### Get Single Item

**Request:**
```bash
curl http://localhost:5000/stac/collections/elevation/items/elevation_tile_001
```

**Response:** Single feature object.

## Search API

STAC Search provides spatiotemporal queries across all collections.

### GET Search

**Basic Search:**
```bash
curl "http://localhost:5000/stac/search?limit=10"
```

**Spatial Search (BBox):**
```bash
curl "http://localhost:5000/stac/search?bbox=-120,35,-115,40&limit=10"
```

**Temporal Search:**
```bash
curl "http://localhost:5000/stac/search?datetime=2024-01-01T00:00:00Z/2024-12-31T23:59:59Z"
```

**Collection Filter:**
```bash
curl "http://localhost:5000/stac/search?collections=elevation,landcover&limit=10"
```

**Combined:**
```bash
curl "http://localhost:5000/stac/search?bbox=-120,35,-115,40&datetime=2024-01-01T00:00:00Z/..&collections=elevation"
```

### POST Search

**Request:**
```bash
curl -X POST http://localhost:5000/stac/search \
  -H "Content-Type: application/json" \
  -d '{
    "collections": ["elevation"],
    "bbox": [-120, 35, -115, 40],
    "datetime": "2024-01-01T00:00:00Z/..",
    "limit": 10
  }'
```

**Intersects (GeoJSON Geometry):**
```bash
curl -X POST http://localhost:5000/stac/search \
  -H "Content-Type: application/json" \
  -d '{
    "intersects": {
      "type": "Polygon",
      "coordinates": [[
        [-120, 35],
        [-120, 40],
        [-115, 40],
        [-115, 35],
        [-120, 35]
      ]]
    },
    "limit": 10
  }'
```

### Search Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `collections` | array | Collection IDs | `["elevation"]` |
| `bbox` | array | Bounding box [west,south,east,north] | `[-120,35,-115,40]` |
| `datetime` | string | Temporal filter | `2024-01-01T00:00:00Z/..` |
| `intersects` | object | GeoJSON geometry | `{"type":"Polygon",...}` |
| `ids` | array | Item IDs | `["tile_001","tile_002"]` |
| `limit` | integer | Max results | `10` (default: 100) |
| `page` | integer | Page number | `2` |

### Datetime Formats

**Single timestamp:**
```
2024-01-15T10:30:00Z
```

**Range:**
```
2024-01-01T00:00:00Z/2024-12-31T23:59:59Z
```

**Open-ended (from start to now):**
```
../2024-12-31T23:59:59Z
```

**Open-ended (from date to end):**
```
2024-01-01T00:00:00Z/..
```

## Extensions

Honua supports these STAC extensions:

### Projection Extension

Adds projection/CRS information.

**Extension URL:**
```
https://stac-extensions.github.io/projection/v1.0.0/schema.json
```

**Properties:**
- `proj:epsg`: EPSG code (e.g., 4326)
- `proj:wkt2`: WKT2 CRS definition
- `proj:bbox`: Projected bbox
- `proj:shape`: Raster dimensions [height, width]
- `proj:transform`: Affine transform

**Example:**
```json
{
  "properties": {
    "proj:epsg": 32610,
    "proj:bbox": [500000, 4000000, 600000, 4100000],
    "proj:shape": [10000, 10000],
    "proj:transform": [10, 0, 500000, 0, -10, 4100000]
  }
}
```

### EO Extension (Planned)

Electro-Optical extension for satellite imagery metadata.

## COG and Zarr Assets

Honua exposes Cloud Optimized GeoTIFF (COG) and Zarr datasets as STAC assets.

### COG Asset

```json
{
  "assets": {
    "data": {
      "href": "https://storage.honua.io/data/elevation.tif",
      "type": "image/tiff; application=geotiff; profile=cloud-optimized",
      "roles": ["data"],
      "title": "Elevation Data",
      "proj:epsg": 4326,
      "file:size": 524288000
    }
  }
}
```

### Zarr Asset

```json
{
  "assets": {
    "zarr": {
      "href": "https://storage.honua.io/data/temperature.zarr",
      "type": "application/vnd+zarr",
      "roles": ["data"],
      "title": "Temperature Zarr Store",
      "proj:epsg": 4326,
      "cube:dimensions": {
        "time": {"type": "temporal"},
        "lat": {"type": "spatial"},
        "lon": {"type": "spatial"}
      }
    }
  }
}
```

### Multiple Assets

```json
{
  "assets": {
    "visual": {
      "href": "https://storage.honua.io/data/scene_rgb.tif",
      "type": "image/tiff; application=geotiff; profile=cloud-optimized",
      "roles": ["visual"],
      "title": "RGB Visual"
    },
    "nir": {
      "href": "https://storage.honua.io/data/scene_nir.tif",
      "type": "image/tiff; application=geotiff",
      "roles": ["data"],
      "eo:bands": [{"name": "nir", "common_name": "nir"}]
    },
    "metadata": {
      "href": "https://storage.honua.io/data/scene_metadata.xml",
      "type": "application/xml",
      "roles": ["metadata"],
      "title": "ISO 19115 Metadata"
    }
  }
}
```

## Client Integration

### Python (PySTAC Client)

```python
from pystac_client import Client

# Connect to STAC API
catalog = Client.open("http://localhost:5000/stac")

# Search for items
search = catalog.search(
    collections=["elevation"],
    bbox=[-120, 35, -115, 40],
    datetime="2024-01-01/2024-12-31",
    limit=10
)

# Iterate results
for item in search.items():
    print(f"Item: {item.id}")
    print(f"Assets: {list(item.assets.keys())}")

    # Access COG asset
    if "data" in item.assets:
        cog_url = item.assets["data"].href
        print(f"COG URL: {cog_url}")
```

### JavaScript (stac-js)

```javascript
const { STAC } = require('@radiantearth/stac-js');

const stac = new STAC('http://localhost:5000/stac');

// Search items
stac.search({
  collections: ['elevation'],
  bbox: [-120, 35, -115, 40],
  datetime: '2024-01-01/2024-12-31',
  limit: 10
}).then(results => {
  results.features.forEach(item => {
    console.log(`Item: ${item.id}`);
    console.log(`Assets:`, Object.keys(item.assets));
  });
});
```

### QGIS STAC Plugin

1. Install "STAC API Browser" plugin
2. Add Connection: `http://localhost:5000/stac`
3. Browse collections and add items to map

### STAC Browser (Web UI)

Deploy STAC Browser pointing to your catalog:

```bash
docker run -p 8080:8080 \
  -e STAC_CATALOG_URL=http://localhost:5000/stac \
  radiantearth/stac-browser
```

Open browser: `http://localhost:8080`

## Troubleshooting

### Issue: Empty Collections

**Symptoms:** `/stac/collections` returns empty array.

**Solutions:**
1. Verify raster datasets are configured
2. Run STAC backfill command
3. Check STAC synchronization service

```bash
# Backfill STAC catalog
honua stac backfill

# Check status
honua status | grep -i stac
```

### Issue: Items Not Found

**Symptoms:** Collection exists but no items.

**Solutions:**
1. Verify dataset sources are accessible
2. Check spatial extent configuration
3. Validate asset URLs

```bash
# Validate dataset
honua metadata validate --dataset elevation
```

### Issue: Search Returns No Results

**Symptoms:** Search with valid bbox returns empty.

**Solutions:**
1. Check bbox order (west, south, east, north)
2. Verify datetime format
3. Check collection ID spelling
4. Expand bbox to include more area

```bash
# Debug search
curl -v "http://localhost:5000/stac/search?bbox=-180,-90,180,90&limit=1"
```

### Issue: Asset URLs Not Accessible

**Symptoms:** Asset href returns 404.

**Solutions:**
1. Verify storage configuration
2. Check CDN/proxy settings
3. Validate file permissions
4. Ensure CDN is enabled

```json
{
  "honua": {
    "cdn": {
      "enabled": true,
      "baseUrl": "https://cdn.honua.io"
    }
  }
}
```

## Related Documentation

- [OGC API Features](./03-01-ogc-api-features.md) - Feature access
- [Raster Processing](./05-03-raster-processing.md) - COG and Zarr
- [Configuration](./02-01-configuration-reference.md) - STAC setup
- [Export Formats](./03-04-export-formats.md) - Asset formats

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**STAC Version**: 1.0.0
**Conformance**: Core, Collections, Features, Item Search
