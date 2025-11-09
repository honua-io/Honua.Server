# Getting Started with Honua

**Audience**: New users deploying or integrating Honua for the first time
**Time to Complete**: 15-30 minutes
**Prerequisites**: Basic understanding of geospatial concepts, Docker or .NET experience

This guide walks you through setting up Honua, understanding core concepts, and making your first API requests. For a quicker start, see the [5-Minute Quick Start](../quickstart/README.md).

---

## Overview

### What is Honua?

Honua (Hawaiian for "Earth") is a cloud-native geospatial server that provides:
- **Multiple API Standards**: OGC API Features, WFS, WMS, STAC, Geoservices REST (Esri-compatible)
- **Multiple Databases**: PostgreSQL, MySQL, MongoDB, Snowflake, BigQuery, and more
- **Rich Export Formats**: GeoJSON, Shapefile, GeoPackage, KML, CSV, and 12+ formats
- **Cloud-Native**: Docker, Kubernetes, and serverless deployment options

### Core Concepts

**Collections**: A collection is a dataset (e.g., "roads", "parcels", "sensors"). Collections are served through multiple API standards.

**Services**: A logical grouping of collections. One database can contain multiple services.

**Layers**: How collections are exposed through APIs. A single collection can have multiple layers (vector, raster, styled).

**Authentication**: Honua supports three modes: QuickStart (no auth), Local (built-in), and OIDC (enterprise SSO).

---

## Installation Options

### Option 1: Docker (Recommended for Testing)

**Best for**: Quick evaluation, development, testing

```bash
# Clone the repository
git clone https://github.com/honua-io/Honua.Server.git
cd Honua.Server/docker

# Start Honua + PostgreSQL
docker compose up -d
```

**What this gives you**:
- Honua Server on `http://localhost:8080`
- PostgreSQL 16 with PostGIS on port `5432`
- QuickStart authentication mode (no login required)
- Sample data pre-loaded (optional)

**Verify it's running**:
```bash
curl http://localhost:8080/ogc
```

**See**: [Docker Deployment Guide](../deployment/README.md#docker-recommended-for-development) for production Docker setup

---

### Option 2: Pre-Built Container Image

**Best for**: Production deployment, cloud platforms

```bash
# Pull and run the latest image
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;Username=postgres;Password=yourpassword" \
  ghcr.io/honuaio/honua-server:latest
```

**Image variants**:
- `ghcr.io/honuaio/honua-server:latest` - Full image with raster support (~150MB)
- `ghcr.io/honuaio/honua-server:lite` - Vector-only, faster cold starts (~60MB)

**See**: [Production Deployment Guide](../deployment/README.md) for production configuration

---

### Option 3: From Source

**Best for**: Development, customization, contributing

**Prerequisites**:
- .NET 9.0 SDK
- PostgreSQL 16+ with PostGIS (or another supported database)

```bash
# Clone and build
git clone https://github.com/honua-io/Honua.Server.git
cd Honua.Server
dotnet restore
dotnet build

# Run
cd src/Honua.Server.Host
dotnet run
```

**See**: [Development Guide](../development/README.md) for development setup

---

## Configuration Basics

Honua is configured through `appsettings.json` or environment variables.

### Minimum Configuration

Create or edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=honua;Username=postgres;Password=yourpassword"
  },
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    },
    "metadata": {
      "provider": "yaml",
      "path": "metadata"
    }
  }
}
```

### Environment Variables

All settings can be overridden with environment variables:

```bash
# Database connection
export ConnectionStrings__DefaultConnection="Host=localhost;Database=honua;Username=postgres;Password=yourpassword"

# Authentication mode
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false

# Metadata location
export HONUA__METADATA__PROVIDER=yaml
export HONUA__METADATA__PATH=metadata
```

**See**: [Configuration Reference](configuration.md) for all options

---

## Your First Service and Layer

### Step 1: Prepare Your Data

Honua works with existing databases. For this guide, we'll use PostgreSQL with a simple table:

```sql
-- Create a sample table
CREATE TABLE public.parks (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    area_sqm NUMERIC,
    geom GEOMETRY(Point, 4326)
);

