# Honua STAC Catalog Guide

**Keywords**: STAC, SpatioTemporal Asset Catalog, raster data, COG, Cloud Optimized GeoTIFF, catalog management, satellite imagery, aerial photography, backfill

**Related Topics**: [Environment Variables](../01-configuration/environment-variables.md), [Performance Tuning](../04-operations/performance-tuning.md), [Kubernetes Deployment](../02-deployment/kubernetes-deployment.md)

---

## Overview

Honua provides comprehensive **STAC (SpatioTemporal Asset Catalog)** support for managing and serving raster datasets, particularly **Cloud-Optimized GeoTIFF (COG)** imagery. This guide covers STAC catalog configuration, data ingestion, API usage, and production deployment patterns.

**STAC Specification**: https://stacspec.org/

---

## Table of Contents

1. [STAC Catalog Configuration](#stac-catalog-configuration)
2. [Catalog Storage Providers](#catalog-storage-providers)
3. [Data Ingestion and Backfill](#data-ingestion-and-backfill)
4. [STAC API Endpoints](#stac-api-endpoints)
5. [Metadata Structure](#metadata-structure)
6. [Production Deployment](#production-deployment)
7. [Performance Optimization](#performance-optimization)

---

## STAC Catalog Configuration

### Environment Variables

```bash
# Enable STAC catalog
HONUA__SERVICES__STAC__ENABLED=true

# Storage provider (sqlite, postgres, sqlserver, mysql)
HONUA__SERVICES__STAC__PROVIDER=sqlite

# Connection string
HONUA__SERVICES__STAC__CONNECTIONSTRING="Data Source=/app/data/stac-catalog.db;Cache=Shared;Pooling=true"

# File path (for SQLite provider)
HONUA__SERVICES__STAC__FILEPATH="/app/data/stac-catalog.db"
```

### appsettings.json Configuration

```json
{
  "honua": {
    "services": {
      "stac": {
        "enabled": true,
        "provider": "sqlite",
        "connectionString": "Data Source=data/stac-catalog.db;Cache=Shared;Pooling=true",
        "filePath": "data/stac-catalog.db"
      }
    }
  }
}
```

**Configuration Options**:

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `enabled` | `true`, `false` | `true` | Enable/disable STAC catalog |
| `provider` | `sqlite`, `postgres`, `sqlserver`, `mysql` | `sqlite` | Storage backend |
| `connectionString` | Connection string | Auto-generated for SQLite | Database connection |
| `filePath` | File path | `data/stac-catalog.db` | SQLite file location |

---

## Catalog Storage Providers

Honua supports multiple STAC catalog storage backends (see `StacCatalogStoreFactory.cs:26-37`):

### SQLite (Default - Development/Small Deployments)

**Best for**: Development, small catalogs (<100K items), single-instance deployments

```bash
# Automatic configuration (default)
HONUA__SERVICES__STAC__ENABLED=true
HONUA__SERVICES__STAC__PROVIDER=sqlite
# FilePath defaults to <workspace>/data/stac-catalog.db

# Custom SQLite path
HONUA__SERVICES__STAC__FILEPATH=/var/lib/honua/stac.db
HONUA__SERVICES__STAC__CONNECTIONSTRING="Data Source=/var/lib/honua/stac.db;Cache=Shared;Pooling=true"
```

**Pros**:
- Zero configuration
- No external database required
- Fast for small datasets
- Easy backup (single file)

**Cons**:
- Single-writer limitation
- Not suitable for high-concurrency
- Performance degrades >100K items

### PostgreSQL (Production - Recommended)

**Best for**: Production deployments, large catalogs, multi-instance scaling

```bash
HONUA__SERVICES__STAC__ENABLED=true
HONUA__SERVICES__STAC__PROVIDER=postgres
HONUA__SERVICES__STAC__CONNECTIONSTRING="Host=postgis;Database=honua_stac;Username=honua_user;Password=***;Pooling=true;MaxPoolSize=50"
```

**Docker Compose Example**:

```yaml
services:
  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: honua_stac
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - stac-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  honua:
    image: honua:latest
    depends_on:
      - postgis
    environment:
      HONUA__SERVICES__STAC__ENABLED: "true"
      HONUA__SERVICES__STAC__PROVIDER: "postgres"
      HONUA__SERVICES__STAC__CONNECTIONSTRING: "Host=postgis;Database=honua_stac;Username=honua_user;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=50"
```

**Pros**:
- Scales to millions of items
- Full-text search support
- Multi-instance concurrency
- Advanced querying (spatial, temporal)

**Cons**:
- Requires external PostgreSQL instance
- More complex deployment

### SQL Server / MySQL

```bash
# SQL Server
HONUA__SERVICES__STAC__PROVIDER=sqlserver
HONUA__SERVICES__STAC__CONNECTIONSTRING="Server=localhost;Database=HonuaStac;User Id=sa;Password=***;TrustServerCertificate=true"

# MySQL
HONUA__SERVICES__STAC__PROVIDER=mysql
HONUA__SERVICES__STAC__CONNECTIONSTRING="Server=localhost;Database=honua_stac;Uid=honua;Pwd=***;Pooling=true;MaxPoolSize=50"
```

---

## Data Ingestion and Backfill

### Using Honua CLI (`stac backfill`)

The `honua stac backfill` command automatically generates STAC collections and items from COG-backed raster datasets defined in your metadata.

**Command Syntax** (from `StacBackfillCommand.cs:180-201`):

```bash
honua stac backfill \
  --workspace /path/to/workspace \
  --metadata /path/to/metadata.json \
  --provider sqlite \
  --connection-string "Data Source=stac.db" \
  --output /path/to/stac-catalog.db
```

**Options**:

| Option | Description | Default |
|--------|-------------|---------|
| `--workspace` | Path to Honua workspace | Current directory |
| `--metadata` | Path to metadata file | `metadata.json` in workspace |
| `--provider` | Storage provider | `sqlite` |
| `--connection-string` | Database connection string | Auto-generated for SQLite |
| `--output` | SQLite file path | `<workspace>/data/stac-catalog.db` |

### Example: Backfill Landsat Imagery

**1. Define Raster Dataset in Metadata**:

```json
{
  "rasterDatasets": [
    {
      "id": "landsat-8-scene-1",
      "title": "Landsat 8 Scene 20250101",
      "type": "COG",
      "source": {
        "url": "s3://landsat-data/LC08_L1TP_044034_20250101_20250102_01_T1.tif",
        "bands": [
          {"name": "B1", "wavelength": 443, "description": "Coastal/Aerosol"},
          {"name": "B2", "wavelength": 482, "description": "Blue"},
          {"name": "B3", "wavelength": 561, "description": "Green"},
          {"name": "B4", "wavelength": 655, "description": "Red"}
        ]
      },
      "extent": {
        "spatial": {
          "bbox": [[-122.5, 37.5, -122.0, 38.0]]
        },
        "temporal": {
          "interval": [["2025-01-01T18:00:00Z", null]]
        }
      }
    }
  ]
}
```

**2. Run Backfill Command**:

```bash
# Using default SQLite
honua stac backfill --workspace /app

# Using PostgreSQL
honua stac backfill \
  --workspace /app \
  --provider postgres \
  --connection-string "Host=postgis;Database=honua_stac;Username=honua;Password=***"
```

**Output**:

```
Found 1 COG raster dataset(s). Rebuilding STAC catalog...
✔ landsat-8-scene-1
Completed STAC backfill for 1 collection(s).
```

### Automated Backfill (Background Service)

Honua includes `StacCatalogSynchronizationHostedService` that automatically synchronizes STAC catalog on metadata changes (for production deployments).

**Enable in appsettings.json**:

```json
{
  "honua": {
    "services": {
      "stac": {
        "enabled": true,
        "provider": "postgres",
        "autoSync": true,  // Enable automatic synchronization
        "syncIntervalMinutes": 60  // Sync every hour
      }
    }
  }
}
```

---

## STAC API Endpoints

Honua implements the **STAC API specification** with the following endpoints:

### Landing Page

```bash
GET /stac

# Response:
{
  "type": "Catalog",
  "id": "honua-stac-catalog",
  "title": "Honua STAC Catalog",
  "description": "SpatioTemporal Asset Catalog for Honua raster datasets",
  "stac_version": "1.0.0",
  "conformsTo": [
    "https://api.stacspec.org/v1.0.0/core",
    "https://api.stacspec.org/v1.0.0/collections",
    "https://api.stacspec.org/v1.0.0/item-search"
  ],
  "links": [
    {"rel": "self", "href": "/stac"},
    {"rel": "data", "href": "/stac/collections"},
    {"rel": "search", "href": "/stac/search", "method": "POST"}
  ]
}
```

### Collections List

```bash
GET /stac/collections

# Response:
{
  "collections": [
    {
      "id": "landsat-8-scene-1",
      "type": "Collection",
      "stac_version": "1.0.0",
      "title": "Landsat 8 Scene 20250101",
      "description": "Landsat 8 OLI/TIRS imagery",
      "license": "proprietary",
      "extent": {
        "spatial": {
          "bbox": [[-122.5, 37.5, -122.0, 38.0]]
        },
        "temporal": {
          "interval": [["2025-01-01T18:00:00Z", null]]
        }
      },
      "links": [
        {"rel": "self", "href": "/stac/collections/landsat-8-scene-1"},
        {"rel": "items", "href": "/stac/collections/landsat-8-scene-1/items"}
      ]
    }
  ],
  "links": [
    {"rel": "self", "href": "/stac/collections"}
  ]
}
```

### Collection Details

```bash
GET /stac/collections/{collection_id}

# Example:
GET /stac/collections/landsat-8-scene-1
```

### Items in Collection

```bash
GET /stac/collections/{collection_id}/items

# Example:
GET /stac/collections/landsat-8-scene-1/items

# With pagination:
GET /stac/collections/landsat-8-scene-1/items?limit=100
```

### Item Details

```bash
GET /stac/collections/{collection_id}/items/{item_id}

# Example:
GET /stac/collections/landsat-8-scene-1/items/LC08_L1TP_044034_20250101
```

### STAC Search (POST)

```bash
POST /stac/search
Content-Type: application/json

{
  "bbox": [-122.5, 37.5, -122.0, 38.0],
  "datetime": "2025-01-01T00:00:00Z/2025-12-31T23:59:59Z",
  "collections": ["landsat-8-scene-1"],
  "limit": 100
}
```

**Search Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `bbox` | array | Bounding box: [west, south, east, north] |
| `datetime` | string | ISO 8601 datetime or interval |
| `collections` | array | Collection IDs to search |
| `ids` | array | Specific item IDs |
| `limit` | integer | Max results (default: 100, max: 1000) |
| `query` | object | Property filters |

---

## Metadata Structure

### STAC Collection

```json
{
  "type": "Collection",
  "stac_version": "1.0.0",
  "id": "landsat-8",
  "title": "Landsat 8 Imagery",
  "description": "Landsat 8 OLI/TIRS satellite imagery",
  "keywords": ["landsat", "satellite", "multispectral"],
  "license": "PDDL-1.0",
  "providers": [
    {
      "name": "USGS",
      "roles": ["producer", "licensor"],
      "url": "https://landsat.usgs.gov/"
    }
  ],
  "extent": {
    "spatial": {
      "bbox": [[-180, -90, 180, 90]]
    },
    "temporal": {
      "interval": [["2013-04-01T00:00:00Z", null]]
    }
  },
  "summaries": {
    "gsd": [30],
    "platform": ["landsat-8"],
    "instruments": ["oli", "tirs"]
  },
  "links": [
    {"rel": "self", "href": "/stac/collections/landsat-8"},
    {"rel": "items", "href": "/stac/collections/landsat-8/items"},
    {"rel": "license", "href": "https://opendatacommons.org/licenses/pddl/1.0/"}
  ]
}
```

### STAC Item

```json
{
  "type": "Feature",
  "stac_version": "1.0.0",
  "id": "LC08_L1TP_044034_20250101",
  "collection": "landsat-8",
  "geometry": {
    "type": "Polygon",
    "coordinates": [
      [
        [-122.5, 37.5],
        [-122.0, 37.5],
        [-122.0, 38.0],
        [-122.5, 38.0],
        [-122.5, 37.5]
      ]
    ]
  },
  "bbox": [-122.5, 37.5, -122.0, 38.0],
  "properties": {
    "datetime": "2025-01-01T18:00:00Z",
    "created": "2025-01-02T10:00:00Z",
    "updated": "2025-01-02T10:00:00Z",
    "platform": "landsat-8",
    "instruments": ["oli", "tirs"],
    "gsd": 30,
    "eo:cloud_cover": 5.2
  },
  "assets": {
    "visual": {
      "href": "s3://landsat-data/LC08_L1TP_044034_20250101_20250102_01_T1.tif",
      "type": "image/tiff; application=geotiff; profile=cloud-optimized",
      "roles": ["visual"],
      "title": "True Color Composite"
    },
    "B1": {
      "href": "s3://landsat-data/LC08_L1TP_044034_20250101_B1.tif",
      "type": "image/tiff; application=geotiff; profile=cloud-optimized",
      "eo:bands": [{"name": "B1", "common_name": "coastal", "center_wavelength": 0.44}],
      "roles": ["data"]
    }
  },
  "links": [
    {"rel": "self", "href": "/stac/collections/landsat-8/items/LC08_L1TP_044034_20250101"},
    {"rel": "collection", "href": "/stac/collections/landsat-8"}
  ]
}
```

---

## Production Deployment

### Docker Deployment with PostgreSQL

```yaml
version: '3.8'

services:
  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: honua_stac
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - stac-postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua_user -d honua_stac"]
      interval: 10s
      timeout: 5s
      retries: 5

  honua:
    image: honua:latest
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      # STAC Configuration
      HONUA__SERVICES__STAC__ENABLED: "true"
      HONUA__SERVICES__STAC__PROVIDER: "postgres"
      HONUA__SERVICES__STAC__CONNECTIONSTRING: "Host=postgis;Database=honua_stac;Username=honua_user;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=50"

      # Metadata
      HONUA__METADATA__PROVIDER: "json"
      HONUA__METADATA__PATH: "/app/config/metadata.json"
    volumes:
      - ./metadata.json:/app/config/metadata.json:ro
      - ./raster-data:/app/raster-data:ro
    ports:
      - "5000:5000"

volumes:
  stac-postgres-data:
```

**Initialize Catalog**:

```bash
# Start services
docker-compose up -d

# Backfill STAC catalog
docker-compose exec honua honua stac backfill \
  --workspace /app \
  --provider postgres \
  --connection-string "Host=postgis;Database=honua_stac;Username=honua_user;Password=${DB_PASSWORD}"
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgis-stac
spec:
  serviceName: postgis-stac
  replicas: 1
  selector:
    matchLabels:
      app: postgis-stac
  template:
    metadata:
      labels:
        app: postgis-stac
    spec:
      containers:
      - name: postgis
        image: postgis/postgis:16-3.4
        env:
        - name: POSTGRES_DB
          value: honua_stac
        - name: POSTGRES_USER
          valueFrom:
            secretKeyRef:
              name: stac-db-secret
              key: username
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: stac-db-secret
              key: password
        volumeMounts:
        - name: stac-data
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: stac-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 100Gi

---
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-stac-config
data:
  HONUA__SERVICES__STAC__ENABLED: "true"
  HONUA__SERVICES__STAC__PROVIDER: "postgres"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua
        image: honua:latest
        envFrom:
        - configMapRef:
            name: honua-stac-config
        env:
        - name: HONUA__SERVICES__STAC__CONNECTIONSTRING
          value: "Host=postgis-stac;Database=honua_stac;Username=$(DB_USER);Password=$(DB_PASSWORD);Pooling=true;MaxPoolSize=50"
        - name: DB_USER
          valueFrom:
            secretKeyRef:
              name: stac-db-secret
              key: username
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: stac-db-secret
              key: password
```

---

## Performance Optimization

### Database Indexing

For PostgreSQL STAC catalog, create indexes for fast spatial and temporal queries:

```sql
-- Spatial index on item geometries
CREATE INDEX idx_stac_items_geom ON stac_items USING GIST(geometry);

-- Temporal index
CREATE INDEX idx_stac_items_datetime ON stac_items (datetime);

-- Collection index
CREATE INDEX idx_stac_items_collection ON stac_items (collection_id);

-- Full-text search index
CREATE INDEX idx_stac_items_fulltext ON stac_items USING GIN(to_tsvector('english', title || ' ' || description));

-- Analyze for query planning
ANALYZE stac_items;
ANALYZE stac_collections;
```

### Caching STAC Responses

```json
{
  "honua": {
    "caching": {
      "http": {
        "enabled": true,
        "durationSeconds": 3600,
        "paths": ["/stac/*"]
      }
    }
  }
}
```

### CDN for STAC Assets

Use CDN to serve COG assets from cloud storage:

```json
{
  "assets": {
    "visual": {
      "href": "https://cdn.example.com/landsat/LC08_L1TP_044034_20250101.tif",
      "type": "image/tiff; application=geotiff; profile=cloud-optimized"
    }
  }
}
```

---

**Last Updated**: 2025-10-04
**Honua Version**: 1.0+
**STAC Specification**: 1.0.0
**Related Documentation**: [Environment Variables](../01-configuration/environment-variables.md), [Performance Tuning](../04-operations/performance-tuning.md)
