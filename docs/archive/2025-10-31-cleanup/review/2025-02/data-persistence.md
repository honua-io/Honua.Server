# Data Persistence Review – 2025-02-17

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Core/Data/**` (repositories, provider implementations, fallback aggregation helpers) |
| Methods | Static analysis only |

---

## High-Level Observations

- PostgreSQL has the most complete coverage (native aggregations, vector tiles, transaction support). MySQL and SQLite lean heavily on shared fallback helpers that read every feature into memory.
- Provider abstractions expose the same surface (query, CRUD, stats, distinct, extent), so deficiencies in one provider tend to bubble up everywhere the repository is used.
- Retry policies exist (Polly pipelines), but some write paths bypass them, so transient faults can bubble directly back to callers.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** → Resolved | MySQL/SQLite stats, distinct, and extent queries now execute in SQL instead of loading whole layers | Added builder support for aggregation/distinct/extent (`src/Honua.Server.Core/Data/MySql/MySqlFeatureQueryBuilder.cs:115-209`, `src/Honua.Server.Core/Data/Sqlite/SqliteFeatureQueryBuilder.cs:93-210`) and rewired the providers to use those definitions (`src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:722-834`, `src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs:720-858`). Queries stay on the server and return only aggregated rows. | Monitor telemetry for any residual fallbacks; add integration tests covering large tables to exercise the SQL paths. |
| 2 | **High** → Resolved | MySQL bulk update batches now run through the retry pipeline | `ExecuteUpdateBatchAsync` wraps each `ExecuteNonQueryAsync` call with `_retryPipeline.ExecuteAsync` (`src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:608-612`), so transient deadlocks/lock waits can be retried without aborting the batch. | Consider capturing retry metrics to spot noisy workloads. |
| 3 | **Medium** → Resolved | MySQL connection bootstrap respects cancellation | Replaced the synchronous helper with `CreateConnectionAsync`, routing every call through the async path and honoring the caller’s token (`src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:841-855`). Callers now await the bootstrap instead of blocking threads. | N/A |

---

## Suggested Follow-Ups

1. Add integration tests covering the new MySQL/SQLite statistics/distinct/extent SQL paths (large tables, group-by, point vs. non-point layers).
2. Add integration tests for MySQL bulk update/delete under induced deadlocks to validate retry behaviour.
3. Collect lightweight telemetry around aggregation/extent calls to detect residual fallbacks and high-cost workloads.

---

## Tests / Verification

- `dotnet build`
