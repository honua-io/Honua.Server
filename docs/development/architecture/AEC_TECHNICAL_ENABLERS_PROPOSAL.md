# AEC Technical Enablers - Proposal & Implementation Plan

**Date:** November 10, 2025
**Status:** Proposal for Review
**Target Verticals:** Architecture, Engineering, Construction

---

## Executive Summary

This document proposes **7 core technical enablers** to transform Honua.Server into a comprehensive platform for Architecture, Engineering, and Construction (AEC) workflows. These enablers focus on **foundational capabilities** rather than vertical-specific solutions, maintaining the platform's flexibility while enabling sophisticated AEC use cases.

**Key Additions:**
1. Built-in Graph Database for semantic relationships
2. Complex 3D geometry support (meshes, solids)
3. IFC (BIM) file parsing and validation
4. Temporal versioning system (4D scheduling)
5. Document/drawing management
6. Relationship modeling engine
7. Cost data integration (5D)

**Implementation Timeline:** 6-9 months (phased approach)
**Estimated Effort:** 2-3 full-time engineers

---

## Current State Analysis

### ✅ Existing Strengths
- 3D GeoJSON geometry (Z-coordinates)
- Spatial analysis (NetTopologySuite)
- Real-time sensor integration
- Time-series data management
- Asset tracking and field collection
- External relationship modeling (Azure Digital Twins)

### ❌ Critical Gaps for AEC
- No support for complex 3D primitives (meshes, B-reps, CSG)
- No IFC/BIM file format support
- No built-in graph database for component hierarchies
- No temporal versioning (construction phases, design iterations)
- No document version control
- Limited relationship modeling (requires Azure)
- No cost/schedule data integration

---

## Proposed Technical Enablers

### Priority 1: Foundation (Must Have)

#### 1.1 Built-in Graph Database for Semantic Relationships

**Problem:** AEC requires rich hierarchical relationships (building → floor → room → equipment, or system → subsystem → component).

**Current Limitation:** Relationship modeling requires Azure Digital Twins (cloud dependency, vendor lock-in).

**Proposed Solution:** Integrate Apache Age (PostgreSQL extension) for native graph capabilities.

**Technical Details:**
```sql
-- Apache AGE provides graph database on top of PostgreSQL
-- No additional database needed
CREATE GRAPH honua_relationships;

-- Example: Building hierarchy
CREATE (:Building {id: 'bldg-1', name: 'Office Tower A'})
  -[:CONTAINS]->(:Floor {level: 1, area_sqm: 1200})
  -[:CONTAINS]->(:Room {number: '101', type: 'office'})
  -[:HAS]->(:Equipment {id: 'hvac-001', type: 'air_handler'});

-- Example: Utility network
CREATE (:WaterMain {id: 'wm-001', diameter_mm: 300})
  -[:FEEDS]->(:Valve {id: 'v-123', status: 'open'})
  -[:FEEDS]->(:ServiceLine {id: 'sl-456', material: 'copper'});

-- Graph traversal queries
SELECT * FROM cypher('honua_relationships', $$
  MATCH (b:Building)-[:CONTAINS*]->(r:Room)
  WHERE b.id = 'bldg-1'
  RETURN r
$$) as (room agtype);
```

**Benefits:**
- No cloud dependency
- Native PostgreSQL integration
- Cypher query language (industry standard)
- Sub-millisecond graph traversals
- Automatic spatial + graph queries in single database

**Integration Points:**
- OGC API Features endpoints return relationship metadata
- GraphQL API for graph traversals
- Relationship sync to/from Azure Digital Twins (optional)

**Implementation Effort:** 3-4 weeks

---

#### 1.2 Complex 3D Geometry Support

**Problem:** AEC uses complex 3D shapes beyond simple [lon, lat, z] coordinates - meshes, curved surfaces, parametric solids.

**Current Limitation:** Only GeoJSON 3D (points, lines, polygons with Z).

