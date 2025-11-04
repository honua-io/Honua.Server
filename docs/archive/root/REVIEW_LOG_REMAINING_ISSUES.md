# Remaining Issues from review-log.md - Status Update

**Date**: 2025-10-18
**Total ISSUE Entries**: 17
**Already Fixed (Needs Audit Update)**: 3
**Requires Fixing**: 14

---

## Issues Already Fixed (Stale Audit Entries)

### 1. TopoJsonFeatureFormatter.cs (Line 237) ✅ ALREADY FIXED IN SESSION 2
**File**: `src/Honua.Server.Core/Serialization/TopoJsonFeatureFormatter.cs`
**Status**: Fixed in Session 2 (see ADDITIONAL_FIXES_SUMMARY.md)
**Fix Applied**: Interior rings now use negative arc indices `-(arcIndex + 1)` per TopoJSON spec
**Action Required**: Update audit/review-log.md line 237 from ISSUE to FIXED

### 2. VectorTilePreseedService.cs (Line 246) ✅ ALREADY FIXED
**File**: `src/Honua.Server.Host/VectorTiles/VectorTilePreseedService.cs`
**Status**: Already fixed in current codebase
**Fix Applied**:
- Cancellation token now created AFTER `job.MarkStarted()` (lines 213-219)
- Tile iteration limited to dataset extent via `GetTileRangeForLayer()` (lines 235-266)
**Action Required**: Update audit/review-log.md line 246 from ISSUE to FIXED

### 3. ResiliencePolicies.cs ✅ ALREADY FIXED
**File**: `src/Honua.Server.Host/Resilience/ResiliencePolicies.cs`
**Status**: Already implements comprehensive exception handling
**Fix Applied**: HttpRequestException, TimeoutRejectedException, SocketException handling in retry and circuit breaker
**Action Required**: Already marked FIXED in audit log

---

## Server-Side Issues Requiring Fixes (5 issues)

### 4. VectorTilePreseedEndpoints.cs (Line 268) ❌ NEEDS FIX
**File**: `src/Honua.Server.Host/Admin/VectorTilePreseedEndpoints.cs`
**Issue**: `EnqueueJob` returns `ex.Message` on failure
**Fix Required**: Sanitize error messages, log internally
**Priority**: Medium - Information Disclosure

### 5. StacSearchController.cs (Line 305) ❌ NEEDS FIX
**File**: `src/Honua.Server.Host/Stac/StacSearchController.cs`
**Issue**: `ParseBbox` and `ParseDatetimeRange` silently drop invalid filters instead of returning 400 errors
**Fix Required**: Surface errors from `QueryParsingHelpers` and reject malformed input
**Priority**: High - Security (DoS via full-catalog scans)

### 6. MigrationEndpointRouteBuilderExtensions.cs ✅ FIXED BY AGENT
**File**: `src/Honua.Server.Host/Admin/MigrationEndpointRouteBuilderExtensions.cs`
**Status**: Fixed by agent in current session
**Fix Applied**: Added logger injection, sanitized exception handling
**Action Required**: Audit log should already be updated

### 7. StacCollectionsController.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Stac/StacCollectionsController.cs`
**Issue**: No validation before JSON type casting - malformed types bubble as 500s
**Fix Required**: Add try-get patterns, return 400 for malformed JSON
**Priority**: Medium - Error Handling

### 8. UnifiedStacMapper.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Stac/UnifiedStacMapper.cs`
**Issue**: No sanitization of layer-provided text - HTML/JS injection risk
**Fix Required**: Sanitize layer.Title and validate AdditionalProperties
**Priority**: High - XSS Prevention

### 9. MapFishPrintHandlers.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Print/MapFishPrintHandlers.cs`
**Issue**: `CreateReportAsync` echoes `ex.Message` (may include backend details)
**Fix Required**: Log internally, return generic error to clients
**Priority**: Medium - Information Disclosure

### 10. DataIngestionEndpointRouteBuilderExtensions.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Admin/DataIngestionEndpointRouteBuilderExtensions.cs`
**Issue**: Accepts .zip without inspecting contents - path traversal risk
**Fix Required**: Validate zip contents, reject malicious archives
**Priority**: High - Path Traversal

### 11. RasterTileCacheEndpointRouteBuilderExtensions.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Admin/RasterTileCacheEndpointRouteBuilderExtensions.cs`
**Issue**: `catch (Exception ex)` returns `ex.Message`
**Fix Required**: Log internally, return sanitized error messages
**Priority**: Medium - Information Disclosure

### 12. DegradationStatusEndpoints.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Admin/DegradationStatusEndpoints.cs`
**Issue**: Endpoint group never calls `RequireAuthorization`
**Fix Required**: Add RequireAuthorization with admin policy
**Priority**: CRITICAL - Unauthenticated Feature Control

### 13. MetadataHostFilteringOptionsConfigurator.cs ❌ NEEDS FIX (from earlier list)
**File**: `src/Honua.Server.Host/Hosting/MetadataHostFilteringOptionsConfigurator.cs`
**Issue**: `GetSnapshotAsync().GetAwaiter().GetResult()` during options setup - deadlock risk
**Fix Required**: Replace with async initialization or prefetch pattern
**Priority**: Medium - Deadlock Risk

