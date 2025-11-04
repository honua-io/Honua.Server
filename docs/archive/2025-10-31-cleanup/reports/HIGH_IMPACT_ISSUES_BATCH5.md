# High-Impact Issues – Batch 5

1. **Tile datetime validation throws 500 instead of 400**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs:398`  
   - **Problem:** `ValidateTemporalParameter` raises `InvalidOperationException` when a caller supplies an out-of-range `datetime`, and the exception bubbles straight to the middleware.  
   - **Impact:** A trivial query parameter typo turns the request into a 500, polluting telemetry and making the tile endpoint appear unstable.  
   - **Recommendation:** Catch the validation failure and return `Results.Problem(..., statusCode: 400)` so bad input does not masquerade as a server fault.

2. **Raster tile cache provider never used**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs:322-335`  
   - **Problem:** The handler receives `IRasterTileCacheProvider`/`IRasterTileCacheMetrics` but immediately discards them (`_ = tileCacheProvider`).  
   - **Impact:** Every raster tile request bypasses the cache, forcing expensive re-rendering and nullifying CDN hints.  
   - **Recommendation:** Inject the cache provider into `GetCollectionTile` and short‑circuit with cache hits before calling the renderer.

3. **Tile range calculator breaks for dateline-crossing extents**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTileMatrixHelper.cs:157-185`  
   - **Problem:** `GetTileRange` simply swaps `min/max` when `minColumn > maxColumn`, collapsing wraparound extents into a huge contiguous range.  
   - **Impact:** Collections wrapping ±180° request almost every tile on Earth, causing runaway rendering and cache churn.  
   - **Recommendation:** Split antimeridian ranges into two segments instead of swapping and recomputing indices.

4. **Zoom range fallback caps data at z14**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTileMatrixHelper.cs:27-57`  
   - **Problem:** When dataset metadata omits `zoomLevels`, `ResolveZoomRange` hardcodes `(0, 14)` even if cached tiles exist above z14.  
   - **Impact:** High-resolution caches (z15–z22) are never served, so vector/raster tiles look blurry when zooming in.  
   - **Recommendation:** Inspect dataset cache metadata (`dataset.Cache.ZoomLevels`/`MaxZoom`) before falling back to 14, or allow configuration.

5. **Temporal validation compares strings lexicographically**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs:642-664`  
   - **Problem:** Validation uses `string.CompareOrdinal` against min/max bounds, so values like `2024-9-5` incorrectly pass while `2024-09-05Z` fails.  
   - **Impact:** Incorrect tiles are accepted or rejected depending on formatting quirks, confusing clients with intermittent 400s.  
   - **Recommendation:** Parse values with `DateTimeOffset.TryParse` and compare actual timestamps.

6. **PMTiles export crashes for providers without MVT support**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcTilesHandlers.cs:403-425`  
   - **Problem:** The handler assumes `GenerateMvtTileAsync` returns a byte array; when a data provider returns `null`, `Guard.NotNull` throws.  
   - **Impact:** MySQL/SQL Server datasets offering `format=pmtiles` always produce 500 responses.  
   - **Recommendation:** Detect providers that do not support vector tiles and return a 501/400 explaining the limitation (or fall back to raster).

7. **Vector tile route fails on null payloads**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2278-2279`  
   - **Problem:** `RenderVectorTileAsync` writes `Results.File(mvtBytes, ...)` without null checks; providers returning `null` trigger `ArgumentNullException`.  
   - **Impact:** `/ogc/collections/{id}/tiles` crashes for every provider lacking native MVT generation.  
   - **Recommendation:** Short‑circuit with a 501 or convert the features to GeoJSON+tippecanoe fallback when MVT is unavailable.

8. **TileMatrixSet links ignore dataset-defined matrices**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2033-2052`  
   - **Problem:** The API hardcodes CRS84/WebMercator links and never surfaces custom tile matrix sets defined on the dataset.  
   - **Impact:** Clients cannot discover region-specific tiling schemes, leading to 404s when requesting advertised tiles.  
   - **Recommendation:** Inspect `dataset.Cache.TileMatrixSets` (or equivalent metadata) and add matching links in addition to the global defaults.

