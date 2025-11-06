# Honua Metadata Schema Reference

**Keywords:** metadata, schema, configuration, json, services, layers, folders, dataSources, catalog, ogc, raster, styles, feature-services, wfs, wms, esri-rest

This document provides a comprehensive reference for the Honua metadata schema. The metadata JSON file defines all spatial services, layers, data sources, and styling that Honua exposes through OGC APIs, Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST APIs, and other geospatial protocols.

## Overview

Honua uses a declarative JSON metadata file to configure all aspects of the geospatial server. The metadata defines:

- **Catalog**: Overall catalog metadata and contact information
- **Folders**: Organizational structure for services
- **Data Sources**: Database connections (PostGIS, SQLite, SQL Server, MySQL)
- **Services**: Feature, raster, and tile services exposed via multiple protocols
- **Layers**: Individual spatial layers within services
- **Raster Datasets**: Cloud-optimized GeoTIFF (COG) and raster data sources
- **Styles**: Visual styling definitions for rendering
- **Server**: CORS and host configuration

## Top-Level Schema Structure

```json
{
  "server": { ... },
  "catalog": { ... },
  "folders": [ ... ],
  "dataSources": [ ... ],
  "services": [ ... ],
  "layers": [ ... ],
  "rasterDatasets": [ ... ],
  "styles": [ ... ]
}
```

---

## Server Configuration

The `server` section configures server-level security and CORS policies.

### Server Definition

```json
{
  "server": {
    "allowedHosts": ["example.com", "*.example.org"],
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowedMethods": ["GET", "POST", "OPTIONS"],
      "allowedHeaders": ["*"],
      "allowAnyHeader": false,
      "exposedHeaders": ["X-Total-Count"],
      "allowCredentials": false,
      "maxAgeSeconds": 3600
    }
  }
}
```

### Server Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `allowedHosts` | string[] | No | List of allowed host headers for the server |
| `cors` | CorsDefinition | No | CORS configuration object |

### CORS Configuration

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `enabled` | boolean | No | false | Enable CORS support |
| `allowedOrigins` | string[] | No | [] | List of allowed origins. Use "*" for any origin |
| `allowAnyOrigin` | boolean | No | false | Computed from allowedOrigins containing "*" |
| `allowedMethods` | string[] | No | [] | Allowed HTTP methods. Use "*" for any method |
| `allowAnyMethod` | boolean | No | false | Computed from allowedMethods containing "*" |
| `allowedHeaders` | string[] | No | [] | Allowed request headers. Use "*" for any header |
| `allowAnyHeader` | boolean | No | false | Computed from allowedHeaders containing "*" |
| `exposedHeaders` | string[] | No | [] | Response headers exposed to client |
| `allowCredentials` | boolean | No | false | Allow credentials (cookies, auth headers) |
| `maxAgeSeconds` | integer | No | null | Preflight cache duration in seconds |

**Important**: CORS configuration cannot allow credentials when `allowAnyOrigin` is true. Specify explicit origins if credentials are required.

---

## Catalog Configuration

The `catalog` section provides top-level metadata about the entire spatial data catalog.

### Catalog Definition

```json
{
  "catalog": {
    "id": "honua-ogc-sample",
    "title": "Honua OGC Sample Catalog",
    "description": "Sample metadata for OGC API Features development",
    "version": "2025.09",
    "publisher": "Organization Name",
    "keywords": ["ogc", "features", "sample"],
    "themeCategories": ["transportation", "environment"],
    "links": [
      {
        "href": "https://ogcapi.ogc.org/features/",
        "rel": "about",
        "type": "text/html",
        "title": "OGC API Features"
      }
    ],
    "contact": {
      "name": "GIS Team",
      "email": "gis@example.com",
      "organization": "Example Organization",
      "phone": "+1-555-0100",
      "url": "https://example.com",
      "role": "pointOfContact"
    },
    "license": {
      "name": "CC-BY-4.0",
      "url": "https://creativecommons.org/licenses/by/4.0/"
    },
    "extents": {
      "spatial": {
        "bbox": [[-180, -90, 180, 90]],
        "crs": "EPSG:4326"
      },
      "temporal": {
        "interval": [
          {
            "start": "2020-01-01T00:00:00Z",
            "end": "2025-12-31T23:59:59Z"
          }
        ],
        "temporalReferenceSystem": "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
      }
    }
  }
}
```

### Catalog Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | **Yes** | Unique identifier for the catalog |
| `title` | string | No | Human-readable title |
| `description` | string | No | Detailed description of the catalog |
| `version` | string | No | Catalog version identifier |
| `publisher` | string | No | Organization or entity publishing the catalog |
| `keywords` | string[] | No | Keywords for discovery |
| `themeCategories` | string[] | No | Theme categories for classification |
| `links` | LinkDefinition[] | No | Related links |
| `contact` | CatalogContactDefinition | No | Primary contact information |
| `license` | CatalogLicenseDefinition | No | License information |
| `extents` | CatalogExtentDefinition | No | Spatial and temporal extents |

---

## Folders

Folders provide organizational structure for services, similar to ArcGIS Server folder organization.

### Folder Definition