**Proposed Solution:** Extend geometry system to support:
- **Triangle meshes** (OBJ, STL, glTF formats)
- **B-Rep (Boundary Representation)** solids
- **Parametric surfaces** (NURBS curves/surfaces)
- **CSG (Constructive Solid Geometry)** operations

**Technical Stack:**
- **OpenCascade Technology (OCCT)** - Industry-standard 3D geometry kernel (LGPL)
  - Used by FreeCAD, Blender, and many commercial CAD tools
  - Full CSG operations (union, intersection, difference)
  - NURBS curve/surface support
  - STEP/IGES file import/export

**Data Model Extension:**
```csharp
// New geometry types in Honua.Server.Core
public enum GeometryType3D
{
    // Existing
    Point3D,
    LineString3D,
    Polygon3D,

    // New complex types
    TriangleMesh,      // OBJ, STL, glTF
    BRepSolid,         // Boundary representation
    ParametricSurface, // NURBS
    CsgSolid          // Constructive solid geometry
}

public class ComplexGeometry3D
{
    public GeometryType3D Type { get; set; }
    public byte[] GeometryData { get; set; } // Binary serialization
    public BoundingBox3D BoundingBox { get; set; }
    public Dictionary<string, object> Metadata { get; set; }

    // Supported formats
    public static ComplexGeometry3D FromOBJ(Stream objFile);
    public static ComplexGeometry3D FromSTL(Stream stlFile);
    public static ComplexGeometry3D FromGLTF(Stream gltfFile);
    public static ComplexGeometry3D FromSTEP(Stream stepFile);
}
```

**Storage Strategy:**
```sql
-- Store complex geometry as binary + metadata
CREATE TABLE complex_geometries (
    id UUID PRIMARY KEY,
    feature_id UUID REFERENCES features(id),
    geometry_type VARCHAR(50),
    geometry_data BYTEA,         -- Binary OCCT format
    bounding_box GEOMETRY(POLYHEDRONZ, 4979),
    vertex_count INTEGER,
    face_count INTEGER,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Spatial index on bounding box
CREATE INDEX idx_complex_geom_bbox ON complex_geometries USING GIST (bounding_box);
```

**API Endpoints:**
```
POST /api/geometry/3d/upload
  - Accept: OBJ, STL, glTF, STEP files
  - Parse and validate
  - Store with bounding box

GET /api/geometry/3d/{id}?format=obj
  - Export to requested format

POST /api/geometry/3d/operations/union
  - CSG operations (union, intersection, difference)
  - Boolean operations on solids
```

**Benefits:**
- Support for real engineering models
- Boolean operations for design workflows
- Industry-standard file format support
- Efficient spatial indexing

**Implementation Effort:** 6-8 weeks

---

#### 1.3 IFC (BIM) File Parsing and Validation

**Problem:** BIM workflows require Industry Foundation Classes (IFC) format support.

**Current Limitation:** No IFC support at all.

**Proposed Solution:** Integrate IfcOpenShell (LGPL) for IFC parsing.

**Technical Details:**
```csharp
// New service: Honua.Server.Core/Services/IfcImportService.cs
public interface IIfcImportService
{
    Task<IfcImportResult> ImportIfcFileAsync(Stream ifcFile, ImportOptions options);
    Task<IEnumerable<Feature>> ConvertIfcToFeaturesAsync(string ifcFilePath, string layerId);
    Task<ValidationResult> ValidateIfcAsync(Stream ifcFile);
}

public class IfcImportOptions
{
    public bool ImportGeometry { get; set; } = true;
    public bool ImportProperties { get; set; } = true;
    public bool ImportRelationships { get; set; } = true;
    public bool CreateGraphRelationships { get; set; } = true; // Use Apache AGE
    public string TargetLayerId { get; set; }
    public CoordinateTransformOptions GeoReference { get; set; }
}

public class IfcImportResult
{
    public int FeaturesCreated { get; set; }
    public int RelationshipsCreated { get; set; }
    public Dictionary<string, int> EntityTypeCounts { get; set; }
    public List<ImportWarning> Warnings { get; set; }
    public BoundingBox3D ProjectExtent { get; set; }
}
```

