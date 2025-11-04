# Geoprocessing Architecture Review

**Date:** 2025-11-03
**Reviewer:** Architecture Review
**Document:** `GEOPROCESSING_ARCHITECTURE.md` (v2.0, 2025-10-25)
**Status:** Requires updates before implementation

---

## Executive Summary

The geoprocessing architecture is **ambitious and coherent**, with a clear multi-tier execution strategy and solid foundations. However, there are **four critical gaps** between the design document and implementation reality that must be addressed before proceeding:

1. **Job Queue Implementation Mismatch**: Document assumes Hangfire; implementation uses Postgres-backed queue
2. **Tier 2 Data Locality Strategy Missing**: No plan for non-Postgres data sources (GeoPackage, Shapefile, external OGC)
3. **Tier 3 Payload Handoff Issues**: Environment variable approach has size/security limits
4. **Tier Selection Complexity Mismatch**: Document describes adaptive AI, implementation is deterministic

The overall vision is sound, but these gaps risk implementation failures if not resolved upfront.

---

## Critical Issues

### 1. Job Queue Technology Mismatch

**Issue Location:** Lines 380-410, 880-901, 2196-2220

**Problem:**

The architecture document extensively describes **Hangfire** as the job queue for Tier 1 & 2 in clustered deployments:

```csharp
// From doc, lines 382-396
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("Hangfire"),
        new PostgreSqlStorageOptions
        {
            SchemaName = "honua_gp_jobs",
            QueuePollInterval = TimeSpan.FromSeconds(5)
        }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount;
    options.Queues = new[] { "geoprocessing", "default" };
});
```

**Current Implementation Reality:**

The existing `PostgresControlPlane` (src/Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs) uses a **Postgres-backed queue WITHOUT Hangfire**:

- Direct SQL inserts to `process_runs` table with status='pending'
- No Hangfire dependency in the codebase (grep confirms zero Hangfire references)
- Queue polling/dequeue logic would be implemented via BackgroundService + direct SQL queries
- Simpler architecture: fewer moving parts, no Hangfire dashboard/schemas

**Impact:**

- **Pros of Current Approach**: Simpler, fewer dependencies, already works with existing Postgres
- **Cons of Current Approach**:
  - No built-in job retry logic
  - No admin dashboard for job monitoring
  - Manual worker polling implementation needed
  - Less mature than Hangfire for distributed job scheduling

**Recommendation:**

**Decision Required:** Pick one path and update documentation accordingly:

**Option A: Keep Postgres-only Queue (Recommended)**
- ✅ Already implemented
- ✅ Simpler deployment (no Hangfire schemas/dashboard)
- ✅ Fewer external dependencies
- ⚠️ Requires implementing worker polling via BackgroundService
- ⚠️ Requires custom retry/DLQ logic
- **Action:** Remove all Hangfire references from architecture doc (lines 380-410, 761-773, 880-901)

**Option B: Adopt Hangfire**
- ✅ Mature job scheduler with retry/DLQ
- ✅ Built-in dashboard for ops
- ⚠️ Additional dependency and operational complexity
- ⚠️ Requires migration from current PostgresControlPlane
- **Action:** Update implementation to match doc OR justify why Hangfire adds value over current solution

**Suggested Path:** Option A (keep Postgres-only). The current implementation is simpler and sufficient for most workloads. Add Hangfire only if specific features (dashboard, complex retry policies) become requirements.

---

### 2. Tier 2 Data Locality Gap

**Issue Location:** Lines 238-242, 275-284

**Problem:**

The document assumes Tier 2 (PostGIS) can directly access feature data via SQL:

```sql
-- From doc, lines 275-284
SELECT ST_VoronoiPolygons(
    ST_Collect(geometry)
) FROM layer_features
WHERE ST_Intersects(geometry, ST_MakeEnvelope(...));
```

**Current Implementation Reality:**

Honua supports **multiple data source types** (see `DataSourceDefinition` in MetadataSnapshot.cs:671):

```csharp
public sealed record DataSourceDefinition
{
    public required string Id { get; init; }
    public required string Provider { get; init; }  // "postgres", "geopackage", "shapefile", etc.
    public required string ConnectionString { get; init; }
}
```

**Data sources can be:**
- ✅ PostgreSQL/PostGIS (Tier 2 works natively)
- ❌ GeoPackage files (SQLite-based, not accessible from PostGIS)
- ❌ Shapefiles (file-based, no SQL interface)
- ❌ External OGC WFS services (HTTP endpoints, not database tables)
- ❌ Oracle Spatial (different RDBMS, not PostGIS)

