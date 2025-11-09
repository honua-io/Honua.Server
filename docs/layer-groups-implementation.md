# Layer Groups Implementation

## Overview

This document describes the implementation of Layer Groups for Honua.Server. Layer Groups are renderable composites that allow multiple layers to be served as a single unit, similar to GeoServer's layer group functionality.

## Implementation Status

### Phase 1: Data Model - COMPLETE

#### Files Created

1. **`/src/Honua.Server.Core/Metadata/LayerGroupDefinition.cs`**
   - Main record types for layer group definitions
   - `LayerGroupDefinition`: The main layer group record with all properties
   - `LayerGroupMember`: Represents a member (layer or nested group) in the group
   - `LayerGroupMemberType`: Enum for Layer or Group types
   - `RenderMode`: Enum for Single, Opaque, or Transparent rendering
   - `LayerGroupWmsDefinition`: WMS-specific configuration

2. **`/src/Honua.Server.Core/Metadata/LayerGroupExpander.cs`**
   - Helper class for expanding layer groups into component layers
   - `ExpandLayerGroup()`: Recursively expands groups, handling nesting
   - `GetReferencedLayerIds()`: Gets all unique layers in a group
   - `CalculateGroupExtent()`: Calculates combined bounding box
   - `GetSupportedCrs()`: Gets common CRS from all member layers
   - `ExpandedLayerMember`: Record for expanded layer with opacity and style

#### Files Modified

3. **`/src/Honua.Server.Core/Metadata/MetadataSnapshot.cs`**
   - Added `LayerGroups` property to constructor and public properties
   - Added `_layerGroupIndex` for fast lookup
   - Added comprehensive validation in `ValidateMetadata()`:
     - Unique layer group IDs
     - Valid service references
     - Valid layer/group member references
     - Opacity range validation (0-1)
     - Style reference validation
     - Scale value validation
     - Circular reference detection
     - Cross-service reference prevention
   - Added `TryGetLayerGroup()` and `GetLayerGroup()` helper methods
   - Added `DetectCircularGroupReferences()` recursive validation method

#### Example Configuration

4. **`/examples/layer-groups-example.json`**
   - Complete example showing:
     - Basic layer group with multiple layers
     - Nested layer groups
     - Different opacity settings
     - Style overrides
     - WMS configuration options

### Phase 2: WMS Integration - IN PROGRESS

#### Files Modified

1. **`/src/Honua.Server.Host/Wms/WmsCapabilitiesBuilder.cs`**
   - Updated `BuildRootLayer()` to include layer groups from all WMS-enabled services
   - Added `BuildLayerGroupElement()` method to create WMS layer elements:
     - Layer name: `{serviceId}:{groupId}`
     - Queryable attribute
     - Keywords from group and catalog
     - Supported CRS (explicit or calculated from members)
     - Bounding box (explicit or calculated from member layers)
     - Styles (group styles or default)
     - Scale denominators (min/max scale)

#### Still To Do

2. **WMS GetMap Handler** (PENDING)
   - Recognize layer group names in LAYERS parameter
   - Expand groups to component layers
   - Apply group-level styling and opacity
   - Render composite image
   - Support different render modes (Single, Opaque, Transparent)

3. **WMS GetFeatureInfo Handler** (PENDING)
   - Support queries on layer groups
   - Return info from all queryable component layers
   - Aggregate results appropriately

### Phase 3: WFS Integration - NOT STARTED

1. **WFS GetCapabilities** (PENDING)
   - Include layer groups in FeatureTypeList
   - Show group metadata

2. **WFS GetFeature** (PENDING)
   - Support layer group names in TYPENAME parameter
   - Return features from all component layers
   - Proper namespace handling

### Phase 4: OGC API Features Integration - NOT STARTED

1. **Collections Endpoint** (PENDING)
   - List layer groups as collections
   - Include group metadata and member information

2. **Items Endpoint** (PENDING)
   - Support querying items from layer groups
   - Aggregate features from component layers

### Phase 5: Admin UI - NOT STARTED

1. **Layer Group Manager** (PENDING)
   - Blazor components for CRUD operations
   - Visual group builder with drag-drop
   - Layer ordering interface
   - Opacity and style controls
   - Preview functionality

2. **REST API Endpoints** (PENDING)
   - GET `/admin/metadata/layergroups` - List all groups
   - GET `/admin/metadata/layergroups/{id}` - Get specific group
   - POST `/admin/metadata/layergroups` - Create new group
   - PUT `/admin/metadata/layergroups/{id}` - Update group
   - DELETE `/admin/metadata/layergroups/{id}` - Delete group
   - Validation endpoints

### Phase 6: Caching - NOT STARTED

1. **Tile Cache Integration** (PENDING)
   - Cache layer group composites
   - Invalidation when component layers change
   - Preseed support for groups

## Key Features Implemented

### 1. Flexible Layer Group Definition

```json
{
  "id": "base-map",
  "title": "Base Map 2025",
  "serviceId": "mapping-service",
  "renderMode": "Single",
  "members": [
    {
      "type": "Layer",
      "layerId": "parcels",
      "order": 0,
      "opacity": 0.7
    },
    {
      "type": "Layer",
      "layerId": "roads",
      "order": 1,
      "opacity": 1.0
    }
  ]
}
```

### 2. Nested Groups Support

```json
{
  "id": "complete-map",
  "members": [
    {
      "type": "Group",
      "groupId": "cadastral-base",
      "order": 0,
      "opacity": 1.0
    },
    {
      "type": "Layer",
      "layerId": "roads",
      "order": 1
    }
  ]
}
```

### 3. Comprehensive Validation

