# Honua Quick Reference

**Quick answers to common questions and tasks**

This page provides quick copy-paste solutions for common Honua operations. For detailed explanations, see the [Getting Started Guide](getting-started.md).

---

## Quick Links

- [API Endpoints](#api-endpoints)
- [Authentication](#authentication)
- [Common Queries](#common-queries)
- [Export Formats](#export-formats)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## API Endpoints

### Base URLs

```bash
# OGC API Features
http://localhost:8080/ogc

# WFS
http://localhost:8080/wfs

# WMS
http://localhost:8080/wms

# Geoservices REST (Esri)
http://localhost:8080/rest/services

# STAC
http://localhost:8080/stac

# Admin API
http://localhost:8080/admin

# Authentication
http://localhost:8080/api/auth
```

---

## Authentication

### QuickStart Mode (No Auth - Development Only)

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    }
  }
}
```

### Local Mode (Built-in Authentication)

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "bootstrap": {
        "adminUsername": "admin",
        "adminEmail": "admin@example.com",
        "adminPassword": null
      }
    }
  }
}
```

### Login and Get Token

```bash
# Login
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"your-password"}' \
  | jq -r '.token')

# Use token
curl http://localhost:8080/ogc/collections \
  -H "Authorization: Bearer $TOKEN"
```

**See**: [Authentication Guide](authentication.md)

---

## Common Queries

### List All Collections

```bash
# OGC API Features
curl http://localhost:8080/ogc/collections | jq '.collections[] | {id, title}'

# Geoservices REST
curl "http://localhost:8080/rest/services?f=pjson" | jq
```

### Get Features from a Collection

```bash
# Get all features (paginated)
curl "http://localhost:8080/ogc/collections/{collection}/items"

# Limit results
curl "http://localhost:8080/ogc/collections/{collection}/items?limit=10"

# Get specific feature by ID
curl "http://localhost:8080/ogc/collections/{collection}/items/{featureId}"
```

### Spatial Queries

```bash
# Bounding box query
curl "http://localhost:8080/ogc/collections/{collection}/items?bbox=-123,37,-122,38"

# With limit
curl "http://localhost:8080/ogc/collections/{collection}/items?bbox=-123,37,-122,38&limit=100"
```

### Attribute Queries (CQL2)

```bash
# Simple comparison
curl "http://localhost:8080/ogc/collections/{collection}/items?filter=population > 100000"

# String matching
curl "http://localhost:8080/ogc/collections/{collection}/items?filter=name LIKE 'San%'"

# Multiple conditions
curl "http://localhost:8080/ogc/collections/{collection}/items?filter=population > 100000 AND state = 'CA'"

# IN clause
curl "http://localhost:8080/ogc/collections/{collection}/items?filter=state IN ('CA', 'OR', 'WA')"
```

**See**: [Advanced Filtering Guide](../features/ADVANCED_FILTERING_GUIDE.md)

---

## Export Formats

### GeoJSON (Default)

```bash
curl "http://localhost:8080/ogc/collections/{collection}/items" -o data.geojson
# or
curl "http://localhost:8080/ogc/collections/{collection}/items?f=geojson" -o data.geojson
```

### CSV

```bash
curl "http://localhost:8080/ogc/collections/{collection}/items?f=csv" -o data.csv
```

### GeoPackage

```bash
curl "http://localhost:8080/ogc/collections/{collection}/items?f=gpkg" -o data.gpkg
```

### Shapefile (ZIP)

```bash
curl "http://localhost:8080/ogc/collections/{collection}/items?f=shp" -o data.zip
```

### KML/KMZ

```bash
# KML
curl "http://localhost:8080/ogc/collections/{collection}/items?f=kml" -o data.kml

# KMZ (compressed)
curl "http://localhost:8080/ogc/collections/{collection}/items?f=kmz" -o data.kmz
```

### All Export Formats

| Format | Parameter | File Extension |
|--------|-----------|----------------|
| GeoJSON | `f=geojson` | `.geojson` |
| CSV | `f=csv` | `.csv` |
| GeoPackage | `f=gpkg` | `.gpkg` |
| Shapefile | `f=shp` | `.zip` |
| KML | `f=kml` | `.kml` |
| KMZ | `f=kmz` | `.kmz` |
| TopoJSON | `f=topojson` | `.topojson` |
| GML | `f=gml` | `.gml` |
| MVT | `f=mvt` | `.mvt` |

**See**: [Format Matrix](format-matrix.md) for complete format support

---

## Configuration

### Database Connection

```bash
# Environment variable
export ConnectionStrings__DefaultConnection="Host=localhost;Database=honua;Username=postgres;Password=yourpassword"
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=honua;Username=postgres;Password=yourpassword"
  }
}
```

### Metadata Location

```bash
# YAML files (recommended)
export HONUA__METADATA__PROVIDER=yaml
export HONUA__METADATA__PATH=metadata

# JSON files
export HONUA__METADATA__PROVIDER=json
export HONUA__METADATA__PATH=metadata

# Database
export HONUA__METADATA__PROVIDER=postgres
```

### Enable Caching (Redis)

```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "redis": {
        "connectionString": "localhost:6379"
      }
    }
  }
}
```

### Enable Observability

```json
{
  "honua": {
    "observability": {
      "enabled": true,
      "otlp": {
        "endpoint": "http://localhost:4317"
      }
    }
  }
}
```

**See**: [Configuration Reference](configuration.md)

---

## Docker Commands

### Start Honua

```bash
# With docker-compose
docker compose up -d

# With docker run
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;..." \
  ghcr.io/honuaio/honua-server:latest
```

### View Logs

```bash
# All logs
docker logs honua-server

# Follow logs
docker logs -f honua-server

# Last 100 lines
docker logs --tail 100 honua-server
```

### Restart Honua

```bash
docker compose restart
# or
docker restart honua-server
```

### Stop Honua

```bash
docker compose down
# or
docker stop honua-server
```

---

## Metadata Quick Start

### Minimum YAML Metadata

Create `metadata/services/{service}/{collection}.yaml`:

```yaml
id: my-collection
title: My Collection
description: Description of my collection

collections:
  - id: my-collection
    title: My Collection
    table: schema.table_name
    idField: id
    geometryField: geom
    geometryType: Point

layers:
  - id: my-collection-default
    title: My Collection (Default)
    collection: my-collection
    vectorEnabled: true
```

### With Properties

```yaml
collections:
  - id: parks
    title: Parks
    table: public.parks
    idField: id
    geometryField: geom
    geometryType: Point
    properties:
      - name: name
        type: string
        title: Park Name
      - name: area_sqm
        type: number
        title: Area (sq meters)
      - name: established
        type: date
        title: Established Date
```

**See**: [Metadata Authoring Guide](metadata-authoring.md)

---

## Troubleshooting

### Check if Honua is Running

```bash
# Docker
docker ps | grep honua

# Check health endpoint
curl http://localhost:8080/health
```

### View Server Information

```bash
# Landing page
curl http://localhost:8080/

# API conformance
curl http://localhost:8080/ogc/conformance
```

### Test Database Connection

```bash
# From Docker container
docker exec -it honua-server dotnet ef database update

# From host
psql -h localhost -U postgres -d honua
```

### Check Metadata Loading

```bash
# List collections (should show your collections)
curl http://localhost:8080/ogc/collections | jq '.collections[] | {id, title}'

# Check logs for metadata errors
docker logs honua-server | grep -i metadata
```

### Common Errors

**"Collection not found"**
- Check metadata YAML file exists and is valid
- Restart Honua after metadata changes

**"Connection refused"**
- Verify Honua is running: `docker ps`
- Check port mapping: Should see `0.0.0.0:8080->8080/tcp`

**"Database connection error"**
- Test database connection with `psql`
- Verify connection string is correct
- Check database is running

**"Unauthorized"**
- Check authentication mode
- Verify token is valid: `jq -R 'split(".") | .[1] | @base64d | fromjson' <<< "$TOKEN"`

**See**: [Operations Guide](../operations/README.md) for detailed troubleshooting

---

## Performance Tips

### Enable Caching

```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "redis": {
        "connectionString": "localhost:6379"
      }
    }
  }
}
```

### Use Spatial Indexes

```sql
-- Create spatial index on geometry column
CREATE INDEX idx_parks_geom ON public.parks USING GIST (geom);

-- Analyze table
ANALYZE public.parks;
```

### Limit Result Sets

```bash
# Always use limit for large datasets
curl "http://localhost:8080/ogc/collections/parks/items?limit=1000"
```

### Use Bounding Box Queries

```bash
# Query only visible area instead of entire dataset
curl "http://localhost:8080/ogc/collections/parks/items?bbox=-123,37,-122,38&limit=1000"
```

**See**: [PostgreSQL Optimizations](../database/POSTGRESQL_OPTIMIZATIONS.md)

---

## Useful jq Commands

### Extract Collection IDs

```bash
curl http://localhost:8080/ogc/collections | jq -r '.collections[] | .id'
```

### Count Features

```bash
curl "http://localhost:8080/ogc/collections/parks/items" | jq '.features | length'
```

### Extract Specific Properties

```bash
curl "http://localhost:8080/ogc/collections/parks/items" | jq '.features[] | {name: .properties.name, area: .properties.area_sqm}'
```

### Pretty Print

```bash
curl http://localhost:8080/ogc/collections | jq '.'
```

---

## Next Steps

- **Detailed Guide**: [Getting Started](getting-started.md)
- **Authentication**: [Authentication Guide](authentication.md)
- **Configuration**: [Configuration Reference](configuration.md)
- **API Reference**: [API Endpoints](endpoints.md)
- **Deployment**: [Deployment Guide](../deployment/README.md)

---

**Updated**: 2025-11-09
**Related**: [Getting Started](getting-started.md) | [Authentication](authentication.md) | [Configuration](configuration.md)