**IFC Entity Mapping:**
```
IFC Entity                → Honua Feature + Graph Node
────────────────────────────────────────────────────────
IfcBuilding              → Building feature + :Building node
IfcBuildingStorey        → Floor feature + :Floor node
IfcSpace                 → Room feature + :Room node
IfcWall                  → Wall feature + :Wall node
IfcWindow                → Window feature + :Window node
IfcDoor                  → Door feature + :Door node
IfcBeam                  → Beam feature + :Beam node
IfcColumn                → Column feature + :Column node
IfcSlab                  → Slab feature + :Slab node

IFC Relationships        → Graph Edges
────────────────────────────────────────────────────────
IfcRelAggregates         → [:CONTAINS] relationship
IfcRelContainedInSpatial → [:LOCATED_IN] relationship
IfcRelConnectsElements   → [:CONNECTS_TO] relationship
```

**Workflow:**
```
1. Upload IFC file → Parse with IfcOpenShell
2. Extract geometric representations → Convert to ComplexGeometry3D
3. Extract properties → Store as feature attributes
4. Extract relationships → Create graph edges (Apache AGE)
5. Geo-reference (if coordinates provided)
6. Create features in target layer
7. Return import summary
```

**API Endpoints:**
```
POST /api/ifc/import
  - Upload IFC file (IFC2x3, IFC4, IFC4.3)
  - Returns import job ID

GET /api/ifc/import/{jobId}/status
  - Poll import progress

GET /api/ifc/validate
  - Validate IFC file schema compliance

GET /api/ifc/metadata/{jobId}
  - Extract IFC project metadata
```

**Benefits:**
- Direct BIM model import
- Automatic graph relationship creation
- Property extraction
- Validation before import
- Supports latest IFC4.3 standard

**Implementation Effort:** 5-6 weeks

---

### Priority 2: Advanced Capabilities (Should Have)

#### 2.1 Temporal Versioning System (4D Scheduling)

**Problem:** Construction requires tracking feature changes over time (design iterations, construction phases, as-built vs. design).

**Current Limitation:** No built-in versioning. Time-series only for observations, not geometry.

**Proposed Solution:** Implement temporal tables with valid-time semantics.

**Technical Details:**
```sql
-- Temporal table structure
CREATE TABLE features_temporal (
    id UUID,
    version INTEGER,
    valid_from TIMESTAMPTZ NOT NULL,
    valid_to TIMESTAMPTZ,
    transaction_time TIMESTAMPTZ DEFAULT NOW(),

    -- Feature data
    service_id VARCHAR(255),
    layer_id VARCHAR(255),
    geometry GEOMETRY,
    properties JSONB,

    -- Version metadata
    change_type VARCHAR(20), -- 'created', 'modified', 'deleted'
    changed_by VARCHAR(255),
    change_reason TEXT,
    construction_phase VARCHAR(100), -- 'design', 'demolition', 'foundation', 'structure', etc.

    PRIMARY KEY (id, version),
    FOREIGN KEY (id) REFERENCES features(id)
);

-- Temporal indexes
CREATE INDEX idx_features_temporal_valid_time ON features_temporal (id, valid_from, valid_to);
CREATE INDEX idx_features_temporal_phase ON features_temporal (construction_phase);

-- Query features at specific time
SELECT * FROM features_temporal
WHERE id = 'feature-123'
  AND valid_from <= '2025-06-01'
  AND (valid_to IS NULL OR valid_to > '2025-06-01');

-- Query features during construction phase
SELECT * FROM features_temporal
WHERE construction_phase = 'foundation'
  AND valid_from <= NOW()
  AND (valid_to IS NULL OR valid_to > NOW());
```

**API Extensions:**
```
GET /api/features?asOf=2025-06-01T00:00:00Z
  - Time-travel queries

GET /api/features?phase=foundation
  - Filter by construction phase

GET /api/features/{id}/history
  - Get all versions of a feature

POST /api/features/{id}/versions
  - Create new version with validity period

GET /api/layers/{layerId}/timeline
  - Get construction timeline visualization data
```

