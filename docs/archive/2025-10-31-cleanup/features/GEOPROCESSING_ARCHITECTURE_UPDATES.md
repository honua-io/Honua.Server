# Geoprocessing Architecture Updates

**Date:** 2025-11-03
**Document:** `GEOPROCESSING_ARCHITECTURE.md` v2.1
**Status:** ✅ Updated and ready for implementation

---

## Summary of Changes

The geoprocessing architecture document has been updated to address critical gaps identified during architecture review. All changes align the document with implementation reality and adopt best practices for job management, data locality, and security.

---

## Major Updates

### 1. ✅ Tier Selection: Deterministic for MVP (Lines 238-259)

**Change:** Simplified tier selection from "adaptive heuristic engine" to deterministic routing for MVP.

**Before:**
> "Adaptive orchestration: Process definitions advertise an ordered list of candidate tiers; the coordinator evaluates payload size, tenant policy, and **live capacity metrics** before committing to a tier."
>
> "**Telemetry feedback loop:** Execution metrics feed heuristics so future runs pick the fastest successful tier for similar parameter envelopes."

**After:**
```
Phase 1 (MVP): Deterministic Tier Selection

Process definitions declare an ordered preference list of tiers. The Control Plane selects
the first compatible tier based on:
- Data source compatibility (PostGIS requires PostgreSQL)
- Tenant policy overrides (explicit tier pinning)
- Process definition defaults (first tier in PreferredTiers)

Phase 2+: Adaptive Tier Selection (Future)
- Collect telemetry and build simple heuristics
- Implement as pluggable IAdaptiveTierSelector
```

**Rationale:**
- MVP doesn't need ML/heuristics - deterministic routing is sufficient
- Avoid over-engineering for first release
- Keep adaptive logic as optional Phase 2+ enhancement

---

### 2. ✅ Data Locality Strategy for Tier 2 (Lines 304-398, NEW)

**Change:** Added comprehensive data locality strategy section explaining how Tier 2 (PostGIS) interacts with non-PostgreSQL data sources.

**Key Points:**
- **Problem identified:** Honua supports GeoPackage, Shapefile, external WFS, Oracle Spatial - but PostGIS can only query PostgreSQL tables
- **Solution adopted:** Hybrid approach with automatic fallback
  - If data is in PostgreSQL → use Tier 2 directly
  - If data is in GeoPackage/Shapefile → automatically fallback to Tier 1 (NTS)
  - If process requires PostGIS and data isn't in PostgreSQL → reject with clear error

**Implementation:**
```csharp
// Remove PostGIS from candidates if data not in PostgreSQL
var candidateTiers = process.PreferredTiers.ToList();
if (dataSource.Provider.ToLowerInvariant() != "postgres")
{
    candidateTiers.Remove(ProcessExecutionTier.PostGIS);
    _logger.LogInformation(
        "Removed PostGIS tier - data source {DataSourceId} is {Provider}, not PostgreSQL",
        dataSource.Id, dataSource.Provider);
}

return candidateTiers.FirstOrDefault(ProcessExecutionTier.NTS);
```

**Process Definition Support:**
```yaml
# Allows fallback to NTS
preferredTiers: [postgis, nts]
hints:
  requiresPostgres: false

# Rejects non-PostgreSQL sources
preferredTiers: [postgis]
hints:
  requiresPostgres: true
```

**Future Option (Phase 3+):**
- Automatic materialization to temp PostgreSQL tables
- Enables PostGIS for all data sources
- Trade-off: double I/O, requires FDW setup

---

### 3. ✅ Tier 3 Payload Handoff: Artifact Staging (Lines 531-589)

**Change:** Fixed environment variable payload pattern - replaced with secure artifact staging approach.

**Problem Identified:**
- Cloud batch services limit environment variables to 4-32 KB total
- Environment variables are logged in plain text (security risk)
- Large JSON payloads silently truncate causing runtime failures

**Before (INCORRECT):**
```csharp
// ❌ DO NOT DO THIS
cloudTask.EnvironmentSettings = new List<EnvironmentSetting>
{
    new("PARAMETERS", JsonSerializer.Serialize(request.Parameters))  // Size limit violation!
};
```