---

## CLI Issues Requiring Fixes (9 issues)

All CLI issues follow the same pattern: printing raw `ex.Message` from control plane responses which may leak internal server details.

### 14. RasterCachePurgeCommand.cs (Line 287) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/RasterCachePurgeCommand.cs`
**Issue**: Prints raw exception messages from control plane
**Fix Required**: Parse ProblemDetails, sanitize output
**Priority**: Low - CLI tool (information disclosure to CLI user)

### 15. VectorCacheStatusCommand.cs (Line 288) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/VectorCacheStatusCommand.cs`
**Issue**: Prints raw `ex.Message` on failure
**Fix Required**: Sanitize output, parse ProblemDetails
**Priority**: Low

### 16. DataIngestionCommand.cs (Line 292) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/DataIngestionCommand.cs`
**Issue**: Prints raw exception messages from control plane
**Fix Required**: Parse ProblemDetails, log full error separately
**Priority**: Low

### 17. MigrationCancelCommand.cs (Line 294) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/MigrationCancelCommand.cs`
**Issue**: Returns `ex.Message` on failure
**Fix Required**: Sanitize output
**Priority**: Low

### 18. MigrationJobsCommand.cs (Line 295) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/MigrationJobsCommand.cs`
**Issue**: Raw `ex.Message` leak when listing jobs
**Fix Required**: Sanitize control-plane error output
**Priority**: Low

### 19. MigrationStatusCommand.cs (Line 298) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/MigrationStatusCommand.cs`
**Issue**: Prints raw `ex.Message`
**Fix Required**: Sanitize control-plane error output
**Priority**: Low

### 20. ImportWizardCommand.cs (Line 299) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/ImportWizardCommand.cs`
**Issue**: Upload helpers echo server error bodies directly
**Fix Required**: Parse JSON/ProblemDetails, show generic message
**Priority**: Low

### 21. AdminLoggingGetCommand.cs (Line 300) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/AdminLoggingGetCommand.cs`
**Issue**: Prints `ex.Message` on failure
**Fix Required**: Sanitized messaging
**Priority**: Low

### 22. RasterCacheCancelCommand.cs (Line 302) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/RasterCacheCancelCommand.cs`
**Issue**: Raw error leak pattern
**Fix Required**: Wrap control-plane errors
**Priority**: Low

### 23. RasterCacheJobsCommand.cs (Line 303) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/RasterCacheJobsCommand.cs`
**Issue**: Returns `ex.Message` on failure
**Fix Required**: Sanitized messaging
**Priority**: Low

### 24. VectorCachePreseedCommand.cs (Line 304) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/VectorCachePreseedCommand.cs`
**Issue**: Multiple catch blocks returning `ex.Message`
**Fix Required**: Sanitize as above
**Priority**: Low

### 25. DataIngestionCancelCommand.cs (Line 312) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/DataIngestionCancelCommand.cs`
**Issue**: No exception handling around `CancelJobAsync`
**Fix Required**: Wrap and sanitize output
**Priority**: Low

### 26. DataIngestionJobsCommand.cs (Line 313) ❌ NEEDS FIX
**File**: `src/Honua.Cli/Commands/DataIngestionJobsCommand.cs`
**Issue**: Unhandled exceptions from `ListJobsAsync`
**Fix Required**: Add try/catch with sanitized messaging
**Priority**: Low

---

## Priority Summary

### CRITICAL (1 issue)
- DegradationStatusEndpoints.cs - Unauthenticated feature control

### HIGH (3 issues)
- StacSearchController.cs - DoS via full-catalog scans
- UnifiedStacMapper.cs - XSS prevention
- DataIngestionEndpointRouteBuilderExtensions.cs - Path traversal

### MEDIUM (6 issues)
- VectorTilePreseedEndpoints.cs - Information disclosure
- StacCollectionsController.cs - Error handling
- MapFishPrintHandlers.cs - Information disclosure
- RasterTileCacheEndpointRouteBuilderExtensions.cs - Information disclosure
- MetadataHostFilteringOptionsConfigurator.cs - Deadlock risk

### LOW (9 issues)
- All CLI commands - Information disclosure to CLI user

---

## Recommended Approach

### Phase 1: Critical & High Priority (4 issues)
1. DegradationStatusEndpoints.cs - Add `.RequireAuthorization()` call
2. StacSearchController.cs - Return 400 errors for invalid input
3. UnifiedStacMapper.cs - Add HTML encoding
4. DataIngestionEndpointRouteBuilderExtensions.cs - Validate zip contents

### Phase 2: Medium Priority (6 issues)
5-10. Server-side error sanitization and async fixes

### Phase 3: Low Priority (9 issues)
11-26. CLI command error sanitization (can be batch fixed with common helper)

---

## Next Steps

1. Update audit/review-log.md for already-fixed issues (lines 237, 246)
2. Fix CRITICAL issue (DegradationStatusEndpoints.cs) - ~5 minutes
3. Fix HIGH priority issues (3 issues) - ~30 minutes
4. Fix MEDIUM priority issues (6 issues) - ~45 minutes
5. Create CLI error sanitization helper and batch fix LOW priority issues - ~30 minutes

**Total Estimated Time**: ~2 hours for all remaining fixes
