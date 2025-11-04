# WFS & WMS Review – 2025-02-17

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/Wfs/**`, `src/Honua.Server.Host/Wms/**`, shared raster/feature helpers |
| Methods | Static analysis (handlers, helpers, response builders); no runtime execution |

---

## High-Level Observations

- WFS GeoJSON/GML responses now stream directly from the repository via `GeoJsonFeatureCollectionStreamingWriter` and `GmlStreamingWriter`. The previous materialisation step was removed, cutting peak memory while keeping telemetry hooks.
- GML writers now preserve source feature identifiers (falling back to deterministic GUIDs when required) and emit bounding boxes + optional lock IDs without holding the full payload in memory.
- WMS tile caching now recognises near-aligned requests, tolerates rectangular output sizes, and folds the request dimensions/time slice into the cache key so more clients benefit from hot tiles.
- Cache metrics capture variant/time tags, so operators can slice hit/miss rates per rendered size or temporal slice straight from `honua.raster.cache_hits/_misses`.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | WFS GetFeature previously buffered entire result sets in memory | Legacy `WfsResponseBuilders` materialised `List<WfsFeature>` plus `JsonArray`/XML nodes, doubling memory usage on large layers. | ✅ **Resolved (2025-02-18)** – `HandleGetFeatureAsync` now streams via `GeoJsonFeatureCollectionStreamingWriter` / `GmlStreamingWriter` (`src/Honua.Server.Host/Wfs/WfsGetFeatureHandlers.cs:76-138`), eliminating intermediate buffers while preserving telemetry and pagination metadata. |
| 2 | **High** | GML responses generated synthetic `gml:id` values that collided across pages | Historical implementation used `${layer.Id}.{index}` keyed off iteration order, so paging restarted IDs at 1. | ✅ **Resolved (2025-02-18)** – `GmlStreamingWriter` reuses the layer’s `IdField` (with deterministic GUID fallback) and carries the bounding box + lockId attributes forward (`src/Honua.Server.Host/Ogc/GmlStreamingWriter.cs:74-333`). |
| 3 | **Medium** | WMS tile cache previously only activated for square, perfectly aligned requests | Legacy logic required `width == height` and exact bounding-box equality, so real-world WMS clients missed the cache (`src/Honua.Server.Host/Wms/WmsGetMapHandlers.cs:169-352`). | ✅ **Resolved (2025-02-18)** – Cache key generation now tolerates non-square outputs, snaps close bounding boxes via adaptive tolerance, encodes dimension variants in the style key, and includes the TIME parameter when present (`src/Honua.Server.Host/Wms/WmsGetMapHandlers.cs:109-360`). |

---

## Suggested Follow-Ups

1. Profile memory usage of the new streaming writers on large feature sets (multi-hundred-thousand features) to validate the absence of backpressure issues.
2. Publish dashboard templates (e.g., Grafana) that visualise the new cache variant/time counters to speed up operational adoption.

---

## Tests / Verification

- `tests/Honua.Server.Host.Tests/Wfs/GmlStreamingWriterTests.cs` – unit coverage for lock-aware GML streaming metadata.
- `tests/Honua.Server.Host.Tests/Wfs/WfsGetFeatureWithLockIntegrationTests.cs` – end-to-end coverage of `GetFeatureWithLock` streaming and cache headers.
- `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj` (180 tests) – passes with the new streaming writers and WMS cache expansions.