```json
{
  "folders": [
    {
      "id": "transportation",
      "title": "Transportation Services",
      "order": 10
    },
    {
      "id": "environment",
      "title": "Environmental Data",
      "order": 20
    }
  ]
}
```

### Folder Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | **Yes** | Unique folder identifier |
| `title` | string | No | Display name for the folder |
| `order` | integer | No | Sort order for folder display |

---

## Data Sources

Data sources define database connections used by services and layers.

### Data Source Definition

```json
{
  "dataSources": [
    {
      "id": "postgis-primary",
      "provider": "postgis",
      "connectionString": "Host=localhost;Database=spatial;Username=gis;Password=secret"
    },
    {
      "id": "sqlite-local",
      "provider": "sqlite",
      "connectionString": "Data Source=./data/local.db;Version=3;Pooling=false;"
    },
    {
      "id": "sqlserver-main",
      "provider": "sqlserver",
      "connectionString": "Server=localhost;Database=GIS;User Id=sa;Password=secret;TrustServerCertificate=True"
    },
    {
      "id": "mysql-geo",
      "provider": "mysql",
      "connectionString": "Server=localhost;Database=geodata;Uid=root;Pwd=secret;"
    }
  ]
}
```

### Data Source Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | **Yes** | Unique data source identifier |
| `provider` | string | **Yes** | Database provider: `postgis`, `sqlite`, `sqlserver`, `mysql` |
| `connectionString` | string | **Yes** | Database connection string |

### Provider-Specific Notes

**PostGIS**:
- Full support for spatial queries and CRS transformations
- Use `EPSG:4326` as default CRS
- Connection pooling enabled by default

**SQLite**:
- Use relative or absolute file paths in connection string
- Disable pooling for file-based databases: `Pooling=false;`
- Spatialite extension required for spatial operations

**SQL Server**:
- Supports both `geometry` and `geography` data types
- Specify SRID in storage configuration
- Use `TrustServerCertificate=True` for development environments

**MySQL**:
- Supports spatial extensions
- Connection string uses `Server`, `Database`, `Uid`, `Pwd` parameters

---

## Services

Services represent logical groupings of layers, exposed via OGC APIs and Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST APIs.

### Service Definition

```json
{
  "services": [
    {
      "id": "roads",
      "title": "Road Centerlines",
      "folderId": "transportation",
      "serviceType": "feature",
      "dataSourceId": "postgis-primary",
      "enabled": true,
      "description": "Transportation datasets for road networks",
      "keywords": ["transportation", "roads", "highways"],
      "links": [
        {
          "href": "https://example.org/datasets/roads",
          "rel": "collection",
          "type": "text/html",
          "title": "Roads Overview"
        }
      ],
      "catalog": {
        "summary": "Official road centerline network",
        "keywords": ["roads", "transport"],
        "themes": ["transportation"],
        "thumbnail": "/media/roads.png",
        "ordering": 10,
        "spatialExtent": {
          "bbox": [[-122.5, 45.5, -122.4, 45.6]],
          "crs": "EPSG:4326"
        },
        "temporalExtent": {
          "start": "2020-01-01T00:00:00Z",
          "end": "2025-12-31T23:59:59Z"
        },
        "contacts": [
          {
            "name": "GIS Team",
            "email": "gis@example.com",
            "organization": "Example Org",
            "role": "custodian"
          }
        ]
      },
      "ogc": {
        "collectionsEnabled": true,
        "itemLimit": 1000,
        "defaultCrs": "EPSG:4326",
        "additionalCrs": ["EPSG:3857", "EPSG:2927"],
        "conformanceClasses": [
          "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
          "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson"
        ]
      }
    }
  ]
}
```

### Service Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | string | **Yes** | - | Unique service identifier |
| `title` | string | No | id value | Display title |
| `folderId` | string | **Yes** | - | Reference to folder `id` |
| `serviceType` | string | No | "feature" | Service type: `feature`, `raster`, `tile`, `image`, `map` |
| `dataSourceId` | string | **Yes** | - | Reference to data source `id` |
| `enabled` | boolean | No | true | Whether service is enabled |
| `description` | string | No | null | Detailed description |
| `keywords` | string[] | No | [] | Keywords for discovery |
| `links` | LinkDefinition[] | No | [] | Related links |
| `catalog` | CatalogEntryDefinition | No | {} | Catalog metadata |
| `ogc` | OgcServiceDefinition | No | {} | OGC-specific configuration |

### Service Types

| Value | Description | Protocols Supported |
|-------|-------------|---------------------|
| `feature` or `FeatureServer` | Vector feature services | OGC API Features, WFS, Geoservices REST a.k.a. Esri REST Feature Server |
| `raster` or `ImageServer` | Raster/imagery services | OGC API Coverages, WMS, Geoservices REST a.k.a. Esri REST Image Server |
| `tile` | Tile services | OGC API Tiles, WMTS |
| `map` or `MapServer` | Map rendering services | WMS, Geoservices REST a.k.a. Esri REST Map Server |

