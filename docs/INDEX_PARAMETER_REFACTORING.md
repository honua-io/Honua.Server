# Parameter Refactoring Documentation Index

**Date:** 2025-11-14  
**Status:** Design Phase Complete

This index provides a roadmap to the comprehensive parameter object designs created for the 5 highest-priority methods in the codebase.

---

## Documents Overview

### 1. PARAMETER_OBJECT_SUMMARY.md (Quick Reference)
**File Size:** 14 KB  
**Best For:** Getting started, executive overview, quick lookup

**Contents:**
- Quick overview table of all 5 methods
- Design patterns applied
- Method-by-method summaries
- Implementation roadmap
- Risk assessment
- Testing recommendations
- Performance considerations

**Start Here:** For a quick understanding of the refactoring scope and priorities.

---

### 2. PARAMETER_OBJECT_DESIGNS.md (Complete Reference)
**File Size:** 46 KB  
**Best For:** Detailed design reference, implementation guide

**Contents:**
- Executive summary with total impact analysis
- Full analysis for each method:
  - Current signature
  - Parameter grouping analysis
  - Proposed parameter object definitions
  - Before/After comparison
  - Complexity assessment
  - Breaking changes assessment
  - Implementation priority
- Implementation roadmap (6 phases, 9-15 days)
- Risk management strategies
- Success metrics
- References

**Use for:** Detailed implementation decisions, design decisions, parameter object specifications.

---

### 3. REFACTORING_PLANS.md (Context & Strategy)
**File:** `docs/REFACTORING_PLANS.md`  
**Best For:** Understanding the larger refactoring context

**Contains:**
- Overall Phase 3 refactoring strategy
- MetadataSnapshot refactoring plan
- OgcFeaturesHandlers refactoring plan
- Method parameter reduction analysis
- Implementation roadmap for all phases
- Success metrics and risk management

**Related Section:** "3. Method Parameter Reduction Analysis" (p. 254-343)

---

## Quick Navigation

### By Method

