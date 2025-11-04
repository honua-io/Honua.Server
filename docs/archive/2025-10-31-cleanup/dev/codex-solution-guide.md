# Developer Orientation Guide

_Updated: 2025-09-18_

This document gives new contributors (and assistants such as Codex) the fastest path to understanding the Honua.Next codebase. It summarises the solution layout, key subsystems, authentication story, CLI tooling, and expected development workflows.

## Solution Topology

```
Honua.Next.sln
├── src/
│   ├── Honua.Cli/                 # Operator CLI (bootstrap, metadata, sandbox helpers)
│   ├── Honua.Server.Core/         # Provider-agnostic domain services, auth abstractions
│   └── Honua.Server.Host/         # ASP.NET Core host exposing OGC, Esri REST, OData
├── tests/
│   ├── Honua.Cli.Tests/           # CLI command coverage
│   └── Honua.Server.Core.Tests/   # Unit & integration tests (WebApplicationFactory)
├── tools/                         # Data seeder and conformance helpers
├── docs/                          # Developer and user documentation
└── samples/                       # Reference metadata and dataset fixtures
```

### Project Highlights

- **Honua.Server.Core** – configuration loading, metadata registry, feature repository, export helpers, and the full authentication stack (repositories, hashing, local login service).
- **Honua.Server.Host** – ASP.NET Core minimal-host wiring, endpoint groups (`/ogc`, `/rest`, `/odata`, `/admin`), health checks, and observability surface.
- **Honua.Cli** – Spectre.Console-based CLI that drives bootstrap, metadata snapshotting, sandbox orchestration, and user management.
- **tests/Honua.Server.Core.Tests** – xUnit tests that exercise both unit-level components and HTTP integrations via `WebApplicationFactory<Program>`.

## Key Subsystems

### Metadata & Providers

- Metadata is stored as JSON (see `samples/metadata/`). `JsonMetadataProvider` parses files into strongly typed `ServiceDefinition`/`LayerDefinition` models.
- `HonuaConfigurationService` caches the active configuration and provides a change token for hosted services.
- `FeatureRepository` orchestrates read queries against providers (SQLite/Postgres/SQL Server). Providers live under `src/Honua.Server.Core/Data/*` and implement `IDataStoreProvider`.
- Providers reuse pooled connections: Postgres/MySQL create/lazy-cache `*DataSource` instances keyed by normalized connection strings, while SQL Server/SQLite normalise strings so the built-in pools coalesce connections. Remember to set pool sizing or `Pooling=false` in metadata if an environment has special requirements.
- CRS reprojection is handled by GDAL via `CrsTransform`, which caches coordinate system handles and feeds SQLite/SQL Server responses (and SQL Server bbox filters) with correctly transformed GeoJSON/WKT when clients request alternate `crs` or `outSR` values.
- OGC API handlers honour the `Accept-Crs` header (falling back to the `crs` query parameter), and the Esri REST translator now respects `outSR`/`Accept-Crs` preferences when building `FeatureQuery` instances.

### Authentication

- Options are configured under `honua:authentication` (see `src/Honua.Server.Core/Configuration/HonuaAuthenticationOptions.cs`). Supported modes:
  - `QuickStart` – anonymous read-only, intended only for demos.
  - `Local` – Honua-managed users stored in SQLite (default) or another relational backend.
  - `Oidc` – delegated JWT validation against an external identity provider (MVP hooks exist; feature currently focused on Local mode).
- Local mode components:
  - `SqliteAuthRepository` (`src/Honua.Server.Core/Data/Auth/SqliteAuthRepository.cs`) manages users, roles, bootstrap state, and lockouts.
  - `PasswordHasher` (`Authentication/PasswordHasher.cs`) performs Argon2id hashing with PBKDF2 fallback.
  - `LocalAuthenticationService` issues JWTs, tracks failed attempts, and enforces lockouts.
  - `/api/auth/local/login` endpoint (`src/Honua.Server.Host/Authentication/LocalAuthController.cs`) is the HTTP entry point.
- Administrative surfaces (`/admin/metadata`, `/metrics`, metadata HTML views, etc.) require role-based policies. QuickStart mode is intentionally read-only.

See `docs/authentication/bootstrap.md` for operator instructions and CLI usage.

### CLI Overview

`Honua.Cli` orchestrates bootstrap and operational workflows. Core commands:

- `honua auth bootstrap` – seeds the authentication store based on `appsettings`.
- `honua auth create-user` – provisions local users, generates passwords, and assigns roles.
- `honua metadata snapshot|snapshots|restore|validate` – manage metadata safety snapshots.
- `honua config init` – stores the default host/token under `~/.config/Honua/config.json` (or `$HONUA_HOME`).
- `honua status` – checks `/healthz/ready` and an authenticated admin probe using the stored defaults.
- `honua sandbox up` – launches the local Docker sandbox (Postgres + Honua host).
- `honua data ingest` – uploads GeoPackage/GeoJSON/zipped Shapefile datasets to the control plane, polls progress, and forwards cancellation when interrupted.
- `honua data jobs|status|cancel` – lists ingestion jobs, inspects a single job, or cancels a running import without crafting raw HTTP requests.

The CLI shares service registrations with `Honua.Server.Core`, so changes to repositories/auth services are automatically available.

