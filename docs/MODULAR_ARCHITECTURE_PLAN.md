# Modular Architecture Plan for Serverless & AOT Optimization

**Status:** Planning Phase
**Created:** 2025-11-01
**Goal:** Split monolithic dependencies to enable lightweight serverless deployments with fast cold starts

## Current Problem

**Honua.Server.Core** is monolithic (~150MB with dependencies):
- GDAL bindings (raster processing) - ~80MB
- OData protocol stack - ~15MB
- Cloud SDKs (AWS/Azure/GCP) - ~25MB
- SkiaSharp rendering - ~50MB
- LibGit2Sharp, ParquetSharp, etc.

**Impact:**
- Every deployment pulls ALL dependencies
- Slow serverless cold starts (5-10 seconds)
- Cannot use Native AOT (OData/GDAL incompatible)
- Large container images even for simple deployments

## Architecture Goals

1. **Modular Core** - Split heavy dependencies into opt-in packages
2. **Fast Serverless** - Lightweight entry point with <2s cold start
3. **ReadyToRun (R2R)** - 30-50% faster startup (AOT alternative)
4. **Backward Compatible** - Existing deployments unchanged

## Proposed Project Structure

```
Core Projects (Required):
├─ Honua.Server.Core (~30MB)
│  ├─ Database abstractions (Dapper, Npgsql, MySql, SQLite, SQL Server)
│  ├─ Configuration & DI
│  ├─ Authentication & Authorization
│  ├─ NetTopologySuite (vector geometry)
│  ├─ Basic data providers (PostgreSQL, MySQL, SQLite)
│  └─ Caching, metrics, core utilities

Optional Feature Modules:
├─ Honua.Server.Core.Raster (~80MB)
│  ├─ MaxRev.Gdal.Core (raster data processing)
│  ├─ SkiaSharp (map rendering)
│  ├─ ParquetSharp (Parquet file format)
│  ├─ LibGit2Sharp (Git operations)
│  └─ BitMiracle.LibTiff.NET (TIFF support)
│
├─ Honua.Server.Core.OData (~15MB)
│  ├─ Microsoft.OData.Core
│  ├─ Microsoft.OData.Edm
│  ├─ Microsoft.Spatial
│  └─ OData query translation to SQL
│
└─ Honua.Server.Core.Cloud (~25MB)
   ├─ AWSSDK.S3, AWSSDK.KMS
   ├─ Azure.Storage.Blobs, Azure.ResourceManager
   └─ Google.Cloud.Storage.V1, Google.Cloud.Kms.V1

Entry Points:
├─ Honua.Server.Host (Full-Featured)
│  └─ References: Core + Raster + OData + Cloud
│  └─ Target: Traditional deployments, Docker Compose, Kubernetes
│  └─ Image Size: ~150MB
│
├─ Honua.Server.Host.Lite (Lightweight)
│  └─ References: Core + OData only (no raster processing)
│  └─ Target: Serverless (Cloud Run, Lambda, Container Apps)
│  └─ Image Size: ~60MB
│  └─ Startup: <2 seconds with R2R
│
└─ Honua.Server.Enterprise.Functions (Minimal)
   └─ References: Core only (existing project)
   └─ Target: Background tasks, timers
   └─ Image Size: ~35MB
```

## Migration Strategy

### Phase 1: Create New Projects (Non-Breaking)

1. **Create `Honua.Server.Core.Raster`**
   - Move GDAL-dependent code from Core
   - Keep interfaces in Core for compatibility
   - Runtime feature detection

2. **Create `Honua.Server.Core.OData`**
   - Move OData endpoint logic from Host
   - Make OData endpoints conditional
   - Graceful degradation if not loaded

3. **Create `Honua.Server.Core.Cloud`**
   - Move cloud provider implementations
   - Keep abstractions in Core
   - Factory pattern for provider loading

4. **Create `Honua.Server.Host.Lite`**
   - New entry point project
   - References Core + OData only
   - Vector-only features enabled

### Phase 2: Update Existing Projects

1. **Honua.Server.Core** - Remove heavy dependencies
   - Keep: Database drivers, NTS, configuration
   - Remove: GDAL, SkiaSharp, LibGit2Sharp, Cloud SDKs
   - Add: Conditional compilation for feature detection