| Method | Priority | Risk | Summary | Details | Status |
|--------|----------|------|---------|---------|--------|
| **BuildJobDto** | 1 | 🟢 LOW | [link](#method-1-buildjobdto) | [details](#method-1-buildjobdto-design) | 📋 Ready |
| **ExecuteCollectionItemsAsync** | 2 | 🟢 LOW | [link](#method-2-executecollectionitemsasync) | [details](#method-2-executecollectionitemsasync-design) | 📋 Ready |
| **BuildLegacyCollectionItemsResponse** | 2 | 🟢 LOW | [link](#method-3-buildlegacycollectionitemsresponse) | [details](#method-3-buildlegacycollectionitemsresponse-design) | 📋 Ready |
| **GetCollectionTile** | 3 | 🟡 MEDIUM | [link](#method-4-getcollectiontile) | [details](#method-4-getcollectiontile-design) | ⚠️ Plan First |
| **GeoservicesRESTQueryContext** | 3 | 🟡 MEDIUM | [link](#method-5-geoservicesrestquerycontext) | [details](#method-5-geoservicesrestquerycontext-design) | ⚠️ Plan First |

### By Document

**Summary Document:**
- Quick Overview: p. 1-2
- Design Patterns: p. 2-3
- Method Summaries: p. 4-8
- Implementation Roadmap: p. 8-10
- Risk Assessment: p. 10-11

**Full Design Document:**
- Executive Summary: p. 1
- Method 1 - BuildJobDto: p. 2-6
- Method 2 - ExecuteCollectionItemsAsync: p. 7-11
- Method 3 - BuildLegacyCollectionItemsResponse: p. 12-16
- Method 4 - GetCollectionTile: p. 17-21
- Method 5 - GeoservicesRESTQueryContext: p. 22-30
- Implementation Roadmap: p. 31-32
- Risk Management: p. 33
- Success Metrics: p. 34
- References: p. 35

---

## Key Metrics at a Glance

### Parameter Reduction
| Method | Before | After | Reduction |
|--------|--------|-------|-----------|
| BuildJobDto | 23 | 9 | 61% |
| ExecuteCollectionItemsAsync | 18 | 10 | 44% |
| BuildLegacyCollectionItemsResponse | 18 | 11 | 39% |
| GetCollectionTile | 18 | 7 | 61% |
| GeoservicesRESTQueryContext | 19 | 8 | 58% |
| **TOTAL** | **92** | **45** | **51%** |

### Effort Estimation
- **Preparation:** 1-2 days
- **Priority 1 Implementation:** 1-2 days
- **Priority 2 Implementation:** 2-3 days
- **Priority 3 Implementation:** 2-3 days
- **Testing & Validation:** 2-3 days
- **Documentation:** 1 day
- **Total:** 9-15 days (1.5-3 weeks)

### Risk Distribution
- 🟢 **Low Risk (3 methods):** 54 parameters → 30 (44% reduction)
- 🟡 **Medium Risk (2 methods):** 37 parameters → 15 (59% reduction)

---

## Implementation Sequence

### Phase 1: Setup (1-2 days)
- [ ] Review design documents with team
- [ ] Approve parameter object designs
- [ ] Set up development environment
- [ ] Create test fixtures

### Phase 2: Priority 1 - BuildJobDto (1-2 days)
**Effort:** LOW | **Risk:** 🟢 LOW | **Visibility:** INTERNAL
- [ ] Create 8 parameter object records
- [ ] Update Dapper mappings
- [ ] Update tests
- [ ] Deploy internally

### Phase 3: Priority 2 - OGC Features (2-3 days)
**Effort:** MEDIUM | **Risk:** 🟢 LOW | **Visibility:** INTERNAL
- [ ] Create OgcFeatures* parameter objects
- [ ] Refactor ExecuteCollectionItemsAsync
- [ ] Refactor BuildLegacyCollectionItemsResponse
- [ ] Register in DI container
- [ ] Update tests

### Phase 4: Priority 3 - Advanced (2-3 days)
**Effort:** MEDIUM | **Risk:** 🟡 MEDIUM | **Visibility:** PUBLIC
- [ ] Create Tile* parameter objects
- [ ] Refactor GetCollectionTile (deprecated endpoint)
- [ ] Create Geoservices option records
- [ ] Refactor GeoservicesRESTQueryContext
- [ ] Create backward compatibility factory
- [ ] Plan deprecation timeline

### Phase 5: Testing (2-3 days)
- [ ] Unit tests for all parameter objects
- [ ] Integration tests for refactored methods
- [ ] Performance regression testing
- [ ] Security review

### Phase 6: Documentation (1 day)
- [ ] Update architecture docs
- [ ] Create ADRs
- [ ] Update developer guide
- [ ] Add code examples

---

## Parameter Objects Summary

### Method 1: BuildJobDto
**8 Parameter Objects:**
1. **CustomerInfo** - Customer/org details (3 props)
2. **BuildConfiguration** - Build spec (5 props)
3. **BuildJobStatus** - Queue position (3 props)
4. **BuildProgress** - Execution tracking (2 props)
5. **BuildArtifacts** - Output locations (3 props)
6. **BuildDiagnostics** - Error info (1 prop)
7. **BuildTimeline** - Event timestamps (4 props + helpers)
8. **BuildMetrics** - Performance metrics (1 prop + helpers)

### Method 2: ExecuteCollectionItemsAsync
**5 Parameter Objects:**
1. **OgcFeaturesRequestContext** - HTTP context (2 props)
2. **OgcFeatureExportServices** - Export handlers (5 props)
3. **OgcFeatureAttachmentServices** - Attachment ops (2 props)
4. **OgcFeatureEnrichmentServices** - Optional enrichment (1 prop)
5. **OgcFeatureObservabilityServices** - Observability (3 props)

**Reused By:** Method 3 (BuildLegacyCollectionItemsResponse)

### Method 3: BuildLegacyCollectionItemsResponse
**5 Parameter Objects:**
1. **LegacyCollectionIdentity** - Service/layer IDs (2 props)
2. **LegacyRequestContext** - HTTP request (1 prop)
3. **LegacyCatalogServices** - Catalog service (1 prop)
4. **OgcFeatureExportServices** ✓ REUSED from Method 2
5. **OgcFeatureAttachmentServices** ✓ REUSED from Method 2
6. **OgcFeatureEnrichmentServices** ✓ REUSED from Method 2
7. **LegacyObservabilityServices** - Metrics + cache (2 props)

### Method 4: GetCollectionTile
**5 Parameter Objects:**
1. **TileCoordinates** - Tile location (6 props consolidated)
2. **TileOperationContext** - HTTP context (1 prop)
3. **TileResolutionServices** - Metadata resolution (4 props)
4. **TileRenderingServices** - Rendering (2 props)
5. **TileCachingServices** - Caching (3 props)

### Method 5: GeoservicesRESTQueryContext
**7 Parameter Objects (+ main record with 8 properties):**
1. **QueryResultOptions** - Result type flags (5 props + validation)
2. **FieldProjectionOptions** - Field selection (3 props)
3. **ResponseFormatOptions** - Format control (2 props)
4. **SpatialOptions** - CRS and precision (3 props)
5. **AggregationOptions** - GROUP BY / stats (3 props)
6. **TemporalOptions** - Historic moment (1 prop)
7. **RenderingOptions** - Optional cartography (1 prop)
8. **GeoservicesRESTQueryContext** - Main container (8 properties)

---

## Key Design Decisions

### 1. Semantic Grouping
Parameters are grouped by conceptual relationship, not just type.
- ✅ All customer info together
- ✅ All output paths together
- ✅ All timing info together

### 2. Validation Methods
Include validation where constraints exist.
- ✅ QueryResultOptions.Validate() for mutually exclusive flags
- ✅ BuildTimeline helper methods for duration calculations

### 3. Backward Compatibility
Provide factory methods for public APIs.
- ✅ GeoservicesRESTQueryContext.Create() for old signature
- ✅ Allows gradual migration

### 4. Control Flow Separation
Never wrap cancellation tokens or result patterns.
- ✅ CancellationToken always separate parameter
- ✅ IResult returned directly

### 5. Optional Objects
Use nullable objects only for entirely optional groups.
- ✅ RenderingOptions? for optional rendering hints
- ✅ Not mixed null properties

---

## Stakeholder Guide

### For Developers
1. Start with **PARAMETER_OBJECT_SUMMARY.md** for overview
2. Reference **PARAMETER_OBJECT_DESIGNS.md** during implementation
3. Use design patterns guide (p. 2-3 of summary)
4. Follow testing recommendations (p. 14-15 of summary)

### For Architects
1. Review design patterns (p. 2-3 of summary)
2. Check risk assessment (p. 10-11 of summary)
3. Review implementation roadmap (p. 8-10 of summary)
4. See backward compatibility strategy (p. 7 of summary)

### For QA/Testers
1. Review test recommendations (p. 14-15 of summary)
2. Check performance considerations (p. 15-16 of summary)
3. Use success metrics for acceptance criteria (p. 11 of summary)

### For Project Managers
1. Review quick overview table (p. 2 of summary)
2. Check implementation roadmap (p. 8-10 of summary)
3. Estimate effort per method (p. 3 of summary)
4. See risk assessment summary (p. 10-11 of summary)

---

## Getting Started

### If You Have 5 Minutes
→ Read the quick overview table in PARAMETER_OBJECT_SUMMARY.md (p. 2)

### If You Have 15 Minutes
→ Read the executive summary and design patterns (PARAMETER_OBJECT_SUMMARY.md, p. 1-3)

### If You Have 30 Minutes
→ Skim method summaries (PARAMETER_OBJECT_SUMMARY.md, p. 4-8)

### If You Have 1 Hour
→ Read the full summary document (PARAMETER_OBJECT_SUMMARY.md)

### If You Have 2-3 Hours
→ Read the full design document (PARAMETER_OBJECT_DESIGNS.md)

---

## Related Documentation

- **REFACTORING_PLANS.md** - Overall Phase 3 strategy
- **REFACTORING_STATUS.md** - Current progress on all refactoring
- *Future:* Individual ADRs for breaking changes

---

## Questions & Discussion

### Approval Checklist
- [ ] Design patterns reviewed and approved
- [ ] Parameter grouping logic accepted
- [ ] Breaking change approach approved
- [ ] Implementation sequence approved
- [ ] Resource allocation confirmed

### Review Checklist
- [ ] All 5 methods analyzed
- [ ] Parameter objects designed
- [ ] Before/After comparisons accurate
- [ ] Backward compatibility planned
- [ ] Testing strategy covered
- [ ] Documentation complete

---

## Document Maintenance

**Version:** 1.0  
**Created:** 2025-11-14  
**Last Updated:** 2025-11-14  
**Status:** Complete - Ready for Implementation Planning

**To Update:**
1. Add version number and date
2. Update status section
3. Add implementation progress
4. Link to related ADRs
5. Update success metrics with actual results

---

**Next Action:** Schedule design review meeting with team
