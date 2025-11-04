# OGC API Review – 2025-02-15

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/Ogc/**`, supporting streaming/export helpers under `Honua.Server.Core` used by the OGC APIs |
| Methods | Static analysis (code/logic review), no execution |

---

## High-Level Observations

- Most OGC behaviour still flows through `OgcSharedHandlers`, with the protocol-specific handler classes (`WMS/WFS/WCS/WMTS`) left as TODO scaffolding. Any regressions here impact every landing/feature/tile response.
- Feature retrieval defaults (GeoJSON/HTML) buffer results into in-memory lists; only the exporter code paths stream directly from the repository.
- Attachment exposure was partially optimised to batch-load descriptors, but the second-stage link builder still performs per-feature lookups, undoing the intended savings.
- `/ogc/search` issues `CountAsync` (hits) and then `QueryAsync` sequentially for each collection; offset/limit management assumes `int`-bounded queries, so large fan-out searches can hammer the backing stores even when the client only needs a small window.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** → Resolved | Attachment batching is ineffective; per-feature lookups still occur | `GetCollectionItems` populated `attachmentMap` with `ListBatchAsync`, yet `CreateAttachmentLinksAsync` immediately called `ListAsync`, causing N+1 queries (`src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs:640`, `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2536`). | Updated `CreateAttachmentLinksAsync` to accept pre-loaded descriptors and taught `GetCollectionItems` to supply the batch results, eliminating the redundant per-feature lookups. |
| 2 | **High** → Resolved | Default GeoJSON/HTML responses buffer entire pages in memory | Added streaming responses for the default GeoJSON and HTML formats when attachments aren’t exposed (`src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs:556-678`). HTML output now streams directly to the response writer (no feature list allocation) and GeoJSON continues to use the lightweight writer introduced earlier. Host tests cover the HTML path (`tests/Honua.Server.Host.Tests/Ogc/OgcFeaturesHandlersTests.cs:331`). | Monitor heap usage for attachment-heavy collections; those still require buffering because attachment link expansion needs per-feature descriptors. |
| 3 | **Medium** → Resolved | `/ogc/search` counts each collection before applying offsets/limits | Removed the automatic `CountAsync` fan-out when the caller only supplies `offset`/`limit`. Offsets are now distributed while streaming results, and `numberMatched` is omitted unless explicitly requested (`src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:1013-1033`). Added regression coverage verifying that `CountAsync` isn’t invoked in this scenario (`tests/Honua.Server.Host.Tests/Ogc/OgcFeaturesHandlersTests.cs:397`). | If clients opt-in to counts or request `resultType=hits`, the handler still issues `COUNT(*)`; consider batching/parallelising those in a future pass. |

---

## Next Steps

1. Profile attachment-heavy collections—batch loading still buffers descriptors; consider range queries to cap memory.
2. Expand HTML streaming to support attachment payloads (requires async link rendering).
3. Explore concurrent search fan-out when `count=true` to limit sequential latency.

---

## Tests / Verification

- Targeted scenarios still to run once fixes land: `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter "Feature=Ogc"` (after adding coverage) and the existing MinIO-backed attachment integration tests once they exercise OGC paths.
