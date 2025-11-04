# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records (ADRs) documenting significant architectural and design decisions made in the Honua project.

## What are ADRs?

Architecture Decision Records capture important architectural decisions along with their context and consequences. They help teams:
- Understand why certain design choices were made
- Avoid re-litigating past decisions
- Onboard new team members effectively
- Track the evolution of the architecture over time

## ADR Index

### Meta

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record Architecture Decisions | Accepted | 2025-10-17 |

### Data & Storage

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0002](0002-use-postgresql-as-primary-database.md) | Use PostgreSQL as Primary Database | Accepted | 2025-10-17 |
| [0004](0004-multi-database-provider-pattern.md) | Multi-Database Provider Pattern | Accepted | 2025-10-17 |
| [0009](0009-redis-distributed-state.md) | Redis for Distributed State Stores | Accepted | 2025-10-17 |
| [0014](0014-multi-cloud-object-storage.md) | Multi-Cloud Object Storage Support | Accepted | 2025-10-17 |

### Raster Processing

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0003](0003-pure-dotnet-raster-readers.md) | Pure .NET Raster Readers (No GDAL Dependency) | Accepted | 2025-10-17 |
| [0013](0013-hybrid-cog-zarr-architecture.md) | Hybrid COG + Zarr Raster Architecture | Accepted | 2025-10-17 |

### Reliability & Observability

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0005](0005-opentelemetry-for-observability.md) | OpenTelemetry for Observability | Accepted | 2025-10-17 |
| [0006](0006-polly-for-resilience.md) | Polly for Resilience Policies | Accepted | 2025-10-17 |

### AI & Orchestration

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0007](0007-semantic-kernel-for-ai-orchestration.md) | Semantic Kernel for AI Orchestration | Accepted | 2025-10-17 |

### API & Standards

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0008](0008-ogc-api-standards-compliance.md) | OGC API Standards Compliance | Accepted | 2025-10-17 |
| [0011](0011-aspnet-core-middleware-pipeline.md) | ASP.NET Core Middleware-based Request Pipeline | Accepted | 2025-10-17 |

### Security

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0010](0010-jwt-oidc-authentication.md) | JWT + OIDC Authentication Strategy | Accepted | 2025-10-17 |

### Deployment

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0012](0012-docker-first-deployment.md) | Docker-First Deployment Strategy | Accepted | 2025-10-17 |

## ADR Statuses

- **Proposed**: Decision under discussion, not yet adopted
- **Accepted**: Decision approved and being/has been implemented
- **Deprecated**: Decision no longer relevant but kept for historical context
- **Superseded**: Decision replaced by a newer ADR (link to replacement)

## Decision Categories

ADRs are organized into the following categories:

### Data & Storage
Decisions about databases, data persistence, caching, and storage systems.
- Primary database choice (PostgreSQL)
- Multi-database support pattern
- Distributed state stores (Redis)
- Object storage abstraction

### Raster Processing
Decisions specific to raster data processing and storage.
- Pure .NET vs GDAL approach
- COG and Zarr format strategy
- Cloud-optimized storage patterns

### Reliability & Observability
Decisions about system reliability, monitoring, and operational excellence.
- Observability framework (OpenTelemetry)
- Resilience policies (Polly)
- Distributed tracing strategy

### AI & Orchestration
Decisions about AI capabilities and workflow orchestration.
- AI framework choice (Semantic Kernel)
- Process orchestration patterns
- Multi-agent coordination

### API & Standards
Decisions about API design and standards compliance.
- OGC API compliance
- REST API architecture
- Middleware pipeline design

### Security
Decisions about authentication, authorization, and security practices.
- Authentication strategy (JWT/OIDC)
- Role-based access control
- Security modes

### Deployment
Decisions about deployment strategies and operational practices.
- Containerization strategy (Docker)
- Cloud platform support
- Infrastructure as code

## Writing New ADRs

### When to Write an ADR

Write an ADR when making decisions about:
- Technology stack choices (databases, frameworks, libraries)
- Architectural patterns (layering, modularity, communication)
- Security and authentication approaches
- Data storage and persistence strategies
- API design principles
- Deployment and operational strategies
- Significant refactoring decisions

### When NOT to Write an ADR

Do not write ADRs for:
- Minor implementation details
- Tactical code organization
- Bug fixes
- Feature additions that follow existing patterns

### ADR Template

Use the MADR (Markdown Any Decision Records) template:

```markdown
# [number]. [Title]

Date: YYYY-MM-DD

Status: [Proposed | Accepted | Deprecated | Superseded]

## Context

What is the issue we're addressing? What constraints exist?

## Decision

What decision did we make? Be specific and actionable.

## Consequences

### Positive
- What benefits does this decision provide?

### Negative
- What are the downsides or limitations?

### Neutral
- What are the trade-offs or neutral aspects?

## Alternatives Considered

### 1. Alternative Name

**Pros:** What's good about this alternative?

**Cons:** What's bad about this alternative?

**Verdict:** Why was this rejected?

## Implementation Details

Code examples, configuration, specific details about implementation.

## Code References

- Links to relevant files in the codebase

## References

- Links to external documentation, specifications, or resources

## Notes

Additional context, future considerations, or migration notes.
```

### Process

1. **Draft**: Create a new ADR in `/docs/architecture/decisions/` with the next sequential number
2. **Discuss**: Share with the team for feedback (PR review)
3. **Decide**: Team approves or requests changes
4. **Accept**: Merge the ADR with status "Accepted"
5. **Implement**: Reference the ADR in implementation PRs
6. **Update Index**: Add to this README in the appropriate category

### Numbering

ADRs are numbered sequentially starting from 0001. Use leading zeros (e.g., `0001`, `0042`, `0143`) for consistent sorting.

### File Naming

Format: `XXXX-short-title-in-kebab-case.md`

Examples:
- `0001-record-architecture-decisions.md`
- `0002-use-postgresql-as-primary-database.md`
- `0003-pure-dotnet-raster-readers.md`

## Superseding Decisions

When a decision changes:

1. Do NOT modify the original ADR (it's historical record)
2. Create a new ADR documenting the new decision
3. Update the old ADR's status to "Superseded by ADR-XXXX"
4. Link the old and new ADRs bidirectionally

Example:
```markdown
# 42. Use MongoDB

Status: Superseded by [ADR-0087](0087-return-to-postgresql.md)
```

## Deprecated Decisions

When a decision is no longer relevant:

1. Update the ADR's status to "Deprecated"
2. Add a note explaining why it's deprecated
3. Keep the ADR for historical context

## Finding Related Code

Many ADRs include "Code References" sections linking to relevant implementation files. Use these to understand how decisions are implemented in practice.

## Questions?

If you have questions about:
- **ADR process**: See [ADR-0001](0001-record-architecture-decisions.md)
- **Specific decisions**: Read the relevant ADR and reach out to the team
- **New ADRs**: Follow the template and propose in a PR

## Archived ADRs

Previous ADRs from before the ADR process was formalized can be found in:
- `/docs/archive/2025-10-15/architecture/`

These have been superseded by the current ADRs but are preserved for historical reference.

## Statistics

**Total ADRs**: 14
**Accepted**: 14
**Proposed**: 0
**Deprecated**: 0
**Superseded**: 0

**Last Updated**: 2025-10-17

---

> "Architecture is the decisions that you wish you could get right early in a project, but that you are not necessarily more likely to get them right than any other." - Ralph Johnson