**Impact:**

When a user requests a Tier 2 process (e.g., Voronoi diagram) on a layer backed by a GeoPackage:

1. PostGIS stored procedure cannot access the file system
2. No FDW (Foreign Data Wrapper) setup exists for GeoPackage → PostGIS
3. Process fails OR silently downgrades to Tier 1 (NTS) without explanation

**Recommendation:**

**Add a Data Locality Strategy** section to the architecture document:

**Option 1: Postgres-Only Flag (Simplest)**
- Process definitions declare `requiresPostgres: true`
- Admission control rejects non-Postgres layers for these processes
- User gets clear error: "Process 'voronoi' requires PostgreSQL data source"
- **Pros:** Simple, explicit, no hidden costs
- **Cons:** Limits Tier 2 applicability

**Option 2: Automatic Materialization (Most Flexible)**
- Control Plane detects non-Postgres source
- Materializes features to temp PostGIS table: `CREATE TEMP TABLE job_{guid} AS SELECT * FROM geopackage_fdw.features`
- Runs process on temp table
- Drops temp table on completion
- **Pros:** Works with any source
- **Cons:**
  - Performance hit for large datasets (double I/O)
  - Requires FDW setup for each source type
  - Temp table cleanup on failures

**Option 3: Hybrid Approach (Recommended)**
- Process definitions declare `preferredTiers: [postgis, nts]` with fallback
- If layer is Postgres → use Tier 2 directly
- If layer is file-based (GeoPackage/Shapefile) → fallback to Tier 1 (NTS) with warning
- If layer is external (WFS) → reject with clear error
- **Pros:** Transparent, handles common cases, clear errors for unsupported cases
- **Cons:** Tier selection becomes source-aware (more complex)

**Update Required:**
- Add "Data Locality Strategy" section to doc (after line 284)
- Update Tier 2 examples to note Postgres-only constraint
- Add `ITierExecutor.SelectTierAsync` logic to check data source compatibility
- Document materialization approach if chosen

---

### 3. Tier 3 Environment Variable Payload Limits

**Issue Location:** Lines 429-437, 441-446

**Problem:**

The document shows passing JSON parameters via environment variables:

```csharp
// From doc, lines 441-446
cloudTask.EnvironmentSettings = new List<EnvironmentSetting>
{
    new("JOB_ID", jobId),
    new("PARAMETERS", JsonSerializer.Serialize(request.Parameters)),  // ⚠️ Problem
    new("OUTPUT_BLOB_CONTAINER", _config["Storage:Container"])
};
```

**Platform Limits:**
- **AWS Batch**: 8 KB total env var size
- **Azure Batch**: 4 KB per variable, 32 KB total
- **GCP Cloud Batch**: 32 KB total
- **Security**: Env vars are logged in plain text (CloudWatch, Azure Monitor, Cloud Logging)

**Impact:**

- **Large input geometries** (>4KB GeoJSON) will **silently truncate**, causing cryptic Python errors
- **Sensitive data** (API keys in metadata, PII in filters) gets logged to cloud monitoring systems
- **No validation** at submission time = runtime failures

**Recommendation:**

**Replace env-var handoff with staged artifacts** (lines 1084-1100 already show this pattern):

```csharp
// CORRECT approach (already shown in doc lines 1084-1089)
private async Task<ProcessResult> ExecuteAsync(string containerTask, ProcessParameters parameters, CancellationToken ct)
{
    // 1. Stage inputs to blob storage (secure, size-unlimited)
    var inputEnvelope = await _artifacts.StageInputAsync(containerTask, parameters, ct);

    // 2. Pass ONLY the secure URL via env var
    var submission = await _batch.SubmitAsync(new CloudBatchJobRequest
    {
        TaskName = containerTask,
        ContainerImage = $"honua-gp-{containerTask}:latest",
        InputUri = inputEnvelope.Uri,  // ✅ Just the URL, ~200 bytes
        Environment =
        {
            ["TRACEPARENT"] = Activity.Current?.Id ?? string.Empty,
            ["HONUA_INPUT_URI"] = inputEnvelope.Uri  // ✅ No sensitive data
        }
    }, ct);
    // ...
}
```

