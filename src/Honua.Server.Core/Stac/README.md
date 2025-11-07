# STAC (SpatioTemporal Asset Catalog) Module

This module provides a complete implementation of the STAC API specification for cataloging and searching geospatial assets across space and time.

## STAC Specification Support

**STAC Version:** 1.0.0

**STAC API Version:** 1.0.0

### Implemented Conformance Classes

The implementation conforms to the following STAC API specification conformance classes:

| Conformance Class | URI | Description |
|-------------------|-----|-------------|
| **Core** | `https://api.stacspec.org/v1.0.0/core` | Landing page, conformance, basic API structure |
| **Collections** | `https://api.stacspec.org/v1.0.0/collections` | Collection enumeration and metadata |
| **Item Search** | `https://api.stacspec.org/v1.0.0/item-search` | Spatial/temporal search via /search endpoint |
| **Fields Extension** | `https://api.stacspec.org/v1.0.0/item-search#fields` | Field filtering for reducing response payload |
| **Sort Extension** | `https://api.stacspec.org/v1.0.0/item-search#sort` | Sort ordering for search results |
| **Filter Extension** | `https://api.stacspec.org/v1.0.0/item-search#filter` | Advanced filtering capabilities |
| **CQL2-JSON** | `http://www.opengis.net/spec/cql2/1.0/conf/cql2-json` | JSON format for CQL2 filter expressions |

### CQL2 Implementation Status

**Implemented Operators:**
- **Logical:** AND, OR, NOT
- **Comparison:** =, <>, <, <=, >, >=
- **Null checks:** IS NULL, IS NOT NULL
- **Pattern matching:** LIKE
- **Range:** BETWEEN, IN
- **Spatial:** s_intersects, s_equals, s_disjoint, s_touches, s_within, s_overlaps, s_crosses, s_contains
- **Temporal:** t_intersects, t_equals, t_disjoint, t_before, t_after, t_meets, t_during, t_overlaps, anyinteracts

**Not Implemented:**
- Arithmetic operators (+, -, *, /)
- Array operations
- Function calls (casei, etc.)
- Full spatial operator set

## Architecture

### Core Components

```
Stac/
├── IStacCatalogStore.cs          # Storage abstraction interface
├── StacTypes.cs                  # Core STAC types (Item, Collection, Links, Assets)
├── StacSearchOptions.cs          # Search configuration options
├── StacCollectionRecord.cs       # Collection metadata record
├── StacItemRecord.cs             # Item metadata record
├── Storage/                      # Storage implementations
│   ├── RelationalStacCatalogStore.cs     # Base relational DB implementation
│   ├── PostgresStacCatalogStore.cs       # PostgreSQL-specific
│   ├── MySqlStacCatalogStore.cs          # MySQL-specific
│   ├── SqlServerStacCatalogStore.cs      # SQL Server-specific
│   ├── SqliteStacCatalogStore.cs         # SQLite-specific
│   └── InMemoryStacCatalogStore.cs       # In-memory for testing
├── Cql2/                         # CQL2 filter support
│   ├── Cql2Parser.cs             # CQL2-JSON parser
│   ├── Cql2Expression.cs         # Expression tree model
│   ├── Cql2SqlQueryBuilder.cs    # SQL query generation
│   └── StacFilterIntegration.cs  # Integration with STAC search
├── VectorStacCatalogBuilder.cs   # Vector dataset cataloging
├── VectorStacCatalogSynchronizer.cs  # Vector catalog sync
├── RasterStacCatalogBuilder.cs   # Raster dataset cataloging
├── RasterStacCatalogSynchronizer.cs  # Raster catalog sync
├── GeometryParser.cs             # GeoJSON geometry parsing
├── FieldsParser.cs               # Fields extension parser
├── FieldsFilter.cs               # Response field filtering
├── StacSortParser.cs             # Sort parameter parser
└── StacCatalogStoreFactory.cs    # Storage factory
```

### Storage Architecture

#### RelationalStacCatalogStore (Base Class)

