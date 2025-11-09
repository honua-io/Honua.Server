# Honua Development Documentation

**Last Updated**: 2025-11-09
**Audience**: Developers, Contributors, Internal Team

This directory contains documentation for developers working on Honua Server. If you're looking for user-facing documentation, please see [/docs/README.md](../README.md).

## Quick Navigation

| Documentation Type | Location | Purpose |
|-------------------|----------|---------|
| 🏗️ **Architecture** | [architecture/](architecture/) | System design, ADRs, architecture analysis |
| 🧪 **Testing** | [testing/](testing/) | Testing guides, strategies, and best practices |
| 📝 **Implementation** | [implementation/](implementation/) | Feature implementations, phase summaries |
| 🔄 **Refactoring** | [refactoring/](refactoring/) | Refactoring plans, summaries, and metrics |
| ⚙️ **Processes** | [processes/](processes/) | Development processes, CI/CD, contributing |

---

## Architecture Documentation

### [architecture/](architecture/)

**System Design & Decision Records**

#### Core Architecture
- [Architecture Diagrams](architecture/ARCHITECTURE_DIAGRAMS.md) - Visual system architecture
- [Architecture Analysis Report](architecture/ARCHITECTURE_ANALYSIS_REPORT.md) - Comprehensive architecture review
- [Codebase Improvement Report](architecture/CODEBASE_IMPROVEMENT_REPORT.md) - Code quality improvements
- [Client 3D Architecture](architecture/CLIENT_3D_ARCHITECTURE.md) - 3D client architecture
- [UI Architecture Overview](../UI_ARCHITECTURE_OVERVIEW.md) - UI/Frontend architecture (root level)

#### Architecture Decision Records (ADRs)
Location: [architecture/decisions/](architecture/decisions/)

All architectural decisions are documented using the ADR format:

1. [Record Architecture Decisions](architecture/decisions/0001-record-architecture-decisions.md)
2. [Use PostgreSQL as Primary Database](architecture/decisions/0002-use-postgresql-as-primary-database.md)
3. [Pure .NET Raster Readers](architecture/decisions/0003-pure-dotnet-raster-readers.md)
4. [Multi-Database Provider Pattern](architecture/decisions/0004-multi-database-provider-pattern.md)
5. [OpenTelemetry for Observability](architecture/decisions/0005-opentelemetry-for-observability.md)
6. [Polly for Resilience](architecture/decisions/0006-polly-for-resilience.md)
7. [Semantic Kernel for AI Orchestration](architecture/decisions/0007-semantic-kernel-for-ai-orchestration.md)
8. [OGC API Standards Compliance](architecture/decisions/0008-ogc-api-standards-compliance.md)
9. [Redis for Distributed State](architecture/decisions/0009-redis-distributed-state.md)
10. [JWT/OIDC Authentication](architecture/decisions/0010-jwt-oidc-authentication.md)
11. [ASP.NET Core Middleware Pipeline](architecture/decisions/0011-aspnet-core-middleware-pipeline.md)
12. [Docker-First Deployment](architecture/decisions/0012-docker-first-deployment.md)
13. [Hybrid COG/Zarr Architecture](architecture/decisions/0013-hybrid-cog-zarr-architecture.md)
14. [Multi-Cloud Object Storage](architecture/decisions/0014-multi-cloud-object-storage.md)

**Template**: Use [decisions/TEMPLATE.md](architecture/decisions/TEMPLATE.md) for new ADRs

---

## Testing Documentation

### [testing/](testing/)

**Comprehensive Testing Guides**

#### Getting Started with Testing
- [Testing Quick Start](testing/QUICKSTART-TESTING.md) - Quick start guide for running tests
- [Testing Guide](testing/TESTING.md) - Comprehensive testing documentation
- [README.TESTING.md](testing/README.TESTING.md) - Testing setup and best practices

