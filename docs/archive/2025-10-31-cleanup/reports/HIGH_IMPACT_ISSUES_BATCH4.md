# High-Impact Issues – Batch 4

1. **CSRF token endpoint exposed to anonymous callers**  
   - **Location:** `src/Honua.Server.Host/Security/CsrfTokenEndpoints.cs:34`  
   - **Problem:** `/api/security/csrf-token` issues fresh antiforgery tokens to any unauthenticated origin with no origin/referrer validation, so malicious sites can obtain valid tokens simply by calling the endpoint.  
   - **Impact:** Attackers can pair the leaked token with the victim's session cookie to bypass CSRF protections on state-changing routes.  
   - **Recommendation:** Require an authenticated session and enforce same-origin checks (Origin/Referer) before issuing tokens.

2. **ArcGIS token generator lacks brute-force protections**  
   - **Location:** `src/Honua.Server.Host/Authentication/ArcGisTokenEndpoints.cs:30`  
   - **Problem:** The anonymous `/api/tokens/generate` route has no rate limiter or CAPTCHA, allowing unbounded password guessing.
   - **Impact:** Enables online credential stuffing against every local account once deployed.  
   - **Recommendation:** Re-enable ASP.NET rate limiting for the endpoint (or move limiter in front of it) and record audit hits.

3. **Credentials accepted in query string**  
   - **Location:** `src/Honua.Server.Host/Authentication/ArcGisTokenEndpoints.cs:130`  
   - **Problem:** `ParseRequestAsync` pulls `username`/`password` from query parameters when form data is absent. URLs are logged and cached, leaking secrets.
   - **Impact:** Credentials land in server logs, proxies, and browser history.  
   - **Recommendation:** Reject requests unless credentials arrive in the POST body.

4. **Basic auth trusts spoofed HTTPS headers**  
   - **Location:** `src/Honua.Server.Host/Authentication/LocalBasicAuthenticationHandler.cs:136`  
   - **Problem:** `IsHttps` treats any `X-Forwarded-Proto=https` as trustworthy without verifying the sender.  
   - **Impact:** An attacker can send Basic auth over plain HTTP while faking the header, exposing credentials.  
   - **Recommendation:** Route the check through `TrustedProxyValidator` and ignore forwarded headers unless the proxy is trusted.

5. **Forwarded headers enabled without proxy allow-list**  
   - **Location:** `src/Honua.Server.Host/Extensions/WebApplicationExtensions.cs:238`  
   - **Problem:** `UseForwardedHeaders()` is invoked with default options, so every client can spoof `X-Forwarded-*` values.  
   - **Impact:** Host/proto spoofing breaks link generation, TLS detection, and IP-based security.  
   - **Recommendation:** Configure `ForwardedHeadersOptions` with `KnownProxies` / `KnownNetworks` and require `RequireHeaderSymmetry`.

6. **STAC base URLs built from untrusted host header**  
   - **Location:** `src/Honua.Server.Host/Stac/StacRequestHelpers.cs:18`  
   - **Problem:** `BuildBaseUri` concatenates `request.Host` without consulting `TrustedProxyValidator`, so a spoofed `Host` header controls every link.  
   - **Impact:** Open URL redirection, phishing links in API payloads, and broken self links.  
   - **Recommendation:** Resolve the effective host via `RequestLinkHelper` (once proxies are trusted) or inject a canonical API base URL.

7. **POST /stac/search cached and keyed incorrectly**  
   - **Location:** `src/Honua.Server.Host/Stac/StacSearchController.cs:161`  
   - **Problem:** The POST search endpoint is output-cached with the GET policy, but cache keys ignore request body content.  
   - **Impact:** First caller’s body is replayed to later POSTs (wrong results) and POST responses are cached against HTTP semantics.  
   - **Recommendation:** Remove output caching for POST or supply a custom body hash key; GET caching is sufficient.

8. **STAC collections listing leaked across users**  
   - **Location:** `src/Honua.Server.Host/Stac/StacCollectionsController.cs:67`  
   - **Problem:** `[OutputCache]` stores `/stac/collections` responses keyed only by Accept header, ignoring viewer identity.  
   - **Impact:** Users with broader rights prime the cache; restricted users receive their view.  
   - **Recommendation:** Add `SetVaryByClaim` (tenant/roles) or disable caching when authorization scopes differ.

9. **Single collection metadata cache ignores user scope**  
   - **Location:** `src/Honua.Server.Host/Stac/StacCollectionsController.cs:120`  
   - **Problem:** `GetCollection` is cached per route value, so privileged metadata leaks to other viewers.  
   - **Recommendation:** Include authorization context in the cache key or remove caching.

