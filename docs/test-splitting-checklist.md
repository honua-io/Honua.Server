# Test Project Splitting - Migration Checklist

## Pre-Migration Validation

- [ ] Current test count verified: 3,046 tests (excluding GitOps)
- [ ] Current test execution time baseline: 30+ minutes
- [ ] All tests passing in current structure
- [ ] Code coverage baseline recorded
- [ ] Git branch created: `feature/split-test-projects`

---

## Phase 1: Create Shared Infrastructure

### 1.1 Create Shared Project
- [ ] Create directory: `tests/Honua.Server.Core.Tests.Shared/`
- [ ] Create `.csproj` file with library configuration
- [ ] Add to solution file
- [ ] Configure as library (not test project)

### 1.2 Move Shared Infrastructure
- [ ] Move `TestInfrastructure/` → `Shared/Infrastructure/`
- [ ] Move `Collections/` → `Shared/Collections/`
- [ ] Move test data files → `Shared/Data/`
- [ ] Update namespaces: `Honua.Server.Core.Tests.Shared.*`

### 1.3 Configure Shared Project
- [ ] Add common package references (xunit, FluentAssertions, etc.)
- [ ] Add framework references (Microsoft.AspNetCore.App)
- [ ] Add project references (Core, Core.Raster, Host)
- [ ] Build shared project successfully

### 1.4 Validation
- [ ] Shared project builds without errors
- [ ] All classes accessible and public
- [ ] No circular dependencies detected
- [ ] Run `dotnet build tests/Honua.Server.Core.Tests.Shared`

---

## Phase 2: Create Test Projects