### Data Ingestion

- Hosted background pipeline (`DataIngestionService`) lives in `src/Honua.Server.Core/Import/`. It configures GDAL through `MaxRev.Gdal.Core`, opens uploaded datasets with OGR, and streams features into the layer’s provider (`IDataStoreProvider.CreateAsync`). Job lifecycle is exposed via `DataIngestionJobSnapshot`.
- Control plane endpoints under `/admin/ingestion/jobs` (see `src/Honua.Server.Host/Admin/DataIngestionEndpointRouteBuilderExtensions.cs`) accept multipart uploads, list jobs, and support cancellation. Access requires the `RequireDataPublisher` policy.
- CLI commands handle uploads and lifecycle management. Provide `--host`/`--token` to override defaults captured via `honua config init`. Use `honua data jobs`, `honua data status <id>`, and `honua data cancel <id>` to manage existing jobs.
- Temporary uploads land under `/tmp/honua-ingest/<job-id>` and are deleted best-effort after a job finishes.
- MVP supports GeoPackage, GeoJSON, and zipped Shapefiles. The `design/phases/mvp/data-ingestion.md` doc captures current behaviour and backlog.

### Host Endpoints

- **OGC API Features**: `/ogc/...` implemented in `Ogc/OgcHandlers.cs` and mapped in `Ogc/OgcApiEndpointExtensions.cs`.
- **Esri REST**: `/rest/services/...` controllers under `GeoservicesREST/` provide FeatureServer/MapServer compatibility.
- **OData**: `DynamicODataController` emits an EDM model derived from metadata.
- **Admin APIs**: `/admin/metadata` handles diff/apply/snapshot operations and is guarded by the `RequireAdministrator` policy.
- **Ingestion APIs**: `/admin/ingestion/jobs` create/list/cancel ingestion jobs and surface job snapshots. Always use the CLI or an authenticated REST client; direct calls must include operator JWTs.
- **Health & Metrics**: `/healthz/*` and `/metrics` configured in `Hosting/HonuaHostConfigurationExtensions.cs`.

## Development Workflow

### Environment Setup

1. Install .NET 9 SDK.
2. Restore dependencies: `dotnet restore` (run at repo root).
3. Optional: seed development metadata using `dotnet run --project tools/DataSeeder`.

### Running the Host

```bash
cd src/Honua.Server.Host
dotnet run
```

Configure the metadata path and authentication mode via `appsettings.Development.json` or environment variables (e.g., `Honua__Authentication__Mode=Local`).

### Testing

- **Full suite**: `dotnet test`
- **Server tests**: `dotnet test tests/Honua.Server.Core.Tests`
- **CLI tests**: `dotnet test tests/Honua.Cli.Tests`

Integration tests spin up lightweight web hosts against temporary metadata/auth stores. Database-specific fixtures use Testcontainers (Postgres) or local SQLite files. When adding new endpoints, mirror them in the corresponding `Hosting/` test class and update the fixture bootstrap logic.

### Linting & Formatting

- Use `dotnet format` before committing substantive C# changes.
- JSON/YAML samples should remain human-readable (two-space indent).

## What Changed Recently

- **Authentication Hardening**: QuickStart mode is locked down; Local/OIDC mode must be enabled for administrative changes. Local auth now includes Argon2id hashing, account lockouts, and a CLI-driven bootstrap/login flow.
- **CLI Enhancements**: User creation command added (`honua auth create-user`), enabling operators to rotate credentials without manual database edits.
- **Endpoint Protection**: OGC, WFS, Esri REST, and Catalog endpoints now require `RequireViewer`; tests were updated to authenticate before exercising protected routes.
- **Raster Observability**: Cache hits/misses, render latency, preseed job outcomes, and purge events now emit OpenTelemetry metrics (`honua.raster.*`) that surface in the Prometheus exporter.

If you encounter 401/403 responses in tests or manual runs, ensure you:

1. Bootstrap the auth store (`honua auth bootstrap`).
2. Create a user with the necessary role.
3. Obtain a JWT from `/api/auth/local/login` and include it in the `Authorization: Bearer` header.

## Useful References

- **Authentication**: `docs/authentication/bootstrap.md`
- **Testing Strategy**: `docs/dev/testing-plan.md`
- **Feature Status**: `docs/dev/features/active-feature.md`
- **Runbooks**: `docs/dev/runbooks/` (observability, conformance suites, architectural reviews)
- **Architecture Deep Dive**: `docs/dev/developer-architecture-guide.md`
- **Operator How-To**: `docs/user/data-ingestion.md`

## Quick Checklist for New Tasks

1. Update `docs/dev/current-task.md` with objective, touched files, and next steps.
2. If the task belongs to a tracked feature, link to its log under `docs/dev/features/`.
3. Run `dotnet build` and relevant `dotnet test` targets before committing.
4. Update user/operator docs when behaviour changes (especially authentication, metadata admin, or CLI workflows).
5. Keep tests in sync—new endpoints should have coverage in `tests/Honua.Server.Core.Tests/Hosting`.

This guide should give you enough context to navigate the codebase confidently. When in doubt, search with `rg`, inspect project files with `fd`, and open related docs under `docs/dev` for deeper background.
