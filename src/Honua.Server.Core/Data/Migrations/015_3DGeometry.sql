-- ============================================================================
-- Honua GIS Server - 3D Geometry Support
-- ============================================================================
-- Version: 015
-- Description: Add 3D geometry support for underground utilities, elevation,
--              and AR applications
-- Dependencies: PostGIS 3.0+
-- Related: HONUA_COMPLETE_SYSTEM_DESIGN.md - Section 2.1
-- ============================================================================
-- PURPOSE:
-- Enable AR visualization, underground utility mapping, and elevation data
-- by adding optional 3D geometry columns alongside existing 2D geometry.
-- 2D geometry remains primary for backward compatibility.
-- ============================================================================

-- ============================================================================
-- Add 3D Geometry Columns to Features Table
-- ============================================================================

-- Add 3D geometry column (PointZ, LineStringZ, PolygonZ, etc.)
ALTER TABLE features
ADD COLUMN IF NOT EXISTS geometry_3d geometry(GeometryZ, 4326);

-- Add elevation column (meters above sea level)
ALTER TABLE features
ADD COLUMN IF NOT EXISTS elevation_m DECIMAL(10,2);

-- Add geometry type for 3D
ALTER TABLE features
ADD COLUMN IF NOT EXISTS geometry_type_3d VARCHAR(50);

COMMENT ON COLUMN features.geometry_3d IS '3D geometry with Z-coordinates (elevation/depth). Optional - 2D geometry remains primary.';
COMMENT ON COLUMN features.elevation_m IS 'Elevation in meters above sea level (positive) or depth below ground (negative)';
COMMENT ON COLUMN features.geometry_type_3d IS '3D geometry type: PointZ, LineStringZ, PolygonZ, MultiPointZ, etc.';

-- ============================================================================
-- Add Underground Utility Specific Columns
-- ============================================================================

-- Utility type classification
ALTER TABLE features
ADD COLUMN IF NOT EXISTS utility_type VARCHAR(50);

-- Depth in meters (negative for underground)
ALTER TABLE features
ADD COLUMN IF NOT EXISTS depth_meters DECIMAL(10,2);

-- Confidence level of depth measurement
ALTER TABLE features
ADD COLUMN IF NOT EXISTS burial_depth_confidence VARCHAR(20);

COMMENT ON COLUMN features.utility_type IS 'Type of utility: gas, water, electric, telecom, sewer, storm_drain, fiber_optic, etc.';
COMMENT ON COLUMN features.depth_meters IS 'Depth in meters. Negative for underground (e.g., -2.5 means 2.5m below ground)';
COMMENT ON COLUMN features.burial_depth_confidence IS 'Confidence level: high, medium, low, unknown';

-- Add check constraint for burial_depth_confidence
ALTER TABLE features
ADD CONSTRAINT chk_burial_depth_confidence
CHECK (burial_depth_confidence IS NULL OR burial_depth_confidence IN ('high', 'medium', 'low', 'unknown'));

-- Add check constraint for utility_type
ALTER TABLE features
ADD CONSTRAINT chk_utility_type
CHECK (utility_type IS NULL OR utility_type IN (
    'gas', 'water', 'electric', 'telecom', 'sewer', 'storm_drain',
    'fiber_optic', 'cable_tv', 'steam', 'chilled_water', 'other'
));

-- ============================================================================
-- 3D Spatial Indexes
-- ============================================================================

-- 3D geometry spatial index
CREATE INDEX IF NOT EXISTS idx_features_geometry_3d
ON features USING GIST(geometry_3d)
WHERE geometry_3d IS NOT NULL;

-- Utility type and depth index (for underground utility queries)
CREATE INDEX IF NOT EXISTS idx_features_utility_type_depth
ON features (utility_type, depth_meters)
WHERE utility_type IS NOT NULL;

-- Elevation index (for terrain queries)
CREATE INDEX IF NOT EXISTS idx_features_elevation
ON features (elevation_m)
WHERE elevation_m IS NOT NULL;

-- Composite index for 3D utility queries (bbox + utility type + depth)
CREATE INDEX IF NOT EXISTS idx_features_utility_spatial
ON features USING GIST(geometry_3d)
WHERE utility_type IS NOT NULL AND depth_meters IS NOT NULL;

-- ============================================================================
-- Functions for 3D Geometry Operations
-- ============================================================================

-- Function to extract Z coordinate from 3D geometry
CREATE OR REPLACE FUNCTION honua_get_z_coordinate(geom geometry)
RETURNS DOUBLE PRECISION AS $$
BEGIN
    IF ST_GeometryType(geom) = 'ST_Point' AND ST_CoordDim(geom) = 3 THEN
        RETURN ST_Z(geom);
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE;