**4D Visualization:**
```csharp
public class ConstructionTimelineService
{
    public async Task<TimelineData> GetConstructionTimelineAsync(
        string layerId,
        DateTime startDate,
        DateTime endDate,
        TimeSpan interval)
    {
        // Return feature states at each time interval
        // Client can animate construction progress
    }
}
```

**Benefits:**
- Track design evolution
- Construction scheduling integration
- As-built vs. design comparison
- Audit trail for compliance
- 4D BIM visualization

**Implementation Effort:** 4-5 weeks

---

#### 2.2 Document & Drawing Management

**Problem:** AEC workflows require managing PDFs, DWG files, specifications, and their versions.

**Current Limitation:** No document management system.

**Proposed Solution:** Lightweight document management with versioning and spatial linking.

**Technical Details:**
```csharp
// New service: Honua.Server.Core/Services/DocumentManagementService.cs
public interface IDocumentManagementService
{
    Task<Document> UploadDocumentAsync(Stream file, DocumentMetadata metadata);
    Task<Document> CreateVersionAsync(Guid documentId, Stream file, string changeNotes);
    Task<IEnumerable<Document>> GetDocumentsForFeatureAsync(Guid featureId);
    Task<IEnumerable<Document>> GetDocumentsForExtentAsync(Geometry extent);
    Task<Stream> GetDocumentContentAsync(Guid documentId, int? version = null);
}

public class DocumentMetadata
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string DocumentType { get; set; } // 'drawing', 'spec', 'photo', 'contract'
    public string FileFormat { get; set; } // 'pdf', 'dwg', 'dxf', 'docx'
    public long FileSizeBytes { get; set; }

    // Spatial linking
    public Geometry? SpatialExtent { get; set; } // Drawing sheet extent
    public List<Guid> RelatedFeatureIds { get; set; }

    // Version control
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string ChangeNotes { get; set; }

    // Classification
    public string DisciplineCode { get; set; } // 'A' (arch), 'S' (struct), 'M' (mech), etc.
    public string DrawingNumber { get; set; }
    public string SheetNumber { get; set; }

    // Custom metadata
    public Dictionary<string, object> CustomProperties { get; set; }
}
```

**Storage Schema:**
```sql
CREATE TABLE documents (
    id UUID PRIMARY KEY,
    file_name VARCHAR(500),
    document_type VARCHAR(50),
    file_format VARCHAR(20),
    file_size_bytes BIGINT,

    -- Version control
    version INTEGER NOT NULL DEFAULT 1,
    is_latest BOOLEAN DEFAULT TRUE,
    parent_version_id UUID REFERENCES documents(id),

    -- Spatial linking
    spatial_extent GEOMETRY(POLYGON, 4326),

    -- Classification
    discipline_code VARCHAR(10),
    drawing_number VARCHAR(100),
    sheet_number VARCHAR(50),

    -- Metadata
    custom_properties JSONB,

    -- Audit
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(255),
    change_notes TEXT,

    -- Storage
    storage_provider VARCHAR(50), -- 's3', 'azure_blob', 'filesystem'
    storage_path TEXT,
    checksum VARCHAR(64)
);

-- Link documents to features
CREATE TABLE document_feature_links (
    document_id UUID REFERENCES documents(id),
    feature_id UUID REFERENCES features(id),
    link_type VARCHAR(50), -- 'referenced_in', 'depicts', 'specifies'
    PRIMARY KEY (document_id, feature_id)
);

-- Spatial index
CREATE INDEX idx_documents_extent ON documents USING GIST (spatial_extent);

-- Version queries
CREATE INDEX idx_documents_version ON documents (parent_version_id, version);
```

**API Endpoints:**
```
POST /api/documents/upload
  - Upload document with metadata

GET /api/documents/{id}/versions
  - Get all versions

GET /api/documents/{id}/content?version=3
  - Download specific version

POST /api/documents/{id}/link/{featureId}
  - Link document to feature

GET /api/features/{id}/documents
  - Get all documents for feature

GET /api/documents/search?extent=POLYGON(...)
  - Spatial search for drawings
```

