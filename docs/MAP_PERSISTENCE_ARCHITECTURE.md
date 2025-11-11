# Honua Map Persistence & Versioning Architecture

**Status:** Architecture Design
**Version:** 1.0
**Date:** 2025-11-11
**Author:** Honua Team

---

## Executive Summary

This document defines a comprehensive, standards-based architecture for persisting, versioning, and managing map configurations in Honua.Server and Honua.MapSDK. The solution combines industry standards (Mapbox Style Spec, ArcGIS Web Map) with Git-like versioning to provide reliable, scalable map state management.

### Key Goals

1. **Standards-Based**: Adopt open standards for interoperability
2. **Complete State Capture**: Persist map config, data sources, UI state, user preferences
3. **Version Control**: Git-like versioning with branches, tags, diffs
4. **Reliable**: ACID transactions, data integrity, audit trails
5. **SDK-Integrated**: Seamless save/load from Honua.MapSDK components

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

### 1. Mapbox Style Specification

**Format:** JSON
**Purpose:** Define visual appearance of vector tile maps
**Status:** Open standard, widely adopted

#### Structure
```json
{
  "version": 8,
  "name": "My Map Style",
  "sources": {
    "source-id": {
      "type": "vector",
      "url": "mapbox://mapbox.mapbox-streets-v8"
    }
  },
  "layers": [
    {
      "id": "layer-id",
      "type": "fill",
      "source": "source-id",
      "paint": { "fill-color": "#ff0000" }
    }
  ],
  "glyphs": "...",
  "sprite": "..."
}
```

**Pros:**
- ✅ Industry standard for vector tiles
- ✅ MapLibre GL native support
- ✅ Extensive styling capabilities
- ✅ Active ecosystem

**Cons:**
- ❌ Focused on visual styling, not application state
- ❌ No versioning/audit built-in
- ❌ Doesn't capture UI components, filters, bookmarks

### 2. ArcGIS Web Map Specification

**Format:** JSON
**Purpose:** Complete web map representation
**Status:** Proprietary but well-documented

#### Structure
```json
{
  "version": "2.35",
  "authoringApp": "Honua.Server",
  "authoringAppVersion": "1.0",
  "baseMap": { ... },
  "operationalLayers": [ ... ],
  "bookmarks": [ ... ],
  "spatialReference": { "wkid": 3857 },
  "widgets": [ ... ],
  "tables": [ ... ],
  "timeInfo": { ... }
}
```

**Pros:**
- ✅ Comprehensive (layers, bookmarks, widgets, tables)
- ✅ Sequential versioning (2.0 → 2.35)
- ✅ Handles complex enterprise scenarios
- ✅ Metadata tracking

**Cons:**
- ❌ Esri-specific terminology/structure
- ❌ Tightly coupled to ArcGIS ecosystem
- ❌ Complex for simple use cases

### 3. OGC Web Map Context (WMC) / OWS Context

**Format:** XML or JSON (GeoJSON encoding)
**Purpose:** Interoperable map configuration
**Status:** OGC standard

#### Structure (JSON)
```json
{
  "type": "FeatureCollection",
  "id": "http://www.opengis.net/owc/1.0",
  "properties": {
    "title": "My Map",
    "updated": "2025-11-11T12:00:00Z",
    "bbox": [-180, -90, 180, 90]
  },
  "features": [
    {
      "type": "Feature",
      "properties": {
        "title": "WMS Layer",
        "offerings": [ ... ]
      }
    }
  ]
}
```

**Pros:**
- ✅ OGC standard for interoperability
- ✅ Supports multiple service types (WMS, WFS, etc.)
- ✅ JSON and XML encodings

**Cons:**
- ❌ Less widely adopted than Mapbox Style Spec
- ❌ Complex for modern web maps
- ❌ Limited styling capabilities

### 4. Decision: Honua Hybrid Approach

**Adopt a hybrid specification:**