9. **OData writes bypass feature edit orchestrator**  
   - **Location:** `src/Honua.Server.Host/OData/DynamicODataController.cs:371-392`  
   - **Problem:** The controller calls `_repository.CreateAsync` directly, skipping validation, auditing, and attachment orchestration performed by `IFeatureEditOrchestrator`.  
   - **Impact:** OData routes can persist invalid schemas, miss audit trails, and break attachment lifecycles.  
   - **Recommendation:** Delegate all write paths to the edit orchestrator just like the Esri GeoServices endpoints.

10. **Repository transactions never used in OData writes**  
    - **Location:** `src/Honua.Server.Host/OData/DynamicODataController.cs:386-391`  
    - **Problem:** Every call passes `transaction: null`, so multi-record edits cannot enlist in a wider transaction.  
    - **Impact:** Complex workflows (edits + attachments) cannot roll back on failure, leaving partial data.  
    - **Recommendation:** Accept optional transaction handles from callers (or create scope per request) and flow them into repository calls.

11. **No concurrency/ETag enforcement on OData updates**  
    - **Location:** `src/Honua.Server.Host/OData/DynamicODataController.cs:456-475`  
    - **Problem:** PUT/PATCH ignore `If-Match` or version fields; the last writer wins silently.  
    - **Impact:** Data loss when two editors update the same feature via OData, without any 412 safeguard.  
    - **Recommendation:** Surface concurrency tokens via OData metadata and reject stale updates unless clients supply the current ETag.

12. **Layer editing configuration ignored by OData**  
    - **Location:** `src/Honua.Server.Host/OData/DynamicODataController.cs:366-369`  
    - **Problem:** The controller only checks the global OData option `AllowWrites`, ignoring per-layer `LayerEditingDefinition`.  
    - **Impact:** Read-only layers can still be edited through OData while GeoServices properly blocks them.  
    - **Recommendation:** Validate `metadata.Layer.Editing.Capabilities` before permitting POST/PUT/PATCH/DELETE.

13. **Global-ID-required layers accept anonymous OData edits**  
    - **Location:** `src/Honua.Server.Host/OData/DynamicODataController.cs:375-389`  
    - **Problem:** Layers that need `globalId` (for attachments or sync) are not checked; clients can insert rows without the required GUID.  
    - **Impact:** Downstream attachment workflows and replica syncs fail because mandatory `globalId` values are missing.  
    - **Recommendation:** Mirror the GeoServices logic—reject writes when `metadata.Layer.Attachments.RequireGlobalIds` is true and the property is absent/mismatched.

14. **MySQL provider advertises PMTiles but returns null tiles**  
    - **Location:** `src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:604-610`  
    - **Problem:** `GenerateMvtTileAsync` returns `null`, so vector tile exports (MVT/PMTiles) always fail for MySQL-backed layers.  
    - **Impact:** Any tile request for MySQL datasets collapses with 500/501 errors.  
    - **Recommendation:** Either implement real MVT generation or explicitly disable vector tile/PMTiles routes for MySQL layers.

15. **SQL Server provider silently lacks vector tile support**  
    - **Location:** `src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs:608-616`  
    - **Problem:** The SQL Server provider also returns `null` from `GenerateMvtTileAsync`, triggering the same runtime crash.  
    - **Impact:** Vector tiles cannot be served from SQL Server layers, contradicting documented capabilities.  
    - **Recommendation:** Add a database-side MVT pipeline or disable the tile endpoints for SQL Server-backed services.

