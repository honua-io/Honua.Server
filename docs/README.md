# Honua Documentation

**Last Updated**: 2025-11-09
**Status**: ✅ Comprehensive, Organized & Current

Complete documentation for Honua - a cloud-native geospatial server built on .NET 9.

---

## 👤 For Users

**New to Honua? Start here:**
- 🚀 **[Quick Start](quickstart/README.md)** - Get running in 5 minutes
- 📖 **[User Documentation](user/README.md)** - Complete user guide
- 🗺️ **[MapSDK](mapsdk/README.md)** - Blazor mapping components

## 🔧 For Developers

**Contributing to Honua? Start here:**
- 🏗️ **[Development Documentation](development/README.md)** - Architecture, testing, processes
- 📝 **[Contributing Guide](../CONTRIBUTING.md)** - How to contribute
- 🧪 **[Testing Guide](development/testing/QUICKSTART-TESTING.md)** - Run tests

---

## Quick Navigation

### User Documentation

| Documentation Type | Location | Purpose |
|-------------------|----------|---------|
| 📖 **User Guide** | [user/](user/) | Authentication, configuration, APIs, data ingestion |
| 🚀 **Getting Started** | [quickstart/](quickstart/) | 5-minute quickstart guide |
| 📚 **API Reference** | [api/](api/) | Complete API documentation |
| ⚙️ **Configuration** | [configuration/](configuration/) | appsettings.json reference |
| 🚢 **Deployment** | [deployment/](deployment/) | Docker, K8s, Cloud platforms |
| 📊 **Observability** | [observability/](observability/) | Monitoring, tracing, alerting |
| 🗺️ **MapSDK** | [mapsdk/](mapsdk/) | Blazor mapping components |
| 🤖 **AI Knowledge Base** | [rag/](rag/) | AI-optimized documentation |

### Development Documentation

| Documentation Type | Location | Purpose |
|-------------------|----------|---------|
| 🏗️ **Architecture** | [development/architecture/](development/architecture/) | ADRs, system design |
| 🧪 **Testing** | [development/testing/](development/testing/) | Test strategies, guides |
| 📝 **Implementation** | [development/implementation/](development/implementation/) | Feature implementations |
| 🔄 **Refactoring** | [development/refactoring/](development/refactoring/) | Refactoring plans & metrics |
| ⚙️ **Processes** | [development/processes/](development/processes/) | CI/CD, contributing |

## What is Honua?

Honua (Hawaiian for "Earth") is a modern geospatial server designed to serve vector and raster data at scale. Built on .NET 9 with native cloud-optimized raster support (COG, Zarr) and comprehensive OGC standards compliance.

**Key Features**:
- OGC Standards (WFS, WMS, WMTS, WCS, CSW)
- OGC API (Features, Tiles, Records)
- Geoservices REST a.k.a. Esri REST API compatibility
- STAC 1.0 catalog
- 12+ export formats
- OpenTelemetry observability
- AI-powered CLI assistant

## Documentation Structure

### User Documentation

#### [Quick Start Guide](quickstart/)
Get Honua running in 5 minutes:
- Docker Compose setup
- First API requests
- Working curl examples for all protocols
- Export format examples

#### [API Reference](api/)
Complete endpoint documentation:
- OGC API Features, WFS, WMS, WMTS, WCS, CSW
- Geoservices REST a.k.a. Esri REST API (FeatureServer, MapServer)
- STAC 1.0 catalog and search
- Authentication and authorization
- Export formats

#### [Configuration Guide](configuration/)
Complete configuration reference:
- appsettings.json options
- Environment variables
- Authentication modes (Local, OIDC, API Keys)
- Storage providers (FileSystem, Azure, S3)
- Rate limiting and security

#### [Deployment Guide](deployment/)
Production deployment:
- Docker and Docker Compose
- Kubernetes manifests
- AWS, Azure, GCP examples
- Performance tuning
- Production checklist