### OGC Service Configuration

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `collectionsEnabled` | boolean | No | true | Enable OGC API collections endpoint |
| `itemLimit` | integer | No | null | Maximum items per request |
| `defaultCrs` | string | No | null | Default CRS (e.g., "EPSG:4326") |
| `additionalCrs` | string[] | No | [] | Additional supported CRS |
| `conformanceClasses` | string[] | No | [] | OGC conformance class URIs |

---

## Layers

Layers represent individual spatial datasets within services.

### Layer Definition

```json
{
  "layers": [
    {
      "id": "roads-primary",
      "serviceId": "roads",
      "title": "Primary Roads",
      "description": "Major roadways extracted from the Simple Features sample bundle",
      "geometryType": "LineString",
      "idField": "road_id",
      "displayField": "name",
      "geometryField": "geom",
      "crs": ["EPSG:4326", "EPSG:3857"],
      "keywords": ["roads", "transport"],
      "itemType": "feature",
      "minScale": 1000000,
      "maxScale": 0,
      "links": [
        {
          "href": "/ogc/collections/roads::roads-primary",
          "rel": "self",
          "type": "application/json",
          "title": "Primary Roads"
        }
      ],
      "extent": {
        "bbox": [[-122.5, 45.5, -122.4, 45.6]],
        "crs": "EPSG:4326",
        "temporal": {
          "interval": [
            ["2020-01-01T00:00:00Z", "2021-12-31T00:00:00Z"]
          ],
          "trs": "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
        }
      },
      "query": {
        "maxRecordCount": 1000,
        "supportedParameters": ["bbox", "limit", "offset", "datetime"],
        "autoFilter": {
          "cql": "status = 'active' AND road_class IN ('primary', 'highway')"
        }
      },
      "storage": {
        "table": "roads_primary",
        "geometryColumn": "geom",
        "primaryKey": "road_id",
        "temporalColumn": "observed_at",
        "srid": 4326,
        "crs": "EPSG:4326"
      },
      "fields": [
        {
          "name": "road_id",
          "alias": "Road ID",
          "dataType": "integer",
          "nullable": false,
          "editable": false
        },
        {
          "name": "name",
          "alias": "Road Name",
          "dataType": "string",
          "maxLength": 100,
          "nullable": true,
          "editable": true
        },
        {
          "name": "road_class",
          "alias": "Classification",
          "dataType": "string",
          "maxLength": 50,
          "nullable": false,
          "editable": true
        },
        {
          "name": "speed_limit",
          "alias": "Speed Limit (mph)",
          "dataType": "integer",
          "nullable": true,
          "editable": true
        },
        {
          "name": "observed_at",
          "alias": "Observation Date",
          "dataType": "datetime",
          "nullable": true,
          "editable": true
        }
      ],
      "styles": {
        "defaultStyleId": "roads-primary-style",
        "styleIds": ["roads-primary-style", "roads-simple"]
      },
      "editing": {
        "capabilities": {
          "allowAdd": true,
          "allowUpdate": true,
          "allowDelete": false,
          "requireAuthentication": true,
          "allowedRoles": ["editor", "admin"]
        },
        "constraints": {
          "immutableFields": ["road_id", "created_at"],
          "requiredFields": ["name", "road_class"],
          "defaultValues": {
            "status": "active",
            "created_by": "$user"
          }
        }
      },
      "attachments": {
        "enabled": true,
        "storageProfileId": "s3-attachments",
        "maxSizeMiB": 10,
        "allowedContentTypes": ["image/jpeg", "image/png", "application/pdf"],
        "requireGlobalIds": false,
        "returnPresignedUrls": true,
        "exposeOgcLinks": false
      },
      "relationships": [
        {
          "id": 0,
          "role": "esriRelRoleOrigin",
          "cardinality": "esriRelCardinalityOneToMany",
          "relatedLayerId": "road-segments",
          "keyField": "road_id",
          "relatedKeyField": "parent_road_id",
          "composite": false,
          "returnGeometry": true,
          "semantics": "PrimaryKeyForeignKey"
        }
      ],
      "catalog": {
        "summary": "Primary road network features",
        "keywords": ["roads", "highways"],
        "themes": ["transportation"],
        "thumbnail": "/media/roads-primary.png",
        "ordering": 1
      }
    }
  ]
}
```

### Layer Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | string | **Yes** | - | Unique layer identifier within service |
| `serviceId` | string | **Yes** | - | Reference to parent service `id` |
| `title` | string | No | id value | Display title |
| `description` | string | No | null | Detailed description |
| `geometryType` | string | **Yes** | - | Geometry type: `Point`, `LineString`, `Polygon`, `MultiPoint`, `MultiLineString`, `MultiPolygon` |
| `idField` | string | **Yes** | - | Field name for feature ID |
| `displayField` | string | No | null | Primary display field name |
| `geometryField` | string | **Yes** | - | Field name for geometry column |
| `crs` | string[] | No | [] | Supported coordinate reference systems |
| `extent` | LayerExtentDefinition | No | null | Spatial and temporal extent |
| `keywords` | string[] | No | [] | Keywords for discovery |
| `links` | LinkDefinition[] | No | [] | Related links |
| `catalog` | CatalogEntryDefinition | No | {} | Catalog metadata |
| `query` | LayerQueryDefinition | No | {} | Query configuration |
| `storage` | LayerStorageDefinition | No | null | Storage mapping configuration |
| `fields` | FieldDefinition[] | No | [] | Field schema definitions |
| `itemType` | string | No | "feature" | Item type identifier |
| `defaultStyleId` | string | No | null | Default style reference |
| `styleIds` | string[] | No | [] | Available style references |
| `relationships` | LayerRelationshipDefinition[] | No | [] | Layer relationships |
| `minScale` | number | No | null | Minimum scale denominator (zoom out limit) |
| `maxScale` | number | No | null | Maximum scale denominator (zoom in limit) |
| `editing` | LayerEditingDefinition | No | disabled | Editing capabilities |
| `attachments` | LayerAttachmentDefinition | No | disabled | Attachment configuration |

