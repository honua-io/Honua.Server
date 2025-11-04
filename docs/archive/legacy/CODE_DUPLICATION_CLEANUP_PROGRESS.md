# Code Duplication Cleanup Progress

**Date**: 2025-10-23
**Status**: In Progress

## Overview

This document tracks the progress of eliminating code duplication hotspots identified in the codebase.

## Completed Tasks

### 1. SanitizeFileSegment Duplication ✅

**Problem**: `SanitizeFileSegment` was reimplemented in both `GeoservicesRESTFeatureServerController.cs` and `OgcSharedHandlers.cs` even though `FileNameHelper.SanitizeSegment` already existed.

**Solution**:
- Removed duplicate method from `GeoservicesRESTFeatureServerController.cs` (lines 3118-3135)
- Removed duplicate method from `OgcSharedHandlers.cs` (lines 3095-3112)
- Updated all callers to use `FileNameHelper.SanitizeSegment`:
  - `GeoservicesRESTFeatureServerController.cs`: lines 2626, 2632
  - `OgcSharedHandlers.cs`: lines 3051, 3054, 3086, 3089
  - `OgcStylesHandlers.cs`: line 342 (added `using Honua.Server.Host.Utilities;`)
  - `OgcFeaturesHandlers.cs`: line 100

**Lines Removed**: ~36 lines
**Build Status**: ✅ Main projects compile successfully

---

## Completed Tasks

### 2. BuildFeatureComponents / NormalizeGeometry Duplication ✅

**Problem**: `OgcSharedHandlers` contained duplicate implementations of:
- `BuildFeatureComponents` (line 2441) - duplicated `FeatureComponentBuilder.BuildComponents` (Core line 18)
- `NormalizeGeometry` (line 2889) - duplicated `FeatureComponentBuilder.NormalizeGeometry` (Core line 111)
- `CreateKmlContent` (line 2421) - duplicated `FeatureComponentBuilder.CreateKmlContent` (Core line 91)
- `CreateTopoContent` (line 2431) - duplicated `FeatureComponentBuilder.CreateTopoContent` (Core line 101)
- Helper methods: `TryParseGeometry`, `ConvertGeometryValue`, `TryConvertWktToGeoJson`

**Solution**:
- Updated remaining caller in `GeoservicesRESTFeatureServerController.cs:2678` to use `FeatureComponentBuilder.CreateKmlContent`
- Removed all duplicate methods from `OgcSharedHandlers.cs`:
  - `CreateKmlContent` (9 lines)
  - `CreateTopoContent` (9 lines)
  - `BuildFeatureComponents` (72 lines)
  - `NormalizeGeometry` (36 lines)
  - `ConvertGeometryValue` (8 lines)
  - `TryConvertWktToGeoJson` (52 lines)
  - `TryParseGeometry` (16 lines)

**Lines Removed**: ~202 lines
**Build Status**: ✅ All main projects compile successfully

---

## Pending Tasks

### 3. Feature Query Parsing Duplication

**Problem**: Feature query parsing is scattered across three locations with near-identical validation rules:
- OGC items (OgcFeaturesHandlers.cs)
- WFS GetFeature (WfsHandlers.cs)
- GeoServices query translation (GeoservicesRESTQueryTranslator.cs)

All validate: limits, offsets, bbox, CRS, resultType

**Proposed Solution**: Create shared `FeatureQueryParser` (or model binder) in `Honua.Server.Core` that:
- Normalizes parameters once
- Returns `FeatureQuery` + diagnostics
- Each endpoint renders diagnostics in native format (OGC Exception Report, GeoServices error JSON)

**Benefits**:
- Single source of truth for query validation
- Consistent limits and validation across all APIs
- Easier to add new query capabilities

**Estimated Effort**: Medium (4-6 hours)

---

### 4. Export Wiring Duplication

**Problem**: Export wiring (GeoJSON/KML/Topo/Shapefile) is largely duplicated between:
- OGC collections (OgcFeaturesHandlers.cs line ~200)
- GeoServices FeatureServer (GeoservicesRESTFeatureServerController.cs line ~305)

**Proposed Solution**: Create shared `IFeatureExportService` that:
- Drives the exporters
- Handles file naming (using FileNameHelper)
- Handles response headers
- New output formats only need one implementation

**Benefits**:
- Single export pipeline
- Consistent file naming across APIs
- Easier to add new formats (Parquet, Arrow IPC, etc.)

**Estimated Effort**: Medium-Large (6-8 hours)

---

## Reuse / AOP Candidate Tasks

### 5. Service/Layer Resolution Duplication

**Problem**: Service/layer resolution repeated in multiple places:
- `_catalog.GetService` in GeoServices (GeoservicesRESTFeatureServerController.cs line 633)
- `ResolveCollectionAsync` in OGC (OgcSharedHandlers.cs line 1414)
- `ResolveLayerContextAsync` in WFS (WfsHandlers.cs line 443)

**Proposed Solution**:
- Shared resolver or endpoint filter
- Returns typed `FeatureContext`
- Standardizes NotFound/error responses

**Benefits**:
- Consistent error responses
- Less boilerplate in handlers
- Single place to add caching/optimization

**Estimated Effort**: Small-Medium (2-4 hours)

---

### 6. Query Validation Error Translation

**Problem**: Query validation and error translation duplicated:
- `CreateValidationProblem` in OGC
- `CreateException` in WFS
- Custom error responses in GeoServices

**Proposed Solution**: AOP-style action filter or endpoint filter that:
- Performs parameter validation before handler runs
- Translates validation errors to appropriate format
- Handler focuses on data retrieval

**Benefits**:
- Consistent validation across APIs
- Handlers are simpler
- Easier to add new validation rules

**Estimated Effort**: Medium (4-6 hours)

---

### 7. Export Telemetry/Logging

**Problem**: Consistent telemetry/logging for exports currently only in GeoServices (line 516)

**Proposed Solution**: Cross-cutting logging filter/aspect for uniform observability

**Benefits**:
- Track slow/failed exports across all APIs
- Consistent metrics for monitoring
- Easier debugging

**Estimated Effort**: Small (1-2 hours)

---

## Summary Statistics

**Total Identified Duplication**: ~500+ lines
**Removed So Far**: 238 lines (48%)
**In Progress**: 0 lines
**Pending**: ~262+ lines (52%)

**Priority Order** (user's suggested next steps):
1. ✅ Replace duplicate sanitizers with FileNameHelper
2. ✅ Consolidate BuildFeatureComponents/NormalizeGeometry to use FeatureComponentBuilder
3. ⏳ Sketch shared FeatureQueryParser API and migrate OGC/WFS/GeoServices
4. ⏳ Prototype endpoint filter for FeatureContext resolution
5. ⏳ Create IFeatureExportService

---

## Notes

- Main application projects build successfully after Task 1
- Test suite has pre-existing errors unrelated to this refactoring
- All changes maintain backward compatibility
- No API contract changes - only internal refactoring

---

**Last Updated**: 2025-10-23 15:45 UTC