**After (CORRECT):**
```csharp
// ✅ Stage inputs to blob storage (no size limit, encrypted at rest)
var inputEnvelope = await _artifacts.StageInputAsync(jobId, request.Parameters, ct);

cloudTask.EnvironmentSettings = new List<EnvironmentSetting>
{
    new("JOB_ID", jobId),
    new("HONUA_INPUT_URI", inputEnvelope.Uri),  // ✅ Just URL (~200 bytes)
    new("TRACEPARENT", Activity.Current?.Id ?? string.Empty)
};
```

**Added Warning Section:**
- AWS Batch: 8 KB total
- Azure Batch: 4 KB per variable, 32 KB total
- GCP Cloud Batch: 32 KB total
- Security: Env vars logged to CloudWatch/Azure Monitor/Cloud Logging

**Mandate:**
- All inputs >1 KB must use artifact staging
- Secrets NEVER in environment variables (use KMS/Key Vault references)
- Server-side encryption required for blob storage

---

### 4. ✅ Hangfire Adoption: Clarified Guidance (Lines 469-523, 1924-1979)

**Change:** Adopted Hangfire for Tier 1 & 2 async job execution in production deployments.

**Decision:**
- **Phase 1:** Inline synchronous execution (no queue)
- **Phase 1.5 (NEW):** Add Hangfire for async execution
- **Phase 2+:** Continue using Hangfire for production K8s deployments

**Why Hangfire:**
- Existing `PostgresControlPlane` provides job tracking but lacks worker execution logic
- Hangfire provides:
  - Automatic retry and DLQ
  - Background worker management
  - Admin dashboard for monitoring
  - Distributed coordination across K8s replicas

**Implementation Pattern (Phase 1.5):**
```csharp
// ControlPlane.EnqueueAsync calls Hangfire
public async Task<ProcessRun> EnqueueAsync(AdmissionDecision decision, CancellationToken ct)
{
    var processRun = await CreateProcessRunAsync(decision, ct);

    // Enqueue to Hangfire (NOT direct SQL insert)
    BackgroundJob.Enqueue<IProcessExecutor>(executor =>
        executor.ExecuteAsync(processRun.JobId, ct));

    return processRun;
}
```

**Added Phase 1.5 Tasks:**
- Install Hangfire.PostgreSql package
- Configure Hangfire storage (dedicated schema `honua_gp_jobs`)
- Implement HangfireJobExecutor wrapper
- Add Hangfire dashboard at `/hangfire` with auth
- Document operational procedures

**Clarification in Architecture Summary:**
- Single-instance: Can use Hangfire or BackgroundService
- Clustered K8s: **Must use Hangfire** for distributed coordination
- Tier 3: Independent of Hangfire (uses cloud batch services)

---

### 5. ✅ Implementation Phases: Revised Estimates (Lines 1891-2041)

**Change:** Updated implementation timeline from 12-15 weeks to 14-18 weeks with detailed breakdown.

**Phase Updates:**

| Phase | Original | Updated | Change | Reason |
|-------|----------|---------|--------|--------|
| Phase 1 | 2-3 weeks | 2-3 weeks | No change | Synchronous NTS operations |
| **Phase 1.5** | — | **1-2 weeks** | **+1-2 weeks** | **Hangfire integration** 🆕 |
| Phase 2 | 2-3 weeks | **3-4 weeks** | +1 week | Data locality strategy |
| Phase 3 | 3-4 weeks | **4-5 weeks** | +1 week | Artifact staging infrastructure |
| Phase 4 | 2-3 weeks | 2-3 weeks | No change | Optional separate worker |
| **Total** | **12-15 weeks** | **14-18 weeks** | **+2-3 weeks** | — |

**New Phase 1.5: Hangfire Queue Implementation**
- **Duration:** 1-2 weeks
- **Goal:** Add async job execution for Tier 1 & 2
- **Why separate:** Existing PostgresControlPlane needs worker execution logic
- **Deliverables:**
  - Hangfire integrated with PostgreSQL storage
  - Background worker management
  - Admin dashboard at `/hangfire`
  - Job retry policies and failure handling

