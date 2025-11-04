using System;
using System.Collections.Generic;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Shared test fixture for OGC handler tests that provides pre-configured dependencies.
/// This fixture eliminates repetitive setup code across all OGC handler test classes.
/// </summary>
/// <remarks>
/// Use this fixture by implementing IClassFixture&lt;OgcHandlerTestFixture&gt; on your test class.
/// All dependencies are initialized once per test class and reused across test methods.
///
/// Example usage:
/// <code>
/// public class MyOgcTests : IClassFixture&lt;OgcHandlerTestFixture&gt;
/// {
///     private readonly OgcHandlerTestFixture _fixture;
///
///     public MyOgcTests(OgcHandlerTestFixture fixture)
///     {
///         _fixture = fixture;
///     }
///
///     [Fact]
///     public async Task MyTest()
///     {
///         var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");
///         // Use _fixture.Registry, _fixture.Resolver, etc.
///     }
/// }
/// </code>
/// </remarks>
public sealed class OgcHandlerTestFixture : IDisposable
{
    /// <summary>
    /// Gets the metadata registry containing test service and layer definitions.
    /// Includes the "roads" service with "roads-primary" layer for testing.
    /// </summary>
    public IMetadataRegistry Registry { get; }

    /// <summary>
    /// Gets the feature context resolver for resolving feature contexts.
    /// </summary>
    public IFeatureContextResolver Resolver { get; }

    /// <summary>
    /// Gets the feature repository containing test feature data.
    /// Includes 2 road features with LineString geometries.
    /// </summary>
    public IFeatureRepository Repository { get; }

    /// <summary>
    /// Gets the GeoPackage exporter stub (throws NotSupportedException on use).
    /// </summary>
    public IGeoPackageExporter GeoPackageExporter { get; }

    /// <summary>
    /// Gets the Shapefile exporter stub (throws NotSupportedException on use).
    /// </summary>
    public IShapefileExporter ShapefileExporter { get; }

    /// <summary>
    /// Gets the FlatGeobuf exporter (fully functional).
    /// </summary>
    public IFlatGeobufExporter FlatGeobufExporter { get; }

    /// <summary>
    /// Gets the GeoArrow exporter (fully functional).
    /// </summary>
    public IGeoArrowExporter GeoArrowExporter { get; }

    /// <summary>
    /// Gets the CSV exporter stub (throws NotSupportedException on use).
    /// </summary>
    public ICsvExporter CsvExporter { get; }

    /// <summary>
    /// Gets the PMTiles exporter (fully functional).
    /// </summary>
    public IPmTilesExporter PmTilesExporter { get; }

    /// <summary>
    /// Gets the attachment orchestrator stub.
    /// Returns empty attachment lists by default; can be customized per test.
    /// </summary>
    public IFeatureAttachmentOrchestrator AttachmentOrchestrator { get; }

    /// <summary>
    /// Gets the API metrics service (no-op implementation).
    /// </summary>
    public IApiMetrics ApiMetrics { get; }

    /// <summary>
    /// Gets the cache header service with default cache durations.
    /// </summary>
    public OgcCacheHeaderService CacheHeaderService { get; }

    /// <summary>
    /// Gets the metadata snapshot used by the registry.
    /// Useful for tests that need to inspect service/layer definitions.
    /// </summary>
    public MetadataSnapshot Snapshot { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OgcHandlerTestFixture"/> class.
    /// All dependencies are created once and reused across all tests in the class.
    /// </summary>
    public OgcHandlerTestFixture()
        : this(attachmentsEnabled: false, exposeOgcLinks: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OgcHandlerTestFixture"/> class
    /// with customizable attachment settings.
    /// </summary>
    /// <param name="attachmentsEnabled">Whether attachments are enabled on the test layer.</param>
    /// <param name="exposeOgcLinks">Whether OGC attachment links are exposed.</param>
    protected OgcHandlerTestFixture(bool attachmentsEnabled, bool exposeOgcLinks)
    {
        // Create metadata snapshot with test service and layer
        Snapshot = OgcTestUtilities.CreateSnapshot(attachmentsEnabled, exposeOgcLinks);

        // Initialize core dependencies
        Registry = OgcTestUtilities.CreateRegistry(Snapshot);
        Resolver = OgcTestUtilities.CreateResolver(Registry);
        Repository = OgcTestUtilities.CreateRepository();

        // Initialize exporters
        GeoPackageExporter = OgcTestUtilities.CreateGeoPackageExporterStub();
        ShapefileExporter = OgcTestUtilities.CreateShapefileExporterStub();
        FlatGeobufExporter = OgcTestUtilities.CreateFlatGeobufExporter();
        GeoArrowExporter = OgcTestUtilities.CreateGeoArrowExporter();
        CsvExporter = OgcTestUtilities.CreateCsvExporter();
        PmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();

        // Initialize supporting services
        AttachmentOrchestrator = OgcTestUtilities.CreateAttachmentOrchestratorStub();
        ApiMetrics = OgcTestUtilities.CreateApiMetrics();
        CacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
    }

    /// <summary>
    /// Creates a test HTTP context with the specified path and query string.
    /// </summary>
    /// <param name="path">The request path (e.g., "/ogc/collections/roads::roads-primary/items").</param>
    /// <param name="queryString">The query string without leading '?' (e.g., "f=geojson&amp;limit=10").</param>
    /// <returns>A configured <see cref="DefaultHttpContext"/> ready for testing.</returns>
    /// <remarks>
    /// The returned context has:
    /// - Scheme: https
    /// - Host: localhost:5001
    /// - Method: GET
    /// - Response.Body: MemoryStream (initialized and ready for reading)
    /// - Services: Logging and IProblemDetailsService
    /// </remarks>
    public DefaultHttpContext CreateHttpContext(string path, string queryString)
    {
        return OgcTestUtilities.CreateHttpContext(path, queryString);
    }

    /// <summary>
    /// Creates a custom attachment orchestrator with predefined attachments for testing.
    /// </summary>
    /// <param name="attachments">
    /// Dictionary mapping feature keys (format: "serviceId:layerId:featureId") to attachment lists.
    /// </param>
    /// <returns>An <see cref="IFeatureAttachmentOrchestrator"/> configured with the specified attachments.</returns>
    /// <example>
    /// <code>
    /// var attachments = new Dictionary&lt;string, IReadOnlyList&lt;AttachmentDescriptor&gt;&gt;
    /// {
    ///     ["roads:roads-primary:1"] = new[]
    ///     {
    ///         new AttachmentDescriptor { Id = "att1", Name = "photo.jpg", ContentType = "image/jpeg" }
    ///     }
    /// };
    /// var orchestrator = fixture.CreateAttachmentOrchestrator(attachments);
    /// </code>
    /// </example>
    public IFeatureAttachmentOrchestrator CreateAttachmentOrchestrator(
        IDictionary<string, IReadOnlyList<AttachmentDescriptor>> attachments)
    {
        return OgcTestUtilities.CreateAttachmentOrchestratorStub(attachments);
    }

    /// <summary>
    /// Creates a custom metadata registry with a different snapshot.
    /// Useful for tests that need to override default metadata.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to use.</param>
    /// <returns>An <see cref="IMetadataRegistry"/> configured with the specified snapshot.</returns>
    public IMetadataRegistry CreateRegistry(MetadataSnapshot snapshot)
    {
        return OgcTestUtilities.CreateRegistry(snapshot);
    }

    /// <summary>
    /// Disposes resources used by the fixture.
    /// </summary>
    public void Dispose()
    {
        // Currently no resources to dispose
        // This is here for future-proofing if we add disposable resources
    }
}
