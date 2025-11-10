# API Documentation

Honua Server provides comprehensive API documentation via Swagger/OpenAPI, making it easy to explore and test all available endpoints.

## Accessing Swagger UI

Once the server is running, navigate to:

```
http://localhost:5000/swagger
```

This provides an interactive web interface to:
- Browse all API endpoints
- View request/response schemas
- Test endpoints directly from the browser
- Generate client SDKs

## OpenAPI Specification

The raw OpenAPI specification (JSON format) is available at:

```
http://localhost:5000/swagger/v1/swagger.json
```

You can use this spec to:
- Generate client libraries in any language
- Import into Postman or Insomnia
- Generate server stubs
- Validate API contracts

## API Categories

### OGC Protocols (Standard Compliance)

Honua implements the following OGC standards. These endpoints follow OGC specifications and may not appear in Swagger (they use query parameters heavily):

#### WMS (Web Map Service)
```
GET /wms?SERVICE=WMS&REQUEST=GetCapabilities
GET /wms?SERVICE=WMS&REQUEST=GetMap&LAYERS=...&BBOX=...
```

#### WFS (Web Feature Service)
```
GET /wfs?SERVICE=WFS&REQUEST=GetCapabilities
GET /wfs?SERVICE=WFS&REQUEST=GetFeature&TYPENAME=...
POST /wfs - Transaction operations (Insert, Update, Delete)
```

#### WMTS (Web Map Tile Service)
```
GET /wmts?SERVICE=WMTS&REQUEST=GetCapabilities
GET /wmts/{layer}/{TileMatrixSet}/{TileMatrix}/{TileRow}/{TileCol}
```

#### WCS (Web Coverage Service)
```
GET /wcs?SERVICE=WCS&REQUEST=GetCapabilities
GET /wcs?SERVICE=WCS&REQUEST=DescribeCoverage&COVERAGEID=...
GET /wcs?SERVICE=WCS&REQUEST=GetCoverage&COVERAGEID=...
```

#### CSW (Catalog Service for the Web)
```
GET /csw?SERVICE=CSW&REQUEST=GetCapabilities
GET /csw?SERVICE=CSW&REQUEST=GetRecords
```

#### OGC API - Features
```
GET /ogc - Landing page
GET /ogc/collections - List collections
GET /ogc/collections/{collectionId} - Collection metadata
GET /ogc/collections/{collectionId}/items - Query features
```

### Admin Endpoints (Documented in Swagger)

These are Honua-specific administration endpoints, fully documented in Swagger:

#### Runtime Configuration
```
GET /admin/config/status - Overall configuration status
GET /admin/config/services - List all global service states
PATCH /admin/config/services/{protocol} - Toggle global protocol
GET /admin/config/services/{serviceId} - Get service-level API configuration
PATCH /admin/config/services/{serviceId}/{protocol} - Toggle service-level protocol
```

#### Logging Configuration
```
GET /admin/logging/levels - Get available log levels
GET /admin/logging/categories - Get current log level configuration
PATCH /admin/logging/categories/{category} - Set log level for a category
DELETE /admin/logging/categories/{category} - Remove runtime override
POST /admin/logging/test - Write test log messages
```

#### Metadata Administration
```
GET /admin/metadata/reload - Reload metadata from disk
POST /admin/metadata/validate - Validate metadata against schema
GET /admin/metadata/snapshots - List metadata snapshots
POST /admin/metadata/snapshots - Create metadata snapshot
```

#### Data Ingestion
```
POST /admin/ingestion/geopackage - Ingest GeoPackage file
POST /admin/ingestion/shapefile - Ingest Shapefile
GET /admin/ingestion/status - Get ingestion job status
```

#### Migration
```
POST /admin/migration/arcgis - Migrate from ArcGIS Server
GET /admin/migration/status/{jobId} - Get migration job status
```

#### Raster Tile Cache
```
GET /admin/raster/cache/status - Get cache status
DELETE /admin/raster/cache/{datasetId} - Clear cache for dataset
POST /admin/raster/cache/preseed - Preseed cache for dataset
GET /admin/raster/cache/statistics - Get cache statistics
GET /admin/raster/cache/quota - Get disk quota information
```

#### Raster Analytics
```
GET /admin/raster/analytics/{datasetId}/statistics - Get raster statistics
GET /admin/raster/analytics/{datasetId}/histogram - Get histogram
POST /admin/raster/analytics/{datasetId}/sample - Sample pixel values
```

#### Raster Mosaic
```
POST /admin/raster/mosaic - Create on-the-fly mosaic
```

### OData v4 API

```
GET /odata/$metadata - OData metadata document
GET /odata/{collection} - Query collection with OData syntax
GET /odata/{collection}({id}) - Get single entity
POST /odata/{collection} - Create entity (requires DataPublisher role)
PATCH /odata/{collection}({id}) - Update entity (requires DataPublisher role)
DELETE /odata/{collection}({id}) - Delete entity (requires DataPublisher role)
```

