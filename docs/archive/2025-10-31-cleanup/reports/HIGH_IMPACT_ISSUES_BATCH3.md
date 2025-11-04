# High-Impact Issues – Batch 3

## Batch A – Existing Findings (1-20)

1. **Geoservices related-records is still a stub**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs:147`  
   - **Problem:** `ExecuteRelatedRecordsQueryAsync` returns a hard-coded empty payload, so every `queryRelatedRecords` call responds with zero groups.  
   - **Impact:** Feature editing clients and ArcGIS-compatible apps cannot retrieve one-to-many relationships, breaking core workflows.  
   - **Recommendation:** Implement the same resolution path used by the controller helper—compose the related query, stream results, and project the pairs into Esri response DTOs.

2. **Streaming GeoJSON writer shares a global formatter instance**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs:33`  
   - **Problem:** A static `GeoJsonWriter` is reused across requests; `GeoJsonWriter` is mutable and not thread-safe.  
   - **Impact:** Under load, concurrent writes race on the shared formatter leading to corrupted JSON output or thrown exceptions.  
   - **Recommendation:** Replace the static field with a per-request formatter (instantiate inside `WriteFeatureCollectionAsync` or use `ThreadLocal`).

3. **Out-field selections are ignored in streaming GeoJSON responses**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs:233`  
   - **Problem:** The writer iterates `layer.Fields` and writes every property, disregarding `context.RequestedOutFields`/`SelectedFields`.  
   - **Impact:** Sensitive columns that callers explicitly excluded still leak to clients, and payload size remains large.  
   - **Recommendation:** Filter the field loop by `context.SelectedFields` (and always include the object ID) before serialising.

4. **Streaming writer assumes the response body is seekable**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/Services/StreamingGeoJsonWriter.cs:145`  
   - **Problem:** It assigns `bytesWritten = response.Body.Length`; on ASP.NET Core the body stream is typically non-seekable and throws `NotSupportedException`.  
   - **Impact:** Requests fail once enough features are written to hit this line, returning 500s instead of completing.  
   - **Recommendation:** Track bytes using the `Utf8JsonWriter` flush length or remove the length read entirely; never call `Stream.Length` on HTTP response bodies.

5. **`FetchFeaturesAsync` materialises entire result sets**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:1351`  
   - **Problem:** Results are collected into a `List<GeoservicesRESTFeature>` before returning.  
   - **Impact:** Large queries exhaust memory, defeating the streaming writer optimisations added elsewhere.  
   - **Recommendation:** Push this logic into the streaming writer (or yield directly into the response) instead of buffering `List<T>`.

6. **Related-record queries buffer every record in memory**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:1504`  
   - **Problem:** `QueryRelatedRecordsInternalAsync` loads the entire related table into `List<(FeatureRecord, GeoservicesRESTFeature)>`.  
   - **Impact:** Relationship lookups quickly OOM with realistic datasets (>10k child rows).  
   - **Recommendation:** Stream results grouped by objectId (or page through the repository) and emit directly to the serializer.

7. **All non-GeoJSON exports buffer entire datasets**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:2235`  
   - **Problem:** WKT, WKB, KML, and KMZ paths each build `List<FeatureRecord>` before serialisation.  
   - **Impact:** Exporting large layers causes memory pressure and request failures.  
   - **Recommendation:** Introduce streaming writers for these formats (akin to CSV/GeoPackage exporters) and pipe the repository cursor directly.

8. **Geometry service ignores request cancellation**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTGeometryServerController.cs:107`  
   - **Problem:** A new `CancellationTokenSource` is created but its token is never passed to `_serializer` or `_executor`.  
   - **Impact:** Long-running GDAL operations can run past the 30 s timeout, consuming CPU even after the client disconnects.  
   - **Recommendation:** Thread `cts.Token` through `_serializer.DeserializeGeometries`, `_executor.Project`, and serialization calls.

9. **GDAL Zarr converter methods are unimplemented**  
   - **Location:** `src/Honua.Server.Core/Raster/Cache/GdalZarrConverterService.cs:81`  
   - **Problem:** Every query method throws `NotImplementedException` when the Python fallback is missing.  
   - **Impact:** Zarr endpoints return 500s whenever GDAL is configured without the Python sidecar, blocking advertised functionality.  
   - **Recommendation:** Either disable the endpoints when GDAL lacks support or implement the GDAL-backed queries.

10. **Feature edit batches never enlist repository operations in the transaction**  
    - **Location:** `src/Honua.Server.Core/Editing/FeatureEditOrchestrator.cs:285`  
    - **Problem:** A transaction is opened but `_repository.CreateAsync/UpdateAsync/DeleteAsync` are called without passing it.  
    - **Impact:** Multi-command batches are not atomic—partial failures leave the database in an inconsistent state.  
    - **Recommendation:** Augment repository APIs to accept an optional `IDataStoreTransaction` and pass the active transaction through each call.

