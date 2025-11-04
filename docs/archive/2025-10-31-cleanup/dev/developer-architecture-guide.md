# Developer Architecture & Code Flow Guide

_Updated: 2025-09-18_

This guide explains how the Honua.Next codebase fits together: startup flow, dependency injection, request handling, background services, CLI/assistant interactions, and testing patterns. Use it alongside the orientation guide (`docs/dev/codex-solution-guide.md`) for quick navigation.

## Runtime Bootstrapping

### 1. Entry Point (`src/Honua.Server.Host/Program.cs`)
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.ConfigureHonuaServices();
var app = builder.Build();
app.ConfigureHonuaRequestPipeline();
app.Run();
```

### 2. Service Registration (`ConfigureHonuaServices` → `AddHonuaCore`)
- `AddHonuaCore(IConfiguration, basePath)` loads `honua` configuration, normalizes paths, and registers core singletons.
- Key registrations:
  - **Configuration & Metadata**: `HonuaConfigurationService`, `MetadataRegistry`, `JsonMetadataProvider`.
  - **Data Providers**: keyed `IDataStoreProvider` implementations (SQLite, Postgres, SQL Server, MySQL) + `FeatureRepository`.
  - **Authentication**: `SqliteAuthRepository`, `PasswordHasher`, `LocalAuthenticationService`, JWT signing key provider.
  - **Export/Import**: `GeoPackageExporter`, `ShapefileExporter`, `DataIngestionService` (hosted worker).
  - **Background Initialization**: `MetadataInitializationHostedService`, `AuthInitializationHostedService`.
  - **Observability Helpers**: memory cache, schema validators, etc.
- Host-level additions:
  - `AddAuthentication().AddJwtBearer()` + `AddAuthorization()` (policies `RequireAdministrator`, `RequireDataPublisher`, `RequireViewer`).
  - Controllers, Razor Pages, OData setup (if `honua:odata:enabled`).
  - Health checks, OpenTelemetry metrics exporter, logging configuration.

### 3. Request Pipeline (`ConfigureHonuaRequestPipeline`)
- Error handling (DeveloperExceptionPage vs ExceptionHandler).
- JWT auth/authorization middleware (skipped in QuickStart mode).
- `app.MapControllers()` (Esri REST, authentication endpoints, etc.) and `app.MapRazorPages()` (metadata UI).
- Endpoint mappers:
  - `app.MapOgcApi()` – OGC API Features handlers.
  - `app.MapWfs()` – conditionally registers WFS endpoints.
  - `app.MapMetadataAdministration()` – metadata diff/apply/snapshot REST surface.
  - `app.MapDataIngestionAdministration()` – ingestion control plane (multipart uploads → GDAL pipeline).
- Health checks (`/healthz/startup|live|ready`), metrics endpoint `/metrics` (Prometheus exporter), root redirect `/ → /ogc`.

## Core Data & Metadata Flow

### Metadata Registry
- `MetadataRegistry` holds the active `MetadataSnapshot` (catalog, services, layers, data sources, styles).
- Provides async initialization and reload. Consumers either query `Snapshot` directly or use `GetSnapshotAsync` with cancellation.
- Change tokens trigger OData model rebuild, etc.

### Feature Repository
- `FeatureRepository` orchestrates read/write operations; resolves context through `IFeatureContextResolver` (ensures service/layer + provider lookup).
- Providers (`SqliteDataStoreProvider`, `PostgresDataStoreProvider`, `SqlServerDataStoreProvider`, `MySqlDataStoreProvider`) translate `FeatureQuery` objects to SQL, handle geometry conversions, enforce SRID conversions, and perform CRUD operations.
- Each provider now maintains a normalized connection-string cache so pooled data sources stay singleton-per-DS across requests (Postgres/MySQL) or at least reuse the exact connection string the ADO.NET pools expect (SQL Server/SQLite). This keeps pool utilisation bounded, preserves custom timeouts, and sets a consistent `ApplicationName` for diagnostics.
- CRS transformations go through GDAL-backed helpers (`src/Honua.Server.Core/Data/CrsTransform.cs`), so envelope filters and providers that emit WKT/GeoJSON from .NET (SQL Server, SQLite) honour requested `crs`/`outSR` values without bespoke math per provider.
- Repository ensures filters/sorts have metadata-based entity definitions via `MetadataQueryModelBuilder`.

### Exporters / Importers
- Exporters stream to temp files with `DeleteOnClose` semantics (GeoPackage, Shapefile) to prevent temp-file buildup.
- `DataIngestionService` (hosted worker) uses GDAL/OGR to read uploaded datasets and pushes rows through provider `CreateAsync`. Jobs track status, progress, messages; cancellation uses linked tokens.

## Authentication & Security
- Authentication modes configured via `HonuaAuthenticationOptions`:
  - **QuickStart**: unauthenticated, read-only demo surface.
  - **Local**: local user store (SQLite by default) with Argon2 hashing, lockouts, CLI bootstrap.
  - **Oidc**: external IdP (JWT validation, role mapping from claims).
- Administrative APIs require role policies; CLI interacts through HTTP and must supply tokens in secured environments.
- Signing keys managed by `LocalSigningKeyProvider` (exclusive file locks, 0600 permissions on Unix).
- CLI environment ensures config/log directories have restricted permissions.

## Control Plane & CLI Flow

### Control-Plane APIs
- `/admin/metadata/*`: diff/apply snapshots, reload registry, stream metadata payloads. Uses `MetadataAdministrationEndpointRouteBuilderExtensions`.
- `/admin/ingestion/jobs`: create/list/get/cancel ingestion jobs. Multipart uploads persisted to `/tmp/honua-ingest/<job-id>`.
- `/api/auth/local/*`: login, bootstrap surfaces for local auth.

### CLI (`Honua.Cli`)
- Spectre.Console CLI registers top-level commands/branches for `assistant`, `status`, `config`, `metadata`, `auth`, `sandbox`, and `data` workflows.
- DI inside CLI hosts reuses server core services (`SqliteAuthRepository`, `MetadataSchemaValidator`, etc.) but operations ultimately go through control-plane APIs.
- `honua config init` persists defaults (host/token) under the CLI config root; `honua status` reuses those values to hit `/healthz/ready` and an authenticated metadata probe.
- `honua data ingest` uploads datasets via multipart, polls job snapshots, and handles Ctrl+C cancellation; companion verbs (`honua data jobs|status|cancel`) surface job lifecycle management without manual HTTP.
- `DataIngestionApiClient` uses named `HttpClient` (`honua-control-plane`), attaches bearer tokens when provided, and guards responses.
- Assistant/plan framework (documented in `design/phases/mvp/natural-language-cli-assistant.md`) outlines future skill integration but is currently plan-only.

## Observability Patterns
- Logging pipeline: JSON/simple console + scopes; sanitization for CLI session logs.
- Metrics pipeline: OpenTelemetry instrumentation with optional Prometheus scraping; metrics endpoint requires viewer role when auth enabled.
- Health checks tag-based (`startup`, `ready`, `live`) for component-specific status.
- Future metrics insight skill (post-MVP) will layer on top of `/metrics` with curated summaries (see `design/phases/post-mvp/metrics-insights-assistant.md`).
- CRS reprojection publishes `honua_crs_*` counters/histograms (transform counts, cache hits/misses, duration) and exposes a readiness health check that exercises a 4326↔3857 round trip.

## Testing Strategy

### Server Tests (`tests/Honua.Server.Core.Tests`)
- Use `WebApplicationFactory<Program>` to spin up host per suite (OData, OGC, WFS, Esri, Metadata Admin, Observability).
- Replace providers or register test doubles via `ConfigureTestServices` (e.g., `services.RemoveAll<IFeatureRepository>()` then add stub).
- Authentication: tests acquire JWTs via local auth endpoints or stub out policies when focusing on unauthenticated behavior.
- New ingestion tests create temporary GeoJSON datasets, enqueue jobs, wait for completion, and verify provider writes.

### CLI Tests (`tests/Honua.Cli.Tests`)
- Focus on command parsing/argument validation; limited integration; expect future expansion once control-plane mocks are available.

### Unit Tests
- Cover authentication services (`JwtBearerOptionsConfigurator`, token service), configuration loaders, metadata validators, query builders.

## Extensibility Guide

### Adding a New API Surface
1. Implement controller/handler under `src/Honua.Server.Host/{Area}/`.
2. Register endpoints in `ConfigureHonuaRequestPipeline` (map group, apply policies).
3. Provide service registration (if new services needed) via `AddHonuaCore` or host-level DI.
4. Write tests with `WebApplicationFactory` verifying auth, responses, error cases.
5. Update docs (orientation + this guide) if it’s a new capability.

### Adding a Data Provider
1. Implement `IDataStoreProvider` (see SQLite/Postgres/SQL Server for patterns).
2. Register via `AddKeyedSingleton<IDataStoreProvider>`.
3. Extend metadata + configuration docs (how to reference the provider).
4. Add integration tests to cover CRUD with the provider (using test containers or local DB).

### Extending CLI
1. Implement Spectre `AsyncCommand<TSettings>`.
2. Register in `Program.cs` under the appropriate branch.
3. Use `HttpClientFactory` if calling control-plane APIs; prefer dependency injection over direct instantiation.
4. Add tests for settings validation and error handling.

### Background Services
- To add a hosted worker, register it in `AddHonuaCore`; ensure cancellation tokens are respected and errors are surfaced via logging.
- Provide control-plane endpoints if operators need visibility/cancellation.

## Reference Map

| Area | Key Files |
|------|-----------|
| Host bootstrap | `src/Honua.Server.Host/Program.cs`, `Hosting/HonuaHostConfigurationExtensions.cs` |
| Core DI & services | `src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs` |
| Metadata & providers | `src/Honua.Server.Core/Metadata/*`, `Data/*` |
| Authentication | `src/Honua.Server.Core/Authentication/*`, `src/Honua.Server.Host/Authentication` |
| Ingestion | `src/Honua.Server.Core/Import/*`, `src/Honua.Server.Host/Admin/DataIngestionEndpointRouteBuilderExtensions.cs` |
| CLI | `src/Honua.Cli/Program.cs`, `src/Honua.Cli/Commands/*`, `src/Honua.Cli/Services/*` |
| Observability | `src/Honua.Server.Host/Observability`, `docs/dev/runbooks/` |
| Tests | `tests/Honua.Server.Core.Tests/*`, `tests/Honua.Cli.Tests/*` |

Keep this guide up to date as new services land (metrics insights, provisioning, enterprise features). When touching core flows, add a brief note here so the next developer understands the pattern.
