# Honua Enterprise Module Strategy

> **⚠️ OBSOLETE DOCUMENT**
>
> This strategy document is **no longer applicable**. HonuaIO now uses the **Elastic License 2.0** with a **single repository** and **runtime license enforcement**.
>
> **See instead:** [docs/ELV2_LICENSING_STRATEGY.md](../../ELV2_LICENSING_STRATEGY.md)
>
> **Why this changed:**
> - ELv2 allows everything in one repo (no separate FOSS/Enterprise split needed)
> - License validation enforces features at runtime (no plugin NuGet packages needed)
> - Simpler architecture, better trust through source visibility
> - Legal protection from ELv2 prevents hosted service competition
>
> This document is preserved for historical reference only.

---

## Overview

This document outlines the strategy for separating Honua into FOSS (Free and Open Source Software) and Enterprise editions while maintaining clean architecture boundaries and enabling seamless plugin integration.

## Repository Structure

### FOSS Repository (Public)
**Repository:** `HonuaIO` (to be made public)

**Contents:**
- Core OGC API Features server implementation
- Standard data providers (PostgreSQL/PostGIS, MySQL, SQL Server, SQLite)
- Core metadata system
- Standard attachment storage (FileSystem, S3, Azure Blob)
- CLI tooling
- AI Consultant (multi-agent deployment assistant)
- Basic raster tile caching
- Docker Compose deployment templates

**License:** MIT or Apache 2.0

### Enterprise Repository (Private)
**Repository:** `HonuaIO-Enterprise`

**Contents:**
- Enterprise data providers (BigQuery, Cosmos DB, MongoDB, Redshift)
- Advanced security features
- Enhanced monitoring and observability
- Premium support tooling
- Enterprise-specific deployment configurations
- Advanced caching strategies
- Multi-tenancy enhancements

**License:** Proprietary/Commercial

## Architecture Principles

### 1. Plugin-Based Architecture
All data providers implement the `IDataStoreProvider` interface defined in the FOSS core. Enterprise providers are drop-in plugins that:
- Reference the FOSS core as a dependency
- Implement the same interfaces
- Are discovered and loaded via dependency injection
- Require no modifications to the FOSS codebase

### 2. Clean Boundaries
```
┌─────────────────────────────────────┐
│   Honua.Server.Host (FOSS)          │
│   ┌─────────────────────────────┐   │
│   │ Honua.Server.Core (FOSS)    │   │
│   │  - IDataStoreProvider       │◄──┼──┐
│   │  - IMetadataProvider        │   │  │
│   │  - IAttachmentStoreProvider │   │  │
│   └─────────────────────────────┘   │  │
└─────────────────────────────────────┘  │
                                          │
┌─────────────────────────────────────────┼──┐
│ Honua.Server.Enterprise (Private)       │  │
│  ┌─────────────────────────────────┐    │  │
│  │ BigQueryDataStoreProvider       ├────┘  │
│  │ CosmosDbDataStoreProvider       │       │
│  │ MongoDbDataStoreProvider        │       │
│  │ RedshiftDataStoreProvider       │       │
│  └─────────────────────────────────┘       │
└──────────────────────────────────────────

────┘
```

### 3. Dependency Flow
- **FOSS Core**: No dependencies on Enterprise modules
- **Enterprise Modules**: Depend on FOSS Core interfaces
- **Host Application**: Conditionally loads Enterprise modules if present

## Project Structure

### Current (FOSS)
```
src/
├── Honua.Server.Core/          # Core interfaces and FOSS providers
│   ├── Data/
│   │   ├── IDataStoreProvider.cs
│   │   ├── Postgres/
│   │   ├── MySql/
│   │   ├── SqlServer/
│   │   └── Sqlite/
│   ├── Metadata/
│   └── Attachments/
├── Honua.Server.Host/          # ASP.NET Core host
└── Honua.Cli/                  # CLI tooling
```

### New Enterprise Addition
```
src/
└── Honua.Server.Enterprise/    # Enterprise-only providers
    ├── Data/
    │   ├── BigQuery/
    │   │   ├── BigQueryDataStoreProvider.cs
    │   │   ├── BigQueryQueryBuilder.cs
    │   │   └── BigQueryCapabilities.cs
    │   ├── CosmosDb/
    │   │   ├── CosmosDbDataStoreProvider.cs
    │   │   ├── CosmosDbQueryBuilder.cs
    │   │   └── CosmosDbCapabilities.cs
    │   ├── MongoDB/
    │   │   ├── MongoDbDataStoreProvider.cs
    │   │   ├── MongoDbQueryBuilder.cs
    │   │   └── MongoDbCapabilities.cs
    │   └── Redshift/
    │       ├── RedshiftDataStoreProvider.cs
    │       ├── RedshiftQueryBuilder.cs
    │       └── RedshiftCapabilities.cs
    ├── Attachments/
    │   └── GcsAttachmentStoreProvider.cs
    └── DependencyInjection/
        └── EnterpriseServiceCollectionExtensions.cs
```

