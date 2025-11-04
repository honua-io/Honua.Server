# STAC & Records Review – 2025-02-16

| Item | Details |
| --- | --- |
| Reviewer | Code Review Agent |
| Scope | `src/Honua.Server.Host/Stac/**`, `src/Honua.Server.Host/Records/**`, supporting STAC contracts in `Honua.Server.Core/Stac` |
| Methods | Static analysis (controllers, services, middleware, policies); no runtime execution |

---

## High-Level Observations

- STAC controllers lean on `StacReadService`/`StacControllerHelper` for most logic, but several advertised STAC API extensions (Filter, anonymous landing/conformance access) are not actually wired through.
- Query handling for `/stac/search` implements bbox/datetime/fields, yet ignores filter payloads and omits certain GET parameters, so the published conformance list overstates what works.
- Collection and records endpoints reuse shared output-cache policies; however, public landing routes remain gated behind `RequireViewer`, breaking expected anonymous discovery in STAC and OGC Records.
- Pagination helpers assume callers provide safe limits; the collections list forwards raw `limit` values straight to the store, opening the door for excessive page size requests.

---

## Findings

| # | Severity | Summary | Evidence / Notes | Recommendation |
| --- | --- | --- | --- | --- |
| 1 | **High** → Resolved | STAC Filter extension was accepted but never propagated to the store. `SearchInternalAsync` now copies the JSON filter and language into `StacSearchParameters`, so both GET and POST searches honour CQL2 payloads (`src/Honua.Server.Host/Stac/StacSearchController.cs:114-152`, `:329-339`). | Add integration coverage exercising a filtered search once catalog filter support is verified. |
| 2 | **High** → Resolved | STAC and OGC Records discovery routes required `RequireViewer`. The STAC GET/POST endpoints now allow anonymous access, and the records router no longer enforces authorization at the group level (`src/Honua.Server.Host/Stac/StacCatalogController.cs:49`, `src/Honua.Server.Host/Stac/StacCollectionsController.cs:67`, `src/Honua.Server.Host/Stac/StacSearchController.cs:72`, `src/Honua.Server.Host/Records/RecordsEndpointExtensions.cs:28`). | Consider service-level configuration if certain deployments still need to gate discovery, but defaults now match the specs. |
| 3 | **Medium** → Resolved | GET `/stac/search` ignored `filter`, `filter-lang`, and `intersects`. Query parsing now accepts those parameters, validates JSON, and reuses the POST geometry parser so both verbs share the same surface area (`src/Honua.Server.Host/Stac/StacSearchController.cs:99-152`). | Add tests that round-trip a GET filter/intersects query to prevent regressions. |
| 4 | **Medium** → Resolved | STAC collection pagination now clamps limits on both the controller and service paths (`src/Honua.Server.Host/Stac/StacCollectionsController.cs:64`, `src/Honua.Server.Host/Stac/Services/StacReadService.cs:50`). This prevents oversized pages from hammering the store. | Monitor telemetry for unusually high requested limits to tune the clamp if needed. |
| 5 | **Medium** → Resolved | OGC Records landing, conformance, and API description endpoints are now public because the router no longer applies `RequireViewer` at the group level (`src/Honua.Server.Host/Records/RecordsEndpointExtensions.cs:26-33`). | N/A |

---

## Suggested Follow-Ups

1. Add regression tests for filtered/intersects searches (GET + POST) to ensure the parameters stay wired through future refactors.
2. Capture lightweight smoke tests for anonymous access to `/stac` and `/records` routes.
3. Expand integration coverage around large collection pagination to validate clamping with real store implementations.

---

## Tests / Verification

Documentation-only review; no automated tests were executed during this pass.
