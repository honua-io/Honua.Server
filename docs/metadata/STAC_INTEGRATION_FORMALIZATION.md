# STAC Integration Formalization

**Status:** Design Proposal
**Target:** Phase 2 Enhancement
**Last Updated:** 2025-01-07

---

## Overview

This document proposes formalizing the integration between STAC (SpatioTemporal Asset Catalog) metadata and Honua's core semantic metadata model. Currently, STAC uses a semi-separate `RasterDatasetDefinition` entity. This proposal unifies vector and raster STAC metadata while maintaining the semantic mapping principle.

---

## Current Architecture

### Raster STAC Implementation

**Current State:**
- `RasterDatasetDefinition` - Separate entity for raster datasets
- `RasterStacCatalogBuilder` - Builds STAC collections from raster datasets
- `StacApiMapper` - Maps raster datasets to STAC Items
- `StacCatalogController`, `StacCollectionsController`, `StacSearchController` - API endpoints

**Linkage to Core Metadata:**
```csharp
public sealed record RasterDatasetDefinition
{
    public string? ServiceId { get; init; }  // Optional link
    public string? LayerId { get; init; }    // Optional link
    // ... other properties
}
```

**Current Flow:**
1. Define `RasterDatasetDefinition` in metadata
2. Optionally link to `ServiceDefinition` and `LayerDefinition`
3. STAC builder discovers raster datasets
4. STAC API serves Items/Collections from raster metadata

### Vector STAC Support

**Current State:**
- ❌ No direct STAC support for vector layers
- Can serve via OGC API Features (similar semantics to STAC)
- No STAC Items/Collections for vector data

---

## Proposed Unified Architecture

### Design Principle

**Semantic Mapping:**
- Core `LayerDefinition` and `RasterDatasetDefinition` remain protocol-agnostic
- Add optional `stac` extension block to both
- STAC API projects core + STAC metadata to STAC JSON
- Single metadata definition serves multiple protocols

### Option 1: Separate STAC Extension Blocks

**Add `layer.stac` and `raster.stac` extensions:**

```json
{
  "layers": [{
    "id": "roads",
    "title": "Road Network",
    "geometryType": "LineString",
    "extent": {
      "bbox": [[-122.5, 37.7, -122.3, 37.9]],
      "temporal": {
        "interval": [["2024-01-01T00:00:00Z", null]]
      }
    },
    "stac": {
      "enabled": true,
      "collectionId": "transportation-roads",
      "license": "CC-BY-4.0",
      "providers": [
        {
          "name": "City Transportation Department",
          "roles": ["producer", "licensor"],
          "url": "https://transportation.city.gov"
        }
      ],
      "assets": {
        "data": {
          "title": "Road Network GeoJSON",
          "type": "application/geo+json",
          "roles": ["data"]
        }
      },
      "summaries": {
        "road_class": ["highway", "arterial", "collector", "local"]
      },
      "stacExtensions": [
        "https://stac-extensions.github.io/version/v1.0.0/schema.json"
      ],
      "itemAssets": {
        "data": {
          "title": "Feature Data",
          "type": "application/geo+json",
          "roles": ["data"]
        }
      }
    }
  }],
  "rasterDatasets": [{
    "id": "dem",
    "title": "Digital Elevation Model",
    "source": {
      "type": "cog",
      "uri": "/data/dem.tif"
    },
    "extent": {
      "bbox": [[-122.5, 37.7, -122.3, 37.9]]
    },
    "stac": {
      "enabled": true,
      "collectionId": "elevation-dem",
      "license": "CC0-1.0",
      "providers": [
        {
          "name": "USGS",
          "roles": ["producer"],
          "url": "https://usgs.gov"
        }
      ],
      "assets": {
        "cog": {
          "title": "Cloud Optimized GeoTIFF",
          "type": "image/tiff; application=geotiff; profile=cloud-optimized",
          "roles": ["data"],
          "eo:bands": [
            {
              "name": "elevation",
              "description": "Elevation in meters"
            }
          ]
        }
      },
      "stacExtensions": [
        "https://stac-extensions.github.io/eo/v1.1.0/schema.json",
        "https://stac-extensions.github.io/projection/v1.1.0/schema.json"
      ]
    }
  }]
}
```