**OData Query Examples:**
```
GET /odata/parcels?$filter=area gt 1000
GET /odata/parcels?$filter=geo.intersects(geometry, geography'POINT(-122.3 47.6)')
GET /odata/parcels?$top=10&$skip=20&$orderby=area desc
GET /odata/parcels?$select=id,owner,area&$expand=attachments
```

### STAC API

```
GET /stac - STAC landing page
GET /stac/collections - List collections
GET /stac/collections/{collectionId} - Get collection
GET /stac/collections/{collectionId}/items - Query items
GET /stac/collections/{collectionId}/items/{itemId} - Get item
POST /stac/collections/{collectionId}/items - Create item (requires DataPublisher role)
```

### Carto API

```
GET /carto/api/v1/sql?q=SELECT * FROM dataset
POST /carto/api/v1/sql - Execute SQL query
```

### OpenRosa/ODK

```
GET /openrosa/formList - List available forms
GET /openrosa/forms/{formId}/form.xml - Get XForm definition
POST /openrosa/submission - Submit form data
```

## Authentication

Most admin endpoints require authentication. Use the `/auth/login` endpoint to obtain a JWT token:

```bash
curl -X POST http://localhost:5000/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "your-password"}'
```

Response:
```json
{
  "token": "eyJhbGc...long-jwt-token",
  "expiresAt": "2025-10-08T12:00:00Z"
}
```

Use the token in subsequent requests:

```bash
curl http://localhost:5000/admin/config/status \
  -H "Authorization: Bearer eyJhbGc...long-jwt-token"
```

In Swagger UI:
1. Click the "Authorize" button (lock icon)
2. Enter your JWT token in the "Bearer" field
3. Click "Authorize"
4. All subsequent requests will include the authorization header

## Roles

Honua uses role-based access control (RBAC):

| Role | Permissions |
|------|-------------|
| **Viewer** | Read-only access to data and metadata |
| **DataPublisher** | Read/write access to data, metadata administration |
| **Administrator** | Full access including runtime configuration, logging |

## Generating Client SDKs

Use the OpenAPI spec to generate client libraries:

### Using OpenAPI Generator

```bash
# Download the spec
curl http://localhost:5000/swagger/v1/swagger.json -o honua-api.json

# Generate TypeScript client
npx @openapitools/openapi-generator-cli generate \
  -i honua-api.json \
  -g typescript-axios \
  -o ./clients/typescript

# Generate Python client
npx @openapitools/openapi-generator-cli generate \
  -i honua-api.json \
  -g python \
  -o ./clients/python

# Generate C# client
npx @openapitools/openapi-generator-cli generate \
  -i honua-api.json \
  -g csharp-netcore \
  -o ./clients/csharp
```

### Using NSwag (C# only)

```bash
dotnet tool install -g NSwag.ConsoleCore

nswag openapi2csclient \
  /input:http://localhost:5000/swagger/v1/swagger.json \
  /output:HonuaClient.cs \
  /namespace:Honua.Client
```

## Testing with Postman

1. Open Postman
2. Click Import
3. Enter URL: `http://localhost:5000/swagger/v1/swagger.json`
4. Postman will import all endpoints as a collection
5. Configure authentication in the collection settings

## Testing with cURL

### Get Server Status
```bash
curl http://localhost:5000/admin/config/status \
  -H "Authorization: Bearer $TOKEN"
```

### Reload Metadata
```bash
curl -X POST http://localhost:5000/admin/metadata/reload \
  -H "Authorization: Bearer $TOKEN"
```

### Query OData
```bash
curl "http://localhost:5000/odata/parcels?\$filter=area%20gt%201000&\$top=10"
```

### Set Log Level
```bash
curl -X PATCH http://localhost:5000/admin/logging/categories/Honua.Server.Core.Data \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"level": "Debug"}'
```

## API Versioning

Currently, Honua uses a single API version (v1). Future versions may introduce:
- URL versioning: `/api/v2/...`
- Header versioning: `X-API-Version: 2`
- Content negotiation: `Accept: application/vnd.honua.v2+json`

## Rate Limiting

Admin endpoints have rate limiting enabled:
- 100 requests per minute per IP address (default)
- Configure in `appsettings.json` under `RateLimiting`

When rate limited, you'll receive:
- HTTP 429 Too Many Requests
- `Retry-After` header indicating when to retry

## CORS Policy

CORS is configured per-service in metadata. To enable cross-origin requests:

1. Edit metadata.json
2. Set `cors.allowedOrigins` for your service
3. Reload metadata: `POST /admin/metadata/reload`

## Next Steps

- [Runtime Configuration API](./RUNTIME_CONFIGURATION.md)
- [Tracing Documentation](./TRACING.md)
- [Deployment Guide](./DEPLOYMENT.md)