```
┌─────────────────────────────────────────────────┐
│         Honua Map Document (HMD)                │
├─────────────────────────────────────────────────┤
│ Metadata:        Version, author, timestamps   │
│ MapLibre Style:  Mapbox Style Spec (embedded)   │
│ Layers:          ArcGIS-inspired layer configs  │
│ Components:      SDK component states           │
│ Data:            Data source configurations     │
│ State:           UI state, bookmarks, filters   │
│ Audit:           Change history, versioning     │
└─────────────────────────────────────────────────┘
```

**Benefits:**
- ✅ **Interoperable**: Export to Mapbox Style Spec
- ✅ **Comprehensive**: Captures full application state
- ✅ **Standard-friendly**: Uses familiar structures
- ✅ **Extensible**: Easy to add Honua-specific features

---

## Honua Map Specification

### Honua Map Document (HMD) v1.0

Complete JSON specification for persisting maps.

```json
{
  "$schema": "https://honua.io/schemas/map/v1.0.json",
  "honuaVersion": "1.0",
  "specVersion": "1.0",

  "metadata": {
    "id": "uuid",
    "name": "San Francisco Parcels",
    "description": "Property analysis dashboard",
    "tags": ["parcels", "san-francisco", "real-estate"],
    "thumbnail": "https://cdn.honua.io/thumbnails/uuid.png",
    "created": "2025-11-11T12:00:00Z",
    "updated": "2025-11-11T14:30:00Z",
    "createdBy": "user@example.com",
    "updatedBy": "user@example.com",
    "version": 3,
    "versionHistory": "version-uuid",
    "published": true,
    "publicAccess": "organization"
  },

  "viewport": {
    "center": [-122.4194, 37.7749],
    "zoom": 12,
    "bearing": 0,
    "pitch": 45,
    "projection": "mercator"
  },

  "maplibreStyle": {
    "version": 8,
    "name": "Honua Dark",
    "sources": { ... },
    "layers": [ ... ],
    "glyphs": "...",
    "sprite": "..."
  },

  "dataSources": [
    {
      "id": "parcels-source",
      "type": "grpc",
      "url": "grpc://api.honua.io/parcels",
      "authentication": {
        "type": "bearer",
        "tokenUrl": "https://auth.honua.io/token"
      },
      "cache": {
        "enabled": true,
        "ttl": 3600
      }
    },
    {
      "id": "buildings-wfs",
      "type": "wfs",
      "url": "https://geoserver.example.com/wfs",
      "layer": "buildings:sf_buildings",
      "version": "2.0.0",
      "outputFormat": "application/json"
    }
  ],

  "operationalLayers": [
    {
      "id": "parcels-layer",
      "name": "Property Parcels",
      "sourceId": "parcels-source",
      "type": "vector",
      "visible": true,
      "opacity": 0.8,
      "minZoom": 10,
      "maxZoom": 22,
      "fields": ["parcel_id", "owner", "value", "land_use"],
      "popupTemplate": {
        "title": "{parcel_id}",
        "content": "Owner: {owner}<br>Value: ${value:NumberFormat}",
        "fieldInfos": [ ... ]
      },
      "renderer": {
        "type": "simple",
        "symbol": {
          "type": "simple-fill",
          "color": "#4A90E2",
          "outline": { "color": "#2E5C8A", "width": 1 }
        }
      },
      "labeling": {
        "enabled": true,
        "field": "parcel_id",
        "minZoom": 15
      }
    }
  ],

  "components": {
    "dataGrid": {
      "enabled": true,
      "syncWith": "map1",
      "visible": true,
      "columns": ["parcel_id", "owner", "value"],
      "pageSize": 50,
      "position": "bottom",
      "height": "300px"
    },
    "chart": {
      "enabled": true,
      "type": "histogram",
      "field": "value",
      "bins": 20,
      "syncWith": "map1"
    },
    "legend": {
      "enabled": true,
      "position": "bottom-right",
      "collapsible": true
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
      "extent": {
        "xmin": -122.42, "ymin": 37.77,
        "xmax": -122.40, "ymax": 37.79
      },
      "zoom": 14,
      "bearing": 0,
      "pitch": 0
    }
  ],

  "timeInfo": {
    "enabled": true,
    "startField": "sale_date",
    "endField": null,
    "interval": { "value": 1, "unit": "months" },
    "extent": ["2020-01-01", "2025-12-31"]
  },

  "spatialReference": {
    "wkid": 3857,
    "latestWkid": 3857
  },

  "applicationState": {
    "activeFilters": [ ... ],
    "selectedFeatures": [ ... ],
    "userPreferences": {
      "theme": "dark",
      "measurementUnits": "metric"
    }
  },

  "extensions": {
    "customAnalysis": { ... },
    "customWidgets": [ ... ]
  }
}
```

