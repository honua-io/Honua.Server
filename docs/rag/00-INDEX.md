# Honua Documentation Index for RAG

This directory contains comprehensive Honua documentation optimized for Retrieval-Augmented Generation (RAG).

## Document Organization

### Configuration (`01-configuration/`)
- `environment-variables.md` - Complete environment variable reference (18 KB)
- `oauth-setup.md` - OAuth/OIDC authentication with Azure AD, Auth0, Okta, Keycloak (26 KB)
- `data-providers.md` - PostGIS, SQL Server, SQLite, MySQL, Redis configuration (41 KB)
- Metadata provider configuration
- Storage backend configuration

### Deployment (`02-deployment/`)
- `docker-deployment.md` - Docker and Docker Compose deployment (22 KB)
- `kubernetes-deployment.md` - Kubernetes with HPA and Helm charts (28 KB)
- `aws-ecs-deployment.md` - AWS ECS Fargate and CloudFormation (28 KB)
- `reverse-proxy-ssl.md` - Nginx, Caddy, Traefik with Let's Encrypt SSL (52 KB)
- `service-endpoints.md` - Complete API reference (OGC, WFS, WMS, Geoservices REST a.k.a. Esri REST, OData, STAC) (69 KB)
- Azure deployment options
- Local development setup

### Architecture (`03-architecture/`)
- `stac-catalog.md` - STAC catalog for raster data management (17 KB)
- `metadata-schema.md` - Complete metadata JSON schema and configuration (44 KB)
- `map-styling.md` - Styling, symbolization, and cartography (42 KB)
- `tile-caching.md` - Tile caching, MVT, raster tiles, and CDN (37 KB)
- System design and components
- OGC API Features implementation
- Plugin system design
- Database schema

### Operations (`04-operations/`)
- `troubleshooting.md` - Comprehensive diagnostic workflows and solutions (26 KB)
- `performance-tuning.md` - Performance optimization strategies (24 KB)
- `backup-disaster-recovery.md` - Backup strategies, PITR, DR procedures (32 KB)
- `monitoring-observability.md` - Prometheus, Grafana, alerting, tracing (72 KB)

### Development (`05-development/`)
- `integration-testing.md` - LocalStack, Minikube/kind, CI/CD integration (66 KB)
- `deployment-documentation.md` - Architecture tracking, runbooks, documentation automation (35 KB)
- Building from source
- Running tests
- Contributing guidelines
- Plugin development
- API extensions

## RAG Integration

These documents are structured for optimal semantic search:

- **Clear headings**: Each section has descriptive titles
- **Context-rich**: Includes related concepts and cross-references
- **Example-heavy**: Concrete code samples and configurations
- **Searchable metadata**: Tags and keywords for common queries

## Usage with Semantic Kernel

The `DocumentationSearchPlugin` provides RAG capabilities:

```csharp
var kernel = builder.Build();
kernel.ImportPluginFromType<DocumentationSearchPlugin>();

// Search is automatically available to the planner
var result = await kernel.InvokeAsync("DocumentationSearch",
    new() { ["query"] = "How do I configure PostGIS?" });
```

## Document Format

Each document follows this structure:

```markdown
# Topic Title

**Keywords**: keyword1, keyword2, keyword3
**Related**: RelatedTopic1, RelatedTopic2

## Overview
Brief introduction with key concepts

## Common Use Cases
- Use case 1
- Use case 2

## Configuration
Detailed configuration options

## Examples
Concrete examples with explanations

## Troubleshooting
Common issues and solutions

## See Also
- Related documentation
- External resources
```

## Maintenance

To update RAG documentation:

1. Edit markdown files in this directory
2. Run `dotnet run --project tools/DocIndexer` to rebuild search index
3. Restart services to pick up changes

## Current Documentation Coverage

**Total RAG Documentation**: 687 KB across 19 comprehensive markdown files

### By Category
- **Configuration** (3 files, 85 KB): Environment variables, OAuth/OIDC, data providers
- **Deployment** (5 files, 199 KB): Docker, Kubernetes, AWS ECS, reverse proxy/SSL, service endpoints
- **Architecture** (4 files, 140 KB): STAC catalog, metadata schema, map styling, tile caching
- **Operations** (4 files, 154 KB): Troubleshooting, performance, backup/DR, monitoring/observability
- **Development** (2 files, 101 KB): Integration testing, deployment documentation

### Documentation Quality
- ✅ Production-ready deployment examples
- ✅ Step-by-step configuration guides
- ✅ Real-world troubleshooting scenarios
- ✅ Performance optimization strategies
- ✅ Security best practices (OAuth, SSL/TLS, headers)
- ✅ Complete API reference for all protocols (OGC, WFS, WMS, Geoservices REST a.k.a. Esri REST, OData, STAC)
- ✅ Complete data provider configuration (PostGIS, SQL Server, SQLite, MySQL, Redis)
- ✅ Metadata schema and service configuration
- ✅ Map styling and symbolization
- ✅ Tile caching and CDN integration
- ✅ Backup, disaster recovery, and PITR procedures
- ✅ Monitoring, observability, and alerting (Prometheus, Grafana, tracing)
- ✅ Integration testing strategies (LocalStack, Minikube/kind, CI/CD)
- ✅ Deployment documentation and architecture tracking
- ✅ Cross-referenced topics

Last Updated: 2025-10-04