### Option 2: Unified STAC Metadata at Catalog Level

**Alternative: Catalog-level STAC configuration:**

```json
{
  "catalog": {
    "id": "city-gis",
    "title": "City GIS Data Catalog",
    "stac": {
      "enabled": true,
      "catalogType": "self-contained",
      "conformsTo": [
        "https://api.stacspec.org/v1.0.0/core",
        "https://api.stacspec.org/v1.0.0/item-search",
        "https://api.stacspec.org/v1.0.0/ogcapi-features"
      ]
    }
  },
  "layers": [{
    "id": "roads",
    "stac": {
      "itemIdTemplate": "roads-{road_id}",
      "collectionId": "transportation"
    }
  }]
}
```

---

## Crosswalk: Core Metadata → STAC

### Layer/Raster → STAC Collection

| Core Metadata | STAC Collection | Notes |
|---------------|-----------------|-------|
| `layer.id` or `raster.id` | `collection.id` | Collection identifier |
| `layer.title` or `raster.title` | `collection.title` | Human-readable title |
| `layer.description` or `raster.description` | `collection.description` | Description |
| `layer.keywords` or `raster.keywords` | `collection.keywords` | Search keywords |
| `layer.extent.bbox` or `raster.extent.bbox` | `collection.extent.spatial.bbox` | Spatial extent |
| `layer.extent.temporal` or `raster.extent.temporal` | `collection.extent.temporal.interval` | Temporal extent |
| `layer.crs` or `raster.crs` | `collection.crs` (STAC extension) | Coordinate systems |
| `layer.links` or `raster.links` | `collection.links` | Related links |
| `layer.catalog.license` | `collection.license` | SPDX license identifier |
| `layer.stac.providers` | `collection.providers` | Organizations involved |
| `layer.stac.summaries` | `collection.summaries` | Attribute value ranges |
| `layer.stac.itemAssets` | `collection.item_assets` | Template for Item assets |

### Layer/Raster Instance → STAC Item

| Core Metadata | STAC Item | Notes |
|---------------|-----------|-------|
| Feature `id` or Raster instance | `item.id` | Unique item identifier |
| `layer.geometryField` value | `item.geometry` | GeoJSON geometry |
| `layer.extent.bbox` (feature-specific) | `item.bbox` | Feature bounding box |
| `layer.temporal.startField` value | `item.properties.datetime` or `start_datetime` | Temporal properties |
| `layer.fields` values | `item.properties.*` | Feature attributes |
| Derived from source | `item.assets` | Links to actual data files |
| `layer.stac.collectionId` | `item.collection` | Parent collection |
| `layer.stac.stacExtensions` | `item.stac_extensions` | STAC extensions used |

---

## STAC Extension Metadata Model

### Proposed C# Type Definitions

```csharp
/// <summary>
/// STAC-specific metadata extension for layers and raster datasets.
/// </summary>
public sealed record StacMetadata
{
    /// <summary>
    /// Whether to expose this resource via STAC API.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// STAC Collection ID. Defaults to layer/raster ID if not specified.
    /// </summary>
    public string? CollectionId { get; init; }

    /// <summary>
    /// SPDX license identifier (e.g., "CC-BY-4.0", "proprietary").
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    /// Organizations/individuals that produced, processed, or licensed the data.
    /// </summary>
    public IReadOnlyList<StacProvider> Providers { get; init; } = Array.Empty<StacProvider>();

    /// <summary>
    /// Asset definitions for the collection.
    /// </summary>
    public IReadOnlyDictionary<string, StacAsset> Assets { get; init; } = new Dictionary<string, StacAsset>();

    /// <summary>
    /// Template for item-level assets (collection.item_assets).
    /// </summary>
    public IReadOnlyDictionary<string, StacAsset> ItemAssets { get; init; } = new Dictionary<string, StacAsset>();

    /// <summary>
    /// Summaries of attribute value ranges (e.g., min/max, unique values).
    /// </summary>
    public IReadOnlyDictionary<string, object> Summaries { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// STAC extensions used (URIs to JSON schemas).
    /// </summary>
    public IReadOnlyList<string> StacExtensions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Template for generating item IDs from feature attributes.
    /// Example: "roads-{road_id}" → "roads-12345"
    /// </summary>
    public string? ItemIdTemplate { get; init; }

    /// <summary>
    /// Additional STAC collection properties not covered by core metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}

public sealed record StacProvider
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // producer, licensor, processor, host
    public string? Url { get; init; }
}

public sealed record StacAsset
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; } // MIME type
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // data, metadata, thumbnail
    public string? Href { get; init; } // Optional explicit URL
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}
```