### Key Design Decisions

1. **`$schema`**: JSON Schema for validation
2. **`honuaVersion`**: Honua.Server version that created the map
3. **`specVersion`**: HMD specification version (semantic versioning)
4. **`maplibreStyle`**: Embed full Mapbox Style Spec for visual layer
5. **`dataSources`**: Separate data sources from layers (normalization)
6. **`operationalLayers`**: Business data layers (vs. basemap)
7. **`components`**: SDK component configurations
8. **`applicationState`**: Runtime state (filters, selections)
9. **`extensions`**: Custom/plugin data

---

## Persistence Architecture

### Storage Options

#### Option 1: PostgreSQL with JSONB

**Recommended approach for Honua.Server**

```sql
CREATE TABLE maps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    owner_id UUID NOT NULL REFERENCES users(id),
    organization_id UUID REFERENCES organizations(id),

    -- Full map document as JSONB
    document JSONB NOT NULL,

    -- Extracted fields for indexing/querying
    tags TEXT[],
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    published BOOLEAN DEFAULT FALSE,
    public_access VARCHAR(50) DEFAULT 'private',

    -- GIN index for JSONB queries
    CONSTRAINT valid_access CHECK (public_access IN ('private', 'organization', 'public'))
);

-- Indexes
CREATE INDEX idx_maps_owner ON maps(owner_id);
CREATE INDEX idx_maps_org ON maps(organization_id);
CREATE INDEX idx_maps_tags ON maps USING GIN(tags);
CREATE INDEX idx_maps_document ON maps USING GIN(document jsonb_path_ops);
CREATE INDEX idx_maps_name_search ON maps USING GIN(to_tsvector('english', name || ' ' || COALESCE(description, '')));
```

**Benefits:**
- ✅ JSONB for flexible schema
- ✅ GIN indexes for fast queries
- ✅ Full-text search on name/description
- ✅ ACID guarantees
- ✅ Proven at scale (100M+ documents)

#### Option 2: Document Store (MongoDB, CosmosDB)

**Alternative for cloud-native deployments**

```javascript
{
  _id: ObjectId("..."),
  ...honuaMapDocument,
  _partition: "org-123",
  _searchTerms: ["parcels", "san francisco"],
  _created: ISODate("2025-11-11"),
  _updated: ISODate("2025-11-11")
}
```

**Benefits:**
- ✅ Native JSON storage
- ✅ Flexible schema
- ✅ Horizontal scalability
- ❌ Eventual consistency (some DBs)
- ❌ More complex transactions

#### Decision: PostgreSQL with JSONB

**Rationale:**
- Honua.Server already uses PostgreSQL
- Strong consistency guarantees
- Excellent JSONB support
- Lower operational complexity

---

## Versioning System

### Git-Inspired Versioning

**Concept:** Treat maps like code repositories

```
map-uuid (HEAD)
  ├── main (branch)
  │   ├── v1 → "Initial version"
  │   ├── v2 → "Added buildings layer"
  │   └── v3 → "Updated styling" (HEAD)
  ├── feature/analysis (branch)
  │   └── v4 → "WIP: Advanced analysis"
  └── tags
      └── production → v2
```