11. **Attachment uploads are fully buffered in memory**  
    - **Location:** `src/Honua.Server.Core/Attachments/FeatureAttachmentOrchestrator.cs:454`  
    - **Problem:** `BufferUploadAsync` copies the entire stream into a `MemoryStream` before handing it to storage.  
    - **Impact:** Uploading large attachments (hundreds of MB) consumes equivalent RAM and risks OOM.  
    - **Recommendation:** Stream uploads directly to the backing store (pipe to `IAttachmentStore`) while computing the checksum.

12. **Download helper copies every non-seekable stream into RAM**  
    - **Location:** `src/Honua.Server.Host/Attachments/AttachmentDownloadHelper.cs:232`  
    - **Problem:** `EnsureSeekableStreamAsync` always buffers to `MemoryStream` if the store returns a non-seekable stream.  
    - **Impact:** Downloading large blobs doubles the memory footprint and prevents true streaming.  
    - **Recommendation:** Support range handling with non-seekable streams (wrap in a buffered response) or expose seekable streams from stores.

13. **Attachment links are suppressed for root-level services**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2442`  
    - **Problem:** `ShouldExposeAttachmentLinks` requires `service.FolderId.HasValue()`. Root collections have no folder, so links are hidden.  
    - **Impact:** Clients never see attachments for top-level datasets, even when enabled.  
    - **Recommendation:** Drop the folder check or replace it with an explicit configuration flag.

14. **Attachment link generation causes an N+1 store query**  
    - **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs:2467`  
    - **Problem:** `CreateAttachmentLinksAsync` calls `attachmentOrchestrator.ListAsync` for every feature inside the main fetch loop.  
    - **Impact:** A page of 100 features triggers 100 attachment lookups, hammering storage backends.  
    - **Recommendation:** Batch attachment lookups (e.g., `ListAsync` accepting multiple feature IDs) or prefetch attachments for the current page.

15. **Forwarded header handling trusts unvalidated input**  
    - **Location:** `src/Honua.Server.Host/Utilities/RequestLinkHelper.cs:262`  
    - **Problem:** The helper honours `X-Forwarded-*` and `Forwarded` headers without consulting `TrustedProxyValidator`.  
    - **Impact:** Any client can spoof scheme/host, producing poisoned absolute URLs in responses and logs.  
    - **Recommendation:** Require a trusted-proxy check before using forwarded headers (or reuse the existing validator).

16. **GeoArrow exporter coerces every property to string**  
    - **Location:** `src/Honua.Server.Core/Serialization/GeoArrowStreamingWriter.cs:413`  
    - **Problem:** `ResolveFieldValues` normalises all attribute values to `string`.  
    - **Impact:** Consumers lose numeric/temporal types, breaking analytics pipelines expecting typed Arrow columns.  
    - **Recommendation:** Implement proper Arrow type mapping (Int32, Double, Timestamp, Boolean, etc.) before building arrays.

17. **GeoArrow ignores requested CRS transformations**  
    - **Location:** `src/Honua.Server.Core/Serialization/GeoArrowStreamingWriter.cs:404`  
    - **Problem:** `TransformGeometry` returns the input geometry with an updated SRID but never reprojects coordinates.  
    - **Impact:** Clients requesting alternate CRS receive mislabeled geometries with incorrect coordinates.  
    - **Recommendation:** Integrate a reprojection library (e.g., ProjNet) to transform geometries when source and target WKIDs differ.

18. **GeoParquet exports all attributes as strings**  
    - **Location:** `src/Honua.Server.Core/Export/GeoParquetExporter.cs:189`  
    - **Problem:** Each attribute column is declared `Column<string?>`, regardless of the underlying field type.  
    - **Impact:** Downstream parquet consumers cannot query numerics or timestamps efficiently and risk type errors.  
    - **Recommendation:** Map metadata field types to the appropriate ParquetSharp column types (Int32, Double, Date, Boolean, etc.).

19. **GeoParquet exporter buffers the entire dataset in memory**  
    - **Location:** `src/Honua.Server.Core/Export/GeoParquetExporter.cs:56`  
    - **Problem:** Geometry, bbox, and attribute values are accumulated into lists before any parquet write occurs.  
    - **Impact:** Large exports spike memory usage and fail for big layers.  
    - **Recommendation:** Write record batches incrementally (flush builder arrays per batch) instead of buffering all values.