## Implementation Strategy

### Phase 1: Prepare FOSS Core (Current Work)
1. ✅ Ensure all interfaces are well-defined in `Honua.Server.Core`
2. ✅ Document plugin architecture
3. ✅ Create comprehensive tests for existing providers
4. Create plugin discovery mechanism (if not already present)

### Phase 2: Create Enterprise Module
1. Create `Honua.Server.Enterprise` project
2. Add NuGet package references for:
   - `Google.Cloud.BigQuery.V2`
   - `Microsoft.Azure.Cosmos`
   - `MongoDB.Driver`
   - `AWSSDK.Redshift`
   - `AWSSDK.RedshiftDataAPIService`
3. Implement each provider following FOSS patterns

### Phase 3: Repository Split
1. Clean commit history in preparation for public release
2. Remove any sensitive data or credentials
3. Update documentation for public consumption
4. Create new private repository for Enterprise modules
5. Set up CI/CD for both repositories

### Phase 4: Distribution
1. Publish FOSS version to GitHub
2. Distribute Enterprise modules as NuGet packages (private feed)
3. Create unified documentation site
4. Establish versioning strategy (semantic versioning aligned across both)

## Provider Implementation Guidelines

Each enterprise data provider must:

1. **Implement `IDataStoreProvider`**
   ```csharp
   public sealed class BigQueryDataStoreProvider : IDataStoreProvider
   {
       public string Provider => "bigquery";
       public IDataStoreCapabilities Capabilities => BigQueryCapabilities.Instance;
       // ... implement all methods
   }
   ```

2. **Define Provider-Specific Capabilities**
   ```csharp
   public sealed class BigQueryCapabilities : IDataStoreCapabilities
   {
       public static BigQueryCapabilities Instance { get; } = new();
       public bool SupportsSpatialQueries => true;
       public bool SupportsTransactions => false; // BigQuery doesn't support traditional transactions
       // ...
   }
   ```

3. **Follow Existing Patterns**
   - Connection pooling/management
   - Query building abstraction
   - Proper async/await patterns
   - Comprehensive error handling
   - Spatial data transformations

4. **Include Tests**
   - Unit tests for query builders
   - Integration tests (optional, can use emulators)
   - Performance benchmarks

## Data Provider Specifics

### BigQuery
- **Use Case**: Analytics on massive geospatial datasets
- **Key Features**: GIS functions via BigQuery GIS
- **Considerations**: Optimized for column-oriented queries, not transactional

### Cosmos DB
- **Use Case**: Globally distributed, low-latency access
- **Key Features**: Native GeoJSON support
- **Considerations**: Geospatial indexing, partition key strategy

### MongoDB
- **Use Case**: Flexible schema, document-oriented
- **Key Features**: Geospatial indexes (2dsphere)
- **Considerations**: Aggregation pipeline for complex queries

### Redshift
- **Use Case**: Data warehouse analytics
- **Key Features**: PostGIS-compatible spatial functions
- **Considerations**: Designed for batch operations, not real-time

## Licensing Considerations

### FOSS Components
- Permissive license (MIT/Apache 2.0)
- No restrictions on commercial use
- Clear attribution requirements

### Enterprise Components
- Proprietary license
- Per-instance or per-core licensing
- Support and maintenance included
- Clear upgrade path from FOSS

## Migration Path

Users can start with FOSS and upgrade to Enterprise:

1. **Start FOSS**: Deploy with PostgreSQL/PostGIS
2. **Add Enterprise**: Add Enterprise NuGet package reference
3. **Configure**: Update metadata to use `bigquery` provider
4. **Deploy**: No code changes required, just configuration

Example metadata change:
```json
{
  "dataSources": {
    "my-bigquery-data": {
      "provider": "bigquery",  // Changed from "postgis"
      "connectionString": "ProjectId=my-project;..."
    }
  }
}
```

## Next Steps

1. ✅ Document current architecture
2. 🔄 Create `Honua.Server.Enterprise` project
3. ⏳ Implement BigQuery provider
4. ⏳ Implement Cosmos DB provider
5. ⏳ Implement MongoDB provider
6. ⏳ Implement Redshift provider
7. ⏳ Create integration tests
8. ⏳ Document enterprise deployment

## Security Considerations

- Enterprise repository access control (private GitHub/Azure DevOps)
- NuGet package signing for Enterprise modules
- Secure credential management (Azure Key Vault, AWS Secrets Manager)
- Audit logging for enterprise features
- Regular security scanning for both FOSS and Enterprise codebases