**Update Required:**
- **Delete or mark incorrect:** Lines 441-446 (env var parameter passing)
- **Emphasize correct pattern:** Lines 1084-1100 (blob staging)
- **Add warning section:**
  - Note payload size limits (4-32 KB)
  - Note encryption requirements for tenant data
  - Mandate SAS/signed URL pattern for inputs >1 KB
  - Document secret handling (never in env vars, use KMS/Key Vault references)

---

### 4. Adaptive Tier Selection vs. Deterministic Reality

**Issue Location:** Lines 238-242

**Problem:**

The document describes an **adaptive heuristic engine**:

> "Adaptive orchestration: Process definitions advertise an ordered list of candidate tiers; the coordinator evaluates payload size, tenant policy, and **live capacity metrics** before committing to a tier."
>
> "**Telemetry feedback loop:** Execution metrics feed heuristics so **future runs pick the fastest successful tier** for similar parameter envelopes."

This implies:
- Machine learning or statistical models
- Telemetry collection and analysis
- Runtime metric aggregation
- Per-tenant learning over time

**Current Implementation Reality:**

`PostgresControlPlane.AdmitAsync` (lines 52-148) uses **simple deterministic logic**:

```csharp
// Line 107
var selectedTier = await _tierExecutor.SelectTierAsync(process, request, ct);
```

No telemetry feedback, no heuristics, just a synchronous tier selection based on process definition.

**Impact:**

- Document sets expectations for "smart" tier selection that doesn't exist
- Implementation effort significantly underestimated if adaptive logic is truly required
- Operators may expect tuning knobs that aren't built

**Recommendation:**

**Simplify the first release** to deterministic routing:

**Phase 1 (MVP): Deterministic Tier Selection**
```csharp
public class SimpleTierSelector : ITierExecutor
{
    public Task<ProcessExecutionTier> SelectTierAsync(ProcessDefinition process, ProcessExecutionRequest request, CancellationToken ct)
    {
        // 1. Check tenant policy override (explicit tier pinning)
        if (request.PreferredTier.HasValue)
            return Task.FromResult(request.PreferredTier.Value);

        // 2. Check data source compatibility (Postgres required for Tier 2)
        var layer = _metadata.GetLayer(request.LayerReference);
        var dataSource = _metadata.GetDataSource(layer.DataSourceId);

        if (dataSource.Provider != "postgres" && process.PreferredTiers.Contains(ProcessExecutionTier.PostGIS))
        {
            // Remove PostGIS from candidates if data not in Postgres
            var candidates = process.PreferredTiers.Where(t => t != ProcessExecutionTier.PostGIS).ToList();
            return Task.FromResult(candidates.FirstOrDefault());
        }

        // 3. Use process default (first preferred tier)
        return Task.FromResult(process.PreferredTiers.FirstOrDefault());
    }
}
```

**Phase 2+ (Future): Adaptive Selection**
- Collect telemetry: tier, duration, success/failure, parameter size
- Store in `process_execution_history` table
- Build simple heuristics:
  - "If input geometry >10K vertices → skip Tier 1, go to Tier 2"
  - "If Tier 2 PostGIS pool >80% busy → try Tier 1 first"
- Implement as pluggable `IAdaptiveTierSelector`

**Update Required:**
- **Replace** "adaptive orchestration" language (lines 238-242) with:
  - "Process definitions declare an ordered preference list of tiers"
  - "Control Plane selects the first compatible tier based on data source and policy"
  - "Future: Telemetry-driven optimization can adjust tier preferences"
