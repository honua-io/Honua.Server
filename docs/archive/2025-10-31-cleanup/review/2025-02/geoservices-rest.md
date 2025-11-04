# Geoservices REST Review – 2025-02-14

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/GeoservicesREST/**`, supporting services under `Honua.Server.Core` used exclusively by GeoServices |
| Methods | Static analysis (code/logic review), no execution |

---

## High-Level Observations

- GeoServices editing endpoints now live in a partial controller that delegates to `GeoservicesEditingService`; the empty “Phase 2” controllers were removed, so routing is consolidated again.
- Query execution continues to flow through `GeoservicesQueryService`; the guard rails around statistics/distinct requests and streaming counts remain in place.
- Streaming helpers (GeoJSON/KML/TopoJSON) still enforce defensive caps and emit OpenTelemetry spans via `ActivityScope`.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** → Resolved | `useGlobalIds=true` deletes never resolve to object IDs | Prior implementation left `requestedGlobalId` null, so delete commands ran against the raw GUID and always returned `not_found`. | Fixed by carrying the requested GlobalID through `PopulateDeleteCommands`, converting to object IDs inside `NormalizeGlobalIdCommandsAsync`, and producing a failure result when the lookup misses. Regression tests now cover delete-by-global-id (success + not-found). |
| 2 | **High** → Resolved | Updates with `useGlobalIds=true` required object IDs | `PopulateUpdateCommands` rejected updates that only supplied `globalId`, preventing legitimate clients from using GlobalID-only edits. | Fixed by allowing updates to enqueue when a GlobalID is present; `NormalizeGlobalIdCommandsAsync` rewrites to the object ID before execution. Covered by a new update-by-global-id unit test. |
| 3 | **Medium** → Resolved | Chunked edit payloads were rejected as “empty” | `ParsePayloadAsync` previously bailed when `ContentLength` was null, so HTTP/1.1 chunked uploads hit `BadRequest`. | Fixed by parsing the request body regardless of `ContentLength` (resetting seekable streams and tolerating chunked transfer encoding). Consider streaming the parser in a follow-up for large payloads. |

---

## Verification Updates (2025-02-15)

- Added focused unit coverage for attachment query/add flows (`GeoservicesRESTAttachmentControllerTests`) to ensure metadata validation and orchestrator wiring stay intact after the controller refactor.
- Added MinIO-backed integration coverage for the core attachment orchestrator (`FeatureAttachmentOrchestratorIntegrationTests`) to exercise upload, list, and delete flows against the S3-compatible emulator.

---

## Recommended Next Steps

1. Backlog: add integration coverage on the full controller pipeline (apply/update/delete) to complement the unit tests.
2. Backlog: consider streaming/pipe-based payload parsing for large edit payloads.
3. Revisit outstanding backlog items (streaming count optimization, KML styling).

---

## Tests / Verification

- `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj`
- `dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj`
- `dotnet test tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj --filter "FullyQualifiedName~GeoservicesRESTAttachmentControllerTests"`
- `dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter "FullyQualifiedName~FeatureAttachmentOrchestratorIntegrationTests"`