16. **MySQL provider can’t participate in orchestrated transactions**  
    - **Location:** `src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:613-618`  
    - **Problem:** `BeginTransactionAsync` always returns `null`, so cross-layer operations cannot ensure atomicity on MySQL.  
    - **Impact:** Multi-step edits (edit + attachment + audit) leave half-written data when any later step fails.  
    - **Recommendation:** Implement `IDataStoreTransaction` for MySQL (mirroring Postgres) and flow it through CRUD calls.

17. **SQL Server provider also skips transaction support**  
    - **Location:** `src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs:631-636`  
    - **Problem:** Transactions are unimplemented, leaving SQL Server in the same partially-complete state as MySQL.  
    - **Impact:** Any orchestrated workflow relying on rollback semantics breaks when a SQL Server layer is involved.  
    - **Recommendation:** Provide an `IDataStoreTransaction` implementation that wraps `SqlTransaction` and pass it through CRUD/bulk operations.

18. **Shapefile exporter blocks on async enumerators**  
    - **Location:** `src/Honua.Server.Core/Export/ShapefileExporter.cs:417-454`  
    - **Problem:** The `IEnumerator` implementation calls `.GetAwaiter().GetResult()` on async enumerators and disposal.  
    - **Impact:** Large shapefile exports tie up ASP.NET threads and risk deadlocks under load.  
    - **Recommendation:** Replace the synchronous iterator with an `IAsyncEnumerable` pipeline or fully buffer outside the request context.

19. **FlatGeobuf exporter also synchronously waits on async sources**  
    - **Location:** `src/Honua.Server.Core/Export/FlatGeobufExporter.cs:660-686`  
    - **Problem:** Similar `.GetAwaiter().GetResult()` usage blocks threads while reading async feature sources.  
    - **Impact:** High-volume FlatGeobuf downloads monopolise the thread pool and hurt throughput.  
    - **Recommendation:** Stream directly via async APIs instead of forcing sync-over-async.

20. **AWS KMS XML encryption blocks threads**  
    - **Location:** `src/Honua.Server.Core/Security/AwsKmsXmlEncryption.cs:77-166`  
    - **Problem:** Encryption/decryption call `.GetAwaiter().GetResult()` on `EncryptAsync`/`DecryptAsync`.  
    - **Impact:** Each encrypt/decrypt ties up a worker thread while waiting on AWS responses, risking starvation under load.  
    - **Recommendation:** Make the encryption service fully async or expose async APIs that the rest of the pipeline can await.

21. **Local signing key loader uses Task.Run + GetResult**  
    - **Location:** `src/Honua.Server.Core/Authentication/LocalSigningKeyProvider.cs:88`  
    - **Problem:** The provider wraps async file IO in `Task.Run(...).GetAwaiter().GetResult()`.  
    - **Impact:** Certificate bootstrapping blocks thread pool threads and can deadlock when called from contexts that already use the synchronization context.  
    - **Recommendation:** Use proper async file APIs and await them from async callers.

22. **git ls-remote command vulnerable to argument injection**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/GitOps/ValidateGitConfigStep.cs:88-117`  
    - **Problem:** The code interpolates `_state.RepoUrl` and branch into a single `Arguments` string.  
    - **Impact:** Malicious repo URLs can inject additional git commands or flags, leaking credentials or corrupting workspaces.  
    - **Recommendation:** Populate `ProcessStartInfo.ArgumentList` per token (or at least quote/escape user-supplied strings).

23. **git clone path also injectable**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/GitOps/ValidateGitConfigStep.cs:316-341`  
    - **Problem:** The clone command uses the same string interpolation for branch name, repo URL, and target directory.  
    - **Impact:** Attackers can trick the CLI into executing arbitrary git options or writing outside the intended folder.  
    - **Recommendation:** Switch to `ArgumentList` and validate/sanitise user-provided paths before invoking git.