20. **GeoJSON sequence writer reuses a global formatter**  
    - **Location:** `src/Honua.Server.Core/Serialization/GeoJsonSeqStreamingWriter.cs:34`  
    - **Problem:** Same threading issue as the GeoServices writer: `_geoJsonWriter` is static.  
    - **Impact:** Concurrent sequence exports corrupt output.  
    - **Recommendation:** Instantiate `GeoJsonWriter` per writer instance or per call.

## Batch B – New Findings (21-40)

21. **GeoJSON sequence writer still emits every field**  
   - **Location:** `src/Honua.Server.Core/Serialization/GeoJsonSeqStreamingWriter.cs:157`  
   - **Problem:** The writer ignores `StreamingWriterContext.PropertyNames`, leaking excluded properties.  
   - **Impact:** Sensitive columns appear in line-delimited exports and responses stay bloated.  
   - **Recommendation:** Restrict the property loop to the caller-selected field set (mirroring API filters).

22. **WKT streaming writer shares static readers**  
   - **Location:** `src/Honua.Server.Core/Serialization/WktStreamingWriter.cs:48`  
   - **Problem:** `_geoJsonReader` is static, so concurrent requests reuse mutable state.  
   - **Impact:** Geometries can be misparsed or throw when multiple threads stream simultaneously.  
   - **Recommendation:** Create a reader per writer instance or guard the static instance with a lock.

23. **Hashed connection-string cache key risks collisions**  
   - **Location:** `src/Honua.Server.Core/Data/Postgres/PostgresConnectionManager.cs:93`  
   - **Problem:** `connectionString.GetHashCode()` (32-bit, randomized) is used as the cache key suffix.  
   - **Impact:** Different encrypted strings can collide, causing one tenant’s decrypted value to be reused for another.  
   - **Recommendation:** Replace `GetHashCode` with a stable cryptographic hash (e.g., SHA256) of the ciphertext.

24. **Kerchunk store only coordinates within-process callers**  
   - **Location:** `src/Honua.Server.Core/Raster/Kerchunk/KerchunkReferenceStore.cs:26`  
   - **Problem:** Locking relies on an in-memory dictionary; multi-instance deployments regenerate the same references concurrently.  
   - **Impact:** Clustered deployments stampede the generator and corrupt cache entries.  
   - **Recommendation:** Introduce distributed locking (Redis, database, etc.) or move locking into the cache provider.

25. **Attachment downloads ignore the descriptor’s storage provider**  
   - **Location:** `src/Honua.Server.Host/Attachments/AttachmentDownloadHelper.cs:89`  
   - **Problem:** The helper resolves stores by the layer’s current profile, then tries to fetch using `descriptor.StorageProvider`.  
   - **Impact:** After switching storage profiles, existing attachments become unreadable.  
   - **Recommendation:** Resolve the store based on `descriptor.StorageProvider` (or maintain backwards compatibility by attempting both).

26. **Deletion path suffers from the same store mismatch**  
   - **Location:** `src/Honua.Server.Core/Attachments/FeatureAttachmentOrchestrator.cs:441`  
   - **Problem:** `ResolveStore` always uses the layer’s active profile when deleting.  
   - **Impact:** Old attachments are never removed from their original store, leaking storage.  
   - **Recommendation:** Resolve stores per descriptor for delete operations as well.

27. **Observable cache decorator breaks synchronous callers**  
   - **Location:** `src/Honua.Server.Core/Caching/ObservableCacheDecorator.cs:100`  
   - **Problem:** `Get`, `Set`, and `Refresh` throw `NotSupportedException`, but many .NET components still call the synchronous APIs.  
   - **Impact:** Decorating the distributed cache causes immediate runtime failures in the data-protection stack and other libraries.  
   - **Recommendation:** Provide synchronous wrappers delegating to the async calls instead of throwing.