**Benefits:**
- Version control for all documents
- Spatial queries (e.g., "drawings covering this area")
- Feature-document linking
- Discipline-based organization
- Audit trail

**Implementation Effort:** 3-4 weeks

---

#### 2.3 Relationship Modeling Engine

**Problem:** Need rich, typed relationships beyond simple foreign keys.

**Current Limitation:** Basic foreign key constraints only (unless using Azure Digital Twins).

**Proposed Solution:** Extend Apache AGE integration with relationship type system and validation.

**Technical Details:**
```csharp
// Relationship type definitions
public class RelationshipType
{
    public string Name { get; set; } // 'CONTAINS', 'FEEDS', 'SUPPORTS', etc.
    public string SourceNodeType { get; set; }
    public string TargetNodeType { get; set; }
    public RelationshipCardinality Cardinality { get; set; }
    public Dictionary<string, PropertySchema> Properties { get; set; }
    public List<ValidationRule> ValidationRules { get; set; }
}

public enum RelationshipCardinality
{
    OneToOne,
    OneToMany,
    ManyToMany
}

// Example relationship types for AEC
var relationshipTypes = new[]
{
    new RelationshipType
    {
        Name = "CONTAINS",
        SourceNodeType = "Building",
        TargetNodeType = "Floor",
        Cardinality = RelationshipCardinality.OneToMany,
        Properties = new()
        {
            ["sequence_order"] = new PropertySchema { Type = "integer" }
        }
    },
    new RelationshipType
    {
        Name = "SUPPORTS",
        SourceNodeType = "Column",
        TargetNodeType = "Beam",
        Cardinality = RelationshipCardinality.OneToMany,
        Properties = new()
        {
            ["load_transfer_type"] = new PropertySchema { Type = "string" },
            ["max_load_kn"] = new PropertySchema { Type = "double" }
        }
    },
    new RelationshipType
    {
        Name = "FEEDS",
        SourceNodeType = "WaterMain",
        TargetNodeType = "ServiceLine",
        Cardinality = RelationshipCardinality.OneToMany,
        Properties = new()
        {
            ["flow_direction"] = new PropertySchema { Type = "string" },
            ["pressure_psi"] = new PropertySchema { Type = "double" }
        }
    }
};
```

**Validation Engine:**
```csharp
public interface IRelationshipValidator
{
    Task<ValidationResult> ValidateRelationshipAsync(
        string relationshipType,
        Guid sourceNodeId,
        Guid targetNodeId,
        Dictionary<string, object> properties);
}

// Example validation rules
public class ValidationRule
{
    public string RuleType { get; set; } // 'cardinality', 'property_range', 'circular_dependency'
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
}
```

**Graph Query API:**
```
POST /api/graph/query
  - Execute Cypher queries

POST /api/graph/relationships
  - Create typed relationship with validation

GET /api/graph/nodes/{id}/relationships?type=CONTAINS&direction=outgoing
  - Get typed relationships

POST /api/graph/traverse
  - Graph traversal with filtering
  {
    "startNodeId": "building-1",
    "relationshipTypes": ["CONTAINS", "HAS"],
    "maxDepth": 5,
    "filters": { "nodeType": "Equipment" }
  }
```

**Benefits:**
- Type-safe relationships
- Cardinality enforcement
- Circular dependency detection
- Rich property support on edges
- Industry-standard patterns

**Implementation Effort:** 4-5 weeks

---

### Priority 3: Integration (Nice to Have)

#### 3.1 Cost Data Integration (5D BIM)

**Problem:** Construction management requires linking cost data to spatial elements.

**Proposed Solution:** Cost schema extension with aggregation capabilities.

