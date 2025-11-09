# Honua User Documentation

**Last Updated**: 2025-11-09
**Audience**: Honua Users, System Administrators, API Consumers

Welcome to the Honua user documentation! This guide will help you get started with Honua Server, configure it for your needs, and integrate it into your applications.

## What is Honua?

Honua (Hawaiian for "Earth") is a cloud-native geospatial server that provides:
- **OGC Standards**: WFS, WMS, WMTS, WCS, CSW, OGC API Features/Tiles/Records
- **Modern APIs**: STAC 1.0 catalog, Geoservices REST (Esri-compatible)
- **Multi-Database Support**: PostgreSQL, MySQL, MongoDB, Snowflake, BigQuery, and more
- **Cloud-Native**: Docker, Kubernetes, serverless deployment options
- **Real-Time Features**: GeoEvent Server for geofencing and alerts
- **Rich Exports**: GeoJSON, Shapefile, GeoPackage, KML/KMZ, and 12+ formats

---

## Quick Start

New to Honua? Start here:

1. **[Getting Started](../quickstart/README.md)** - Get Honua running in 5 minutes
2. **[First API Request](../quickstart/README.md#first-api-requests)** - Make your first API call
3. **[Authentication Setup](authentication.md)** - Configure user authentication
4. **[Deploy to Production](../deployment/README.md)** - Production deployment guide

---

## Core User Documentation

### 🔐 Authentication & Security

**[Authentication Guide](authentication.md)**
- Local user authentication with bcrypt
- OAuth 2.0 / OIDC integration
- SAML 2.0 for enterprise SSO
- JWT token validation
- API key authentication
- Role-based access control (RBAC)

**Related:**
- [Security Policy](../SECURITY.md) - Security best practices
- [IAM Setup](../IAM_SETUP.md) - Identity and Access Management

---

### 🌐 API Reference

**[API Endpoints](endpoints.md)**
Complete reference for all Honua endpoints:
- OGC API Features endpoints
- WFS 2.0/3.0 endpoints
- WMS 1.3 endpoints
- STAC catalog endpoints
- Geoservices REST (Esri) endpoints
- Admin API endpoints

**[Admin API](admin-api.md)**
Administrative operations:
- User management
- Layer configuration
- System settings
- Analytics and monitoring
- License management

**Related:**
- [Full API Documentation](../api/README.md)
- [GeoEvent API Guide](../GEOEVENT_API_GUIDE.md)

---

### ⚙️ Configuration

**[Configuration Guide](configuration.md)**
Complete configuration reference:
- Database connections
- Authentication modes
- Cache settings
- Rate limiting
- Storage providers
- Feature flags

**Related:**
- [Configuration Reference](../configuration/README.md) - Detailed config options
- [Rate Limiting](../configuration/rate-limiting.md)
- [Environment Variables](../rag/01-configuration/environment-variables.md)

---

### 📥 Data Ingestion

**[Data Ingestion Guide](data-ingestion.md)**
Loading data into Honua:
- Importing GeoJSON, Shapefile, GeoPackage
- Bulk data loading
- Real-time data ingestion
- OpenROSA/ODK form submission
- Data validation and transformation

**Related:**
- [Metadata Authoring](metadata-authoring.md)
- [Format Matrix](format-matrix.md)

---

### 📄 Metadata & Formats

**[Metadata Authoring](metadata-authoring.md)**
Creating and managing metadata:
- ISO 19115 metadata standards
- STAC catalog metadata
- Layer metadata configuration
- Collection metadata

**[Format Matrix](format-matrix.md)**
Supported data formats:
- Input formats
- Output/export formats
- Format conversion capabilities
- Format-specific limitations

**Related:**
- [Export Formats](../api/README.md#export-formats)
- [STAC Integration](../metadata/STAC_INTEGRATION_FORMALIZATION.md)
- [ISO 19115 Integration](../metadata/ISO_19115_INTEGRATION.md)

---

### 🗺️ Product Roadmap

**[Roadmap](roadmap.md)**
Upcoming features and improvements:
- Planned enhancements
- Feature timeline
- Version history
- Deprecation notices

---

### 🆘 Support

**[Support Documentation](support/README.md)**
Getting help:
- Community support channels
- Issue reporting
- Feature requests
- Commercial support options

**[Privacy Policy](support/privacy.md)**
- Data privacy information
- GDPR compliance
- Data retention policies

---

## Additional Resources

### Platform Components

Honua is part of a comprehensive geospatial platform:

#### 🗺️ MapSDK
**[MapSDK Documentation](../mapsdk/README.md)**
- Visual map builder with no-code editor
- Blazor mapping components
- Interactive dashboards
- Component library

**Quick Links:**
- [MapSDK Getting Started](../mapsdk/getting-started/quick-start.md)
- [MapSDK Tutorials](../mapsdk/tutorials/)
- [MapSDK Component Catalog](../mapsdk/ComponentCatalog.md)

#### ⚡ GeoEvent Server
**[GeoEvent API Guide](../GEOEVENT_API_GUIDE.md)**
- Real-time geofencing (< 100ms latency)
- Batch processing
- Webhook notifications
- State tracking

#### 🔄 GeoETL
**[GeoETL Documentation](../geoetl/README.md)**
- Data transformation pipelines
- Container distribution
- Multi-tenant deployments
- Performance optimization

**Quick Links:**
- [GeoETL Performance Guide](../geoetl/PERFORMANCE_GUIDE.md)
- [GeoETL Error Handling](../geoetl/ERROR_HANDLING.md)

---

### Deployment & Operations

#### 🚀 Deployment

**[Deployment Guide](../deployment/README.md)**
Production deployment options:
- Docker / Docker Compose
- Kubernetes / Helm
- AWS, Azure, GCP
- Serverless platforms

**Quick Links:**
- [Docker Deployment](../DOCKER_DEPLOYMENT.md)
- [Docker Quick Reference](../DOCKER_QUICK_REFERENCE.md)
- [Kubernetes Deployment](../deployment/README.md#kubernetes)
- [Process Framework Deployment](../deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)

#### 📊 Monitoring & Observability

**[Health Checks](../health-checks/README.md)**
- Health check endpoints
- Readiness probes
- Liveness probes

**Operations:**
- [Operations Guide](../operations/README.md)
- [Cache Consistency Guide](../operations/cache-consistency-guide.md)
- [GitOps Security Guide](../operations/gitops-security-guide.md)
- [GitOps Troubleshooting](../operations/gitops-troubleshooting-guide.md)

#### ☁️ Cloud Deployment

**Cloud-Specific Guides:**
- [Azure DNS Configuration](../configuration/AZURE_DNS_CONFIGURATION.md)
- [Azure Backup Policy](../deployment/AZURE_BACKUP_POLICY.md)
- [Azure Restore Procedures](../deployment/AZURE_RESTORE_PROCEDURES.md)
- [Azure Key Vault Recovery](../deployment/AZURE_KEY_VAULT_RECOVERY.md)

#### 🌐 CDN & Performance

**[CDN Deployment](../cdn/CDN_DEPLOYMENT_GUIDE.md)**
- CDN setup and configuration
- Caching policies
- Cache invalidation strategies

**Quick Links:**
- [CDN Caching Policies](../cdn/CDN_CACHING_POLICIES.md)
- [CDN Cache Invalidation](../cdn/CDN_CACHE_INVALIDATION.md)

---

### Advanced Features

#### 🛡️ Security

**[Security Guide](../SECURITY.md)**
- Security best practices
- Vulnerability reporting
- Security scanning
- Compliance

**[Prompt Injection Defense](../PROMPT_INJECTION_DEFENSE.md)**
- AI security considerations
- Input validation
- Prompt injection mitigation

#### ⚡ Performance

**[Resilience](../RESILIENCE.md)**
- Retry policies
- Circuit breakers
- Timeout handling
- Failover strategies

**Database:**
- [PostgreSQL Optimizations](../database/POSTGRESQL_OPTIMIZATIONS.md)
- [SQL Views](../SQL_VIEWS.md)

#### 🌍 Standards & Integration

**[SensorThings Integration](../features/SENSORTHINGS_INTEGRATION.md)**
- OGC SensorThings API
- IoT sensor integration
- Real-time observations

**[Advanced Filtering](../features/ADVANCED_FILTERING_GUIDE.md)**
- CQL2 filtering
- Complex spatial queries
- Temporal filtering

**[Feature Flags](../features/FEATURE_FLAGS_GUIDE.md)**
- Feature flag system
- A/B testing
- Gradual rollouts

**[Auto Discovery](../features/AUTO_DISCOVERY.md)**
- Automatic layer discovery
- Schema introspection
- Dynamic configuration

---

### Licensing & Legal

**[Licensing Strategy](../ELV2_LICENSING_STRATEGY.md)**
- Elastic License 2.0 overview
- Usage terms and restrictions
- Commercial licensing options

**[Licensing Tiers](../LICENSING_TIER_STRATEGY.md)**
- Free tier
- Professional tier
- Enterprise tier
- Feature comparison

**[Third-Party Licenses](../../THIRD-PARTY-LICENSES.md)**
- Open source dependencies
- License compliance
- Attribution

---

## Documentation Organization

### User Documentation (You Are Here)
📍 `/docs/user/` - User-facing documentation
- Authentication, configuration, APIs
- Data ingestion and metadata
- Support and roadmap

### Other Documentation Sections
- 📚 **[Main Docs](../README.md)** - Documentation hub and index
- 🎯 **[Quick Start](../quickstart/)** - 5-minute getting started guides
- 🔌 **[API Reference](../api/)** - Complete API documentation
- ⚙️ **[Configuration](../configuration/)** - Detailed configuration reference
- 🚀 **[Deployment](../deployment/)** - Deployment guides for all platforms
- 🗺️ **[MapSDK](../mapsdk/)** - MapSDK component documentation
- 🤖 **[RAG Docs](../rag/)** - AI-optimized knowledge base

### Development Documentation
- 🔧 **[Development Docs](../development/)** - For developers working on Honua
  - Architecture decision records (ADRs)
  - Implementation details and phase summaries
  - Refactoring plans and metrics
  - Testing strategies and guides
  - Development processes and CI/CD

---

## Getting Help

### Community Support
- **GitHub Discussions**: [Ask questions and share ideas](https://github.com/honua-io/Honua.Server/discussions)
- **GitHub Issues**: [Report bugs or request features](https://github.com/honua-io/Honua.Server/issues)
- **Documentation Issues**: Use label `documentation`

### Commercial Support
- **Email**: support@honua.io
- **Response Times**:
  - Free tier: Community support
  - Professional: 2-day email response
  - Enterprise: 4-hour priority support

### Security Issues
- **Security Policy**: [SECURITY.md](../SECURITY.md)
- **Private Disclosure**: security@honua.io

---

## Contributing to Documentation

Found an issue or want to improve documentation?

1. Check existing [documentation issues](https://github.com/honua-io/Honua.Server/issues?q=label%3Adocumentation)
2. Open a new issue with label `documentation`
3. Provide:
   - Which document has the issue
   - What's incorrect or missing
   - Suggested improvement (if applicable)

**Note**: For contributing code, see [Development Documentation](../development/README.md)

---

## Document Metadata

**Maintained By**: Honua Documentation Team
**Last Review**: 2025-11-09
**Next Review**: 2025-12-09
**Feedback**: [Open an issue](https://github.com/honua-io/Honua.Server/issues/new?labels=documentation)

---

**Honua User Documentation**
Built for the geospatial community ❤️