### Updated Layer/Raster Definitions

```csharp
public sealed record LayerDefinition
{
    // ... existing properties ...

    /// <summary>
    /// STAC-specific metadata for exposing this layer via STAC API.
    /// </summary>
    public StacMetadata? Stac { get; init; }
}

public sealed record RasterDatasetDefinition
{
    // ... existing properties ...

    /// <summary>
    /// STAC-specific metadata for exposing this raster via STAC API.
    /// </summary>
    public StacMetadata? Stac { get; init; }
}
```

---

## STAC API Projection Logic

### Collection Projection

**Current (Raster Only):**
```csharp
// RasterStacCatalogBuilder.cs
public StacCollection BuildCollection(RasterDatasetDefinition raster)
{
    return new StacCollection
    {
        Id = raster.Id,
        Title = raster.Title,
        Description = raster.Description,
        Extent = MapExtent(raster.Extent),
        // ...
    };
}
```

**Proposed (Unified):**
```csharp
// UnifiedStacMapper.cs
public StacCollection MapLayerToCollection(LayerDefinition layer, ServiceDefinition service)
{
    var stac = layer.Stac ?? new StacMetadata { Enabled = true };

    if (!stac.Enabled)
        return null;

    return new StacCollection
    {
        Id = stac.CollectionId ?? layer.Id,
        Type = "Collection",
        StacVersion = "1.0.0",
        Title = layer.Title,
        Description = layer.Description ?? layer.Catalog.Summary,
        Keywords = layer.Keywords.Concat(layer.Catalog.Keywords).Distinct().ToList(),
        License = stac.License ?? "proprietary",
        Providers = stac.Providers.ToList(),
        Extent = new StacExtent
        {
            Spatial = new StacSpatialExtent
            {
                Bbox = layer.Extent?.Bbox?.ToList() ?? new List<double[]>()
            },
            Temporal = layer.Extent?.Temporal != null
                ? new StacTemporalExtent
                {
                    Interval = layer.Extent.Temporal.Select(t => new[] { t.Start, t.End }).ToList()
                }
                : null
        },
        Summaries = stac.Summaries,
        ItemAssets = stac.ItemAssets.ToDictionary(
            kvp => kvp.Key,
            kvp => MapToStacAsset(kvp.Value, layer)
        ),
        Links = BuildCollectionLinks(layer, service),
        StacExtensions = stac.StacExtensions.ToList()
    };
}

public StacCollection MapRasterToCollection(RasterDatasetDefinition raster)
{
    var stac = raster.Stac ?? new StacMetadata { Enabled = true };

    if (!stac.Enabled)
        return null;

    return new StacCollection
    {
        Id = stac.CollectionId ?? raster.Id,
        Type = "Collection",
        StacVersion = "1.0.0",
        Title = raster.Title,
        Description = raster.Description ?? raster.Catalog.Summary,
        Keywords = raster.Keywords.Concat(raster.Catalog.Keywords).Distinct().ToList(),
        License = stac.License ?? "proprietary",
        Providers = stac.Providers.ToList(),
        Extent = MapExtent(raster.Extent),
        Summaries = stac.Summaries,
        Assets = stac.Assets.ToDictionary(
            kvp => kvp.Key,
            kvp => MapToStacAsset(kvp.Value, raster)
        ),
        Links = BuildRasterCollectionLinks(raster),
        StacExtensions = stac.StacExtensions.ToList()
    };
}
```

### Item Projection

