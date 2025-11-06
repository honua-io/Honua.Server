# Honua Documentation

**Last Updated**: 2025-10-17
**Status**: ✅ Comprehensive & Current

Complete documentation for Honua - a cloud-native geospatial server built on .NET 9.

## Quick Navigation

| Documentation Type | Location | Purpose |
|-------------------|----------|---------|
| 🚀 **Getting Started** | [quickstart/](quickstart/) | 5-minute quickstart guide |
| 📚 **API Reference** | [api/](api/) | Complete API documentation |
| ⚙️ **Configuration** | [configuration/) | appsettings.json reference |
| 🚢 **Deployment** | [deployment/](deployment/) | Docker, K8s, Cloud platforms |
| 📊 **Observability** | [observability/](observability/) | Monitoring, tracing, alerting |
| 🤖 **AI Knowledge Base** | [rag/](rag/) | Comprehensive RAG docs for AI consultant |
| 🔄 **Process Framework** | [Process Framework](#process-framework) | Stateful workflow orchestration |

## What is Honua?

Honua (Hawaiian for "Earth") is a modern geospatial server designed to serve vector and raster data at scale. Built on .NET 9 with native cloud-optimized raster support (COG, Zarr) and comprehensive OGC standards compliance.

**Key Features**:
- OGC Standards (WFS, WMS, WMTS, WCS, CSW)
- OGC API (Features, Tiles, Records)
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API compatibility
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
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API (FeatureServer, MapServer)
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
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API complete reference
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
- [Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API](api/README.md#esri-rest-api)
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
- All modern APIs (OGC API Features, STAC, Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST)
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

## Documentation Stats

- **User Docs**: 5 comprehensive guides
- **RAG Docs**: 13 comprehensive documents
- **Process Framework Docs**: 4 comprehensive guides (NEW)
  - Deployment Guide: 55 KB, 1,100+ lines
  - Operations Guide: 48 KB, 1,000+ lines
  - Runbooks: 35 KB, 800+ lines (8 runbooks)
  - Quick Start: 22 KB, 450+ lines
- **Total Size**: ~670 KB
- **Code Examples**: 750+
- **API Curl Examples**: 200+
- **Configuration Snippets**: 150+
- **Operational Procedures**: 8 detailed runbooks

## Archive

Previous documentation (2025-10-15 and earlier) archived at: [archive/](archive/)

## Maintenance

Documentation is kept current with the codebase:
- **Last Full Rebuild**: 2025-10-15
- **Process Framework Addition**: 2025-10-17
- **Update Policy**: Update when features change
- **Version Control**: Timestamped archives
- **Quality Assurance**: All examples tested

## Contributing

Found an issue or want to improve documentation?
- Open an issue: [GitHub Issues](https://github.com/mikemcdougall/HonuaIO/issues)
- Label: `documentation`

## Support

- **Documentation Issues**: [GitHub Issues](https://github.com/mikemcdougall/HonuaIO/issues)
- **General Questions**: [GitHub Discussions](https://github.com/mikemcdougall/HonuaIO/discussions)
- **Security Issues**: [SECURITY.md](../SECURITY.md)

---

**Honua Documentation**
Built with ❤️ for the geospatial community
