# Geoprocessing Module

Enterprise-tier feature for asynchronous OGC-compliant geoprocessing operations.

## Overview

The Geoprocessing module provides a scalable, cloud-native geoprocessing engine that implements the OGC API - Processes standard. It supports a wide range of vector and raster operations with automatic execution tier selection, job queuing, and result caching.

**Key Capabilities:**
- OGC API - Processes Part 1: Core standard compliance
- 30+ geoprocessing operations (buffer, intersection, union, etc.)
- Multi-tier execution architecture (NTS, PostGIS, Cloud Batch)
- Asynchronous job execution with progress tracking
- Result caching and webhook notifications
- Tenant isolation and quota enforcement
- Priority queuing for premium tiers

## Architecture

### Multi-Tier Execution Model

```
┌─────────────────────────────────────────────────────────────┐
│                   Client Application                         │
│  POST /processes/{processId}/execution                      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│                  Control Plane                               │
│  • Admission Control (quotas, resource limits)              │
│  • Tier Selection (Auto, NTS, PostGIS, CloudBatch)          │
│  • Job Queuing (priority-based)                             │
└──────┬──────────────────────┬───────────────────────────────┘
       │                      │
       v                      v
┌──────────────┐      ┌──────────────────┐
│ Sync Mode    │      │ Async Mode       │
│ (< 5s jobs)  │      │ (> 5s jobs)      │
│ Return result│      │ Queue job        │
│ immediately  │      │ Return 202 + URL │
└──────────────┘      └─────────┬────────┘
                                │
                                v
┌─────────────────────────────────────────────────────────────┐
│              Tier Executor Coordinator                       │
│  Routes job to appropriate executor based on:               │
│  • Job size (feature count, geometry complexity)            │
│  • Operation type (NTS-supported vs. PostGIS-only)          │
│  • Tenant tier (Core → NTS, Pro → PostGIS, Enterprise → Cloud) │
└──────┬────────────────┬─────────────────┬───────────────────┘
       │                │                 │
       v                v                 v
┌──────────────┐ ┌──────────────┐ ┌──────────────────┐
│ NtsExecutor  │ │PostGisExecutor│ │CloudBatchExecutor│
│              │ │               │ │                  │
│ In-process   │ │ PostgreSQL    │ │ Azure Batch      │
│ NetTopology  │ │ PostGIS       │ │ GDAL/OGR         │
│ Suite        │ │ ST_* functions│ │ containers       │
│              │ │               │ │                  │
│ < 100 MB     │ │ < 1 GB        │ │ > 1 GB           │
│ < 1000 feat  │ │ < 100k feat   │ │ Unlimited        │
└──────────────┘ └──────────────┘ └──────────────────┘
```

### Job Lifecycle

```
1. Submit Process Execution
   POST /processes/buffer/execution
   ↓
2. Admission Control
   • Check quotas (builds/month, concurrent jobs)
   • Validate parameters
   • Estimate resource usage
   ↓
3. Mode Selection
   • Sync: Job completes < 5s → Return result immediately
   • Async: Job takes > 5s → Queue and return 202 Accepted
   ↓
4. Job Enqueued
   • Stored in PostgreSQL job queue
   • Status: Pending
   • Priority assigned based on tenant tier
   ↓
5. Worker Picks Up Job
   • GeoprocessingWorkerService polls queue
   • Status: Running
   • Progress updates sent to webhook
   ↓
6. Execution
   • TierExecutorCoordinator selects executor
   • Operation executes (buffer, union, etc.)
   • Result cached in blob storage or database
   ↓
7. Completion
   • Status: Completed / Failed
   • Result available at /processes/jobs/{jobId}/results
   • Webhook notification sent
   • Cache entry created (TTL: 24 hours)
```

## Supported Operations

### Vector Operations

#### Geometric Operations

**Buffer**
- Creates polygon around geometries at specified distance
- Parameters: `distance` (meters), `segments` (quad segs), `dissolve` (boolean)
- Example: 100m buffer around roads

**Intersection**
- Returns geometric intersection of two datasets
- Parameters: `input_a`, `input_b`
- Example: Find areas where floodplains intersect parcels