The `RelationalStacCatalogStore` is an abstract base class providing:

**Features:**
- Transactional CRUD operations for collections and items
- Spatial indexing with bbox filtering
- Temporal indexing for datetime queries
- Optimized bulk insert operations
- Soft delete support
- ETag-based optimistic concurrency
- Streaming search for large result sets
- Database-agnostic SQL with provider-specific optimizations

**Database Tables:**

```sql
-- STAC Collections
CREATE TABLE stac_collections (
    id TEXT PRIMARY KEY,
    title TEXT,
    description TEXT,
    license TEXT NOT NULL,
    version TEXT,
    keywords_json TEXT,
    extent_json TEXT,
    properties_json TEXT,
    links_json TEXT,
    extensions_json TEXT,
    conforms_to TEXT,
    data_source_id TEXT,
    service_id TEXT,
    layer_id TEXT,
    etag TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    deleted_at TEXT,
    deleted_by TEXT
);

-- STAC Items
CREATE TABLE stac_items (
    collection_id TEXT NOT NULL,
    id TEXT NOT NULL,
    title TEXT,
    description TEXT,
    properties_json TEXT,
    assets_json TEXT,
    links_json TEXT,
    extensions_json TEXT,
    bbox_json TEXT,
    geometry_json TEXT,
    datetime TEXT,
    start_datetime TEXT,
    end_datetime TEXT,
    raster_dataset_id TEXT,
    etag TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    deleted_at TEXT,
    deleted_by TEXT,
    PRIMARY KEY (collection_id, id)
);

-- Indexes for performance
CREATE INDEX idx_stac_items_datetime ON stac_items(datetime);
CREATE INDEX idx_stac_items_collection ON stac_items(collection_id);
```

#### Database-Specific Implementations

**PostgresStacCatalogStore:**
- Native PostGIS spatial operations
- JSONB for efficient property queries
- GiST indexes for spatial queries
- Optimized bulk insert with COPY

**MySqlStacCatalogStore:**
- Spatial extensions for geometry operations
- JSON functions for property queries
- Bulk insert optimization

**SqlServerStacCatalogStore:**
- SQL Server spatial types (geography)
- JSON_VALUE for property extraction
- OFFSET/FETCH for pagination

**SqliteStacCatalogStore:**
- SpatiaLite extension for spatial operations
- JSON1 extension for JSON queries
- Lightweight for development/testing

**InMemoryStacCatalogStore:**
- Thread-safe in-memory storage
- Used for testing and development
- No persistence

### Catalog Storage Interface

```csharp
public interface IStacCatalogStore
{
    // Initialization
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    // Collections
    Task UpsertCollectionAsync(StacCollectionRecord collection, string? expectedETag = null, CancellationToken cancellationToken = default);
    Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StacCollectionRecord>> ListCollectionsAsync(CancellationToken cancellationToken = default);
    Task<StacCollectionListResult> ListCollectionsAsync(int limit, string? token = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    // Soft Delete
    Task<bool> SoftDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default);
    Task<bool> RestoreCollectionAsync(string collectionId, CancellationToken cancellationToken = default);
    Task<bool> HardDeleteCollectionAsync(string collectionId, string? deletedBy, CancellationToken cancellationToken = default);

    // Items
    Task UpsertItemAsync(StacItemRecord item, string? expectedETag = null, CancellationToken cancellationToken = default);
    Task<BulkUpsertResult> BulkUpsertItemsAsync(IReadOnlyList<StacItemRecord> items, BulkUpsertOptions? options = null, CancellationToken cancellationToken = default);
    Task<StacItemRecord?> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StacItemRecord>> ListItemsAsync(string collectionId, int limit, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);

    // Soft Delete for Items
    Task<bool> SoftDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default);
    Task<bool> RestoreItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);
    Task<bool> HardDeleteItemAsync(string collectionId, string itemId, string? deletedBy, CancellationToken cancellationToken = default);

    // Search
    Task<StacSearchResult> SearchAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StacItemRecord> SearchStreamAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default);
}
```

