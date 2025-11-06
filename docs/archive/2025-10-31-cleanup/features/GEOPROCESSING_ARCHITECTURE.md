# Geoprocessing Architecture Design

## Overview

This document outlines the design for comprehensive geoprocessing capabilities in Honua, leveraging a multi-tier execution strategy to balance performance, functionality, and operational complexity.

**Status:** Design Phase - Ready for Implementation
**Version:** 2.0
**Last Updated:** 2025-10-25

**Note:** This document incorporates lessons learned from Esri ArcGIS Server (GeoservicesREST GPServer) and GeoServer WPS implementations. The comparative analysis has been archived to `docs/archive/GEOPROCESSING_COMPARATIVE_ANALYSIS.md` for reference.

---

## Goals & Design Decisions

### Primary Goals

1. **Ecosystem Compatibility:** Implement GeoservicesREST GPServer specification (Priority 1) for broad compatibility
2. **Standards Compliance:** Support OGC API - Processes 1.0 specification (Priority 2)
3. **Multi-Tier Execution:** Support .NET, PostGIS, and cloud-native Python operations
4. **Cloud-Native Ready:** Leverage managed batch services (AWS Batch, Azure Batch, GCP Batch) for Tier 3
5. **Scalability:** Start embedded, migrate to distributed workers when needed
6. **Security First:** Keep Honua private (no public IP required) using event-driven patterns
7. **Operational Simplicity:** Use familiar patterns (background services, event queues)

### Key Design Decisions

Based on analysis of Esri GeoservicesREST and GeoServer WPS architectures:

| Decision | Rationale |
|----------|-----------|
| **GeoservicesREST API first** | Wider adoption than OGC WPS, JSON-based, open specification (OWF) |
| **Shared backend with thin adapters** | Single implementation supports multiple API surfaces |
| **Cloud batch for Tier 3** | Avoid building job orchestration, leverage managed services |
| **Event-driven completion** | SQS/Service Bus/Pub/Sub - zero polling, Honua stays private |
| **PostGIS stored procedures** | Tier 2 SQL injection protection, leverages existing infrastructure |
| **Resource class governance** | Proactive capacity management, tenant boundaries |

---

## Lessons Learned from Industry Leaders

### From Esri ArcGIS Server

| Esri Practice | Honua Implementation | Rationale |
|---------------|----------------------|-----------|
| **GeoservicesREST GPServer spec** | Priority 1 API surface | JSON-based, open spec (OWF), wider adoption than OGC WPS |
| **Dual execution modes (sync/async)** | `execute` vs `submitJob` endpoints | Prevents misuse - fast ops sync, slow ops async |
| **9-state job lifecycle** | Simplified to 5 states (Submitted, Running, Succeeded, Failed, Cancelled) | Balance complexity vs functionality |
| **Process isolation via OS** | Tier 3 uses containers (AWS Batch/Azure Batch) | Crash isolation - Python failures don't affect API |
| **Separate geoprocessing site** | Phase 2: Separate workers OR Phase 3: Cloud batch | Prevents GP from starving API resources |
| **Connection pooling** | PostGIS stored procedures via existing connection pool | Reuse infrastructure, no new dependencies |
| **File upload pattern (2-step)** | Supported in Tier 3 (S3/Blob upload → reference) | Avoids JSON encoding overhead for large datasets |
| **RBAC at service level** | Tenant-based resource quotas | Per-tenant capacity limits |

### From GeoServer WPS

| GeoServer Practice | Honua Implementation | Rationale |
|--------------------|----------------------|-----------|
| **Plugin architecture (SPI)** | `IProcessRegistry` with auto-discovery | Extensible process registration |
| **Catalog-aware processes** | Processes reference Honua layers by ID | UX benefit - no manual data upload |
| **PPIO format handling** | `ProcessParameter` with typed inputs/outputs | Type safety, validation |
| **Asynchronous execution** | BackgroundService + Hangfire + Cloud Batch | Three-tier scalability |
| **Process chaining** | Deferred to Phase 2+ | Complex feature, implement after MVP |
| **GeoTools integration** | NTS integration (existing) | Leverage existing geometry library |
| **Geometry streaming** | Artifact store for large outputs (S3/Blob) | Avoid in-memory bloat |

### Key Architectural Improvements Over Both Platforms

| Innovation | How It Improves on Esri/GeoServer |
|------------|-----------------------------------|
| **Shared backend with thin adapters** | Support 2 API surfaces (GeoservicesREST, OGC) with single implementation - Esri/GeoServer only support one |
| **Cloud-native Tier 3** | Leverage AWS/Azure/GCP batch services - Esri/GeoServer use VMs/local processes |
| **Event-driven completion** | Zero polling via SQS/Service Bus/Pub/Sub - Esri/GeoServer require polling |
| **Private deployment** | Honua doesn't need public IP (pulls from queue) - improves security |
| **Multi-tier execution with fallback** | Adaptive tier selection based on capacity - Esri/GeoServer fixed execution mode |
| **PostGIS as Tier 2** | SQL injection protection via stored procedures - Esri requires ArcPy, GeoServer uses in-process Java |
| **Resource class governance** | Proactive capacity management - Esri/GeoServer have reactive throttling |

---

## Current State

### Existing Capabilities

Honua already implements **Tier 1** geoprocessing via NetTopologySuite:

**File:** `src/Honua.Server.Core/Geoservices/GeometryService/GeometryOperationExecutor.cs`

**Operations (18 total):**
- **Spatial Operations:** Buffer, Union, Intersection, Difference, ConvexHull
- **Transformations:** Project, Simplify, Densify, Generalize, Offset
- **Analysis:** Distance, Areas, Lengths, LabelPoints
- **Editing:** Cut, Reshape, TrimExtend

**Endpoint:** `rest/services/Geometry/GeometryServer` (Esri compatibility)

**Limitations:**
- Synchronous execution only
- No job management or async support
- Limited to NTS capabilities
- No PostGIS or Python integration
- Not OGC API compliant

---

## API Surface

Honua implements **two API surfaces** over a shared backend:

### Priority 1: GeoservicesREST GPServer (Esri Compatible)

**URL Structure:**
```
/arcgis/rest/services/GP/GPServer
/arcgis/rest/services/GP/GPServer/{task}
/arcgis/rest/services/GP/GPServer/{task}/execute       # Synchronous
/arcgis/rest/services/GP/GPServer/{task}/submitJob     # Asynchronous
/arcgis/rest/services/GP/GPServer/jobs/{jobId}
```

**Why Priority 1:**
- JSON-based REST API (easier than XML)
- Transferred to Open Web Foundation (2010) - open specification
- Wide ecosystem adoption (ArcGIS Online, Enterprise, third-party tools)
- Familiar to GIS professionals migrating to Honua

**Example Request:**
```json
POST /arcgis/rest/services/GP/GPServer/buffer/submitJob
{
  "Input_Features": {"layerReference": "roads"},
  "Distance": {"distance": 100, "units": "esriMeters"},
  "f": "json"
}
```

### Priority 2: OGC API - Processes 1.0 (Standards Compliant)

**URL Structure:**
```
/processes
/processes/{processId}
/processes/{processId}/execution
/jobs/{jobId}
/jobs/{jobId}/results
```

**Why Priority 2:**
- OGC standard (interoperability)
- Standards-compliant alternative
- Modern JSON (no XML like classic WPS)

**Example Request:**
```json
POST /processes/buffer/execution
{
  "inputs": {
    "features": {"layerReference": "roads"},
    "distance": {"value": 100, "unit": "meters"}
  },
  "mode": "async"
}
```

### Shared Backend Pattern

Both API surfaces translate to the same internal execution model through a **unified execution adapter**:

```
┌────────────────────────────────────────────────────────┐
│  API Layer                                             │
│  ┌──────────────────────┐  ┌──────────────────────┐   │
│  │ GeoservicesREST      │  │ OGC Processes        │   │
│  │ GPServerController   │  │ ProcessesController  │   │
│  └──────────┬───────────┘  └──────────┬───────────┘   │
│             │                          │               │
│             └────────────┬─────────────┘               │
│                          ↓                             │
│          ┌───────────────────────────────┐             │
│          │ Unified Execution Adapter     │             │
│          │ - Parameter translation       │             │
│          │ - Result formatting           │             │
│          │ - Both APIs evolve together   │             │
│          └──────────────┬────────────────┘             │
└─────────────────────────┼──────────────────────────────┘
                          ↓
         ┌────────────────────────────────┐
         │ Control Plane                  │
         │ - Admission (policy/quotas)    │
         │ - Scheduling (queue/tier)      │
         │ - Auditing (provenance/cost)   │
         └──────────────┬─────────────────┘
                        ↓
         ┌──────────────────────────────┐
         │ Multi-Tier Execution Engine  │
         │ Tier 1 | Tier 2 | Tier 3     │
         └──────────────────────────────┘
```

**Key Design Principles:**
- ✅ **Unified Adapter**: Single adapter layer ensures both APIs evolve together
- ✅ **Control Plane**: Single boundary for admission, scheduling, auditing (no scattered components)
- ✅ **Feature Parity**: Both APIs get identical capabilities
- ✅ **ProcessRun as Source of Truth**: Central record for scheduling, billing, provenance

---

## Proposed Architecture

### High-Level Design

```
┌─────────────────────────────────────────────────────────┐
│  API Layer (2 surfaces, shared backend)                 │
│  - GeoservicesREST GPServer (Priority 1)                │
│  - OGC API - Processes (Priority 2)                     │
└────────────────┬────────────────────────────────────────┘
                 │
    ┌────────────▼───────────────┐
    │  Process Registry          │
    │  - Discovery               │
    │  - Job Management          │
    │  - Execution Coordination  │
    └────┬─────────┬──────────┬──┘
         │         │          │
   ┌─────▼──┐ ┌───▼────┐ ┌──▼──────────────┐
   │ Tier 1 │ │ Tier 2 │ │ Tier 3          │
   │  .NET  │ │PostGIS │ │ Cloud Batch     │
   │  NTS   │ │Stored  │ │ (AWS/Azure/GCP) │
   └────────┘ │Procs   │ │ Python/GPU      │
              └────────┘ └─────────────────┘
```

### Execution Tiers

#### Tier Selection & Fallback

**Phase 1 (MVP): Deterministic Tier Selection**

Process definitions declare an ordered preference list of tiers. The Control Plane selects the first compatible tier based on:
- **Data source compatibility:** PostGIS tier requires data in PostgreSQL (see Data Locality Strategy below)
- **Tenant policy overrides:** Explicit tier pinning via `TenantPolicyOverride.ForceTier`
- **Process definition defaults:** First tier in `ProcessDefinition.PreferredTiers` that passes validation

**Graceful degradation:** When the preferred tier is incompatible (e.g., PostGIS requested but data is in GeoPackage), the coordinator tries the next tier in the preference list. If no compatible tier exists, the request is rejected with a clear error message explaining the constraint violation.

**Per-tenant overrides:** Tenants can pin processes to specific tiers for cost controls or opt into experimental Python flows via `TenantPolicyOverride`.

**Phase 2+: Adaptive Tier Selection (Future)**

After collecting sufficient telemetry, implement adaptive heuristics:
- Track execution metrics: tier, duration, success/failure, parameter size
- Build simple rules: "If input geometry >10K vertices → skip Tier 1, go to Tier 2"
- Monitor capacity: "If PostGIS pool >80% busy → try Tier 1 first with fallback"
- Implement as pluggable `IAdaptiveTierSelector` for A/B testing