**Union**
- Merges overlapping geometries into single geometry
- Parameters: `inputs[]`, `dissolve_attributes` (boolean)
- Example: Combine multiple polygons into unified boundary

**Difference**
- Subtracts geometry B from geometry A
- Parameters: `input_a`, `input_b`
- Example: Remove protected areas from development zones

**Dissolve**
- Merges adjacent geometries based on attribute
- Parameters: `dissolve_field`, `aggregations[]`
- Example: Dissolve counties into states

**Clip**
- Cuts geometries to boundary extent
- Parameters: `input`, `clip_boundary`
- Example: Clip buildings to city limits

**Simplify**
- Reduces vertex count while preserving shape
- Parameters: `tolerance` (meters), `preserve_topology` (boolean)
- Example: Simplify coastline for web mapping

**Convex Hull**
- Creates smallest convex polygon containing all points
- Parameters: `input`
- Example: Find bounding polygon of scattered points

#### Spatial Analysis

**Spatial Join**
- Joins attributes based on spatial relationship
- Parameters: `target`, `join`, `relationship` (intersects, within, contains)
- Example: Join census data to parcels

**Centroid**
- Calculates geometric center of features
- Parameters: `input`
- Example: Find center point of polygons

**Area / Length**
- Calculates geometric measurements
- Parameters: `input`, `units` (sq_meters, sq_km, hectares)
- Example: Calculate parcel areas

**Distance**
- Measures distance between geometries
- Parameters: `input_a`, `input_b`, `units`
- Example: Find distance from points to nearest road

**Nearest Feature**
- Finds closest feature in target dataset
- Parameters: `source`, `target`, `max_distance`
- Example: Find nearest hospital to accident location

### Raster Operations (Phase 2)

**Mosaic**
- Combines multiple rasters into single raster
- Parameters: `inputs[]`, `blend_method`

**Hillshade**
- Creates 3D visualization from elevation data
- Parameters: `dem`, `azimuth`, `altitude`

**Slope / Aspect**
- Calculates terrain derivatives
- Parameters: `dem`, `units` (degrees, percent)

**Zonal Statistics**
- Summarizes raster values within polygons
- Parameters: `raster`, `zones`, `statistics[]`

## API Endpoints

### List Available Processes

**Endpoint:** `GET /processes`

**Response:**
```json
{
  "processes": [
    {
      "id": "buffer",
      "version": "1.0.0",
      "title": "Buffer",
      "description": "Creates a buffer (polygon) around input geometries at a specified distance",
      "keywords": ["buffer", "proximity", "geometric"],
      "links": [
        {
          "href": "https://api.honua.io/processes/buffer",
          "rel": "self",
          "type": "application/json"
        },
        {
          "href": "https://api.honua.io/processes/buffer/execution",
          "rel": "execute",
          "type": "application/json"
        }
      ]
    }
  ]
}
```

### Get Process Description

**Endpoint:** `GET /processes/{processId}`

**Example:** `GET /processes/buffer`

**Response:**
```json
{
  "id": "buffer",
  "version": "1.0.0",
  "title": "Buffer",
  "description": "Creates a buffer (polygon) around input geometries at a specified distance",
  "inputs": {
    "distance": {
      "title": "Buffer distance",
      "description": "Distance in meters",
      "schema": {
        "type": "number",
        "format": "double",
        "minValue": 0.1,
        "maxValue": 100000,
        "required": true
      }
    },
    "segments": {
      "title": "Quadrant segments",
      "description": "Number of segments per quadrant (higher = smoother)",
      "schema": {
        "type": "integer",
        "default": 8,
        "minValue": 1,
        "maxValue": 32
      }
    },
    "dissolve": {
      "title": "Dissolve overlapping buffers",
      "schema": {
        "type": "boolean",
        "default": false
      }
    }
  },
  "outputs": {
    "result": {
      "title": "Process result",
      "description": "Buffered geometries",
      "schema": {
        "type": "object",
        "contentMediaType": "application/geo+json"
      }
    }
  }
}
```

### Execute Process (Synchronous)

**Endpoint:** `POST /processes/{processId}/execution`

