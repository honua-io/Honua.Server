# Honua Documentation Guide

**Last Updated**: 2025-11-09
**Purpose**: Help you find the right documentation quickly

This guide helps you navigate Honua's comprehensive documentation system. Whether you're deploying Honua, developing features, or integrating it into your application, this guide will point you to the right resources.

---

## Quick Navigation

### I want to...

#### 🚀 Get Started Quickly
→ **[Quick Start Guide](quickstart/README.md)**
- Get Honua running in 5 minutes
- Make your first API request
- Test basic functionality

#### 👤 Use Honua (Deploy, Configure, Integrate)
→ **[User Documentation](user/README.md)**
- Authentication and security setup
- Configuration reference
- API documentation
- Data ingestion
- Deployment guides

#### 🗺️ Build Maps and Dashboards
→ **[MapSDK Documentation](mapsdk/README.md)**
- Blazor mapping components
- Interactive dashboards
- Tutorials and guides
- Component catalog

#### 🔧 Develop or Contribute to Honua
→ **[Development Documentation](development/README.md)**
- Architecture decision records (ADRs)
- Testing strategies
- Implementation details
- Development processes

#### 🤖 Build AI Assistants with Honua Knowledge
→ **[RAG Documentation](rag/)**
- AI-optimized knowledge base
- Comprehensive technical reference
- 30+ structured documents

---

## Documentation by Audience

### For End Users

**I'm deploying Honua for my organization**
1. Start: [Quick Start](quickstart/README.md)
2. Review: [User Documentation](user/README.md)
3. Configure: [Configuration Guide](configuration/README.md)
4. Deploy: [Deployment Guide](deployment/README.md)
5. Monitor: [Operations Guide](operations/README.md)

**I'm integrating Honua into my application**
1. Review: [API Reference](api/README.md)
2. Authentication: [Authentication Setup](user/authentication.md)
3. Endpoints: [API Endpoints](user/endpoints.md)
4. Examples: [RAG Documentation](rag/) (includes working examples)

**I'm building maps with MapSDK**
1. Install: [MapSDK Installation](mapsdk/getting-started/installation.md)
2. First Map: [Quick Start](mapsdk/getting-started/quick-start.md)
3. Learn: [MapSDK Tutorials](mapsdk/tutorials/)
4. Reference: [Component Catalog](mapsdk/ComponentCatalog.md)

### For System Administrators

**I'm operating Honua in production**
1. Deploy: [Deployment Guide](deployment/README.md)
2. Configure: [Configuration Reference](configuration/README.md)
3. Monitor: [Observability](observability/)
4. Troubleshoot: [Operations Guide](operations/README.md)
5. Secure: [Security Guide](../SECURITY.md)

**I need to set up real-time geofencing**
1. Review: [GeoEvent API Guide](GEOEVENT_API_GUIDE.md)
2. Deploy: [Process Framework Deployment](deployment/PROCESS_FRAMEWORK_DEPLOYMENT.md)
3. Operate: [Process Framework Operations](operations/PROCESS_FRAMEWORK_OPERATIONS.md)
4. Troubleshoot: [Operational Runbooks](operations/RUNBOOKS.md)

### For Developers

**I'm contributing code to Honua**
1. Read: [Development Documentation](development/README.md)
2. Architecture: [ADRs](development/architecture/decisions/)
3. Testing: [Testing Guide](development/testing/QUICKSTART-TESTING.md)
4. Processes: [Development Processes](development/processes/)

**I'm understanding the architecture**
1. Overview: [Architecture Analysis](development/architecture/ARCHITECTURE_ANALYSIS_REPORT.md)
2. Decisions: [Architecture Decision Records](development/architecture/decisions/)
3. Diagrams: [Architecture Diagrams](development/architecture/ARCHITECTURE_DIAGRAMS.md)
4. Codebase: [Codebase Analysis](development/architecture/CODEBASE_IMPROVEMENT_REPORT.md)

**I'm running tests**
1. Quick Start: [Testing Quick Start](development/testing/QUICKSTART-TESTING.md)
2. Integration: [Integration Testing](development/testing/INTEGRATION_TESTING_STRATEGY.md)
3. Parallel: [Parallel Testing](development/testing/PARALLEL_TESTING_GUIDE.md)
4. Database: [Database Testing](development/testing/DATABASE_TESTING_GUIDE.md)

---

## Documentation by Topic

### Authentication & Security
- **[User Authentication](user/authentication.md)** - User auth guide
- **[Security Best Practices](../SECURITY.md)** - Security policy
- **[IAM Setup](IAM_SETUP.md)** - Identity and access management
- **[Prompt Injection Defense](PROMPT_INJECTION_DEFENSE.md)** - AI security

