# Honua Administrative API

Complete reference for administrative endpoints used to manage metadata, migrations, and raster caching.

## Table of Contents
- [Authentication](#authentication)
- [Metadata Management](#metadata-management)
- [Esri Service Migration](#esri-service-migration)
- [Raster Tile Cache](#raster-tile-cache)
- [Error Responses](#error-responses)

## Authentication

All administrative endpoints require the `administrator` role.

**Include JWT token in requests:**
```http
Authorization: Bearer {token}
```

See [Authentication Guide](authentication.md) for obtaining tokens.

## Metadata Management

Manage layer and service metadata configuration.

### Base URL
```
/admin/metadata
```

### Reload Metadata

Reload metadata from disk without restarting the server.

```http
POST /admin/metadata/reload
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Metadata reloaded successfully",
  "servicesCount": 3,
  "layersCount": 12,
  "reloadedAt": "2025-10-01T10:30:00Z"
}
```

**Use Case:** After manually editing metadata files on disk.

### Validate Metadata

Validate metadata against JSON schema without applying changes.

```http
POST /admin/metadata/validate
Content-Type: application/json
Authorization: Bearer {token}

{
  "services": [...],
  "rasterDatasets": [...],
  "styles": [...]
}
```

**Success Response (200 OK):**
```json
{
  "valid": true,
  "warnings": [
    "Layer 'roads-primary' missing storage.crs recommendation"
  ]
}
```

**Validation Error Response (422 Unprocessable Entity):**
```json
{
  "valid": false,
  "errors": [
    {
      "path": "/services/0/layers/2/dataSource/table",
      "message": "Required property 'table' is missing"
    },
    {
      "path": "/services/1/id",
      "message": "Service ID must be unique"
    }
  ],
  "warnings": []
}
```

### Apply Metadata

Apply new metadata configuration with validation.

```http
POST /admin/metadata/apply
Content-Type: application/json
Authorization: Bearer {token}

{
  "services": [
    {
      "id": "transportation",
      "title": "Transportation Services",
      "type": "feature",
      "layers": [...]
    }
  ],
  "rasterDatasets": [],
  "styles": []
}
```

**Success Response (200 OK):**
```json
{
  "applied": true,
  "message": "Metadata applied successfully",
  "servicesCount": 3,
  "layersCount": 12,
  "appliedAt": "2025-10-01T10:35:00Z",
  "warnings": [
    "Layer 'parcels' has no spatial index configured"
  ]
}
```

**Validation Error Response (422 Unprocessable Entity):**
```json
{
  "applied": false,
  "valid": false,
  "errors": [
    {
      "path": "/services/0/layers/0/id",
      "message": "Layer ID 'roads' conflicts with existing layer"
    }
  ]
}
```

**Process:**
1. Validate against JSON schema
2. Check for conflicts with existing metadata
3. Write to disk (atomic operation)
4. Reload in-memory metadata

### Compare Metadata

Compare proposed metadata with current configuration.

```http
POST /admin/metadata/diff
Content-Type: application/json
Authorization: Bearer {token}

{
  "services": [...]
}
```

**Response (200 OK):**
```json
{
  "changes": [
    {
      "type": "added",
      "path": "/services/transportation/layers/bike-lanes",
      "description": "New layer 'bike-lanes' added to service 'transportation'"
    },
    {
      "type": "modified",
      "path": "/services/utilities/layers/water-mains/title",
      "oldValue": "Water Mains",
      "newValue": "Water Distribution Network",
      "description": "Title changed"
    },
    {
      "type": "removed",
      "path": "/services/legacy/layers/old-parcels",
      "description": "Layer 'old-parcels' removed from service 'legacy'"
    }
  ],
  "summary": {
    "servicesAdded": 0,
    "servicesModified": 2,
    "servicesRemoved": 0,
    "layersAdded": 1,
    "layersModified": 1,
    "layersRemoved": 1
  }
}
```

**Change Types:**
- `added` - New service, layer, or property
- `modified` - Changed value
- `removed` - Deleted service, layer, or property

### List Snapshots

Get all metadata snapshots.

```http
GET /admin/metadata/snapshots
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "snapshots": [
    {
      "label": "pre-migration-2025-10-01",
      "createdAt": "2025-10-01T08:00:00Z",
      "createdBy": "admin",
      "notes": "Backup before ArcGIS migration",
      "servicesCount": 2,
      "layersCount": 8,
      "sizeBytes": 45678
    },
    {
      "label": "production-stable-v1",
      "createdAt": "2025-09-28T14:30:00Z",
      "createdBy": "admin",
      "notes": "Stable production configuration",
      "servicesCount": 2,
      "layersCount": 8,
      "sizeBytes": 42103
    }
  ]
}
```

### Create Snapshot

Create a backup of current metadata.

```http
POST /admin/metadata/snapshots
Content-Type: application/json
Authorization: Bearer {token}

{
  "label": "pre-update-2025-10-01",
  "notes": "Backup before adding new layers"
}
```

**Response (201 Created):**
```json
{
  "label": "pre-update-2025-10-01",
  "createdAt": "2025-10-01T11:00:00Z",
  "createdBy": "admin",
  "notes": "Backup before adding new layers",
  "servicesCount": 3,
  "layersCount": 12,
  "sizeBytes": 52491,
  "path": "data/snapshots/pre-update-2025-10-01.json"
}
```

**Best Practices:**
- Create snapshot before major changes
- Use descriptive labels
- Include notes explaining purpose
- Keep snapshots for 30-90 days

### Get Snapshot

Retrieve specific snapshot metadata.

```http
GET /admin/metadata/snapshots/{label}
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "label": "pre-migration-2025-10-01",
  "createdAt": "2025-10-01T08:00:00Z",
  "createdBy": "admin",
  "notes": "Backup before ArcGIS migration",
  "metadata": {
    "services": [...],
    "rasterDatasets": [...],
    "styles": [...]
  }
}
```

### Restore Snapshot

Restore metadata from a snapshot.

```http
POST /admin/metadata/snapshots/{label}/restore
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "restored": true,
  "label": "pre-migration-2025-10-01",
  "restoredAt": "2025-10-01T15:30:00Z",
  "servicesCount": 2,
  "layersCount": 8,
  "message": "Metadata restored successfully from snapshot 'pre-migration-2025-10-01'"
}
```

**Process:**
1. Load snapshot from disk
2. Validate snapshot metadata
3. Apply as current metadata
4. Reload server configuration

**Warning:** Restoring a snapshot replaces **all** current metadata.

## Esri Service Migration

Migrate layers and data from ArcGIS Server / ArcGIS Online to Honua.

### Base URL
```
/admin/migrations
```

### Create Migration Job

Start a new migration from an Esri service.

```http
POST /admin/migrations/jobs
Content-Type: application/json
Authorization: Bearer {token}

{
  "sourceUrl": "https://services.arcgis.com/abc123/arcgis/rest/services/Transportation/FeatureServer",
  "sourceToken": null,
  "targetProvider": "postgis",
  "targetConnectionString": "env:HONUA_POSTGIS_CONN",
  "targetSchema": "arcgis_migration",
  "options": {
    "batchSize": 1000,
    "includeAttachments": true,
    "translator": {
      "tableNamingStrategy": "service-layer",
      "geometryColumnName": "geom",
      "idColumnPrefix": "esri_"
    },
    "layerFilter": null
  }
}
```

**Request Parameters:**

| Parameter | Description | Required |
|-----------|-------------|----------|
| `sourceUrl` | Esri FeatureServer URL | Yes |
| `sourceToken` | Esri token (if secured) | No |
| `targetProvider` | Target database (`postgis`, `sqlite`, `sqlserver`) | Yes |
| `targetConnectionString` | Database connection (supports `env:VAR`) | Yes |
| `targetSchema` | Database schema | Yes (except SQLite) |
| `options.batchSize` | Features per batch | No (default: 1000) |
| `options.includeAttachments` | Migrate attachments | No (default: false) |
| `options.translator.tableNamingStrategy` | Table naming (`service-layer`, `layer-only`) | No (default: `service-layer`) |
| `options.translator.geometryColumnName` | Geometry column name | No (default: `geom`) |
| `options.translator.idColumnPrefix` | ID column prefix | No (default: empty) |
| `options.layerFilter` | Layer IDs to migrate (null = all) | No |

**Response (202 Accepted):**
```json
{
  "jobId": "migration-a3f9c8e1",
  "status": "queued",
  "createdAt": "2025-10-01T09:00:00Z",
  "sourceUrl": "https://services.arcgis.com/abc123/arcgis/rest/services/Transportation/FeatureServer",
  "targetProvider": "postgis",
  "estimatedLayers": 5
}
```

**Migration Process:**
1. Query Esri service metadata
2. Create target tables/schemas
3. Migrate features in batches
4. Download attachments (if enabled)
5. Generate Honua metadata
6. Validate migrated data

### List Migration Jobs

Get all migration jobs.

```http
GET /admin/migrations/jobs
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "jobs": [
    {
      "jobId": "migration-a3f9c8e1",
      "status": "completed",
      "createdAt": "2025-10-01T09:00:00Z",
      "completedAt": "2025-10-01T09:15:00Z",
      "sourceUrl": "https://services.arcgis.com/.../Transportation/FeatureServer",
      "layersMigrated": 5,
      "featuresMigrated": 45632,
      "attachmentsMigrated": 128
    },
    {
      "jobId": "migration-b7e2d5f3",
      "status": "running",
      "createdAt": "2025-10-01T10:00:00Z",
      "progress": 0.45,
      "currentLayer": "Parcels (3/8)",
      "featuresMigrated": 12500
    }
  ]
}
```

### Get Migration Job Status

Get detailed status of a migration job.

```http
GET /admin/migrations/jobs/{jobId}
Authorization: Bearer {token}
```

**Response (200 OK) - Running:**
```json
{
  "jobId": "migration-b7e2d5f3",
  "status": "running",
  "createdAt": "2025-10-01T10:00:00Z",
  "progress": 0.45,
  "currentLayer": {
    "layerId": 3,
    "name": "Parcels",
    "featureCount": 50000,
    "featuresProcessed": 22500
  },
  "layers": [
    {"layerId": 0, "name": "Roads", "status": "completed", "featuresMigrated": 5000},
    {"layerId": 1, "name": "Buildings", "status": "completed", "featuresMigrated": 12000},
    {"layerId": 2, "name": "Utilities", "status": "completed", "featuresMigrated": 3200},
    {"layerId": 3, "name": "Parcels", "status": "running", "featuresMigrated": 22500},
    {"layerId": 4, "name": "Zoning", "status": "pending", "featuresMigrated": 0}
  ],
  "totalFeatures": 95000,
  "featuresMigrated": 42700,
  "errors": []
}
```

**Response (200 OK) - Completed:**
```json
{
  "jobId": "migration-a3f9c8e1",
  "status": "completed",
  "createdAt": "2025-10-01T09:00:00Z",
  "completedAt": "2025-10-01T09:15:00Z",
  "duration": "00:15:23",
  "sourceUrl": "https://services.arcgis.com/.../Transportation/FeatureServer",
  "targetProvider": "postgis",
  "layers": [
    {"layerId": 0, "name": "Roads", "status": "completed", "featuresMigrated": 5000, "attachmentsMigrated": 25},
    {"layerId": 1, "name": "Bridges", "status": "completed", "featuresMigrated": 150, "attachmentsMigrated": 75},
    {"layerId": 2, "name": "Signs", "status": "completed", "featuresMigrated": 3500, "attachmentsMigrated": 28}
  ],
  "featuresMigrated": 8650,
  "attachmentsMigrated": 128,
  "generatedMetadata": {
    "services": [
      {
        "id": "transportation",
        "title": "Transportation",
        "type": "feature",
        "provider": "postgis",
        "layers": [...]
      }
    ]
  },
  "errors": []
}
```

**Response (200 OK) - Failed:**
```json
{
  "jobId": "migration-c9a4b6d2",
  "status": "failed",
  "createdAt": "2025-10-01T11:00:00Z",
  "failedAt": "2025-10-01T11:05:00Z",
  "error": "Failed to connect to target database",
  "errors": [
    {
      "layer": "Roads",
      "message": "Connection timeout to PostgreSQL server"
    }
  ],
  "featuresMigrated": 0
}
```

**Status Values:**
- `queued` - Waiting to start
- `running` - Actively migrating
- `completed` - Successfully finished
- `failed` - Error occurred
- `cancelled` - User cancelled

### Cancel Migration Job

Cancel a running migration.

```http
DELETE /admin/migrations/jobs/{jobId}
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "jobId": "migration-b7e2d5f3",
  "status": "cancelled",
  "cancelledAt": "2025-10-01T10:30:00Z",
  "featuresMigrated": 22500,
  "message": "Migration cancelled by user. Partial data may exist in target database."
}
```

**Note:** Cancelling does not roll back migrated data. Use database transactions or manual cleanup if needed.

## Raster Tile Cache

Manage raster tile generation and caching.

### Base URL
```
/admin/raster-cache
```

### Create Preseed Job

Pre-generate tiles for a raster dataset.

```http
POST /admin/raster-cache/jobs
Content-Type: application/json
Authorization: Bearer {token}

{
  "datasetIds": ["roads-imagery", "parcels-aerial"],
  "tileMatrixSet": "WorldWebMercatorQuad",
  "minZoom": 0,
  "maxZoom": 14,
  "bbox": [-122.6, 45.5, -122.3, 45.7],
  "concurrency": 4,
  "format": "png"
}
```

**Request Parameters:**

| Parameter | Description | Required |
|-----------|-------------|----------|
| `datasetIds` | Raster dataset IDs to preseed | Yes |
| `tileMatrixSet` | Tile matrix set ID | No (default: `WorldWebMercatorQuad`) |
| `minZoom` | Minimum zoom level | No (default: 0) |
| `maxZoom` | Maximum zoom level | Yes |
| `bbox` | Bounding box [west, south, east, north] | No (null = full extent) |
| `concurrency` | Parallel tile generation | No (default: 4) |
| `format` | Tile format (`png`, `jpeg`, `webp`) | No (default: `png`) |

**Response (202 Accepted):**
```json
{
  "jobId": "preseed-f4e8a2c7",
  "status": "queued",
  "createdAt": "2025-10-01T12:00:00Z",
  "datasetIds": ["roads-imagery", "parcels-aerial"],
  "estimatedTiles": 25678,
  "minZoom": 0,
  "maxZoom": 14
}
```

**Estimated Tiles Calculation:**
For each zoom level, tiles = (bbox_area / tile_area) × 2^zoom

### List Preseed Jobs

Get all preseed jobs.

```http
GET /admin/raster-cache/jobs
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "jobs": [
    {
      "jobId": "preseed-f4e8a2c7",
      "status": "running",
      "createdAt": "2025-10-01T12:00:00Z",
      "progress": 0.35,
      "tilesGenerated": 9000,
      "estimatedTiles": 25678,
      "currentZoom": 12
    },
    {
      "jobId": "preseed-a1b2c3d4",
      "status": "completed",
      "createdAt": "2025-09-30T08:00:00Z",
      "completedAt": "2025-09-30T10:45:00Z",
      "tilesGenerated": 15234,
      "duration": "02:45:12"
    }
  ]
}
```

### Get Preseed Job Status

Get detailed status of a preseed job.

```http
GET /admin/raster-cache/jobs/{jobId}
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "jobId": "preseed-f4e8a2c7",
  "status": "running",
  "createdAt": "2025-10-01T12:00:00Z",
  "progress": 0.35,
  "datasetIds": ["roads-imagery"],
  "minZoom": 0,
  "maxZoom": 14,
  "tilesGenerated": 9000,
  "estimatedTiles": 25678,
  "currentZoom": 12,
  "zoomProgress": [
    {"zoom": 0, "tiles": 1, "status": "completed"},
    {"zoom": 1, "tiles": 4, "status": "completed"},
    {"zoom": 2, "tiles": 16, "status": "completed"},
    ...
    {"zoom": 12, "tiles": 4096, "generated": 1435, "status": "running"},
    {"zoom": 13, "tiles": 16384, "status": "pending"},
    {"zoom": 14, "tiles": 65536, "status": "pending"}
  ],
  "errors": []
}
```

### Cancel Preseed Job

Cancel a running preseed job.

```http
DELETE /admin/raster-cache/jobs/{jobId}
Authorization: Bearer {token}
```

**Response (200 OK):**
```json
{
  "jobId": "preseed-f4e8a2c7",
  "status": "cancelled",
  "cancelledAt": "2025-10-01T13:30:00Z",
  "tilesGenerated": 9000,
  "message": "Preseed job cancelled. Generated tiles remain in cache."
}
```

### Purge Cache

Delete cached tiles for specific datasets.

```http
POST /admin/raster-cache/datasets/purge
Content-Type: application/json
Authorization: Bearer {token}

{
  "datasetIds": ["roads-imagery"],
  "zoomLevels": null
}
```

**Request Parameters:**

| Parameter | Description | Required |
|-----------|-------------|----------|
| `datasetIds` | Dataset IDs to purge | Yes |
| `zoomLevels` | Specific zoom levels (null = all) | No |

**Response (200 OK):**
```json
{
  "purged": true,
  "datasetIds": ["roads-imagery"],
  "tilesDeleted": 25678,
  "bytesFreed": 125894672,
  "purgedAt": "2025-10-01T14:00:00Z"
}
```

**Use Cases:**
- Clear cache after updating source imagery
- Free disk space
- Force regeneration of tiles

## Error Responses

### 401 Unauthorized

No authentication token provided.

```json
{
  "error": "Unauthorized",
  "message": "No authentication token provided"
}
```

### 403 Forbidden

Insufficient permissions.

```json
{
  "error": "Forbidden",
  "message": "Administrator role required for this operation"
}
```

### 404 Not Found

Resource not found.

```json
{
  "error": "Not Found",
  "message": "Migration job 'migration-xyz' not found"
}
```

### 409 Conflict

Resource conflict.

```json
{
  "error": "Conflict",
  "message": "A migration job is already running. Wait for completion or cancel it."
}
```

### 422 Unprocessable Entity

Validation error.

```json
{
  "error": "Validation Error",
  "message": "Metadata validation failed",
  "details": [
    {
      "path": "/services/0/id",
      "message": "Service ID cannot be empty"
    }
  ]
}
```

### 500 Internal Server Error

Server error.

```json
{
  "error": "Internal Server Error",
  "message": "Failed to write metadata to disk",
  "requestId": "abc123-def456"
}
```

## See Also

- [Authentication Guide](authentication.md) - Obtaining admin tokens
- [Metadata Authoring](metadata-authoring.md) - Metadata schema
- [Configuration Reference](configuration.md) - Admin API settings