**Vector Feature → STAC Item:**
```csharp
public StacItem MapFeatureToItem(
    Feature feature,
    LayerDefinition layer,
    ServiceDefinition service)
{
    var stac = layer.Stac ?? new StacMetadata();

    var itemId = GenerateItemId(feature, layer);
    var geometry = feature.Geometry; // GeoJSON geometry
    var bbox = CalculateBbox(geometry);

    var properties = new Dictionary<string, object>();

    // Map temporal fields if configured
    if (layer.Temporal.Enabled)
    {
        var datetime = GetFeatureDateTime(feature, layer.Temporal);
        properties["datetime"] = datetime;
    }

    // Map all feature attributes
    foreach (var field in layer.Fields)
    {
        if (feature.Attributes.TryGetValue(field.Name, out var value))
        {
            properties[field.Name] = value;
        }
    }

    // Generate assets
    var assets = GenerateFeatureAssets(feature, layer, stac);

    return new StacItem
    {
        Id = itemId,
        Type = "Feature",
        StacVersion = "1.0.0",
        Geometry = geometry,
        Bbox = bbox,
        Properties = properties,
        Collection = stac.CollectionId ?? layer.Id,
        Assets = assets,
        Links = BuildItemLinks(feature, layer, service),
        StacExtensions = stac.StacExtensions.ToList()
    };
}

private string GenerateItemId(Feature feature, LayerDefinition layer)
{
    if (layer.Stac?.ItemIdTemplate != null)
    {
        // Template: "roads-{road_id}" → "roads-12345"
        var template = layer.Stac.ItemIdTemplate;
        foreach (var field in layer.Fields)
        {
            var placeholder = $"{{{field.Name}}}";
            if (template.Contains(placeholder))
            {
                var value = feature.Attributes[field.Name]?.ToString() ?? "";
                template = template.Replace(placeholder, value);
            }
        }
        return template;
    }

    // Default: layer_id-feature_id
    var featureId = feature.Attributes[layer.IdField]?.ToString() ?? Guid.NewGuid().ToString();
    return $"{layer.Id}-{featureId}";
}

private Dictionary<string, StacAsset> GenerateFeatureAssets(
    Feature feature,
    LayerDefinition layer,
    StacMetadata stac)
{
    var assets = new Dictionary<string, StacAsset>();

    // Data asset (GeoJSON)
    if (stac.ItemAssets.TryGetValue("data", out var dataAssetTemplate))
    {
        var featureId = feature.Attributes[layer.IdField]?.ToString();
        assets["data"] = new StacAsset
        {
            Href = $"/ogc/collections/{layer.Id}/items/{featureId}?f=json",
            Type = "application/geo+json",
            Title = dataAssetTemplate.Title,
            Roles = dataAssetTemplate.Roles.ToList()
        };
    }

    // Attachments (if enabled)
    if (layer.Attachments.Enabled)
    {
        assets["attachments"] = new StacAsset
        {
            Href = $"/rest/services/.../FeatureServer/0/{featureId}/attachments",
            Type = "application/json",
            Title = "Feature Attachments",
            Roles = new List<string> { "metadata" }
        };
    }

    return assets;
}
```

---

## Vector STAC Use Cases

### Use Case 1: Building Footprints

**Metadata:**
```json
{
  "layer": {
    "id": "buildings",
    "title": "Building Footprints",
    "geometryType": "Polygon",
    "temporal": {
      "enabled": true,
      "startField": "built_year",
      "endField": null
    },
    "stac": {
      "collectionId": "buildings",
      "license": "ODbL-1.0",
      "providers": [
        {
          "name": "OpenStreetMap Contributors",
          "roles": ["producer"],
          "url": "https://www.openstreetmap.org"
        }
      ],
      "itemIdTemplate": "building-{building_id}",
      "summaries": {
        "building_type": ["residential", "commercial", "industrial"],
        "height": {"min": 3, "max": 250}
      },
      "itemAssets": {
        "data": {
          "title": "Building Feature GeoJSON",
          "type": "application/geo+json",
          "roles": ["data"]
        }
      }
    }
  }
}
```