### Database Schema for Versioning

```sql
-- Map versions table
CREATE TABLE map_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    version_number INT NOT NULL,
    branch VARCHAR(100) DEFAULT 'main',

    -- Full document snapshot
    document JSONB NOT NULL,

    -- Version metadata
    commit_message TEXT,
    author_id UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    parent_version_id UUID REFERENCES map_versions(id),

    -- Diff from parent (optional, for efficiency)
    diff JSONB,

    UNIQUE(map_id, branch, version_number)
);

-- Map branches
CREATE TABLE map_branches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    map_id UUID NOT NULL REFERENCES maps(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    head_version_id UUID REFERENCES map_versions(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by UUID NOT NULL REFERENCES users(id),

    UNIQUE(map_id, name)
);

-- Map tags (named versions)
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

-- Audit log
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

### Versioning Operations

#### 1. **Create Initial Version**

```csharp
// Service method
public async Task<MapVersion> CreateMapAsync(HonuaMapDocument document, string userId)
{
    using var transaction = await _db.BeginTransactionAsync();

    // Create map record
    var map = new Map
    {
        Id = Guid.NewGuid(),
        Name = document.Metadata.Name,
        OwnerId = userId,
        Document = document,
        CreatedAt = DateTime.UtcNow
    };
    await _db.Maps.AddAsync(map);

    // Create main branch
    var branch = new MapBranch
    {
        MapId = map.Id,
        Name = "main",
        CreatedBy = userId
    };
    await _db.MapBranches.AddAsync(branch);

    // Create v1
    var version = new MapVersion
    {
        MapId = map.Id,
        VersionNumber = 1,
        Branch = "main",
        Document = document,
        CommitMessage = "Initial version",
        AuthorId = userId
    };
    await _db.MapVersions.AddAsync(version);

    // Update branch HEAD
    branch.HeadVersionId = version.Id;

    // Audit
    await _auditLog.LogAsync(new MapAuditEntry
    {
        MapId = map.Id,
        VersionId = version.Id,
        Action = "create",
        UserId = userId
    });

    await transaction.CommitAsync();
    return version;
}
```

#### 2. **Save New Version**

```csharp
public async Task<MapVersion> SaveVersionAsync(
    Guid mapId,
    HonuaMapDocument document,
    string commitMessage,
    string userId,
    string branch = "main")
{
    using var transaction = await _db.BeginTransactionAsync();

    // Get current HEAD
    var currentHead = await _db.MapVersions
        .Where(v => v.MapId == mapId && v.Branch == branch)
        .OrderByDescending(v => v.VersionNumber)
        .FirstOrDefaultAsync();

    // Calculate diff (optional optimization)
    var diff = JsonDiffService.CalculateDiff(
        currentHead?.Document,
        document
    );

    // Create new version
    var version = new MapVersion
    {
        MapId = mapId,
        VersionNumber = (currentHead?.VersionNumber ?? 0) + 1,
        Branch = branch,
        Document = document,
        Diff = diff,
        CommitMessage = commitMessage,
        AuthorId = userId,
        ParentVersionId = currentHead?.Id
    };
    await _db.MapVersions.AddAsync(version);

    // Update map's current document
    var map = await _db.Maps.FindAsync(mapId);
    map.Document = document;
    map.UpdatedAt = DateTime.UtcNow;

    // Update branch HEAD
    var branchRecord = await _db.MapBranches
        .FirstAsync(b => b.MapId == mapId && b.Name == branch);
    branchRecord.HeadVersionId = version.Id;

    // Audit
    await _auditLog.LogAsync(new MapAuditEntry
    {
        MapId = mapId,
        VersionId = version.Id,
        Action = "update",
        UserId = userId,
        Details = new { commitMessage, branch, diff = diff != null }
    });

    await transaction.CommitAsync();
    return version;
}
```

#### 3. **Create Branch**

```csharp
public async Task<MapBranch> CreateBranchAsync(
    Guid mapId,
    string branchName,
    string sourceBranch,
    string userId)
{
    // Get source branch HEAD
    var sourceHead = await _db.MapBranches
        .Include(b => b.HeadVersion)
        .FirstAsync(b => b.MapId == mapId && b.Name == sourceBranch);

    // Create new branch pointing to same HEAD
    var branch = new MapBranch
    {
        MapId = mapId,
        Name = branchName,
        HeadVersionId = sourceHead.HeadVersionId,
        CreatedBy = userId
    };
    await _db.MapBranches.AddAsync(branch);
    await _db.SaveChangesAsync();

    return branch;
}
```

#### 4. **Tag Version**

```csharp
public async Task<MapTag> TagVersionAsync(
    Guid mapId,
    Guid versionId,
    string tagName,
    string description,
    string userId)
{
    var tag = new MapTag
    {
        MapId = mapId,
        VersionId = versionId,
        Name = tagName,
        Description = description,
        CreatedBy = userId
    };
    await _db.MapTags.AddAsync(tag);
    await _db.SaveChangesAsync();

    return tag;
}
```

#### 5. **Revert to Version**

```csharp
public async Task<MapVersion> RevertToVersionAsync(
    Guid mapId,
    Guid targetVersionId,
    string userId,
    string branch = "main")
{
    var targetVersion = await _db.MapVersions.FindAsync(targetVersionId);

    // Create new version with old document
    return await SaveVersionAsync(
        mapId,
        targetVersion.Document,
        $"Revert to version {targetVersion.VersionNumber}",
        userId,
        branch
    );
}
```

#### 6. **Get Version History**

```csharp
public async Task<List<MapVersionInfo>> GetVersionHistoryAsync(
    Guid mapId,
    string branch = "main")
{
    return await _db.MapVersions
        .Where(v => v.MapId == mapId && v.Branch == branch)
        .OrderByDescending(v => v.VersionNumber)
        .Select(v => new MapVersionInfo
        {
            Id = v.Id,
            VersionNumber = v.VersionNumber,
            CommitMessage = v.CommitMessage,
            Author = v.Author.Name,
            CreatedAt = v.CreatedAt,
            ParentId = v.ParentVersionId
        })
        .ToListAsync();
}
```

### Version Diff Visualization

**JSON Patch (RFC 6902) for diffs:**

```json
[
  { "op": "replace", "path": "/operationalLayers/0/opacity", "value": 0.5 },
  { "op": "add", "path": "/operationalLayers/-", "value": { "id": "new-layer", ... } },
  { "op": "remove", "path": "/bookmarks/2" }
]
```

**Diff visualization in UI:**

```
v2 → v3 (2 changes)
├── Modified: layers/parcels/opacity (0.8 → 0.5)
└── Added: layers/buildings
```

---

## SDK Integration

### MapSDK Component API

#### Auto-Save Feature

```razor
<HonuaMap Id="map1"
          AutoSave="true"
          AutoSaveInterval="30000"
          MapId="@_mapId"
          OnSaved="HandleMapSaved">
    ...
