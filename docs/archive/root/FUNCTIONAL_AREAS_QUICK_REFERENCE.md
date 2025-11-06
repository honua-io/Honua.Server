# HonuaIO Functional Areas - Quick Reference Guide

## High-Level Organization

### Core Assemblies
- **Honua.Server.Core** (488 files) - All business logic
- **Honua.Server.Host** (309 files) - HTTP API layer
- **Honua.Cli** (138 files) - CLI tool
- **Honua.Cli.AI** (extensive) - LLM-powered automation
- **Honua.Server.AlertReceiver** - Alert microservice
- **Honua.Server.Enterprise** - Cloud warehouse integrations

---

## API & Protocols (8 Standards Implemented)

| Protocol | Location | Key Files | Status |
|----------|----------|-----------|--------|
| **OGC API Features 1.0** | `Host/Ogc/OgcFeatures*` | OgcFeaturesHandlers, Ogc*Handlers | Full |
| **OGC API Tiles 1.0** | `Host/Ogc/OgcTiles*` | OgcTilesHandlers | Full |
| **OGC API Records** | `Host/Ogc/OgcShared*` | OgcSharedHandlers | Full |
| **WFS 2.0** | `Host/Wfs/` | WfsHandlers, WfsTransactionHandlers, WfsLockHandlers | Full transactional |
| **WMS 1.3** | `Host/Wms/` | WmsHandlers, WmsGetMapHandlers, WmsCapabilitiesBuilder | Full |
| **STAC 1.0** | `Host/Stac/` + `Core/Stac/` | StacSearchController, StacCatalogBuilder | Full |
| **Geoservices REST a.k.a. Esri REST** | `Host/GeoservicesREST/` | Esri adapters for Feature/Map/Image Server | Full |
| **OData v4** | `Host/OData/` | OData filtering & projection | Full |

**Query Filters:**
- CQL2 Text/JSON: `Core/Query/Filter/CqlFilterParser.cs`, `Cql2JsonParser.cs`
- OData: `Core/Query/Filter/ODataFilterParser.cs`
- WFS XML: `Host/Wfs/Filters/XmlFilterParser.cs`

---

## Data Operations

### Feature Editing
- **Location**: `Core/Editing/`
- **Key Class**: `FeatureEditOrchestrator.cs`
- **Supports**: CRUD, geometry validation, locking, audit trails

### Data Ingestion (Async Jobs)
- **Location**: `Core/Import/`
- **Key Class**: `DataIngestionService.cs`
- **Supports**: GDAL-based import, STAC sync, job tracking

### Export (10+ Formats)
- **Location**: `Core/Export/` and `Core/Serialization/`
- **Formats**: GeoJSON, CSV, Shapefile, KML/KMZ, Parquet, FlatGeobuf, GeoArrow, etc.
- **Streaming**: `GeoJsonSeqStreamingWriter`, `GeoJsonFeatureCollectionStreamingWriter`

---

## Raster & Imagery

### Pure .NET Readers (No GDAL)
- **COG**: `Core/Raster/Readers/LibTiffCogReader.cs` - IFD parsing, HTTP range requests
- **Zarr**: `Core/Raster/Readers/IZarrReader.cs` - Sharded chunks, compression (Blosc, Zstd, LZ4)

### Raster Caching (Multi-Cloud)
- **Abstraction**: `Core/Raster/Caching/IRasterTileCacheProvider.cs`
- **Backends**: S3, Azure Blob, GCS, FileSystem, Null
- **Quota**: `RasterTileCacheDiskQuotaService.cs` - LRU eviction

### Rendering
- **Engine**: `Core/Raster/Rendering/SkiaSharpRasterRenderer.cs`
- **Formats**: PNG, JPEG, WebP
- **Features**: Band selection, color ramps, resampling

### Analytics
- **Service**: `Core/Raster/Analytics/RasterAnalyticsService.cs`
- **Operations**: Band math, NDVI, statistics
- **Mosaic**: `Core/Raster/Mosaic/RasterMosaicService.cs`

---

## Metadata Management

### Providers (Pluggable)
- `Core/Metadata/IMetadataProvider.cs`
- **Implementations**: JSON, YAML, PostgreSQL, Redis, SQL Server
- **Cache**: `CachedMetadataRegistry.cs` with TTL

### Validation
- `MetadataValidator.cs` - Schema compliance
- `LayerDefinitionValidator.cs` - Layer configs
- `ProtocolMetadataValidator.cs` - OGC/WFS/WMS validation

### Snapshots (Versioning)
- `Core/Metadata/Snapshots/IMetadataSnapshotStore.cs`
- **Implementations**: File-based, database-backed
- **Operations**: Save, restore, diff

---

## Query & Filtering

### CQL2 Parser
- **Text**: `Core/Query/Filter/CqlFilterParser.cs`
- **JSON**: `Core/Query/Filter/Cql2JsonParser.cs`
- **SQL Generator**: `Core/Stac/Cql2/Cql2SqlQueryBuilder.cs`