### Configuration & Deployment
- **[Configuration Guide](user/configuration.md)** - User configuration
- **[Configuration Reference](configuration/README.md)** - Detailed reference
- **[Deployment Guide](deployment/README.md)** - Production deployment
- **[Docker Deployment](DOCKER_DEPLOYMENT.md)** - Docker-specific guide
- **[Docker Quick Reference](DOCKER_QUICK_REFERENCE.md)** - Docker commands
- **[Docker Gotchas](DOCKER_GOTCHAS.md)** - Common Docker issues

### APIs & Integration
- **[API Reference](api/README.md)** - Complete API documentation
- **[User Endpoints](user/endpoints.md)** - All API endpoints
- **[Admin API](user/admin-api.md)** - Administration API
- **[GeoEvent API](GEOEVENT_API_GUIDE.md)** - Real-time geofencing

### Data Management
- **[Data Ingestion](user/data-ingestion.md)** - Loading data
- **[Metadata Authoring](user/metadata-authoring.md)** - Creating metadata
- **[Format Matrix](user/format-matrix.md)** - Supported formats
- **[GeoETL](geoetl/)** - Data transformation

### MapSDK
- **[MapSDK Overview](mapsdk/README.md)** - Getting started
- **[Quick Start](mapsdk/getting-started/quick-start.md)** - First map
- **[Tutorials](mapsdk/tutorials/)** - Step-by-step guides
- **[Component Catalog](mapsdk/ComponentCatalog.md)** - All components
- **[Architecture](mapsdk/Architecture.md)** - How it works

### Operations & Monitoring
- **[Operations Guide](operations/README.md)** - Day-to-day operations
- **[Observability](observability/)** - Monitoring and tracing
- **[Health Checks](health-checks/README.md)** - Health endpoints
- **[Process Framework Operations](operations/PROCESS_FRAMEWORK_OPERATIONS.md)** - Process operations
- **[Operational Runbooks](operations/RUNBOOKS.md)** - Incident response

### Cloud & Infrastructure
- **[Azure DNS Configuration](configuration/AZURE_DNS_CONFIGURATION.md)**
- **[Azure Backup Policy](deployment/AZURE_BACKUP_POLICY.md)**
- **[Azure Restore Procedures](deployment/AZURE_RESTORE_PROCEDURES.md)**
- **[CDN Deployment](cdn/CDN_DEPLOYMENT_GUIDE.md)**
- **[CDN Caching](cdn/CDN_CACHING_POLICIES.md)**

### Advanced Features
- **[Advanced Filtering](features/ADVANCED_FILTERING_GUIDE.md)** - Complex queries
- **[Feature Flags](features/FEATURE_FLAGS_GUIDE.md)** - Feature toggles
- **[Auto Discovery](features/AUTO_DISCOVERY.md)** - Automatic layer discovery
- **[SensorThings Integration](features/SENSORTHINGS_INTEGRATION.md)** - IoT sensors

### Performance & Optimization
- **[Resilience](RESILIENCE.md)** - Retry and circuit breaker patterns
- **[PostgreSQL Optimizations](database/POSTGRESQL_OPTIMIZATIONS.md)**
- **[GeoETL Performance](geoetl/PERFORMANCE_GUIDE.md)**
- **[SQL Views](SQL_VIEWS.md)**

### Development Topics
- **[Architecture](development/architecture/)** - System design and ADRs
- **[Testing](development/testing/)** - Test strategies and guides
- **[Implementation](development/implementation/)** - Feature implementations
- **[Refactoring](development/refactoring/)** - Refactoring documentation
- **[Processes](development/processes/)** - CI/CD and workflows

---

## Documentation Structure