</HonuaMap>

@code {
    private Guid _mapId = Guid.Parse("...");

    private async Task HandleMapSaved(MapSaveEventArgs args)
    {
        Snackbar.Add($"Map saved: v{args.VersionNumber}", Severity.Success);
    }
}
```

#### Manual Save

```csharp
@inject IMapPersistenceService MapPersistence

private async Task SaveMapAsync()
{
    var mapState = await _mapRef.GetStateAsync();

    var version = await MapPersistence.SaveVersionAsync(
        mapId: _currentMapId,
        document: mapState.ToHonuaMapDocument(),
        commitMessage: _commitMessage,
        userId: _currentUserId
    );

    Snackbar.Add($"Saved version {version.VersionNumber}", Severity.Success);
}
```

#### Load Map

```csharp
private async Task LoadMapAsync(Guid mapId, int? version = null)
{
    MapVersion mapVersion;

    if (version.HasValue)
    {
        // Load specific version
        mapVersion = await MapPersistence.GetVersionAsync(mapId, version.Value);
    }
    else
    {
        // Load latest
        mapVersion = await MapPersistence.GetLatestVersionAsync(mapId);
    }

    await _mapRef.LoadConfigurationAsync(mapVersion.Document);
}
```

#### Version History UI

```razor
<MudDrawer @bind-Open="_historyOpen" Anchor="Anchor.Right" Width="400px">
    <MudText Typo="Typo.h6">Version History</MudText>

    <MudList>
        @foreach (var version in _versions)
        {
            <MudListItem>
                <div class="d-flex justify-space-between">
                    <div>
                        <MudText Typo="Typo.body1">v@version.VersionNumber</MudText>
                        <MudText Typo="Typo.body2" Color="Color.Secondary">
                            @version.CommitMessage
                        </MudText>
                        <MudText Typo="Typo.caption">
                            @version.Author • @version.CreatedAt.Humanize()
                        </MudText>
                    </div>
                    <div>
                        <MudIconButton Icon="@Icons.Material.Filled.Restore"
                                      OnClick="() => RevertToVersion(version.Id)"
                                      Title="Revert to this version" />
                        <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                                      OnClick="() => PreviewVersion(version.Id)"
                                      Title="Preview" />
                    </div>
                </div>
            </MudListItem>
        }
    </MudList>