## Features

### 1. Collection Management

**STAC Collections** represent groups of related STAC Items:

```csharp
var collection = new StacCollectionRecord
{
    Id = "landsat-8",
    Title = "Landsat 8 Imagery",
    Description = "Landsat 8 satellite imagery collection",
    License = "CC-BY-4.0",
    Extent = new StacExtent
    {
        Spatial = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
        Temporal = new[]
        {
            new StacTemporalInterval
            {
                Start = DateTimeOffset.Parse("2013-04-11T00:00:00Z"),
                End = null // ongoing
            }
        }
    },
    Keywords = new[] { "landsat", "satellite", "imagery" }
};

await store.UpsertCollectionAsync(collection);
```

### 2. Item Management

**STAC Items** represent individual assets (scenes, features, etc.):

```csharp
var item = new StacItemRecord
{
    Id = "LC08_L1TP_001002_20240101_20240105_02_T1",
    CollectionId = "landsat-8",
    Datetime = DateTimeOffset.Parse("2024-01-01T10:30:00Z"),
    Geometry = /* GeoJSON geometry */,
    Bbox = new[] { -122.5, 37.7, -122.3, 37.9 },
    Properties = new JsonObject
    {
        ["eo:cloud_cover"] = 5.2,
        ["platform"] = "landsat-8",
        ["instruments"] = new JsonArray { "OLI", "TIRS" }
    },
    Assets = new Dictionary<string, StacAsset>
    {
        ["thumbnail"] = new StacAsset
        {
            Href = "https://example.com/thumbnail.jpg",
            Type = "image/jpeg",
            Roles = new[] { "thumbnail" }
        },
        ["visual"] = new StacAsset
        {
            Href = "https://example.com/visual.tif",
            Type = "image/tiff; application=geotiff",
            Roles = new[] { "visual" }
        }
    }
};

await store.UpsertItemAsync(item);
```

### 3. Search Capabilities

#### Spatial Search

```csharp
// Bounding box search
var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    Bbox = new[] { -122.5, 37.7, -122.3, 37.9 },
    Limit = 100
};

var results = await store.SearchAsync(parameters);
```

#### Temporal Search

```csharp
// Date range search
var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    Start = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
    End = DateTimeOffset.Parse("2024-12-31T23:59:59Z"),
    Limit = 100
};

var results = await store.SearchAsync(parameters);
```

#### Advanced Filtering with CQL2

```csharp
var filterJson = @"{
  ""op"": ""and"",
  ""args"": [
    {
      ""op"": ""<"",
      ""args"": [
        {""property"": ""eo:cloud_cover""},
        10
      ]
    },
    {
      ""op"": ""s_intersects"",
      ""args"": [
        {""property"": ""geometry""},
        {
          ""type"": ""Polygon"",
          ""coordinates"": [[
            [-122.5, 37.7],
            [-122.3, 37.7],
            [-122.3, 37.9],
            [-122.5, 37.9],
            [-122.5, 37.7]
          ]]
        }
      ]
    }
  ]
}";

var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    Filter = filterJson,
    FilterLang = "cql2-json",
    Limit = 50
};

var results = await store.SearchAsync(parameters);
```

#### Full-Text Search

Property-based search using CQL2:

```csharp
var filterJson = @"{
  ""op"": ""like"",
  ""args"": [
    {""property"": ""title""},
    ""%San Francisco%""
  ]
}";

var parameters = new StacSearchParameters
{
    Filter = filterJson,
    FilterLang = "cql2-json"
};
```

#### Sorting Results

```csharp
var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    SortBy = new[]
    {
        new StacSortField
        {
            Field = "datetime",
            Direction = StacSortDirection.Descending
        },
        new StacSortField
        {
            Field = "properties.eo:cloud_cover",
            Direction = StacSortDirection.Ascending
        }
    }
};
```

#### Field Filtering (Fields Extension)

Reduce response size by selecting specific fields:

```csharp
// Only include id, geometry, and specific properties
var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    // Note: Fields filtering is applied at API layer
};

// At API layer (Host/Stac):
var fieldsSpec = FieldsParser.ParseGetFields("id,geometry,properties.datetime,properties.eo:cloud_cover");
var filteredResults = FieldsFilter.ApplyFieldsFilter(results, fieldsSpec);
```

### 4. Asset Management

**STAC Assets** represent the actual data files:

```csharp
var assets = new Dictionary<string, StacAsset>
{
    ["B1"] = new StacAsset
    {
        Href = "https://storage.example.com/LC08_B1.tif",
        Title = "Band 1 - Coastal/Aerosol",
        Type = "image/tiff; application=geotiff; profile=cloud-optimized",
        Roles = new[] { "data" },
        Properties = new JsonObject
        {
            ["eo:bands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "B1",
                    ["common_name"] = "coastal",
                    ["center_wavelength"] = 0.44,
                    ["full_width_half_max"] = 0.02
                }
            }
        }
    },
    ["metadata"] = new StacAsset
    {
        Href = "https://storage.example.com/LC08_MTL.txt",
        Title = "Metadata",
        Type = "text/plain",
        Roles = new[] { "metadata" }
    }
};
```

### 5. Bulk Operations

Efficient bulk insert for large catalogs:

```csharp
var items = new List<StacItemRecord>();
// ... populate items ...

var options = new BulkUpsertOptions
{
    BatchSize = 1000,
    SkipValidation = false
};

var result = await store.BulkUpsertItemsAsync(items, options);

Console.WriteLine($"Inserted: {result.SuccessCount}");
Console.WriteLine($"Failed: {result.FailureCount}");
```

### 6. Streaming Search

For large result sets, use streaming to avoid loading all results into memory:

```csharp
var parameters = new StacSearchParameters
{
    Collections = new[] { "landsat-8" },
    Limit = 10000 // Large result set
};

await foreach (var item in store.SearchStreamAsync(parameters, cancellationToken))
{
    // Process item
    Console.WriteLine($"Processing {item.Id}");
}
```

### 7. Temporal Indexing

Efficient temporal queries using indexed datetime columns:

```csharp
// Single datetime
var item = new StacItemRecord
{
    Id = "scene-1",
    CollectionId = "sentinel-2",
    Datetime = DateTimeOffset.UtcNow
};

// Date range (for items spanning time)
var item = new StacItemRecord
{
    Id = "video-1",
    CollectionId = "videos",
    StartDatetime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
    EndDatetime = DateTimeOffset.Parse("2024-01-01T01:00:00Z")
};
```

## API Integration

The STAC module integrates with the API layer in `/src/Honua.Server.Host/Stac/`:

### API Endpoints

**Base Path:** `/stac` or `/v1/stac`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/stac` | GET | STAC catalog root |
| `/stac/conformance` | GET | Conformance declaration |
| `/stac/collections` | GET | List collections |
| `/stac/collections` | POST | Create collection |
| `/stac/collections/{collectionId}` | GET | Get collection |
| `/stac/collections/{collectionId}` | PUT | Update collection |
| `/stac/collections/{collectionId}` | DELETE | Delete collection |
| `/stac/collections/{collectionId}/items` | GET | List items in collection |
| `/stac/collections/{collectionId}/items` | POST | Create item |
| `/stac/collections/{collectionId}/items/{itemId}` | GET | Get item |
| `/stac/collections/{collectionId}/items/{itemId}` | PUT | Update item |
| `/stac/collections/{collectionId}/items/{itemId}` | DELETE | Delete item |
| `/stac/search` | GET | Search items (query params) |
| `/stac/search` | POST | Search items (JSON body) |

### API Controllers

- **`StacCatalogController`** - Catalog root and conformance
- **`StacCollectionsController`** - Collection CRUD operations
- **`StacSearchController`** - Search operations (GET/POST)

## Integration with OGC APIs

STAC and OGC API - Features are closely related standards:

### Key Differences

| Aspect | STAC API | OGC API - Features |
|--------|----------|-------------------|
| **Primary Use Case** | Asset cataloging and search | Feature data access |
| **Search Endpoint** | `/stac/search` (cross-collection) | `/ogc/search` (cross-collection) |
| **Data Model** | STAC Items (assets, datetime) | GeoJSON Features |
| **Filtering** | CQL2-JSON via filter parameter | CQL2-JSON + bbox/datetime params |
| **Extensions** | STAC Extensions (eo, sar, etc.) | OGC API extensions |

### Shared Components

Both STAC and OGC implementations share:
- **CQL2 Parser** (`Cql2Parser.cs`)
- **Geometry Parser** (`GeometryParser.cs`)
- **Database Storage** (RelationalStacCatalogStore can back both)

### Integration Pattern

```csharp
// STAC for asset cataloging
var stacItem = new StacItemRecord
{
    Id = "scene-123",
    CollectionId = "landsat-8",
    Datetime = DateTimeOffset.UtcNow,
    Assets = new Dictionary<string, StacAsset>
    {
        ["visual"] = new StacAsset { Href = "https://..." }
    }
};