- **ID Uniqueness**: Ensures no duplicate layer group IDs
- **Reference Validation**: All referenced layers and groups must exist
- **Service Binding**: Groups can only reference layers/groups from the same service
- **Circular Reference Detection**: Prevents group A → group B → group A
- **Opacity Range**: Validates 0.0 ≤ opacity ≤ 1.0
- **Style References**: Validates all style IDs exist
- **Scale Validation**: Ensures minScale ≤ maxScale

### 4. Smart Group Expansion

The `LayerGroupExpander` class provides:
- Recursive expansion of nested groups
- Cumulative opacity calculation
- Style override handling
- Automatic bounding box calculation
- Common CRS determination

### 5. WMS Integration

- Layer groups appear in WMS GetCapabilities as named layers
- Format: `{serviceId}:{groupId}`
- Includes all metadata: bounding box, CRS, styles, keywords
- Configurable visibility via `wms.advertiseInCapabilities`
- Queryable flag support

## Technical Design Decisions

### 1. Record Types

Following existing Honua.Server patterns, all definitions use C# 12 record types:
- Immutable by default
- Value-based equality
- Concise syntax
- Thread-safe

### 2. Validation Strategy

Validation occurs in `MetadataSnapshot` constructor:
- Two-pass validation (first pass validates basic structure, second pass validates references)
- Fail-fast approach with descriptive error messages
- Validation prevents circular references before they can cause runtime issues

### 3. Expansion Strategy

The `LayerGroupExpander` uses recursive depth-first traversal:
- Visited tracking prevents infinite loops
- Cumulative opacity calculation (parent × child)
- Order preservation (lower order values render first/bottom)
- Lazy evaluation - only expands when needed

### 4. Service Isolation

Layer groups are bound to a single service:
- Prevents cross-service dependencies
- Simplifies permission and access control
- Aligns with existing service-based architecture

## Usage Examples

### Basic Layer Group

```json
{
  "layerGroups": [
    {
      "id": "transportation",
      "title": "Transportation Networks",
      "serviceId": "mapping-service",
      "members": [
        {"type": "Layer", "layerId": "highways", "order": 0, "opacity": 1.0},
        {"type": "Layer", "layerId": "streets", "order": 1, "opacity": 0.8},
        {"type": "Layer", "layerId": "bike-paths", "order": 2, "opacity": 0.6}
      ]
    }
  ]
}
```

### WMS GetCapabilities Response

```xml
<Layer queryable="1">
  <Name>mapping-service:transportation</Name>
  <Title>Transportation Networks</Title>
  <CRS>EPSG:4326</CRS>
  <CRS>EPSG:3857</CRS>
  <EX_GeographicBoundingBox>
    <westBoundLongitude>-122.5</westBoundLongitude>
    <eastBoundLongitude>-122.0</eastBoundLongitude>
    <southBoundLatitude>37.5</southBoundLatitude>
    <northBoundLatitude>38.0</northBoundLatitude>
  </EX_GeographicBoundingBox>
  <BoundingBox CRS="EPSG:4326" minx="37.5" miny="-122.5" maxx="38.0" maxy="-122.0"/>
  <Style>
    <Name>default</Name>
    <Title>Default</Title>
  </Style>
</Layer>
```

### WMS GetMap Request

```
GET /wms?
  SERVICE=WMS&
  VERSION=1.3.0&
  REQUEST=GetMap&
  LAYERS=mapping-service:transportation&
  CRS=EPSG:4326&
  BBOX=37.5,-122.5,38.0,-122.0&
  WIDTH=800&
  HEIGHT=600&
  FORMAT=image/png
```

## Testing Strategy

### Unit Tests (Pending)

1. **LayerGroupDefinitionTests**
   - Validation of required fields
   - Member type validation
   - Opacity range validation

2. **MetadataSnapshotValidationTests**
   - Circular reference detection
   - Cross-service reference prevention
   - Duplicate ID detection
   - Style reference validation

3. **LayerGroupExpanderTests**
   - Basic expansion
   - Nested group expansion
   - Opacity calculation
   - CRS aggregation
   - Bounding box calculation

### Integration Tests (Pending)

1. **WmsCapabilitiesTests**
   - Layer groups appear in capabilities
   - Correct metadata in XML
   - Multiple services with groups

2. **WmsGetMapTests**
   - Group expansion
   - Composite rendering
   - Style application
   - Opacity handling

## Migration Guide

### Adding Layer Groups to Existing Metadata

1. Add the `layerGroups` array to your metadata.json:

```json
{
  "catalog": {...},
  "services": [...],
  "layers": [...],
  "layerGroups": [
    {
      "id": "my-group",
      "title": "My Layer Group",
      "serviceId": "my-service",
      "members": [...]
    }
  ]
}
```

2. Ensure all referenced layers and styles exist
3. Validate the configuration (errors will be thrown on startup)
4. Test WMS GetCapabilities to see the group

## Future Enhancements

1. **Dynamic Groups**: Support for groups defined by filters/queries
2. **Time-aware Groups**: Temporal synchronization across members
3. **Security Integration**: Per-group permissions and access control
4. **Performance Optimization**: Caching of expanded group structures
5. **Client Libraries**: Update MapSDK to support layer groups
6. **Group Templates**: Reusable group configurations

## References

- [OGC WMS 1.3.0 Specification](http://www.opengeospatial.org/standards/wms)
- [GeoServer Layer Groups Documentation](https://docs.geoserver.org/stable/en/user/data/webadmin/layergroups.html)
- [Existing LayerDefinition Implementation](/src/Honua.Server.Core/Metadata/MetadataSnapshot.cs)
