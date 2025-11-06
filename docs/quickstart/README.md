# Honua Quickstart Guide

Get Honua up and running in 5 minutes with Docker, then learn how to create services, layers, and make your first API requests.

## Prerequisites

- **Docker Desktop** (or Docker Engine with Compose v2)
- **.NET 9.0 SDK** (optional, for development without Docker)
- **Git** (to clone the repository)

## 5-Minute Docker Quickstart

### 1. Clone and Setup

```bash
# Clone the repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO/docker

# Start Honua with PostgreSQL/PostGIS
docker compose up --build
```

This starts:
- **Honua Server** on `http://localhost:8080`
- **PostgreSQL 16** with PostGIS on port `5432`

The server runs in **QuickStart authentication mode** (no login required) - perfect for testing!

### 2. Verify Installation

```bash
# Get the landing page
curl http://localhost:8080/

# List collections
curl http://localhost:8080/ogc/collections

# View Swagger API docs
open http://localhost:8080/swagger
```

## First API Requests

### OGC API Features

```bash
# List collections
curl http://localhost:8080/ogc/collections | jq

# Query features
curl "http://localhost:8080/ogc/collections/{collection}/items" | jq

# Spatial query with bounding box
curl "http://localhost:8080/ogc/collections/{collection}/items?bbox=-120,37,-119,38" | jq

# Filter by attribute
curl "http://localhost:8080/ogc/collections/{collection}/items?filter=property<value" | jq
```

### Export Formats

```bash
# GeoJSON (default)
curl "http://localhost:8080/ogc/collections/{collection}/items" -o data.geojson

# CSV
curl "http://localhost:8080/ogc/collections/{collection}/items?f=csv" -o data.csv

# KML
curl "http://localhost:8080/ogc/collections/{collection}/items?f=kml" -o data.kml

# Shapefile
curl "http://localhost:8080/ogc/collections/{collection}/items?f=shp" -o data.zip
```

### WFS 2.0

```bash
curl "http://localhost:8080/wfs?service=WFS&version=2.0.0&request=GetCapabilities"
curl "http://localhost:8080/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames={collection}"
```

### WMS 1.3

```bash
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetCapabilities"
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetMap&layers={layer}&bbox=-120,36,-109,46&width=800&height=600&crs=EPSG:4326&format=image/png" -o map.png
```

### Geoservices REST a.k.a. Esri REST API

```bash
curl http://localhost:8080/rest/services | jq
curl "http://localhost:8080/rest/services/{service}/FeatureServer?f=json" | jq
curl "http://localhost:8080/rest/services/{service}/FeatureServer/0/query?where=1=1&outFields=*&f=json" | jq
```

### STAC 1.0

```bash
curl http://localhost:8080/stac | jq
curl http://localhost:8080/stac/collections | jq
curl "http://localhost:8080/stac/search?collections={collection}&limit=10" | jq
```

## Monitoring and Observability

Add Prometheus + Grafana monitoring:

```bash
docker compose -f docker-compose.yml -f docker-compose.prometheus.yml up --build
```

Access:
- **Prometheus**: `http://localhost:9090`
- **Grafana**: `http://localhost:3000` (admin/admin)
- **Metrics Endpoint**: `http://localhost:8080/metrics`

## Next Steps

- [Configuration Reference](../configuration/) - Complete configuration options
- [API Documentation](../api/) - All available endpoints
- [Deployment Guide](../deployment/) - Production deployment
- [Observability](../observability/) - Monitoring and tracing

## Quick Reference

| API | Endpoint | Description |
|-----|----------|-------------|
| OGC API Features | `/ogc/collections` | Feature collections and items |
| WFS 2.0 | `/wfs` | Web Feature Service |
| WMS 1.3 | `/wms` | Web Map Service |
| STAC 1.0 | `/stac` | SpatioTemporal Asset Catalog |
| Geoservices REST a.k.a. Esri REST | `/rest/services` | ArcGIS-compatible API |
| OData v4 | `/odata` | Query with OData |
| Swagger | `/swagger` | Interactive API docs |
| Metrics | `/metrics` | Prometheus metrics |

---

**Honua** - _Hawaiian for "Earth"_