// OGC Features for vector data access
// The same scene might be referenced as a feature
// in an OGC API - Features collection
```

## Configuration

### Search Options

```csharp
services.AddSingleton(new StacSearchOptions
{
    // Count query timeout
    CountTimeoutSeconds = 5,
    UseCountEstimation = true,
    MaxExactCountThreshold = 100_000,

    // Large result sets
    SkipCountForLargeResultSets = true,
    SkipCountLimitThreshold = 1000,

    // Streaming
    StreamingPageSize = 100,
    MaxStreamingItems = 100_000,
    EnableAutoStreaming = true,
    StreamingThreshold = 1000
});
```

### Database Selection

```csharp
// PostgreSQL (recommended for production)
services.AddSingleton<IStacCatalogStore>(provider =>
    new PostgresStacCatalogStore(connectionString, logger));

// MySQL
services.AddSingleton<IStacCatalogStore>(provider =>
    new MySqlStacCatalogStore(connectionString, logger));

// SQL Server
services.AddSingleton<IStacCatalogStore>(provider =>
    new SqlServerStacCatalogStore(connectionString, logger));

// SQLite (development)
services.AddSingleton<IStacCatalogStore>(provider =>
    new SqliteStacCatalogStore(connectionString, logger));

// In-Memory (testing)
services.AddSingleton<IStacCatalogStore, InMemoryStacCatalogStore>();
```

## Usage Examples

### Example 1: Creating a Collection

```csharp
var store = serviceProvider.GetRequiredService<IStacCatalogStore>();
await store.EnsureInitializedAsync();

var collection = new StacCollectionRecord
{
    Id = "sentinel-2-l2a",
    Title = "Sentinel-2 Level-2A",
    Description = "Sentinel-2 Level-2A atmospherically corrected surface reflectance",
    License = "proprietary",
    Keywords = new[] { "sentinel", "copernicus", "esa", "msi", "surface reflectance" },
    Extent = new StacExtent
    {
        Spatial = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
        Temporal = new[]
        {
            new StacTemporalInterval
            {
                Start = DateTimeOffset.Parse("2017-03-28T00:00:00Z"),
                End = null
            }
        }
    }
};

await store.UpsertCollectionAsync(collection);
```

### Example 2: Adding Items to a Collection

```csharp
var items = new List<StacItemRecord>();

for (int i = 0; i < 100; i++)
{
    items.Add(new StacItemRecord
    {
        Id = $"S2A_MSIL2A_20240101_{i:D3}",
        CollectionId = "sentinel-2-l2a",
        Datetime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").AddMinutes(i),
        Geometry = /* GeoJSON */,
        Bbox = new[] { -122.5 + i * 0.01, 37.7, -122.4 + i * 0.01, 37.8 },
        Properties = new JsonObject
        {
            ["eo:cloud_cover"] = Random.Shared.NextDouble() * 100,
            ["platform"] = "sentinel-2a",
            ["constellation"] = "sentinel-2"
        },
        Assets = new Dictionary<string, StacAsset>
        {
            ["visual"] = new StacAsset
            {
                Href = $"https://storage.example.com/scene_{i}.tif",
                Type = "image/tiff; application=geotiff; profile=cloud-optimized",
                Roles = new[] { "visual" }
            }
        }
    });
}

