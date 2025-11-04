# Test Infrastructure Stubs

This directory contains centralized stub implementations extracted from test files throughout the HonuaIO test suite. These stubs provide reusable, configurable implementations of core interfaces for testing purposes.

## Overview

Stubs in this directory follow these principles:

1. **Reusable**: Designed to be shared across multiple test files
2. **Configurable**: Allow test-specific customization via constructors or methods
3. **Well-documented**: Include XML documentation explaining behavior and usage
4. **Consistent naming**: Follow patterns like `Stub*`, `Null*`, `NoOp*`, or `InMemory*`

## Available Stubs

### Feature Repositories

#### `StubFeatureRepository`
In-memory implementation of `IFeatureRepository` with configurable test data and query filtering.

**Default Data:**
- `roads-primary` layer: 3 road features with LineString geometries
- `roads-inspections` layer: 3 inspection features with Point geometries

**Supported Operations:**
- Query with filtering (WHERE, spatial, temporal)
- Sorting and pagination
- Count and single feature retrieval
- Read-only (mutations throw `NotSupportedException`)

**Usage Example:**
```csharp
// Use default data
var repository = new StubFeatureRepository();

// Or configure custom data
var customData = new Dictionary<string, IReadOnlyList<FeatureRecord>>
{
    ["my-layer"] = new[] { feature1, feature2 }
};
var repository = new StubFeatureRepository(customData);

// Or add data after construction
repository.SetFeatures("another-layer", feature3, feature4);
```

**When to Use:**
- Integration tests requiring feature data
- Testing query parameter parsing
- Testing response formatting
- Cases where you need controlled, predictable data

**Alternative:** For write operations (Create/Update/Delete), use `InMemoryEditableFeatureRepository` from the parent `TestInfrastructure` directory.

---

### Metadata

#### `StaticMetadataRegistry`
Implementation of `IMetadataRegistry` that returns a fixed snapshot.

**Characteristics:**
- Always returns the same snapshot provided at construction
- Reports as initialized immediately
- Ignores reload and update requests
- Returns a no-op change token

**Usage Example:**
```csharp
var snapshot = CreateTestSnapshot(); // Your test metadata
var registry = new StaticMetadataRegistry(snapshot);

// Use in services that need IMetadataRegistry
var service = new MyService(registry);
```

**When to Use:**
- Tests that need stable metadata throughout execution
- Avoiding complexity of full metadata provider infrastructure
- Unit tests focused on other concerns than metadata loading

---

### Attachments

#### `StubAttachmentOrchestrator`
In-memory implementation of `IFeatureAttachmentOrchestrator` with configurable attachment data.

**Supported Operations:**
- List attachments by feature (returns pre-configured data)
- Get individual attachment (returns null)
- Write operations throw `NotSupportedException`

**Usage Example:**
```csharp
// Empty stub
var orchestrator = new StubAttachmentOrchestrator();

// With pre-configured attachments
var attachments = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>
{
    ["roads:roads-primary:1"] = new[] { attachment1, attachment2 }
};
var orchestrator = new StubAttachmentOrchestrator(attachments);

// Or add attachments after construction
orchestrator.AddAttachments("roads", "roads-primary", "1", attachment1, attachment2);
```

**When to Use:**
- Testing attachment listing and metadata
- Testing OGC API Features with attachment links
- Cases where file upload/download isn't needed

---

### Exporters

#### `NullGeoPackageExporter`
#### `NullShapefileExporter`
#### `NullCsvExporter`

Null implementations that throw `NotSupportedException` for all operations.

**When to Use:**
- Tests that configure export services but don't execute exports
- Dependency injection when exports aren't needed
- Reducing test setup complexity

**Usage Example:**
```csharp
services.AddSingleton<IGeoPackageExporter>(new NullGeoPackageExporter());
services.AddSingleton<IShapefileExporter>(new NullShapefileExporter());
services.AddSingleton<ICsvExporter>(new NullCsvExporter());
```

---

### Raster Caching

#### `InMemoryRasterTileCacheProvider`
Full-featured in-memory implementation of `IRasterTileCacheProvider`.

**Capabilities:**
- Store and retrieve cached tiles
- Remove individual tiles
- Purge all tiles for a dataset
- Thread-safe operations
- Additional test utilities: `Count` property and `Clear()` method

**Usage Example:**
```csharp
var cache = new InMemoryRasterTileCacheProvider();

// Store a tile
var entry = new RasterTileCacheEntry(tileBytes, "image/png", DateTime.UtcNow);
await cache.StoreAsync(cacheKey, entry);

// Retrieve a tile
var hit = await cache.TryGetAsync(cacheKey);
if (hit is not null)
{
    var tileData = hit.Content;
}

// Test assertions
Assert.Equal(1, cache.Count);

// Cleanup
cache.Clear();
```

**When to Use:**
- Testing raster tile caching logic
- Integration tests for tile rendering
- Avoiding file system dependencies in tests

---

### Data Store Providers

#### `StubDataStoreProvider`
#### `StubDataStoreProviderFactory`

Stub provider that throws `NotSupportedException` for most operations.

**Characteristics:**
- Reports full capabilities via `TestDataStoreCapabilities`
- All query/CRUD operations throw exceptions
- Connectivity check returns success
- MVT tile generation returns null

**Usage Example:**
```csharp
// Single provider
var provider = new StubDataStoreProvider("memory");

// Factory
var factory = new StubDataStoreProviderFactory();
var provider = factory.Create("stub");
```

**When to Use:**
- Satisfying `IDataStoreProvider` dependencies when not actually querying data
- Testing configuration and initialization logic
- Unit tests focused on other concerns

