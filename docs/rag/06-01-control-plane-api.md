---
tags: [admin, control-plane, api, management, runtime-config, observability]
category: api-reference
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# Honua Control Plane API Complete Reference

The Control Plane API provides administrative endpoints for managing Honua Server at runtime. All endpoints require authentication and appropriate authorization.

## Table of Contents
- [Overview](#overview)
- [Authentication](#authentication)
- [Configuration Management](#configuration-management)
- [Observability](#observability)
- [Data Ingestion](#data-ingestion)
- [Metadata Management](#metadata-management)
- [Migration](#migration)
- [Raster Tile Cache](#raster-tile-cache)
- [Error Responses](#error-responses)
- [Related Documentation](#related-documentation)

## Overview

### Base Path
```
/admin/*
```

### Endpoint Categories

| Category | Base Path | Purpose | Required Role |
|----------|-----------|---------|---------------|
| Configuration | `/admin/config` | Runtime protocol configuration | Administrator |
| Logging | `/admin/logging` | Runtime log level management | Administrator |
| Tracing | `/admin/observability/tracing` | Runtime tracing configuration | Administrator |
| Data Ingestion | `/admin/ingestion` | Upload and ingest datasets | DataPublisher |
| Metadata | `/admin/metadata` | Metadata management | Administrator |
| Migration | `/admin/migrations` | Esri service migrations | DataPublisher |
| Raster Cache | `/admin/raster-cache` | Tile cache management | Administrator |
| Cache Statistics | `/admin/raster-cache/statistics` | Cache metrics | Viewer |
| Cache Quota | `/admin/raster-cache/quota` | Quota management | Administrator |

### Authorization Roles

- **Administrator** - Full control over all admin endpoints
- **DataPublisher** - Can ingest data and run migrations
- **Viewer** - Read-only access to statistics

**QuickStart Mode**: Most endpoints allow anonymous access when `authentication.mode=QuickStart` (development only).

## Configuration Management

### Get Configuration Status

Get overall runtime configuration status for all services and protocols.

```bash
GET /admin/config/status
```

**Response:**
```json
{
  "global": {
    "wfs": true,
    "wms": true,
    "wmts": true,
    "csw": false,
    "wcs": false,
    "stac": true,
    "geometry": true,
    "rasterTiles": true,
    "note": "Global settings are configured in appsettings.json and are read-only at runtime."
  },
  "services": [
    {
      "serviceId": "my-service",
      "apis": {
        "collections": {
          "serviceLevel": true,
          "globalLevel": true,
          "effective": true
        },
        "wfs": {
          "serviceLevel": true,
          "globalLevel": true,
          "effective": true
        },
        "wms": {
          "serviceLevel": false,
          "globalLevel": true,
          "effective": false
        }
      }
    }
  ]
}
```

### Get Global Service Configuration

```bash
GET /admin/config/services
```

**Response:**
```json
{
  "wfs": { "enabled": true },
  "wms": { "enabled": true },
  "wmts": { "enabled": true },
  "csw": { "enabled": false },
  "wcs": { "enabled": false },
  "stac": { "enabled": true },
  "geometry": { "enabled": true },
  "rasterTiles": { "enabled": true },
  "note": "These global settings can be toggled at runtime. When disabled, the protocol is disabled for ALL services regardless of service-level settings."
}
```

### Toggle Global Protocol

Enable or disable a protocol globally (master kill switch).

```bash
PATCH /admin/config/services/{protocol}
Content-Type: application/json

{
  "enabled": false
}
```

**Parameters:**
- `{protocol}`: `wfs`, `wms`, `wmts`, `csw`, `wcs`, `stac`, `geometry`, `rasterTiles`

**Request Body:**
```json
{
  "enabled": true
}
```

**Response:**
```json
{
  "status": "updated",
  "protocol": "wfs",
  "enabled": false,
  "message": "WFS globally disabled.",
  "affectedServices": ["service-1", "service-2"],
  "note": "ALL services are now blocked from serving this protocol, regardless of service-level settings."
}
```

**Restrictions:**
- ❌ Not available in QuickStart mode

### Get Service-Level Configuration

```bash
GET /admin/config/services/{serviceId}
```

**Response:**
```json
{
  "serviceId": "my-service",
  "apis": {
    "collections": {
      "enabled": true,
      "effective": true,
      "note": "Collections (OGC API Features) don't have a global toggle"
    },
    "wfs": {
      "enabled": true,
      "globalEnabled": true,
      "effective": true
    },
    "wms": {
      "enabled": false,
      "globalEnabled": true,
      "effective": false
    }
  }
}
```

### Toggle Service-Level Protocol

Enable or disable a protocol for a specific service.

```bash
PATCH /admin/config/services/{serviceId}/{protocol}
Content-Type: application/json

{
  "enabled": true
}
```

**Parameters:**
- `{serviceId}`: Service identifier from metadata.json
- `{protocol}`: `collections`, `wfs`, `wms`, `wmts`, `csw`, `wcs`

**Response:**
```json
{
  "status": "updated",
  "serviceId": "my-service",
  "protocol": "wfs",
  "enabled": true,
  "message": "WFS enabled for service 'my-service'.",
  "note": "This change is in-memory only. To persist, update metadata.json and reload or restart."
}
```

**Restrictions:**
- ❌ Not available in QuickStart mode
- ❌ Cannot enable if globally disabled

## Observability

### Logging Configuration

#### Get Available Log Levels

```bash
GET /admin/logging/levels
```

**Response:**
```json
{
  "levels": [
    { "value": 0, "name": "Trace", "description": "Most verbose - includes sensitive data" },
    { "value": 1, "name": "Debug", "description": "Debugging diagnostics" },
    { "value": 2, "name": "Information", "description": "General flow of application" },
    { "value": 3, "name": "Warning", "description": "Abnormal or unexpected events" },
    { "value": 4, "name": "Error", "description": "Errors and exceptions" },
    { "value": 5, "name": "Critical", "description": "Critical failures" },
    { "value": 6, "name": "None", "description": "Disable logging" }
  ],
  "note": "Set logging levels at runtime using PATCH /admin/logging/categories/{category}"
}
```

#### Get Current Log Configuration

```bash
GET /admin/logging/categories
```

**Response:**
```json
{
  "current": {
    "Default": { "level": "Information", "value": 2 },
    "Honua.Server.Core.Data": { "level": "Debug", "value": 1 }
  },
  "recommended": {
    "Default": { "description": "Default log level for all categories", "recommended": "Information" },
    "Microsoft.AspNetCore": { "description": "ASP.NET Core framework logs", "recommended": "Warning" },
    "Honua.Server.Core": { "description": "Core business logic", "recommended": "Debug" },
    "Honua.Server.Core.Data": { "description": "Database operations", "recommended": "Debug" },
    "Honua.Server.Core.Raster": { "description": "Raster tile operations", "recommended": "Information" }
  },
  "note": "Use PATCH /admin/logging/categories/{category} to set log levels. Use DELETE to remove overrides."
}
```

#### Set Log Level

```bash
PATCH /admin/logging/categories/{category}
Content-Type: application/json

{
  "level": "Debug"
}
```

**Example:**
```bash
curl -X PATCH http://localhost:8080/admin/logging/categories/Honua.Server.Core.Data \
  -H "Content-Type: application/json" \
  -d '{"level":"Trace"}'
```

**Response:**
```json
{
  "status": "updated",
  "category": "Honua.Server.Core.Data",
  "level": "Trace",
  "levelValue": 0,
  "message": "Log level for 'Honua.Server.Core.Data' set to Trace",
  "note": "This change is in-memory only and applies immediately. To persist, update appsettings.json Logging:LogLevel section.",
  "effective": {
    "trace": true,
    "debug": true,
    "information": true,
    "warning": true,
    "error": true,
    "critical": true
  }
}
```

**Effect:**
- ✅ Takes effect immediately
- ❌ In-memory only (lost on restart)

#### Remove Log Level Override

```bash
DELETE /admin/logging/categories/{category}
```

**Response:**
```json
{
  "status": "removed",
  "category": "Honua.Server.Core.Data",
  "message": "Runtime override removed. Category will now use default log level from appsettings.json."
}
```

### Tracing Configuration

#### Get Current Tracing Configuration

```bash
GET /admin/observability/tracing
```

**Response:**
```json
{
  "exporter": "otlp",
  "otlpEndpoint": "http://jaeger:4317",
  "samplingRatio": 0.1,
  "note": "Exporter changes require application restart. Sampling changes apply immediately."
}
```

#### Get Available Activity Sources

```bash
GET /admin/observability/tracing/activity-sources
```

**Response:**
```json
{
  "activitySources": [
    {
      "name": "Honua.Server.Ogc.Protocols",
      "description": "OGC protocol handlers (WFS, WMS, WMTS, WCS, CSW)"
    },
    {
      "name": "Honua.Server.OData",
      "description": "OData query processing"
    },
    {
      "name": "Honua.Server.Database",
      "description": "Database queries and transactions"
    },
    {
      "name": "Honua.Server.RasterTiles",
      "description": "Raster tile rendering and caching"
    }
  ]
}
```

#### Update Tracing Exporter

```bash
PATCH /admin/observability/tracing/exporter
Content-Type: application/json

{
  "exporter": "otlp"
}
```

**Valid exporters:** `none`, `console`, `otlp`

**Response:**
```json
{
  "status": "updated",
  "exporter": "otlp",
  "warning": "⚠️ Exporter changes require application restart to take effect."
}
```

**Effect:**
- ❌ Requires restart to take effect

#### Update OTLP Endpoint

```bash
PATCH /admin/observability/tracing/endpoint
Content-Type: application/json

{
  "endpoint": "http://tempo:4317"
}
```

**Response:**
```json
{
  "status": "updated",
  "endpoint": "http://tempo:4317",
  "warning": "⚠️ Endpoint changes require application restart to take effect."
}
```

#### Update Sampling Ratio

```bash
PATCH /admin/observability/tracing/sampling
Content-Type: application/json

{
  "ratio": 0.5
}
```

**Valid values:** 0.0 to 1.0 (0% to 100%)

**Response:**
```json
{
  "status": "updated",
  "samplingRatio": 0.5,
  "message": "✅ Sampling ratio updated to 50% and is now in effect."
}
```

**Effect:**
- ✅ Takes effect immediately
- ❌ In-memory only (lost on restart)

#### Create Test Trace

Create a test activity to verify tracing configuration.

```bash
POST /admin/observability/tracing/test
Content-Type: application/json

{
  "activityName": "TestActivity",
  "duration": 1000
}
```

**Response:**
```json
{
  "status": "created",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7",
  "duration": 1024,
  "exporter": "otlp",
  "endpoint": "http://jaeger:4317",
  "note": "Check your tracing backend (Jaeger, Tempo, etc.) for this trace ID."
}
```

#### Get Platform-Specific Guidance

```bash
GET /admin/observability/tracing/platforms
```

**Response:**
```json
{
  "platforms": {
    "jaeger": {
      "name": "Jaeger",
      "exporter": "otlp",
      "defaultEndpoint": "http://jaeger:4317",
      "setup": "docker run -d --name jaeger -p 4317:4317 -p 16686:16686 jaegertracing/all-in-one:latest",
      "ui": "http://localhost:16686"
    },
    "tempo": {
      "name": "Grafana Tempo",
      "exporter": "otlp",
      "defaultEndpoint": "http://tempo:4317"
    },
    "azure": {
      "name": "Azure Application Insights",
      "exporter": "otlp",
      "defaultEndpoint": "https://dc.services.visualstudio.com/v2/track"
    }
  }
}
```

## Data Ingestion

### Create Ingestion Job

Upload and ingest a geospatial dataset into a layer.

```bash
POST /admin/ingestion/jobs
Content-Type: multipart/form-data

FormData:
  serviceId: my-service
  layerId: my-layer
  overwrite: false
  file: @dataset.gpkg
```

**Example (curl):**
```bash
curl -X POST http://localhost:8080/admin/ingestion/jobs \
  -F "serviceId=my-service" \
  -F "layerId=cities" \
  -F "overwrite=false" \
  -F "file=@cities.gpkg"
```

**Supported Formats:**
- `.shp` - ESRI Shapefile (with .shx, .dbf, .prj)
- `.geojson`, `.json` - GeoJSON
- `.gpkg` - GeoPackage
- `.zip` - Zipped shapefile
- `.kml` - Keyhole Markup Language
- `.gml` - Geography Markup Language
- `.csv` - CSV with lat/lon columns

**File Size Limits:**
- Max: 1 GB per file

**Response (202 Accepted):**
```json
{
  "job": {
    "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "serviceId": "my-service",
    "layerId": "cities",
    "fileName": "cities.gpkg",
    "status": "Queued",
    "createdAt": "2025-10-15T12:00:00Z",
    "progress": {
      "phase": "Queued",
      "percentComplete": 0
    }
  }
}
```

**Job Statuses:**
- `Queued` - Waiting to start
- `Running` - Processing
- `Completed` - Successfully completed
- `Failed` - Failed with error
- `Cancelled` - Cancelled by user

### List Ingestion Jobs

```bash
GET /admin/ingestion/jobs
```

**Response:**
```json
{
  "jobs": [
    {
      "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "serviceId": "my-service",
      "layerId": "cities",
      "fileName": "cities.gpkg",
      "status": "Completed",
      "createdAt": "2025-10-15T12:00:00Z",
      "completedAt": "2025-10-15T12:05:32Z",
      "progress": {
        "phase": "Completed",
        "percentComplete": 100,
        "featuresProcessed": 10000,
        "totalFeatures": 10000
      }
    }
  ]
}
```

### Get Ingestion Job

```bash
GET /admin/ingestion/jobs/{jobId}
```

**Response:**
```json
{
  "job": {
    "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "serviceId": "my-service",
    "layerId": "cities",
    "fileName": "cities.gpkg",
    "status": "Running",
    "createdAt": "2025-10-15T12:00:00Z",
    "progress": {
      "phase": "Importing",
      "percentComplete": 45,
      "featuresProcessed": 4500,
      "totalFeatures": 10000,
      "currentRate": 250.5
    }
  }
}
```

### Cancel Ingestion Job

```bash
DELETE /admin/ingestion/jobs/{jobId}
```

**Response:**
```json
{
  "job": {
    "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Cancelled",
    "cancelledAt": "2025-10-15T12:02:15Z",
    "message": "Cancelled via API"
  }
}
```

## Metadata Management

### Reload Metadata

Reload metadata from metadata.json without restarting the server.

```bash
POST /admin/metadata/reload
```

**Response:**
```json
{
  "status": "reloaded"
}
```

**Effect:**
- Reloads metadata.json from disk
- Resets OData model cache
- Services immediately reflect new configuration

**Errors:**
```json
{
  "error": "Metadata validation failed: Service 'my-service' references unknown datasource 'db'"
}
```

### Validate Metadata

Validate metadata without applying changes.

```bash
POST /admin/metadata/validate
Content-Type: application/json

{
  "catalog": { ... },
  "services": [ ... ]
}
```

**Response (Valid):**
```json
{
  "status": "valid",
  "warnings": [
    "Service 'test-service' has no layers configured"
  ]
}
```

**Response (Invalid):**
```json
{
  "error": "Metadata schema validation failed.",
  "details": [
    "services[0].id: Required field is missing",
    "dataSources[1].connectionString: Invalid connection string format"
  ]
}
```

### Diff Metadata

Compare proposed metadata with current configuration.

```bash
POST /admin/metadata/diff
Content-Type: application/json

{
  "catalog": { ... },
  "services": [ ... ]
}
```

**Response:**
```json
{
  "status": "ok",
  "warnings": [],
  "diff": {
    "services": {
      "added": ["new-service"],
      "removed": [],
      "modified": ["my-service"]
    },
    "layers": {
      "added": ["my-service.new-layer"],
      "removed": ["my-service.old-layer"],
      "modified": []
    },
    "dataSources": {
      "added": [],
      "removed": [],
      "modified": ["db"]
    }
  }
}
```

### Apply Metadata

Apply new metadata configuration.

```bash
POST /admin/metadata/apply
Content-Type: application/json

{
  "catalog": { ... },
  "services": [ ... ]
}
```

**Response:**
```json
{
  "status": "applied",
  "warnings": []
}
```

**Effect:**
- Validates metadata
- Writes to metadata.json on disk
- Reloads metadata registry
- Resets OData cache

**Restrictions:**
- ❌ Not available in QuickStart mode

### Metadata Snapshots

#### List Snapshots

```bash
GET /admin/metadata/snapshots
```

**Response:**
```json
{
  "snapshots": [
    {
      "label": "2025-10-15-pre-migration",
      "createdAt": "2025-10-15T10:30:00Z",
      "notes": "Before migrating cities layer",
      "sizeBytes": 45678
    }
  ]
}
```

#### Create Snapshot

```bash
POST /admin/metadata/snapshots
Content-Type: application/json

{
  "label": "2025-10-15-pre-migration",
  "notes": "Before migrating cities layer"
}
```

**Response (201 Created):**
```json
{
  "snapshot": {
    "label": "2025-10-15-pre-migration",
    "createdAt": "2025-10-15T10:30:00Z",
    "notes": "Before migrating cities layer",
    "sizeBytes": 45678
  }
}
```

**Restrictions:**
- ❌ Not available in QuickStart mode

#### Get Snapshot

```bash
GET /admin/metadata/snapshots/{label}
```

**Response:**
```json
{
  "snapshot": {
    "label": "2025-10-15-pre-migration",
    "createdAt": "2025-10-15T10:30:00Z",
    "notes": "Before migrating cities layer",
    "sizeBytes": 45678
  },
  "metadata": {
    "catalog": { ... },
    "services": [ ... ]
  }
}
```

#### Restore Snapshot

```bash
POST /admin/metadata/snapshots/{label}/restore
```

**Response:**
```json
{
  "status": "restored",
  "label": "2025-10-15-pre-migration"
}
```

**Effect:**
- Restores metadata.json from snapshot
- Reloads metadata registry
- Resets OData cache

**Restrictions:**
- ❌ Not available in QuickStart mode

## Migration

### Create Migration Job

Migrate an Esri FeatureServer or MapServer to Honua.

```bash
POST /admin/migrations/jobs
Content-Type: application/json

{
  "sourceServiceUri": "https://services.arcgis.com/P3ePLMYs2RVChkJx/arcgis/rest/services/USA_Counties/FeatureServer",
  "targetServiceId": "my-service",
  "targetFolderId": "my-folder",
  "targetDataSourceId": "db",
  "layerIds": [0, 1, 2],
  "includeData": true,
  "batchSize": 1000,
  "translatorOptions": {
    "serviceTitle": "USA Counties",
    "serviceDescription": "Migrated from ArcGIS Online",
    "tableNamePrefix": "usa_",
    "geometryColumnName": "shape",
    "layerIdPrefix": "county_",
    "useLayerIdsForTables": false
  }
}
```

**Request Fields:**
- `sourceServiceUri` (required) - Geoservices REST a.k.a. Esri REST service URL
- `targetServiceId` (required) - Target service ID in metadata.json
- `targetFolderId` (required) - Target folder ID
- `targetDataSourceId` (required) - Target data source ID
- `layerIds` (optional) - Specific layer IDs to migrate (null = all)
- `includeData` (optional) - Include feature data (default: true)
- `batchSize` (optional) - Features per batch (default: 1000)
- `translatorOptions` (optional) - Metadata translation options

**Response (202 Accepted):**
```json
{
  "job": {
    "jobId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "sourceServiceUri": "https://services.arcgis.com/.../FeatureServer",
    "targetServiceId": "my-service",
    "status": "Queued",
    "createdAt": "2025-10-15T14:00:00Z",
    "progress": {
      "phase": "Queued",
      "percentComplete": 0
    }
  }
}
```

### List Migration Jobs

```bash
GET /admin/migrations/jobs
```

### Get Migration Job

```bash
GET /admin/migrations/jobs/{jobId}
```

**Response:**
```json
{
  "job": {
    "jobId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "sourceServiceUri": "https://services.arcgis.com/.../FeatureServer",
    "targetServiceId": "my-service",
    "status": "Running",
    "createdAt": "2025-10-15T14:00:00Z",
    "progress": {
      "phase": "MigratingData",
      "percentComplete": 35,
      "layersCompleted": 1,
      "totalLayers": 3,
      "currentLayer": "Counties",
      "featuresProcessed": 3500,
      "totalFeatures": 10000
    }
  }
}
```

### Cancel Migration Job

```bash
DELETE /admin/migrations/jobs/{jobId}
```

## Raster Tile Cache

### Create Preseed Job

Pre-generate raster tiles for specified datasets and zoom levels.

```bash
POST /admin/raster-cache/jobs
Content-Type: application/json

{
  "datasetIds": ["landsat8-b1", "landsat8-b2"],
  "tileMatrixSetId": "WorldWebMercatorQuad",
  "minZoom": 0,
  "maxZoom": 10,
  "styleId": "default",
  "transparent": true,
  "format": "image/png",
  "overwrite": false,
  "tileSize": 256
}
```

**Request Fields:**
- `datasetIds` (required) - Array of raster dataset IDs
- `tileMatrixSetId` (optional) - Default: `WorldWebMercatorQuad`
- `minZoom` (optional) - Default: 0
- `maxZoom` (optional) - Default: 18
- `styleId` (optional) - Style to apply
- `transparent` (optional) - Default: true
- `format` (optional) - Default: `image/png`
- `overwrite` (optional) - Default: false
- `tileSize` (optional) - Default: 256

**Response (202 Accepted):**
```json
{
  "job": {
    "jobId": "8d7c5b02-3e1f-4a6d-9c8b-f2e1d3c4a5b6",
    "datasetIds": ["landsat8-b1", "landsat8-b2"],
    "tileMatrixSetId": "WorldWebMercatorQuad",
    "minZoom": 0,
    "maxZoom": 10,
    "status": "Queued",
    "createdAt": "2025-10-15T15:00:00Z",
    "progress": {
      "phase": "Queued",
      "percentComplete": 0
    }
  }
}
```

### List Preseed Jobs

```bash
GET /admin/raster-cache/jobs
```

### Get Preseed Job

```bash
GET /admin/raster-cache/jobs/{jobId}
```

**Response:**
```json
{
  "job": {
    "jobId": "8d7c5b02-3e1f-4a6d-9c8b-f2e1d3c4a5b6",
    "datasetIds": ["landsat8-b1", "landsat8-b2"],
    "status": "Running",
    "createdAt": "2025-10-15T15:00:00Z",
    "progress": {
      "phase": "GeneratingTiles",
      "percentComplete": 42,
      "tilesGenerated": 4200,
      "totalTiles": 10000,
      "currentZoom": 5,
      "currentDataset": "landsat8-b1"
    }
  }
}
```

### Cancel Preseed Job

```bash
DELETE /admin/raster-cache/jobs/{jobId}
```

### Purge Cache

Remove cached tiles for specified datasets.

```bash
POST /admin/raster-cache/datasets/purge
Content-Type: application/json

{
  "datasetIds": ["landsat8-b1", "landsat8-b2"]
}
```

**Response:**
```json
{
  "purged": ["landsat8-b1", "landsat8-b2"],
  "failed": []
}
```

### Cache Statistics

#### Get Overall Statistics

```bash
GET /admin/raster-cache/statistics
```

**Authorization:** Viewer role or higher

**Response:**
```json
{
  "totalSizeBytes": 5368709120,
  "totalTiles": 125000,
  "datasets": 5,
  "cacheHits": 2500000,
  "cacheMisses": 125000,
  "hitRate": 0.952
}
```

#### Get All Dataset Statistics

```bash
GET /admin/raster-cache/statistics/datasets
```

**Response:**
```json
[
  {
    "datasetId": "landsat8-b1",
    "totalSizeBytes": 1073741824,
    "totalTiles": 25000,
    "cacheHits": 500000,
    "cacheMisses": 25000,
    "hitRate": 0.952,
    "lastAccessedAt": "2025-10-15T16:00:00Z"
  }
]
```

#### Get Dataset Statistics

```bash
GET /admin/raster-cache/statistics/datasets/{datasetId}
```

**Response:**
```json
{
  "datasetId": "landsat8-b1",
  "totalSizeBytes": 1073741824,
  "totalTiles": 25000,
  "cacheHits": 500000,
  "cacheMisses": 25000,
  "hitRate": 0.952,
  "lastAccessedAt": "2025-10-15T16:00:00Z",
  "zoomLevels": [
    {
      "zoom": 0,
      "tiles": 1,
      "sizeBytes": 42949
    },
    {
      "zoom": 5,
      "tiles": 1024,
      "sizeBytes": 43980465
    }
  ]
}
```

#### Reset Statistics

```bash
POST /admin/raster-cache/statistics/reset
```

**Response:** 204 No Content

### Cache Quota Management

#### Get All Quotas

```bash
GET /admin/raster-cache/quota
```

**Response:**
```json
{
  "quotas": {
    "landsat8-b1": {
      "maxSizeBytes": 5368709120,
      "maxTiles": 100000,
      "evictionPolicy": "LRU"
    },
    "landsat8-b2": {
      "maxSizeBytes": 5368709120,
      "maxTiles": 100000,
      "evictionPolicy": "LRU"
    }
  }
}
```

#### Get Quota Status

```bash
GET /admin/raster-cache/quota/{datasetId}/status
```

**Response:**
```json
{
  "datasetId": "landsat8-b1",
  "quota": {
    "maxSizeBytes": 5368709120,
    "maxTiles": 100000,
    "evictionPolicy": "LRU"
  },
  "current": {
    "sizeBytes": 4294967296,
    "tiles": 75000
  },
  "utilization": {
    "sizePercent": 80.0,
    "tilesPercent": 75.0
  },
  "overQuota": false
}
```

#### Update Quota

```bash
PUT /admin/raster-cache/quota/{datasetId}
Content-Type: application/json

{
  "maxSizeBytes": 10737418240,
  "maxTiles": 200000,
  "evictionPolicy": "LFU"
}
```

**Eviction Policies:**
- `LRU` - Least Recently Used
- `LFU` - Least Frequently Used
- `FIFO` - First In First Out

**Response:** 204 No Content

#### Enforce Quota

Immediately enforce quota by removing tiles if over quota.

```bash
POST /admin/raster-cache/quota/{datasetId}/enforce
```

**Response:**
```json
{
  "datasetId": "landsat8-b1",
  "before": {
    "sizeBytes": 6442450944,
    "tiles": 120000
  },
  "after": {
    "sizeBytes": 5368709120,
    "tiles": 100000
  },
  "removed": {
    "sizeBytes": 1073741824,
    "tiles": 20000
  }
}
```

## Error Responses

### Common Error Codes

| Status Code | Meaning | Example |
|-------------|---------|---------|
| 400 Bad Request | Invalid request body or parameters | `{"error": "Invalid file type '.txt'"}` |
| 401 Unauthorized | Authentication required | `{"error": "Authentication required"}` |
| 403 Forbidden | Insufficient permissions | `{"error": "Administrator role required"}` |
| 404 Not Found | Resource not found | `{"error": "Job not found"}` |
| 422 Unprocessable Entity | Validation failed | `{"error": "Metadata validation failed", "details": [...]}` |
| 500 Internal Server Error | Server error | `{"error": "Internal server error"}` |

### QuickStart Mode Restrictions

When `authentication.mode=QuickStart`, certain endpoints return:

```json
{
  "error": "Metadata updates are disabled while QuickStart authentication mode is active."
}
```

**Status:** 403 Forbidden

**Affected Endpoints:**
- `POST /admin/metadata/apply`
- `POST /admin/metadata/snapshots`
- `POST /admin/metadata/snapshots/{label}/restore`
- `PATCH /admin/config/services/{protocol}`
- `PATCH /admin/config/services/{serviceId}/{protocol}`

## Related Documentation

- [Authentication Setup](02-02-authentication-setup.md) - Configure admin authentication
- [Configuration Reference](02-01-configuration-reference.md) - appsettings.json options
- [Common Issues](05-02-common-issues.md) - Troubleshooting admin API
- [CLI Commands](05-01-cli-commands.md) - CLI control plane clients

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Status**: ✅ COMPREHENSIVE & CURRENT