var result = await store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
{
    BatchSize = 100
});
```

### Example 3: Searching the Catalog

```csharp
// Search for low-cloud scenes in a specific area
var parameters = new StacSearchParameters
{
    Collections = new[] { "sentinel-2-l2a" },
    Bbox = new[] { -122.5, 37.7, -122.3, 37.9 },
    Start = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
    End = DateTimeOffset.Parse("2024-12-31T23:59:59Z"),
    Filter = @"{
        ""op"": ""<"",
        ""args"": [
            {""property"": ""eo:cloud_cover""},
            10
        ]
    }",
    FilterLang = "cql2-json",
    SortBy = new[]
    {
        new StacSortField
        {
            Field = "properties.eo:cloud_cover",
            Direction = StacSortDirection.Ascending
        }
    },
    Limit = 50
};

var results = await store.SearchAsync(parameters);

Console.WriteLine($"Found {results.Matched} matching items");
foreach (var item in results.Items)
{
    var cloudCover = item.Properties?["eo:cloud_cover"]?.GetValue<double>() ?? 0;
    Console.WriteLine($"{item.Id}: {cloudCover:F2}% cloud cover");
}
```

### Example 4: Streaming Large Result Sets

```csharp
var parameters = new StacSearchParameters
{
    Collections = new[] { "sentinel-2-l2a" },
    Start = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
    End = DateTimeOffset.Parse("2024-12-31T23:59:59Z"),
    Limit = 100000 // Very large
};

int count = 0;
await foreach (var item in store.SearchStreamAsync(parameters, cancellationToken))
{
    // Process item without loading entire result set
    count++;

    if (count % 1000 == 0)
    {
        Console.WriteLine($"Processed {count} items...");
    }
}

Console.WriteLine($"Total items processed: {count}");
```

## Performance Considerations

### Indexing

Ensure proper database indexes:
```sql
-- Temporal queries
CREATE INDEX idx_stac_items_datetime ON stac_items(datetime);
CREATE INDEX idx_stac_items_start_end ON stac_items(start_datetime, end_datetime);

-- Collection filtering
CREATE INDEX idx_stac_items_collection ON stac_items(collection_id);

-- Spatial queries (PostgreSQL with PostGIS)
CREATE INDEX idx_stac_items_geom ON stac_items USING GIST((geometry_json::geometry));
```

### Bulk Operations

Use bulk insert for large datasets:
```csharp
// Bad: Individual inserts
foreach (var item in items)
{
    await store.UpsertItemAsync(item); // Slow!
}

// Good: Bulk insert
await store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
{
    BatchSize = 1000
});
```

### Streaming

Use streaming for large result sets:
```csharp
// Bad: Load all results
var results = await store.SearchAsync(new StacSearchParameters { Limit = 100000 });
// All 100k items in memory!

// Good: Stream results
await foreach (var item in store.SearchStreamAsync(parameters))
{
    // Process one at a time
}
```

## Testing

The module includes comprehensive test coverage in:
- `/tests/Honua.Server.Core.Tests.Apis/Stac/`
- `/tests/Honua.Server.Host.Tests/Stac/`
- `/tests/Honua.Server.Core.Tests.OgcProtocols/Hosting/StacEndpointTests.cs`

## Related Specifications

- [STAC Specification](https://github.com/radiantearth/stac-spec)
- [STAC API Specification](https://github.com/radiantearth/stac-api-spec)
- [CQL2 Specification](https://docs.ogc.org/DRAFTS/21-065.html)
- [STAC Extensions](https://github.com/stac-extensions)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