**Note:** For functional data operations, use `StubFeatureRepository` or `InMemoryEditableFeatureRepository` instead.

---

### Metrics

#### `NullRasterTileCacheMetrics`
#### `NullApiMetrics`

No-op implementations that ignore all metric recording calls.

**Usage Example:**
```csharp
// Use singleton instances
services.AddSingleton<IRasterTileCacheMetrics>(NullRasterTileCacheMetrics.Instance);
services.AddSingleton<IApiMetrics>(NullApiMetrics.Instance);
```

**When to Use:**
- Tests that don't need to verify metric collection
- Reducing test complexity
- Avoiding metric infrastructure setup

---

### Output Caching

#### `NoOpOutputCacheInvalidationService`
#### `NoOpOutputCacheStore`

No-op implementations for ASP.NET Core output caching.

**Characteristics:**
- All operations complete immediately
- Store returns empty data
- No actual caching behavior

**Usage Example:**
```csharp
services.AddSingleton<IOutputCacheInvalidationService>(
    NoOpOutputCacheInvalidationService.Instance);
services.AddSingleton<IOutputCacheStore>(
    NoOpOutputCacheStore.Instance);
```

**When to Use:**
- Integration tests with WebApplicationFactory
- Tests that need output caching infrastructure but don't test caching behavior
- Avoiding Redis or other cache provider dependencies

---

## Naming Conventions

| Prefix/Pattern | Meaning | Example |
|----------------|---------|---------|
| `Stub*` | Configurable implementation with test data | `StubFeatureRepository` |
| `Static*` | Returns fixed, unchanging data | `StaticMetadataRegistry` |
| `InMemory*` | Full-featured in-memory implementation | `InMemoryRasterTileCacheProvider` |
| `Null*` | Throws exceptions or returns null | `NullGeoPackageExporter` |
| `NoOp*` | Accepts calls but does nothing | `NoOpOutputCacheStore` |

## Design Patterns

### Read-Only Stubs
Many stubs are intentionally read-only to prevent unintended side effects in tests:
- `StubFeatureRepository` - Use `InMemoryEditableFeatureRepository` for writes
- `StubAttachmentOrchestrator` - Use real implementation for file operations
- `StaticMetadataRegistry` - Use `MetadataRegistry` for dynamic updates

### Configurable Test Data
Stubs support multiple configuration approaches:
1. **Constructor parameters** - Provide initial data
2. **Public methods** - Add/modify data after construction
3. **Factory methods** - Create common test scenarios

### Singleton Pattern for No-Ops
No-op implementations use singletons to avoid allocation overhead:
```csharp
public static readonly NullApiMetrics Instance = new();
```

## Migration Guide

### From Inline Stubs

**Before (GeoservicesRestLeafletTests.cs):**
```csharp
internal sealed class StubFeatureRepository : IFeatureRepository
{
    // 500 lines of implementation...
}

// In test setup:
services.AddSingleton<IFeatureRepository>(_ => new StubFeatureRepository());
```

**After:**
```csharp
using Honua.Server.Core.Tests.TestInfrastructure.Stubs;

// In test setup:
services.AddSingleton<IFeatureRepository>(_ => new StubFeatureRepository());
```

### From OgcTestUtilities

**Before:**
```csharp
var registry = OgcTestUtilities.CreateRegistry();
var repository = OgcTestUtilities.CreateRepository();
var exporter = OgcTestUtilities.CreateGeoPackageExporterStub();
```

**After:**
```csharp
var registry = new StaticMetadataRegistry(snapshot);
var repository = new StubFeatureRepository();
var exporter = new NullGeoPackageExporter();
```

## Testing Best Practices

### 1. Choose the Right Stub
- Need write operations? → `InMemoryEditableFeatureRepository`
- Need read-only query testing? → `StubFeatureRepository`
- Need to throw exceptions? → `Null*` stubs
- Need to ignore calls? → `NoOp*` stubs

### 2. Configure for Your Test
```csharp
// Good - Minimal configuration
var repository = new StubFeatureRepository();

// Better - Explicit about test data
var repository = new StubFeatureRepository();
repository.SetFeatures("test-layer", testFeature1, testFeature2);

// Best - Clear test intent
var repository = new StubFeatureRepository();
repository.SetFeatures("empty-layer"); // Explicitly empty for negative test
```

### 3. Use Assertions with Test Utilities
```csharp
var cache = new InMemoryRasterTileCacheProvider();

// Perform operations...

// Verify behavior
Assert.Equal(expectedCount, cache.Count);
Assert.NotNull(await cache.TryGetAsync(key));

// Cleanup
cache.Clear();
```

## Future Enhancements

Potential additions to this directory:
- `StubSTACCatalogStore` - In-memory STAC catalog
- `InMemoryAuthenticationStore` - Stub for authentication tests
- `StubStyleRepository` - Configurable style data
- `FakeHttpMessageHandler` - For HTTP client testing

## Related Files

- `TestInfrastructure/InMemoryEditableFeatureRepository.cs` - Writable feature repository
- `TestInfrastructure/TestDataStoreCapabilities.cs` - Capability flags for stubs
- `TestInfrastructure/TestChangeTokens.cs` - No-op change tokens
- `Ogc/OgcTestUtilities.cs` - Factory methods (partially migrated)

## Questions?

If you're unsure which stub to use or need a new stub:
1. Check existing tests for similar scenarios
2. Review the usage examples in this README
3. Consider whether you need read-only or read-write operations
4. Ask in test infrastructure discussions
