-- Database schema for Complex 3D Geometry Support
-- Part of Phase 1.2: AEC Technical Enablers
-- PostgreSQL with PostGIS

-- Table for storing complex 3D geometries
CREATE TABLE IF NOT EXISTS complex_geometries (
    -- Identity
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    feature_id UUID REFERENCES features(id) ON DELETE CASCADE,

    -- Geometry type and data
    geometry_type VARCHAR(50) NOT NULL,
    geometry_data BYTEA NOT NULL,  -- Binary serialized mesh/solid data

    -- Spatial indexing (PostGIS)
    bounding_box GEOMETRY(POLYHEDRONZ, 4979),  -- 3D bounding box in WGS84

    -- Statistics
    vertex_count INTEGER NOT NULL,
    face_count INTEGER NOT NULL,

    -- Metadata
    metadata JSONB DEFAULT '{}'::jsonb,
    source_format VARCHAR(20),  -- 'obj', 'stl', 'gltf', 'step', etc.

    -- Storage
    storage_path TEXT,  -- Path in blob storage (S3, Azure Blob, etc.)
    checksum VARCHAR(64) NOT NULL,  -- SHA256 hash for integrity
    size_bytes BIGINT NOT NULL,

    -- Audit
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(255),

    -- Constraints
    CONSTRAINT valid_geometry_type CHECK (
        geometry_type IN (
            'Point3D',
            'LineString3D',
            'Polygon3D',
            'TriangleMesh',
            'PointCloud',
            'BRepSolid',
            'ParametricSurface',
            'CsgSolid'
        )
    ),
    CONSTRAINT positive_counts CHECK (
        vertex_count >= 0 AND face_count >= 0
    ),
    CONSTRAINT positive_size CHECK (
        size_bytes > 0
    )
);

-- Spatial index on bounding box for efficient spatial queries
CREATE INDEX idx_complex_geom_bbox
ON complex_geometries
USING GIST (bounding_box);

-- Index on feature_id for quick lookup
CREATE INDEX idx_complex_geom_feature
ON complex_geometries(feature_id)
WHERE feature_id IS NOT NULL;

-- Index on geometry_type for filtering
CREATE INDEX idx_complex_geom_type
ON complex_geometries(geometry_type);

-- Index on created_at for temporal queries
CREATE INDEX idx_complex_geom_created
ON complex_geometries(created_at DESC);

-- GIN index on metadata for JSONB queries
CREATE INDEX idx_complex_geom_metadata
ON complex_geometries
USING GIN (metadata);

-- Trigger to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_complex_geometry_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_complex_geometry_updated
BEFORE UPDATE ON complex_geometries
FOR EACH ROW
EXECUTE FUNCTION update_complex_geometry_timestamp();

-- Example queries

-- 1. Find geometries intersecting a bounding box
-- SELECT * FROM complex_geometries
-- WHERE ST_Intersects(
--     bounding_box,
--     ST_MakeEnvelope(-10, -10, 10, 10, 4979)
-- );

-- 2. Get all geometries for a feature
-- SELECT * FROM complex_geometries
-- WHERE feature_id = '123e4567-e89b-12d3-a456-426614174000';

-- 3. Find geometries by metadata tag
-- SELECT * FROM complex_geometries
-- WHERE metadata @> '{"tags": ["building"]}';

-- 4. Get geometry statistics
-- SELECT
--     geometry_type,
--     COUNT(*) as count,
--     AVG(vertex_count) as avg_vertices,
--     AVG(face_count) as avg_faces,
--     SUM(size_bytes) / (1024*1024) as total_mb
-- FROM complex_geometries
-- GROUP BY geometry_type;

-- 5. Find large geometries (>10MB)
-- SELECT id, source_format, size_bytes / (1024*1024) as size_mb
-- FROM complex_geometries
-- WHERE size_bytes > 10 * 1024 * 1024
-- ORDER BY size_bytes DESC;
