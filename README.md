<div align="center">
  <img src="docs/media/honua-logo.png" alt="Honua logo" width="180" />
</div>

# Honua Server

A cloud-native geospatial server built on .NET 9, implementing OGC standards and Geoservices REST a.k.a. Esri REST APIs with first-class support for modern cloud infrastructure.

**Part of the Honua Platform** - A comprehensive geospatial ecosystem including Honua Server, Honua Field Mobile, Honua MapSDK, GeoEvent Server, and GeoETL.

[![Build](https://github.com/honua-io/Honua.Server/workflows/build/badge.svg)](https://github.com/honua-io/Honua.Server/actions)
[![Tests](https://github.com/honua-io/Honua.Server/workflows/tests/badge.svg)](https://github.com/honua-io/Honua.Server/actions)
[![codecov](https://codecov.io/gh/honua-io/Honua.Server/branch/main/graph/badge.svg)](https://codecov.io/gh/honua-io/Honua.Server)
[![License](https://img.shields.io/badge/license-Elastic_2.0-blue.svg)](LICENSE)

[Documentation](docs/) • [Quick Start](#quick-start) • [Architecture](#architecture) • [Pricing](#pricing)

---

## Overview

Honua provides a complete OGC-compliant geospatial server with:

- **Standards Implementation**: OGC API Features/Tiles/Records, WFS 2.0/3.0, WMS 1.3, WCS 2.0, STAC 1.0, Geoservices REST (Esri), Carto SQL API
- **Data Providers**: PostgreSQL/PostGIS, MySQL, SQLite, SQL Server, Oracle, Snowflake, BigQuery, Redshift, MongoDB, Cosmos DB
- **Cloud-Native Architecture**: Docker, Kubernetes, multi-cloud deployment, OpenTelemetry observability
- **High Performance**: Built on .NET 9 with NetTopologySuite for geometry operations
- **Transactional Editing**: Full WFS-T support with versioning and conflict resolution
- **Export Formats**: GeoJSON, GeoPackage, Shapefile, KML/KMZ, CSV, MVT, and more

### Design Goals

1. **Standards Compliance**: Complete implementation of OGC standards without shortcuts
2. **Cloud-Native**: Designed for containerized deployment from the start
3. **Performance**: Leverage .NET's performance characteristics for geospatial workloads
4. **Flexibility**: Support both traditional databases and cloud data warehouses
5. **Observability**: Built-in metrics, tracing, and logging via OpenTelemetry

---

## Platform Components

| Component | Description |
|-----------|-------------|
| **🗺️ MapSDK** | Visual map builder with no-code editor, live preview, and export to JSON/YAML/HTML/Blazor. [Docs](src/Honua.MapSDK/README.md) |
| **📱 HonuaField Mobile** | Cross-platform field data collection app for iOS/Android/Windows/macOS with offline support. [Docs](src/HonuaField/README.md) |
| **⚡ GeoEvent Server** | Real-time geofencing with <100ms latency, batch processing, and webhook notifications. [API Guide](docs/GEOEVENT_API_GUIDE.md) |
| **🔄 GeoETL** | Container registry provisioning and build delivery for multi-tenant deployments. [Docs](src/Honua.Server.Intake/README.md) |
| **⚙️ Geoprocessing** | Distributed spatial analysis with 40+ operations (buffer, union, dissolve, heatmaps). Enterprise tier. |
| **🔔 Alert Receiver** | Cloud event webhook receiver for AWS SNS and Azure Event Grid notifications. |
| **🎛️ Admin Portal** | Web-based UI for managing maps, layers, geofences, users, and analytics. Built with Blazor. |

---

## Docker Deployment

Honua provides production-ready container images with multi-architecture support and platform-specific optimizations.

### Quick Start with Docker

```bash
# Run Full variant (vector + raster + cloud features)
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;..." \
  ghcr.io/honuaio/honua-server:latest

# Run Lite variant (vector-only, serverless-optimized)
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;..." \
  ghcr.io/honuaio/honua-server:lite

# Run with Docker Compose (includes PostgreSQL + Redis)
docker compose up
```

### Image Variants

**Full Image** (~150-180MB)
- Vector and raster processing (GDAL 3.11)
- All database providers (PostgreSQL, MySQL, SQL Server, Oracle, Snowflake, BigQuery, MongoDB, Cosmos DB)
- Cloud storage integration (AWS S3, Azure Blob, Google Cloud Storage)
- Map rendering and tile generation
- Complete OGC WCS 2.0 coverage support
- Enterprise geoprocessing

**Lite Image** (~60-80MB)
- Vector-only processing (no GDAL)
- PostgreSQL, MySQL, SQLite support
- 50% faster cold starts
- Perfect for serverless platforms

### Multi-Architecture Support

All images support both amd64 (x86_64) and arm64 (aarch64) architectures:

```bash
# Docker automatically pulls the correct architecture for your platform
docker pull ghcr.io/honuaio/honua-server:latest

# Explicit architecture selection
docker pull --platform linux/amd64 ghcr.io/honuaio/honua-server:latest
docker pull --platform linux/arm64 ghcr.io/honuaio/honua-server:latest

# Build for specific architecture
docker build --platform linux/amd64 -t honua-server:latest .
docker build --platform linux/arm64 -t honua-server:latest .
```

**ARM64 benefits:**
- AWS Graviton: Up to 34% better price-performance
- Azure ARM VMs: 15-20% cost savings
- Google Cloud: Available in select regions
- Apple Silicon: Native performance for local development

### Available Image Tags

```bash
# Version-specific tags
ghcr.io/honuaio/honua-server:1.0.0              # Specific version (full)
ghcr.io/honuaio/honua-server:1.0.0-lite         # Specific version (lite)
ghcr.io/honuaio/honua-server:1.0.0-amd64        # Architecture-specific
ghcr.io/honuaio/honua-server:1.0.0-arm64        # Architecture-specific

# Rolling tags
ghcr.io/honuaio/honua-server:latest             # Latest stable (full)
ghcr.io/honuaio/honua-server:lite               # Latest stable (lite)
ghcr.io/honuaio/honua-server:dev                # Development branch
ghcr.io/honuaio/honua-server:stable             # Alias for latest
```

### Registry Locations

Images are published to multiple registries for optimal performance:

```bash
# GitHub Container Registry (primary)
ghcr.io/honuaio/honua-server:latest

# AWS Elastic Container Registry Public
public.ecr.aws/honuaio/honua-server:latest

# Google Container Registry
gcr.io/honuaio/honua-server:latest

# Azure Container Registry
honuaio.azurecr.io/honua-server:latest
```

### ReadyToRun Compilation

All production images use **ReadyToRun (R2R) ahead-of-time compilation** for optimal performance:

- 30-50% faster cold starts vs traditional JIT compilation
- Platform-specific optimizations for linux-amd64 and linux-arm64
- Lower memory footprint during startup
- Essential for serverless deployments

### Serverless Deployment

Honua fits comfortably within serverless platform limits:

| Platform | Max Size | Full Size | Lite Size | Cold Start (Lite) |
|----------|----------|-----------|-----------|-------------------|
| AWS Lambda Container | 10GB | 150MB | 60MB | 1.5-2.5s |
| Google Cloud Run | 10GB | 150MB | 60MB | 1.5-2.0s |
| Azure Container Apps | N/A | 150MB | 60MB | 2.0-2.5s |
| Azure Functions | 1.5GB | 150MB | 60MB | 2.0-3.0s |

**Recommendation:** Use Lite variant for serverless platforms for faster cold starts and lower costs.

### Docker Documentation

**Comprehensive Guides:**
- [Docker Deployment Strategy](docs/DOCKER_DEPLOYMENT.md) - Complete guide to building, deploying, and publishing multi-arch images
- [Docker Quick Reference](docs/DOCKER_QUICK_REFERENCE.md) - Quick commands and examples for common operations
- [Docker Gotchas](docs/DOCKER_GOTCHAS.md) - Critical notes on GDAL compatibility, ReadyToRun, and platform-specific issues

**Key Topics Covered:**
- Multi-architecture builds with Docker Buildx (amd64/arm64)
- Platform-specific deployments (AWS Lambda, Google Cloud Run, Azure Container Apps, Kubernetes)
- Registry publishing to GHCR, ECR, GCR, ACR
- Base image selection and GDAL compatibility
- Security best practices and image signing with Cosign
- Performance tuning and ReadyToRun optimization

---

## Quick Start

### Docker (Full-Featured)

```bash
# Pull and run full deployment
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=postgres;Database=honua" \
  ghcr.io/honuaio/honua:latest
```

### Docker (Lite - Serverless Optimized)

```bash
# Pull and run lightweight deployment (vector-only)
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=postgres;Database=honua" \
  ghcr.io/honuaio/honua:lite
```

### Docker Compose

```bash
# Full stack with PostgreSQL
docker compose up
```

### Cloud Run (Google Cloud)

```bash
# Deploy lite version for fast cold starts
gcloud run deploy honua \
  --image ghcr.io/honuaio/honua:lite \
  --platform managed \
  --region us-central1
```

### Kubernetes

```bash
helm repo add honua https://charts.honua.io
helm install honua honua/honua-server \
  --set image.tag=latest \
  --set postgresql.enabled=true
```

See [deployment documentation](docs/DEPLOYMENT.md) for production configurations, feature comparison, and cloud-specific optimizations.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  Client Applications                                                │
│  Web Apps · Mobile Apps (iOS/Android) · Desktop · IoT Devices      │
└────────┬────────────────────┬───────────────────┬───────────────────┘
         │                    │                   │
    ┌────▼─────┐      ┌──────▼──────┐      ┌────▼─────────┐
    │ MapSDK   │      │ HonuaField  │      │ Custom Apps  │
    │ (Blazor) │      │ (.NET MAUI) │      │ (Any Client) │
    └────┬─────┘      └──────┬──────┘      └────┬─────────┘
         │                   │                   │
         └───────────────────┴───────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────┐
│  Honua Platform APIs                                      │
├───────────────────────────────────────────────────────────┤
│  • OGC API Features/Tiles/Records                         │
│  • WFS/WMS/WCS                                            │
│  • STAC · Geoservices REST · Carto SQL · OData · GraphQL  │
│  • GeoEvent API (Geofencing & Alerts)                     │
│  • Admin API (Map Configs, Users, Settings)               │
└────────────────────────────┬──────────────────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────┐
│  Core Services (.NET 9)                                   │
├───────────────────────────────────────────────────────────┤
│  • Query Engine (CQL2, SQL)                               │
│  • Geometry Processing (NetTopologySuite)                 │
│  • GeoEvent Engine (Geofencing, State Tracking)           │
│  • Export Pipeline (Multi-format)                         │
│  • Transaction Manager (WFS-T)                            │
│  • GeoETL/Intake (Container Distribution)                 │
│  • Cache Layer (Redis + Memory)                           │
│  • SignalR Hub (Real-time Events)                         │
└────────────────────────────┬──────────────────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────┐
│  Data & Storage Layer                                     │
├───────────────────────────────────────────────────────────┤
│  Relational DB (PostgreSQL, MySQL, SQL Server, Oracle)    │
│  Cloud DW (Snowflake, BigQuery, Redshift)                 │
│  NoSQL (MongoDB, Cosmos DB)                               │
│  Object Storage (S3, Azure Blob, GCS)                     │
│  Search (Elasticsearch)                                   │
│  Container Registries (GHCR, ECR, ACR, GCR)               │
└───────────────────────────────────────────────────────────┘
```

**Key Components:**
- **ASP.NET Core** - API hosting with minimal endpoints
- **NetTopologySuite** - Geometry operations and spatial algorithms
- **Polly** - Resilience policies (retry, circuit breaker, timeout)
- **OpenTelemetry** - Metrics, traces, and distributed logging
- **Dapper** - High-performance data access

---

## Features

### Standards Implementation

| Standard | Version | Capabilities |
|----------|---------|--------------|
| OGC API - Features | 1.0 | Core, CQL2 (text/JSON), transactions, search |
| OGC API - Tiles | 1.0 | Vector (MVT), raster, TileJSON |
| OGC API - Records | Draft | STAC 1.0 catalog |
| WFS | 2.0, 3.0 | Transactional operations, locking, versioning |
| WMS | 1.3.0 | GetMap, GetFeatureInfo, GetLegendGraphic |
| WCS | 2.0.1 | GetCoverage with subsetting, CRS transform |
| STAC | 1.0 | Collections, items, search API |
| OpenRosa | 1.0 | ODK/KoboToolbox form compatibility |
| Carto SQL API | v3 | Dataset discovery, SQL queries (SELECT, WHERE, GROUP BY, aggregates) |

### Data Providers

**Relational Databases:**
- PostgreSQL/PostGIS (recommended)
- MySQL with spatial extensions
- SQLite/SpatiaLite
- SQL Server with spatial types
- Oracle Spatial

**Cloud Data Warehouses:**
- Google BigQuery
- Snowflake
- AWS Redshift
- Databricks (via JDBC)

**NoSQL & Search:**
- MongoDB (GeoJSON support)
- Azure Cosmos DB
- Elasticsearch (geo_shape)

### Export Formats

Vector: GeoJSON, GeoJSON-Seq (streaming), GeoPackage, Shapefile, KML/KMZ, CSV, TopoJSON, GML, GeoServices JSON format

Raster: PNG, JPEG, WebP, GeoTIFF, COG

Tiles: MVT (Mapbox Vector Tiles), PBF

### Authentication & Security

- Local user database with bcrypt hashing
- OIDC/OAuth 2.0 integration
- SAML 2.0 for enterprise SSO
- JWT token validation
- API key authentication
- Role-based access control (RBAC)
- Audit logging with structured events

### Observability

- OpenTelemetry exporters (OTLP, Prometheus, Jaeger)
- Structured logging via Serilog
- Health check endpoints (`/health`, `/health/ready`, `/health/live`)
- Metrics: request rates, latencies, error rates, cache hit ratios
- Distributed tracing across data providers

---

## Performance

Honua is optimized for high-throughput geospatial operations:

- **Async I/O**: All database operations are fully asynchronous
- **Connection Pooling**: Efficient connection reuse with Npgsql, MySqlConnector
- **Query Optimization**: Prepared statements, parameter caching
- **Spatial Indexing**: Automatic use of spatial indexes (R-tree, GiST)
- **Streaming Export**: Large datasets exported without memory buffering
- **Caching**: Two-tier cache (Redis + in-memory) with automatic invalidation

Benchmark comparisons available in [docs/benchmarks/](docs/benchmarks/).

---

## Deployment Options

### Kubernetes

Helm charts include:
- Horizontal Pod Autoscaling (HPA)
- PodDisruptionBudgets
- NetworkPolicies
- ServiceMonitors for Prometheus
- Support for both Full and Lite deployments

See [Kubernetes deployment guide](docs/deployment/kubernetes.md).

### Serverless Platforms

**Recommended: Lite deployment** for serverless (60% smaller images, 50%+ faster cold starts)

**Supported platforms:**
- Google Cloud Run
- AWS Lambda (Container Image)
- Azure Container Apps
- AWS Fargate
- Google Cloud Functions (2nd gen)

See [serverless deployment guide](docs/DEPLOYMENT.md#serverless-platforms) for platform-specific configurations.

### Configuration

Configuration via environment variables, appsettings.json, or command-line arguments.

**Key settings:**
```bash
HONUA__METADATA__PROVIDER=json|yaml|postgres
HONUA__METADATA__PATH=/path/to/metadata
HONUA__CONNECTIONSTRINGS__DEFAULT=<connection-string>
HONUA__AUTHENTICATION__MODE=Local|OIDC|SAML
HONUA__CACHE__REDIS__CONNECTIONSTRING=<redis-url>
HONUA__TELEMETRY__ENDPOINT=<otlp-endpoint>
```

Full configuration reference: [docs/configuration/](docs/configuration/)

---

## Pricing

Honua uses the **Elastic License 2.0** (source-available, not open source).

### Licensing Summary

**You can:**
- Use for internal projects
- Self-host in your organization
- Modify the source code
- Build applications on top of Honua

**You cannot:**
- Offer Honua as a hosted service to third parties
- Remove or bypass license validation
- Circumvent feature restrictions

### Tiers

| Feature | Free | Professional | Enterprise |
|---------|------|--------------|------------|
| **Monthly Cost** | $0 | $299 | $1,499 |
| **Users** | 1 | 10 | Unlimited |
| **Layers** | 10 | 100 | Unlimited |
| **API Requests/Day** | 10,000 | 100,000 | Unlimited |
| **PostgreSQL/MySQL/SQLite** | ✓ | ✓ | ✓ |
| **SQL Server** | - | ✓ | ✓ |
| **Cloud DW (Snowflake, BigQuery, Redshift)** | - | - | ✓ |
| **NoSQL (MongoDB, Cosmos)** | - | - | ✓ |
| **Local Auth** | ✓ | ✓ | ✓ |
| **OIDC/OAuth** | - | ✓ | ✓ |
| **SAML/SSO** | - | - | ✓ |
| **STAC Catalog** | - | ✓ | ✓ |
| **BI Connectors (Power BI, Tableau)** | - | ✓ | ✓ |
| **Geoprocessing** | - | ✓ | ✓ |
| **Cloud Batch Processing** | - | - | ✓ |
| **Versioning & Branching** | - | - | ✓ |
| **Multi-tenancy** | - | - | ✓ |
| **Support** | Community | Email (2-day) | Priority (4-hour) |

Annual pricing available (20% discount).

---

## Documentation

**Getting Started:**
- [Quick Start Guide](docs/user/getting-started.md)
- [Configuration Guide](docs/configuration/)
- [Deployment Guides](docs/deployment/)

**Platform Components:**
- [MapSDK Documentation](src/Honua.MapSDK/README.md) - Visual map builder
- [HonuaField Mobile App](src/HonuaField/README.md) - Field data collection
- [GeoEvent API Guide](docs/GEOEVENT_API_GUIDE.md) - Real-time geofencing
- [GeoETL/Intake README](src/Honua.Server.Intake/README.md) - Container distribution

**API References:**
- [OGC API Reference](docs/api/)
- [GeoEvent API](docs/GEOEVENT_API_GUIDE.md)
- [Admin API](docs/api/admin/)

**Operations:**
- [Performance Tuning](docs/operations/performance.md)
- [Security Best Practices](docs/SECURITY.md)
- [Monitoring & Observability](docs/operations/)

---

## Development

### Building from Source

```bash
git clone https://github.com/honuaio/honua.git
cd honua
dotnet restore
dotnet build
dotnet test
```

### Running Tests

```bash
# Unit tests
dotnet test --filter "Category=Unit"

# Integration tests (requires Docker)
dotnet test --filter "Category=Integration"

# OGC conformance tests
export HONUA_RUN_OGC_CONFORMANCE=true
dotnet test --filter "FullyQualifiedName~OgcConformanceTests"
```

### Code Coverage

Minimum coverage requirements:
- Honua.Server.Core: 65%
- Honua.Server.Host: 60%
- Overall: 60%

```bash
./scripts/check-coverage.sh
```

---

## Project Structure

```
src/
├── Honua.Server.Core/              # Core services (both Full and Lite)
├── Honua.Server.Core.Raster/       # Raster data sources (Full only)
├── Honua.Server.Core.OData/        # OData protocol (both)
├── Honua.Server.Core.Cloud/        # Cloud SDKs (Full only)
├── Honua.Server.Host/              # Full-featured entry point
├── Honua.Server.Enterprise/        # Enterprise features
│
├── Honua.MapSDK/                   # Map SDK & visual builder
│   ├── Components/                 # Blazor map components
│   ├── Core/                       # Message bus & coordination
│   ├── Models/                     # Map configuration models
│   └── Services/                   # Export & configuration services
│
├── Honua.Admin.Blazor/             # Admin portal UI
│   └── Components/
│       └── Pages/
│           └── Maps/               # Map builder pages
│
├── HonuaField/                     # Mobile field app (.NET MAUI)
│   └── HonuaField/                 # iOS, Android, Windows, macOS
│       ├── Models/                 # Feature, Collection models
│       ├── Data/                   # SQLite repositories
│       ├── Services/               # Sync, GPS, biometric
│       └── Platforms/              # Platform-specific code
│
├── Honua.Server.Intake/            # GeoETL/Container registry system
│   ├── Services/                   # Registry provisioning
│   ├── Controllers/                # Intake API endpoints
│   └── Models/                     # Build delivery models
│
├── Honua.Server.AlertReceiver/     # Cloud event receiver
│   └── Controllers/                # SNS, Event Grid webhooks
│
├── Honua.Server.Gateway/           # API gateway
├── Honua.Server.Observability/     # Metrics & monitoring
├── Honua.Cli/                      # Command-line tools
└── Honua.Cli.AI/                   # AI-powered deployment agents

tests/
├── Honua.Server.Core.Tests/
├── Honua.Server.Integration.Tests/
│   └── GeoEvent/                   # GeoEvent & geofencing tests
├── Honua.MapSDK.Tests/             # MapSDK tests
├── HonuaField.Tests/               # Mobile app tests
└── Honua.Server.Benchmarks/
```

**Modular architecture** enables building optimized deployment configurations with only required dependencies.

---

## Contributing

Honua is source-available under Elastic License 2.0. We are not currently accepting code contributions, but welcome:

- Bug reports
- Feature requests
- Documentation improvements
- Use case descriptions

See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

---

## Support

- **Community**: [GitHub Discussions](https://github.com/honuaio/honua/discussions)
- **Issues**: [GitHub Issues](https://github.com/honuaio/honua/issues)
- **Security**: [SECURITY.md](SECURITY.md)
- **Commercial Support**: support@honua.io

---

## License

**Elastic License 2.0** - Source-available, not open source.

Key restrictions:
- Cannot offer as a hosted service to third parties
- Cannot remove license validation
- Must include license notices

Full terms: [LICENSE](LICENSE)

Commercial licensing available for specific use cases.

---

## Acknowledgments

Built with:

**Core Platform:**
- [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) - Geometry operations
- [Polly](https://github.com/App-vNext/Polly) - Resilience policies
- [Serilog](https://serilog.net/) - Structured logging
- [Dapper](https://github.com/DapperLib/Dapper) - Data access
- [MaxRev.Gdal.Core](https://github.com/MaxRev-Dev/gdal.netcore) - Raster processing

**MapSDK:**
- [MapLibre GL](https://github.com/maplibre/maplibre-gl-js) - WebGL mapping
- [MudBlazor](https://mudblazor.com/) - Blazor component library
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - .NET web framework

**HonuaField Mobile:**
- [.NET MAUI](https://dotnet.microsoft.com/apps/maui) - Cross-platform framework
- [Mapsui](https://github.com/Mapsui/Mapsui) - Native mapping library
- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D graphics
- [ML.NET](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet) - Machine learning
- [SQLite](https://www.sqlite.org/) - Embedded database

**GeoETL:**
- [crane](https://github.com/google/go-containerregistry) - Container image operations
- [Octokit](https://github.com/octokit/octokit.net) - GitHub API
- [AWS SDK](https://aws.amazon.com/sdk-for-net/) - AWS integrations
- [Azure SDK](https://azure.github.io/azure-sdk/) - Azure integrations

---

**Honua** - _Hawaiian for "Earth"_
