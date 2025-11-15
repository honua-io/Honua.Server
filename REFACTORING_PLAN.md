# Architectural Refactoring Plan

**Project:** Honua.Server
**Date:** 2025-11-15
**Based on:** Architecture Patterns Compliance Report
**Timeline:** 12 months (4 phases)

---

## Executive Summary

This refactoring plan addresses the critical architectural gaps identified in the comprehensive patterns compliance review. The plan is organized into four phases over 12 months, prioritized by impact and dependencies.

**Primary Goals:**
1. Transform anemic domain model into rich domain model with DDD tactical patterns
2. Improve scalability through CQRS and leader election
3. Enhance maintainability by refactoring large classes and consolidating configuration
4. Strengthen reliability with event sourcing and Saga pattern

**Total Estimated Effort:** ~52 weeks (1 year)
**Team Size:** 3-4 senior developers
**Risk Level:** Medium (primarily additive changes, limited breaking changes)

---

## Phase 1: Foundation & Critical Fixes (Weeks 1-12)

**Goals:** Address critical issues, establish DDD foundation, unblock future improvements

### 1.1 DDD Foundation Infrastructure (Weeks 1-4)

**Objective:** Create base classes and infrastructure for rich domain model

**Tasks:**

1. **Create Base Domain Classes** (Week 1)
   ```
   Files to create:
   - src/Honua.Server.Core/Domain/Entity.cs
   - src/Honua.Server.Core/Domain/ValueObject.cs
   - src/Honua.Server.Core/Domain/AggregateRoot.cs
   - src/Honua.Server.Core/Domain/DomainException.cs
   ```

2. **Create Domain Events Infrastructure** (Week 2)
   ```
   Files to create:
   - src/Honua.Server.Core/Domain/IDomainEvent.cs
   - src/Honua.Server.Core/Domain/DomainEventDispatcher.cs
   - src/Honua.Server.Core/Domain/IDomainEventHandler.cs
   - src/Honua.Server.Core/Domain/DomainEventCollection.cs
   ```

3. **Create Common Value Objects** (Week 3)
   ```
   Files to create:
   - src/Honua.Server.Core/Domain/ValueObjects/EmailAddress.cs
   - src/Honua.Server.Core/Domain/ValueObjects/Coordinate.cs
   - src/Honua.Server.Core/Domain/ValueObjects/ShareTokenId.cs
   - src/Honua.Server.Core/Domain/ValueObjects/MapId.cs
   - src/Honua.Server.Core/Domain/ValueObjects/Permission.cs
   - src/Honua.Server.Core/Domain/ValueObjects/UserId.cs
   - src/Honua.Server.Core/Domain/ValueObjects/Username.cs
   ```

4. **Set Up Testing Infrastructure** (Week 4)
   ```
   Files to create:
   - tests/Honua.Server.Domain.Tests/EntityTests.cs
   - tests/Honua.Server.Domain.Tests/ValueObjectTests.cs
   - tests/Honua.Server.Domain.Tests/DomainEventTests.cs
   ```

**Effort:** 4 weeks (1 developer)
**Risk:** Low
**Dependencies:** None

---

### 1.2 Refactor Share Aggregate (Weeks 5-7)

**Objective:** Transform ShareToken from anemic to rich domain model

**Files to create:**
- `src/Honua.Server.Core/Domain/Sharing/ShareAggregate.cs`
- `src/Honua.Server.Core/Domain/Sharing/ShareComment.cs` (entity)
- `src/Honua.Server.Core/Domain/Sharing/ShareConfiguration.cs` (value object)
- `src/Honua.Server.Core/Domain/Sharing/Events/ShareCreatedEvent.cs`

**Files to modify:**
- `src/Honua.Server.Core/Models/ShareToken.cs` (mark as obsolete)
- `src/Honua.Server.Core/Services/Sharing/ShareService.cs` (orchestration only)

**Effort:** 3 weeks (1 developer)
**Risk:** Medium
**Dependencies:** 1.1 (DDD Foundation)

---

### 1.3 Refactor Authentication Aggregate (Weeks 8-10)

**Objective:** Create User aggregate with rich authentication behavior