**Contingency when signals unavailable:** Always fall back to deterministic tier order from process definition. Never silently downgrade without logging the reason.

#### Tier 1: Pure .NET (NetTopologySuite)
**Status:** ✅ Exists
**Use Cases:** Fast, synchronous geometry operations
**Technology:** NetTopologySuite
**Latency:** < 100ms
**Examples:** buffer, simplify, project, union, intersection

**Recommended Extensions:**
- Voronoi Diagrams (NTS native)
- Delaunay Triangulation (NTS native)
- Line Merge, Polygonize
- MinimumBoundingCircle, MinimumDiameter
- Geometry validation (IsSimple, IsValid)

#### Tier 2: PostGIS Server-Side Processing
**Status:** 🆕 New
**Use Cases:** Database-side spatial operations, clustering, interpolation
**Technology:** PostGIS SQL queries via existing data store
**Latency:** 1-10 seconds
**Advantage:** Zero additional dependencies

**Operations to Implement:**
- **Voronoi & Delaunay:** `ST_VoronoiPolygons`, `ST_DelaunayTriangles`
- **Clustering:** `ST_ClusterDBSCAN`, `ST_ClusterKMeans`, `ST_ClusterWithin`
- **Heatmaps:** Kernel density via `ST_HexagonGrid` + aggregation
- **Interpolation:** IDW via custom PL/pgSQL functions
- **Network Analysis:** pgRouting integration (if installed)
- **Raster Analytics:** PostGIS Raster operations

**Example Query:**
```sql
-- Voronoi diagram generation
SELECT ST_VoronoiPolygons(
    ST_Collect(geometry)
) FROM layer_features
WHERE ST_Intersects(geometry, ST_MakeEnvelope(...));

-- DBSCAN clustering
SELECT ST_ClusterDBSCAN(geometry, eps := 100, minpoints := 5)
OVER () AS cluster_id, *
FROM layer_features;
```

#### Data Locality Strategy for Tier 2

**Problem:** Honua supports multiple data source types (PostgreSQL, GeoPackage, Shapefile, external WFS services), but Tier 2 (PostGIS) can only execute directly against PostgreSQL data sources.

**Supported Data Sources:**
- ✅ **PostgreSQL/PostGIS** - Native Tier 2 execution
- ❌ **GeoPackage** - SQLite-based, not accessible from PostGIS
- ❌ **Shapefile** - File-based, no SQL interface
- ❌ **External OGC WFS** - HTTP endpoints, not database tables
- ❌ **Oracle Spatial** - Different RDBMS, not PostGIS

**Implementation Strategy (Hybrid Approach):**

1. **Data Source Compatibility Check**
   ```csharp
   public async Task<ProcessExecutionTier> SelectTierAsync(
       ProcessDefinition process,
       ProcessExecutionRequest request,
       CancellationToken ct)
   {
       // Check tenant policy override first
       var policy = _controlPlane.GetTenantPolicyOverride(request.TenantId, process.Id);
       if (policy.ForceTier.HasValue)
           return policy.ForceTier.Value;

       // Get data source for input layer
       var layerRef = request.Inputs.GetValueOrDefault("layer") as string;
       if (!string.IsNullOrEmpty(layerRef))
       {
           var layer = await _metadata.GetLayerAsync(layerRef, ct);
           var dataSource = await _metadata.GetDataSourceAsync(layer.DataSourceId, ct);

           // Remove PostGIS from candidates if data not in PostgreSQL
           var candidateTiers = process.PreferredTiers.ToList();
           if (dataSource.Provider.ToLowerInvariant() != "postgres")
           {
               candidateTiers.Remove(ProcessExecutionTier.PostGIS);
               _logger.LogInformation(
                   "Removed PostGIS tier for process {ProcessId} - data source {DataSourceId} is {Provider}, not PostgreSQL",
                   process.Id, dataSource.Id, dataSource.Provider);
           }

           // Return first compatible tier
           return candidateTiers.FirstOrDefault(ProcessExecutionTier.NTS);
       }

       // No layer reference, use default
       return process.PreferredTiers.FirstOrDefault(ProcessExecutionTier.NTS);
   }
   ```

2. **Fallback Behavior**
   - If process prefers `[PostGIS, NTS]` but data is in GeoPackage → automatically use NTS
   - Log the downgrade with reason: "Tier 2 (PostGIS) unavailable for non-PostgreSQL source"
   - If process requires PostGIS (e.g., `[PostGIS]` only) → reject with error:
     ```
     "Process 'voronoi' requires PostgreSQL data source. Layer 'roads' uses GeoPackage provider."
     ```

3. **Process Definition Constraints**
   ```yaml
   # docs/processes/voronoi.yaml
   id: voronoi
   title: Voronoi Diagram
   preferredTiers: [postgis, nts]  # Fallback to NTS if data not in Postgres
   hints:
     requiresPostgres: false  # Allow fallback

   # docs/processes/complex-heatmap.yaml
   id: complex-heatmap
   title: Complex Heatmap (PostGIS Only)
   preferredTiers: [postgis]  # No fallback - PostGIS required
   hints:
     requiresPostgres: true  # Reject if data not in Postgres
   ```

4. **Future: Automatic Materialization (Phase 3+)**
   For processes that benefit significantly from PostGIS but need to support non-PostgreSQL sources:
   ```csharp
   // Phase 3+: Materialize to temp table
   if (dataSource.Provider != "postgres" && process.AllowMaterialization)
   {
       var tempTable = await _materializer.MaterializeToPostgresAsync(
           layerRef, request.Inputs, ct);
       request.Inputs["__materialized_table"] = tempTable;
       return ProcessExecutionTier.PostGIS;
   }
   ```
   **Trade-offs:**
   - ✅ Enables PostGIS for all data sources
   - ⚠️ Performance hit (double I/O: read source → write temp table)
   - ⚠️ Requires FDW setup or application-level ETL
   - ⚠️ Temp table cleanup on failures

**Recommendation:** Start with hybrid approach (automatic fallback). Add materialization only if specific high-value processes justify the complexity.

#### Tier 3: Cloud-Native Python Execution
**Status:** 🆕 New (Recommended)
**Use Cases:** Machine learning, advanced interpolation, raster analytics, GPU operations
**Technology:** Managed batch services (AWS Batch, Azure Batch, GCP Cloud Batch)
**Latency:** 10 seconds - 30 minutes
**Libraries:** GeoPandas, Rasterio, scikit-learn, scipy, PyTorch, RAPIDS

**Operations to Implement:**
- **Interpolation:** IDW, Kriging, Spline (scipy)
- **Machine Learning:** Clustering (DBSCAN, K-means), classification
- **Raster Analysis:** NDVI, slope/aspect, raster algebra
- **Network Analysis:** NetworkX integration
- **Statistical Analysis:** Spatial autocorrelation, hot spot analysis
- **GPU Acceleration:** RAPIDS cuSpatial, PyTorch geospatial models

**Why Cloud Batch Services:**

| Aspect | Cloud Batch ✅ | Local Subprocess ⚠️ | Python.NET ❌ |
|--------|---------------|---------------------|---------------|
| Isolation | Container isolation | Process isolation | Shared memory |
| Scalability | 0 to 1000s of workers | Limited by host CPU | Limited by GIL |
| GPU Support | Yes (spot instances) | Complex setup | No |
| Cost | Pay per job | Always-on VMs | Included |
| Fault Tolerance | Auto-retry, DLQ | Manual handling | Manual handling |
| State Management | Built-in persistent queue | Hangfire required | Hangfire required |

**Cloud Provider Options:**

| Provider | Service | Cost (1000 jobs/day, avg 5 min) | GPU Support | Spot Instances |
|----------|---------|----------------------------------|-------------|----------------|
| **AWS** | AWS Batch | ~$96/month (Spot: $29) | Yes (G4, P3) | 70% savings |
| **Azure** | Azure Batch | ~$72/month (Low-priority: $22) | Yes (NC, ND) | 70-90% savings |
| **GCP** | Cloud Batch | ~$60/month (Spot: $3) | Yes (T4, A100) | 91% savings |

**Recommendation:** Start with **embedded Tier 1/2** for development, migrate directly to **cloud batch (Tier 3)** when advanced analytics or scale demands it.

**Deployment Architecture:**

```
┌──────────────────────────────────────┐
│  Honua.Server.Host (Private VNet)   │
│  - Submits jobs to batch service     │
│  - Pulls completion events from queue│
└────────────┬─────────────────────────┘
             │ (outbound HTTPS)
             ↓
┌────────────────────────────────────────┐
│  AWS Batch / Azure Batch / Cloud Batch │
│  - Manages job queue                   │
│  - Scales workers 0→N                  │
│  - Publishes completion events         │
└────────────┬───────────────────────────┘
             │
             ↓
┌────────────────────────────────────────┐
│  EventBridge/Event Grid/Pub/Sub       │
│  → SQS/Service Bus Queue               │
│  → Honua BackgroundService consumes   │
└────────────────────────────────────────┘
```

**Security:** Honua does NOT need a public IP - it **pulls** messages from the queue (outbound only).

---

## Job Management Strategy

### Tier 1 & 2: Queue Strategy Depends on Deployment

**For Tier 1 (NTS) and Tier 2 (PostGIS):**

#### Option A: Single-Instance Deployment (Simple)

Use ASP.NET Core `BackgroundService` with in-memory queue - **no Hangfire needed**.

```csharp
// Program.cs
builder.Services.AddSingleton<IProcessJobQueue, InMemoryProcessJobQueue>();
builder.Services.AddHostedService<GeoprocessingBackgroundService>();
```

**Pros:**
- ✅ Simpler architecture
- ✅ No additional dependencies
- ✅ Faster job execution (no serialization overhead)

**Use When:**
- Single Honua instance (1 pod/container)
- Development environment
- Small deployments

#### Option B: Clustered Deployment (Kubernetes/Multi-Instance) ✅

Use **Hangfire with shared PostgreSQL storage** for distributed job queue.

```csharp
// Program.cs
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

**Pros:**
- ✅ Shared job queue across all instances
- ✅ Even load distribution
- ✅ Job persistence (survives pod restarts)
- ✅ Hangfire dashboard for monitoring

**Use When:**
- **Clustered Honua (2+ replicas in K8s)** ← Most production deployments
- High availability required
- Need job persistence

**Recommendation for Production:** If you're running Honua clustered (Kubernetes with 2+ replicas), **use Hangfire for Tier 1 & 2**.

### Tier 3: Cloud Batch Services (Event-Driven)

**For Tier 3 (Python/GPU), use managed batch services with event-driven completion:**

**Job Submission Pattern:**

⚠️ **CRITICAL: Payload Size and Security Constraints**

Cloud batch services impose strict limits on environment variables:
- **AWS Batch:** 8 KB total environment variable size
- **Azure Batch:** 4 KB per variable, 32 KB total
- **GCP Cloud Batch:** 32 KB total
- **Security Risk:** Environment variables are logged in plain text to CloudWatch/Azure Monitor/Cloud Logging

**❌ INCORRECT APPROACH (DO NOT USE):**
```csharp
// ❌ DO NOT DO THIS - Parameters will truncate silently if >4KB
cloudTask.EnvironmentSettings = new List<EnvironmentSetting>
{
    new("PARAMETERS", JsonSerializer.Serialize(request.Parameters))  // ❌ Size limit violation!
};
```

**✅ CORRECT APPROACH: Stage Artifacts to Blob Storage**

```csharp
// Honua.Server.Host/Geoprocessing/CloudBatchExecutor.cs
public class AzureBatchExecutor : ICloudBatchExecutor
{
    private readonly BatchClient _batch;
    private readonly IProcessRunRepository _processRuns;
    private readonly IArtifactStore _artifacts;

