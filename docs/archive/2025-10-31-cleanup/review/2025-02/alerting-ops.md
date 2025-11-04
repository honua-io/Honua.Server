# Alerting & Ops Integrations Review – 2025-02-18

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.AlertReceiver/**`, alert publishers, alert metrics, persistence, rate limiting; host-level observability wiring (`Extensions/ObservabilityExtensions.cs`, tracing exporters) |
| Methods | Static analysis only (no runtime execution) |

---

## High-Level Observations

- Alert receiver now leans on a Dapper-backed Postgres store for alert history, dedupe, and silencing. Core resilience features (retry/circuit breaker) exist but still lack operator-facing telemetry (Prometheus metrics, alert-on-alert feedback).
- Persistence failures remain easy to miss because write paths swallow exceptions—alerts continue flowing even when nothing reaches the database.
- Schema creation happens lazily on first DB connection; we need a startup/self-test to guarantee the store is reachable before accepting traffic.
- Metrics use .NET `Meter` but the app does not expose Prometheus/OpenTelemetry exporters by default; operators must configure one manually.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** | Alert writes still fail silently | `AlertPersistenceService.SaveAlertAsync` catches all exceptions and only logs. When Postgres is down the API returns success but history is lost. | Make persistence failures fatal (or surface them via metrics/health). Add structured logging plus retry/queue so operators catch outages quickly. |
| 2 | **High** | No startup guard for Postgres connectivity | Schema is created lazily inside `AlertHistoryStore` when the first request arrives. If the DB is unreachable startup succeeds and the first alert fails later. | Add hosted startup check / readiness probe that validates the connection and schema before the app reports ready. |
| 3 | **Medium** | JWT rotation plumbing present but operational story missing | `Program.cs` now supports multiple `Authentication:JwtSigningKeys`, yet docs/config still describe a single shared secret. Operators lack rotation guidance. | Document staged key rollout (active/next), ensure health endpoints expose active key id, and integrate with managed secret store. |
| 4 | **Medium** | Circuit breaker callbacks do not update metrics | `AlertMetricsService.RecordCircuitBreakerState` exists but `CircuitBreakerAlertPublisher` still never calls it. Ops lack insight into open circuits. | Emit metrics in circuit breaker callbacks and surface gauges/durations; raise alert-on-alert when a provider stays offline. |
| 5 | **Medium** | Alert history API lacks paging/retention controls | `AlertHistoryController` returns the full dataset ordered by time. With no TTL, tables will keep growing. | Add paging parameters & a retention job (scheduled purge or TTL). Surface metrics for DB size/backlog. |
| 6 | **Low** | Metrics exported via `Meter` but no Prometheus/Otel exporter configured | `Program.cs` still does not register exporters. Operators must add them manually; docs omit guidance. | Provide optional Prometheus endpoint or OTLP exporter (e.g., `AddOpenTelemetryMetering`) and document setup in ops guide. |

---

## Suggested Follow-Ups

1. Add readiness/startup validation for the Postgres store plus structured alerting when writes fail.
2. Document and automate JWT signing-key rotation (active/next) leveraging managed secret storage.
3. Wire circuit breaker callbacks to metrics/logging and create dashboards for downstream provider health.
4. Introduce paging + retention for alert history tables; instrument table growth metrics.
5. Provide Prometheus/OTel exporter wiring and operator runbook for metrics ingestion.

---

## Tests / Verification

- `dotnet test tests/Honua.Server.AlertReceiver.Tests/Honua.Server.AlertReceiver.Tests.csproj`
- `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter FullyQualifiedName~Honua.Server.Host.Tests.Security.SecurityPolicyMiddlewareTests`