**Technical Details:**
```sql
-- Cost data linked to features
CREATE TABLE feature_costs (
    id UUID PRIMARY KEY,
    feature_id UUID REFERENCES features(id),
    cost_type VARCHAR(50), -- 'material', 'labor', 'equipment', 'overhead'
    unit_cost DECIMAL(15,2),
    quantity DECIMAL(15,2),
    unit_of_measure VARCHAR(50),
    total_cost DECIMAL(15,2),
    currency_code VARCHAR(3) DEFAULT 'USD',

    -- Temporal
    valid_from TIMESTAMPTZ,
    valid_to TIMESTAMPTZ,

    -- Classification
    cost_code VARCHAR(100), -- CSI MasterFormat code
    work_breakdown_structure VARCHAR(255),

    -- Metadata
    cost_source VARCHAR(100),
    confidence_level VARCHAR(20), -- 'estimate', 'quote', 'actual'
    notes TEXT,

    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Aggregate queries
CREATE MATERIALIZED VIEW project_cost_summary AS
SELECT
    l.layer_id,
    f.properties->>'construction_phase' as phase,
    fc.cost_type,
    SUM(fc.total_cost) as total_cost,
    COUNT(DISTINCT f.id) as feature_count
FROM features f
JOIN feature_costs fc ON f.id = fc.feature_id
JOIN layers l ON f.layer_id = l.id
GROUP BY l.layer_id, phase, fc.cost_type;
```

**API Endpoints:**
```
POST /api/features/{id}/costs
  - Add cost data to feature

GET /api/layers/{layerId}/costs/summary
  - Aggregate cost by phase, type, etc.

GET /api/costs/spatial?extent=POLYGON(...)
  - Cost summary for spatial extent
```

**Benefits:**
- Cost estimation by area
- Budget tracking by construction phase
- Cost-loaded schedules
- Value engineering analysis

**Implementation Effort:** 2-3 weeks

---

## Implementation Plan

### Phase 1: Foundation (Months 1-3)
**Goal:** Core technical enablers for basic AEC workflows

| Task | Effort | Dependencies | Deliverable |
|------|--------|--------------|-------------|
| **1.1 Apache AGE Integration** | 3 weeks | PostgreSQL 15+ | Graph database operational |
| - Install AGE extension | 3 days | - | Extension enabled |
| - Create graph schema | 5 days | AGE | Graph tables, indexes |
| - Implement graph query API | 1 week | - | REST endpoints |
| - Unit tests | 3 days | - | 90%+ coverage |
| **1.2 Complex 3D Geometry** | 6 weeks | - | 3D file import/export |
| - Integrate OpenCascade | 1 week | - | C++ bindings |
| - Implement mesh support (OBJ, STL) | 2 weeks | OCCT | Import/export working |
| - Implement B-Rep solids | 2 weeks | OCCT | CSG operations |
| - Storage schema + API | 1 week | - | REST endpoints |
| **1.3 IFC Parsing** | 5 weeks | IfcOpenShell | IFC import working |
| - Integrate IfcOpenShell | 1 week | - | Library installed |
| - Implement IFC → Feature mapping | 2 weeks | - | Entity conversion |
| - Implement relationship extraction | 1 week | AGE | Graph creation |
| - Validation + testing | 1 week | - | Sample IFC files |

**Milestones:**
- ✅ Week 6: Graph database queries working
- ✅ Week 10: Import OBJ/STL files
- ✅ Week 14: Import IFC files with relationships

**Success Criteria:**
- Import sample IFC file (20+ MB)
- Create 1000+ graph nodes with relationships
- Render complex 3D mesh in client
- Sub-second graph traversal queries

---

### Phase 2: Advanced Features (Months 4-6)
**Goal:** Temporal versioning and document management