</MudDrawer>
```

### ComponentBus Messages

```csharp
// Map state changed (debounced)
public class MapStateChangedMessage
{
    public string MapId { get; init; }
    public HonuaMapDocument CurrentState { get; init; }
    public bool HasUnsavedChanges { get; init; }
}

// Map saved
public class MapSavedMessage
{
    public Guid MapId { get; init; }
    public Guid VersionId { get; init; }
    public int VersionNumber { get; init; }
    public string CommitMessage { get; init; }
}

// Map loaded
public class MapLoadedMessage
{
    public Guid MapId { get; init; }
    public Guid VersionId { get; init; }
    public int VersionNumber { get; init; }
    public HonuaMapDocument Document { get; init; }
}
```

---

## API Design

### RESTful API Endpoints

```
# Maps CRUD
GET    /api/v1/maps                          # List maps
POST   /api/v1/maps                          # Create map
GET    /api/v1/maps/{id}                     # Get map (latest version)
PUT    /api/v1/maps/{id}                     # Update map
DELETE /api/v1/maps/{id}                     # Delete map

# Versions
GET    /api/v1/maps/{id}/versions            # List versions
POST   /api/v1/maps/{id}/versions            # Create version (save)
GET    /api/v1/maps/{id}/versions/{version}  # Get specific version
POST   /api/v1/maps/{id}/revert/{version}    # Revert to version

# Branches
GET    /api/v1/maps/{id}/branches            # List branches
POST   /api/v1/maps/{id}/branches            # Create branch
GET    /api/v1/maps/{id}/branches/{name}     # Get branch HEAD
DELETE /api/v1/maps/{id}/branches/{name}     # Delete branch

# Tags
GET    /api/v1/maps/{id}/tags                # List tags
POST   /api/v1/maps/{id}/tags                # Create tag
DELETE /api/v1/maps/{id}/tags/{name}         # Delete tag

# Diff & Merge
GET    /api/v1/maps/{id}/diff/{v1}/{v2}      # Diff between versions
POST   /api/v1/maps/{id}/merge               # Merge branches

# Export
GET    /api/v1/maps/{id}/export/mapbox       # Export as Mapbox Style
GET    /api/v1/maps/{id}/export/arcgis       # Export as ArcGIS Web Map
GET    /api/v1/maps/{id}/export/json         # Export as Honua JSON
GET    /api/v1/maps/{id}/export/yaml         # Export as YAML