**Request:**
```json
{
  "inputs": {
    "distance": 100,
    "segments": 8,
    "dissolve": false,
    "input": {
      "type": "geojson",
      "data": {
        "type": "FeatureCollection",
        "features": [
          {
            "type": "Feature",
            "geometry": {
              "type": "Point",
              "coordinates": [-122.4194, 37.7749]
            }
          }
        ]
      }
    }
  },
  "response": "raw"
}
```

**Response (Sync - Completes < 5s):**
```json
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "successful",
  "result": {
    "geojson": {
      "type": "FeatureCollection",
      "features": [/* buffered geometries */]
    },
    "count": 1,
    "dissolved": false
  }
}
```

### Execute Process (Asynchronous)

**Request:**
```json
{
  "inputs": {
    "distance": 100,
    "input": {
      "type": "collection",
      "source": "roads-layer",
      "filter": "highway = 'primary'"
    }
  },
  "response": "document"
}
```

**Response (Async - Takes > 5s):**
```
HTTP/1.1 202 Accepted
Location: /processes/jobs/550e8400-e29b-41d4-a716-446655440000
```
```json
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000",
  "process_id": "buffer",
  "status": "accepted",
  "created": "2025-11-05T10:30:00Z",
  "links": [
    {
      "href": "/processes/jobs/550e8400-e29b-41d4-a716-446655440000",
      "rel": "status",
      "type": "application/json"
    },
    {
      "href": "/processes/jobs/550e8400-e29b-41d4-a716-446655440000/results",
      "rel": "results",
      "type": "application/json"
    }
  ]
}
```

### Get Job Status

**Endpoint:** `GET /processes/jobs/{jobId}`

**Response:**
```json
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000",
  "process_id": "buffer",
  "status": "running",
  "created": "2025-11-05T10:30:00Z",
  "started": "2025-11-05T10:30:02Z",
  "progress": 45,
  "message": "Buffered 450/1000 features",
  "links": [
    {
      "href": "/processes/jobs/550e8400-e29b-41d4-a716-446655440000",
      "rel": "self",
      "type": "application/json"
    }
  ]
}
```

**Status Values:**
- `accepted` - Job queued, waiting for worker
- `running` - Job in progress
- `successful` - Job completed successfully
- `failed` - Job failed with error
- `dismissed` - Job cancelled by user

### Get Job Results

**Endpoint:** `GET /processes/jobs/{jobId}/results`

**Response:**
```json
{
  "geojson": {
    "type": "FeatureCollection",
    "features": [/* buffered geometries */]
  },
  "count": 1000,
  "dissolved": false,
  "processing_time_ms": 15420
}
```

### List Jobs

**Endpoint:** `GET /processes/jobs`

**Query Parameters:**
- `process_id` (string): Filter by process type
- `status` (string): Filter by status (accepted, running, successful, failed)
- `limit` (int): Results per page (default: 100, max: 1000)
- `offset` (int): Pagination offset

**Response:**
```json
{
  "jobs": [
    {
      "job_id": "550e8400-e29b-41d4-a716-446655440000",
      "process_id": "buffer",
      "status": "successful",
      "created": "2025-11-05T10:30:00Z",
      "started": "2025-11-05T10:30:02Z",
      "finished": "2025-11-05T10:30:17Z",
      "progress": 100,
      "links": [/* ... */]
    }
  ],
  "links": [
    {
      "href": "/processes/jobs?limit=100&offset=0",
      "rel": "self"
    },
    {
      "href": "/processes/jobs?limit=100&offset=100",
      "rel": "next"
    }
  ]
}
```

### Cancel Job

**Endpoint:** `DELETE /processes/jobs/{jobId}`

**Response:**
```json
{
  "message": "Job cancelled successfully"
}
```

## Execution Tiers

### Tier 1: NTS Executor (In-Process)

**Technology:** NetTopologySuite (NTS) library

**Characteristics:**
- In-process execution (same AppDomain)
- Low latency (< 1s for small datasets)
- Limited to available memory
- No external dependencies

**Limits:**
- Max features: 1,000
- Max input size: 100 MB
- Max geometry complexity: 10,000 vertices