10. **Collection items cache ignores authorization context**  
    - **Location:** `src/Honua.Server.Host/Stac/StacCollectionsController.cs:160`  
    - **Problem:** The cached `/items` responses don’t vary by user or role.  
    - **Impact:** Users see cached results generated under a different security filter.  
    - **Recommendation:** Expand vary-by to include user claims or drop caching.

11. **Single item metadata cache shared between users**  
    - **Location:** `src/Honua.Server.Host/Stac/StacCollectionsController.cs:187`  
    - **Problem:** Item responses are cached without tenant/user keys.  
    - **Impact:** Item-level access controls can be bypassed by cached responses.  
    - **Recommendation:** Add vary-by-claim or disable caching for protected collections.

12. **GET /stac/search cache leaks cross-tenant results**  
    - **Location:** `src/Honua.Server.Host/Stac/StacSearchController.cs:75`  
    - **Problem:** Search results are cached only by query params, not by user.  
    - **Impact:** One user’s dataset visibility is exposed to other tenants via cache reuse.  
    - **Recommendation:** Add user/tenant vary keys or disable caching for protected datasets.

13. **Records search buffers entire catalog**  
    - **Location:** `src/Honua.Server.Host/Records/RecordsEndpointExtensions.cs:166`  
    - **Problem:** `/records/search` materialises the full result set (`filtered.ToList()`) before paging.  
    - **Impact:** Large catalogs exhaust memory and negate streaming benefits.  
    - **Recommendation:** Page directly over the queryable (defer materialisation) or stream cursor results.

14. **Absolute URL helper unusable behind proxies**  
    - **Location:** `src/Honua.Server.Host/Utilities/RequestLinkHelper.cs:363`  
    - **Problem:** `ShouldTrustForwardedHeaders` returns false when `TrustedProxyValidator` isn’t registered (default), so all absolute links use the internal host.  
    - **Impact:** Clients see broken/self links when the app runs behind a proxy.  
    - **Recommendation:** Register `TrustedProxyValidator` during startup and reuse it here; fail fast if proxies aren’t configured.

15. **Vector tile preseed job APIs block on async stores**  
    - **Location:** `src/Honua.Server.Host/VectorTiles/VectorTilePreseedService.cs:152`  
    - **Problem:** `ListJobs` / `TryGetJob` call `GetAllAsync().GetAwaiter().GetResult()`.  
    - **Impact:** Thread-pool threads deadlock under contention, stalling admin APIs.  
    - **Recommendation:** Make the public API async and await the store methods.

16. **Raster preseed job APIs block likewise**  
    - **Location:** `src/Honua.Server.Host/Raster/RasterTilePreseedService.cs:101`  
    - **Problem:** The raster service exposes the same synchronous wrappers around async job stores.
    - **Impact:** Admin endpoints hang under load just like the vector variant.  
    - **Recommendation:** Provide async accessors and await store operations.

17. **Data ingestion job listing blocks synchronously**  
    - **Location:** `src/Honua.Server.Core/Import/DataIngestionService.cs:86`  
    - **Problem:** `ListJobs/TryGetJob/CancelAsync` synchronously wait on `ActiveJobStore`, risking deadlocks when invoked from HTTP context.  
    - **Recommendation:** Convert to async path (or expose synchronous snapshots guarded by locks).

18. **Esri service migration job APIs block synchronously**  
    - **Location:** `src/Honua.Server.Core/Migration/EsriServiceMigrationService.cs:70`  
    - **Problem:** Same `.GetAwaiter().GetResult()` pattern on async stores.  
    - **Impact:** Hangs migration admin endpoints.  
    - **Recommendation:** Make the surface async and await job store calls.