# Search
GET    /api/v1/maps/search?q={query}         # Full-text search
GET    /api/v1/maps?tags=parcels,sf          # Filter by tags
```

### Example API Usage

```bash
# Create map
curl -X POST https://api.honua.io/v1/maps \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @map.json

# Save new version
curl -X POST https://api.honua.io/v1/maps/uuid/versions \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"commitMessage": "Updated layer styling"}'

# Get version history
curl https://api.honua.io/v1/maps/uuid/versions

# Create tag
curl -X POST https://api.honua.io/v1/maps/uuid/tags \
  -d '{"name": "production", "versionId": "version-uuid"}'

# Diff two versions
curl https://api.honua.io/v1/maps/uuid/diff/2/3
```

---

## Implementation Roadmap

### Phase 1: Foundation (2 weeks)

**Week 1:**
- [ ] Design database schema
- [ ] Create migrations
- [ ] Implement `Map`, `MapVersion`, `MapBranch`, `MapTag` entities
- [ ] Create `IMapPersistenceService` interface
- [ ] Implement basic CRUD operations

**Week 2:**
- [ ] Implement versioning logic (save, load, list)
- [ ] Add audit logging
- [ ] Create REST API controllers
- [ ] Write unit tests
- [ ] API documentation (OpenAPI/Swagger)

### Phase 2: SDK Integration (1 week)

- [ ] Extend `MapConfiguration` to support full HMD spec
- [ ] Add `IMapPersistenceService` to MapSDK DI
- [ ] Implement auto-save in `HonuaMap` component
- [ ] Create version history UI component
- [ ] Add ComponentBus messages
- [ ] Testing with live maps

### Phase 3: Advanced Features (2 weeks)

**Week 1:**
- [ ] Branch support (create, list, delete)
- [ ] Tag support (create, list, delete)
- [ ] Diff calculation (JSON Patch)
- [ ] Diff visualization UI

**Week 2:**
- [ ] Revert to version
- [ ] Branch merging
- [ ] Conflict resolution UI
- [ ] Export to Mapbox Style Spec
- [ ] Export to ArcGIS Web Map

### Phase 4: Polish & Documentation (1 week)

- [ ] Performance optimization (indexing, caching)
- [ ] Comprehensive documentation
- [ ] Migration guide for existing maps
- [ ] Video tutorials
- [ ] Example applications

---

## Security Considerations

### Access Control

```csharp
public enum MapPermission
{
    View,
    Edit,
    Delete,
    ManageVersions,
    ManageBranches,
    Publish
}

// Check permission before operations
public async Task<MapVersion> SaveVersionAsync(...)
{
    if (!await _authz.HasPermissionAsync(userId, mapId, MapPermission.Edit))
        throw new UnauthorizedException("No edit permission");

    // ... proceed with save
}
```

### Row-Level Security (PostgreSQL)

```sql
-- Enable RLS
ALTER TABLE maps ENABLE ROW LEVEL SECURITY;

-- Policy: Users can see maps they own or maps in their org
CREATE POLICY maps_select_policy ON maps
    FOR SELECT
    USING (
        owner_id = current_user_id() OR
        organization_id = current_org_id() OR
        public_access = 'public'
    );

-- Policy: Users can update maps they own
CREATE POLICY maps_update_policy ON maps
    FOR UPDATE
    USING (owner_id = current_user_id())
    WITH CHECK (owner_id = current_user_id());