**Use Cases:**
- Quick buffers, centroids, convex hulls
- Small dataset operations
- Trial/Core tier tenants

### Tier 2: PostGIS Executor (Database)

**Technology:** PostgreSQL/PostGIS ST_* functions

**Characteristics:**
- Executes in database
- Leverages spatial indexes
- Scales to larger datasets
- Parallel query execution

**Limits:**
- Max features: 100,000
- Max input size: 1 GB
- Supports complex spatial operations

**Use Cases:**
- Medium-large dataset operations
- Spatial joins, overlays
- Pro tier tenants

**Example Query:**
```sql
-- Buffer operation via PostGIS
INSERT INTO temp_results
SELECT
    id,
    ST_Buffer(geometry::geography, @distance)::geometry as geometry
FROM input_features
WHERE collection_id = @collection_id;
```

### Tier 3: Cloud Batch Executor (Azure Batch)

**Technology:** Azure Batch + Docker containers (GDAL/OGR)

**Characteristics:**
- Containerized execution
- Unlimited scalability
- Fault-tolerant
- Supports raster operations

**Limits:**
- No practical limits
- Autoscaling based on queue depth
- Enterprise tier only

**Container Spec:**
```dockerfile
FROM osgeo/gdal:ubuntu-full-latest

RUN pip install azure-storage-blob requests

COPY geoprocessing-worker.py /app/
WORKDIR /app

ENTRYPOINT ["python", "geoprocessing-worker.py"]
```

## Job Queue and Priority

### Priority Levels

```csharp
public enum JobPriority
{
    Low = 1,        // Trial tier
    Normal = 5,     // Core tier
    High = 7,       // Pro tier
    Urgent = 9      // Enterprise tier
}
```

### Queue Management

```sql
-- Job queue table
CREATE TABLE geoprocessing_jobs (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL,
    operation TEXT NOT NULL,
    status TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 5,
    parameters JSONB NOT NULL,
    inputs JSONB NOT NULL,
    result JSONB,
    error_message TEXT,
    progress INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    worker_id TEXT
);

-- Index for efficient job polling
CREATE INDEX idx_jobs_queue ON geoprocessing_jobs(status, priority DESC, created_at)
WHERE status = 'Pending';
```

### Worker Polling

```csharp
public class GeoprocessingWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll for next job (priority-based)
            var job = await _controlPlane.DequeueJobAsync(stoppingToken);

            if (job != null)
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            else
            {
                // No jobs available, wait before polling again
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

## Progress Tracking

### Progress Updates

```csharp
public async Task<OperationResult> ExecuteAsync(
    Dictionary<string, object> parameters,
    List<GeoprocessingInput> inputs,
    IProgress<GeoprocessingProgress>? progress = null)
{
    var totalFeatures = await GetFeatureCountAsync(inputs[0]);

    for (int i = 0; i < totalFeatures; i++)
    {
        // Process feature
        ProcessFeature(features[i]);

        // Report progress every 100 features
        if (i % 100 == 0)
        {
            progress?.Report(new GeoprocessingProgress
            {
                ProgressPercent = (int)((i / (double)totalFeatures) * 100),
                Message = $"Processed {i}/{totalFeatures} features",
                FeaturesProcessed = i,
                TotalFeatures = totalFeatures
            });
        }
    }
}
```

### Webhook Notifications

```csharp
// Job submission with webhook
POST /processes/buffer/execution
{
  "inputs": { /* ... */ },
  "webhook_url": "https://myapp.com/geoprocessing/callback"
}

// Webhook payload sent on completion
POST https://myapp.com/geoprocessing/callback
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "successful",
  "completed_at": "2025-11-05T10:30:17Z",
  "result_url": "https://api.honua.io/processes/jobs/550e8400-.../results"
}
```

## Result Caching

### Cache Strategy

```csharp
// Cache key generation (deterministic hash of inputs)
var cacheKey = ComputeCacheKey(processId, parameters, inputs);

// Check cache before execution
if (_cache.TryGet(cacheKey, out var cachedResult))
{
    return cachedResult; // Cache hit
}