#### [Observability](observability/)
Monitoring and alerting:
- OpenTelemetry metrics and tracing
- Prometheus + Grafana setup
- Distributed tracing (Jaeger, Tempo)
- Runtime configuration API
- Performance baselines and SLOs

#### [Security](security/)
Security scanning and vulnerability management:
- [Container Security](CONTAINER_SECURITY.md) - Complete vulnerability scanning guide
- [Container Security Quick Start](security/CONTAINER_SECURITY_QUICKSTART.md) - Quick reference guide
- [Sample Trivy Scan Output](SAMPLE_TRIVY_SCAN_OUTPUT.md) - Example scan results
- Trivy, Grype, and Docker Scout integration
- CI/CD security scanning workflows
- Remediation procedures and best practices

#### SBOM (Software Bill of Materials)
Supply chain security and transparency:
- [SBOM Guide](SBOM_GUIDE.md) - Comprehensive SBOM documentation
- [SBOM Quick Start](SBOM_QUICK_START.md) - Quick reference guide
- Accessing and verifying SBOMs attached to container images
- SPDX and CycloneDX format support
- Cryptographic signature verification with Cosign
- Integration with vulnerability scanning tools

### Process Framework

**NEW**: Stateful, event-driven workflow orchestration for long-running operations.

#### [Process Framework Quick Start](quickstart/PROCESS_FRAMEWORK_QUICKSTART.md)
Get started in 5 minutes:
- Local setup with mock LLM
- Running your first workflow
- Viewing metrics and traces
- Testing with Docker Compose

#### [Process Framework Deployment](deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)
Production deployment guide:
- Prerequisites (Redis, LLM API keys, .NET 9)
- Configuration (appsettings.json, user secrets)
- 4 deployment scenarios (local, Docker, Kubernetes, Azure)
- Redis setup (standalone, cluster, Azure Cache, AWS ElastiCache)
- Monitoring (Prometheus, Grafana, Azure Monitor)
- TLS/SSL configuration
- Horizontal and vertical scaling

#### [Process Framework Operations](operations/PROCESS_FRAMEWORK_OPERATIONS.md)
Daily operations and maintenance:
- Daily, weekly, monthly checklists
- Monitoring dashboards overview
- 6 common troubleshooting scenarios
- Performance tuning (CPU, memory, Redis, LLM)
- Backup and recovery procedures
- Disaster recovery testing

#### [Operational Runbooks](operations/RUNBOOKS.md)
Step-by-step incident response:
- **8 detailed runbooks** for common scenarios
- High process failure rate investigation
- Process timeout recovery
- Redis failover procedure
- LLM provider failover
- Emergency process cancellation
- Data recovery from Redis
- Pod restart and recovery
- Database connection failure

### AI Consultant Knowledge Base

#### [RAG Documentation](rag/)
**14 comprehensive documents (320+ KB, 13,000+ lines)** optimized for AI retrieval:

**Architecture** (2 docs):
- System architecture and design patterns
- OGC standards implementation (WFS, WMS, WMTS, WCS, CSW)

**Configuration** (2 docs):
- Complete configuration reference
- Authentication setup (Local, JWT, OIDC, API Keys)

**API Reference** (5 docs):
- OGC API Features with CQL2 filtering
- Geoservices REST a.k.a. Esri REST API complete reference
- STAC 1.0 catalog implementation
- Export formats (GeoJSON, Shapefile, GeoPackage, etc.)
- Control Plane API (admin endpoints)

**Deployment** (2 docs):
- Docker deployment with monitoring
- Kubernetes deployment with Helm

**Operations** (3 docs):
- Complete CLI command reference
- Common issues troubleshooting
- Raster processing (COG, Zarr, analytics)

Each document includes:
- YAML frontmatter with tags and metadata
- 500+ working code examples
- 200+ tested curl commands
- Troubleshooting sections
- Cross-references

## Quick Links