24. **AWS traffic switch command splits arguments incorrectly**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:455-512`  
    - **Problem:** `forwardConfig` contains spaces and braces, but it is injected as a bare `--actions` value.  
    - **Impact:** `aws` CLI fails to parse the command, so blue/green switching never executes.  
    - **Recommendation:** Quote the entire `--actions` payload (or use `ArgumentList`) so the CLI receives it as one argument.

25. **AWS traffic switch exposes command-injection surface**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:455-512`  
    - **Problem:** All ARNs and percentages are concatenated into the same argument, allowing users/environment variables to smuggle extra CLI flags.  
    - **Impact:** A compromised environment variable (or user-supplied state) can run arbitrary `aws` commands.  
    - **Recommendation:** Treat each token as a separate argument and validate ARNs before emitting the command.

26. **Azure CLI call does no quoting or validation**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:515-568`  
    - **Problem:** Profile/resource names are inserted directly into a single argument string.  
    - **Impact:** Names with spaces or `;` characters break the command, and hostile values can inject additional `az` flags.  
    - **Recommendation:** Build argument lists and validate environment inputs.

27. **GCP traffic switch leaves temp files on failure**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:585-641`  
    - **Problem:** The method writes config JSON but only deletes it when `Start()` succeeds; exceptions leak sensitive JSON to `/tmp`.  
    - **Impact:** Credentials/project metadata linger on disk if `gcloud` fails to start.  
    - **Recommendation:** Wrap temp-file usage in try/finally and delete the file on all error paths.

28. **Nginx traffic switch overwrites include atomically unsafe**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:652-706`  
    - **Problem:** Configuration is written directly to the target path without backup or atomic rename.  
    - **Impact:** A partial write (disk full, SIGTERM) leaves Nginx config truncated, bringing the ingress down.  
    - **Recommendation:** Write to a temp file and `File.Move` with `overwrite: true`, keeping a backup for rollback.

29. **HAProxy backend rewrite drops custom directives**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:730-772`  
    - **Problem:** The method replaces the entire `backend honua_backend` block with a canned template, discarding health checks/timeouts admins added.  
    - **Impact:** Production HAProxy deployments lose mandatory directives (e.g., TLS, stickiness) after running the CLI.  
    - **Recommendation:** Parse and adjust only server weight lines or expose a safe templating mechanism preserving custom settings.

30. **Placeholder tests give false confidence**  
    - **Location:** `tests/Honua.Server.Core.Tests/Raster/Readers/HttpRangeStreamTests.cs:346` (and similar)  
    - **Problem:** Multiple tests do nothing but `Assert.True(true)` after catching exceptions.  
    - **Impact:** Critical components (range readers, Zarr cache, token revocation) appear covered but actually have zero verification.  
    - **Recommendation:** Replace placeholders with meaningful assertions or delete the tests until proper coverage is available.

31. **Alert receiver service lacks automated tests**  
    - **Location:** `src/Honua.Server.AlertReceiver/*`  
    - **Problem:** The mission-critical alert receiver project has no unit or integration tests validating deduplication, silencing, or SNS/SQS publishing.  
    - **Impact:** Regression bugs in alert handling reach production unnoticed, risking lost incident notifications.  
    - **Recommendation:** Add unit tests for each service and integration tests verifying the full webhook → publisher pipeline.

32. **OgcSharedHandlers enormous god class hampers reliability**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:1`  
    - **Problem:** At ~4,800 lines the class mixes tiles, search, filters, caching, and attachments.  
    - **Impact:** Changes in one protocol frequently break another; the file is untestable in isolation.  
    - **Recommendation:** Split into focused services (tiles, search, attachments) with dedicated unit tests.

33. **DeploymentConfigurationAgent monolith (4k lines)**  
    - **Location:** `src/Honua.Cli.AI/Services/Agents/Specialized/DeploymentConfigurationAgent.cs:1`  
    - **Problem:** The agent bundles cloud providers, templating, validation, and secrets handling into a single mega-class.  
    - **Impact:** Touching any path risks regressions in unrelated providers; the file is practically unreviewable.  
    - **Recommendation:** Break the agent into provider-specific strategy classes with shared abstractions and tests.

34. **Geoservices FeatureServer controller is unmaintainable**  
    - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:1`  
    - **Problem:** >3,500 lines covering metadata, queries, edits, exports, attachments, and sync.  
    - **Impact:** Even small bug fixes require touching massive switch statements, making regressions likely.  
    - **Recommendation:** Extract per-route handlers and wiring similar to minimal APIs, adding unit tests per concern.