// Execute operation
var result = await ExecuteOperationAsync(...);

// Cache result (24-hour TTL)
_cache.Set(cacheKey, result, TimeSpan.FromHours(24));
```

### Cache Storage

**Azure Blob Storage:**
```csharp
// Store large results in blob storage
var blobClient = _blobContainerClient.GetBlobClient($"results/{jobId}.geojson");
await blobClient.UploadAsync(resultStream);

// Return signed URL
var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(24));
```

**PostgreSQL:**
```sql
-- Store small results in database
CREATE TABLE geoprocessing_cache (
    cache_key TEXT PRIMARY KEY,
    process_id TEXT NOT NULL,
    result JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_cache_expiry ON geoprocessing_cache(expires_at);
```

## Performance Considerations

### Input Size Limits

| Tier | Max Features | Max Size | Max Vertices/Feature |
|------|-------------|----------|---------------------|
| NTS | 1,000 | 100 MB | 10,000 |
| PostGIS | 100,000 | 1 GB | 100,000 |
| Cloud Batch | Unlimited | Unlimited | Unlimited |

### Optimization Strategies

**1. Spatial Indexing**
```sql
CREATE INDEX idx_features_geometry ON features USING GIST(geometry);
```

**2. Parallel Processing**
```csharp
var tasks = batches.Select(batch => ProcessBatchAsync(batch));
await Task.WhenAll(tasks);
```

**3. Geometry Simplification**
```sql
-- Simplify before buffering
SELECT ST_Buffer(ST_SimplifyPreserveTopology(geometry, 1.0), 100)
FROM features;
```

**4. Bounding Box Pre-Filter**
```sql
-- Use bounding box for quick filtering
WHERE geometry && ST_MakeEnvelope(@xmin, @ymin, @xmax, @ymax, 4326)
  AND ST_Intersects(geometry, clip_boundary);
```

## Error Handling

### Validation

```csharp
public ValidationResult Validate(Dictionary<string, object> parameters, List<GeoprocessingInput> inputs)
{
    var result = new ValidationResult { IsValid = true };

    // Check required parameters
    if (!parameters.ContainsKey("distance"))
    {
        result.IsValid = false;
        result.Errors.Add("Parameter 'distance' is required");
    }

    // Validate parameter ranges
    if (parameters.TryGetValue("distance", out var distValue))
    {
        var distance = Convert.ToDouble(distValue);
        if (distance <= 0)
        {
            result.Errors.Add("Distance must be greater than 0");
        }
        if (distance > 100000)
        {
            result.Warnings.Add("Large buffer distance may result in long processing time");
        }
    }

    return result;
}
```

### Job Failure Handling

```csharp
try
{
    var result = await ExecuteOperationAsync(job);
    await _controlPlane.CompleteJobAsync(job.Id, result);
}
catch (Exception ex)
{
    await _controlPlane.FailJobAsync(job.Id, new
    {
        error_message = ex.Message,
        error_details = ex.StackTrace,
        failed_at = DateTime.UtcNow
    });

    _logger.LogError(ex, "Job {JobId} failed", job.Id);
}
```

## Configuration

### Dependency Injection

```csharp
// In Program.cs
services.AddGeoprocessing(configuration);

// Registers:
// - IProcessRegistry (process definitions)
// - IControlPlane (admission control, queuing)
// - ITierExecutor implementations (NTS, PostGIS, CloudBatch)
// - TierExecutorCoordinator
// - GeoprocessingWorkerService
```

### appsettings.json

```json
{
  "Geoprocessing": {
    "ConnectionString": "Host=postgres;Database=honua;...",
    "EnableCaching": true,
    "CacheTTLHours": 24,
    "DefaultExecutionMode": "Auto",
    "TierSelection": {
      "NtsMaxFeatures": 1000,
      "NtsMaxSizeMB": 100,
      "PostGisMaxFeatures": 100000,
      "PostGisMaxSizeMB": 1024
    },
    "AzureBatch": {
      "Enabled": false,
      "AccountUrl": "https://mybatch.eastus.batch.azure.com",
      "PoolId": "geoprocessing-pool"
    }
  }
}
```

## Usage Examples

### Buffer Roads

```bash
curl -X POST https://api.honua.io/processes/buffer/execution \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "inputs": {
      "distance": 50,
      "segments": 16,
      "dissolve": true,
      "input": {
        "type": "collection",
        "source": "roads",
        "filter": "highway IN ('"'primary'"', '"'secondary'"')"
      }
    }
  }'