-- Insert sample data
INSERT INTO public.parks (name, area_sqm, geom)
VALUES
    ('Central Park', 341000000, ST_SetSRID(ST_MakePoint(-73.9654, 40.7829), 4326)),
    ('Golden Gate Park', 412000000, ST_SetSRID(ST_MakePoint(-122.4862, 37.7694), 4326));
```

### Step 2: Create Metadata

Create `metadata/services/public/parks.yaml`:

```yaml
id: parks
title: City Parks
description: Public parks dataset
keywords:
  - parks
  - recreation

# Define collections from database tables
collections:
  - id: parks
    title: Parks
    description: City parks with locations
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

# Define layers (how collections are exposed)
layers:
  - id: parks-default
    title: Parks (Default Style)
    collection: parks
    vectorEnabled: true
    rasterEnabled: false
```

### Step 3: Restart Honua

```bash
# Docker
docker compose restart

# From source
# Ctrl+C and dotnet run again
```

### Step 4: Test Your Layer

```bash
# List collections
curl http://localhost:8080/ogc/collections | jq '.collections[] | {id, title}'

# Get parks features
curl http://localhost:8080/ogc/collections/parks/items | jq

# Export as CSV
curl "http://localhost:8080/ogc/collections/parks/items?f=csv" -o parks.csv
```

**See**: [Metadata Authoring Guide](metadata-authoring.md) for complete metadata reference

---

## Understanding Honua's APIs

Honua exposes your data through multiple API standards. All APIs work with the same underlying collections.

### OGC API Features (Modern, RESTful)

**When to use**: Modern applications, JavaScript clients, REST-based integrations

```bash
# Landing page
curl http://localhost:8080/ogc

# List collections
curl http://localhost:8080/ogc/collections

# Get features
curl "http://localhost:8080/ogc/collections/parks/items?limit=10"

# Spatial query
curl "http://localhost:8080/ogc/collections/parks/items?bbox=-123,37,-122,38"

# Export formats
curl "http://localhost:8080/ogc/collections/parks/items?f=geojson"  # GeoJSON
curl "http://localhost:8080/ogc/collections/parks/items?f=csv"      # CSV
curl "http://localhost:8080/ogc/collections/parks/items?f=gpkg"     # GeoPackage
```

---

### WFS (OGC Web Feature Service)

**When to use**: Legacy GIS clients (QGIS, ArcGIS), established workflows

```bash
# Capabilities
curl "http://localhost:8080/wfs?service=WFS&request=GetCapabilities"

# Get features
curl "http://localhost:8080/wfs?service=WFS&request=GetFeature&typeNames=parks:parks&outputFormat=application/geo+json"

# Describe feature type
curl "http://localhost:8080/wfs?service=WFS&request=DescribeFeatureType&typeNames=parks:parks"
```

---

### Geoservices REST (Esri-Compatible)

**When to use**: Esri ArcGIS clients, ArcGIS Online, Esri JavaScript API

```bash
# Service directory
curl "http://localhost:8080/rest/services?f=pjson"

# Layer metadata
curl "http://localhost:8080/rest/services/public/parks/FeatureServer/0?f=json"

# Query features
curl "http://localhost:8080/rest/services/public/parks/FeatureServer/0/query?where=1=1&outFields=*&f=geojson"
```

---

### STAC (SpatioTemporal Asset Catalog)

**When to use**: Raster/imagery data, time-series data, satellite imagery

```bash
# STAC catalog root
curl http://localhost:8080/stac

# Collections
curl http://localhost:8080/stac/collections

# Search items
curl "http://localhost:8080/stac/search?collections=parks&limit=10"
```

**See**: [API Endpoints Reference](endpoints.md) for complete API documentation

---

## Authentication Setup

By default, Honua runs in QuickStart mode with no authentication. This is **only for testing**.

### Switching to Local Authentication

**Step 1: Update Configuration**

Edit `appsettings.json`:

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetimeMinutes": 480,
        "storePath": "data/users"
      },
      "bootstrap": {
        "adminUsername": "admin",
        "adminEmail": "admin@example.com",
        "adminPassword": null  // Prompts on first run
      }
    }
  }
}
```

