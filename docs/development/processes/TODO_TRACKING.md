# TODO Tracking System

**Last Updated:** 2025-11-06
**Total TODOs:** 78
**Status Overview:** 0 Fixed | 0 In Progress | 78 Need Review | 0 Deferred

---

## Table of Contents
1. [Overview](#overview)
2. [Status Legend](#status-legend)
3. [Priority Categories](#priority-categories)
4. [Critical Security & Authentication TODOs](#critical-security--authentication-todos)
5. [Feature Implementation TODOs](#feature-implementation-todos)
6. [Configuration & Enhancement TODOs](#configuration--enhancement-todos)
7. [Technical Debt TODOs](#technical-debt-todos)
8. [Test & Development TODOs](#test--development-todos)
9. [GitHub Issues Created](#github-issues-created)
10. [Deferred Items](#deferred-items)

---

## Overview

This document tracks all TODO, FIXME, XXX, and HACK comments in the Honua.Server codebase. Each TODO is categorized by priority, assigned to a category, and linked to GitHub issues where appropriate.

**Key Metrics:**
- **Critical (P0):** 15 items - Security, Authentication, Authorization
- **High Priority (P1):** 18 items - Core feature implementations
- **Medium Priority (P2):** 23 items - Enhancements and configuration
- **Low Priority (P3):** 22 items - Technical debt and test improvements

---

## Status Legend

| Status | Description |
|--------|-------------|
| 🔴 **Issue Created** | Converted to GitHub issue for tracking |
| 🟡 **In Progress** | Currently being worked on |
| 🟢 **Fixed** | Completed and resolved |
| ⚪ **Need Review** | Requires triage and prioritization |
| 🔵 **Deferred** | Intentionally postponed |

---

## Priority Categories

| Priority | Description | Target Timeline |
|----------|-------------|-----------------|
| **P0 - Critical** | Security vulnerabilities, authorization gaps | Sprint 1 (Week 1-2) |
| **P1 - High** | Core features blocking releases | Sprint 2 (Week 3-4) |
| **P2 - Medium** | Enhancements, configuration improvements | Sprint 3-4 (Month 2) |
| **P3 - Low** | Technical debt, nice-to-haves | Backlog (Quarter 2) |

---

## Critical Security & Authentication TODOs

**Priority:** P0 - Critical
**Count:** 15 items
**Target:** Sprint 1 (Complete within 2 weeks)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 1 | 🔴 | `AlertAdministrationEndpoints.cs` | 34 | Add authorization after auth integration | [#TBD-001](#) | - |
| 2 | 🔴 | `ServerAdministrationEndpoints.cs` | 30 | Add authorization after auth integration | [#TBD-001](#) | - |
| 3 | 🔴 | `MetadataAdministrationEndpoints.cs` | 31 | Add authorization after auth integration | [#TBD-001](#) | - |
| 4 | 🔴 | `FeatureFlagEndpoints.cs` | 28 | Add authorization after auth integration | [#TBD-001](#) | - |
| 5 | 🔴 | `AlertAdministrationEndpoints.cs` | 218 | Get from authentication context (CreatedBy) | [#TBD-002](#) | - |
| 6 | 🔴 | `AlertAdministrationEndpoints.cs` | 289 | Get from authentication context (ModifiedBy) | [#TBD-002](#) | - |
| 7 | 🔴 | `AlertAdministrationEndpoints.cs` | 488 | Get from authentication context (CreatedBy) | [#TBD-002](#) | - |
| 8 | 🔴 | `AlertAdministrationEndpoints.cs` | 549 | Get from authentication context (ModifiedBy) | [#TBD-002](#) | - |
| 9 | 🔴 | `AlertAdministrationEndpoints.cs` | 856 | Get from authentication context (ModifiedBy) | [#TBD-002](#) | - |
| 10 | 🔴 | `GeoEventController.cs` | 345 | Extract tenant ID from claims or context | [#TBD-003](#) | - |
| 11 | 🔴 | `AzureStreamAnalyticsController.cs` | 291 | Extract tenant ID from claims or context | [#TBD-003](#) | - |
| 12 | 🔴 | `GeofencesController.cs` | 266 | Extract tenant ID from claims or context | [#TBD-003](#) | - |
| 13 | 🔴 | `EncryptedFileSecretsManager.cs` | 476 | In production, prompt user for actual passphrase via secure input | [#TBD-004](#) | - |
| 14 | 🔴 | `SamlEndpoints.cs` | 186 | Get session ID from authentication context | [#TBD-002](#) | - |
| 15 | ⚪ | `AuthorizationTests.cs` | 338, 441 | Implement full multi-user and multi-tenant security tests | - | - |

**Context:**
These TODOs represent critical security gaps in authorization and multi-tenancy. The application currently:
- Has admin endpoints without proper authorization checks
- Uses hardcoded user identities ("admin") instead of extracting from auth context
- Lacks tenant isolation in multi-tenant controllers
- Missing secure passphrase input for encryption keys

**Blocking:** Production deployment, security audit compliance

---

## Feature Implementation TODOs

**Priority:** P1 - High
**Count:** 18 items
**Target:** Sprint 2 (Complete within 4 weeks)

### Alert & Notification System (6 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 16 | 🔴 | `AlertAdministrationEndpoints.cs` | 361 | Implement actual alert publishing logic | [#TBD-005](#) | - |
| 17 | 🔴 | `AlertAdministrationEndpoints.cs` | 621 | Implement actual notification channel testing | [#TBD-006](#) | - |
| 18 | 🔴 | `AlertAdministrationEndpoints.cs` | 666 | Enhance AlertHistoryStore to support full filtering | [#TBD-007](#) | - |
| 19 | ⚪ | `AlertAdministrationEndpoints.cs` | 700 | Add method to get alert by ID | - | - |
| 20 | ⚪ | `AlertAdministrationEndpoints.cs` | 725 | Get alert by ID first (extract fingerprint) | - | - |
| 21 | ⚪ | `AlertAdministrationEndpoints.cs` | 761 | Get alert by ID first to extract matchers | - | - |

**Context:**
Alert system currently returns mock responses. Need to integrate with `IAlertPublisher` infrastructure and implement:
- Actual alert publishing to notification channels
- Connection testing for Slack, Email, PagerDuty, etc.
- Full filtering and querying of alert history

### Data Discovery & Connection Testing (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 22 | 🔴 | `MetadataAdministrationEndpoints.cs` | 1385 | Implement actual connection test based on provider | [#TBD-008](#) | - |
| 23 | 🔴 | `MetadataAdministrationEndpoints.cs` | 1429 | Implement actual table discovery based on provider | [#TBD-009](#) | - |

**Context:**
Connection testing and table discovery return stub responses. Need provider-specific implementations for:
- PostgreSQL/PostGIS
- MySQL
- SQL Server
- Oracle
- MongoDB, CosmosDB, Elasticsearch, BigQuery

### UI Component Features (6 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 24 | ⚪ | `HonuaAttributeTable.razor.cs` | 667 | Show advanced filter builder dialog | - | - |
| 25 | ⚪ | `HonuaAttributeTable.razor.cs` | 673 | Show filter presets dialog | - | - |
| 26 | ⚪ | `HonuaAttributeTable.razor.cs` | 679 | Save current filter as preset | - | - |
| 27 | ⚪ | `HonuaAttributeTable.razor.cs` | 709 | Implement proper filter application | - | - |
| 28 | ⚪ | `HonuaAttributeTable.razor.cs` | 846 | Show bulk edit dialog | - | - |
| 29 | ⚪ | `HonuaAttributeTable.razor.cs` | 852 | Show calculate field dialog | - | - |

### Data Loading Protocols (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 30 | ⚪ | `HonuaDataGrid.razor.cs` | 486 | Implement WFS loading | - | - |
| 31 | ⚪ | `HonuaDataGrid.razor.cs` | 493 | Implement gRPC loading | - | - |

### Hard Delete Functionality (4 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 32 | ⚪ | `MongoDbDataStoreProvider.cs` | 249 | Implement hard delete functionality for MongoDB | - | - |
| 33 | ⚪ | `ElasticsearchDataStoreProvider.Deletes.cs` | 90 | Implement hard delete functionality for Elasticsearch | - | - |
| 34 | ⚪ | `BigQueryDataStoreProvider.cs` | 248 | Implement hard delete functionality for BigQuery | - | - |
| 35 | ⚪ | `CosmosDbDataStoreProvider.cs` | 268 | Implement hard delete functionality for CosmosDB | - | - |

**Context:**
Soft delete is implemented; hard delete (permanent removal) is not implemented for NoSQL/cloud data stores.

---

## Configuration & Enhancement TODOs

**Priority:** P2 - Medium
**Count:** 23 items
**Target:** Sprints 3-4 (Month 2)

### Service Configuration (6 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 36 | ⚪ | `PostgresControlPlane.cs` | 87 | Make maxConcurrent configurable per tenant | - | - |
| 37 | ⚪ | `PostgresControlPlane.cs` | 101 | Make rateLimit configurable per tenant | - | - |
| 38 | ⚪ | `PostgresControlPlane.cs` | 140 | Actual validation and normalization of inputs | - | - |
| 39 | ⚪ | `PostgresControlPlane.cs` | 154 | Load process definitions from database | - | - |
| 40 | ⚪ | `PostgresControlPlane.cs` | 176 | Pass ApiSurface from request | - | - |
| 41 | ⚪ | `PostgresControlPlane.cs` | 716 | Type validation, range validation, etc. | - | - |

### Metadata Model Enhancements (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 42 | ⚪ | `ServiceDtos.cs` | 50 | Add CreatedAt to metadata model | - | - |
| 43 | ⚪ | `ServiceDtos.cs` | 51 | Add ModifiedAt to metadata model | - | - |

### Query Optimization (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 44 | ⚪ | `OptimizedPostgresFeatureOperations.cs` | 327 | Add zoom level to FeatureQuery context | - | - |
| 45 | ⚪ | `OptimizedPostgresFeatureOperations.cs` | 338 | Translate simple filters to SQL | - | - |

### Spatial Filtering (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 46 | ⚪ | `HonuaDataGrid.razor.cs` | 606 | Implement proper spatial filtering | - | - |
| 47 | ⚪ | `HonuaDataGrid.razor.cs` | 614 | Implement attribute filtering based on filter expression | - | - |

### ETL Enhancements (3 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|------|-------------|
| 48 | ⚪ | `DataSourceNodes.cs` | 260 | Add support for URL download and other formats (Shapefile, GeoPackage) | - | - |
| 49 | ⚪ | `DataSinkNodes.cs` | 120 | Properly parse and insert geometry using ST_GeomFromGeoJSON or ST_GeomFromText | - | - |
| 50 | ⚪ | `BufferOperation.cs` | 176 | Implement loading from collections, URLs, etc. | - | - |

### Cloud Provider Updates (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 51 | ⚪ | `RegistryProvisioner.cs` | 439 | Update to use the latest Azure.ResourceManager.ContainerRegistry API | - | - |
| 52 | ⚪ | `RegistryProvisioner.cs` | 523 | Install Google.Apis.Iam.v1 NuGet package or use Google.Cloud.Iam.Admin.V1 | - | - |

### Raster Processing (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 53 | ⚪ | `GdalKerchunkGenerator.cs` | 262 | Generate chunk byte offset references | - | - |

### Validation Enhancements (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 54 | ⚪ | `FeatureSchemaValidator.cs` | 622 | Could add more detailed GeoJSON validation here | - | - |

### Health Checks (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 55 | ⚪ | `TierExecutorCoordinator.cs` | 125 | Implement actual health checks | - | - |

### Progress Reporting (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 56 | ⚪ | `GeoprocessingWorkerService.cs` | 161 | Update progress in database | - | - |

### Alert History Enhancements (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 57 | ⚪ | `AlertAdministrationEndpoints.cs` | 679 | Check acknowledgement status | - | - |

---

## Technical Debt TODOs

**Priority:** P3 - Low
**Count:** 22 items
**Target:** Backlog (Quarter 2)

### Middleware Extension Methods (9 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 58 | ⚪ | `WebApplicationExtensions.cs` | 100 | Implement UseRequestResponseLogging middleware extension method | - | - |
| 59 | ⚪ | `WebApplicationExtensions.cs` | 192 | Implement UseCsrfValidation middleware extension method | - | - |
| 60 | ⚪ | `WebApplicationExtensions.cs` | 400 | Implement UseLegacyApiRedirect middleware extension method | - | - |
| 61 | ⚪ | `WebApplicationExtensions.cs` | 410 | Implement UseApiVersioning middleware extension method | - | - |
| 62 | ⚪ | `WebApplicationExtensions.cs` | 414 | Implement UseDeprecationWarnings middleware extension method | - | - |
| 63 | ⚪ | `WebApplicationExtensions.cs` | 418 | Implement UseHonuaCaching middleware extension method | - | - |
| 64 | ⚪ | `WebApplicationExtensions.cs` | 439 | Implement UseSecurityPolicy middleware extension method | - | - |

**Context:**
These are stub middleware extension methods. Most functionality may already exist elsewhere, need to verify and either implement or remove TODOs.

### Dependency Injection (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 65 | ⚪ | `ServiceCollectionExtensions.cs` | 216 | RasterTilePreseedService has unregistered dependencies (IRasterRenderer, IRasterTileCacheProvider) | - | - |
| 66 | ⚪ | `ServiceCollectionExtensions.cs` | 428 | Register remaining services as they are implemented | - | - |

### Health Checks (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 67 | ⚪ | `HealthCheckExtensions.cs` | 30 | Fix CacheConsistencyHealthCheck implementation | - | - |

### GitOps Feature (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 68 | 🔵 | `HonuaHostConfigurationExtensions.cs` | 3 | GitOps feature not yet implemented | - | - |
| 69 | 🔵 | `HonuaHostConfigurationExtensions.cs` | 101 | GitOps conditional registration | - | - |

**Context:**
GitOps feature is a future enhancement, intentionally deferred.

### AI Agent Integration (2 items)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 70 | ⚪ | `DeployPlanCommand.cs` | 97 | Integrate ArchitectureConsultingAgent (requires Kernel, not ILlmProvider) | - | - |
| 71 | ⚪ | `HonuaMagenticCoordinator.cs` | 752 | Implement HonuaGroupChatManager that uses LLM to dynamically select agents | - | - |

### Mobile App (1 item)

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 72 | ⚪ | `LoginViewModel.cs` | 192 | Navigate to server settings page | - | - |

---

## Test & Development TODOs

**Priority:** P3 - Low
**Count:** 5 items
**Target:** Backlog

| # | Status | File | Line | TODO | GitHub Issue | Assigned To |
|---|--------|------|------|------|--------------|-------------|
| 73 | ⚪ | `HonuaConsultantAgentTests.cs` | 48, 69 | Update after DeploymentConfigurationAgent refactoring | - | - |
| 74 | ⚪ | `AuthorizationTests.cs` | 338 | Implement full multi-user resource ownership test when user management is available | - | - |
| 75 | ⚪ | `AuthorizationTests.cs` | 441 | Implement full multi-tenant isolation test when multi-tenancy is available | - | - |
| 76 | ⚪ | `OgcFeaturesApiTests.cs` | 309 | Fix bug - nonexistent collection causes 500 instead of 404 | - | - |

**Context:**
Test TODOs are waiting for dependent features to be implemented.

---

## Deferred Items

These TODOs have been intentionally deferred for future consideration:

| # | File | Line | TODO | Reason | Review Date |
|---|------|------|------|--------|-------------|
| 68-69 | `HonuaHostConfigurationExtensions.cs` | 3, 101 | GitOps feature not yet implemented | Future roadmap item | Q2 2025 |

---

## GitHub Issues Created

The following GitHub issues have been created to track complex TODOs:

1. **[#TBD-001] Add Authorization to Admin Endpoints** - Critical
2. **[#TBD-002] Extract User Identity from Authentication Context** - Critical
3. **[#TBD-003] Extract Tenant ID from Claims for Multi-Tenancy** - Critical
4. **[#TBD-004] Implement Secure Passphrase Input for EncryptedFileSecretsManager** - Critical
5. **[#TBD-005] Implement Alert Publishing Logic** - High
6. **[#TBD-006] Implement Notification Channel Testing** - High
7. **[#TBD-007] Enhance AlertHistoryStore with Full Filtering** - High
8. **[#TBD-008] Implement Connection Testing for Data Sources** - High
9. **[#TBD-009] Implement Table Discovery for Data Sources** - High

See `.github/ISSUE_TEMPLATE/` directory for detailed issue templates.

---

## Management Guidelines

### When to Create a TODO vs. GitHub Issue

**Use TODO comments when:**
- Implementation is straightforward and can be done in < 2 hours
- Waiting for a dependency (library, API, another PR)
- Refactoring existing functionality
- Adding validation or error handling to existing code

**Create GitHub issue immediately when:**
- Security vulnerability or authorization gap
- New feature requiring multiple files/days of work
- Breaking change or API redesign
- Blocked by external dependencies
- Requires design discussion or stakeholder input

### TODO Format Standard

All TODOs must follow this format:

```csharp
// TODO(#issue-number): Brief description
// Context: Why this is needed and what's blocking it
// Example: How it should work when implemented
```

**Examples:**

```csharp
// TODO(#123): Add authorization check for admin endpoints
// Context: Waiting for auth integration PR #120 to merge
if (!User.IsInRole("Administrator"))
{
    return Forbid();
}

// TODO(#456): Extract tenant ID from claims
// Context: Multi-tenant claims provider not yet implemented
var tenantId = User.FindFirst("tenant_id")?.Value ?? throw new InvalidOperationException("Tenant ID not found");
```

### Code Review Checklist for TODOs

**Before Approving PR:**

- [ ] All new TODOs have issue numbers (format: `TODO(#123)`)
- [ ] TODOs include context comment explaining why it's needed
- [ ] Critical TODOs (security, auth) have P0 issues created
- [ ] TODOs that will remain > 2 sprints have GitHub issues
- [ ] No TODOs with placeholder values in production code (`"admin"`, `Guid.Empty`, etc.)
- [ ] No TODOs bypassing security (authorization, validation, encryption)
- [ ] Team agrees TODO can wait or has plan to address in next sprint

**When Reviewing Existing TODOs:**

- [ ] Verify the TODO is still relevant (not already implemented elsewhere)
- [ ] Check if blocking dependency has been resolved
- [ ] Update GitHub issue status if TODO is completed
- [ ] Remove TODO comment when implementation is complete
- [ ] Add test coverage when removing TODO placeholders

---

## Automation & Tooling

### Recommended CI/CD Checks

Add to `.github/workflows/pull-request.yml`:

```yaml
- name: Check for TODOs without issue numbers
  run: |
    # Find TODOs that don't have (#xxx) format
    if grep -r "TODO:" --include="*.cs" src/ | grep -v "TODO(#" ; then
      echo "❌ Found TODOs without issue numbers"
      exit 1
    fi
    echo "✅ All TODOs have issue numbers"
```

### SonarQube Rule Configuration

Update `sonar-project.properties`:

```properties
# Fail build if critical TODOs are added
sonar.issue.enforce.multicriteria=e1,e2
sonar.issue.enforce.multicriteria.e1.ruleKey=csharpsquid:S1135
sonar.issue.enforce.multicriteria.e1.resourceKey=**/Admin/**/*.cs
sonar.issue.enforce.multicriteria.e2.ruleKey=csharpsquid:S1135
sonar.issue.enforce.multicriteria.e2.resourceKey=**/Security/**/*.cs
```

---

## Reporting & Metrics

### Weekly TODO Report (Automated)

```bash
#!/bin/bash
# Generate weekly TODO report
echo "=== TODO Report for Week $(date +%W) ==="
echo ""
echo "Total TODOs: $(grep -r "TODO:" --include="*.cs" src/ | wc -l)"
echo "TODOs added this week: $(git log --since='1 week ago' -p | grep '^+.*TODO:' | wc -l)"
echo "TODOs removed this week: $(git log --since='1 week ago' -p | grep '^-.*TODO:' | wc -l)"
echo ""
echo "TODOs by priority:"
echo "- Critical (Admin, Auth): $(grep -r "TODO:" --include="*.cs" src/Honua.Server.Host/Admin src/Honua.Server.Host/Authentication | wc -l)"
echo "- High (Features): $(grep -r "TODO:" --include="*.cs" src/Honua.Server.Host src/Honua.Server.Enterprise | wc -l)"
echo "- Medium (Config): $(grep -r "TODO:" --include="*.cs" src/Honua.Server.Core | wc -l)"
echo "- Low (Tests): $(grep -r "TODO:" --include="*.cs" tests/ | wc -l)"
```

### SonarQube Dashboard

Track TODOs in SonarQube:
- **Technical Debt Ratio:** Target < 5%
- **TODO Count:** Target trend: decreasing
- **New TODOs per Sprint:** Target < 10
- **TODO Lifetime:** Target < 30 days

---

## Next Steps

1. **Immediate (This Sprint):**
   - Create GitHub issues #TBD-001 through #TBD-009
   - Assign critical security TODOs (P0) to developers
   - Set up automated TODO checking in CI/CD
   - Review and remove obsolete TODOs

2. **Short-term (Next Sprint):**
   - Implement authorization checks (#TBD-001, #TBD-002, #TBD-003)
   - Complete alert publishing implementation (#TBD-005, #TBD-006)
   - Add TODO format enforcement to code review checklist

3. **Long-term (Quarter 2):**
   - Reduce total TODO count by 50%
   - Implement middleware extension methods or remove stubs
   - Complete GitOps feature planning

---

**Document Maintenance:**
- Update this file weekly after sprint planning
- Mark TODOs as completed when merged to main
- Archive completed items monthly to RESOLVED_TODOS.md
- Review deferred items quarterly