#### Integration & Advanced Testing
- [Integration Testing Strategy](testing/INTEGRATION_TESTING_STRATEGY.md) - Integration test approach
- [Run Integration Tests](testing/RUN_INTEGRATION_TESTS.md) - How to run integration tests
- [Database Testing Guide](testing/DATABASE_TESTING_GUIDE.md) - Database-specific testing

#### Parallel Testing
- [Parallel Testing Architecture](testing/PARALLEL_TESTING_ARCHITECTURE.md) - Parallel test architecture
- [Parallel Testing Guide](testing/PARALLEL_TESTING_GUIDE.md) - Detailed parallel testing guide
- [Parallel Testing Quick Reference](testing/PARALLEL_TESTING_QUICKREF.md) - Quick commands
- [Test Split Quick Reference](testing/TEST_SPLIT_QUICK_REFERENCE.md) - Test splitting

#### Benchmarking
- [Benchmarks](testing/BENCHMARKS.md) - Performance benchmarking guide

---

## Implementation Documentation

### [implementation/](implementation/)

**Feature Implementation & Phase Summaries**

#### Core Implementations
- [Implementation Summary](implementation/IMPLEMENTATION_SUMMARY.md) - Overall implementation overview
- [Implementation Verification](implementation/IMPLEMENTATION_VERIFICATION.md) - Verification procedures
- [Scheduling Implementation](implementation/IMPLEMENTATION_SUMMARY_SCHEDULING.md) - Scheduling framework
- [Export Implementation Guide](implementation/EXPORT_IMPLEMENTATION_GUIDE.md) - Export functionality

#### GeoETL Implementations
- [GeoETL AI Implementation](implementation/GEOETL_AI_IMPLEMENTATION_SUMMARY.md) - AI features
- [GeoETL Integration Tests](implementation/GEOETL_INTEGRATION_TESTS_SUMMARY.md) - Test coverage
- [GeoETL Template Library](implementation/GEOETL_TEMPLATE_LIBRARY_SUMMARY.md) - Template system

#### Feature Implementations
- [Advanced Filtering Integration](implementation/ADVANCED_FILTERING_INTEGRATION.md)
- [Advanced Filtering Summary](implementation/ADVANCED_FILTERING_SUMMARY.md)
- [AI Consultant Review](implementation/AI_CONSULTANT_REVIEW.md)
- [AI Consultant Fixes Summary](implementation/AI_CONSULTANT_FIXES_SUMMARY.md)
- [Layer Groups Implementation](implementation/layer-groups-implementation.md)

#### 3D, VR & Drone Features
- [3D Support](implementation/3D_SUPPORT.md) - 3D rendering capabilities
- [Blazor 3D Interop Performance](implementation/BLAZOR_3D_INTEROP_PERFORMANCE.md)
- [Terrain Visualization](implementation/TERRAIN_VISUALIZATION.md)
- [VR Headset Integration](implementation/VR_HEADSET_INTEGRATION.md)
- [Meta Quest Index](implementation/META_QUEST_INDEX.md)
- [Meta Quest Quick Reference](implementation/META_QUEST_QUICK_REFERENCE.md)
- [Meta Quest VR/AR Integration Analysis](implementation/META_QUEST_VR_AR_INTEGRATION_ANALYSIS.md)
- [Drone Data Implementation](implementation/DRONE_DATA_IMPLEMENTATION.md)
- [Drone Data Integration](implementation/DRONE_DATA_INTEGRATION.md)

#### Phase Completion Summaries
- [Phase 1 Summary](implementation/PHASE_1_SUMMARY.md)
- [Phase 2 Completion Summary](implementation/PHASE_2_COMPLETION_SUMMARY.md)
- [Phase 3 Completion Summary](implementation/PHASE_3_COMPLETION_SUMMARY.md)
- [Phase 4 Completion Summary](implementation/PHASE_4_COMPLETION_SUMMARY.md)

---

## Refactoring Documentation

### [refactoring/](refactoring/)

**Refactoring Plans, Metrics & Reports**

