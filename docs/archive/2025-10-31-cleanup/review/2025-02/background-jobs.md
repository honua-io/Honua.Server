# Background Jobs & Pipelines Review – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/Raster/**`, `.../VectorTiles/**`, `.../Admin/*Preseed*`, `src/Honua.Server.Core/Import/**`, `src/Honua.Server.Core/Migration/**`, GitOps watcher, hosted services registered in `Extensions/ServiceCollectionExtensions.cs` |
| Methods | Static analysis of background services, job orchestration, cancellation behaviour, and admin APIs; no runtime execution |

---

## High-Level Observations

- All long-running job systems share a consistent pattern (bounded `Channel`, `ActiveJobStore`, cancellation tokens). However, none of the admin endpoints require CSRF/anti-forgery and rely solely on role-based policies—worth re-evaluating for exposed deployments.
- Raster/vector preseeders still honour legacy cache assumptions (square tiles, synchronous per-tile rendering). Parallelism and queue back-pressure exist, but failure handling around cache provider I/O is shallow (exceptions swallowed, no retry/backoff).
- Data ingestion and migration services depend on GDAL/OGR native binaries; process-wide static configuration happens lazily, but misconfiguration throws synchronously on Enqueue without remediation guidance.
- Background hosted services (schema warmup, security validation, STAC sync) run sequentially at startup; failures bubble and can halt the host. Logging is informative but we should consider circuit breakers/health status updates.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | Raster preseed queue drops dataset exceptions without updating job status | `ProcessDatasetAsync` catches general exceptions, logs, but still marks job as completed (`RasterTilePreseedService.cs:278-336`). Clients see “Completed” despite partial cache failures. | Mark job `Failed`/`Partial` when any dataset fails. Add per-dataset error list to the snapshot so operators can requeue.
| 2 | **High** | Vector tile preseed cancellation only sets a flag; long-running tile generation ignores it | `GenerateTileAsync` reads cancellation token once (`VectorTilePreseedService.cs:367-433`) but downstream exporters (GeoJSON → MVT) run synchronously; large zoom ranges ignore cancellation until completion. | Break large loops with `ThrowIfCancellationRequested`, stream tiles asynchronously, or chunk work to honour cancellation quickly.
| 3 | **High** | Data ingestion accepts arbitrary files but writes to `/tmp` without quota checks | `HandleCreateJob` stores uploads to temp paths (`DataIngestionEndpointRouteBuilderExtensions.cs:142-221`) with only coarse `MaxFileSizeMiB`. Multi-GB uploads can exhaust disk. | Enforce per-job and global disk quotas; stream uploads directly to blob storage or reject when space low via `DriveInfo.AvailableFreeSpace`.
| 4 | **Medium** | Admin ingestion endpoints skip antiforgery but also lack explicit CORS restrictions | Endpoints are tagged admin-only but run on same origin as public APIs; if auth cookies are reused, CSRF risk remains. | Require antiforgery tokens or separate host/API domain; at least annotate policy expectations in docs and middleware.
| 5 | **Medium** | Esri migration job retries missing | `ProcessJobAsync` logs and marks failure; transient HTTP/DB errors require manual restart. | Introduce retry policy (e.g., Polly) for known transient categories, expose failure reason to clients.
| 6 | **Medium** | GitWatcher polls via `Task.Delay`; tight interval can spawn overlapping reconciles if prior run slow | `ExecuteAsync` loops without guarding against long-running `ReconcileAsync`. | Use semaphore or track in-flight reconcile task; if still running, skip new poll or queue single re-run.
| 7 | **Low** | Background hosted services log at `Information` even when disabled by configuration | e.g., `ProductionSecurityValidationHostedService` logs “Validating” even for Development environment (`ProductionSecurityValidationHostedService.cs:59-113`). | Gate registration or reduce log level to `Debug` when feature disabled.
| 8 | **Low** | Completed job stores keep only last 100 jobs; no paging/filtering on admin endpoints | Large tenants lose history silently. | Support pagination backed by storage (e.g., Redis/DB) or document retention limits and add ordering parameters.

---

## Suggested Follow-Ups

1. Harden admin pipeline endpoints: enforce antiforgery or isolate under dedicated origin; add disk quota checks and streaming ingestion support.
2. Improve job state telemetry: include dataset-level failure details and expose Prometheus counters for success/failure, queue depth, cancellation.
3. Refactor tile preseeders to honour cancellation promptly and surface partial completion vs success in API responses.
4. Add retry/circuit breaker strategy to Esri migration + STAC synchronization to avoid manual restarts on transient infra issues.

---

## Tests / Verification

- Static analysis only. No automated tests executed for this pass.