**Files to create:**
- `src/Honua.Server.Core/Domain/Auth/User.cs` (aggregate root)
- `src/Honua.Server.Core/Domain/Auth/AuthenticationAttempt.cs` (value object)
- `src/Honua.Server.Core/Domain/Auth/PasswordHash.cs` (value object)
- `src/Honua.Server.Core/Domain/Auth/Events/UserAuthenticatedEvent.cs`

**Effort:** 3 weeks (1 developer)
**Risk:** Medium (security-critical)
**Dependencies:** 1.1 (DDD Foundation)

---

### 1.4 Break Up Large Static Handler Classes (Weeks 11-12)

**Objective:** Refactor SRP violations into properly structured services

**Files to refactor:**
1. `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs` (3,235 lines)
   - Split into: CollectionService, ItemsQueryService, ItemsCrudService

2. `src/Honua.Server.Host/GeoservicesREST/GeoservicesQueryService.cs` (1,371 lines)
   - Split into: FeatureQueryService, StatisticsQueryService, DistinctQueryService

**Effort:** 2 weeks (2 developers)
**Risk:** Medium
**Dependencies:** None

---

### Phase 1 Deliverables

- ✅ DDD base classes and infrastructure
- ✅ Domain events system
- ✅ Common value objects
- ✅ Share aggregate with rich behavior
- ✅ User/Auth aggregate with domain logic
- ✅ Refactored large handler classes into services
- ✅ Unit tests for all new domain logic

**Phase 1 Effort:** 12 weeks (3-4 developers)
**Phase 1 Risk:** Medium

---

## Phase 2: Scalability & Reliability (Weeks 13-24)

**Goals:** Implement CQRS, leader election, improve horizontal scalability

### 2.1 Implement Leader Election (Weeks 13-14)

**Objective:** Enable HA deployments without split-brain scenarios

**Files to create:**
- `src/Honua.Server.Core/Coordination/ILeaderElection.cs`
- `src/Honua.Server.Core/Coordination/RedisLeaderElection.cs`
- `src/Honua.Server.Core/Coordination/LeaderElectionService.cs`

**Effort:** 2 weeks (1 developer)
**Risk:** Low
**Dependencies:** None

---

### 2.2 Implement CQRS (Weeks 15-20)

**Objective:** Separate read and write models for better scalability

**Files to create:**
- `src/Honua.Server.Core/CQRS/ICommand.cs`
- `src/Honua.Server.Core/CQRS/ICommandHandler.cs`
- `src/Honua.Server.Core/CQRS/IQuery.cs`
- `src/Honua.Server.Core/CQRS/IQueryHandler.cs`
- `src/Honua.Server.Core/ReadModels/FeatureListItem.cs`
- `src/Honua.Server.Core/ReadModels/IFeatureReadRepository.cs`

**Effort:** 6 weeks (2 developers)
**Risk:** Medium
**Dependencies:** 1.1 (DDD Foundation)

---

### 2.3 Implement Identity Map (Week 21)

**Objective:** Ensure single instance per entity ID

**Files to create:**
- `src/Honua.Server.Core/Data/IdentityMap.cs`
- `src/Honua.Server.Core/Data/IIdentityMap.cs`

**Files to modify:**
- `src/Honua.Server.Core/Data/FeatureRepository.cs`

**Effort:** 1 week (1 developer)
**Risk:** Low
**Dependencies:** 1.1 (Entity base class)

---

### 2.4 Strengthen Optimistic Locking (Week 22)

**Objective:** Enforce version checking in all updates

**Files to modify:**
- `src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs`
- `src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs`
- `src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs`

**Effort:** 1 week (1 developer)
**Risk:** Medium
**Dependencies:** None

---

### 2.5 Enable CI/CD Workflows (Weeks 23-24)

**Objective:** Automate deployments, improve dev/prod parity

**Files to modify:**
- `.github/workflows/ci.yml.disabled` → `ci.yml`
- `.github/workflows/cd.yml.disabled` → `cd.yml`

