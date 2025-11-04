# OgcHandlerTestFixture Usage Guide

## Overview
The `OgcHandlerTestFixture` eliminates ~8 lines of repetitive setup code from every test method in OGC handler tests.

## Before (Old Pattern)
```csharp
public class OgcHandlersGeoJsonTests
{
    [Fact]
    public async Task Items_WithGeoJsonFormat_ShouldReturnFeatureCollection()
    {
        // 8-10 lines of setup repeated in EVERY test method
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = OgcTestUtilities.CreateRepository();
        var geoPackageExporter = OgcTestUtilities.CreateGeoPackageExporterStub();
        var shapefileExporter = OgcTestUtilities.CreateShapefileExporterStub();
        var flatGeobufExporter = OgcTestUtilities.CreateFlatGeobufExporter();
        var geoArrowExporter = OgcTestUtilities.CreateGeoArrowExporter();
        var csvExporter = OgcTestUtilities.CreateCsvExporter();
        var attachmentOrchestrator = OgcTestUtilities.CreateAttachmentOrchestratorStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=2");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            resolver,
            repository,
            geoPackageExporter,
            shapefileExporter,
            flatGeobufExporter,
            geoArrowExporter,
            csvExporter,
            attachmentOrchestrator,
            registry,
            OgcTestUtilities.CreateApiMetrics(),
            OgcTestUtilities.CreateCacheHeaderService(),
            CancellationToken.None);

        // ... assertions ...
    }
}
```

## After (New Pattern with Fixture)
```csharp
public class OgcHandlersGeoJsonTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersGeoJsonTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithGeoJsonFormat_ShouldReturnFeatureCollection()
    {
        // Just create the context - everything else is ready to use
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=2");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        // ... assertions ...
    }
}
```

## Benefits
1. **Reduced LOC**: Eliminates 8-10 lines of setup per test method
2. **Better Performance**: Dependencies created once per test class, not per method
3. **Consistency**: All tests use the same configuration
4. **Maintainability**: Update fixture once to change all tests

## Available Properties
- `Registry` - Pre-configured metadata registry
- `Resolver` - Feature context resolver
- `Repository` - Fake repository with 2 road features
- `GeoPackageExporter` - Stub exporter
- `ShapefileExporter` - Stub exporter
- `FlatGeobufExporter` - Functional exporter
- `GeoArrowExporter` - Functional exporter
- `CsvExporter` - Stub exporter
- `PmTilesExporter` - Functional exporter
- `AttachmentOrchestrator` - Stub orchestrator
- `ApiMetrics` - No-op metrics service
- `CacheHeaderService` - Cache header service

## Helper Methods
```csharp
// Create HTTP context
var context = _fixture.CreateHttpContext("/path", "querystring");

// Create custom attachment orchestrator
var attachments = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>
{
    ["roads:roads-primary:1"] = new[] { new AttachmentDescriptor { ... } }
};
var customOrchestrator = _fixture.CreateAttachmentOrchestrator(attachments);

// Create custom registry with different metadata
var customSnapshot = OgcTestUtilities.CreateSnapshot(attachmentsEnabled: true);
var customRegistry = _fixture.CreateRegistry(customSnapshot);
```

## Test Data
The fixture provides:
- Service: "roads"
- Layer: "roads-primary" (LineString geometry)
- Features: 2 road records
  - Feature 1: road_id=1, name="First"
  - Feature 2: road_id=2, name="Second"
- All OGC export formats enabled
- Style: "primary-roads-line"

## Advanced Usage: Custom Fixture
For tests needing attachments:
```csharp
public class MyCustomFixture : OgcHandlerTestFixture
{
    public MyCustomFixture() : base(attachmentsEnabled: true, exposeOgcLinks: true)
    {
    }
}

public class MyTests : IClassFixture<MyCustomFixture>
{
    // ...
}
```
