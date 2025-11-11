# Honua Map Persistence & Versioning Architecture

**Status:** Architecture Design v2.0 (OGC-Based)
**Version:** 2.0
**Date:** 2025-11-11
**Author:** Honua Team

---

## Executive Summary

This document defines a comprehensive, **OGC standards-based** architecture for persisting, versioning, and managing map configurations in Honua.Server and Honua.MapSDK. The solution uses **OGC OWS Context** as the foundation, extended with Honua-specific capabilities for application state, versioning, and SDK integration.

### Key Goals

1. ✅ **OGC Standards-Based**: Use OGC OWS Context for interoperability
2. ✅ **Complete State Capture**: Persist map config, data sources, UI state, user preferences
3. ✅ **Version Control**: Git-like versioning with branches, tags, diffs
4. ✅ **Reliable**: ACID transactions, data integrity, audit trails
5. ✅ **SDK-Integrated**: Seamless save/load from Honua.MapSDK components

---

## Table of Contents

1. [Standards Analysis](#standards-analysis)
2. [Honua Map Specification](#honua-map-specification)
3. [Persistence Architecture](#persistence-architecture)
4. [Versioning System](#versioning-system)
5. [Database Schema](#database-schema)
6. [SDK Integration](#sdk-integration)
7. [API Design](#api-design)
8. [Implementation Roadmap](#implementation-roadmap)

---

## Standards Analysis

### 1. OGC OWS Context (GeoJSON Encoding)

**Format:** GeoJSON (also supports Atom)
**Purpose:** Interoperable map configuration exchange
**Status:** OGC standard, modern and JSON-based

#### Structure
```json
{
  "type": "FeatureCollection",
  "id": "http://www.opengis.net/owc/1.0",
  "properties": {
    "lang": "en",
    "title": "My Map Context",
    "subtitle": "Example map configuration",
    "updated": "2025-11-11T12:00:00Z",
    "author": "user@honua.io",
    "generator": "Honua.Server v1.0",
    "rights": "Private",
    "bbox": [-122.5, 37.7, -122.3, 37.9],
    "date": "2025-11-11"
  },
  "features": [
    {
      "type": "Feature",
      "id": "http://honua.io/layers/parcels",
      "geometry": null,
      "properties": {
        "title": "Property Parcels",
        "abstract": "San Francisco property parcels",
        "updated": "2025-11-11T12:00:00Z",
        "offerings": [
          {
            "code": "http://www.opengis.net/spec/owc-geojson/1.0/req/wfs",
            "operations": [
              {
                "code": "GetFeature",
                "method": "GET",
                "type": "application/json",
                "href": "https://geoserver.example.com/wfs"
              }
            ],
            "contents": [
              {
                "type": "application/json",
                "href": "https://api.honua.io/data/parcels.geojson"
              }
            ],
            "styles": [
              {
                "name": "default",
                "title": "Default Style",
                "legendURL": "https://api.honua.io/legend/parcels.png",
                "content": {
                  "type": "application/vnd.mapbox.style+json",
                  "href": "https://api.honua.io/styles/parcels.json"
                }
              }
            ]
          }
        ],
        "active": true,
        "minscaledenominator": 100000,
        "maxscaledenominator": 5000
      }
    }
  ]
}
```

**Pros:**
- ✅ **OGC standard** for map configurations
- ✅ **GeoJSON-based** (modern, widely supported)
- ✅ **Service-agnostic** (WMS, WFS, WMTS, custom)
- ✅ **Interoperable** across OGC-compliant tools

**Limitations:**
- ❌ Focused on **service layers** only
- ❌ No application state (UI components, filters)
- ❌ No versioning metadata
- ❌ No user preferences
- ❌ Limited styling (links to external styles)

### 2. Mapbox Style Specification

**Format:** JSON
**Purpose:** Define visual appearance of vector tile maps
**Status:** Open standard, widely adopted

**Pros:**
- ✅ Industry standard for styling
- ✅ MapLibre GL native support
- ✅ Rich styling capabilities

**Use in Honua:**
- Use for **visual layer definitions**
- Embed in OWS Context "styles" section
- Reference or inline MapLibre styles

### 3. Decision: OGC OWS Context + Honua Extensions

**Adopt a hybrid approach:**

```
┌─────────────────────────────────────────────────┐
│      Honua Map Document (HMD) v2.0             │
├─────────────────────────────────────────────────┤
│ Base:         OGC OWS Context (GeoJSON)        │
│ Extensions:   Honua-specific metadata           │
│               - Application state               │
│               - Component configurations        │
│               - User preferences                │
│               - Versioning metadata             │
│               - Security/permissions            │
└─────────────────────────────────────────────────┘
```

**Benefits:**
- ✅ **OGC compliant** for service layers
- ✅ **Interoperable** with OGC tools
- ✅ **Extensible** for Honua-specific needs
- ✅ **Standards-based** foundation

---

## Honua Map Specification

### Honua Map Document (HMD) v2.0

Complete specification extending OGC OWS Context.

```json
{
  "$schema": "http://www.opengis.net/owc/1.0",
  "type": "FeatureCollection",
  "id": "https://maps.honua.io/map/uuid",

  "properties": {
    "lang": "en",
    "title": "San Francisco Property Analysis",
    "subtitle": "Property values and ownership",
    "updated": "2025-11-11T14:30:00Z",
    "authors": [
      {
        "name": "John Doe",
        "email": "john@example.com"
      }
    ],
    "publisher": "Acme Corp",
    "generator": {
      "title": "Honua.Server",
      "version": "1.0.0",
      "uri": "https://honua.io"
    },
    "rights": "© 2025 Acme Corp - Internal Use Only",
    "date": "2025-11-11",
    "bbox": [-122.5, 37.7, -122.3, 37.9],

    "categories": [
      {
        "scheme": "https://honua.io/categories",
        "term": "parcels",
        "label": "Property Parcels"
      },
      {
        "scheme": "https://honua.io/categories",
        "term": "real-estate",
        "label": "Real Estate"
      }
    ],

    "links": {
      "profiles": [
        {
          "href": "http://www.opengis.net/spec/owc-geojson/1.0/req/core"
        }
      ],
      "via": [
        {
          "href": "https://data.sfgov.org/parcels",
          "title": "Original Data Source"
        }
      ]
    },

    "display": {
      "pixelWidth": 1920,
      "pixelHeight": 1080,
      "mmPerPixel": 0.28
    },

    "extent": {
      "spatial": {
        "bbox": [-122.5, 37.7, -122.3, 37.9],
        "crs": "http://www.opengis.net/def/crs/EPSG/0/3857"
      },
      "temporal": {
        "start": "2020-01-01T00:00:00Z",
        "end": "2025-12-31T23:59:59Z"
      }
    },

    "x-honua": {
      "version": "2.0",
      "mapId": "550e8400-e29b-41d4-a716-446655440000",
      "versionNumber": 3,
      "branch": "main",
      "tags": ["production", "v1.0"],
      "published": true,
      "publicAccess": "organization",

      "viewport": {
        "center": [-122.4194, 37.7749],
        "zoom": 12,
        "bearing": 0,
        "pitch": 45,
        "projection": "mercator"
      },

      "basemap": {
        "style": "https://demotiles.maplibre.org/style.json",
        "type": "vector"
      },

      "components": {
        "legend": {
          "enabled": true,
          "position": "bottom-right",
          "collapsible": true
        },
        "dataGrid": {
          "enabled": true,
          "syncWith": "map1",
          "visible": true,
          "columns": ["parcel_id", "owner", "value"],
          "pageSize": 50,
          "position": "bottom"
        },
        "chart": {
          "enabled": true,
          "type": "histogram",
          "field": "value",
          "bins": 20,
          "syncWith": "map1"
        },
        "filterPanel": {
          "enabled": true,
          "position": "left",
          "filters": [
            {
              "field": "value",
              "type": "range",
              "min": 0,
              "max": 5000000,
              "defaultValue": [0, 1000000]
            }
          ]
        }
      },

      "bookmarks": [
        {
          "id": "downtown",
          "name": "Downtown SF",
          "bbox": [-122.42, 37.77, -122.40, 37.79],
          "zoom": 14,
          "bearing": 0,
          "pitch": 0
        }
      ],

      "applicationState": {
        "activeFilters": [],
        "selectedFeatures": [],
        "userPreferences": {
          "theme": "dark",
          "measurementUnits": "metric"
        }
      },

      "security": {
        "ownerId": "user-uuid",
        "organizationId": "org-uuid",
        "permissions": {
          "view": ["organization"],
          "edit": ["user-uuid"],
          "delete": ["user-uuid"]
        }
      }
    }
  },

  "features": [
    {
      "type": "Feature",
      "id": "https://api.honua.io/layers/parcels",
      "geometry": null,
      "properties": {
        "title": "Property Parcels",
        "abstract": "San Francisco property parcels with ownership and values",
        "updated": "2025-11-11T12:00:00Z",

        "offerings": [
          {
            "code": "https://honua.io/offerings/grpc",
            "operations": [
              {
                "code": "Query",
                "method": "POST",
                "type": "application/grpc",
                "href": "grpc://api.honua.io/datasets/sf-parcels"
              }
            ],
            "contents": [
              {
                "type": "application/geo+json",
                "href": "https://api.honua.io/data/parcels.geojson"
              }
            ],
            "styles": [
              {
                "name": "value-choropleth",
                "title": "Property Value Choropleth",
                "abstract": "Color-coded by property value",
                "default": true,
                "legendURL": "https://api.honua.io/legend/parcels-value.png",
                "content": {
                  "type": "application/vnd.mapbox.style+json",
                  "href": "https://api.honua.io/styles/parcels-value.json",
                  "inline": {
                    "version": 8,
                    "layers": [
                      {
                        "id": "parcels-fill",
                        "type": "fill",
                        "source": "parcels",
                        "paint": {
                          "fill-color": [
                            "step",
                            ["get", "value"],
                            "#ffffcc",
                            500000, "#a1dab4",
                            1000000, "#41b6c4",
                            2000000, "#2c7fb8",
                            3000000, "#253494"
                          ],
                          "fill-opacity": 0.7,
                          "fill-outline-color": "#333333"
                        }
                      }
                    ]
                  }
                }
              }
            ]
          }
        ],

        "active": true,
        "minscaledenominator": 100000,
        "maxscaledenominator": 5000,

        "x-honua": {
          "layerId": "parcels-layer",
          "sourceType": "grpc",
          "fields": ["parcel_id", "owner", "value", "land_use", "geometry"],
          "popupTemplate": {
            "title": "{parcel_id}",
            "content": "Owner: {owner}<br>Value: ${value:NumberFormat}",
            "fieldInfos": [
              {
                "fieldName": "value",
                "format": {
                  "digitSeparator": true,
                  "places": 0
                }
              }
            ]
          },
          "labeling": {
            "enabled": true,
            "field": "parcel_id",
            "minZoom": 15
          }
        }
      }
    },

    {
      "type": "Feature",
      "id": "https://geoserver.example.com/wfs/buildings",
      "geometry": null,
      "properties": {
        "title": "Building Footprints",
        "abstract": "3D building footprints with heights",
        "updated": "2025-11-11T10:00:00Z",

        "offerings": [
          {
            "code": "http://www.opengis.net/spec/owc-geojson/1.0/req/wfs",
            "operations": [
              {
                "code": "GetFeature",
                "method": "GET",
                "type": "application/json",
                "href": "https://geoserver.example.com/wfs",
                "request": {
                  "version": "2.0.0",
                  "typeName": "buildings:sf_buildings",
                  "outputFormat": "application/json",
                  "srsName": "EPSG:3857"
                }
              }
            ],
            "styles": [
              {
                "name": "3d-extrusion",
                "title": "3D Buildings",
                "content": {
                  "type": "application/vnd.mapbox.style+json",
                  "inline": {
                    "version": 8,
                    "layers": [
                      {
                        "id": "buildings-3d",
                        "type": "fill-extrusion",
                        "source": "buildings",
                        "paint": {
                          "fill-extrusion-color": "#aaa",
                          "fill-extrusion-height": ["get", "height"],
                          "fill-extrusion-base": 0,
                          "fill-extrusion-opacity": 0.8
                        }
                      }
                    ]
                  }
                }
              }
            ]
          }
        ],

        "active": true,

        "x-honua": {
          "layerId": "buildings-3d",
          "sourceType": "wfs",
          "renderType": "3d-extrusion"
        }
      }
    }
  ]
}
```

### Key Design Decisions

1. **`$schema`**: OGC OWS Context standard
2. **`type: FeatureCollection`**: GeoJSON structure
3. **`properties`**: OGC-standard map metadata
4. **`features`**: Each feature = one layer with offerings
5. **`offerings`**: OGC-standard service definitions
6. **`x-honua`**: Honua-specific extensions (namespaced)
7. **Inline vs. Referenced Styles**: Support both patterns

### Extension Strategy

All Honua-specific properties use the **`x-honua` namespace**:
- Top-level: Map-wide settings (versioning, components, bookmarks)
- Feature-level: Layer-specific settings (popups, labels)

**Benefits:**
- ✅ Preserves OGC compliance
- ✅ Tools can ignore `x-*` extensions
- ✅ Clear separation of concerns
- ✅ Forward-compatible

---

## Persistence Architecture

### Storage: PostgreSQL with JSONB

**Recommended approach for Honua.Server**

```sql
CREATE TABLE maps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- OWS Context properties
    title VARCHAR(255) NOT NULL,
    subtitle TEXT,
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    -- Full map document (OGC OWS Context + Honua extensions)
    document JSONB NOT NULL,

    -- Extracted fields for indexing/querying
    owner_id UUID NOT NULL REFERENCES users(id),
    organization_id UUID REFERENCES organizations(id),
    published BOOLEAN DEFAULT FALSE,
    public_access VARCHAR(50) DEFAULT 'private',

    -- Spatial index on bbox
    bbox GEOMETRY(POLYGON, 4326),

    -- Full-text search
    search_vector TSVECTOR,

    created_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT valid_access CHECK (public_access IN ('private', 'organization', 'public'))
);

-- Indexes
CREATE INDEX idx_maps_owner ON maps(owner_id);
CREATE INDEX idx_maps_org ON maps(organization_id);
CREATE INDEX idx_maps_document ON maps USING GIN(document jsonb_path_ops);
CREATE INDEX idx_maps_bbox ON maps USING GIST(bbox);
CREATE INDEX idx_maps_search ON maps USING GIN(search_vector);

-- Trigger to extract bbox from document
CREATE OR REPLACE FUNCTION extract_map_bbox() RETURNS TRIGGER AS $$
BEGIN
    NEW.bbox := ST_MakeEnvelope(
        (NEW.document->'properties'->'bbox'->>0)::float,
        (NEW.document->'properties'->'bbox'->>1)::float,
        (NEW.document->'properties'->'bbox'->>2)::float,
        (NEW.document->'properties'->'bbox'->>3)::float,
        4326
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER map_bbox_trigger
    BEFORE INSERT OR UPDATE ON maps
    FOR EACH ROW EXECUTE FUNCTION extract_map_bbox();

-- Trigger for full-text search
CREATE OR REPLACE FUNCTION update_map_search_vector() RETURNS TRIGGER AS $$
BEGIN
    NEW.search_vector := to_tsvector('english',
        COALESCE(NEW.title, '') || ' ' ||
        COALESCE(NEW.subtitle, '') || ' ' ||
        COALESCE(NEW.document->'properties'->>'abstract', '')
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER map_search_trigger
    BEFORE INSERT OR UPDATE ON maps
    FOR EACH ROW EXECUTE FUNCTION update_map_search_vector();
```

---

## Versioning System

### Git-Inspired Versioning

**Same as before - Git-like versioning works well:**

```sql
CREATE TABLE map_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    version_number INT NOT NULL,
    branch VARCHAR(100) DEFAULT 'main',

    -- Full OWS Context document snapshot
    document JSONB NOT NULL,

    -- Version metadata
    commit_message TEXT,
    author_id UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    parent_version_id UUID REFERENCES map_versions(id),

    -- JSON Patch diff from parent (optional)
    diff JSONB,

    UNIQUE(map_id, branch, version_number)
);

CREATE TABLE map_branches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    head_version_id UUID REFERENCES map_versions(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by UUID NOT NULL REFERENCES users(id),
    UNIQUE(map_id, name)
);

CREATE TABLE map_tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    version_id UUID NOT NULL REFERENCES map_versions(id),
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by UUID NOT NULL REFERENCES users(id),
    UNIQUE(map_id, name)
);

CREATE TABLE map_audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    version_id UUID REFERENCES map_versions(id),
    action VARCHAR(50) NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id),
    timestamp TIMESTAMPTZ DEFAULT NOW(),
    details JSONB,
    ip_address INET,
    user_agent TEXT
);
```

**Versioning operations remain the same** as the previous design (create, save, branch, tag, revert).

---

## SDK Integration

### MapSDK Component API

**Updated to work with OGC OWS Context:**

```csharp
public class OwsContextService
{
    // Load map from OWS Context
    public async Task<OwsContext> LoadContextAsync(Guid mapId, int? version = null);

    // Save current map state as OWS Context
    public async Task<MapVersion> SaveContextAsync(OwsContext context, string commitMessage);

    // Convert between HMD and MapLibre
    public MapLibreStyle ConvertToMapLibreStyle(OwsContext context);
    public OwsContext ConvertFromMapLibreState(string mapId, MapLibreState state);

    // Export formats
    public string ExportAsOwsContextJson(OwsContext context);
    public string ExportAsOwsContextAtom(OwsContext context);
    public string ExportAsMapLibreStyle(OwsContext context);
}
```

**Component usage:**

```razor
<HonuaMap Id="map1"
          AutoSave="true"
          MapId="@_mapId"
          OnSaved="HandleMapSaved">
    ...
</HonuaMap>

@code {
    [Inject] private OwsContextService OwsService { get; set; }

    private async Task SaveMapAsync()
    {
        // Get current map state
        var mapState = await _mapRef.GetStateAsync();

        // Convert to OWS Context
        var owsContext = OwsService.ConvertFromMapLibreState(_mapId, mapState);

        // Add Honua extensions
        owsContext.Properties.Extensions["x-honua"] = new
        {
            components = _componentConfigs,
            bookmarks = _bookmarks,
            applicationState = _appState
        };

        // Save with versioning
        var version = await OwsService.SaveContextAsync(owsContext, "Updated styling");

        Snackbar.Add($"Saved version {version.VersionNumber}", Severity.Success);
    }
}
```

---

## API Design

### RESTful API Endpoints

```
# Maps CRUD
GET    /api/v1/maps                          # List maps
POST   /api/v1/maps                          # Create map (OWS Context JSON)
GET    /api/v1/maps/{id}                     # Get map (latest version)
PUT    /api/v1/maps/{id}                     # Update map
DELETE /api/v1/maps/{id}                     # Delete map

# OGC Compatibility
GET    /api/v1/maps/{id}/owc                 # Get as OWS Context JSON
GET    /api/v1/maps/{id}/owc/atom            # Get as OWS Context Atom

# Versions (same as before)
GET    /api/v1/maps/{id}/versions
POST   /api/v1/maps/{id}/versions
GET    /api/v1/maps/{id}/versions/{version}
POST   /api/v1/maps/{id}/revert/{version}

# Branches & Tags (same as before)
GET    /api/v1/maps/{id}/branches
POST   /api/v1/maps/{id}/branches
GET    /api/v1/maps/{id}/tags
POST   /api/v1/maps/{id}/tags

# Export formats
GET    /api/v1/maps/{id}/export/owc          # OWS Context JSON
GET    /api/v1/maps/{id}/export/owc-atom     # OWS Context Atom
GET    /api/v1/maps/{id}/export/maplibre     # MapLibre Style JSON
GET    /api/v1/maps/{id}/export/json         # Full Honua JSON (with extensions)

# OGC Spatial Queries
GET    /api/v1/maps/search/bbox?bbox=-122.5,37.7,-122.3,37.9
GET    /api/v1/maps/search/within?geometry={geojson}
```

---

## Implementation Roadmap

### Phase 1: Foundation (2 weeks)

**Week 1:**
- [ ] Design OWS Context schema
- [ ] Create database migrations
- [ ] Implement entities (Map, MapVersion, etc.)
- [ ] Create `IOwsContextService` interface
- [ ] OWS Context parser/serializer

**Week 2:**
- [ ] Implement versioning (save, load, list)
- [ ] Add audit logging
- [ ] REST API controllers
- [ ] Unit tests
- [ ] OpenAPI documentation

### Phase 2: SDK Integration (1 week)

- [ ] Extend MapConfiguration to OWS Context
- [ ] Add `IOwsContextService` to MapSDK DI
- [ ] Implement auto-save in HonuaMap
- [ ] Version history UI component
- [ ] ComponentBus messages
- [ ] Testing

### Phase 3: OGC Interoperability (1 week)

- [ ] Export to OWS Context Atom
- [ ] Import from external OWS Context
- [ ] Validate against OGC schema
- [ ] Test with OGC tools (QGIS, etc.)
- [ ] Documentation

### Phase 4: Advanced Features (2 weeks)

- [ ] Branches, tags, diffs
- [ ] Revert to version
- [ ] Conflict resolution
- [ ] Performance optimization
- [ ] Migration guide

**Total: 6 weeks**

---

## OGC Compliance

### Validation

**Validate against OGC schema:**

```csharp
public class OwsContextValidator
{
    public ValidationResult Validate(OwsContext context)
    {
        var result = new ValidationResult();

        // Required OGC properties
        if (context.Type != "FeatureCollection")
            result.Errors.Add("Must be FeatureCollection");

        if (context.Id == null || !Uri.IsWellFormedUriString(context.Id, UriKind.Absolute))
            result.Errors.Add("id must be absolute URI");

        if (context.Properties == null)
            result.Errors.Add("properties is required");

        if (string.IsNullOrWhiteSpace(context.Properties.Title))
            result.Errors.Add("properties.title is required");

        if (context.Properties.Updated == default)
            result.Errors.Add("properties.updated is required");

        // Validate features (layers)
        foreach (var feature in context.Features)
        {
            if (feature.Properties?.Offerings == null || !feature.Properties.Offerings.Any())
                result.Errors.Add($"Feature {feature.Id}: offerings required");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}
```

### Interoperability Testing

**Test with OGC-compliant tools:**
1. Export OWS Context JSON/Atom
2. Import into QGIS (OWS Context plugin)
3. Import into ArcGIS Pro (OWS Context support)
4. Verify layers load correctly
5. Verify spatial extent preserved

---

## Migration from Old Format

### Convert Existing Maps

```csharp
public async Task MigrateToOwsContextAsync()
{
    var existingMaps = await _db.Maps
        .Where(m => m.Document != null)
        .ToListAsync();

    foreach (var map in existingMaps)
    {
        // Convert old format to OWS Context
        var owsContext = new OwsContext
        {
            Type = "FeatureCollection",
            Id = $"https://maps.honua.io/map/{map.Id}",
            Properties = new OwsContextProperties
            {
                Title = map.Name,
                Subtitle = map.Description,
                Updated = map.UpdatedAt,
                Authors = new[] { new Author { Email = map.CreatedBy } },
                Generator = new Generator
                {
                    Title = "Honua.Server",
                    Version = "1.0.0",
                    Uri = "https://honua.io"
                },
                BBox = ExtractBBox(map.Document),
                Extensions = new Dictionary<string, object>
                {
                    ["x-honua"] = ExtractHonuaExtensions(map.Document)
                }
            },
            Features = ConvertLayersToFeatures(map.Document)
        };

        // Save as OWS Context
        map.Document = JsonSerializer.SerializeToNode(owsContext);

        // Create initial version
        await _versionService.CreateVersionAsync(map.Id, "Migrated to OWS Context");
    }

    await _db.SaveChangesAsync();
}
```

---

## Security Considerations

### Access Control (Same as Before)

```csharp
public enum MapPermission
{
    View,
    Edit,
    Delete,
    ManageVersions,
    Publish
}
```

### Row-Level Security

```sql
ALTER TABLE maps ENABLE ROW LEVEL SECURITY;

CREATE POLICY maps_select_policy ON maps
    FOR SELECT
    USING (
        owner_id = current_user_id() OR
        organization_id = current_org_id() OR
        public_access = 'public'
    );
```

---

## Conclusion

This architecture provides:

1. ✅ **OGC Standards-Based**: Uses OWS Context (GeoJSON) as foundation
2. ✅ **Interoperable**: Compatible with OGC tools (QGIS, ArcGIS)
3. ✅ **Extensible**: `x-honua` namespace for Honua features
4. ✅ **Complete**: Captures map config, layers, UI state
5. ✅ **Versioned**: Git-like versioning system
6. ✅ **Reliable**: PostgreSQL JSONB with ACID
7. ✅ **SDK-Integrated**: Seamless Blazor integration

**No ArcGIS terminology or patterns** - pure OGC + Honua.

**Next Steps:**
1. Review and approve architecture
2. Create database migrations
3. Implement Phase 1 (Foundation)
4. Begin OGC compliance testing

---

**Questions or feedback?** Contact the Honua architecture team.