    public async Task<string> SubmitJobAsync(ProcessExecutionRequest request, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString();

        // 1. Store job metadata in Honua's database
        await _processRuns.CreateAsync(new ProcessRun
        {
            JobId = jobId,
            ProcessId = request.ProcessId,
            Status = ProcessStatus.Submitted,
            TenantId = request.TenantId
        });

        // 2. Stage input parameters to blob storage (no size limit, encrypted at rest)
        var inputEnvelope = await _artifacts.StageInputAsync(jobId, request.Parameters, ct);

        // 3. Submit to Azure Batch - pass ONLY the secure URL
        var cloudTask = new CloudTask(jobId, $"python /app/process.py");
        cloudTask.EnvironmentSettings = new List<EnvironmentSetting>
        {
            new("JOB_ID", jobId),
            new("HONUA_INPUT_URI", inputEnvelope.Uri),  // ✅ Just URL (~200 bytes)
            new("HONUA_OUTPUT_CONTAINER", _config["Storage:Container"]),
            new("TRACEPARENT", Activity.Current?.Id ?? string.Empty)  // Distributed tracing
        };

        await _batch.JobOperations.AddTaskAsync("geoprocessing-pool", cloudTask, cancellationToken: ct);

        return jobId;
    }
}
```

**Event-Driven Completion (Zero Polling):**

```csharp
// Honua.Server.Host/Geoprocessing/BatchCompletionService.cs
public class AzureBatchCompletionService : BackgroundService
{
    private readonly ServiceBusClient _serviceBus;
    private readonly IProcessRunRepository _processRuns;
    private readonly ILogger<AzureBatchCompletionService> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var receiver = _serviceBus.CreateReceiver("batch-completion");

        await foreach (var message in receiver.ReceiveMessagesAsync(ct))
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<EventGridEvent>(message.Body);

                if (eventData.EventType == "Microsoft.Batch.TaskCompleted")
                {
                    var data = eventData.Data.ToObject<BatchTaskCompletedData>();
                    var jobId = data.TaskId;

                    await _processRuns.UpdateStatusAsync(jobId,
                        data.ExitCode == 0 ? ProcessStatus.Succeeded : ProcessStatus.Failed,
                        resultUri: data.OutputBlobUri);

                    await receiver.CompleteMessageAsync(message, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch completion event");
                await receiver.AbandonMessageAsync(message, cancellationToken: ct);
            }
        }
    }
}
```

**Infrastructure Setup (One-Time):**

```bash
# Azure: Event Grid → Service Bus
az eventgrid event-subscription create \
  --name honua-batch-completion \
  --source-resource-id /subscriptions/.../providers/Microsoft.Batch/batchAccounts/honua \
  --endpoint-type servicebusqueue \
  --endpoint /subscriptions/.../providers/Microsoft.ServiceBus/namespaces/honua/queues/batch-completion \
  --included-event-types Microsoft.Batch.TaskCompleted

# AWS: EventBridge → SQS
aws events put-rule --name honua-batch-completion \
  --event-pattern '{"source":["aws.batch"],"detail-type":["Batch Job State Change"],"detail":{"status":["SUCCEEDED","FAILED"]}}'

aws events put-targets --rule honua-batch-completion \
  --targets Id=1,Arn=arn:aws:sqs:us-east-1:123456789012:honua-batch-completion

# GCP: Cloud Logging → Pub/Sub
gcloud logging sinks create honua-batch-completion \
  pubsub.googleapis.com/projects/honua/topics/batch-completion \
  --log-filter='resource.type="cloud_batch_job" AND severity="INFO" AND jsonPayload.message="Job completed"'
```

**Networking Security:**
- ✅ Honua **pulls** from queue (outbound HTTPS only)
- ✅ No public IP required for Honua
- ✅ Use VPC Endpoints (AWS), Private Link (Azure), Private Service Connect (GCP)
- ✅ Minimal attack surface

**State Persistence:**
- Batch service maintains internal persistent queue (like Hangfire, but managed)
- Honua database caches job metadata for API queries
- Event-driven updates keep cache in sync

### Hybrid Execution Strategy

The Control Plane consolidates all capacity management, scheduling, and auditing into a single boundary with three clear responsibilities:

**Control Plane Responsibilities:**

1. **Admission** - Policy checks, quotas, capacity reservation
2. **Scheduling** - Queue management, job tracking, tier selection
3. **Auditing** - Provenance, cost tracking, telemetry (ProcessRun is source of truth)

```csharp
/// <summary>
/// Single boundary for all geoprocessing orchestration.
/// Consolidates queue governance, scheduling, policy enforcement, and provenance tracking.
/// </summary>
public interface IControlPlane
{
    // ══════════════════════════════════════════════════════════════
    // ADMISSION: Policy checks, quotas, capacity reservation
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks admission policy and reserves capacity for the request.
    /// Returns reservation if admitted, null if rejected.
    /// </summary>
    Task<AdmissionDecision> AdmitAsync(ProcessExecutionRequest request, CancellationToken ct);

    /// <summary>
    /// Retrieves per-tenant policy overrides (sync vs async, tier preferences, cost caps).
    /// Same process definition can run with different defaults per tenant.
    /// </summary>
    TenantPolicyOverride GetTenantPolicyOverride(string tenantId, string processId);

    // ══════════════════════════════════════════════════════════════
    // SCHEDULING: Queue management, job tracking, tier selection
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Enqueues admitted job with capacity reservation.
    /// Returns ProcessRun record (source of truth for scheduling/billing/provenance).
    /// </summary>
    Task<ProcessRun> EnqueueAsync(AdmissionDecision decision, CancellationToken ct);

    /// <summary>
    /// Executes inline (sync) when capacity permits and policy allows.
    /// Updates ProcessRun record with execution details.
    /// </summary>
    Task<ProcessResult> ExecuteInlineAsync(AdmissionDecision decision, CancellationToken ct);

    /// <summary>
    /// Retrieves job status from ProcessRun (single source of truth).
    /// </summary>
    Task<ProcessRun?> GetJobStatusAsync(string jobId, CancellationToken ct);

    /// <summary>
    /// Cancels job and updates ProcessRun record.
    /// </summary>
    Task<bool> CancelJobAsync(string jobId, CancellationToken ct);

    // ══════════════════════════════════════════════════════════════
    // AUDITING: Provenance, cost tracking, telemetry
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Records job completion with full provenance (tier, duration, cost, artifacts).
    /// Updates ProcessRun as source of truth for billing queries.
    /// </summary>
    Task RecordCompletionAsync(string jobId, ProcessResult result, ProcessExecutionTier tier, TimeSpan duration, CancellationToken ct);

    /// <summary>
    /// Records job failure with error details.
    /// </summary>
    Task RecordFailureAsync(string jobId, Exception error, CancellationToken ct);

    /// <summary>
    /// Queries ProcessRun records for billing/reporting (source of truth).
    /// Guards against "shadow state" across Hangfire, tracing, and artifact storage.
    /// </summary>
    Task<IReadOnlyList<ProcessRun>> QueryRunsAsync(ProcessRunQuery query, CancellationToken ct);
}
```

**Simplified Job Manager Using Control Plane:**

```csharp
public sealed class GeoprocessingJobManager : IGeoprocessingJobManager
{
    private readonly IControlPlane _controlPlane;
    private readonly IProcessExecutionCoordinator _coordinator;

    public async Task<JobTicket> SubmitAsync(ProcessExecutionRequest request, CancellationToken ct)
    {
        // Single admission check - policy, quotas, capacity
        var admission = await _controlPlane.AdmitAsync(request, ct);

        if (!admission.Admitted)
        {
            throw new ProcessAdmissionException(admission.Reason, admission.RetryAfter);
        }

        // Execute inline if policy permits and capacity allows
        if (admission.ExecutionMode == ExecutionMode.Sync)
        {
            var result = await _controlPlane.ExecuteInlineAsync(admission, ct);
            return JobTicket.Completed(result);
        }

        // Otherwise enqueue (ProcessRun becomes source of truth)
        var processRun = await _controlPlane.EnqueueAsync(admission, ct);
        return JobTicket.Queued(processRun.Id);
    }
}
```

**Resource Classes (configurable per tenant via policy overrides):**

| Class | Typical Tier | Limits | Notes |
|-------|--------------|--------|-------|
| `CpuBurst` | NTS, lightweight PostGIS | ≤ 30s CPU, ≤ 512 MiB RAM | Executes inline when headroom exists. |
| `DbHeavy` | PostGIS | ≤ 5 min runtime, ≤ 8 concurrent connections | Enforced via Control Plane + Postgres role limits. |
| `PythonGpu` | Python | ≤ 60 min, GPU quota 1, RAM 8–32 GiB | Requires dedicated worker pool; always async. |
| `LongTail` | Any | ≥ 60 min, large output footprints | Queued with cost approvals and budget tracking. |

**Control Plane consolidates these previously scattered responsibilities:**
  - ✅ Tracks per-tenant concurrency, CPU seconds, and PostGIS connection budgets (was IQueueGovernor)
  - ✅ Applies rate limits before enqueuing to protect background workers from stampedes (was IQueueGovernor)
  - ✅ Surfaces `429 Too Many Requests` with retry hints when limits are hit (was IQueueGovernor)
  - ✅ Manages job queue and tracks status (was IGeoprocessingJobScheduler)
  - ✅ Enforces per-tenant policy overrides without cloning processes (was PolicyService)
  - ✅ Maintains ProcessRun as single source of truth for scheduling/billing/provenance (was scattered)
  - ✅ Negotiates resource classes with Kubernetes pod autoscalers via custom metrics (`honua.geoprocessing.capacity.available`)

```csharp
public enum ResourceClass
{
    CpuBurst,
    DbHeavy,
    PythonGpu,
    LongTail
}
```

**Control Plane Supporting Types:**

```csharp
/// <summary>
/// Admission decision from Control Plane - replaces separate governor/scheduler calls
/// </summary>
public sealed record AdmissionDecision(
    bool Admitted,
    ExecutionMode ExecutionMode,
    ResourceClass ResourceClass,
    ProcessExecutionRequest Request,
    string? Reason = null,
    TimeSpan? RetryAfter = null);

/// <summary>
/// Per-tenant policy overrides - same process, different execution defaults
/// </summary>
public sealed record TenantPolicyOverride(
    string TenantId,
    string ProcessId,
    ExecutionMode? PreferredMode = null,          // Force sync or async
    IReadOnlyList<ProcessExecutionTier>? TierOrder = null,  // Override tier preferences
    ResourceClass? ResourceClass = null,          // Override resource class
    double? CostCapUnits = null);                 // Per-job cost limit

/// <summary>
/// ProcessRun is the single source of truth for all job tracking
/// Guards against "shadow state" across Hangfire, tracing, and artifact storage
/// </summary>
public sealed class ProcessRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string ProcessId { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public ProcessParameters Parameters { get; init; } = new();
    public ResourceClass ResourceClass { get; init; }
    public ExecutionMode Mode { get; init; }