### Geometry Types

Supported geometry types follow OGC Simple Features:

- **Point**: Single point features
- **LineString**: Line features
- **Polygon**: Polygon features
- **MultiPoint**: Multi-point features
- **MultiLineString**: Multi-line features
- **MultiPolygon**: Multi-polygon features

### Layer Extent

```json
{
  "extent": {
    "bbox": [
      [-122.5, 45.5, -122.4, 45.6],
      [-123.0, 45.0, -122.0, 46.0]
    ],
    "crs": "EPSG:4326",
    "temporal": {
      "interval": [
        ["2020-01-01T00:00:00Z", "2021-12-31T00:00:00Z"],
        ["2022-01-01T00:00:00Z", ".."]
      ],
      "trs": "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
    }
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bbox` | double[][] | No | Array of bounding boxes [minX, minY, maxX, maxY] |
| `crs` | string | No | CRS for bbox coordinates |
| `temporal.interval` | string[][] | No | Temporal intervals [start, end]. Use `".."` for open-ended |
| `temporal.trs` | string | No | Temporal reference system URI |

### Layer Query Configuration

```json
{
  "query": {
    "maxRecordCount": 1000,
    "supportedParameters": ["bbox", "limit", "offset", "datetime", "cql-filter"],
    "autoFilter": {
      "cql": "status = 'active'"
    }
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `maxRecordCount` | integer | No | Maximum features returned per request |
| `supportedParameters` | string[] | No | Supported query parameters |
| `autoFilter.cql` | string | No | CQL filter automatically applied to all queries |

### Layer Storage Configuration

The `storage` section maps layer fields to database schema.

```json
{
  "storage": {
    "table": "roads_primary",
    "geometryColumn": "geom",
    "primaryKey": "road_id",
    "temporalColumn": "observed_at",
    "srid": 4326,
    "crs": "EPSG:4326"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `table` | string | No | Database table or view name |
| `geometryColumn` | string | No | Geometry column name |
| `primaryKey` | string | No | Primary key column name |
| `temporalColumn` | string | No | Temporal/timestamp column name |
| `srid` | integer | No | Spatial Reference System ID |
| `crs` | string | No | CRS identifier (e.g., "EPSG:4326") |

**Note**: Provide at least one of `srid` or `crs` to ensure proper CRS handling. If both are specified, they should be consistent.

### Field Definitions

```json
{
  "fields": [
    {
      "name": "road_id",
      "alias": "Road Identifier",
      "dataType": "integer",
      "storageType": "bigint",
      "nullable": false,
      "editable": false,
      "maxLength": null,
      "precision": null,
      "scale": null
    },
    {
      "name": "name",
      "alias": "Road Name",
      "dataType": "string",
      "maxLength": 200,
      "nullable": true,
      "editable": true
    },
    {
      "name": "length_km",
      "alias": "Length (km)",
      "dataType": "double",
      "precision": 10,
      "scale": 2,
      "nullable": true,
      "editable": true
    }
  ]
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `name` | string | **Yes** | - | Field name (database column) |
| `alias` | string | No | null | User-friendly field name |
| `dataType` | string | No | null | Logical data type: `string`, `integer`, `double`, `date`, `datetime`, `boolean`, `guid` |
| `storageType` | string | No | null | Database storage type |
| `nullable` | boolean | No | true | Whether field allows null values |
| `editable` | boolean | No | true | Whether field is editable |
| `maxLength` | integer | No | null | Maximum string length |
| `precision` | integer | No | null | Numeric precision |
| `scale` | integer | No | null | Numeric scale (decimal places) |

### Layer Editing Configuration

```json
{
  "editing": {
    "capabilities": {
      "allowAdd": true,
      "allowUpdate": true,
      "allowDelete": false,
      "requireAuthentication": true,
      "allowedRoles": ["editor", "admin"]
    },
    "constraints": {
      "immutableFields": ["id", "created_at", "created_by"],
      "requiredFields": ["name", "status"],
      "defaultValues": {
        "status": "pending",
        "created_at": "$now",
        "created_by": "$user"
      }
    }
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `capabilities.allowAdd` | boolean | false | Allow feature creation |
| `capabilities.allowUpdate` | boolean | false | Allow feature updates |
| `capabilities.allowDelete` | boolean | false | Allow feature deletion |
| `capabilities.requireAuthentication` | boolean | true | Require authentication for edits |
| `capabilities.allowedRoles` | string[] | [] | Roles allowed to edit |
| `constraints.immutableFields` | string[] | [] | Fields that cannot be modified |
| `constraints.requiredFields` | string[] | [] | Fields required during creation |
| `constraints.defaultValues` | object | {} | Default field values. Supports `$user`, `$now` placeholders |

### Layer Attachments Configuration

```json
{
  "attachments": {
    "enabled": true,
    "storageProfileId": "s3-primary",
    "maxSizeMiB": 25,
    "allowedContentTypes": ["image/jpeg", "image/png", "application/pdf"],
    "disallowedContentTypes": ["application/x-msdownload"],
    "requireGlobalIds": false,
    "returnPresignedUrls": true,
    "exposeOgcLinks": false
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `enabled` | boolean | false | Enable attachment support |
| `storageProfileId` | string | null | Storage backend reference |
| `maxSizeMiB` | integer | null | Maximum attachment size in MiB |
| `allowedContentTypes` | string[] | [] | Allowed MIME types (whitelist) |
| `disallowedContentTypes` | string[] | [] | Disallowed MIME types (blacklist) |
| `requireGlobalIds` | boolean | false | Require GlobalID field |
| `returnPresignedUrls` | boolean | false | Return pre-signed URLs for direct access |
| `exposeOgcLinks` | boolean | false | Expose attachment links in OGC responses |

### Layer Relationships

```json
{
  "relationships": [
    {
      "id": 0,
      "role": "esriRelRoleOrigin",
      "cardinality": "esriRelCardinalityOneToMany",
      "relatedLayerId": "related-layer-id",
      "relatedTableId": "related-table-id",
      "keyField": "parent_id",
      "relatedKeyField": "foreign_key",
      "composite": false,
      "returnGeometry": true,
      "semantics": "PrimaryKeyForeignKey"
    }
  ]
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | integer | No | auto-increment | Relationship ID |
| `role` | string | No | "esriRelRoleOrigin" | Relationship role |
| `cardinality` | string | No | "esriRelCardinalityOneToMany" | Relationship cardinality |
| `relatedLayerId` | string | **Yes** | - | Related layer identifier |
| `relatedTableId` | string | No | null | Related table identifier (if different from layer) |
| `keyField` | string | **Yes** | - | Key field in origin layer |
| `relatedKeyField` | string | **Yes** | - | Key field in related layer |
| `composite` | boolean | No | null | Whether relationship is composite |
| `returnGeometry` | boolean | No | null | Return geometry in related features |
| `semantics` | string | No | "Unknown" | Relationship semantics: `PrimaryKeyForeignKey`, `Unknown` |

---

## Raster Datasets

Raster datasets define imagery and raster data sources, typically Cloud-Optimized GeoTIFFs (COG).

### Raster Dataset Definition

```json
{
  "rasterDatasets": [
    {
      "id": "roads-imagery",
      "title": "Roads Aerial Imagery",
      "description": "High-resolution aerial imagery of road network",
      "serviceId": "roads",
      "layerId": "roads-primary",
      "keywords": ["imagery", "aerial", "roads"],
      "crs": ["EPSG:4326", "EPSG:3857"],
      "source": {
        "type": "cog",
        "uri": "https://storage.example.com/imagery/roads.tif",
        "mediaType": "image/tiff; application=geotiff; profile=cloud-optimized",
        "credentialsId": "s3-credentials",
        "disableHttpRangeRequests": false
      },
      "extent": {
        "bbox": [[-122.6, 45.5, -122.3, 45.7]],
        "crs": "EPSG:4326",
        "temporal": {
          "interval": [
            ["2020-01-01T00:00:00Z", "2021-12-31T00:00:00Z"]
          ]
        }
      },
      "styles": {
        "defaultStyleId": "natural-color",
        "styleIds": ["natural-color", "infrared"]
      },
      "cache": {
        "enabled": true,
        "preseed": false,
        "zoomLevels": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
      },
      "catalog": {
        "summary": "Aerial imagery for road network visualization",
        "keywords": ["imagery", "cog"],
        "thumbnail": "/media/imagery-thumb.png"
      }
    }
  ]
}
```

### Raster Dataset Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | string | **Yes** | - | Unique raster dataset identifier |
| `title` | string | No | id value | Display title |
| `description` | string | No | null | Detailed description |
| `serviceId` | string | No | null | Parent service reference |
| `layerId` | string | No | null | Associated layer reference |
| `keywords` | string[] | No | [] | Keywords for discovery |
| `crs` | string[] | No | [] | Supported CRS |
| `source` | RasterSourceDefinition | **Yes** | - | Raster source configuration |
| `extent` | LayerExtentDefinition | No | null | Spatial and temporal extent |
| `styles` | RasterStyleDefinition | No | {} | Style references |
| `cache` | RasterCacheDefinition | No | {} | Caching configuration |
| `catalog` | CatalogEntryDefinition | No | {} | Catalog metadata |

### Raster Source Configuration

```json
{
  "source": {
    "type": "cog",
    "uri": "s3://bucket/path/to/image.tif",
    "mediaType": "image/tiff; application=geotiff; profile=cloud-optimized",
    "credentialsId": "aws-credentials",
    "disableHttpRangeRequests": false
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `type` | string | **Yes** | Source type: `cog`, `geotiff`, `cloud-optimized-geotiff`, `vector` |
| `uri` | string | **Yes** | File path or URL (supports `file://`, `https://`, `s3://`) |
| `mediaType` | string | No | MIME type with optional profile |
| `credentialsId` | string | No | Credentials reference for authenticated access |
| `disableHttpRangeRequests` | boolean | No | Disable HTTP range requests for non-COG sources |

### Raster Cache Configuration

```json
{
  "cache": {
    "enabled": true,
    "preseed": false,
    "zoomLevels": [0, 1, 2, 3, 4, 5, 6, 7, 8]
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `enabled` | boolean | true | Enable tile caching |
| `preseed` | boolean | false | Pre-generate cache tiles on startup |
| `zoomLevels` | integer[] | [] | Zoom levels to cache (0-22) |

---

## Styles

Styles define visual rendering rules for layers and raster datasets.

### Style Definition

```json
{
  "styles": [
    {
      "id": "roads-primary-style",
      "title": "Primary Roads Style",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "line",
      "simple": {
        "label": "Primary Road",
        "description": "Standard rendering for primary roads",
        "symbolType": "line",
        "strokeColor": "#FF8800FF",
        "strokeWidth": 3.0,
        "strokeStyle": "solid",
        "opacity": 1.0
      }
    },
    {
      "id": "road-classification",
      "title": "Road Classification",
      "renderer": "uniqueValue",
      "format": "mvp-style",
      "geometryType": "line",
      "uniqueValue": {
        "field": "road_class",
        "defaultSymbol": {
          "symbolType": "line",
          "strokeColor": "#CCCCCCFF",
          "strokeWidth": 1.0
        },
        "classes": [
          {
            "value": "highway",
            "symbol": {
              "symbolType": "line",
              "strokeColor": "#FF0000FF",
              "strokeWidth": 4.0
            }
          },
          {
            "value": "primary",
            "symbol": {
              "symbolType": "line",
              "strokeColor": "#FF8800FF",
              "strokeWidth": 3.0
            }
          },
          {
            "value": "secondary",
            "symbol": {
              "symbolType": "line",
              "strokeColor": "#FFFF00FF",
              "strokeWidth": 2.0
            }
          }
        ]
      }
    },
    {
      "id": "point-symbols",
      "title": "Point Symbols",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "point",
      "simple": {
        "symbolType": "shape",
        "fillColor": "#0088FFFF",
        "strokeColor": "#FFFFFFFF",
        "strokeWidth": 1.0,
        "size": 8.0,
        "opacity": 0.8
      }
    },
    {
      "id": "polygon-fill",
      "title": "Polygon Fill",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "polygon",
      "simple": {
        "symbolType": "polygon",
        "fillColor": "#00FF0080",
        "strokeColor": "#006600FF",
        "strokeWidth": 2.0
      }
    }
  ]
}
```

### Style Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | string | **Yes** | - | Unique style identifier |
| `title` | string | No | null | Display title |
| `renderer` | string | No | "simple" | Renderer type: `simple`, `uniqueValue`, `unique-value` |
| `format` | string | No | "legacy" | Style format identifier |
| `geometryType` | string | No | "polygon" | Geometry type: `point`, `line`, `polyline`, `polygon`, `raster` |
| `rules` | StyleRuleDefinition[] | No | [] | Rule-based styling (advanced) |
| `simple` | SimpleStyleDefinition | No | null | Simple renderer configuration |
| `uniqueValue` | UniqueValueStyleDefinition | No | null | Unique value renderer configuration |

### Simple Style Definition

Used for single-symbol rendering.

```json
{
  "simple": {
    "label": "Feature Label",
    "description": "Feature description",
    "symbolType": "line",
    "fillColor": "#00FF00FF",
    "strokeColor": "#000000FF",
    "strokeWidth": 2.0,
    "strokeStyle": "solid",
    "iconHref": "/symbols/icon.png",
    "size": 12.0,
    "opacity": 0.8
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `label` | string | Symbol label |
| `description` | string | Symbol description |
| `symbolType` | string | Symbol type: `shape`, `line`, `polygon` |
| `fillColor` | string | Fill color (hex RGBA: `#RRGGBBAA`) |
| `strokeColor` | string | Stroke/outline color (hex RGBA) |
| `strokeWidth` | number | Stroke width in pixels |
| `strokeStyle` | string | Stroke style: `solid`, `dash`, `dot` |
| `iconHref` | string | Icon/marker image URL |
| `size` | number | Symbol size in pixels |
| `opacity` | number | Opacity (0.0-1.0) |

### Unique Value Style Definition

Used for categorical/classified rendering.

```json
{
  "uniqueValue": {
    "field": "classification_field",
    "defaultSymbol": {
      "symbolType": "polygon",
      "fillColor": "#CCCCCCFF",
      "strokeColor": "#000000FF",
      "strokeWidth": 1.0
    },
    "classes": [
      {
        "value": "class1",
        "symbol": {
          "symbolType": "polygon",
          "fillColor": "#FF0000FF",
          "strokeColor": "#000000FF",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "class2",
        "symbol": {
          "symbolType": "polygon",
          "fillColor": "#00FF00FF",
          "strokeColor": "#000000FF",
          "strokeWidth": 1.0
        }
      }
    ]
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `field` | string | **Yes** | Field name for classification |
| `defaultSymbol` | SimpleStyleDefinition | No | Default symbol for unmatched values |
| `classes` | UniqueValueStyleClassDefinition[] | **Yes** | Classification classes |

**UniqueValueStyleClassDefinition**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `value` | string | **Yes** | Field value to match |
| `symbol` | SimpleStyleDefinition | **Yes** | Symbol for this class |

### Advanced Rule-Based Styling

For complex styling scenarios, use the `rules` array:

```json
{
  "styles": [
    {
      "id": "advanced-roads",
      "title": "Advanced Road Styling",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "line",
      "rules": [
        {
          "id": "highways",
          "label": "Highways",
          "filter": {
            "field": "road_class",
            "value": "highway"
          },
          "minScale": 1000000,
          "maxScale": 0,
          "default": false,
          "symbolizer": {
            "symbolType": "line",
            "strokeColor": "#FF0000FF",
            "strokeWidth": 4.0
          }
        },
        {
          "id": "other",
          "label": "Other Roads",
          "default": true,
          "symbolizer": {
            "symbolType": "line",
            "strokeColor": "#888888FF",
            "strokeWidth": 1.5
          }
        }
      ]
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | **Yes** | Rule identifier |
| `label` | string | No | Rule label |
| `filter.field` | string | No | Field name for filtering |
| `filter.value` | string | No | Value to match |
| `minScale` | number | No | Minimum scale denominator (zoom out limit) |
| `maxScale` | number | No | Maximum scale denominator (zoom in limit) |
| `default` | boolean | No | Whether this is the default rule |
| `symbolizer` | SimpleStyleDefinition | **Yes** | Symbol definition |

---

## Common Definitions

### Link Definition

```json
{
  "href": "https://example.com/resource",
  "rel": "alternate",
  "type": "text/html",
  "title": "Human-readable title"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `href` | string | **Yes** | URL or URI |
| `rel` | string | No | Link relationship type |
| `type` | string | No | MIME type |
| `title` | string | No | Link title |

### Catalog Entry Definition

Used in services, layers, and raster datasets for catalog metadata.

```json
{
  "catalog": {
    "summary": "Brief summary",
    "keywords": ["keyword1", "keyword2"],
    "themes": ["theme1", "theme2"],
    "thumbnail": "/media/thumbnail.png",
    "ordering": 10,
    "spatialExtent": {
      "bbox": [[-180, -90, 180, 90]],
      "crs": "EPSG:4326"
    },
    "temporalExtent": {
      "start": "2020-01-01T00:00:00Z",
      "end": "2025-12-31T23:59:59Z"
    },
    "contacts": [
      {
        "name": "Contact Name",
        "email": "contact@example.com",
        "organization": "Organization",
        "phone": "+1-555-0100",
        "url": "https://example.com",
        "role": "pointOfContact"
      }
    ],
    "links": []
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `summary` | string | Brief summary text |
| `keywords` | string[] | Keywords for discovery |
| `themes` | string[] | Theme classifications |
| `thumbnail` | string | Thumbnail image URL |
| `ordering` | integer | Sort order in catalog |
| `spatialExtent` | CatalogSpatialExtentDefinition | Spatial extent |
| `temporalExtent` | CatalogTemporalExtentDefinition | Temporal extent |
| `contacts` | CatalogContactDefinition[] | Contact information |
| `links` | LinkDefinition[] | Related links |

---

## Complete Working Example

Here's a complete metadata file incorporating all major components:

```json
{
  "server": {
    "allowedHosts": ["*.example.com"],
    "cors": {
      "enabled": true,
      "allowedOrigins": ["https://app.example.com"],
      "allowedMethods": ["GET", "POST", "OPTIONS"],
      "allowedHeaders": ["*"],
      "allowCredentials": false,
      "maxAgeSeconds": 3600
    }
  },
  "catalog": {
    "id": "example-catalog",
    "title": "Example Spatial Data Catalog",
    "description": "Comprehensive spatial data services",
    "version": "1.0.0",
    "publisher": "Example Organization",
    "keywords": ["geospatial", "ogc", "features"],
    "contact": {
      "name": "GIS Team",
      "email": "gis@example.com",
      "organization": "Example Org"
    }
  },
  "folders": [
    {
      "id": "transportation",
      "title": "Transportation",
      "order": 10
    }
  ],
  "dataSources": [
    {
      "id": "postgis-main",
      "provider": "postgis",
      "connectionString": "Host=localhost;Database=gis;Username=postgres;Password=secret"
    }
  ],
  "services": [
    {
      "id": "roads",
      "title": "Road Network",
      "folderId": "transportation",
      "serviceType": "feature",
      "dataSourceId": "postgis-main",
      "enabled": true,
      "ogc": {
        "collectionsEnabled": true,
        "itemLimit": 1000,
        "defaultCrs": "EPSG:4326",
        "additionalCrs": ["EPSG:3857"]
      }
    }
  ],
  "layers": [
    {
      "id": "primary-roads",
      "serviceId": "roads",
      "title": "Primary Roads",
      "geometryType": "LineString",
      "idField": "id",
      "displayField": "name",
      "geometryField": "geom",
      "crs": ["EPSG:4326", "EPSG:3857"],
      "storage": {
        "table": "roads",
        "geometryColumn": "geom",
        "primaryKey": "id",
        "srid": 4326
      },
      "fields": [
        {
          "name": "id",
          "alias": "ID",
          "dataType": "integer",
          "nullable": false,
          "editable": false
        },
        {
          "name": "name",
          "alias": "Road Name",
          "dataType": "string",
          "maxLength": 100,
          "nullable": true,
          "editable": true
        }
      ],
      "query": {
        "maxRecordCount": 1000,
        "supportedParameters": ["bbox", "limit", "offset"]
      }
    }
  ],
  "styles": [
    {
      "id": "road-style",
      "title": "Road Style",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "line",
      "simple": {
        "symbolType": "line",
        "strokeColor": "#FF8800FF",
        "strokeWidth": 2.0
      }
    }
  ],
  "rasterDatasets": []
}
```

---

## Validation and Best Practices

### Schema Validation

Honua performs comprehensive validation when loading metadata:

1. **Required Fields**: All required fields must be present
2. **ID Uniqueness**: All IDs must be unique within their scope
3. **Reference Integrity**: All references (serviceId, folderId, dataSourceId, styleId) must point to existing entities
4. **Geometry Validation**: Geometry types must be valid OGC Simple Feature types
5. **CRS Consistency**: When both `srid` and `crs` are specified, they must be consistent
6. **CORS Security**: Cannot allow credentials with wildcard origins
7. **Scale Denominators**: minScale must be <= maxScale

### Best Practices

1. **Always specify CRS information**: Include `srid` or `crs` in storage configuration to avoid CRS ambiguity
2. **Use meaningful IDs**: Use descriptive, kebab-case identifiers (e.g., `roads-primary` not `r1`)
3. **Provide metadata**: Populate title, description, and keywords for better discovery
4. **Define extents**: Include spatial and temporal extents for better client rendering
5. **Secure connections**: Never commit connection strings with credentials to source control
6. **Use environment variables**: Reference secrets via environment variables in production
7. **Test styles**: Verify style definitions render correctly across different geometry types
8. **Document relationships**: Use clear keyField names that indicate relationship semantics
9. **Optimize queries**: Set appropriate maxRecordCount limits based on data volume
10. **Enable CORS carefully**: Only allow specific origins in production environments

---

## Troubleshooting

### Common Issues

**Issue: "Service references unknown folder"**
- **Cause**: Service `folderId` does not match any folder `id`
- **Solution**: Ensure folder is defined in `folders` array before referencing

**Issue: "Layer references unknown service"**
- **Cause**: Layer `serviceId` does not match any service `id`
- **Solution**: Define service before referencing in layer

**Issue: "Duplicate layer id"**
- **Cause**: Multiple layers have the same `id`
- **Solution**: Ensure all layer IDs are unique across all services

**Issue: "Layer missing geometryType"**
- **Cause**: Required field `geometryType` is not specified
- **Solution**: Add valid `geometryType` (Point, LineString, Polygon, etc.)

**Issue: "Style validation failed"**
- **Cause**: Style renderer doesn't match style definition (e.g., `renderer: "uniqueValue"` but no `uniqueValue` object)
- **Solution**: Ensure renderer matches provided style configuration

**Issue: "CORS configuration invalid"**
- **Cause**: `allowCredentials: true` with `allowedOrigins: ["*"]`
- **Solution**: Specify explicit origins when allowing credentials

**Issue: "CRS warning during validation"**
- **Cause**: Layer storage missing both `srid` and `crs`
- **Solution**: Add at least one CRS identifier to storage configuration

**Issue: "Raster source not found"**
- **Cause**: Raster source URI is invalid or inaccessible
- **Solution**: Verify file path or URL is correct and accessible from server

---

## Related Topics

- **OGC API Features**: See OGC API implementation documentation
- **Geoservices REST a.k.a. Esri REST Services**: See Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API compatibility guide
- **Data Source Configuration**: See database provider setup guides
- **Styling and Symbology**: See styling documentation for advanced rendering
- **Security and Authentication**: See authentication and authorization guide

---

## Metadata File Location

By default, Honua loads metadata from:
- Development: `./metadata/metadata.json`
- Production: Configured via `HONUA_METADATA_PATH` environment variable

The metadata provider supports file watching for hot-reload during development.

---

## Schema Evolution

The Honua metadata schema follows semantic versioning. Breaking changes will be announced with migration guides. The current schema version is embedded in the catalog `version` field.

For questions or issues with metadata configuration, consult the Honua documentation or open an issue on the GitHub repository.