28. **OGC items responses rebuild lists for every format**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs:628`  
   - **Problem:** All formats create in-memory lists (KML, TopoJSON, WKT, etc.) before responding.  
   - **Impact:** Large collections OOM and negate streaming export capabilities.  
   - **Recommendation:** Refactor to reusable streaming writers per format and avoid buffering.

29. **Feature creation reads the entire request into memory**  
   - **Location:** `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs:1206`  
   - **Problem:** `EnumerateGeoJsonFeatures(...).ToList()` materialises the full request payload.  
   - **Impact:** Uploading large FeatureCollections consumes equivalent RAM and blocks streaming ingestion.  
   - **Recommendation:** Process `IEnumerable<JsonElement>` lazily, translating each feature into a command without storing the list.

30. **PostgreSQL bulk insert buffers every record first**  
   - **Location:** `src/Honua.Server.Core/Data/Postgres/PostgresBulkOperations.cs:56`  
   - **Problem:** Records are read into lists before `COPY` begins.  
   - **Impact:** Bulk imports lose their advantage on large datasets and risk memory blowups.  
   - **Recommendation:** Stream records directly into the binary import writer, flushing per batch.

31. **WKB streaming writer shares the static GeoJSON reader**  
   - **Location:** `src/Honua.Server.Core/Serialization/WkbStreamingWriter.cs:40`  
   - **Problem:** Identical concurrency issue as WKT/GeoJSON writers.  
   - **Impact:** Binary exports can fail or corrupt under load.  
   - **Recommendation:** Use thread-safe reader instances.

32. **Return-IDs-only queries still walk every record**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs:159`  
   - **Problem:** Without a limit, `FetchIdsAsync` enumerates the whole dataset and throws when exceeding 10 k rows.  
   - **Impact:** Requests intended for paging or counting DoS the server or raise 500s instead of signalling transfer limits.  
   - **Recommendation:** Stream IDs with paging (and set `ExceededTransferLimit`) or enforce `MaxRecordCount` before enumerating.

33. **Distinct queries scan up to 100k rows into memory**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:1530`  
   - **Problem:** Distinct handling uses a `List<Dictionary<...>>` with a large scan cap.  
   - **Impact:** High-cardinality distinct requests hit the 100k limit and still allocate huge structures.  
   - **Recommendation:** Push distinct work into the repository (database-level `SELECT DISTINCT`) and stream the results.

34. **Statistics fallback loads all groups in-process**  
   - **Location:** `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs:1643`  
   - **Problem:** Aggregations build a `List<StatisticsResult>` and a dictionary of groups before paging.  
   - **Impact:** High-cardinality group-bys exhaust memory and ignore transfer-limit semantics.  
   - **Recommendation:** Offload statistics to the datastore or stream groups with bounded memory (e.g., temp tables).

35. **Host test project references NSubstitute without adding the package**  
   - **Locations:** `tests/Honua.Server.Host.Tests/Stac/StacSortTests.cs:12`, `tests/Honua.Server.Host.Tests/Honua.Server.Host.Tests.csproj:25`  
   - **Problem:** Tests use `Substitute.For` but the csproj only references Moq; build fails with missing type errors.  
   - **Impact:** The host test suite cannot compile or run.  
   - **Recommendation:** Add `PackageReference Include="NSubstitute"` (or convert tests to Moq consistently).

36. **WMTS capabilities builder blocks on async I/O**  
   - **Location:** `src/Honua.Server.Host/Wmts/WmtsCapabilitiesBuilder.cs:86`  
   - **Problem:** `GetAllAsync(...).GetAwaiter().GetResult()` runs inside request handling.  
   - **Impact:** Thread-pool deadlocks (under sync contexts) and degraded throughput.  
   - **Recommendation:** Make the method async and await the registry call (or prefetch datasets during warmup).

37. **FlatGeobuf streaming writer reuses mutable reader state**  
   - **Location:** `src/Honua.Server.Host/Ogc/FlatGeobufStreamingWriter.cs:56`  
   - **Problem:** Static `GeoJsonReader` is shared across threads.  
   - **Impact:** Concurrent FlatGeobuf exports can misparse geometries.  
   - **Recommendation:** Instantiate a reader per writer or guard access.

38. **FlatGeobuf output ignores requested property filtering**  
   - **Location:** `src/Honua.Server.Host/Ogc/FlatGeobufStreamingWriter.cs:209`  
   - **Problem:** The exporter loops over `layer.Fields` and writes everything regardless of query projections.  
   - **Impact:** Clients receive unwanted columns and data leakage mirrors the GeoJSON issue.  
   - **Recommendation:** Respect the streaming context’s selected property list when building the attributes table.

39. **WMS capabilities builder blocks on async data retrieval**  
   - **Location:** `src/Honua.Server.Host/Wms/WmsCapabilitiesBuilder.cs:152`  
   - **Problem:** Another `GetAllAsync(...).GetAwaiter().GetResult()` in the request pipeline.  
   - **Impact:** Same deadlock and scalability problems as WMTS.  
   - **Recommendation:** Make the builder async or pre-cache datasets ahead of time.

40. **WCS capabilities builder also blocks on the raster registry**  
   - **Location:** `src/Honua.Server.Host/Wcs/WcsCapabilitiesBuilder.cs:75`  
   - **Problem:** Uses `.GetAwaiter().GetResult()` to fetch coverages synchronously.  
   - **Impact:** WCS capability requests can hang or stall threads under load.  
   - **Recommendation:** Await the registry call or move capability assembly to an async initialisation stage.