| Task | Effort | Dependencies | Deliverable |
|------|--------|--------------|-------------|
| **2.1 Temporal Versioning** | 4 weeks | - | 4D scheduling support |
| - Design temporal schema | 1 week | - | Schema design doc |
| - Implement versioning logic | 2 weeks | - | CRUD operations |
| - Time-travel API | 1 week | - | REST endpoints |
| **2.2 Document Management** | 3 weeks | - | Document linking |
| - Storage abstraction layer | 1 week | - | S3/Azure/Local |
| - Version control logic | 1 week | - | CRUD operations |
| - Spatial linking | 1 week | PostGIS | Spatial queries |
| **2.3 Relationship Engine** | 4 weeks | Phase 1 AGE | Typed relationships |
| - Define relationship types | 1 week | - | Type registry |
| - Validation engine | 2 weeks | - | Validation logic |
| - Enhanced query API | 1 week | - | REST endpoints |

**Milestones:**
- ✅ Week 18: Feature versioning working
- ✅ Week 21: Document upload/versioning
- ✅ Week 25: Relationship validation

**Success Criteria:**
- Store 10+ versions of same feature
- Link documents to spatial features
- Validate relationship cardinality
- Query feature history over time

---

### Phase 3: Integration & Polish (Months 7-9)
**Goal:** Cost integration, performance optimization, documentation

| Task | Effort | Dependencies | Deliverable |
|------|--------|--------------|-------------|
| **3.1 Cost Data Integration** | 2 weeks | - | 5D BIM support |
| - Cost schema design | 3 days | - | Schema |
| - Aggregation queries | 1 week | - | Materialized views |
| - API implementation | 4 days | - | REST endpoints |
| **3.2 Performance Optimization** | 3 weeks | All above | Production-ready |
| - Graph query optimization | 1 week | AGE | Indexed traversals |
| - 3D geometry caching | 1 week | - | Redis/in-memory |
| - Batch operations | 1 week | - | Bulk imports |
| **3.3 Documentation & Examples** | 2 weeks | All above | User-facing docs |
| - API documentation | 1 week | - | OpenAPI specs |
| - Tutorial: Import IFC file | 3 days | - | Step-by-step guide |
| - Tutorial: Graph queries | 2 days | - | Example queries |
| **3.4 Client Integration** | 3 weeks | - | 3D rendering |
| - Update MapSDK for complex 3D | 2 weeks | Phase 1.2 | Mesh rendering |
| - Graph visualization component | 1 week | Phase 1.1 | Relationship viewer |

**Milestones:**
- ✅ Week 29: Cost aggregation queries
- ✅ Week 32: Performance benchmarks passed
- ✅ Week 36: Documentation complete

**Success Criteria:**
- Import 100 MB IFC file in <2 minutes
- Graph queries <100ms for 10K nodes
- Complete API documentation
- 2+ end-to-end tutorials

---

## Resource Requirements

### Team Composition

