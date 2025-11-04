# Cross-Cutting Pass: Performance & Streaming – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | Streaming exporters (GeoJSON, GML, GeoParquet, attachment pipeline), preseed services, CLI automation, alerting pipelines |
| Methods | Static analysis; no benchmarks executed |

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | GeoParquet exporter still buffers full dataset | `src/Honua.Server.Core/Export/GeoParquetExporter.cs` TODOs for type mapping; writer loads entire result set. | Implement streaming writer using Parquet row groups; avoid loading full dataset in memory; add tests for large datasets.
| 2 | **High** | GeoArrow streaming writer coerces all values to strings | `GeoArrowStreamingWriter` leaves TODO for numeric/temporal mapping; causes large payload + downstream parsing cost. | Implement schema inference and proper column types before release; add microbenchmarks.
| 3 | **High** | Attachment download still loads attachments into memory before streaming | `AttachmentDownloadHelper` fetches stream but may buffer to memory/disk for non-seekable streams. | Implement streaming pipeline with `CopyToAsync`, support range requests without buffering.
| 4 | **Medium** | WFS transaction handler materialises result lists for locking | `WfsTransactionHandlers` enumerates records into `List<WfsFeature>` before processing. | Refactor to streaming pipeline; consider chunking.
| 5 | **Medium** | CLI process steps run Terraform sequentially; no concurrency but plan/apply always re-run even when unchanged | `DeployInfrastructureStep` always runs `plan`→`apply`. | Cache tfplan, detect no-op to skip apply; reduce runtime.
| 6 | **Medium** | Raster preseed service uses single reader; per-tile tasks sequential for each dataset | `RasterTilePreseedService` loops sequentially; limited parallelism. | Introduce configurable degree of parallelism with backpressure.
| 7 | **Low** | Alert receiver metrics aggregator uses `ConcurrentDictionary` with lock; okay but gauge measurement closure allocates per call | `AlertMetricsService.RecordCircuitBreakerState`. | Minor: consider `ImmutableDictionary` or `Meter` instrumentation to reduce lock overhead.
| 8 | **Low** | Observability builder removes spans on high-throughput endpoints; ensure sampling config documented | `ObservabilityExtensions` sets activity sampling but default unclear. | Document recommended sampling rate; ensure streaming endpoints not suppressed.

---

## Suggested Follow-Ups

1. Prioritise streaming improvements (GeoParquet, GeoArrow, attachments) before GA; add integration benchmarks.
2. Extend performance smoke tests to cover WFS transactions and attachment downloads under load.
3. Document resource tuning knobs (preseed DOP, alert throughput) for operators.

---

## Tests / Verification

- None (static analysis).
