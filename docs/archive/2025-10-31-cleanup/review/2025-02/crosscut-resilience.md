# Cross-Cutting Pass: Resilience & Reliability – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | Background services (preseed, ingestion, migration), circuit breakers, retry policies, HA considerations |
| Methods | Static analysis; no chaos tests executed |

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | Raster/Vector preseed services store active/completed jobs in memory | `ActiveRasterPreseedJobStore` / `CompletedRasterPreseedJobStore` use in-memory collections; process crash loses job metadata. | Persist jobs to Redis/Postgres; add resume capability (idempotent operations).
| 2 | **High** | Data ingestion service queue is in-memory channel; restart drops queued jobs | `DataIngestionService` uses `Channel<DataIngestionWorkItem>`; no durable queue. | Use durable queue (Service Bus, SQS) or persist job requests before enqueue.
| 3 | **High** | Migration service lacks retry/backoff | `EsriServiceMigrationService.ProcessJobAsync` does not implement retry; transient errors cause job failure. | Add Polly retry/backoff; mark job status Partial and allow resume.
| 4 | **Medium** | Circuit breaker metrics missing; operators can’t detect open circuits | As noted in performance pass; same implication for reliability. | Emit metrics/alerts when circuit opens; ensure fallback actions.
| 5 | **Medium** | Alert receiver DB migration failure logged but not surfaced | As earlier: reliability issue (loss of history). | Fail startup or raise health failure.
| 6 | **Medium** | CLI automation no rollback verification | `DeployInfrastructureStep` implements rollback but no tests; cancellation might leave resources. | Add integration tests verifying rollback/destroy on failure; track deployed resources and handle partial completion.
| 7 | **Low** | GracefulShutdownService resets tokens but lacks integration tests | `GracefulShutdownService` handles ctrl+c but no tests/monitoring. | Add smoke test to ensure readiness/liveness toggles.
| 8 | **Low** | No chaos/resilience runbook documented | Docs missing procedures for failing over services, clearing queues, etc. | Add resilience runbook covering preseed queue recovery, ingestion job restart, circuit breaker handling.

---

## Suggested Follow-Ups

1. Introduce durable storage for job queues and status; expose status API for operators. 
2. Add retry/backoff policies to long-running jobs (migration, ingestion) with partial-progress persistence.
3. Update operational docs/runbooks to cover failure recovery steps and monitoring.

---

## Tests / Verification

- None (static analysis).