    // Scheduling (Control Plane responsibility)
    public ProcessRunStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ProcessExecutionTier? ExecutedTier { get; set; }
    public TimeSpan? Duration => CompletedAt - StartedAt;

    // Billing (Control Plane responsibility)
    public double CostUnits { get; set; }
    public double EstimatedCostUnits { get; init; }

    // Provenance (Control Plane responsibility)
    public string? ResultArtifactUri { get; set; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? ErrorMessage { get; set; }
}

public enum ProcessRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum ExecutionMode
{
    Sync,
    Async
}

public sealed record ProcessRunQuery(
    string? TenantId = null,
    string? ProcessId = null,
    ProcessRunStatus? Status = null,
    DateTimeOffset? SubmittedAfter = null,
    DateTimeOffset? SubmittedBefore = null,
    int Limit = 100);
```

```csharp
public sealed record ProcessExecutionEnvelope(
    string ProcessId,
    ProcessParameters Parameters,
    ResourceClass ResourceClass,
    ProcessExecutionHints Hints,
    ClaimsPrincipal Principal,
    DateTimeOffset SubmittedAt)
{
    public static ProcessExecutionEnvelope FromRequest(ProcessExecutionRequest request)
    {
        return new ProcessExecutionEnvelope(
            request.ProcessId,
            request.Parameters,
            request.Estimate.ResourceClass,
            request.Hints,
            request.Principal,
            DateTimeOffset.UtcNow);
    }
}
```

```csharp
public class ProcessExecutionRequest
{
    public string ProcessId { get; init; } = default!;
    public ProcessParameters Parameters { get; init; } = new();
    public ExecutionEstimate Estimate { get; init; } = new(ResourceClass.CpuBurst, TimeSpan.FromSeconds(1), 1);
    public ProcessExecutionHints Hints { get; init; } = new(TimeSpan.FromSeconds(1), 1, Array.Empty<string>());
    public ExecutionOptions? Execution { get; init; }

    [JsonIgnore]
    public ClaimsPrincipal Principal { get; set; } = new ClaimsPrincipal();
}

public record ExecutionEstimate(
    ResourceClass ResourceClass,
    TimeSpan ExpectedDuration,
    double EstimatedCostUnits);

public record ExecutionOptions(string Mode);
```

```csharp
/// <summary>
/// Job ticket returned from job submission
/// </summary>
public sealed record JobTicket(string JobId, ProcessResult? Result)
{
    public static JobTicket Completed(ProcessResult result) => new("inline", result);
    public static JobTicket Queued(string jobId) => new(jobId, null);
}
```

---

## Deployment Architecture

### Phase 1: Embedded (Development & Small Deployments)

```yaml
┌──────────────────────────────────────┐
│  Honua.Server.Host                   │
│  ├─ API Layer (2 surfaces)           │
│  │  - GeoservicesREST GPServer       │
│  │  - OGC API Processes              │
│  ├─ BackgroundService (in-memory)    │
│  │  ├─ Tier 1: NTS operations        │
│  │  └─ Tier 2: PostGIS operations    │
│  └─ (No Tier 3 Python in Phase 1)   │
└──────────────────────────────────────┘
```

**Pros:**
- ✅ Simple deployment (one container)
- ✅ No external dependencies
- ✅ Shared connection pools
- ✅ Easier debugging
- ✅ Lower operational overhead

**Cons:**
- ⚠️ CPU-intensive processing affects API responsiveness
- ⚠️ Can't scale geoprocessing independently
- ⚠️ No Python/GPU support

**When to Use:**
- Development environments
- < 100 concurrent processes/day
- < 10 active processing jobs
- Single-team operations
- No Python/ML requirements

**Docker Compose:**
```yaml
version: '3.8'
services:
  honua:
    image: honua/server:latest
    environment:
      - Processing__Mode=Embedded
      - Processing__Tier1Enabled=true
      - Processing__Tier2Enabled=true
      - Processing__Tier3Enabled=false
    ports:
      - "5000:8080"
    depends_on:
      - postgres
```

### Phase 2: Local Python Workers (Optional - Hybrid Scale)

**Note:** Phase 2 is optional. Most deployments should go directly from Phase 1 (embedded) to Phase 3 (cloud-native).

Use this phase only if you need local Python execution (not cloud batch) and high scale.

```yaml
┌─────────────────────────┐         ┌──────────────────────────┐
│  Honua.Server.Host      │         │  Honua.GeoProcessing     │
│  ├─ API Layer           │         │  Worker                  │
│  ├─ Tier 1 & 2          │◄───────►│  ├─ Python Runtime       │
│  └─ Job Queue           │  Queue  │  ├─ GDAL/Rasterio        │
└─────────────────────────┘         │  └─ ML Libraries         │
                                    └──────────────────────────┘
```

**Implementation:** Use Hangfire with shared PostgreSQL queue for this phase.

**Pros:**
- ✅ Independent scaling (API vs processing)
- ✅ Crash isolation (Python failures don't affect API)
- ✅ Different resource profiles (CPU-heavy workers)
- ✅ GPU support (deploy workers on GPU-enabled nodes)

**Cons:**
- ⚠️ More operational complexity than Phase 1 or Phase 3
- ⚠️ Need Hangfire dependency
- ⚠️ Need shared PostgreSQL for job queue
- ⚠️ More deployment artifacts
- ⚠️ Higher cost than cloud batch

**When to Use:**
- ⚠️ Rare - only if you can't use cloud batch services
- \> 100 concurrent processes/day
- Local Python execution required (compliance/security)
- On-premises deployment

**Recommendation:** Skip Phase 2 and go directly to Phase 3 (cloud batch) for most deployments.

---

### Phase 3: Cloud-Native (Recommended for Production)

```
┌──────────────────────────────────────────┐
│  Honua.Server.Host (Private VNet)       │
│  ├─ API Layer (2 surfaces)              │
│  │  - GeoservicesREST GPServer          │
│  │  - OGC API Processes                 │
│  ├─ Tier 1 & 2 (in-process/Hangfire)    │
│  └─ Submits Tier 3 to batch service     │
└────────────┬─────────────────────────────┘
             │ (outbound HTTPS)
             ↓
┌──────────────────────────────────────────┐
│  AWS Batch / Azure Batch / Cloud Batch  │
│  ├─ Auto-scaling 0→1000s workers        │
│  ├─ Python + ML libraries               │
│  ├─ GPU support (Spot instances)        │
│  └─ Publishes completion events         │
└────────────┬─────────────────────────────┘
             │
             ↓
┌──────────────────────────────────────────┐
│  EventBridge → SQS                       │
│  Event Grid → Service Bus                │
│  Cloud Logging → Pub/Sub                 │
│  ↓                                       │
│  Honua BackgroundService consumes queue │
└──────────────────────────────────────────┘
```

**Infrastructure:**
- **AWS**: EventBridge → SQS → Honua consumes via long-polling
- **Azure**: Event Grid → Service Bus → Honua consumes via ReceiveMessagesAsync
- **GCP**: Cloud Logging → Pub/Sub → Honua subscribes

**Pros:**
- ✅ **Zero baseline cost** - batch service scales to zero when idle
- ✅ **Massive scalability** - 0 to 1000s of workers on demand
- ✅ **GPU support** - Spot instances 70-91% cheaper
- ✅ **Fully managed queue** - no Hangfire needed for Tier 3
- ✅ **Event-driven** - zero polling, Honua stays private
- ✅ **No public IP required** - Honua pulls from queue (outbound only)
- ✅ **Built-in fault tolerance** - auto-retry, DLQ, health checks

**Cons:**
- ⚠️ Cloud vendor dependency
- ⚠️ Cold start latency (5-30s first job)
- ⚠️ More infrastructure setup (one-time)

**When to Use:**
- \> 500 concurrent processes/day
- \> 50 active processing jobs
- Production at scale
- Need GPU acceleration (ML/raster processing)
- Cost optimization important
- Multi-cloud strategy

**Cost Comparison** (1000 jobs/day, avg 5 min/job):

| Provider | Always-On VMs | Batch with Spot | Savings |
|----------|---------------|-----------------|---------|
| **AWS** | ~$350/month | ~$29/month | 92% |
| **Azure** | ~$280/month | ~$22/month | 92% |
| **GCP** | ~$240/month | ~$3/month | 99% |

**Setup Example (Azure):**

```bash
# 1. Create batch account
az batch account create --name honua-batch --resource-group honua --location eastus

# 2. Create pool with low-priority VMs
az batch pool create \
  --id python-pool \
  --image canonical:ubuntuserver:18.04-lts \
  --node-agent-sku-id "batch.node.ubuntu 18.04" \
  --target-low-priority-nodes 10 \
  --vm-size Standard_D2s_v3

# 3. Create Service Bus queue
az servicebus queue create --resource-group honua --namespace-name honua --name batch-completion

# 4. Create Event Grid subscription
az eventgrid event-subscription create \
  --name batch-to-servicebus \
  --source-resource-id /subscriptions/.../providers/Microsoft.Batch/batchAccounts/honua-batch \
  --endpoint-type servicebusqueue \
  --endpoint /subscriptions/.../providers/Microsoft.ServiceBus/namespaces/honua/queues/batch-completion \
  --included-event-types Microsoft.Batch.TaskCompleted
```

**Honua Configuration:**

```json
{
  "Geoprocessing": {
    "Tier3": {
      "Provider": "AzureBatch",
      "BatchAccount": "honua-batch",
      "PoolId": "python-pool",
      "CompletionQueue": "batch-completion"
    }
  }
}
```

---

## Implementation Details

### Tier 2: PostGIS Executor

```csharp
// src/Honua.Server.Core/Processing/PostGisProcessingExecutor.cs
public class PostGisProcessingExecutor : IPostGisExecutor
{
    private readonly IDbConnectionFactory _connections;
    private readonly IPostGisProcedureRegistry _procedures;
    private readonly IArtifactStore _artifacts;
    private readonly ILogger<PostGisProcessingExecutor> _logger;

    public Task<ProcessResult> BufferAsync(ProcessParameters parameters, CancellationToken ct)
        => ExecuteAsync("buffer", parameters, ct);

    public Task<ProcessResult> VoronoiAsync(ProcessParameters parameters, CancellationToken ct)
        => ExecuteAsync("voronoi", parameters, ct);

    public Task<ProcessResult> InterpolateIdwAsync(ProcessParameters parameters, CancellationToken ct)
        => ExecuteAsync("interpolate_idw", parameters, ct);

    private async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var procedure = _procedures.Resolve(processId);

        await using var connection = await _connections.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = procedure.SchemaQualifiedName; // honua_gp.voronoi
        command.CommandType = CommandType.StoredProcedure;
        procedure.BindParameters(command, parameters);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            var outputs = await procedure.MaterializeOutputsAsync(reader, ct);
            var artifacts = await _artifacts.StageAsync(outputs.Artifacts, ct);

