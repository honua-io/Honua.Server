# 3D Geometry Support - Proof of Concept

This directory contains proof-of-concept files for Phase 1.2 of the AEC Technical Enablers roadmap: **Complex 3D Geometry Support**.

## Overview

The implementation uses **AssimpNet** to support importing and exporting 3D mesh files in various formats:
- **OBJ** - Wavefront Object files
- **STL** - Stereolithography files (ASCII and binary)
- **glTF/GLB** - GL Transmission Format
- **FBX** - Autodesk Filmbox
- **PLY** - Polygon File Format
- **Collada (DAE)** - Collaborative Design Activity

## Files

- `cube.obj` - Simple cube mesh for testing (8 vertices, 12 triangular faces)
- `poc-demo.sh` - Shell script demonstrating API usage with curl

## API Endpoints

### Upload 3D Geometry
```bash
POST /api/geometry/3d/upload
Content-Type: multipart/form-data

# Example
curl -X POST \
  -F "file=@cube.obj" \
  -F "featureId=123e4567-e89b-12d3-a456-426614174000" \
  http://localhost:5000/api/geometry/3d/upload
```

Response:
```json
{
  "success": true,
  "geometryId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "type": "TriangleMesh",
  "vertexCount": 8,
  "faceCount": 12,
  "boundingBox": {
    "minX": -1.0,
    "minY": -1.0,
    "minZ": -1.0,
    "maxX": 1.0,
    "maxY": 1.0,
    "maxZ": 1.0
  },
  "warnings": []
}
```

### Get Geometry Metadata
```bash
GET /api/geometry/3d/{id}?includeMesh=false

# Example
curl http://localhost:5000/api/geometry/3d/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

### Export Geometry
```bash
GET /api/geometry/3d/{id}/export?format=stl&binary=true

# Example - Export as STL
curl -o output.stl \
  "http://localhost:5000/api/geometry/3d/a1b2c3d4-e5f6-7890-abcd-ef1234567890/export?format=stl"

# Example - Export as glTF
curl -o output.gltf \
  "http://localhost:5000/api/geometry/3d/a1b2c3d4-e5f6-7890-abcd-ef1234567890/export?format=gltf"
```

### Search by Bounding Box
```bash
GET /api/geometry/3d/search/bbox?minX=-10&minY=-10&minZ=-10&maxX=10&maxY=10&maxZ=10

# Example
curl "http://localhost:5000/api/geometry/3d/search/bbox?minX=-10&minY=-10&minZ=-10&maxX=10&maxY=10&maxZ=10"
```

### Delete Geometry
```bash
DELETE /api/geometry/3d/{id}

# Example
curl -X DELETE http://localhost:5000/api/geometry/3d/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

## Architecture

### Models
- `GeometryType3D` - Enum defining supported geometry types
- `TriangleMesh` - Core mesh data structure (vertices, indices, normals, etc.)
- `BoundingBox3D` - 3D axis-aligned bounding box
- `ComplexGeometry3D` - Main geometry entity with metadata

### Services
- `IGeometry3DService` - Service interface for 3D operations
- `Geometry3DService` - Implementation using AssimpNet

### Storage Strategy
Current implementation uses in-memory storage for proof-of-concept.

Production implementation should use:
```sql
CREATE TABLE complex_geometries (
    id UUID PRIMARY KEY,
    feature_id UUID REFERENCES features(id),
    geometry_type VARCHAR(50),
    geometry_data BYTEA,         -- Binary mesh data
    bounding_box GEOMETRY(POLYHEDRONZ, 4979),
    vertex_count INTEGER,
    face_count INTEGER,
    metadata JSONB,
    source_format VARCHAR(20),
    checksum VARCHAR(64),
    size_bytes BIGINT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Spatial index on bounding box
CREATE INDEX idx_complex_geom_bbox ON complex_geometries USING GIST (bounding_box);
```

## Next Steps

### Phase 1: Current (Mesh Support) ✅
- [x] AssimpNet integration
- [x] Import/export OBJ, STL, glTF
- [x] Basic API endpoints
- [x] Bounding box queries

### Phase 2: Advanced Features (Future)
- [ ] OpenCascade integration for B-Rep solids
- [ ] CSG operations (union, intersection, difference)
- [ ] NURBS curve/surface support
- [ ] STEP/IGES file import
- [ ] Database persistence with PostGIS
- [ ] Blob storage integration (S3, Azure Blob)
- [ ] Level of Detail (LOD) generation
- [ ] Mesh simplification/optimization

## Performance Considerations

Current limitations (proof-of-concept):
- In-memory storage only
- No chunking for large files
- No streaming support
- Max file size: 100 MB

Production improvements needed:
- Stream processing for large files
- Progressive mesh loading
- Spatial indexing with PostGIS
- CDN integration for geometry delivery
- Compression (gzip, brotli)
- Caching with Redis

## Testing

To test the implementation:

1. Start Honua.Server:
```bash
cd /home/user/Honua.Server
dotnet run --project src/Honua.Server.Host
```

2. Upload the test cube:
```bash
curl -X POST \
  -F "file=@examples/3d-geometry-poc/cube.obj" \
  http://localhost:5000/api/geometry/3d/upload
```

3. Note the `geometryId` from the response

4. Export to different formats:
```bash
# STL binary
curl -o cube.stl \
  "http://localhost:5000/api/geometry/3d/{geometryId}/export?format=stl&binary=true"

# glTF
curl -o cube.gltf \
  "http://localhost:5000/api/geometry/3d/{geometryId}/export?format=gltf"
```

## References

- [AssimpNet Documentation](https://bitbucket.org/Starnick/assimpnet)
- [Assimp Supported Formats](https://github.com/assimp/assimp#supported-file-formats)
- [AEC Technical Enablers Proposal](../../AEC_TECHNICAL_ENABLERS_PROPOSAL.md)