### Getting Started
- [5-Minute Quickstart](quickstart/README.md)
- [Docker Setup](deployment/README.md#docker-recommended-for-development)
- [First API Request](quickstart/README.md#first-api-requests)

### Configuration
- [Authentication Setup](configuration/README.md#authentication-modes)
- [Database Configuration](configuration/README.md#database-configuration)
- [Observability Configuration](configuration/README.md#observability-configuration)

### API Documentation
- [OGC API Features](api/README.md#ogc-api-features)
- [WFS/WMS/WMTS](api/README.md#wfs-20)
- [Geoservices REST a.k.a. Esri REST API](api/README.md#esri-rest-api)
- [STAC Catalog](api/README.md#stac-10)
- [Export Formats](api/README.md#export-formats)

### Deployment
- [Docker Compose](deployment/README.md#docker-recommended-for-development)
- [Kubernetes](deployment/README.md#kubernetes)
- [AWS/Azure/GCP](deployment/README.md#cloud-platforms)

### Monitoring
- [Metrics Setup](observability/README.md#quick-start)
- [Distributed Tracing](observability/README.md#enable-distributed-tracing)
- [Performance Baselines](observability/performance-baselines.md)
- [Runtime Configuration API](observability/README.md#runtime-configuration)

### Security
- [Container Security Guide](CONTAINER_SECURITY.md)
- [Quick Start Guide](security/CONTAINER_SECURITY_QUICKSTART.md)
- [Sample Scan Output](SAMPLE_TRIVY_SCAN_OUTPUT.md)
- [GitHub Security Tab](https://github.com/honua/honua.next/security/code-scanning)

### Process Framework
- [Quick Start (5 minutes)](quickstart/PROCESS_FRAMEWORK_QUICKSTART.md)
- [Deployment Guide](deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)
- [Operations Guide](operations/PROCESS_FRAMEWORK_OPERATIONS.md)
- [Operational Runbooks (8 scenarios)](operations/RUNBOOKS.md)

## What's Documented

✅ **Complete Coverage**:
- All OGC protocols (WFS, WMS, WMTS, WCS, CSW)
- All modern APIs (OGC API Features, STAC, Geoservices REST a.k.a. Esri REST)
- All authentication modes (Local, JWT, OIDC, API Keys)
- All 12+ export formats
- Docker, Kubernetes, and cloud deployment
- Complete CLI reference
- Raster processing (COG, Zarr)
- Observability and monitoring
- Container security scanning
- Troubleshooting and diagnostics

✅ **Quality Standards**:
- All code examples tested and working
- Based on actual codebase (not aspirational)
- Comprehensive error handling
- Platform-specific guidance
- AI-optimized for RAG retrieval

## Platform Components

Honua is part of a comprehensive geospatial platform:

### 🗺️ MapSDK - Blazor Mapping Components
**[Complete Documentation](mapsdk/README.md)**
- Visual map builder with no-code editor
- Interactive dashboards and components
- Pub/sub architecture via ComponentBus
- [Getting Started](mapsdk/getting-started/quick-start.md) | [Tutorials](mapsdk/tutorials/) | [Component Catalog](mapsdk/ComponentCatalog.md)

### ⚡ GeoEvent Server - Real-Time Geofencing
**[API Guide](GEOEVENT_API_GUIDE.md)**
- Sub-100ms geofencing latency
- Batch processing and state tracking
- Webhook notifications

### 🔄 GeoETL - Data Transformation
**[Documentation](geoetl/)**
- ETL pipelines for geospatial data
- Container distribution system
- [Performance Guide](geoetl/PERFORMANCE_GUIDE.md) | [Error Handling](geoetl/ERROR_HANDLING.md)

---

## Development Documentation

### For Contributors & Internal Team

All development documentation has been organized into [development/](development/):

**Architecture** - [development/architecture/](development/architecture/)
- 14 Architecture Decision Records (ADRs)
- System architecture analysis
- UI/Client architecture

**Testing** - [development/testing/](development/testing/)
- Test strategies and guides
- Integration testing
- Parallel testing architecture
- Benchmarking

**Implementation** - [development/implementation/](development/implementation/)
- Feature implementation summaries
- Phase completion reports
- 3D, VR, and drone features
- Advanced filtering and AI features

**Refactoring** - [development/refactoring/](development/refactoring/)
- Refactoring plans and strategies
- Before/after comparisons
- Metrics and reports

**Processes** - [development/processes/](development/processes/)
- CI/CD pipelines
- Code coverage requirements
- Pull request guidelines

👉 **See [Development Documentation Index](development/README.md)** for complete details

---

## Documentation Organization

### User-Facing Documentation
**Location**: `/docs/` (this directory)

Documentation for users deploying, configuring, and using Honua:
- [User Guide](user/README.md) - Core user documentation
- [Quick Start](quickstart/) - Getting started guides
- [API Reference](api/) - Complete API docs
- [Configuration](configuration/) - Configuration reference
- [Deployment](deployment/) - Deployment guides
- [MapSDK](mapsdk/) - MapSDK components
- [Operations](operations/) - Day-to-day operations
- [RAG](rag/) - AI-optimized knowledge base

### Development Documentation
**Location**: `/docs/development/`

Documentation for developers working on Honua:
- [Architecture](development/architecture/) - ADRs and system design
- [Testing](development/testing/) - Testing strategies
- [Implementation](development/implementation/) - Feature implementations
- [Refactoring](development/refactoring/) - Refactoring documentation
- [Processes](development/processes/) - Development workflows

### Source Code Documentation
**Location**: `/src/*/README.md`

Component-specific documentation lives alongside source code:
- MapSDK component READMEs
- Core service documentation
- Feature-specific guides

---

## Documentation Stats

- **User Documentation**: 60+ comprehensive guides
  - User guide: 8 core documents
  - Quick start guides: 5+
  - API documentation: 15+ endpoints
  - MapSDK: 30+ component docs
  - Process Framework: 4 operational guides
- **Development Documentation**: 50+ documents
  - ADRs: 14 architectural decisions
  - Testing: 10+ testing guides
  - Implementation: 20+ feature summaries
  - Refactoring: 8 refactoring documents
- **RAG Docs**: 30+ AI-optimized documents (320+ KB)
- **Total Documentation**: 200+ files, 1.5+ MB
- **Code Examples**: 750+
- **API Curl Examples**: 200+
- **Configuration Snippets**: 150+
- **Operational Runbooks**: 8 detailed procedures

## Maintenance

Documentation is kept current with the codebase:
- **Last Major Reorganization**: 2025-11-09
  - Separated user and development documentation
  - Created comprehensive indices and cross-references
  - Organized development docs into logical categories
- **Last Documentation Cleanup**: 2025-11-06
- **Update Policy**: Update when features change
- **Quality Assurance**: All examples tested

## Contributing

### User Documentation
Found an issue or want to improve user-facing documentation?
- Open an issue: [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
- Label: `documentation`
- See [User Documentation Index](user/README.md)

### Development Documentation
Contributing code or improving development docs?
- See [Development Documentation](development/README.md)
- Read [Contributing Guide](../CONTRIBUTING.md)
- Review [Pull Request Guidelines](development/processes/PULL_REQUEST_DESCRIPTION.md)

## Support

- **Documentation Issues**: [GitHub Issues](https://github.com/honua-io/Honua.Server/issues?q=label%3Adocumentation)
- **General Questions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **User Support**: See [Support Documentation](user/support/README.md)
- **Security Issues**: [SECURITY.md](../SECURITY.md)

---

## Navigation Summary

**👤 For Users**: Start at [User Documentation](user/README.md)
**🔧 For Developers**: Start at [Development Documentation](development/README.md)
**🚀 For Quick Start**: Go to [Quick Start Guide](quickstart/README.md)
**🗺️ For MapSDK**: See [MapSDK Documentation](mapsdk/README.md)

---

**Honua Documentation**
Organized, comprehensive, and built with ❤️ for the geospatial community
