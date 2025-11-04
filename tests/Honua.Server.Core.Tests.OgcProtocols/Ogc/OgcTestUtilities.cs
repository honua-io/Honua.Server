using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Raster;
using NetTopologySuite.Geometries;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Static utility class providing factory methods for creating OGC test dependencies.
/// </summary>
/// <remarks>
/// This class provides static factory methods for creating individual test dependencies.
/// For a more convenient fixture-based approach that eliminates repetitive setup code,
/// consider using <see cref="OgcHandlerTestFixture"/> instead.
/// </remarks>
internal static class OgcTestUtilities
{
    /// <summary>
    /// Creates a metadata snapshot containing test service and layer definitions.
    /// </summary>
    /// <param name="attachmentsEnabled">Whether attachments should be enabled on the test layer.</param>
    /// <param name="exposeOgcLinks">Whether OGC attachment links should be exposed.</param>
    /// <returns>A <see cref="MetadataSnapshot"/> with the "roads" service and "roads-primary" layer.</returns>
    internal static MetadataSnapshot CreateSnapshot(bool attachmentsEnabled = false, bool exposeOgcLinks = false)
    {
        var catalog = new CatalogDefinition { Id = "honua", Title = "Honua" };
        var folder = new FolderDefinition { Id = "root", Title = "Root" };
        var dataSource = new DataSourceDefinition { Id = "stub", Provider = "stub", ConnectionString = "ignored" };

        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = "roads",
            Title = "Primary Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Crs = new[] { "EPSG:4326" },
            DefaultStyleId = "primary-roads-line",
            StyleIds = new[] { "primary-roads-line" },
            Query = new LayerQueryDefinition
            {
                SupportedParameters = new[] { "tileMatrix" }
            },
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int64", StorageType = "INTEGER", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "TEXT", Nullable = true }
            },
            Attachments = attachmentsEnabled
                ? new LayerAttachmentDefinition
                {
                    Enabled = true,
                    StorageProfileId = "local",
                    ExposeOgcLinks = exposeOgcLinks
                }
                : LayerAttachmentDefinition.Disabled
        };

        var service = new ServiceDefinition
        {
            Id = "roads",
            Title = "Roads",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "stub",
            Enabled = true,
            Ogc = new OgcServiceDefinition
            {
                CollectionsEnabled = true,
                ItemLimit = 1000,
                DefaultCrs = "EPSG:4326",
                ExportFormats = new ExportFormatsDefinition
                {
                    GeoJsonEnabled = true,
                    HtmlEnabled = true,
                    KmlEnabled = true,
                    KmzEnabled = true,
                    ShapefileEnabled = true,
                    GeoPackageEnabled = true,
                    FlatGeobufEnabled = true,
                    GeoArrowEnabled = true,
                    GeoParquetEnabled = true,
                    PmTilesEnabled = true,
                    TopoJsonEnabled = true
                }
            }
        };

        var rasterDataset = new RasterDatasetDefinition
        {
            Id = "roads-imagery",
            Title = "Roads Imagery",
            ServiceId = service.Id,
            LayerId = layer.Id,
            Crs = new[] { "EPSG:4326" },
            Source = new RasterSourceDefinition
            {
                Type = "cog",
                Uri = "file:///data/rasters/roads-imagery.tif"
            },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "natural-color",
                StyleIds = new[] { "natural-color", "infrared" }
            },
            Cache = new RasterCacheDefinition
            {
                Enabled = true,
                Preseed = false,
                ZoomLevels = new[] { 0, 1, 2 }
            }
        };

        var vectorDataset = new RasterDatasetDefinition
        {
            Id = "roads-vectortiles",
            Title = "Roads Vector Tiles",
            ServiceId = service.Id,
            LayerId = layer.Id,
            Crs = new[] { "EPSG:3857" },
            Source = new RasterSourceDefinition
            {
                Type = "vector",
                Uri = "pmtiles://roads"
            },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "primary-roads-line",
                StyleIds = new[] { "primary-roads-line" }
            },
            Cache = new RasterCacheDefinition
            {
                Enabled = true,
                Preseed = false,
                ZoomLevels = new[] { 0, 1, 2 }
            }
        };

        var styles = new[]
        {
            new StyleDefinition
            {
                Id = "primary-roads-line",
                Title = "Primary Roads",
                Renderer = "simple",
                GeometryType = "line",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "line",
                    StrokeColor = "#FF8800FF",
                    StrokeWidth = 2.0
                }
            },
            new StyleDefinition
            {
                Id = "natural-color",
                Title = "Natural Color",
                Renderer = "simple",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "polygon",
                    FillColor = "#5AA06EFF",
                    StrokeColor = "#FFFFFFFF",
                    StrokeWidth = 1.5
                }
            },
            new StyleDefinition
            {
                Id = "infrared",
                Title = "Infrared",
                Renderer = "simple",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "polygon",
                    FillColor = "#DC5578FF",
                    StrokeColor = "#FFFFFFFF",
                    StrokeWidth = 1.5
                }
            }
        };

        return new MetadataSnapshot(
            catalog,
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            new[] { layer },
            new[] { rasterDataset, vectorDataset },
            styles);
    }

    /// <summary>
    /// Creates a metadata registry with the specified snapshot.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to use, or null to use the default test snapshot.</param>
    /// <returns>An <see cref="IMetadataRegistry"/> instance.</returns>
    internal static IMetadataRegistry CreateRegistry(MetadataSnapshot? snapshot = null)
        => new StaticMetadataRegistry(snapshot ?? CreateSnapshot());

    /// <summary>
    /// Creates a fake feature repository containing 2 test road features.
    /// </summary>
    /// <returns>An <see cref="IFeatureRepository"/> instance.</returns>
    internal static IFeatureRepository CreateRepository()
        => new FakeFeatureRepository();

    /// <summary>
    /// Creates a feature context resolver using the specified metadata registry.
    /// </summary>
    /// <param name="registry">The metadata registry to use.</param>
    /// <returns>An <see cref="IFeatureContextResolver"/> instance.</returns>
    internal static IFeatureContextResolver CreateResolver(IMetadataRegistry registry)
    {
        return new FeatureContextResolver(registry, new StubDataStoreProviderFactory());
    }

    /// <summary>
    /// Creates a stub GeoPackage exporter that throws <see cref="NotSupportedException"/> when used.
    /// </summary>
    /// <returns>An <see cref="IGeoPackageExporter"/> stub instance.</returns>
    internal static IGeoPackageExporter CreateGeoPackageExporterStub()
        => new NullGeoPackageExporter();

    /// <summary>
    /// Creates a stub Shapefile exporter that throws <see cref="NotSupportedException"/> when used.
    /// </summary>
    /// <returns>An <see cref="IShapefileExporter"/> stub instance.</returns>
    internal static IShapefileExporter CreateShapefileExporterStub()
        => new NullShapefileExporter();

    /// <summary>
    /// Creates a functional FlatGeobuf exporter.
    /// </summary>
    /// <returns>An <see cref="IFlatGeobufExporter"/> instance.</returns>
    internal static IFlatGeobufExporter CreateFlatGeobufExporter()
        => new FlatGeobufExporter();

    /// <summary>
    /// Creates a functional GeoArrow exporter.
    /// </summary>
    /// <returns>An <see cref="IGeoArrowExporter"/> instance.</returns>
    internal static IGeoArrowExporter CreateGeoArrowExporter()
        => new GeoArrowExporter();

    /// <summary>
    /// Creates a stub CSV exporter that throws <see cref="NotSupportedException"/> when used.
    /// </summary>
    /// <returns>An <see cref="ICsvExporter"/> stub instance.</returns>
    internal static ICsvExporter CreateCsvExporter()
        => new NullCsvExporter();

    /// <summary>
    /// Creates a functional PMTiles exporter.
    /// </summary>
    /// <returns>An <see cref="IPmTilesExporter"/> instance.</returns>
    internal static IPmTilesExporter CreatePmTilesExporter()
        => new PmTilesExporter();

    /// <summary>
    /// Creates a stub attachment orchestrator with optional predefined attachments.
    /// </summary>
    /// <param name="attachments">
    /// Dictionary mapping feature keys (format: "serviceId:layerId:featureId") to attachment lists,
    /// or null for an empty orchestrator.
    /// </param>
    /// <returns>An <see cref="IFeatureAttachmentOrchestrator"/> stub instance.</returns>
    internal static IFeatureAttachmentOrchestrator CreateAttachmentOrchestratorStub(
        IDictionary<string, IReadOnlyList<AttachmentDescriptor>>? attachments = null)
        => new StubAttachmentOrchestrator(attachments ?? new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Creates an in-memory raster tile cache provider for testing.
    /// </summary>
    /// <returns>An <see cref="IRasterTileCacheProvider"/> instance.</returns>
    internal static IRasterTileCacheProvider CreateRasterTileCacheProvider()
        => new InMemoryRasterTileCacheProvider();

    /// <summary>
    /// Creates a null raster tile cache metrics service (no-op implementation).
    /// </summary>
    /// <returns>An <see cref="IRasterTileCacheMetrics"/> instance.</returns>
    internal static IRasterTileCacheMetrics CreateRasterTileCacheMetrics()
        => new NullRasterTileCacheMetrics();

    /// <summary>
    /// Creates a SkiaSharp raster renderer with file system source provider.
    /// </summary>
    /// <returns>A <see cref="Honua.Server.Core.Raster.Rendering.SkiaSharpRasterRenderer"/> instance.</returns>
    internal static Honua.Server.Core.Raster.Rendering.SkiaSharpRasterRenderer CreateRasterRenderer()
    {
        var providers = new List<Honua.Server.Core.Raster.Sources.IRasterSourceProvider>
        {
            new Honua.Server.Core.Raster.Sources.FileSystemRasterSourceProvider()
        };
        var registry = new Honua.Server.Core.Raster.Sources.RasterSourceProviderRegistry(providers);
        var metadataCache = new Honua.Server.Core.Raster.RasterMetadataCache();
        return new Honua.Server.Core.Raster.Rendering.SkiaSharpRasterRenderer(registry, metadataCache);
    }

    /// <summary>
    /// Creates a null API metrics service (no-op implementation).
    /// </summary>
    /// <returns>An <see cref="IApiMetrics"/> instance.</returns>
    internal static IApiMetrics CreateApiMetrics()
        => new NullApiMetrics();

    /// <summary>
    /// Creates an OGC cache header service with default cache durations.
    /// </summary>
    /// <returns>An <see cref="OgcCacheHeaderService"/> instance.</returns>
    internal static OgcCacheHeaderService CreateCacheHeaderService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CacheHeaderOptions
        {
            TileCacheDurationSeconds = 300,
            MetadataCacheDurationSeconds = 60
        });
        return new OgcCacheHeaderService(options);
    }

    /// <summary>
    /// Creates a test HTTP context with the specified path and query string.
    /// </summary>
    /// <param name="path">The request path (e.g., "/ogc/collections/roads::roads-primary/items").</param>
    /// <param name="query">The query string without leading '?' (e.g., "f=geojson&amp;limit=10").</param>
    /// <returns>A configured <see cref="DefaultHttpContext"/> ready for testing.</returns>
    internal static DefaultHttpContext CreateHttpContext(string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 5001);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?" + query);
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IProblemDetailsService, TestProblemDetailsService>();
        context.RequestServices = services.BuildServiceProvider();

        return context;
    }

    internal static FeatureRecord CreateFeatureRecord(
        object idValue,
        NtsGeometry? geometry,
        IDictionary<string, object?> attributes,
        string idField = "road_id",
        string geometryField = "geom")
    {
        var merged = attributes is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(attributes, StringComparer.OrdinalIgnoreCase);

        merged[idField] = idValue;

        if (geometry is not null)
        {
            merged[geometryField] = geometry;
        }

        return new FeatureRecord(merged);
    }

    private sealed class NullGeoPackageExporter : IGeoPackageExporter
    {
        public Task<GeoPackageExportResult> ExportAsync(
            LayerDefinition layer,
            FeatureQuery query,
            string contentCrs,
            IAsyncEnumerable<FeatureRecord> source,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NullShapefileExporter : IShapefileExporter
    {
        public Task<ShapefileExportResult> ExportAsync(
            LayerDefinition layer,
            FeatureQuery query,
            string contentCrs,
            IAsyncEnumerable<FeatureRecord> source,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubAttachmentOrchestrator : IFeatureAttachmentOrchestrator
    {
        private readonly IDictionary<string, IReadOnlyList<AttachmentDescriptor>> _attachments;

        public StubAttachmentOrchestrator(IDictionary<string, IReadOnlyList<AttachmentDescriptor>> attachments)
        {
            _attachments = attachments;
        }

        public Task<IReadOnlyList<AttachmentDescriptor>> ListAsync(string serviceId, string layerId, string featureId, CancellationToken cancellationToken = default)
        {
            var key = BuildKey(serviceId, layerId, featureId);
            return Task.FromResult(_attachments.TryGetValue(key, out var descriptors)
                ? descriptors
                : Array.Empty<AttachmentDescriptor>());
        }

        public Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListBatchAsync(
            string serviceId,
            string layerId,
            IReadOnlyList<string> featureIds,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase);

            foreach (var featureId in featureIds)
            {
                var key = BuildKey(serviceId, layerId, featureId);
                result[featureId] = _attachments.TryGetValue(key, out var descriptors)
                    ? descriptors
                    : Array.Empty<AttachmentDescriptor>();
            }

            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>>(result);
        }

        public Task<AttachmentDescriptor?> GetAsync(string serviceId, string layerId, string attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<AttachmentDescriptor?>(null);

        public Task<FeatureAttachmentOperationResult> AddAsync(AddFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureAttachmentOperationResult> UpdateAsync(UpdateFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(DeleteFeatureAttachmentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static string BuildKey(string serviceId, string layerId, string featureId)
            => $"{serviceId}:{layerId}:{featureId}";
    }

    private sealed class InMemoryRasterTileCacheProvider : IRasterTileCacheProvider
    {
        private readonly Dictionary<RasterTileCacheKey, RasterTileCacheEntry> _entries = new();

        public ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                return ValueTask.FromResult<RasterTileCacheHit?>(new RasterTileCacheHit(entry.Content, entry.ContentType, entry.CreatedUtc));
            }

            return ValueTask.FromResult<RasterTileCacheHit?>(null);
        }

        public Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default)
        {
            _entries[key] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
        {
            foreach (var cacheKey in _entries.Keys.Where(k => string.Equals(k.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _entries.Remove(cacheKey);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NullRasterTileCacheMetrics : IRasterTileCacheMetrics
    {
        public void RecordCacheHit(string datasetId, string? variant = null, string? timeSlice = null) { }

        public void RecordCacheMiss(string datasetId, string? variant = null, string? timeSlice = null) { }

        public void RecordRenderLatency(string datasetId, TimeSpan duration, bool fromPreseed) { }

        public void RecordPreseedJobCompleted(RasterTilePreseedJobSnapshot snapshot) { }

        public void RecordPreseedJobFailed(Guid jobId, string? message) { }

        public void RecordPreseedJobCancelled(Guid jobId) { }

        public void RecordCachePurge(string datasetId, bool succeeded) { }
    }

    private sealed class StaticMetadataRegistry : IMetadataRegistry
    {
        public StaticMetadataRegistry(MetadataSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public MetadataSnapshot Snapshot { get; }

        public bool IsInitialized => true;

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Snapshot);

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReloadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IChangeToken GetChangeToken() => TestChangeTokens.Noop;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = Snapshot;
            return true;
        }
    }


    private sealed class TestProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var httpContext = context.HttpContext ?? throw new ArgumentNullException(nameof(context.HttpContext));
            var problem = context.ProblemDetails ?? new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred"
            };

            httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/problem+json";
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NullApiMetrics : Honua.Server.Core.Observability.IApiMetrics
    {
        public void RecordRequest(string apiProtocol, string? serviceId, string? layerId) { }
        public void RecordRequestDuration(string apiProtocol, string? serviceId, string? layerId, TimeSpan duration, int statusCode) { }
        public void RecordError(string apiProtocol, string? serviceId, string? layerId, string errorType) { }
        public void RecordError(string apiProtocol, string? serviceId, string? layerId, Exception exception, string? additionalContext = null) { }
        public void RecordFeatureCount(string apiProtocol, string? serviceId, string? layerId, long count) { }
        public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration) { }
        public void RecordHttpError(string method, string endpoint, int statusCode, string errorType) { }
        public void RecordRateLimitHit(string endpoint, string clientIp) { }
    }

    private sealed class NullCsvExporter : ICsvExporter
    {
        public Task<CsvExportResult> ExportAsync(
            LayerDefinition layer,
            FeatureQuery query,
            IAsyncEnumerable<FeatureRecord> records,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