            return new ProcessResult(outputs.Values, artifacts, ProcessExecutionTier.PostGIS);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.QueryCanceled)
        {
            throw new CapacityRejectedException("PostGIS execution timed out", ex);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "PostGIS procedure {Procedure} failed for process {ProcessId}", procedure.SchemaQualifiedName, processId);
            throw;
        }
    }
}
```

- **Procedure registry controls surface area:** `IPostGisProcedureRegistry` maps process IDs to schema-qualified stored procedures, validates parameter metadata, and enforces an allow-list of views/tables each procedure can touch.
- **Prepared statements stay warm:** Stored procedures encapsulate SQL and leverage PostgreSQL plan caching instead of regenerating dynamic SQL per request.
- **Artifacts stay out of the database:** Large rasters/vector sets are streamed to the artifact store from PL/pgSQL functions, returned to .NET as lightweight descriptors.

### Tier 3: Cloud Batch Python Executor

```csharp
// src/Honua.Server.Core/Processing/CloudBatchPythonExecutor.cs
public sealed class CloudBatchPythonExecutor : ICloudBatchPythonExecutor
{
    private readonly ICloudBatchClient _batch;
    private readonly IArtifactStore _artifacts;
    private readonly ILogger<CloudBatchPythonExecutor> _logger;

    public Task<ProcessResult> InterpolateIdwAsync(ProcessParameters parameters, CancellationToken ct)
        => ExecuteAsync("interpolate-idw", parameters, ct);

    private async Task<ProcessResult> ExecuteAsync(string containerTask, ProcessParameters parameters, CancellationToken ct)
    {
        var inputEnvelope = await _artifacts.StageInputAsync(containerTask, parameters, ct);

        var submission = await _batch.SubmitAsync(new CloudBatchJobRequest
        {
            TaskName = containerTask,
            ContainerImage = $"honua-gp-{containerTask}:latest",
            InputUri = inputEnvelope.Uri,
            ResourceClass = ResourceClass.PythonGpu,
            Environment =
            {
                ["TRACEPARENT"] = Activity.Current?.Id ?? string.Empty,
                ["HONUA_INPUT_URI"] = inputEnvelope.Uri
            }
        }, ct);

        var completion = await _batch.WaitForCompletionAsync(submission.JobId, ct);
        if (!completion.Succeeded)
        {
            _logger.LogError("Cloud batch job {JobId} failed: {Reason}", submission.JobId, completion.Error);
            await _artifacts.PersistLogsAsync(completion.LogUri, completion.ErrorLog, ct);
            throw new ProcessExecutionException($"Cloud batch job '{submission.JobId}' failed: {completion.Error}");
        }

        var response = await _artifacts.ReadJsonAsync<PythonProcessResponse>(completion.OutputUri, ct)
            ?? throw new InvalidOperationException("Cloud batch job returned null payload");

        var artifacts = await _artifacts.StageAsync(response.Artifacts, ct);
        return new ProcessResult(response.Outputs, artifacts, ProcessExecutionTier.Python);
    }
}

public interface ICloudBatchClient
{
    Task<CloudBatchSubmission> SubmitAsync(CloudBatchJobRequest request, CancellationToken ct);
    Task<CloudBatchCompletion> WaitForCompletionAsync(string jobId, CancellationToken ct);
}

public sealed record CloudBatchJobRequest(
    string TaskName,
    string ContainerImage,
    string InputUri,
    ResourceClass ResourceClass,
    IDictionary<string, string> Environment);

public sealed record CloudBatchSubmission(string JobId);

public sealed record CloudBatchCompletion(
    bool Succeeded,
    string OutputUri,
    string LogUri,
    string? Error,
    string? ErrorLog);
```

**Python Script Template:**
```python
# Docker image: ghcr.io/honua/geoprocessing/interpolate-idw:latest
import json
import os
from pathlib import Path

import fsspec
import geopandas as gpd
import numpy as np
from scipy.interpolate import griddata

INPUT_URI = os.environ["HONUA_INPUT_URI"]
ARTIFACT_DIR = Path(os.environ["HONUA_ARTIFACT_DIR"])

with fsspec.open(INPUT_URI, "r") as stream:
    params = json.load(stream)

gdf = gpd.read_file(params["input_layer"])
points = np.array([[p.x, p.y] for p in gdf.geometry])
values = gdf[params["value_field"]].values

extent = params["extent"]
resolution = params.get("grid_resolution", 100)

grid_x, grid_y = np.mgrid[
    extent[0] : extent[2] : complex(0, resolution),
    extent[1] : extent[3] : complex(0, resolution)
]

grid_z = griddata(points, values, (grid_x, grid_y), method="linear")

features = []
for i in range(grid_x.shape[0]):
    for j in range(grid_x.shape[1]):
        if not np.isnan(grid_z[i, j]):
            features.append(
                {
                    "type": "Feature",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [grid_x[i, j], grid_y[i, j]],
                    },
                    "properties": {"interpolated_value": float(grid_z[i, j])},
                }
            )

artifact_path = ARTIFACT_DIR / "interpolate-idw.geojson"
artifact_path.write_text(json.dumps({"type": "FeatureCollection", "features": features}))

print(
    json.dumps(
        {
            "outputs": {
                "summary": {
                    "feature_count": len(features),
                    "extent": extent,
                }
            },
            "artifacts": [
                {
                    "rel": "collection",
                    "path": artifact_path.name,
                    "type": "application/geo+json",
                }
            ],
        }
    )
)
```

### Process Registry

```csharp
// src/Honua.Server.Core/Processing/ProcessDefinition.cs
public record ProcessDefinition(
    string Id,
    string Title,
    string Description,
    string Version,
    IReadOnlyList<ProcessExecutionTier> PreferredTiers,
    ProcessExecutionHints Hints,
    IReadOnlyList<ProcessParameter> Parameters,
    Func<ProcessExecutionContext, CancellationToken, Task<ProcessResult>> ExecuteAsync);

public sealed record ProcessExecutionContext(
    ProcessParameters Parameters,
    ProcessExecutionTier Tier);

public record ProcessExecutionHints(
    TimeSpan TargetP95,
    double EstimatedCostUnits,
    string[] RequiredCapabilities);

public enum ProcessExecutionTier
{
    NetTopologySuite,  // Tier 1: Fast, synchronous
    PostGIS,           // Tier 2: Database-side
    Python             // Tier 3: Python interop
}

public record ProcessParameter(
    string Name,
    string Description,
    ProcessParameterType Type,
    bool Required,
    object? DefaultValue = null);

public enum ProcessParameterType
{
    String,
    Number,
    Integer,
    Boolean,
    BoundingBox,
    Geometry,
    FeatureCollection
}

### Declarative Process Catalog

- Process definitions live in `docs/processes/*.yaml` and ship with the application.
- Catalog entries describe metadata, parameter schema, preferred tiers, and executor bindings per tier.
- Non-engineers can add/modify processes; the control plane enforces tenant-specific overrides without duplicating definitions.

```yaml
# docs/processes/buffer.yaml
id: buffer
title: Buffer Geometries
description: Creates buffer polygons around input geometries.
version: 1.0.0
preferredTiers: [postgis, nettopologysuite]
hints:
  targetP95: 00:00:200
  estimatedCostUnits: 1
  requiredCapabilities: []
parameters:
  - name: geometries
    description: Input geometries
    type: FeatureCollection
    required: true
  - name: distance
    description: Buffer distance
    type: Number
    required: true
  - name: unit
    description: Distance unit
    type: String
    required: false
    defaultValue: meters
handlers:
  nettopologysuite:
    service: geometryOperationExecutor
    method: BufferAsync
  postgis:
    service: postGisExecutor
    method: BufferAsync
```

```csharp
public interface IProcessRegistry
{
    IReadOnlyCollection<ProcessDefinition> GetProcesses();
    ProcessDefinition? GetProcess(string id);
}

public sealed class ProcessCatalogRegistry : IProcessRegistry
{
    private readonly Dictionary<string, ProcessDefinition> _processes;

    public ProcessCatalogRegistry(IFileProvider catalogProvider, IServiceProvider services)
    {
        _processes = ProcessCatalog.Load(catalogProvider, services);
    }

    public IReadOnlyCollection<ProcessDefinition> GetProcesses() => _processes.Values;

    public ProcessDefinition? GetProcess(string id)
        => _processes.TryGetValue(id, out var definition) ? definition : null;
}

public static class ProcessCatalog
{
    public static Dictionary<string, ProcessDefinition> Load(IFileProvider provider, IServiceProvider services)
    {
        var definitions = new Dictionary<string, ProcessDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in provider.GetDirectoryContents("/"))
        {
            if (!file.Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);
            var document = ProcessDocument.Parse(reader.ReadToEnd());

            definitions[document.Id] = document.ToDefinition(services);
        }

        return definitions;
    }
}
```

```csharp
public sealed class ProcessExecutionCoordinator : IProcessExecutionCoordinator
{
    private readonly IProcessRegistry _registry;
    private readonly IExecutorFactory _executors;
    private readonly IControlPlane _controlPlane;

    public async Task<ProcessResult> ExecuteAsync(string processId, ProcessParameters parameters, CancellationToken ct)
    {
        var definition = _registry.GetProcess(processId)
            ?? throw new ProcessExecutionException($"Process '{processId}' not registered.");

        var tierOrder = _controlPlane.GetTenantPolicyOverride(parameters.TenantId, processId).TierOrder
            ?? definition.PreferredTiers;

        foreach (var tier in tierOrder)
        {
            var executor = _executors.GetExecutor(tier);

            try
            {
                var context = new ProcessExecutionContext(parameters, tier);
                return await definition.ExecuteAsync(context, ct);
            }
            catch (CapacityRejectedException)
            {
                continue;
            }
        }

        throw new ProcessExecutionException($"No execution tier available for '{definition.Id}'.");
    }
}

public interface IExecutorFactory
{
    IGeoprocessingExecutor GetExecutor(ProcessExecutionTier tier);
}

### Process Expression Layer

- Catalog definitions compile into a neutral expression graph (`ProcessExpression`) representing inputs, parameter bindings, and operation pipelines.
- Executors translate the shared expression into tier-specific calls (NTS function invocation, PostGIS stored procedure arguments, Cloud Batch payload).
- Expression compiler enforces deterministic behavior and provides static validation (required parameters, type compatibility) before any executor runs.
- Shared expression unlocks future capabilities like process chaining and memoization without rewriting tier adapters.
```
```

### Process Result Contract

- **Outputs as structured values:** Executors return a JSON-serializable dictionary keyed by process output identifiers (`result`, `metadata`, etc.).
- **Artifact indirection:** Large responses (rasters, shapefiles) are streamed to the artifact store; API responses surface time-limited URIs that callers can dereference.
- **Tier provenance:** `ProcessResult.ExecutedTier` tracks which tier fulfilled the call to aid debugging and cost attribution.

```csharp
public record ProcessResult(
    IReadOnlyDictionary<string, JsonElement> Outputs,
    IReadOnlyList<ProcessArtifact> Artifacts,
    ProcessExecutionTier ExecutedTier);

public record ProcessArtifact(
    string Rel,
    string Href,
    string Type,
    long? SizeBytes,
    DateTimeOffset? ExpiresAt);