19. **Attachment overlays fetch up to 10k geometries into memory**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2158`  
    - **Problem:** `CollectVectorGeometriesAsync` buffers geometries in a `List<Geometry>` with a cap of 10 000, regardless of tile size.  
    - **Impact:** Rendering tiles with rich overlays causes large heap spikes and slowed GC.  
    - **Recommendation:** Stream batched geometries or cap by size, not absolute feature count.

20. **Host test suite missing NSubstitute package**  
    - **Location:** `tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj:17`  
    - **Problem:** Tests reference `NSubstitute`, but the project file only includes Moq, so the suite won’t compile.  
    - **Impact:** CI cannot execute host tests, hiding regressions.  
    - **Recommendation:** Add `PackageReference Include="NSubstitute"` (or migrate those tests to Moq).

21. **MySQL data provider tests lack MySqlConnector import**  
    - **Location:** `tests/Honua.Server.Core.Tests/Data/MySql/MySqlDataStoreProviderTests.cs:93`  
    - **Problem:** `MySqlConnection` is used without the corresponding `using MySqlConnector;`, causing build errors.  
    - **Recommendation:** Add the missing `using` statements (and ensure the package reference is present).

22. **SQL Server data provider tests lack Microsoft.Data.SqlClient import**  
    - **Location:** `tests/Honua.Server.Core.Tests/Data/SqlServer/SqlServerDataStoreProviderTests.cs:119`  
    - **Problem:** Same `SqlConnection` usage without `using Microsoft.Data.SqlClient;`, so the project fails to compile.  
    - **Recommendation:** Restore the appropriate `using` directive.

23. **Digest auth requires plaintext password storage**  
    - **Location:** `src/Honua.Server.Core/OpenRosa/DigestAuthenticationHandler.cs:124`  
    - **Problem:** `ComputeHA1` uses `user.Password` directly, expecting plaintext or HA1 values.  
    - **Impact:** Either credentials must be stored unhashed, or the handler breaks — both unacceptable.  
    - **Recommendation:** Store HA1 digests only and update `GetByUsernameAsync` to return the pre-computed hash.

24. **Vector tile preseed ignores tile budget for bounded extents**  
    - **Location:** `src/Honua.Server.Host/VectorTiles/VectorTilePreseedService.cs:430`  
    - **Problem:** `CalculateTotalTilesForExtent` returns the computed total without re-checking `_limits.MaxTilesPerJob`.  
    - **Impact:** Large extents bypass the safeguard and enqueue millions of tiles.  
    - **Recommendation:** Compare the computed total against `_limits.MaxTilesPerJob` and fail fast if exceeded.

25. **Per-layer rate limit dictionary never pruned**  
    - **Location:** `src/Honua.Server.Host/VectorTiles/VectorTilePreseedService.cs:118`  
    - **Problem:** `_userRateLimits` grows per `{service}/{layer}` key and is never cleaned up.  
    - **Impact:** Long-lived nodes leak memory as new datasets are processed.  
    - **Recommendation:** Expire entries (e.g., via `MemoryCache`) after the rate-limit window.

26. **Catalog search cache shared across users**  
    - **Location:** `src/Honua.Server.Host/Catalog/CatalogApiController.cs:48`  
    - **Problem:** `/api/catalog` results are cached without regard to the caller’s claims.  
    - **Impact:** One tenant’s catalog exposes to another via cache hits.  
    - **Recommendation:** Include tenant/user claims in the cache key or disable caching when scope differs.

27. **Catalog record cache leaks protected metadata**  
    - **Location:** `src/Honua.Server.Host/Catalog/CatalogApiController.cs:148`  
    - **Problem:** Individual record responses are cached purely by route parameters.  
    - **Impact:** Users lacking access may see cached metadata.  
    - **Recommendation:** Add user/role vary-by or disable caching here.

28. **Catalog total count capped at 1000**  
    - **Location:** `src/Honua.Server.Host/Catalog/CatalogApiController.cs:87`  
    - **Problem:** `totalCount` is computed by re-running `Search(limit: 1000, offset: 0)`, clipping totals above 1000.  
    - **Impact:** Paginators and analytics receive incorrect totals.  
    - **Recommendation:** Add a dedicated count API/query or expose the repository’s real count.

29. **Catalog search doubles read load**  
    - **Location:** `src/Honua.Server.Host/Catalog/CatalogApiController.cs:75`  
    - **Problem:** Every request executes two full catalog queries (page + counting).  
    - **Impact:** Throughput halves and big catalogs thrash caches.  
    - **Recommendation:** Return count alongside the initial query (e.g., COUNT window) or add cheap metadata endpoints.

30. **Application-level rate limiting disabled**  
    - **Location:** `src/Honua.Server.Host/Extensions/WebApplicationExtensions.cs:104`  
    - **Problem:** `UseHonuaRateLimitingMiddleware` is now a no-op, so attributes like `[EnableRateLimiting]` have zero effect without YARP.  
    - **Impact:** Deployments without the prescribed proxy lose brute-force and DoS protections.  
    - **Recommendation:** Keep an in-process fallback limiter or fail fast when YARP isn’t fronting the site.

31. **Input validation middleware removed without fallback**  
    - **Location:** `src/Honua.Server.Host/Extensions/WebApplicationExtensions.cs:118`  
    - **Problem:** Input validation was shifted to the proxy tier, but the application no longer enforces size/validation limits itself.  
    - **Impact:** Standalone deployments have no request-size guardrails.  
    - **Recommendation:** Provide an optional in-app validator or document that the app must not run without the proxy.

32. **WFS Transaction reads entire body without limits**  
    - **Location:** `src/Honua.Server.Host/Wfs/WfsTransactionHandlers.cs:59`  
    - **Problem:** The handler streams the entire XML body into memory via `ReadToEndAsync` without checking length.  
    - **Impact:** Attackers can post huge payloads to exhaust memory and crash the service.  
    - **Recommendation:** Apply a content-length ceiling, wrap the stream in a limiter, or parse incrementally.

33. **STAC search cache missing sort/filter keys**  
    - **Location:** `src/Honua.Server.Host/Middleware/CachingConfiguration.cs:76`  
    - **Problem:** The policy varies by collections/ids/bbox/datetime/limit/token only. `sortby`, `fields`, `filter`, and `filter-lang` are ignored, so distinct queries share the same entry.  
    - **Impact:** Clients receive incorrect ordering or field projections.  
    - **Recommendation:** Include all query parameters influencing results in `SetVaryByQuery`.

34. **Collection items cache omits key parameters**  
    - **Location:** `src/Honua.Server.Host/Middleware/CachingConfiguration.cs:66`  
    - **Problem:** The `/items` cache varies on limit/token/bbox/datetime/filter but ignores `fields`, `sortby`, `resultType`, and `ids`.  
    - **Impact:** Cached responses are reused for different projections or hits-only requests.  
    - **Recommendation:** Expand the vary-by list or disable caching for formats sensitive to those parameters.

35. **Catalog collections cache ignores paging parameters**  
    - **Location:** `src/Honua.Server.Host/Middleware/CachingConfiguration.cs:97`  
    - **Problem:** The policy only varies by `q` and `group`, so different `limit`/`offset` values return cached pages.  
    - **Impact:** Clients see duplicate pages or wrong page sizes.  
    - **Recommendation:** Add `limit` and `offset` (and any future pagination token) to the vary-by list.

36. **Digest auth nonce allows replay**  
    - **Location:** `src/Honua.Server.Core/OpenRosa/DigestAuthenticationHandler.cs:104`  
    - **Problem:** Nonces encode only a timestamp and remain valid for the full lifetime without a server-side replay cache.  
    - **Impact:** Captured digests can be replayed repeatedly until the nonce expires.  
    - **Recommendation:** Track used nonces (e.g., via cache) or include a server secret in the nonce verification step.

37. **STAC cache invalidation misses search entries**  
    - **Location:** `src/Honua.Server.Host/Middleware/OutputCacheInvalidationService.cs:52`  
    - **Problem:** `InvalidateStacCollectionCacheAsync` only evicts `stac-collections` / `stac-collection-metadata`; cached `/stac/search` results remain stale after collection edits.  
    - **Recommendation:** Evict the `stac-search` tag whenever collections or items mutate.

38. **Deleting a collection leaves item caches intact**  
    - **Location:** `src/Honua.Server.Host/Stac/Services/StacCollectionService.cs:233`  
    - **Problem:** `DeleteCollectionAsync` invokes only `InvalidateStacCollectionCacheAsync`, so `/stac/collections/{id}/items` stays cached.  
    - **Impact:** Clients can continue retrieving items for deleted collections until the cache expires.  
    - **Recommendation:** Also call `InvalidateStacItemsCacheAsync(collectionId)` (and evict search results).

39. **New OGC attachment download route lacks coverage**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs:1202`  
    - **Problem:** The fresh `GetCollectionItemAttachment` endpoint has no unit or integration tests for happy-path, 404, or storage errors.  
    - **Impact:** Regressions (e.g., attachment mismatches) will slip through.  
    - **Recommendation:** Add API tests exercising success, missing attachment, mismatched feature, and storage failures.

40. **STAC search eagerly loads every collection**  
    - **Location:** `src/Honua.Server.Host/Stac/StacSearchController.cs:255`  
    - **Problem:** Each search request calls `_store.ListCollectionsAsync()` and materialises all collections before filtering.  
    - **Impact:** Search time grows with catalog size and wastes memory on every request.  
    - **Recommendation:** Fetch only the collections referenced in the query (or cache a lightweight lookup table).