**STAC Collection Output:**
```json
{
  "id": "buildings",
  "type": "Collection",
  "stac_version": "1.0.0",
  "title": "Building Footprints",
  "description": "Building footprints with height and type",
  "license": "ODbL-1.0",
  "extent": {
    "spatial": {"bbox": [[-122.5, 37.7, -122.3, 37.9]]},
    "temporal": {"interval": [["1900-01-01T00:00:00Z", null]]}
  },
  "providers": [
    {
      "name": "OpenStreetMap Contributors",
      "roles": ["producer"],
      "url": "https://www.openstreetmap.org"
    }
  ],
  "summaries": {
    "building_type": ["residential", "commercial", "industrial"],
    "height": {"min": 3, "max": 250}
  },
  "item_assets": {
    "data": {
      "title": "Building Feature GeoJSON",
      "type": "application/geo+json",
      "roles": ["data"]
    }
  },
  "links": [
    {"rel": "self", "href": "/stac/collections/buildings"},
    {"rel": "items", "href": "/stac/collections/buildings/items"},
    {"rel": "root", "href": "/stac"}
  ]
}
```

**STAC Item Output (individual building):**
```json
{
  "id": "building-12345",
  "type": "Feature",
  "stac_version": "1.0.0",
  "collection": "buildings",
  "geometry": {
    "type": "Polygon",
    "coordinates": [[[-122.4, 37.8], [-122.41, 37.8], [-122.41, 37.81], [-122.4, 37.81], [-122.4, 37.8]]]
  },
  "bbox": [-122.41, 37.8, -122.4, 37.81],
  "properties": {
    "datetime": "2010-01-01T00:00:00Z",
    "building_id": 12345,
    "building_type": "commercial",
    "height": 45.5,
    "address": "123 Main St"
  },
  "assets": {
    "data": {
      "href": "/ogc/collections/buildings/items/12345?f=json",
      "type": "application/geo+json",
      "title": "Building Feature GeoJSON",
      "roles": ["data"]
    }
  },
  "links": [
    {"rel": "self", "href": "/stac/collections/buildings/items/building-12345"},
    {"rel": "collection", "href": "/stac/collections/buildings"}
  ]
}
```

### Use Case 2: Sensor Observations (IoT)

**Metadata:**
```json
{
  "layer": {
    "id": "weather_stations",
    "title": "Weather Station Observations",
    "geometryType": "Point",
    "temporal": {
      "enabled": true,
      "startField": "observation_time"
    },
    "stac": {
      "collectionId": "weather-obs",
      "license": "CC0-1.0",
      "itemIdTemplate": "obs-{station_id}-{observation_time}",
      "stacExtensions": [
        "https://stac-extensions.github.io/scientific/v1.0.0/schema.json"
      ],
      "summaries": {
        "temperature": {"min": -10, "max": 45},
        "humidity": {"min": 0, "max": 100}
      }
    }
  }
}
```

**Generated STAC Items:**
- One Item per observation
- Item ID: `obs-station001-2024-01-15T12:00:00Z`
- Properties include temperature, humidity, wind_speed
- Temporal query support via STAC Item Search

---

## Implementation Plan

### Phase 1: Data Model Extension (1-2 weeks)

1. ✅ Add `StacMetadata` record type
2. ✅ Add `layer.Stac` and `raster.Stac` properties
3. ✅ Update JSON schema
4. ✅ Add validation for STAC metadata

### Phase 2: Vector STAC Support (2-3 weeks)

1. Create `UnifiedStacMapper` class
2. Implement `MapLayerToCollection()`
3. Implement `MapFeatureToItem()`
4. Update `StacCollectionsController` to include vector collections
5. Implement STAC Item Search for vector features
6. Add unit tests

### Phase 3: Enhanced Raster STAC (1-2 weeks)

1. Refactor `RasterStacCatalogBuilder` to use `StacMetadata`
2. Support raster `stac.assets` configuration
3. Support STAC extensions (EO, Projection, etc.)
4. Migrate existing raster STAC to new model

### Phase 4: Documentation & Examples (1 week)

1. Update metadata authoring guide
2. Add STAC examples for vector and raster
3. Document STAC extension support
4. Add STAC API usage examples

---

## Benefits

### For Users