**Phase 1 (3 months):**
- 1 Senior Backend Engineer (C#, PostgreSQL, graph databases)
- 1 3D Graphics Engineer (OpenCascade, geometry processing)
- 0.5 QA Engineer

**Phase 2 (3 months):**
- 2 Backend Engineers
- 0.5 Frontend Engineer (graph visualization)
- 0.5 QA Engineer

**Phase 3 (3 months):**
- 1 Backend Engineer
- 1 Frontend Engineer
- 0.5 Technical Writer
- 0.5 QA Engineer

### Technology Stack

**Core Dependencies:**
```xml
<!-- Apache AGE for graph database -->
<PackageReference Include="Apache.Age" Version="1.3.0" />

<!-- OpenCascade for 3D geometry -->
<!-- Native C++ library with .NET bindings -->
<PackageReference Include="OpenCascade.Core" Version="7.7.0" />

<!-- IFC parsing -->
<PackageReference Include="IfcOpenShell.NET" Version="0.7.0" />

<!-- 3D file formats -->
<PackageReference Include="AssimpNet" Version="5.2.0" /> <!-- OBJ, STL, glTF -->

<!-- Document storage -->
<PackageReference Include="Minio" Version="6.0.0" /> <!-- S3-compatible -->
```

**Infrastructure:**
- PostgreSQL 15+ (for Apache AGE)
- Redis (for 3D geometry caching)
- S3-compatible storage (MinIO or AWS S3) for documents
- 16GB+ RAM per server (3D processing memory intensive)

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **OpenCascade integration complexity** | High | High | Proof-of-concept in Week 1, fallback to simpler mesh-only support |
| **IFC file variety/complexity** | High | Medium | Start with IFC2x3, add IFC4 later. Test with diverse files |
| **Graph query performance** | Medium | High | Extensive indexing, query optimization, consider read replicas |
| **3D file size/memory usage** | High | Medium | Implement LOD, streaming, server-side simplification |
| **Apache AGE maturity** | Medium | Medium | Contribute upstream fixes, maintain fork if needed |
| **Learning curve for graph queries** | Medium | Low | Comprehensive documentation, visual query builder |

---

## Success Metrics

### Technical KPIs

| Metric | Target | Measurement |
|--------|--------|-------------|
| **IFC Import Time** | <2 min for 100MB file | Benchmark suite |
| **Graph Query Latency** | <100ms (10K nodes) | P95 latency |
| **3D Mesh Rendering** | 60 FPS (1M triangles) | Client FPS counter |
| **Storage Efficiency** | <10% overhead vs. file size | Disk usage |
| **API Response Time** | <200ms (simple queries) | APM tools |

### Business KPIs (Year 1)

- **Pilot Projects:** 3-5 AEC customers
- **IFC Files Imported:** 100+
- **Graph Nodes Created:** 1M+
- **Document Versions Stored:** 10K+
- **Cost Savings vs. BIM360:** >70%

---

## Alternative Approaches Considered

### 1. Cloud-Only (Azure Digital Twins)
**Pros:** Fully managed, Microsoft ecosystem
**Cons:** Vendor lock-in, cloud-only, cost scaling
**Decision:** Rejected - need on-premises support

### 2. Neo4j for Graph Database
**Pros:** Most mature graph database, excellent tooling
**Cons:** Separate database, licensing costs, data duplication
**Decision:** Rejected - Apache AGE keeps everything in PostgreSQL

### 3. Full BIM Authoring
**Pros:** Complete BIM solution
**Cons:** Massive scope, compete with Autodesk/Bentley
**Decision:** Rejected - focus on BIM consumption, not creation

---

## Next Steps

### Immediate Actions (Week 1)

1. **Technical Validation:**
   - [ ] Install Apache AGE on test PostgreSQL instance
   - [ ] Build OpenCascade proof-of-concept (import OBJ file)
   - [ ] Test IfcOpenShell with sample IFC files
   - [ ] Benchmark graph query performance

2. **Planning:**
   - [ ] Review and approve this proposal
   - [ ] Recruit Senior Backend Engineer
   - [ ] Recruit 3D Graphics Engineer
   - [ ] Set up development environment

3. **Stakeholder Alignment:**
   - [ ] Present to product team
   - [ ] Identify pilot AEC customers
   - [ ] Create market positioning document

### Decision Points

**Go/No-Go Decision (End of Week 2):**
- Apache AGE proof-of-concept successful?
- OpenCascade integration feasible?
- Team resources secured?

**Phase 1 Review (End of Month 3):**
- All core enablers delivered?
- Performance targets met?
- Proceed to Phase 2?

---

## Conclusion

These **7 technical enablers** transform Honua.Server from a geospatial platform into a **comprehensive AEC data foundation**. By focusing on core capabilities rather than vertical solutions, we maintain flexibility while enabling sophisticated workflows.

**Key Advantages:**
- ✅ **No vendor lock-in** - All open-source components
- ✅ **On-premises support** - No cloud dependency
- ✅ **Cost-effective** - Zero licensing fees
- ✅ **Industry standards** - IFC, STEP, Cypher, DTDL
- ✅ **Unified platform** - Single database for spatial + graph + temporal

**Investment:** 6-9 months, 2-3 engineers, ~$500K total
**Return:** Opens $10B+ AEC software market with 70%+ cost advantage

---

**Prepared by:** Claude
**Review Required:** Engineering Lead, Product Manager
**Target Approval Date:** November 15, 2025