### Supported Operations
- Logical: AND, OR, NOT
- Comparison: =, <>, <, >, <=, >=
- Pattern: LIKE, IN
- Spatial: INTERSECTS, CONTAINS, WITHIN, DISJOINT, CROSSES, TOUCHES
- Temporal: Before, After, Between
- Arrays

### Filter Complexity
- `Core/Query/Filter/FilterComplexityScorer.cs` - DoS prevention

---

## Authentication & Authorization

### Authentication
- **Local**: `Core/Authentication/LocalAuthenticationService.cs`, `LocalTokenService.cs`
  - Password hashing: Argon2 (KDF)
  - Bootstrap: `AuthBootstrapService.cs`
- **OIDC**: `JwtBearerOptionsConfigurator.cs`

### Authorization
- **RBAC**: `Core/Authorization/ResourceAuthorizationService.cs`
- **Cache**: `ResourceAuthorizationCache.cs` (TTL)
- **Handlers**: `CollectionAuthorizationHandler.cs`, `LayerAuthorizationHandler.cs`
- **Cascading**: Collection → Layer → Feature

### Token Management
- **Revocation**: `Core/Auth/RedisTokenRevocationService.cs`

---

## Security & Encryption

### Connection String Encryption
- **Service**: `Core/Security/ConnectionStringEncryptionService.cs`
- **Backends**: GCP KMS, AWS KMS, ASP.NET Data Protection

### Validation Framework
- **SQL Injection**: `SqlIdentifierValidator.cs`
- **Path Traversal**: `SecurePathValidator.cs`
- **Archives**: `ZipArchiveValidator.cs`
- **URLs**: `UrlValidator.cs`

### Host-Level Security
- **Middleware**: `Host/Security/SecurityPolicyMiddleware.cs`
- **Headers**: HSTS, CSP, X-Frame-Options
- **CORS**: Configurable policy

---

## Observability & Monitoring

### Distributed Tracing
- **Core**: `Core/Observability/HonuaTelemetry.cs`
- **Scopes**: `ActivityScope.cs`
- **Instrumentation**: `OperationInstrumentation.cs`

### Metrics
- **API**: `ApiMetrics.cs`
- **Database**: `DatabaseMetrics.cs`
- **Cache**: `CacheMetrics.cs`
- **Security**: `SecurityMetrics.cs`
- **Tiles**: `VectorTileMetrics.cs`
- **Raster**: `ZarrTimeSeriesMetrics.cs`
- **Resilience**: `CircuitBreakerMetrics.cs`

### Logging
- **Framework**: Serilog with structured logging
- **Sinks**: Console, File, Seq, Elasticsearch
- **Audit**: `Core/Logging/SecurityAuditLogger.cs`

### Alerts
- **Emission**: `Core/Observability/AlertClient.cs`
- **Sink**: `SerilogAlertSink.cs`

---

## Resilience & Reliability

### Polly Policies
- **Location**: `Core/Resilience/ResiliencePolicies.cs`
- **Patterns**: Retry, Circuit Breaker, Hedging, Bulkhead, Timeout

### Service Wrappers
- **Generic**: `ResilientServiceExecutor.cs`
- **External**: `ResilientExternalServiceWrapper.cs`
- **Raster**: `ResilientRasterTileService.cs`

### Cache Resilience
- **Circuit Breaker**: `Core/Caching/CacheCircuitBreaker.cs`
- **Safe Wrapper**: `ResilientCacheWrapper.cs`
- **Observable**: `ObservableCacheDecorator.cs`

---

## Data Access

### Supported Databases
- **PostgreSQL/PostGIS**: Npgsql
- **SQL Server**: Microsoft.Data.SqlClient
- **SQLite/SpatiaLite**: Microsoft.Data.Sqlite
- **MySQL**: MySqlConnector

### ORM Strategy
- **Primary**: Dapper (lightweight)
- **Pattern**: Custom mappers for spatial types

### Schema Discovery
- **Service**: `Core/Data/Validation/ISchemaDiscoveryService.cs`
- **Features**: Auto-detection, type inference, SRID discovery

---

## Advanced Features

### Print Services
- **Location**: `Core/Print/`
- **Integration**: MapFish Print
- **Features**: Template-based rendering, legend generation

### Styling
- **Repository**: `Core/Styling/IStyleRepository.cs`
- **Formats**: SLD, CSS
- **Caching**: TTL-based

### Blue/Green Deployment
- **Location**: `Core/BlueGreen/`
- **Features**: Validation, traffic switching, rollback

### GitOps
- **Location**: `Core/GitOps/`
- **Features**: Config management, cert renewal, DB migrations

### Feature Flags
- **Service**: `Core/Features/IFeatureManagementService.cs`
- **Degradation**: AI, Caching, Search, STAC, Metrics fallbacks