1. **Unified STAC experience** - Vector and raster data via same API
2. **Rich metadata** - Providers, licenses, summaries in STAC format
3. **STAC tooling compatibility** - Works with pystac, STAC Browser, etc.
4. **Temporal search** - Query features by time via STAC Item Search

### For Honua

1. **Semantic consistency** - STAC follows same metadata mapping as other protocols
2. **Competitive feature** - Few platforms support vector STAC natively
3. **Modern discovery** - STAC is emerging standard for geospatial catalogs
4. **IoT/sensor support** - STAC excellent for time-series observations

---

## Comparison to Alternatives

### GeoServer
- ❌ No native STAC support for vector or raster
- Community plugins available but limited

### ArcGIS Server
- ❌ No STAC support
- Proprietary catalog only

### Carto
- ⚠️ Limited STAC support (primarily tile-based)

### Mapbox
- ❌ No STAC support

### pygeoapi
- ✅ Strong STAC support for both vector and raster
- Honua's proposed model similar to pygeoapi's unified approach

**Competitive Advantage:** Native vector STAC puts Honua ahead of GeoServer, ArcGIS, and commercial alternatives.

---

## STAC Extensions Support

### Recommended Extensions

| Extension | Use Case | Support |
|-----------|----------|---------|
| **EO (Electro-Optical)** | Satellite imagery bands | Raster |
| **Projection** | CRS and transform info | Vector & Raster |
| **Scientific** | Scientific datasets | Vector & Raster |
| **Version** | Versioned datasets | Vector & Raster |
| **Timestamps** | Published/expires dates | Vector & Raster |
| **File** | File-level metadata | Raster |
| **Raster** | Raster-specific props | Raster |
| **Point Cloud** | LiDAR/point clouds | Future |

### Example: EO Extension for Satellite Imagery

```json
{
  "raster": {
    "stac": {
      "stacExtensions": [
        "https://stac-extensions.github.io/eo/v1.1.0/schema.json"
      ],
      "assets": {
        "cog": {
          "type": "image/tiff; application=geotiff; profile=cloud-optimized",
          "roles": ["data"],
          "eo:bands": [
            {"name": "red", "common_name": "red", "center_wavelength": 0.665},
            {"name": "green", "common_name": "green", "center_wavelength": 0.560},
            {"name": "blue", "common_name": "blue", "center_wavelength": 0.490},
            {"name": "nir", "common_name": "nir", "center_wavelength": 0.842}
          ]
        }
      }
    }
  }
}
```

---

## Migration Guide

### Existing Raster STAC Metadata

**Before (current):**
```json
{
  "rasterDatasets": [{
    "id": "dem",
    "title": "Elevation",
    "source": {"type": "cog", "uri": "/data/dem.tif"}
  }]
}
```

**After (proposed):**
```json
{
  "rasterDatasets": [{
    "id": "dem",
    "title": "Elevation",
    "source": {"type": "cog", "uri": "/data/dem.tif"},
    "stac": {
      "enabled": true,
      "license": "CC0-1.0",
      "providers": [
        {"name": "USGS", "roles": ["producer"], "url": "https://usgs.gov"}
      ],
      "assets": {
        "cog": {
          "title": "Cloud Optimized GeoTIFF",
          "type": "image/tiff; application=geotiff; profile=cloud-optimized",
          "roles": ["data"]
        }
      }
    }
  }]
}
```

### Backward Compatibility

- ✅ Existing raster datasets without `stac` block continue to work
- ✅ STAC API generates default collection from core metadata
- ✅ Adding `stac` block enhances output but is optional

---

## Conclusion

Formalizing STAC integration in Honua:

✅ **Unifies vector and raster** - Same STAC API for all geospatial data
✅ **Maintains semantic mapping** - Single metadata, multiple projections
✅ **Backward compatible** - Existing raster STAC continues working
✅ **Competitive advantage** - Native vector STAC support rare in industry
✅ **Modern discovery** - STAC emerging as standard for cloud-native geospatial

**Recommendation:** Implement in Phase 2 after core OGC/Esri protocols are stable. Prioritize vector STAC support to differentiate from competitors.