35. **Plugin system largely untested**  
    - **Location:** `src/Honua.Cli.AI/Services/Agents/Plugins/*`  
    - **Problem:** Most plugins (security, deployment, compliance) lack any automated tests verifying CLI integration.  
    - **Impact:** Plugin misconfigurations slip through CI, potentially shipping broken automation for customers.  
    - **Recommendation:** Create a plugin test harness that loads each plugin and exercises its primary workflow.

36. **Flaky GitOps tests rely on fixed Task.Delay**  
    - **Location:** `tests/Honua.Server.Core.Tests/GitOps/GitWatcherTests.cs:250`  
    - **Problem:** The tests use `await Task.Delay(2500)` to wait for async polling, leading to heisenbugs on loaded CI agents.  
    - **Impact:** CI intermittently fails or hides races depending on machine speed.  
    - **Recommendation:** Replace sleeps with deterministic signalling (FakeTimeProvider or polling with bounded timeout).

37. **MySQL connection strings never decrypted**  
    - **Location:** `src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:744-760`  
    - **Problem:** `GetOrCreateDataSource` uses `NormalizeConnectionString` on the raw value without calling `DecryptConnectionStringAsync`.  
    - **Impact:** Encrypted connection strings (`encrypted:...`) cannot be used with MySQL providers.  
    - **Recommendation:** Decrypt before caching/normalising, mirroring the Postgres connection manager.

38. **SQL Server connection strings also ignore encryption**  
    - **Location:** `src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs:780-812`  
    - **Problem:** `CreateConnection` builds `SqlConnectionStringBuilder` from the raw connection string, bypassing `_encryptionService`.  
    - **Impact:** Operators cannot store SQL Server credentials securely, forcing plaintext configurations.  
    - **Recommendation:** Call `DecryptConnectionStringAsync` before building the connection string.

39. **Plugin tests missing adversarial coverage**  
    - **Location:** `src/Honua.Cli.AI/Services/Agents/Plugins` & `tests/**`  
    - **Problem:** Security plugins (e.g., secrets rotation) have no tests covering unicode/encoding edge cases.  
    - **Impact:** Input sanitisation bugs can slip into production, undermining defensive guarantees.  
    - **Recommendation:** Add adversarial test vectors (unicode homoglyphs, encoded payloads) for each guard plugin.

40. **GitOps metrics monitoring lacks cancellation respect**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:776-834`  
    - **Problem:** `MonitorMetrics` loops for two minutes without observing a cancellation token; calling rollback mid‑monitor requires killing the process.  
    - **Impact:** Automation cannot abort when metrics go south quickly, delaying failback.  
    - **Recommendation:** Thread the step cancellation token through monitoring checks and break early on cancellation.

41. **HAProxy reload command assumes systemctl exists**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:752-772`  
    - **Problem:** The CLI hardcodes `systemctl reload haproxy`, failing silently on containerised or non-systemd environments.  
    - **Impact:** Traffic weights change in the config file but HAProxy keeps serving old weights.  
    - **Recommendation:** Detect init system or allow an override command per environment.