**Files to create:**
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`

**Effort:** 2 weeks (1 developer + 1 DevOps)
**Risk:** Low
**Dependencies:** None

---

### Phase 2 Deliverables

- ✅ Leader election for HA deployments
- ✅ CQRS with separate read/write models
- ✅ Identity Map for entity caching
- ✅ Enforced optimistic locking
- ✅ Automated CI/CD pipelines

**Phase 2 Effort:** 12 weeks (2-3 developers)
**Phase 2 Risk:** Medium

---

## Phase 3: Domain Enrichment & Configuration (Weeks 25-36)

**Goals:** Complete DDD transformation, simplify configuration

### 3.1 Refactor Remaining Aggregates (Weeks 25-30)

**Objective:** Enrich remaining domain models

1. **GeofenceAlert Aggregate** (Weeks 25-27)
   - Create `src/Honua.Server.Core/Domain/Geofencing/GeofenceAlertRule.cs`
   - Create `src/Honua.Server.Core/Domain/Geofencing/GeofenceAlertMatcher.cs` (domain service)

2. **MapConfiguration Aggregate** (Weeks 28-30)
   - Create `src/Honua.Server.Core/Domain/Maps/MapConfiguration.cs`
   - Create `src/Honua.Server.Core/Domain/Maps/MapLayer.cs` (entity)

**Effort:** 6 weeks (2 developers)
**Risk:** Medium
**Dependencies:** 1.1-1.3 (DDD patterns established)

---

### 3.2 Consolidate Configuration (Weeks 31-33)

**Objective:** Simplify configuration management

**Tasks:**
1. Reduce 28+ appsettings files to 2-3 base files (Week 31)
2. Move environment-specific config to environment variables (Week 32)
3. Document configuration (Week 33)

**Files to create:**
- `docs/configuration.md`
- `docs/environment-variables.md`
- `.env.example` (comprehensive)

**Effort:** 3 weeks (1 developer)
**Risk:** Low
**Dependencies:** None

---

### 3.3 Implement Full Unit of Work (Weeks 34-36)

**Objective:** Change tracking and automatic persistence

**Files to create:**
- `src/Honua.Server.Core/Data/IUnitOfWork.cs`
- `src/Honua.Server.Core/Data/UnitOfWork.cs`
- `src/Honua.Server.Core/Data/ChangeTracker.cs`

**Effort:** 3 weeks (1 developer)
**Risk:** Medium
**Dependencies:** 1.1 (Entity base classes)

---

### Phase 3 Deliverables

- ✅ All major aggregates enriched with behavior
- ✅ Consolidated configuration (2-3 files + env vars)
- ✅ Full Unit of Work with change tracking
- ✅ Configuration documentation

**Phase 3 Effort:** 12 weeks (2-3 developers)
**Phase 3 Risk:** Medium

---

## Phase 4: Advanced Patterns & Long-term Improvements (Weeks 37-52)

**Goals:** Event sourcing, Saga pattern, global scalability

### 4.1 Implement Event Sourcing (Weeks 37-44)

**Objective:** Complete audit trail and temporal queries

**Files to create:**
- `src/Honua.Server.Core/EventSourcing/IEventStore.cs`
- `src/Honua.Server.Core/EventSourcing/PostgresEventStore.cs`
- `src/Honua.Server.Core/EventSourcing/EventStreamReader.cs`
- `src/Honua.Server.Core/EventSourcing/Projections/ShareProjection.cs`

**Database:**
- `migrations/AddEventStoreTable.sql`

**Effort:** 8 weeks (2 developers)
**Risk:** High
**Dependencies:** 1.1-1.3 (Domain events, aggregates)

---

### 4.2 Implement Saga Pattern (Weeks 45-48)

**Objective:** Distributed transaction coordination

**Files to create:**
- `src/Honua.Server.Core/Sagas/ISaga.cs`
- `src/Honua.Server.Core/Sagas/SagaOrchestrator.cs`
- `src/Honua.Server.Enterprise/ETL/Sagas/DataIngestionSaga.cs`

**Effort:** 4 weeks (2 developers)
**Risk:** Medium
**Dependencies:** 2.2 (CQRS), event infrastructure

---

### 4.3 Database Sharding (Weeks 49-52)

**Objective:** Horizontal database scalability

**Files to create:**
- `src/Honua.Server.Core/Data/Sharding/IShardCoordinator.cs`
- `src/Honua.Server.Core/Data/Sharding/TenantShardCoordinator.cs`
- `src/Honua.Server.Core/Data/Sharding/ShardMap.cs`

**Files to modify:**
- `src/Honua.Server.Core/Data/IDataStoreProviderFactory.cs`

**Effort:** 4 weeks (2 developers)
**Risk:** High
**Dependencies:** Complete data access refactoring

---

### Phase 4 Deliverables

- ✅ Event sourcing for critical aggregates
- ✅ Saga pattern for ETL workflows
- ✅ Database sharding for horizontal scalability
- ✅ Complete architectural transformation

**Phase 4 Effort:** 16 weeks (2-3 developers)
**Phase 4 Risk:** High

---

## Testing Strategy

### Per-Phase Testing Requirements

**Phase 1:**
- Unit tests for all domain models, aggregates, value objects
- Unit tests for domain services
- Integration tests for refactored services
- Target: 80% code coverage for new domain code

**Phase 2:**
- Unit tests for CQRS handlers
- Integration tests for leader election
- Load tests for optimistic locking under concurrency
- End-to-end tests for CI/CD pipelines
- Target: 75% code coverage

**Phase 3:**
- Unit tests for new aggregates
- Integration tests for Unit of Work
- Configuration validation tests
- Target: 80% code coverage

**Phase 4:**
- Event sourcing replay tests
- Saga compensation tests
- Sharding routing tests
- Performance benchmarks
- Target: 70% code coverage (complex scenarios)

---

## Risk Mitigation

### High-Risk Items

1. **Event Sourcing Implementation**
   - Mitigation: Pilot on single aggregate first, validate before expanding
   - Rollback: Keep existing repositories parallel during transition

2. **Database Sharding**
   - Mitigation: Extensive testing in staging with production-like data
   - Rollback: Design allows falling back to single database

3. **CQRS Breaking Changes**
   - Mitigation: Implement alongside existing repositories, migrate incrementally
   - Rollback: Facade pattern to support both old and new

### Medium-Risk Items

4. **DDD Refactoring Impact on Existing Services**
   - Mitigation: Use Adapter pattern for backward compatibility
   - Rollback: Keep obsolete classes marked but functional

5. **CI/CD Pipeline Changes**
   - Mitigation: Test in separate repository first
   - Rollback: Manual deployment procedures documented

---

## Success Metrics

### Phase 1 Metrics
- Domain model test coverage: 80%+
- Service class average size: <500 lines
- Number of anemic models: 0 (down from 15+)

### Phase 2 Metrics
- Leader election failover time: <5 seconds
- Read query performance improvement: 30%+ (with CQRS)
- Deployment frequency: Daily (from manual)

### Phase 3 Metrics
- Configuration files: 3 (down from 28)
- Environment variable documentation: 100%
- Aggregate count with rich behavior: 6+

### Phase 4 Metrics
- Event sourcing rebuild time: <1 hour for 1M events
- Saga success rate: 99.9%
- Shard distribution balance: ±10%

---

## Timeline Summary

| Phase | Duration | Focus | Risk |
|-------|----------|-------|------|
| Phase 1 | Weeks 1-12 | DDD Foundation & Critical Fixes | Medium |
| Phase 2 | Weeks 13-24 | Scalability & Reliability | Medium |
| Phase 3 | Weeks 25-36 | Domain Enrichment & Config | Medium |
| Phase 4 | Weeks 37-52 | Advanced Patterns | High |
| **Total** | **52 weeks** | **Complete Transformation** | **Medium** |

---

## Resource Requirements

**Team Composition:**
- 2-3 Senior Backend Developers (full-time)
- 1 DevOps Engineer (50% time, Phase 2)
- 1 QA Engineer (50% time, all phases)
- 1 Technical Lead/Architect (oversight, 25% time)

**Infrastructure:**
- Staging environment matching production
- CI/CD pipeline (GitHub Actions)
- Redis instance for testing leader election
- Database instances for sharding tests

---

## Conclusion

This refactoring plan transforms Honua.Server from a well-engineered data-centric application to a domain-driven, highly scalable enterprise platform. The phased approach minimizes risk while delivering continuous value.

**Key Benefits:**
- Improved maintainability through rich domain model
- Enhanced scalability through CQRS and sharding
- Increased reliability through leader election and Saga
- Better development velocity through automated CI/CD
- Complete audit trail through event sourcing

**Recommended Approach:**
Start with Phase 1 (DDD Foundation) as it unlocks all subsequent improvements and addresses the most critical architectural debt.

---

**Document Version:** 1.0
**Last Updated:** 2025-11-15
**Author:** Architecture Review Team