COMMENT ON FUNCTION honua_get_z_coordinate IS 'Extract Z coordinate from 3D point geometry';

-- Function to create 3D point from 2D point + elevation
CREATE OR REPLACE FUNCTION honua_add_z_to_geometry(
    geom_2d geometry,
    z_value DOUBLE PRECISION
)
RETURNS geometry AS $$
BEGIN
    IF ST_GeometryType(geom_2d) = 'ST_Point' THEN
        RETURN ST_MakePoint(ST_X(geom_2d), ST_Y(geom_2d), z_value);
    END IF;
    -- For non-point geometries, return as-is (future enhancement)
    RETURN geom_2d;
END;
$$ LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE;

COMMENT ON FUNCTION honua_add_z_to_geometry IS 'Add Z coordinate to 2D geometry to create 3D geometry';

-- Function to validate 3D geometry
CREATE OR REPLACE FUNCTION honua_validate_3d_geometry()
RETURNS TRIGGER AS $$
BEGIN
    -- If geometry_3d is set, ensure it has 3 dimensions
    IF NEW.geometry_3d IS NOT NULL THEN
        IF ST_CoordDim(NEW.geometry_3d) < 3 THEN
            RAISE EXCEPTION 'geometry_3d must have Z coordinate (3 dimensions)';
        END IF;

        -- Auto-populate geometry_type_3d
        NEW.geometry_type_3d := ST_GeometryType(NEW.geometry_3d);

        -- Extract Z value for points and populate elevation_m if not set
        IF NEW.elevation_m IS NULL AND ST_GeometryType(NEW.geometry_3d) = 'ST_Point' THEN
            NEW.elevation_m := ST_Z(NEW.geometry_3d);
        END IF;
    END IF;

    -- If depth_meters is set, ensure it's negative for underground utilities
    IF NEW.depth_meters IS NOT NULL AND NEW.depth_meters > 0 AND NEW.utility_type IS NOT NULL THEN
        -- Convert positive depth to negative (underground convention)
        NEW.depth_meters := -ABS(NEW.depth_meters);
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Add trigger for 3D geometry validation
DROP TRIGGER IF EXISTS trg_validate_3d_geometry ON features;
CREATE TRIGGER trg_validate_3d_geometry
    BEFORE INSERT OR UPDATE ON features
    FOR EACH ROW
    EXECUTE FUNCTION honua_validate_3d_geometry();

-- ============================================================================
-- Update Existing Metadata View to Include 3D Info
-- ============================================================================

-- Add 3D geometry info to collection metadata
-- This will be used by OGC Features API to advertise 3D support

-- Note: This assumes there's a metadata table or view
-- Adjust table name if different in your schema

COMMENT ON TABLE features IS 'Geospatial features with optional 3D geometry support. geometry_3d provides Z-coordinates for elevation/depth, used for AR and underground utility visualization.';

-- ============================================================================
-- Sample Data for Testing (Optional - Remove in Production)
-- ============================================================================

-- Example: Underground gas pipe
-- INSERT INTO features (collection_id, geometry, geometry_3d, properties, utility_type, depth_meters, burial_depth_confidence)
-- VALUES (
--     'utilities',
--     ST_GeomFromText('LINESTRING(-122.4194 37.7749, -122.4180 37.7755)', 4326),
--     ST_GeomFromText('LINESTRING Z(-122.4194 37.7749 -2.5, -122.4180 37.7755 -2.5)', 4326),
--     '{"material": "steel", "diameter_inches": 6, "install_year": 1985}'::jsonb,
--     'gas',
--     -2.5,
--     'high'
-- );

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. 3D indexes are only created where geometry_3d IS NOT NULL (partial indexes)
-- 2. 2D geometry remains primary - no performance impact on existing queries
-- 3. 3D queries should use geometry_3d column explicitly
-- 4. PostGIS 3D functions (ST_3DDistance, ST_3DIntersects) work with Z coordinates
-- ============================================================================

-- ============================================================================
-- Migration Complete
-- ============================================================================

-- Version tracking
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_version') THEN
        CREATE TABLE schema_version (
            version INT PRIMARY KEY,
            applied_at TIMESTAMPTZ DEFAULT NOW(),
            description TEXT
        );
    END IF;

    INSERT INTO schema_version (version, description)
    VALUES (15, '3D geometry support for underground utilities and AR')
    ON CONFLICT (version) DO NOTHING;
END $$;