---

## AI & Automation (CLI.AI)

### LLM Providers (7+)
- OpenAI, Azure OpenAI, Anthropic, Ollama, LocalAI, Azure AI Search, Mocks

### Agent Architecture
- **Coordination**: `Services/Agents/IAgentCoordinator.cs`
- **Decomposition**: Hierarchical task breakdown
- **Validation**: Loop-based validation and retry
- **Rollback**: Orchestration framework

### Plugins
- **Types**: Workspace, Migration, Documentation, ControlPlane, Monitoring, Compliance
- **Location**: `Services/Plugins/`

### Processes
- Deployment, Upgrade, MetadataManagement, Benchmarking, CertificateRenewal, GitOps, NetworkDiagnostics

### Guardrails
- **Deployment Validation**: `Services/Guardrails/DeploymentGuardrailValidator.cs`
- **Cost Tracking**: `Services/Cost/CostTrackingService.cs`
- **Metrics Monitoring**: `IDeploymentMetricsProvider.cs`

---

## Testing Structure

### Test Projects
| Project | Files | Focus |
|---------|-------|-------|
| Core.Tests | 307+ | Business logic, protocols, queries |
| Host.Tests | 42+ | API endpoints, middleware |
| Cli.Tests | Multiple | CLI commands |
| Cli.AI.Tests | Multiple | LLM agents, processes |
| Enterprise.Tests | Multiple | BigQuery, Redshift, Snowflake |
| Deployment.E2E | Multiple | Cloud/K8s scenarios |

### Test Categories
- Api, Attachments, Auth, Catalog, Editing, Export, Features, Metadata, Ogc, Query, Raster, Serialization, Stac, Security, Wfs/Wms/Wmts, Collections

### Compliance
- OGC Conformance Suite
- STAC Validation
- Load Testing
- Python e2e

---

## Key File Locations Quick Index

| Concern | File/Namespace |
|---------|----------------|
| OGC Features API | `/Host/Ogc/OgcFeatures*` |
| WFS Transactions | `/Host/Wfs/WfsTransaction*` |
| WMS Rendering | `/Host/Wms/WmsGetMap*` |
| STAC Search | `/Host/Stac/StacSearchController.cs` |
| Feature Editing | `/Core/Editing/FeatureEditOrchestrator.cs` |
| CQL2 Parsing | `/Core/Query/Filter/CqlFilterParser.cs` |
| Raster Caching | `/Core/Raster/Caching/IRasterTileCacheProvider.cs` |
| COG Reading | `/Core/Raster/Readers/LibTiffCogReader.cs` |
| Zarr Support | `/Core/Raster/Readers/IZarrReader.cs` |
| Metadata | `/Core/Metadata/IMetadataProvider.cs` |
| Auth | `/Core/Authentication/LocalAuthenticationService.cs` |
| Authz | `/Core/Authorization/ResourceAuthorizationService.cs` |
| Encryption | `/Core/Security/ConnectionStringEncryptionService.cs` |
| Export | `/Core/Export/`, `/Core/Serialization/` |
| Observability | `/Core/Observability/HonuaTelemetry.cs` |
| Resilience | `/Core/Resilience/ResiliencePolicies.cs` |
| Data Ingestion | `/Core/Import/DataIngestionService.cs` |
| LLM Agents | `/Cli.AI/Services/Agents/` |
| Plugins | `/Cli.AI/Services/Plugins/` |
| Guardrails | `/Cli.AI/Services/Guardrails/` |

---

## Configuration & Extension Points

### Configuration
- `appsettings.json` - Main configuration
- `metadata.json`/`metadata.yaml` - Layer/collection definitions
- Feature flags - Runtime behavior
- Resilience policies - Retry/circuit breaker settings

### Extension Points
- `IMetadataProvider` - Metadata backends
- `IRasterSourceProvider` - Raster sources
- `IResourceAuthorizationHandler` - RBAC handlers
- `INotificationService` - Notifications (Slack, Email)
- `IAlertPublisher` - Alert routing
- `ILlmProvider` - LLM backends
- Export format handlers
- Filter parser implementations

---

## External Dependencies

### Spatial
NetTopologySuite, GDAL, LibTiff, SkiaSharp, FlatGeobuf, GeoArrow, Parquet

### Data Access
Npgsql, SqlClient, SQLite, MySqlConnector, Dapper

### Cloud
AWS SDK (S3, KMS), Azure SDK (Blobs, KMS), Google Cloud SDK (Storage, KMS)

### Infrastructure
Serilog, Polly, OpenTelemetry, StackExchange.Redis, Swashbuckle

### Serialization
System.Text.Json, Newtonsoft.Json, YamlDotNet, SharpKml, LibGit2Sharp

---

*Last Updated: October 29, 2025*
*Version: 2.0 (MVP Release Candidate)*