2. **Honua.Server.Host** - Add all feature modules
   - Reference: Core.Raster, Core.OData, Core.Cloud
   - No code changes needed
   - Maintains full feature set

3. **Honua.Server.Enterprise** - Update references
   - May need Core.Raster for some operations
   - Evaluate per-feature

### Phase 3: Optimize Dockerfiles

1. **Dockerfile** - Add ReadyToRun compilation
2. **Dockerfile.lite** - New lightweight image for Host.Lite
3. **Dockerfile.cloudrun** - Update with R2R
4. **DELETE** - `deployment/docker/Dockerfile.host` (redundant)

## Technical Implementation

### Conditional Feature Loading

```csharp
// In Honua.Server.Core
public interface IGdalRasterProvider { }

// In Honua.Server.Core.Raster
public class GdalRasterProvider : IGdalRasterProvider { }

// In Program.cs
services.AddRasterSupport(); // Extension method from Core.Raster
// OR
services.AddVectorOnlySupport(); // Extension method from Core
```

### ReadyToRun Configuration

```xml
<!-- All Dockerfile builds -->
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode>
</PropertyGroup>
```

## Deployment Targets

### Full-Featured (Honua.Server.Host)
- **Use Cases:** Primary API server, all features
- **Deployment:** Docker Compose, Kubernetes, VMs
- **Image:** Dockerfile (chiseled)
- **Size:** ~150MB
- **Startup:** 3-5s with R2R

### Lightweight (Honua.Server.Host.Lite)
- **Use Cases:** Vector-only API, OData queries, high-scale serverless
- **Deployment:** Cloud Run, AWS Lambda (Container), Azure Container Apps
- **Image:** Dockerfile.lite (alpine-slim)
- **Size:** ~60MB
- **Startup:** <2s with R2R
- **Features:** OGC APIs (WFS, WMTS vector), STAC, OData, GeoJSON

### Minimal (Honua.Server.Enterprise.Functions)
- **Use Cases:** Background jobs, cleanup, monitoring
- **Deployment:** Azure Functions, AWS Lambda
- **Size:** ~35MB
- **Startup:** <1s

## Performance Estimates

| Deployment | Current | With Modular + R2R |
|------------|---------|-------------------|
| **Docker Image Size** | 150MB | 60-150MB (selectable) |
| **Cold Start (Serverless)** | 5-10s | 1-3s |
| **Memory Footprint** | 200-400MB | 100-400MB |
| **First Request** | 6-12s | 2-4s |

## File Organization

```
src/
├─ Honua.Server.Core/                    # Base (30MB)
├─ Honua.Server.Core.Raster/            # NEW: GDAL/rendering (80MB)
├─ Honua.Server.Core.OData/             # NEW: OData protocol (15MB)
├─ Honua.Server.Core.Cloud/             # NEW: Cloud SDKs (25MB)
├─ Honua.Server.Host/                    # Full entry point
├─ Honua.Server.Host.Lite/              # NEW: Lightweight entry point
├─ Honua.Server.Enterprise/              # Enterprise features
├─ Honua.Server.Enterprise.Functions/    # Serverless functions
└─ ...existing projects...
```

## Breaking Changes

**None** - This is additive:
- Existing `Honua.Server.Host` deployments unchanged
- New `Honua.Server.Host.Lite` opt-in
- Backward compatible APIs

## Success Criteria

1. ✅ Honua.Server.Host.Lite builds successfully
2. ✅ Lite deployment <60MB container image
3. ✅ Lite cold start <2 seconds (Cloud Run)
4. ✅ Full deployment maintains all features
5. ✅ No breaking changes to existing APIs
6. ✅ ReadyToRun compilation working

## Next Steps

1. Create project files for Core.Raster, Core.OData, Core.Cloud
2. Move dependencies from Core to new projects
3. Create Honua.Server.Host.Lite entry point
4. Update Dockerfiles with ReadyToRun
5. Test both deployment paths
6. Update documentation

## Notes

- **AOT Not Feasible:** OData, GDAL incompatible with Native AOT
- **R2R Alternative:** 30-50% faster startup, works with all dependencies
- **Phased Rollout:** Can implement incrementally without breaking existing deployments
- **License Compatibility:** All new project structure under Elastic License 2.0