```

Artifacts are written through an `IArtifactStore` abstraction that supports inline storage (small payloads) and externalized storage (e.g., S3, Azure Blob) with server-side encryption and access logs.

```csharp
public class ProcessParameters
{
    public string TenantId { get; init; } = "default";
    public string? Schema { get; init; }
    public string? Table { get; init; }
    public string? GeometryField { get; init; }
    public string? IdField { get; init; }
    public string? Filter { get; init; }
    public JsonElement? Inputs { get; init; }
    public IDictionary<string, object?> Extras { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
```

`Extras` provides a flexible escape hatch for process-specific parameters while the first-class properties cover common spatial concerns (schema/table/geometry fields).

```csharp
public sealed class ProcessExecutionException : Exception
{
    public ProcessExecutionException(string message, Exception? inner = null)
        : base(message, inner) { }
}

public sealed class ProcessAdmissionException : Exception
{
    public string? Reason { get; }
    public TimeSpan? RetryAfter { get; }
    
    public ProcessAdmissionException(string? reason, TimeSpan? retryAfter)
        : base(reason ?? "Process admission denied")
    {
        Reason = reason;
        RetryAfter = retryAfter;
    }
}

public sealed class CapacityRejectedException : Exception
{
    public CapacityRejectedException(string message, Exception? inner = null)
        : base(message, inner) { }
}
```

### OGC API - Processes Handlers

```csharp
// src/Honua.Server.Host/Ogc/OgcProcessesHandlers.cs
public static class OgcProcessesHandlers
{
    /// <summary>
    /// GET /processes - List all available processes
    /// </summary>
    public static IResult GetProcessList(IProcessRegistry registry)
    {
        var processes = registry.GetProcesses();
        return Results.Json(new
        {
            processes = processes.Select(p => new
            {
                id = p.Id,
                title = p.Title,
                description = p.Description,
                version = p.Version,
                jobControlOptions = new[] { "sync-execute", "async-execute" },
                outputTransmission = new[] { "value", "reference" },
                links = new[]
                {
                    new { href = $"/processes/{p.Id}", rel = "self", type = "application/json" },
                    new { href = $"/processes/{p.Id}/execution", rel = "execute", type = "application/json" }
                }
            })
        });
    }

    /// <summary>
    /// GET /processes/{processId} - Get process details
    /// </summary>
    public static IResult GetProcessDescription(
        string processId,
        IProcessRegistry registry)
    {
        var process = registry.GetProcess(processId);
        if (process is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-process",
                title = "Process not found",
                detail = $"Process '{processId}' does not exist"
            });
        }

        return Results.Json(new
        {
            id = process.Id,
            title = process.Title,
            description = process.Description,
            version = process.Version,
            jobControlOptions = new[] { "sync-execute", "async-execute", "dismiss" },
            outputTransmission = new[] { "value", "reference" },
            inputs = process.Parameters.ToDictionary(
                p => p.Name,
                p => new
                {
                    title = p.Description,
                    schema = new { type = p.Type.ToString().ToLower() },
                    minOccurs = p.Required ? 1 : 0,
                    maxOccurs = 1
                }),
            outputs = new
            {
                result = new
                {
                    title = "Process result",
                    schema = new { type = "object" }
                }
            }
        });
    }

