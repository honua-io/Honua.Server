Testing Modernization Plan
==========================

Last Updated: 2025-09-17

Overview
--------
We are rebuilding the Honua test suite using a test-driven approach focused on high-value protocol scenarios. Existing legacy tests are being phased out in favor of new integration-driven coverage and automated conformance runs via official OGC TEAM Engine Docker containers.

Current Objectives
------------------
1. Evaluate the current metadata domain model (services/layers) against both Esri catalog and OGC requirements; capture gaps.
2. Seed a fresh integration harness with canonical OGC data (CITE fixtures) applied through a public metadata API surface.
3. Add minimal, meaningful integration tests that drive the API-first contract (metadata import/export, service + layer inspection).
4. Incrementally introduce WMS GetMap, WFS/GeoJSON, KML export validations backed by schema checks and canonical datasets.
5. Automate official TEAM Engine suites (OGC API, WMS 1.3, WFS 2.0, etc.) via Testcontainers for CI gating.

Working Notes
-------------
- Need a comparison matrix mapping Esri catalog fields (service metadata, layer descriptors, symbology, security) to OGC capability requirements to determine missing attributes in `Service`/`Layer` models.
- Canonical test data comes from OGC ETS repositories (`ets-wms13` shapefiles, `ets-ogcapi-features10` JSON collections).
- PyQGIS smoke coverage lives under `tests/qgis`; these tests load WMS/GeoJSON layers through QGIS to exercise real client behaviour against a running Honua instance (`scripts/run-qgis-smoke.sh` launches Honua, bootstraps auth, and executes the suite).
- Tile JSON and WMTS tile fetches (PNG + JPEG, WebMercator + CRS84) are validated to ensure raster cache responses remain healthy (including ETag reuse across repeated requests).
- `SeedDataHelper` currently reads shapefiles -> GeoJSON for assertions; it will be supplanted by API-driven seeding once the endpoint exists.
- Postgres provider integration harness lives in `tests/Honua.Server.Core.Tests/Data/Postgres`; SQLite parity lives under `.../Data/Sqlite`. Both leverage shared metadata fixtures to keep CRUD parity.
- Legacy metadata configuration now targets `folder: CITE` / `service: cite` with canonical layer names to mimic TEAM Engine fixtures.
- `HonuaCoreTest/Honua.OGC/Honua.OGC.csproj` references `NetTopologySuite.IO.ShapeFile` to inspect shapefile fixtures inside tests.
- Ingestion integration tests (`Import/DataIngestionServiceTests`) validate the GDAL pipeline end-to-end; run via `dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter DataIngestionServiceTests` when iterating.

Immediate Next Steps
--------------------
- Draft the Esri vs. OGC metadata field comparison and highlight changes required in `Service`/`Layer` (e.g., format capabilities, CRS lists, attribution, style refs).
- Add WMTS/tile cache regression checks for additional tilesets once new datasets land.
- Design a lightweight metadata import/export endpoint (e.g., `/admin/metadata`) with Swagger-visible contracts, auth hooks, versioning, and dry-run support so developers and headless integrators can manage Honua configuration.
- Add automated smoke coverage for format negotiation (`?f=geojson`, `?f=kml`, `?f=kmz`, `?f=geopackage`, `?f=mvt`) once serializers are wired so regressions surface in CI.
- Author the first TDD integration test that POSTs the metadata payload (using the API contract) and verifies `/ogc/cite/collections` reflects the seeded layers.
- Refactor `SpatialMetadataProvisioner` to call the new endpoint so tests and runtime deployments share the same seeding path.
- Draft quick-start docs/snippets covering Swagger usage, CLI examples, and Testcontainers workflows for headless integrators.
- Expand ingestion coverage to include failure modes (unsupported formats, schema mismatch) and CLI-facing smoke tests once mocks for the control-plane client are ready.

Esri ↔ OGC Metadata Matrix (Draft)
----------------------------------
| Geoservices REST a.k.a. Esri REST Field        | Honua Metadata Source                                | OGC API Expectation                               | Gap / Notes |
|------------------------|------------------------------------------------------|----------------------------------------------------|-------------|
| `serviceDescription`   | `ServiceDefinition.Catalog.Summary`                  | `collections/{id}.description`                     | Align names; ensure summary propagates consistently to OGC responses and HTML catalog. |
| `currentVersion`       | Hard-coded in controllers (`10.81`)                  | Not required by OGC; landing page advertises conformance classes | Track version in configuration so docs stay honest when we bump Esri parity. |
| `capabilities`         | Not explicitly modeled; implied by enabled endpoints | `conformance` list + `collections/{id}.links`      | Add explicit service capability list so both Esri and OGC outputs reflect query/export support. |
| `maxRecordCount`       | `LayerDefinition.Query.MaxRecordCount`               | `service.Ogc.ItemLimit` (collection `itemType`)    | Ensure layer override wins; expose negotiated limit in both Esri JSON and OGC pagination links. |
| `supportedQueryFormats`| Derived in controllers (`JSON,geoJSON,KML…`)         | Advertised via `links`/`mediaTypes`                | Drive from metadata so new exporters automatically appear in both surfaces. |
| `drawingInfo`          | Not captured today                                   | N/A (OGC has styles via maps/tiles extensions)     | Record optional renderer/style metadata to unblock future symbology tests. |
| `hasVersionedData`     | Absent                                               | N/A                                                | Decide whether to expose as metadata flag or drop for MVP parity messaging. |
| `extent`               | `LayerDefinition.Catalog.SpatialExtent`              | `collections/{id}.extent.spatial`                  | OGC already consumes; confirm coordinate order and CRS list stay in sync with Esri envelope payload. |

Admin Metadata Endpoint Contract (Sketch)
-----------------------------------------
- `GET /admin/metadata`: returns the active `honua` configuration payload plus ETag/version metadata for optimistic concurrency.
- `POST /admin/metadata/dry-run`: accepts a JSON payload, validates against schema, and returns a diff summary (`added`, `changed`, `removed`) without mutating runtime state.
- `POST /admin/metadata/apply`: same payload as dry-run; requires `If-Match` header for optimistic locking, persists via provider, and triggers registry reload.
- `POST /admin/metadata/import`: multipart upload that stores the payload as a new snapshot and optionally activates it (feature-flagged for MVP).
- `GET /admin/metadata/history`: paginated list of stored snapshots with labels, timestamps, author, and checksum for audit trails.
- All routes require admin-scoped auth, emit structured events (`metadata.apply.requested`, `metadata.apply.completed`), and share a common `ProblemDetails` envelope for validation failures.

Parking Lot / Future Ideas
--------------------------
- Add XML schema validation for WMS/WFS responses once the core API is green.
- Build regression snapshots (GeoJSON/KML) for key endpoints to spot diffs automatically.
- Script TEAM Engine container orchestration (ports, suite IDs, credentials) so developers can run conformance checks locally.
