# Quick Start Guide - 3D Geometry Support

## Prerequisites

- .NET 9.0 SDK
- PostgreSQL 15+ with PostGIS (for production)
- S3-compatible blob storage (optional, for production)

## Step 1: Restore Dependencies

```bash
cd /home/user/Honua.Server
dotnet restore
```

This will install AssimpNet and its native dependencies.

## Step 2: Run the Demo (Standalone)

```bash
cd examples/3d-geometry-poc
dotnet run Demo.cs
```

This demonstrates:
- Importing cube.obj
- Retrieving geometry metadata
- Exporting to different formats (OBJ, STL, PLY)
- Spatial search by bounding box
- Metadata updates
- Deletion

## Step 3: Test via API (Full Server)

### Start the server:
```bash
dotnet run --project src/Honua.Server.Host
```

### Upload a 3D file:
```bash
curl -X POST \
  -F "file=@examples/3d-geometry-poc/cube.obj" \
  http://localhost:5000/api/geometry/3d/upload
```

### Get the geometry:
```bash
# Replace {id} with the geometryId from upload response
curl http://localhost:5000/api/geometry/3d/{id}
```

### Export to STL:
```bash
curl -o output.stl \
  "http://localhost:5000/api/geometry/3d/{id}/export?format=stl&binary=true"
```

### Export to glTF:
```bash
curl -o output.gltf \
  "http://localhost:5000/api/geometry/3d/{id}/export?format=gltf"
```

## Step 4: Deploy Database Schema (Production)

```bash
psql -U postgres -d honua -f examples/3d-geometry-poc/schema.sql
```

This creates:
- `complex_geometries` table
- Spatial indexes
- Metadata indexes
- Triggers for timestamp updates

## Supported File Formats

### Import (40+ formats)
- **OBJ** - Wavefront Object
- **STL** - Stereolithography (ASCII and binary)
- **glTF/GLB** - GL Transmission Format
- **FBX** - Autodesk Filmbox
- **Collada (DAE)** - Collaborative Design
- **PLY** - Polygon File Format
- **3DS** - 3D Studio
- **DXF** - AutoCAD Drawing Exchange
- **And 30+ more...**

### Export (15+ formats)
- OBJ, STL, glTF/GLB, PLY, Collada (DAE)

## Example: Import Building Model

```bash
# Upload a building model
curl -X POST \
  -F "file=@building.obj" \
  -F "featureId=123e4567-e89b-12d3-a456-426614174000" \
  http://localhost:5000/api/geometry/3d/upload

# Response
{
  "success": true,
  "geometryId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "type": "TriangleMesh",
  "vertexCount": 15234,
  "faceCount": 8456,
  "boundingBox": {
    "minX": -50.0,
    "minY": -30.0,
    "minZ": 0.0,
    "maxX": 50.0,
    "maxY": 30.0,
    "maxZ": 45.0,
    "width": 100.0,
    "height": 60.0,
    "depth": 45.0
  },
  "warnings": []
}
```

## Troubleshooting

### Error: "No file provided"
Make sure you're using `-F "file=@path/to/file.obj"` with the `@` symbol.

### Error: "Unsupported file format"
Check that the file extension is one of the supported formats.

### Error: "File too large"
Current limit is 100 MB for POC. Increase the limit or implement streaming for production.

### Error: "Failed to import file"
Check that the file is valid. Try opening it in a 3D viewer like Blender or MeshLab.

## Performance Tips

1. **Use binary formats** - STL binary is faster than ASCII
2. **Enable compression** - Use gzip for API responses
3. **Cache exports** - Store converted formats in blob storage
4. **Use spatial indexes** - Filter by bounding box before loading full mesh
5. **Generate LOD** - Create multiple resolution levels for large meshes

## Next Steps

1. Read the [full README](README.md) for detailed API documentation
2. Review the [implementation summary](../../PHASE_1_2_IMPLEMENTATION_SUMMARY.md)
3. Check the [database schema](schema.sql) for production setup
4. See the [AEC proposal](../../AEC_TECHNICAL_ENABLERS_PROPOSAL.md) for roadmap

## Support

For issues or questions:
1. Check the implementation summary document
2. Review the API documentation
3. Consult the AssimpNet documentation
4. Open an issue in the repository