### 2.1 Create Honua.Server.Core.Tests.Raster (563 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: MaxRev.Gdal.Core, Testcontainers.Minio, Testcontainers.Azurite
- [ ] Copy directories: `Raster/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 563 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.2 Create Honua.Server.Core.Tests.Data (383 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: Testcontainers.PostgreSql, MySql, MsSql, MySqlConnector
- [ ] Copy directories: `Data/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 383 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.3 Create Honua.Server.Core.Tests.OgcProtocols (475 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Copy directories: `Ogc/`, `Wfs/`, `Wcs/`, `Wmts/`, `Csw/`, `Carto/`, `Print/`
- [ ] Copy OGC-related files from `Hosting/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 475 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.4 Create Honua.Server.Core.Tests.Apis (390 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: FlatGeobuf, Apache.Arrow
- [ ] Copy directories: `Stac/`, `Geoservices/`, `OData/`, `OpenRosa/`, `Api/`, `Catalog/`, `Metadata/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 390 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.5 Create Honua.Server.Core.Tests.Security (435 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Copy directories: `Authentication/`, `Security/`, `Authorization/`, `Auth/`, `Query/`
- [ ] Copy security-related config tests from `Configuration/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 435 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.6 Create Honua.Server.Core.Tests.DataOperations (330 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: FlatGeobuf, Apache.Arrow
- [ ] Copy directories: `Import/`, `Export/`, `Editing/`, `Features/`, `Attachments/`, `Geometry/`, `Serialization/`, `SoftDelete/`, `Styling/`, `Concurrency/`, `Resilience/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 330 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.7 Create Honua.Server.Core.Tests.Infrastructure (305 tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: Testcontainers.Redis, Docker.DotNet
- [ ] Copy directories: `Configuration/`, `Deployment/`, `BlueGreen/`, `Docker/`, `Caching/`, `HealthChecks/`, `Observability/`, `Discovery/`, `DependencyInjection/`, `Extensions/`, `Utilities/`, `Support/`, `Pagination/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 305 tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.8 Create Honua.Server.Core.Tests.Integration (165+ tests)
- [ ] Create project directory
- [ ] Create `.csproj` with test SDK
- [ ] Reference Shared project
- [ ] Add specific packages: FsCheck.Xunit, NetArchTest.Rules, all Testcontainers
- [ ] Copy directories: `Integration/`, `Performance/`, `PropertyTests/`
- [ ] Update namespaces
- [ ] Build project
- [ ] Verify test count: 165+ tests
- [ ] Run tests locally
- [ ] All tests passing

### 2.9 Final Validation
- [ ] Total test count across all projects = 3,046
- [ ] No duplicate test files
- [ ] No orphaned test files
- [ ] All 8 projects build independently
- [ ] All projects reference Shared correctly

---

## Phase 3: CI/CD Integration

### 3.1 Update GitHub Actions Workflow
- [ ] Backup existing workflow file
- [ ] Create parallel test matrix for 8 projects
- [ ] Configure test result aggregation
- [ ] Configure code coverage aggregation
- [ ] Test workflow locally with `act` (if possible)

### 3.2 Run Tests in CI
- [ ] Push to feature branch
- [ ] Verify all 8 projects run in parallel
- [ ] Check total execution time < 15 minutes
- [ ] Verify all tests passing
- [ ] Check code coverage maintained

### 3.3 Configure Test Reporting
- [ ] Aggregate test results from all projects
- [ ] Generate combined coverage report
- [ ] Configure failure notifications
- [ ] Update PR check requirements

---

## Phase 4: Cleanup and Documentation

### 4.1 Delete Original Project
- [ ] Verify all tests migrated (double-check test count)
- [ ] Run full test suite 3+ times to ensure stability
- [ ] Delete `tests/Honua.Server.Core.Tests/` directory
- [ ] Remove project from solution file
- [ ] Commit deletion with clear message

### 4.2 Update Documentation
- [ ] Update README.md with new test structure
- [ ] Update CONTRIBUTING.md with test guidelines
- [ ] Create "Which project for my test?" guide
- [ ] Update CI/CD documentation
- [ ] Document test execution commands

### 4.3 Developer Workflow
- [ ] Create IDE templates for new tests
- [ ] Update VS Code workspace recommendations
- [ ] Test local test execution workflow
- [ ] Document how to run specific test projects
- [ ] Create troubleshooting guide

### 4.4 Team Communication
- [ ] Announce changes to team
- [ ] Provide migration guide
- [ ] Answer questions and concerns
- [ ] Monitor for issues in first week
- [ ] Gather feedback for improvements

---

## Post-Migration Validation

### Test Execution
- [ ] All 3,046 tests still passing
- [ ] Local execution time improved
- [ ] CI execution time < 15 minutes
- [ ] No flaky tests introduced
- [ ] Resource usage optimized

### Code Coverage
- [ ] Coverage maintained or improved
- [ ] Coverage reports work correctly
- [ ] Per-project coverage tracked
- [ ] Combined coverage report generated

### Developer Experience
- [ ] Faster feedback for domain-specific tests
- [ ] Clear which project to use for new tests
- [ ] Easy to run individual test projects
- [ ] Good error messages when tests fail
- [ ] Documentation clear and helpful

### CI/CD
- [ ] Parallel execution working
- [ ] Build times acceptable
- [ ] Test result reporting clear
- [ ] Failure notifications working
- [ ] PR checks configured correctly

---

## Rollback Plan (If Needed)

If critical issues arise during migration:

1. [ ] Keep original `Honua.Server.Core.Tests` until Phase 4
2. [ ] Revert CI/CD changes to use original project
3. [ ] Document issues encountered
4. [ ] Address issues before retrying migration
5. [ ] Consider gradual migration (one project at a time)

---

## Success Metrics

### Performance
- Target: Test execution time < 15 minutes (currently 30+)
- Stretch goal: < 12 minutes
- Measure: CI pipeline total duration

### Reliability
- Target: 0% increase in test flakiness
- Measure: Track flaky test rate over 2 weeks

### Developer Experience
- Target: 90% of developers prefer new structure
- Measure: Survey after 1 month

### Maintenance
- Target: No increase in maintenance burden
- Measure: Time to add new tests, time to fix failing tests

---

## Timeline Tracking

- Phase 1 Start: _______________
- Phase 1 Complete: _______________
- Phase 2 Start: _______________
- Phase 2 Complete: _______________
- Phase 3 Start: _______________
- Phase 3 Complete: _______________
- Phase 4 Start: _______________
- Phase 4 Complete: _______________

**Total Duration:** Target 4 weeks, Actual: _______________

---

## Notes and Issues

(Use this section to track any issues, decisions, or important notes during migration)

-
-
-