**Phase 2 Extensions:**
- Added data source compatibility checks
- Added validation and fallback logic
- Added tests for PostGIS → NTS downgrade scenarios
- Added process definition `requiresPostgres` hint documentation

**Phase 3 Extensions:**
- Implement `IArtifactStore.StageInputAsync/StageOutputAsync`
- Configure blob storage with server-side encryption
- Generate and validate SAS tokens
- Document artifact staging security model

---

### 6. ✅ Architecture Summary: Updated (Lines 2406-2483)

**Changes:**
1. **Added Tier Selection section** highlighting deterministic routing for MVP
2. **Updated Tier 2 description** to note "PostgreSQL data sources only"
3. **Added Security points:**
   - Tier 3 artifact staging (no sensitive data in env vars)
   - Tenant isolation (tenant_id filter in all queries)
4. **Updated Deployment Recommendation table** with Phase 1, 1.5, 2, 3 progression
5. **Updated Key Improvements** to include:
   - "Multi-tier deterministic fallback with data locality awareness"
   - "Secure artifact staging (no sensitive data in environment variables)"
6. **Updated Estimated Implementation Effort:**
   - From: "12-15 weeks across all phases"
   - To: "14-18 weeks across all phases (updated from 12-15 weeks)"
   - Added detailed breakdown with 🆕 markers for new phases

---

## Migration Guidance

### For Existing Implementations

If you've already started implementing based on the old document:

1. **Tier Selection Logic:**
   - Replace any adaptive/ML logic with deterministic routing
   - Add data source provider checks before selecting PostGIS tier
   - Add logging for tier downgrade events

2. **Hangfire Integration:**
   - Keep existing `PostgresControlPlane` for job tracking
   - Add Hangfire in Phase 1.5 for worker execution
   - Update `EnqueueAsync` to call `BackgroundJob.Enqueue` instead of direct SQL insert

3. **Tier 3 Environment Variables:**
   - Replace any parameter passing via env vars with artifact staging
   - Implement `IArtifactStore.StageInputAsync` before submitting batch jobs
   - Update Python containers to read from `HONUA_INPUT_URI` instead of `PARAMETERS`

---

## Next Steps

### Immediate (Before Starting Implementation)

✅ **Document Review Complete** - Architecture document updated and aligned with implementation reality

### Ready for Implementation

1. **Phase 1 (Weeks 1-3):**
   - Implement OGC API - Processes endpoints
   - Wrap existing NTS operations
   - Synchronous execution only

2. **Phase 1.5 (Weeks 4-5):**
   - Install and configure Hangfire
   - Implement async job execution
   - Add Hangfire dashboard

3. **Phase 2 (Weeks 6-9):**
   - Implement PostGIS executor
   - Add data locality validation
   - Create 5-10 PostGIS processes

4. **Phase 3 (Weeks 10-14):**
   - Provision cloud batch infrastructure
   - Implement artifact staging
   - Build 5 Python container images

---

## Review Approval

**Status:** ✅ **APPROVED FOR IMPLEMENTATION**

**Review Date:** 2025-11-03
**Architecture Version:** v2.1 (updated from v2.0)
**Estimated Timeline:** 14-18 weeks (10-14 weeks required, 2-3 weeks optional Phase 4)

**Key Decisions Finalized:**
- ✅ Adopt Hangfire for Tier 1 & 2 async execution
- ✅ Use deterministic tier selection for MVP (defer adaptive logic to Phase 2+)
- ✅ Implement hybrid data locality strategy (automatic fallback)
- ✅ Mandate artifact staging for Tier 3 (no env var parameters)

**Documents:**
- Architecture: `docs/archive/2025-10-31-cleanup/features/GEOPROCESSING_ARCHITECTURE.md` (v2.1)
- Review: `docs/archive/2025-10-31-cleanup/features/GEOPROCESSING_ARCHITECTURE_REVIEW.md`
- Updates: `docs/archive/2025-10-31-cleanup/features/GEOPROCESSING_ARCHITECTURE_UPDATES.md` (this file)

---

**Prepared by:** Architecture Team
**Date:** 2025-11-03
**Ready for:** Implementation kickoff
