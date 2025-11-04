# OData & Compatibility Review – 2025-02-17

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/OData/**`, OData bootstrap/host wiring, and legacy compatibility surfaces (`ArcGisTokenEndpoints`, `Carto/**`) |
| Methods | Static analysis only (code walkthrough, no execution) |

---

## High-Level Observations

- The dynamic OData controller now routes every write through the edit orchestrator, but concurrency safeguards were bolted on without completing the ETag handshake.
- Geo filter pushdown relies on manually parsing the `$filter` string; when parsing fails, manual fallback silently suppresses all results instead of surfacing an error.
- Carto compatibility helpers roll their own absolute-link builder and skip the trusted proxy utilities, so URLs drift behind TLS terminators.
- The ArcGIS-compatible token endpoint advertises an ASP.NET rate limiter, yet the host no longer registers any policies/middleware, leaving the route brute-forceable.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** → Resolved | OData concurrency tokens now emit deterministic ETags and compare correctly | Single-item reads compute and return strong ETags (`src/Honua.Server.Host/OData/DynamicODataController.cs:359-362`), writes normalise `If-Match` headers and re-compute deterministic hashes before passing them to the orchestrator (`:520-612`, `:712-808`, `:899-985`), and the new helpers replace the unstable `GetHashCode` fallback with SHA256-based payloads (`:1071-1134`). | Follow-up tests should cover GET→PATCH/DELETE happy path vs. stale ETag to guard the new logic. |
| 2 | **High** → Resolved | `geo.intersects` fallback honours geography literals and rejects bad geometry instead of silently dropping rows | The parser now accepts both `geometry'` and `geography'` tokens and trims whitespace before extracting the literal (`src/Honua.Server.Host/OData/Services/ODataQueryService.cs:297-333`), and the manual filter path raises an `ODataException` when the geometry cannot be prepared instead of returning an empty result (`src/Honua.Server.Host/OData/DynamicODataController.cs:148-166`). | Add parser and controller tests that cover geography literals, malformed payloads, and manual-filter fallbacks to prevent regressions. |
| 3 | **High** → Resolved | ArcGIS token endpoint no longer claims in-process rate limiting | Removed the stale `.RequireRateLimiting("token-generation")` metadata so the route reflects the YARP-only throttling model (`src/Honua.Server.Host/Authentication/ArcGisTokenEndpoints.cs:24-42`). | Document in deployment notes that ArcGIS token throttling must be configured at the proxy (YARP) tier. |
| 4 | **Medium** → Resolved | Carto compatibility links respect forwarded headers | All absolute links now flow through `RequestLinkHelper.BuildAbsoluteUri`, which already honours trusted proxy configuration (`src/Honua.Server.Host/Carto/CartoHandlers.cs:22-55`, `:244-273`). | Add a proxy-mode test to validate URLs under `X-Forwarded-*` scenarios. |

---

## Suggested Follow-Ups

1. Add a focused OData test suite for GET→PATCH/DELETE with ETag round-tripping once the concurrency fix lands.
2. Introduce parser coverage for the geo functions (happy path, malformed, whitespace) to guard against future regressions.
3. Wire the token-rate limit policy through the existing security configuration and run a brute-force smoke test to confirm throttling.
4. Extend the Carto HTTP tests (or a lightweight unit test) to validate link generation behind a mock proxy.

---

## Tests / Verification

- `dotnet build` (root solution)  
- Recommended follow-ups: once the new OData concurrency tests are in place, run  
  `dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter "Category=OData"` and extend proxy-mode smoke tests for Carto link generation.