42. **MySQL `NormalizeRecord` uses ST_GeomFromGeoJSON with magic flag**  
    - **Location:** `src/Honua.Server.Core/Data/MySql/MySqlDataStoreProvider.cs:832-853`  
    - **Problem:** The second argument (`1`) is the geometry dimension flag; for non-collection geometries MySQL expects `NULL`, causing WKB warnings.  
    - **Impact:** Inserts with lines/polygons can coerce to `GeometryCollection`, breaking spatial indexes.  
    - **Recommendation:** Pass `ST_GeomFromGeoJSON(param, srid, NULL)` according to MySQL docs.

43. **SQL Server delete batching forms huge IN clauses**  
    - **Location:** `src/Honua.Server.Core/Data/SqlServer/SqlServerDataStoreProvider.cs:560-592`  
    - **Problem:** Bulk deletes build a single `IN (@id0,@id1,...)` without chunking; large batches exceed SQL Server’s 2,100-parameter limit.  
    - **Impact:** High-volume deletes crash with SQL exceptions and leave the system partially cleaned.  
    - **Recommendation:** Split batches to respect the SQL Server parameter limit or switch to table-valued parameters.

44. **Attachment download helper still uses sync over async**  
    - **Location:** `src/Honua.Server.Host/Attachments/AttachmentDownloadHelper.cs:180`  
    - **Problem:** `ToActionResultAsync(...).GetAwaiter().GetResult()` blocks the calling thread.  
    - **Impact:** Large attachment downloads tie up ASP.NET threads unnecessarily.  
    - **Recommendation:** Make the helper fully async and await the underlying tasks.

45. **Vector tile overlays ignore cancellation**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2130-2175`  
    - **Problem:** Overlay collection loops run without checking `cancellationToken` until each batch finishes.  
    - **Impact:** When a client disconnects, overlay fetch keeps hammering the datastore.  
    - **Recommendation:** Bubble the token into each batch fetch and break early when cancellation is requested.

46. **Azure/GCP command paths leak secrets to console on failure**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:515-640`  
    - **Problem:** The CLI logs full stderr (including secrets) before throwing.  
    - **Impact:** Access tokens printed to pipelines or shared logs.  
    - **Recommendation:** Scrub sensitive values before logging or downgrade to debug-level tracing.

47. **SandboxUpCommand never honours cancellation token**  
    - **Location:** `src/Honua.Cli/Commands/SandboxUpCommand.cs:20-88`  
    - **Problem:** The command ignores `CancellationToken` and passes `CancellationToken.None` to output forwarding.  
    - **Impact:** Cancelling the CLI leaves the docker compose process orphaned.  
    - **Recommendation:** Thread the passed token through output forwarding and kill the process when cancelled.

48. **GitOps switch traffic lacks rollback telemetry**  
    - **Location:** `src/Honua.Cli.AI/Services/Processes/Steps/Upgrade/SwitchTrafficStep.cs:82-122`  
    - **Problem:** If any backend update fails, the step throws without recording which environment succeeded.  
    - **Impact:** Operators cannot tell whether partial switches occurred, complicating manual recovery.  
    - **Recommendation:** Capture step results and emit a structured rollback plan when exceptions occur.

49. **Redis process state store tests rely on real delays**  
    - **Location:** `tests/Honua.Cli.AI.Tests/Processes/RedisProcessStateStoreTests.cs:267`  
    - **Problem:** The test sleeps for 2.5 seconds to wait for expiry instead of using deterministic timing.  
    - **Impact:** Test suite becomes slow and flaky on busy agents.  
    - **Recommendation:** Use controllable clocks or synchronous expiry hooks for deterministic tests.

50. **No automated coverage for CSRF token endpoints**  
    - **Location:** `src/Honua.Server.Host/Security/CsrfTokenEndpoints.cs:24-108`  
    - **Problem:** Despite complex origin validation, there are no unit/integration tests asserting behaviour for valid vs. invalid origins.  
    - **Impact:** Refactors risk silently reopening CSRF token leakage vectors.  
    - **Recommendation:** Add endpoint tests covering happy-path, missing headers, and mismatched origins.