    /// <summary>
    /// POST /processes/{processId}/execution - Execute process
    /// </summary>
    public static async Task<IResult> ExecuteProcess(
        string processId,
        HttpRequest request,
        IProcessRegistry registry,
        IGeoprocessingJobManager jobManager,
        CancellationToken requestAborted)
    {
        var process = registry.GetProcess(processId);
        if (process is null)
        {
            return Results.NotFound();
        }

        var executionRequest = await JsonSerializer
            .DeserializeAsync<ProcessExecutionRequest>(request.Body);

        if (executionRequest is null)
        {
            return Results.BadRequest(new { error = "Invalid execution request" });
        }
        executionRequest.Principal = request.HttpContext.User;

        var prefer = request.Headers["Prefer"].ToString();
        var requestedMode = executionRequest.Execution?.Mode ?? string.Empty;
        var clientWantsAsync =
            prefer.Contains("respond-async", StringComparison.OrdinalIgnoreCase) ||
            requestedMode.Equals("async", StringComparison.OrdinalIgnoreCase);

        var synchronousCapable = process.PreferredTiers.Contains(ProcessExecutionTier.NetTopologySuite)
            || process.PreferredTiers.Contains(ProcessExecutionTier.PostGIS);

        if (!clientWantsAsync && !synchronousCapable)
        {
            clientWantsAsync = true;
        }

        if (!clientWantsAsync)
        {
            // Inline execution with timeout tied to target P95 + guard
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            timeoutCts.CancelAfter(process.Hints.TargetP95 + TimeSpan.FromSeconds(5));

            try
            {
                executionRequest.ProcessId = processId;
                var ticket = await jobManager.SubmitAsync(executionRequest, timeoutCts.Token);

                if (prefer.Contains("return=minimal", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NoContent();
                }

                return Results.Ok(new
                {
                    type = "process-result",
                    outputs = ticket.Result!.Outputs,
                    links = ticket.Result.Artifacts
                });
            }
            catch (CapacityRejectedException)
            {
                // Retry async when synchronous slot unavailable
                clientWantsAsync = true;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                clientWantsAsync = true; // fallback to async if sync timeout exceeded
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Process execution failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        // Defer to background execution (Control Plane handles admission/scheduling)
        executionRequest.ProcessId = processId;
        var ticket = await jobManager.SubmitAsync(executionRequest, requestAborted);

        var responsePayload = new
        {
            jobID = ticket.JobId,
            status = "accepted",
            message = "Process execution scheduled",
            links = new[]
            {
                new { href = $"/jobs/{ticket.JobId}", rel = "self", type = "application/json" },
                new { href = $"/jobs/{ticket.JobId}/results", rel = "results", type = "application/json" },
                new { href = $"/jobs/{ticket.JobId}", rel = "monitor", type = "application/json" }
            }
        };

        if (prefer.Contains("return=minimal", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Accepted($"/jobs/{ticket.JobId}", null);
        }

        return Results.Accepted($"/jobs/{ticket.JobId}", responsePayload);
    }

    /// <summary>
    /// GET /jobs/{jobId} - Get job status
    /// </summary>
    public static async Task<IResult> GetJobStatus(
        string jobId,
        IControlPlane controlPlane,
        CancellationToken requestAborted)
    {
        var status = await controlPlane.GetJobStatusAsync(jobId, requestAborted);
        if (status is null)
        {
            return Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-job",
                title = "Job not found",
                status = 404
            });
        }

        return Results.Json(new
        {
            jobID = jobId,
            status = status.Status.ToString().ToLowerInvariant(),
            progress = CalculateProgress(status),
            created = status.SubmittedAt,
            started = status.StartedAt,
            finished = status.CompletedAt,
            links = new[]
            {
                new { href = $"/jobs/{jobId}", rel = "self", type = "application/json" },
                new { href = $"/jobs/{jobId}/results", rel = "results", type = "application/json" },
                new { href = $"/jobs/{jobId}", rel = "cancel", type = "application/json" }
            }
        });
    }

    /// <summary>
    /// DELETE /jobs/{jobId} - Cancel a running job
    /// </summary>
    public static async Task<IResult> CancelJob(
        string jobId,
        IControlPlane controlPlane,
        CancellationToken requestAborted)
    {
        var cancelled = await controlPlane.CancelJobAsync(jobId, requestAborted);
        return cancelled
            ? Results.Ok(new { jobID = jobId, status = "dismissed" })
            : Results.NotFound(new
            {
                type = "http://www.opengis.net/def/exceptions/ogcapi-processes-1/1.0/no-such-job",
                title = "Job not found",
                status = 404
            });
    }
}
```

- **Policy service integration:** `IProcessingPolicyProvider` hydrates the control plane with tenant-specific policies, refreshing every 60 seconds and exposing strongly typed quotas to admission and scheduling flows.
- **Hot reconfiguration:** Because policies are data-driven, operators can adjust per-tenant budgets or tier pinning without redeploying API servers.

---

## Configuration

```csharp
// src/Honua.Server.Core/Configuration/ProcessingConfiguration.cs
public record ProcessingConfiguration
{
    public Uri ControlPlane { get; init; } = new("https://control-plane.honua.svc");
    public Uri PolicyService { get; init; } = new("https://policy.honua.svc");
    public ArtifactStoreSettings ArtifactStore { get; init; } = new();
    public CloudBatchSettings CloudBatch { get; init; } = new();
}

public record ArtifactStoreSettings
{
    public string Provider { get; init; } = "s3";
    public string Bucket { get; init; } = "honua-gp-artifacts";
    public TimeSpan DefaultExpiry { get; init; } = TimeSpan.FromHours(6);
    public bool RequireServerSideEncryption { get; init; } = true;
}

public record CloudBatchSettings
{
    public string Provider { get; init; } = "aws-batch"; // aws-batch | azure-batch | gcp-batch
    public string Queue { get; init; } = "gp-high";
    public string JobDefinition { get; init; } = "honua-gp-default:1";
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromHours(1);
}
```

**appsettings.json:**
```json
{
  "Processing": {
    "ControlPlane": "https://control-plane.honua.svc",
    "PolicyService": "https://policy.honua.svc",
    "ArtifactStore": {
      "Provider": "s3",
      "Bucket": "honua-gp-artifacts",
      "DefaultExpiry": "06:00:00"
    },
    "CloudBatch": {
      "Provider": "aws-batch",
      "Queue": "gp-high",
      "JobDefinition": "honua-gp-default:1",
      "DefaultTimeout": "01:00:00"
    }
  }
}
```

---

## Implementation Phases

**Revised Total Estimate:** 14-18 weeks (was 12-15 weeks)

**Key Changes:**
- Added Phase 1.5 for Hangfire worker implementation (+1-2 weeks)
- Extended Phase 2 for data locality strategy (+1 week)
- Extended Phase 3 for artifact staging implementation (+1 week)

---

### Phase 1: Foundation (2-3 weeks)
**Goal:** OGC API - Processes framework with synchronous Tier 1 (NTS) operations

**Tasks:**
- [ ] Implement `ProcessDefinition`, `ProcessRegistry`, `ProcessParameter` types (existing types from `PostgresControlPlane`)
- [ ] Implement `OgcProcessesHandlers` (GET /processes, GET /processes/{id}, POST /processes/{id}/execution)
- [ ] Implement job status endpoints (GET /jobs/{id})
- [ ] Wrap existing NTS operations as OGC Processes (leverage existing `GeometryOperationExecutor`)
- [ ] Implement inline/synchronous execution path via `ControlPlane.ExecuteInlineAsync`
- [ ] Write integration tests for synchronous process execution
- [ ] Document process definition schema (YAML format)

**Deliverables:**
- OGC API - Processes compliant endpoints
- 10-15 NTS-based processes (buffer, simplify, union, etc.) running synchronously
- Synchronous execution for fast operations (<100ms)
- Process registry with auto-discovery

**Note:** This phase does NOT include async/queue functionality. All processes execute inline.

---

### Phase 1.5: Hangfire Queue Implementation (1-2 weeks) 🆕
**Goal:** Add asynchronous job execution via Hangfire for Tier 1 & 2

**Why Separate Phase:**
The existing `PostgresControlPlane` provides basic job tracking (insert to `process_runs` table), but lacks worker polling and job execution logic. Hangfire adds:
- Job queue with automatic retry and DLQ
- Background worker management
- Admin dashboard for monitoring
- Distributed job coordination across K8s replicas

**Tasks:**
- [ ] Install `Hangfire.PostgreSql` NuGet package
- [ ] Configure Hangfire storage with dedicated schema (`honua_gp_jobs`)
- [ ] Implement `HangfireJobExecutor` that wraps `ITierExecutor`
- [ ] Update `ControlPlane.EnqueueAsync` to call `BackgroundJob.Enqueue`
- [ ] Configure Hangfire server options (worker count, queue names)
- [ ] Add Hangfire dashboard at `/hangfire` with authentication
- [ ] Implement job progress updates via Hangfire state transitions
- [ ] Write tests for async job submission → execution → completion
- [ ] Document Hangfire operational procedures (monitoring, retry policies)

**Implementation Pattern:**
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

// Hangfire worker executes the job
public class ProcessExecutor : IProcessExecutor
{
    public async Task ExecuteAsync(string jobId, CancellationToken ct)
    {
        var run = await _controlPlane.GetJobStatusAsync(jobId, ct);
        var process = await _registry.GetProcessAsync(run.ProcessId, ct);

        var result = await _tierExecutor.ExecuteAsync(run, process, run.Tier, ct);

        await _controlPlane.RecordCompletionAsync(jobId, result, run.Tier, duration, ct);
    }
}
```

**Deliverables:**
- Hangfire integrated with PostgreSQL storage
- Async job execution for Tier 1 & 2 operations
- Hangfire dashboard for ops monitoring
- Job retry policies and failure handling
- Documentation: "Hangfire Operations Guide"

---

### Phase 2: PostGIS Integration (3-4 weeks, was 2-3 weeks)
**Goal:** Add Tier 2 (PostGIS) operations with data locality strategy

**Tasks:**
- [ ] Implement `PostGisProcessingExecutor`
- [ ] **Implement data source compatibility checks in `ITierExecutor.SelectTierAsync`** 🆕
- [ ] **Add validation: reject PostGIS tier if data source is not PostgreSQL** 🆕
- [ ] Add Voronoi diagram process (`ST_VoronoiPolygons`)
- [ ] Add DBSCAN clustering (`ST_ClusterDBSCAN`)
- [ ] Add K-means clustering (`ST_ClusterKMeans`)
- [ ] Add heatmap generation (`ST_HexagonGrid` + aggregation)
- [ ] Add PL/pgSQL IDW interpolation function
- [ ] Implement `IPostGisProcedureRegistry` with SQL injection protection
- [ ] Configuration for allowed PostGIS schemas/procedures
- [ ] Write unit tests for PostGIS executor
- [ ] **Write tests for data locality fallback (PostGIS → NTS downgrade)** 🆕
- [ ] **Document process definition `requiresPostgres` hint** 🆕

**Deliverables:**
- 5-10 PostGIS-based processes with fallback to NTS
- PL/pgSQL functions for complex operations
- Data locality validation and clear error messages
- Performance benchmarks vs NTS
- Documentation: "PostGIS Process Development Guide"

**Extra Week Justification:** Data locality strategy requires metadata lookups, tier selection logic updates, and comprehensive testing of fallback scenarios.

---

### Phase 3: Python Integration (4-5 weeks, was 3-4 weeks)
**Goal:** Add Tier 3 (cloud batch) operations with secure artifact staging

**Tasks:**
- [ ] Provision cloud batch queue + job definition (AWS Batch / Azure Batch / GCP Batch)
- [ ] **Implement `IArtifactStore.StageInputAsync` and `StageOutputAsync`** 🆕
- [ ] **Configure blob storage with server-side encryption and SAS token generation** 🆕
- [ ] Implement `CloudBatchPythonExecutor` with artifact staging (not env vars)
- [ ] Integrate distributed tracing (TRACEPARENT propagation)
- [ ] Build signed container images for 5 starter processes:
  - [ ] IDW interpolation (scipy)
  - [ ] Kriging interpolation (pykrige)
  - [ ] DBSCAN clustering (scikit-learn)
  - [ ] Hot spot analysis (PySAL)
  - [ ] NDVI raster calculation (rasterio)
- [ ] Configure event-driven completion (EventBridge→SQS / Event Grid→Service Bus)
- [ ] Implement `BatchCompletionService` BackgroundService
- [ ] Author integration tests with mocked batch client
- [ ] Document container authoring guidelines and security requirements
- [ ] **Document artifact staging security model (encryption, access control)** 🆕

**Deliverables:**
- Cloud batch executor with secure artifact staging
- Signed container images for 5 Python processes
- Event-driven completion with zero polling
- Control plane integration tests (admission → batch → completion)
- Documentation: "Cloud Batch Process Development Guide"
- Documentation: "Artifact Store Security Model"

**Extra Week Justification:** Artifact staging infrastructure (blob storage, SAS tokens, encryption, cleanup policies) requires significant setup and testing beyond basic batch integration.

### Phase 4: Separate Worker (Optional, 2-3 weeks)
**Goal:** Extract processing to separate deployable

**Tasks:**
- [ ] Create `Honua.GeoProcessing.Worker` project
- [ ] Implement worker-only mode configuration
- [ ] Update Kubernetes manifests for split deployment
- [ ] Implement horizontal pod autoscaling (HPA)
- [ ] Add metrics for queue depth and worker utilization
- [ ] Load testing and capacity planning
- [ ] Update deployment documentation

**Deliverables:**
- Separate worker deployment
- Kubernetes HPA configuration
- Performance benchmarks
- Migration guide

---

## Security Considerations

### PostGIS SQL Injection Protection
```csharp
public sealed class PostGisProcedureRegistry : IPostGisProcedureRegistry
{
    private readonly IReadOnlyDictionary<string, PostGisProcedure> _procedures;
    private readonly IReadOnlySet<string> _allowedSchemas;

    public PostGisProcedureRegistry(IEnumerable<PostGisProcedure> procedures, IEnumerable<string> allowedSchemas)
    {
        _procedures = procedures.ToDictionary(p => p.ProcessId, StringComparer.OrdinalIgnoreCase);
        _allowedSchemas = allowedSchemas.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public PostGisProcedure Resolve(string processId)
    {
        if (!_procedures.TryGetValue(processId, out var procedure))
        {
            throw new SecurityException($"Process '{processId}' not registered for PostGIS execution.");
        }

        if (!_allowedSchemas.Contains(procedure.Schema))
        {
            throw new SecurityException($"Schema '{procedure.Schema}' is not allow-listed.");
        }

        return procedure;
    }
}

public sealed record PostGisProcedure(
    string ProcessId,
    string Schema,
    string SchemaQualifiedName,
    IReadOnlyList<PostGisParameter> Parameters,
    Func<NpgsqlCommand, ProcessParameters, CancellationToken, ValueTask> BindParametersAsync,
    Func<NpgsqlDataReader, IArtifactStore, CancellationToken, ValueTask<ProcedureResultPayload>> MaterializeOutputsAsync);

public sealed record PostGisParameter(string Name, NpgsqlDbType Type, bool Required, object? DefaultValue = null);

public sealed record ProcedureResultPayload(
    IReadOnlyDictionary<string, JsonElement> Outputs,
    IReadOnlyList<PendingArtifact> Artifacts);

public sealed record PendingArtifact(
    string Rel,
    Stream Content,
    string ContentType,
    long? Length);
```

- Stored procedures run as a dedicated database role with least privilege (read-only unless explicitly required).
- Inputs are bound via Npgsql parameters; free-form expressions (filters) are satisfied by views defined ahead of time rather than injecting SQL fragments.
- Result payloads stream through `MaterializeOutputsAsync`, which enforces output row counts/size limits before artifacts leave the database.
- `IArtifactStore.StageAsync` transforms `PendingArtifact` streams into durable `ProcessArtifact` descriptors with signed URLs and retention metadata.

### Python Script Security
```csharp
public sealed class PythonSandboxPolicy : IPythonSandboxPolicy
{
    private readonly IManifestSigner _signer;
    private readonly ImmutableDictionary<string, PythonSandboxManifest> _manifests;

    public PythonSandboxManifest Require(string scriptName)
    {
        if (!_manifests.TryGetValue(scriptName, out var manifest))
        {
            throw new SecurityException($"Script '{scriptName}' is not approved.");
        }

        if (!_signer.Verify(manifest))
        {
            throw new SecurityException($"Python manifest signature invalid for '{scriptName}'.");
        }

        return manifest;
    }
}

public sealed record PythonSandboxManifest(
    string ScriptName,
    string ContainerImage,
    string Sha256,
    IReadOnlyList<string> RequiredEnvironmentVariables,
    ResourceQuota ResourceQuota);
```

### Resource Limits
```csharp
public record ResourceLimits
{
    public int MaxInputFeatures { get; init; } = 10_000;
    public int MaxOutputFeatures { get; init; } = 100_000;
    public long MaxInputSizeBytes { get; init; } = 100 * 1024 * 1024; // 100MB
    public long MaxArtifactSizeBytes { get; init; } = 5L * 1024 * 1024 * 1024;
    public TimeSpan MaxInlineDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromMinutes(30);
    public int MaxConcurrentJobs { get; init; } = 10;
    public double MaxEstimatedCostUnits { get; init; } = 100;
}
```

---

## Monitoring and Observability

### OpenTelemetry Metrics

```csharp
public static class GeoprocessingMetrics
{
    private static readonly Meter Meter = new("Honua.Geoprocessing");
    public static readonly ActivitySource ActivitySource = new("Honua.Geoprocessing");

    public static readonly Counter<long> ProcessExecutions = Meter.CreateCounter<long>(
        "honua.geoprocessing.executions",
        unit: "1",
        description: "Total process executions by tier");

    public static readonly Histogram<double> ProcessDuration = Meter.CreateHistogram<double>(
        "honua.geoprocessing.duration",
        unit: "seconds",
        description: "Process execution duration by tier and process");

    public static readonly Counter<long> ProcessFailures = Meter.CreateCounter<long>(
        "honua.geoprocessing.failures",
        unit: "1",
        description: "Process failures tagged by tier and exception type");

    public static readonly UpDownCounter<long> CapacityAvailable = Meter.CreateUpDownCounter<long>(
        "honua.geoprocessing.capacity.available",
        unit: "slots",
        description: "Available capacity per resource class for autoscaling hints");

    public static readonly ObservableGauge<int> ActiveJobs = Meter.CreateObservableGauge<int>(
        "honua.geoprocessing.active_jobs",
        () => GetActiveJobCount(),
        description: "Number of currently active jobs by state");

    public static readonly ObservableGauge<int> QueueDepth = Meter.CreateObservableGauge<int>(
        "honua.geoprocessing.queue_depth",
        () => GetQueueDepth(),
        description: "Jobs queued per resource class");

    public static Activity? StartExecutionActivity(string processId, ProcessExecutionTier tier)
    {
        return ActivitySource.StartActivity("honua.process.execute", ActivityKind.Internal, Activity.Current?.Context ?? default, tags: new ActivityTagsCollection
        {
            { "honua.process.id", processId },
            { "honua.process.tier", tier.ToString() }
        });
    }
}
```

- **Trace propagation:** `IGeoprocessingJobScheduler` stamps `traceparent`/`tracestate` onto queued jobs; workers resume the activity via `ActivitySource` before calling executors. Python sandboxes receive the same headers through environment variables so spans stay contiguous in OpenTelemetry exporters.

### Grafana Dashboard Queries

```promql
# Process execution rate
rate(honua_geoprocessing_executions_total[5m])

# P95 execution duration by tier
histogram_quantile(0.95,
  rate(honua_geoprocessing_duration_bucket[5m])
) by (tier)

# Error rate
rate(honua_geoprocessing_failures_total[5m]) /
rate(honua_geoprocessing_executions_total[5m])

# Active jobs by status
honua_geoprocessing_active_jobs by (status)

# Queue depth
honua_geoprocessing_queue_depth

# Capacity headroom
honua_geoprocessing_capacity_available

# Success ratio by tier
1 - (
  sum(rate(honua_geoprocessing_failures_total[5m])) by (tier) /
  clamp_min(sum(rate(honua_geoprocessing_executions_total[5m])) by (tier), 1)
)
```

---

## Provenance & Audit Trail

- **ProcessRun ledger:** Every execution produces a `ProcessRun` document capturing input artifact URIs + hashes, execution tier, policy version, cost units consumed, and the identity that triggered the job. Records append-only in PostgreSQL (partitioned by month) and replicated to the analytics warehouse.
- **Dataset snapshots:** When inputs reference Honua datasets, the orchestrator resolves immutable snapshot IDs and records them alongside run metadata so downstream consumers can replay with identical data.
- **Log retention:** Python stderr/stdout streams and PostGIS notices are uploaded to the artifact store with configurable retention (default 7 days, extendable per tenant) for post-mortem analysis.
- **Budget alerting:** Aggregated cost telemetry feeds the billing pipeline; when tenants approach their budget the policy service flips processes into async-only mode and queues an operator review.

---

## Testing Strategy

### Unit Tests
```csharp
public class PostGisExecutorTests
{
    [Fact]
    public async Task VoronoiDiagram_ValidInput_ReturnsPolygons()
    {
        // Arrange
        var executor = new PostGisProcessingExecutor(_connections, _procedureRegistry, _artifactStore, NullLogger<PostGisProcessingExecutor>.Instance);
        var parameters = new ProcessParameters
        {
            Schema = "public",
            Table = "test_points",
            GeometryField = "geometry"
        };

        // Act
        var result = await executor.VoronoiAsync(parameters, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ProcessExecutionTier.PostGIS, result.ExecutedTier);
        Assert.True(result.Outputs.TryGetValue("collection", out var output));
        var featureCollection = Assert.IsType<JsonElement>(output);
        Assert.True(featureCollection.GetProperty("features").GetArrayLength() > 0);
    }
}
```

### Integration Tests
```csharp
public class OgcProcessesIntegrationTests
{
    [Fact]
    public async Task ExecuteProcess_BufferOperation_ReturnsBufferedGeometries()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            inputs = new
            {
                geometries = new
                {
                    type = "FeatureCollection",
                    features = new[] { /* GeoJSON features */ }
                },
                distance = 100,
                unit = "meters"
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/processes/buffer/execution", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProcessResult>();
        Assert.NotNull(result);
        Assert.True(result.Outputs.ContainsKey("buffered"));
        Assert.Equal(ProcessExecutionTier.NetTopologySuite, result.ExecutedTier);
    }
}
```

### Resiliency (Chaos) Tests
- **Worker fault injection:** Intentionally kill Python sandboxes, revoke S3 credentials, or pause PostGIS connections mid-execution and assert jobs transition to `failed` with actionable diagnostics.
- **Queue backpressure:** Saturate the governor with synthetic load and verify `429` responses include `Retry-After` headers and that Hangfire ingestion stays below configured depth.
- **Timeout enforcement:** Force processes to exceed tenant CPU budgets and assert cancellations propagate through all tiers, releasing reservations and cgroup slots.

---

## Performance Baselines

### Target Latencies

| Operation Type | Target P50 | Target P95 | Target P99 |
|----------------|-----------|-----------|-----------|
| Tier 1 (NTS) - Simple | < 50ms | < 100ms | < 200ms |
| Tier 1 (NTS) - Complex | < 500ms | < 1s | < 2s |
| Tier 2 (PostGIS) | < 1s | < 5s | < 10s |
| Tier 3 (Python) | < 10s | < 60s | < 300s |

### Throughput Targets

| Deployment Mode | Concurrent Jobs | Jobs/Hour |
|-----------------|----------------|-----------|
| Embedded (4 workers) | 10 | 100 |
| Separate Worker (8 workers) | 50 | 500 |
| Multi-Worker (24 workers) | 200 | 2000 |

---

## Migration Path from Current State

### Step 1: Add OGC Processes API (Non-Breaking)
- Implement new `/processes` endpoints
- Keep existing `/rest/services/Geometry/GeometryServer` (Esri compatibility)
- Both endpoints call same underlying `GeometryOperationExecutor`

### Step 2: Add Hangfire (Optional for Simple Deployments)
- Add Hangfire for async operations
- Synchronous operations continue to work via Task.Run
- Configuration flag to enable/disable Hangfire

### Step 3: Add PostGIS Operations (Leverages Existing Infrastructure)
- No new dependencies required
- Add PostGIS executor that uses existing `IDataStoreProvider`
- Register new processes via `ProcessRegistry`

### Step 4: Add Python Support (Opt-In)
- Python executor disabled by default
- Requires Python runtime in deployment environment
- Enable via configuration flag

### Step 5: Extract Workers (Scale Phase Only)
- Create new worker project
- Update Kubernetes manifests
- Zero code changes required (configuration-driven)

---

## Open Questions

1. **GPU Acceleration:** Should we support GPU-accelerated operations (RAPIDS, cuSpatial)?
   - **Decision:** Phase 4+ - deploy workers on GPU-enabled K8s nodes

2. **Distributed Tracing:** Should process executions create child spans?
   - **Decision:** Yes - use OpenTelemetry ActivitySource

3. **Cost Limits:** Should we implement cost estimation and budgets?
   - **Decision:** Phase 2+ - add cost estimation based on operation complexity

4. **Versioning:** How do we handle process definition versioning?
   - **Decision:** Include version in process ID (e.g., `buffer-v1`, `buffer-v2`)

5. **Output Storage:** Should large results be stored (S3/blob) vs returned inline?
   - **Decision:** Phase 2+ - support both inline and reference output

---

## Architecture Summary

### Final Decisions

**API Surface (2 surfaces, shared backend):**
1. ✅ **GeoservicesREST GPServer** (Priority 1) - Esri compatible, JSON, wide adoption
2. ✅ **OGC API - Processes 1.0** (Priority 2) - Standards compliant

**Multi-Tier Execution:**
- ✅ **Tier 1**: NetTopologySuite (NTS) - Pure .NET, in-process, < 100ms
- ✅ **Tier 2**: PostGIS stored procedures - Server-side, 1-10s, SQL injection protection, **PostgreSQL data sources only**
- ✅ **Tier 3**: Cloud batch services - AWS/Azure/GCP Batch, 10s-30min, GPU support

**Tier Selection (MVP):**
- ✅ **Deterministic routing** based on data source compatibility and process preferences
- ✅ **Data locality validation**: PostGIS tier automatically skipped if data not in PostgreSQL
- ✅ **Graceful fallback**: PostGIS → NTS downgrade with logging
- 🔮 **Future (Phase 2+)**: Adaptive heuristics based on telemetry (optional enhancement)

**Job Management:**
- ✅ **Tier 1 & 2 (Single-Instance)**: ASP.NET Core BackgroundService (in-memory queue)
- ✅ **Tier 1 & 2 (Clustered/K8s)**: Hangfire (shared PostgreSQL queue) ← **Production recommendation**
- ✅ **Tier 3**: Cloud batch services with event-driven completion (independent of Hangfire)

**Event-Driven Completion (Zero Polling):**
- ✅ **AWS**: EventBridge → SQS → BackgroundService consumes
- ✅ **Azure**: Event Grid → Service Bus → BackgroundService consumes
- ✅ **GCP**: Cloud Logging → Pub/Sub → BackgroundService consumes

**Security:**
- ✅ **Honua stays private** - no public IP required (pulls from queue, outbound only)
- ✅ **VPC Endpoints** (AWS), Private Link (Azure), Private Service Connect (GCP)
- ✅ **PostGIS SQL injection protection** via stored procedures with parameter binding
- ✅ **Tier 3 artifact staging** - no sensitive data in environment variables (encrypted blob storage with SAS tokens)
- ✅ **Tenant isolation** - all queries include tenant_id filter

**Deployment Recommendation:**

| Deployment | Tier 1 & 2 | Tier 3 | When to Use |
|------------|------------|--------|-------------|
| **Phase 1: Embedded** | Inline synchronous execution | None | Development, single instance, < 10 concurrent processes |
| **Phase 1.5: Async** | Hangfire (shared queue) | None | Single instance with async needs, < 100 jobs/day |
| **Phase 2: PostGIS** | Hangfire (shared queue) | None | Production K8s, PostgreSQL data sources, < 1000 jobs/day |
| **Phase 3: Cloud-Native** ✅ | Hangfire (shared queue) | AWS/Azure/GCP Batch | **Production K8s - full scale, all data sources** |

**Clarification:**
- **Development**: Use inline synchronous execution (Phase 1) - simplest setup
- **Single-instance with async**: Use Hangfire (Phase 1.5) - adds job queue without K8s complexity
- **Clustered Honua (K8s)**: Use Hangfire (shared PostgreSQL queue) for Tier 1 & 2 ← **Most production deployments**
- **Tier 3 (all deployments)**: Use cloud batch services (AWS/Azure/GCP Batch) for Python/GPU workloads

**Cost Optimization (Phase 3 Cloud Batch with Spot VMs):**

| Provider | Monthly Cost (1000 jobs/day @ 5 min) | Savings vs Always-On VMs |
|----------|--------------------------------------|--------------------------|
| **AWS** | ~$29/month | 92% |
| **Azure** | ~$22/month | 92% |
| **GCP** | ~$3/month | 99% |

**Key Improvements Over Esri/GeoServer:**
- ✅ Support 2 API surfaces with single implementation (GeoservicesREST + OGC)
- ✅ Cloud-native Tier 3 (not VMs/local processes)
- ✅ Event-driven completion (not polling)
- ✅ Private deployment (no public IP)
- ✅ Multi-tier deterministic fallback with data locality awareness
- ✅ PostGIS SQL injection protection via stored procedures
- ✅ Secure artifact staging (no sensitive data in environment variables)

**Estimated Implementation Effort:** 14-18 weeks across all phases (updated from 12-15 weeks)

**Breakdown:**
- Phase 1 (Foundation): 2-3 weeks
- Phase 1.5 (Hangfire Queue): 1-2 weeks 🆕
- Phase 2 (PostGIS + Data Locality): 3-4 weeks (+1 week)
- Phase 3 (Cloud Batch + Artifact Staging): 4-5 weeks (+1 week)
- Phase 4 (Separate Worker): 2-3 weeks (optional)

**Total: 14-18 weeks** (10-14 weeks required phases + 2-3 weeks optional Phase 4)

---

## References

- [OGC API - Processes Specification](https://docs.ogc.org/is/18-062r2/18-062r2.html)
- [GeoservicesREST Specification (Open Web Foundation)](https://www.openwebfoundation.org/)
- [AWS Batch Documentation](https://docs.aws.amazon.com/batch/)
- [Azure Batch Documentation](https://docs.microsoft.com/en-us/azure/batch/)
- [Google Cloud Batch Documentation](https://cloud.google.com/batch/docs)
- [PostGIS Reference](https://postgis.net/docs/reference.html)
- [NetTopologySuite Documentation](https://nettopologysuite.github.io/)
- [GeoPandas Documentation](https://geopandas.org/)
- [Archived Comparative Analysis](../archive/GEOPROCESSING_COMPARATIVE_ANALYSIS.md)
