---
tags: [architecture, system-design, components, layers, tech-stack, deployment]
category: architecture
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# Honua Server Architecture Overview

Complete guide to Honua's architecture, technology stack, and system design principles.

## Table of Contents
- [System Overview](#system-overview)
- [Layered Architecture](#layered-architecture)
- [Core Components](#core-components)
- [Technology Stack](#technology-stack)
- [Data Flow](#data-flow)
- [Deployment Models](#deployment-models)
- [Related Documentation](#related-documentation)

## System Overview

Honua is a cloud-native geospatial server built on ASP.NET Core 9.0 that implements multiple OGC standards, Geoservices REST a.k.a. Esri REST API, and STAC specifications. It's designed for high performance, scalability, and modern cloud deployments.

### Key Characteristics

- **Cloud-Native**: Built for containerized deployments with Kubernetes support
- **Standards-Based**: OGC API Features, WMS, WFS, WMTS, WCS, CSW, Geoservices REST a.k.a. Esri REST API, STAC
- **High Performance**: Native COG/Zarr readers, tile caching, compression
- **Observable**: OpenTelemetry metrics, traces, and structured logging
- **Flexible**: Multiple database backends (PostGIS, SQLite, SQL Server)

### Design Principles

1. **Separation of Concerns**: Clear boundaries between presentation, business logic, and data
2. **Dependency Injection**: All services use DI for testability and flexibility
3. **Asynchronous First**: Non-blocking I/O throughout the stack
4. **Observability by Default**: Metrics, traces, and logs built-in
5. **Configuration as Code**: GitOps-friendly YAML metadata

## Layered Architecture

Honua follows a three-layer architecture pattern:

```
┌─────────────────────────────────────────────────────────┐
│           Honua.Server.Host (Presentation)              │
│  - ASP.NET Core Web API                                 │
│  - OGC/GeoServices REST/STAC Handlers                              │
│  - Middleware (Auth, Logging, Rate Limiting)           │
│  - Controllers (OData, Admin API)                      │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│       Honua.Server.Core (Business Logic)                │
│  - Feature Query/Edit Services                          │
│  - Raster Processing (COG, Zarr)                       │
│  - Metadata Registry                                    │
│  - Export/Import Services                              │
│  - Authentication/Authorization                         │
│  - Observability Services                              │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│              Data Layer (Infrastructure)                │
│  - PostGIS (spatial queries)                           │
│  - SQLite (embedded deployments)                       │
│  - Cloud Storage (S3, Azure Blob, GCS)                 │
│  - File System (local development)                     │
└─────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

#### Honua.Server.Host (Presentation Layer)
- HTTP request handling
- Protocol implementations (OGC, GeoServices REST, STAC)
- Authentication middleware
- Input validation
- Response formatting

**Key Files:**
- `/src/Honua.Server.Host/Program.cs` - Application entry point
- `/src/Honua.Server.Host/Hosting/HonuaHostConfigurationExtensions.cs` - Service configuration
- `/src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs` - OGC API Features implementation
- `/src/Honua.Server.Host/Middleware/` - Custom middleware components

#### Honua.Server.Core (Business Logic Layer)
- Feature querying and editing
- Spatial operations
- Raster processing (tiles, analytics)
- Export format generation
- Metadata management
- Business rule enforcement

**Key Directories:**
- `/src/Honua.Server.Core/Data/` - Data access interfaces
- `/src/Honua.Server.Core/Query/` - Query parsing and execution
- `/src/Honua.Server.Core/Raster/` - COG and Zarr processing
- `/src/Honua.Server.Core/Export/` - Format exporters
- `/src/Honua.Server.Core/Metadata/` - Metadata registry

#### Data Layer
- Database operations
- Cloud storage integration
- Caching
- Connection pooling

## Core Components

### 1. Metadata Registry

Central registry for all service definitions, layers, and configurations.

```csharp
// Location: Honua.Server.Core/Metadata/IMetadataRegistry.cs
public interface IMetadataRegistry
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);
    MetadataSnapshot Snapshot { get; }
}
```

**Features:**
- Hot-reload capability (file watching)
- Version-controlled YAML configuration
- Service and layer metadata
- Style definitions
- Data source connections

**Example Usage:**
```csharp
await metadataRegistry.EnsureInitializedAsync(cancellationToken);
var snapshot = metadataRegistry.Snapshot;
var service = snapshot.Services.FirstOrDefault(s => s.Id == "my-service");
```

### 2. Feature Context Resolver

Resolves service/layer combinations to queryable contexts.

```csharp
// Resolves collection ID to service + layer context
var resolution = await resolver.ResolveCollectionAsync(
    collectionId,
    cancellationToken
);

if (resolution.IsSuccess)
{
    var service = resolution.Value.Service;
    var layer = resolution.Value.Layer;
}
```

### 3. Feature Repository

Executes spatial queries against configured data sources.

```csharp
// Query features with filtering and paging
await foreach (var feature in repository.QueryAsync(
    serviceId,
    layerId,
    query,
    cancellationToken))
{
    // Process feature
}
```

### 4. Raster Processing

**COG Reader (Pure .NET):**
- HTTP range request optimization
- Multi-resolution tile reading
- No GDAL dependency for reading

**Zarr Reader:**
- Multi-dimensional array support
- Chunk-based access
- Time-series data handling

```csharp
// Location: Honua.Server.Core/Raster/
- Readers/ICogReader.cs
- Readers/IZarrReader.cs
- Cache/RasterStorageRouter.cs
```

### 5. Export Services

Generate output in multiple formats:

- **Vector**: GeoJSON, GeoPackage, Shapefile, FlatGeobuf, GeoArrow, CSV, KML
- **Raster**: PNG, JPEG, WebP, MVT

```csharp
// Example: GeoPackage export
var result = await geoPackageExporter.ExportAsync(
    layer,
    query,
    crs,
    featureStream,
    cancellationToken
);
```

### 6. Authentication System

Supports multiple authentication modes:

- **Local**: Username/password with session management
- **JWT**: Bearer token authentication
- **API Key**: Header-based authentication
- **QuickStart**: Development-only bypass mode

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true
    }
  }
}
```

### 7. Observability Stack

**Metrics:**
- Prometheus-compatible endpoint (`/metrics`)
- Custom metrics for API calls, features returned, cache hits
- ASP.NET Core built-in metrics

**Tracing:**
- OpenTelemetry distributed tracing
- OTLP exporter support (Jaeger, Tempo)
- Activity correlation across service boundaries

**Logging:**
- Structured JSON logging
- Configurable log levels
- Request/response logging middleware

## Technology Stack

### Core Framework
- **.NET 9.0**: Latest LTS runtime
- **ASP.NET Core**: Web framework
- **C# 12**: Language features (nullable reference types, records)

### Key Libraries

**Spatial:**
- `NetTopologySuite` (2.6.0) - Geometry operations
- `NetTopologySuite.IO.GeoJSON` (4.0.0) - GeoJSON serialization

**Data Access:**
- `Npgsql` - PostgreSQL/PostGIS driver
- `Microsoft.Data.Sqlite` - SQLite support
- `Dapper` - Lightweight ORM

**Raster Processing:**
- Pure .NET COG reader (no GDAL for reading)
- `LibTIFF` - TIFF file handling
- Custom Zarr implementation

**API:**
- `Microsoft.AspNetCore.OData` (9.4.0) - OData support
- `Swashbuckle.AspNetCore` (7.2.0) - OpenAPI/Swagger

**Observability:**
- `OpenTelemetry.Extensions.Hosting` (1.12.0)
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` (1.12.0)
- `Serilog.AspNetCore` (9.0.0) - Structured logging

**Performance:**
- Response compression (Gzip, Brotli)
- In-memory caching
- Redis support for distributed caching

### Database Support

| Database | Features | Best For |
|----------|----------|----------|
| **PostGIS** | Full spatial, best performance | Production deployments |
| **SQLite** | Embedded, SpatiaLite extension | Single-user, development |
| **SQL Server** | Enterprise integration | Microsoft shops |

### Cloud Storage

- **Azure Blob Storage**: Native integration
- **AWS S3**: S3-compatible storage
- **File System**: Local development and on-premises

## Data Flow

### Feature Query Flow

```
HTTP Request
    ↓
[Middleware Pipeline]
    ↓ (Rate Limiting, Auth, Logging)
OGC Handler
    ↓ (Parse query parameters)
Feature Context Resolver
    ↓ (Resolve service + layer)
Feature Repository
    ↓ (Execute SQL query)
PostgreSQL/PostGIS
    ↓ (Return result set)
Feature Serializer
    ↓ (Format as GeoJSON/etc)
HTTP Response
```

### Raster Tile Flow

```
HTTP Request
    ↓
[Tile Cache Check]
    ↓ (Cache miss)
Raster Source Provider
    ↓
COG/Zarr Reader
    ↓ (HTTP range request)
Cloud Storage
    ↓ (Chunk data)
Tile Renderer
    ↓ (Apply styling)
[Cache Write]
    ↓
HTTP Response (PNG/JPEG)
```

## Deployment Models

### 1. Docker Compose (Development/Small Deployments)

```bash
# Minimal setup
docker compose -f docker/docker-compose.yml up

# Full observability stack
docker compose -f docker/docker-compose.full.yml up
```

**Components:**
- Honua Server
- PostgreSQL/PostGIS
- Prometheus (metrics)
- Grafana (dashboards)
- Jaeger (tracing)

### 2. Kubernetes (Production)

**Scaling Strategy:**
- Horizontal Pod Autoscaling based on CPU/memory
- StatefulSet for PostgreSQL
- Deployment for Honua servers
- Service mesh ready (Istio, Linkerd)

**Storage:**
- PersistentVolumeClaims for metadata
- Cloud storage for rasters
- Redis for distributed caching

### 3. Cloud Platforms

**Azure:**
- Azure App Service
- Azure Database for PostgreSQL
- Azure Blob Storage
- Application Insights

**AWS:**
- ECS/EKS
- RDS PostgreSQL
- S3
- CloudWatch

**GCP:**
- Google Kubernetes Engine
- Cloud SQL
- Cloud Storage
- Cloud Monitoring

## Performance Characteristics

### Throughput
- **OGC API Features**: 1000+ req/s (simple queries)
- **Raster Tiles**: 500+ tiles/s (cached)
- **Export Operations**: Depends on dataset size

### Latency (p95)
- **Feature Queries**: <50ms (indexed)
- **Cached Tiles**: <10ms
- **Cold Tiles**: <200ms (COG HTTP range)

### Resource Usage
- **Memory**: 512MB minimum, 2GB recommended
- **CPU**: 2 cores minimum, 4+ recommended
- **Disk**: Primarily for cache (configurable)

## Security Architecture

### Defense in Depth

1. **Network Layer**: HTTPS enforcement, rate limiting
2. **Authentication**: JWT, Local, API Keys
3. **Authorization**: Role-based access control (RBAC)
4. **Input Validation**: Query parameter sanitization
5. **Output Encoding**: Prevent XSS in HTML responses

### Security Headers

Automatically applied:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Content-Security-Policy`
- `Strict-Transport-Security` (HTTPS)

## Related Documentation

- [Configuration Reference](02-01-configuration-reference.md) - Detailed config options
- [OGC API Features Guide](03-01-ogc-api-features.md) - API usage examples
- [Docker Deployment](04-01-docker-deployment.md) - Container deployment
- [Common Issues](05-02-common-issues.md) - Troubleshooting guide

## Keywords for Search

architecture, system design, components, layers, technology stack, ASP.NET Core, .NET 9, PostGIS, SQLite, OGC standards, OpenTelemetry, observability, microservices, cloud-native, deployment models, Docker, Kubernetes, Azure, AWS, GCP, performance, scalability, security

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Covers**: Honua Server 1.0.0-rc1