```
Honua.Server/
├── README.md                          # Project overview (YOU ARE HERE links to docs)
├── SECURITY.md                        # Security policy
├── CONFIGURATION.md                   # Quick config reference
│
├── docs/                              # Main documentation directory
│   ├── README.md                      # Documentation hub (START HERE)
│   ├── DOCUMENTATION_GUIDE.md         # This file - navigation guide
│   │
│   ├── user/                          # 👤 User documentation
│   │   ├── README.md                  # User docs index
│   │   ├── authentication.md
│   │   ├── configuration.md
│   │   ├── endpoints.md
│   │   ├── admin-api.md
│   │   ├── data-ingestion.md
│   │   ├── metadata-authoring.md
│   │   ├── format-matrix.md
│   │   ├── roadmap.md
│   │   └── support/
│   │
│   ├── development/                   # 🔧 Development documentation
│   │   ├── README.md                  # Development docs index
│   │   ├── architecture/              # ADRs and system design
│   │   ├── testing/                   # Testing strategies
│   │   ├── implementation/            # Feature implementations
│   │   ├── refactoring/               # Refactoring docs
│   │   └── processes/                 # CI/CD and workflows
│   │
│   ├── quickstart/                    # 🚀 Quick start guides
│   ├── api/                           # 📚 API reference
│   ├── configuration/                 # ⚙️ Configuration reference
│   ├── deployment/                    # 🚢 Deployment guides
│   ├── operations/                    # 📊 Operations guides
│   ├── observability/                 # 🔍 Monitoring and tracing
│   ├── mapsdk/                        # 🗺️ MapSDK documentation
│   ├── rag/                           # 🤖 AI knowledge base
│   ├── geoetl/                        # 🔄 GeoETL documentation
│   ├── features/                      # ⚡ Feature-specific docs
│   ├── cdn/                           # 🌐 CDN configuration
│   ├── database/                      # 💾 Database documentation
│   ├── metadata/                      # 📋 Metadata standards
│   └── architecture/                  # 🏗️ User-facing architecture
│
└── src/                               # Source code with inline docs
    ├── Honua.MapSDK/
    │   ├── README.md
    │   └── Components/*/README.md     # Component-specific docs
    └── [other projects]/README.md
```

---

## Finding What You Need

### By File Type

**README files** - Overview and getting started
- [Main README](../README.md) - Project overview
- [Docs README](README.md) - Documentation hub
- [User README](user/README.md) - User guide index
- [Development README](development/README.md) - Development docs index
- [MapSDK README](mapsdk/README.md) - MapSDK overview

**Guides** - Step-by-step instructions
- Quick start guides in [quickstart/](quickstart/)
- Deployment guides in [deployment/](deployment/)
- Operations guides in [operations/](operations/)
- Testing guides in [development/testing/](development/testing/)

**References** - Complete documentation
- API reference in [api/](api/)
- Configuration reference in [configuration/](configuration/)
- Component reference in [mapsdk/](mapsdk/)

**Summaries** - Implementation details (Development)
- Implementation summaries in [development/implementation/](development/implementation/)
- Refactoring summaries in [development/refactoring/](development/refactoring/)
- Phase summaries in [development/implementation/](development/implementation/)

**ADRs** - Architecture decisions (Development)
- All ADRs in [development/architecture/decisions/](development/architecture/decisions/)

---

## Common Questions

**Q: Where do I start if I'm new to Honua?**
A: [Quick Start Guide](quickstart/README.md) → [User Documentation](user/README.md)

**Q: How do I deploy Honua to production?**
A: [Deployment Guide](deployment/README.md) with platform-specific instructions

**Q: Where's the API documentation?**
A: [API Reference](api/README.md) for detailed docs, [User Endpoints](user/endpoints.md) for quick reference

**Q: How do I contribute to Honua?**
A: [Development Documentation](development/README.md) → [Testing Guide](development/testing/QUICKSTART-TESTING.md)

**Q: Where are the architecture decisions documented?**
A: [Architecture Decision Records (ADRs)](development/architecture/decisions/)

**Q: How do I build maps with Honua?**
A: [MapSDK Documentation](mapsdk/README.md) → [Quick Start](mapsdk/getting-started/quick-start.md)

**Q: Where's the GeoEvent (geofencing) documentation?**
A: [GeoEvent API Guide](GEOEVENT_API_GUIDE.md)

**Q: How do I troubleshoot production issues?**
A: [Operational Runbooks](operations/RUNBOOKS.md) for common scenarios

**Q: Where's documentation for developers working on Honua?**
A: All development docs are in [development/](development/), separate from user docs

---

## Getting Help

### For Users
- **Documentation Issues**: [GitHub Issues with `documentation` label](https://github.com/honua-io/Honua.Server/issues?q=label%3Adocumentation)
- **User Questions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **Support**: See [Support Documentation](user/support/README.md)

### For Developers
- **Development Questions**: [Development Documentation](development/README.md)
- **Testing Issues**: [Testing Guide](development/testing/QUICKSTART-TESTING.md)
- **Architecture Questions**: [ADRs](development/architecture/decisions/)

### Security
- **Security Issues**: [SECURITY.md](../SECURITY.md)
- **Private Disclosure**: security@honua.io

---

## Documentation Maintenance

**Maintained By**: Honua Documentation Team
**Last Reorganization**: 2025-11-09
**Update Policy**: Documentation updated with feature changes

Found an issue? [Open a documentation issue](https://github.com/honua-io/Honua.Server/issues/new?labels=documentation)

---

**Honua Documentation Guide**
Your compass for navigating Honua's documentation