**Step 2: Restart and Create Admin User**

```bash
docker compose restart
# Follow prompts to create admin password
```

**Step 3: Login and Get Token**

```bash
# Login
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"your-password"}' \
  | jq -r '.token' > token.txt

# Use token in requests
curl http://localhost:8080/ogc/collections \
  -H "Authorization: Bearer $(cat token.txt)"
```

**See**: [Authentication Guide](authentication.md) for OIDC and enterprise SSO

---

## Common Tasks

### Task: Add a New Collection

1. Ensure table exists in database
2. Create or update YAML metadata file in `metadata/services/{service}/`
3. Restart Honua
4. Verify: `curl http://localhost:8080/ogc/collections`

### Task: Change Export Formats

Use the `?f=` parameter:

```bash
curl "http://localhost:8080/ogc/collections/parks/items?f=geojson"  # GeoJSON
curl "http://localhost:8080/ogc/collections/parks/items?f=csv"      # CSV
curl "http://localhost:8080/ogc/collections/parks/items?f=gpkg"     # GeoPackage
curl "http://localhost:8080/ogc/collections/parks/items?f=shp"      # Shapefile (ZIP)
curl "http://localhost:8080/ogc/collections/parks/items?f=kml"      # KML
```

**See**: [Format Matrix](format-matrix.md) for all supported formats

### Task: Filter Data

**By bounding box**:
```bash
curl "http://localhost:8080/ogc/collections/parks/items?bbox=-123,37,-122,38"
```

**By attribute** (CQL2):
```bash
curl "http://localhost:8080/ogc/collections/parks/items?filter=area_sqm > 100000"
```

**See**: [Advanced Filtering Guide](../features/ADVANCED_FILTERING_GUIDE.md)

### Task: Enable Caching

Edit `appsettings.json`:

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

**See**: [Configuration Reference](configuration.md#caching)

---

## Troubleshooting

### Problem: "Connection refused" when accessing APIs

**Check**:
```bash
# Is Honua running?
docker ps  # Should show honua container

# Check logs
docker logs honua-server
```

**Solution**: Ensure Honua is running and accessible on the expected port

---

### Problem: "Collection not found"

**Check**:
```bash
# List available collections
curl http://localhost:8080/ogc/collections | jq '.collections[] | .id'
```

**Solution**:
- Verify metadata YAML files exist in `metadata/services/`
- Check YAML syntax is valid
- Restart Honua after metadata changes

---

### Problem: Database connection errors

**Check**:
```bash
# Test database connection
psql -h localhost -U postgres -d honua
```

**Solution**:
- Verify `ConnectionStrings__DefaultConnection` is correct
- Ensure database is running
- Check firewall/network access

**See**: [Operations Guide](../operations/README.md) for more troubleshooting

---

## Next Steps

### For Users
- **[Authentication Setup](authentication.md)** - Configure production authentication
- **[Configuration Reference](configuration.md)** - All configuration options
- **[API Endpoints](endpoints.md)** - Complete API reference
- **[Data Ingestion](data-ingestion.md)** - Loading data into Honua

### For Administrators
- **[Deployment Guide](../deployment/README.md)** - Production deployment
- **[Operations Guide](../operations/README.md)** - Day-to-day operations
- **[Monitoring & Observability](../observability/)** - Metrics and logging

### For Developers
- **[MapSDK](../mapsdk/README.md)** - Build mapping applications with Blazor
- **[Development Guide](../development/README.md)** - Contributing to Honua

---

## Getting Help

- **Documentation Issues**: [GitHub Issues with `documentation` label](https://github.com/honua-io/Honua.Server/issues?q=label%3Adocumentation)
- **Questions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **Support**: See [Support Documentation](support/README.md)

---

**Next**: [Authentication Setup](authentication.md) or [Configuration Reference](configuration.md)