```

### Spatial Intersection

```bash
curl -X POST https://api.honua.io/processes/intersection/execution \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "inputs": {
      "input_a": {
        "type": "collection",
        "source": "floodplains"
      },
      "input_b": {
        "type": "collection",
        "source": "parcels"
      }
    }
  }'
```

### Dissolve by Attribute

```bash
curl -X POST https://api.honua.io/processes/dissolve/execution \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "inputs": {
      "input": {
        "type": "collection",
        "source": "counties"
      },
      "dissolve_field": "state_name",
      "aggregations": [
        {"field": "population", "function": "sum"},
        {"field": "area_sqkm", "function": "sum"}
      ]
    }
  }'
```

## Monitoring

### Metrics

```csharp
private static readonly Histogram JobDuration = Metrics.CreateHistogram(
    "geoprocessing_job_duration_seconds",
    "Time to complete geoprocessing job",
    new HistogramConfiguration { LabelNames = new[] { "operation", "tier" } });

private static readonly Gauge QueueDepth = Metrics.CreateGauge(
    "geoprocessing_queue_depth",
    "Number of jobs in queue");

private static readonly Counter JobsCompleted = Metrics.CreateCounter(
    "geoprocessing_jobs_total",
    "Total jobs completed",
    new CounterConfiguration { LabelNames = new[] { "operation", "status" } });
```

### Analytics Queries

```sql
-- Job statistics by operation
SELECT
    operation,
    COUNT(*) as total,
    AVG(EXTRACT(EPOCH FROM (completed_at - started_at))) as avg_duration_seconds,
    COUNT(*) FILTER (WHERE status = 'Completed') as successful,
    COUNT(*) FILTER (WHERE status = 'Failed') as failed
FROM geoprocessing_jobs
WHERE created_at > NOW() - INTERVAL '7 days'
GROUP BY operation;

-- Queue depth over time
SELECT
    DATE_TRUNC('hour', created_at) as hour,
    COUNT(*) as jobs_queued,
    AVG(EXTRACT(EPOCH FROM (started_at - created_at))) as avg_wait_seconds
FROM geoprocessing_jobs
WHERE created_at > NOW() - INTERVAL '24 hours'
GROUP BY hour
ORDER BY hour;
```

## Best Practices

1. **Estimate Before Execution:** Use `EstimateJobAsync()` to check resource requirements
2. **Use Appropriate Tier:** Let auto-selection choose the right executor
3. **Cache Results:** Leverage result caching for repeated operations
4. **Progress Webhooks:** Use webhooks for long-running jobs instead of polling
5. **Batch Operations:** Combine multiple operations into single workflow when possible
6. **Validate Geometries:** Ensure input geometries are valid before processing
7. **Monitor Quotas:** Track job counts against tenant quotas

## Troubleshooting

**Job Stuck in Pending:**
- Check worker service is running: `kubectl get pods | grep geoprocessing-worker`
- Verify queue not paused: `SELECT * FROM geoprocessing_jobs WHERE status = 'Pending'`

**Out of Memory Errors:**
- Reduce input size or split into batches
- Use PostGIS tier instead of NTS
- Enable Cloud Batch for large datasets

**Slow Performance:**
- Verify spatial indexes exist
- Simplify geometries before complex operations
- Use bounding box pre-filters

## Related Documentation

- [ENTERPRISE_FEATURES.md](/home/user/Honua.Server/src/Honua.Server.Enterprise/ENTERPRISE_FEATURES.md) - Enterprise features overview
- [OGC API - Processes Standard](https://docs.ogc.org/is/18-062r2/18-062r2.html) - OGC specification
- [Multitenancy Module](../Multitenancy/README.md) - Multi-tenant architecture
- ETL Module - Workflow orchestration with geoprocessing nodes