```

### Audit Trail

Every operation logged:
- Who (user_id)
- What (action, details)
- When (timestamp)
- Where (IP address, user agent)
- Which version (version_id)

---

## Performance Optimization

### 1. **Caching Strategy**

```csharp
// Cache latest version in Redis
public async Task<HonuaMapDocument> GetLatestAsync(Guid mapId)
{
    var cacheKey = $"map:{mapId}:latest";

    // Try cache first
    var cached = await _cache.GetAsync<HonuaMapDocument>(cacheKey);
    if (cached != null) return cached;

    // Load from DB
    var map = await _db.Maps.FindAsync(mapId);

    // Cache for 5 minutes
    await _cache.SetAsync(cacheKey, map.Document, TimeSpan.FromMinutes(5));

    return map.Document;
}
```

### 2. **Pagination**

```csharp
public async Task<PagedResult<MapSummary>> ListMapsAsync(
    int page = 1,
    int pageSize = 20)
{
    var query = _db.Maps
        .Where(m => m.PublicAccess == "public" || m.OwnerId == _currentUserId);

    var total = await query.CountAsync();

    var items = await query
        .OrderByDescending(m => m.UpdatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m => new MapSummary { ... })
        .ToListAsync();

    return new PagedResult<MapSummary>
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = total,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };
}
```

### 3. **Lazy Loading Versions**

```csharp
// Only load version history when explicitly requested
// Don't eager-load all versions with every map query

public async Task<List<MapVersionInfo>> GetVersionHistoryAsync(Guid mapId)
{
    // Use projection to only load necessary fields
    return await _db.MapVersions
        .Where(v => v.MapId == mapId)
        .Select(v => new MapVersionInfo
        {
            Id = v.Id,
            VersionNumber = v.VersionNumber,
            CommitMessage = v.CommitMessage,
            CreatedAt = v.CreatedAt
            // Don't include full document
        })
        .ToListAsync();
}
```

### 4. **Diff Storage**

```csharp
// Store diffs instead of full documents for space efficiency
// Reconstruct document on-demand by applying diffs

public async Task<HonuaMapDocument> GetVersionDocumentAsync(Guid versionId)
{
    var version = await _db.MapVersions
        .Include(v => v.Parent)
        .FirstAsync(v => v.Id == versionId);

    // If full document stored, return it
    if (version.Document != null)
        return version.Document;

    // Otherwise, reconstruct from parent + diff
    var parentDoc = await GetVersionDocumentAsync(version.ParentVersionId.Value);
    return JsonPatchService.ApplyPatch(parentDoc, version.Diff);
}
```

---

## Migration Strategy

### Existing Maps

```csharp
public async Task MigrateExistingMapsAsync()
{
    var existingMaps = await _db.Maps
        .Where(m => m.Document != null)
        .ToListAsync();

    foreach (var map in existingMaps)
    {
        // Create initial version
        var version = new MapVersion
        {
            MapId = map.Id,
            VersionNumber = 1,
            Branch = "main",
            Document = map.Document,
            CommitMessage = "Migrated from legacy format",
            AuthorId = map.OwnerId,
            CreatedAt = map.CreatedAt
        };

        await _db.MapVersions.AddAsync(version);

        // Create main branch
        var branch = new MapBranch
        {
            MapId = map.Id,
            Name = "main",
            HeadVersionId = version.Id,
            CreatedBy = map.OwnerId
        };

        await _db.MapBranches.AddAsync(branch);
    }

    await _db.SaveChangesAsync();
}
```

---

## Conclusion

This architecture provides:

1. ✅ **Standards-based**: Uses Mapbox Style Spec + custom extensions
2. ✅ **Complete state capture**: All map config, data, UI state
3. ✅ **Git-like versioning**: Branches, tags, diffs, reverts
4. ✅ **Reliable**: ACID transactions, audit logs
5. ✅ **SDK-integrated**: Seamless save/load from components
6. ✅ **Performant**: Caching, pagination, diff storage
7. ✅ **Secure**: Row-level security, access control
8. ✅ **Interoperable**: Export to Mapbox/ArcGIS formats

**Next Steps:**
1. Review and approve architecture
2. Create database migrations
3. Implement Phase 1 (Foundation)
4. Begin Phase 2 (SDK Integration)

---

**Questions or feedback?** Contact the Honua architecture team.