- [Refactoring Plan](refactoring/REFACTORING_PLAN.md) - Overall refactoring strategy
- [OGC Refactoring Plan](refactoring/REFACTORING_PLAN_OGC.md) - OGC-specific refactoring
- [Refactoring Summary](refactoring/REFACTORING_SUMMARY.md) - High-level summary
- [Refactoring Implementation](refactoring/REFACTORING_IMPLEMENTATION.md) - Implementation details
- [Refactoring Before/After](refactoring/REFACTORING_BEFORE_AFTER.md) - Code comparisons
- [Refactoring Completion Report](refactoring/REFACTORING_COMPLETION_REPORT.md) - Final report
- [Refactoring Executive Summary](refactoring/REFACTORING_EXECUTIVE_SUMMARY.md) - Executive overview
- [Refactoring Metrics Final](refactoring/REFACTORING_METRICS_FINAL.md) - Final metrics

---

## Development Processes

### [processes/](processes/)

**Contributing, CI/CD & Development Workflows**

#### Code Quality
- [Code Coverage](processes/CODE_COVERAGE.md) - Coverage requirements and tools
- [Benchmarks](testing/BENCHMARKS.md) - Performance benchmarking (see Testing)

#### Development Workflows
- [CI/CD](processes/CI_CD.md) - Continuous integration and deployment
- [Pull Request Description](processes/PULL_REQUEST_DESCRIPTION.md) - PR templates and standards

---

## Relationship to User Documentation

**This is DEVELOPMENT documentation** - meant for people working on Honua Server itself.

For **USER documentation** (deploying, configuring, using Honua Server), see:
- [User Documentation Index](../README.md) - Main documentation hub
- [Quick Start](../quickstart/) - Getting started guides
- [API Reference](../api/) - API documentation
- [Deployment](../deployment/) - Deployment guides
- [Configuration](../configuration/) - Configuration reference

---

## Contributing

Before contributing, please read:
1. User-facing [CONTRIBUTING.md](../../CONTRIBUTING.md) in project root (if exists)
2. [Pull Request Description](processes/PULL_REQUEST_DESCRIPTION.md) - PR standards
3. [Code Coverage](processes/CODE_COVERAGE.md) - Quality requirements

---

## Documentation Standards

### When to Create Development Documentation

Create development documentation for:
- **Architecture Decisions** - Use ADR format in [architecture/decisions/](architecture/decisions/)
- **Refactoring Plans** - Document in [refactoring/](refactoring/)
- **Implementation Notes** - Complex feature implementations in [implementation/](implementation/)
- **Testing Strategies** - Testing approaches in [testing/](testing/)
- **Process Changes** - Development workflow changes in [processes/](processes/)

### What Belongs in User Documentation

Move to [/docs/](../README.md) if documentation covers:
- How to deploy/configure/use Honua
- API reference for users
- Troubleshooting user issues
- User-facing features and capabilities

---

## Quick Links

### Architecture & Design
- [ADR Index](architecture/decisions/README.md)
- [Architecture Diagrams](architecture/ARCHITECTURE_DIAGRAMS.md)
- [Architecture Analysis](architecture/ARCHITECTURE_ANALYSIS_REPORT.md)

### Testing
- [Quick Start Testing](testing/QUICKSTART-TESTING.md)
- [Integration Tests](testing/RUN_INTEGRATION_TESTS.md)
- [Parallel Testing](testing/PARALLEL_TESTING_GUIDE.md)

### Implementation
- [Implementation Summary](implementation/IMPLEMENTATION_SUMMARY.md)
- [Phase Summaries](implementation/)
- [Feature Implementations](implementation/)

### Processes
- [CI/CD Pipeline](processes/CI_CD.md)
- [Code Coverage](processes/CODE_COVERAGE.md)
- [PR Guidelines](processes/PULL_REQUEST_DESCRIPTION.md)

---

**Honua Development Documentation**
For internal development team use