- **Move** adaptive/ML-based tier selection to "Phase 4: Future Enhancements"
- **Add** "Contingency: When signals unavailable" section:
  - Fall back to process default tier order
  - Fail-fast if no compatible tier (don't silently downgrade)

---

## Strengths of the Architecture

Despite the gaps above, the architecture has **strong foundations**:

### ✅ Multi-Tier Execution Strategy
- Clear separation: Tier 1 (NTS, fast), Tier 2 (PostGIS, medium), Tier 3 (Cloud Batch, complex)
- Each tier has well-defined latency/cost/capability profiles
- Fallback strategy between tiers is sound (when implemented correctly)

### ✅ Unified Control Plane
- Single admission/scheduling/auditing boundary (IControlPlane)
- `ProcessRun` as single source of truth for job tracking
- Clear separation of concerns: admission, scheduling, auditing

### ✅ Shared Backend for Dual APIs
- GeoservicesREST + OGC Processes share implementation
- Reduces duplication, ensures feature parity
- Well-designed adapter pattern

### ✅ Event-Driven Tier 3
- Zero polling architecture (EventBridge → SQS, Event Grid → Service Bus)
- Honua stays private (no public IP required)
- Leverages managed batch services instead of building orchestration

### ✅ Security-First Design
- PostGIS SQL injection protection via stored procedures
- Tenant isolation in all queries (e.g., `WHERE tenant_id = @TenantId`)
- Container image signing for Python scripts

---

## Implementation Effort Adjustments

**Original Estimate:** 12-15 weeks across all phases

**Revised Estimate (with gap fixes):**

| Phase | Original | Adjusted | Notes |
|-------|----------|----------|-------|
| Phase 1: Foundation | 2-3 weeks | 2-3 weeks | No change (Tier 1 already exists) |
| **Phase 1.5: Queue Implementation** | — | **+1-2 weeks** | Implement BackgroundService worker polling for Postgres queue |
| Phase 2: PostGIS | 2-3 weeks | **3-4 weeks** | +1 week for data locality strategy implementation |
| Phase 3: Python/Batch | 3-4 weeks | **4-5 weeks** | +1 week for artifact staging refactor (remove env var pattern) |
| Phase 4: Separate Worker | 2-3 weeks | 2-3 weeks | No change (optional phase) |
| **Total** | **12-15 weeks** | **14-18 weeks** | +2-3 weeks for gap remediation |

---

## Required Actions Before Implementation

### Immediate (Before Phase 1)

1. **Job Queue Decision (1-2 days)**
   - [ ] Decide: Keep Postgres-only queue OR adopt Hangfire
   - [ ] If Postgres: Design BackgroundService polling architecture
   - [ ] If Hangfire: Justify benefits over current PostgresControlPlane
   - [ ] Update architecture doc sections 380-410, 880-901, 2196-2220

2. **Data Locality Strategy (2-3 days)**
   - [ ] Choose approach: Postgres-only flag, materialization, or hybrid
   - [ ] Document how non-Postgres sources interact with Tier 2
   - [ ] Add validation logic to `ITierExecutor.SelectTierAsync`
   - [ ] Update doc lines 275-284 with data source constraints

3. **Tier 3 Payload Handoff (1 day)**
   - [ ] Remove/deprecate env-var parameter passing (lines 441-446)
   - [ ] Add security warning section (payload encryption, size limits)
   - [ ] Mandate artifact staging for all inputs >1 KB
   - [ ] Document secret handling requirements

4. **Tier Selection Simplification (1 day)**
   - [ ] Replace "adaptive heuristic" language with "deterministic routing"
   - [ ] Move ML/telemetry tier selection to Phase 4+
   - [ ] Add "When signals unavailable" contingency section
   - [ ] Update lines 238-242

### Before Each Phase

- **Phase 1**: Finalize queue implementation (Postgres polling OR Hangfire)
- **Phase 2**: Implement data locality checks before PostGIS execution
- **Phase 3**: Complete artifact staging infrastructure before batch integration

---

## Conclusion

The geoprocessing architecture is **implementable and well-reasoned**, but the document needs **updates to match implementation reality**:

1. ✅ **Keep the overall vision**: Multi-tier execution, unified control plane, event-driven Tier 3
2. ⚠️ **Fix the queue story**: Either fully commit to Hangfire or document Postgres-only approach
3. ⚠️ **Add data locality strategy**: Don't assume all data is in PostGIS
4. ⚠️ **Fix Tier 3 payload handoff**: Use artifact staging, not env vars
5. ⚠️ **Simplify tier selection for MVP**: Save adaptive/ML logic for later phases

**Estimated Delay:** +2-3 weeks to implementation timeline, primarily for queue worker implementation and data locality strategy.

**Recommended Next Steps:**

1. **Decision Meeting (1 hour)**: Resolve the 4 issues above with stakeholders
2. **Document Update (1 day)**: Apply changes to architecture doc
3. **Prototype Data Locality (2 days)**: Build spike to validate chosen strategy
4. **Proceed to Phase 1**: With clarified queue + tier selection design

---

**Prepared by:** Architecture Review
**Date:** 2025-11-03
**Reviewed Document:** GEOPROCESSING_ARCHITECTURE.md v2.0
**Recommendation:** **APPROVE WITH REQUIRED UPDATES**
