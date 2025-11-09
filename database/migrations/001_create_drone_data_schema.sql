-- Drone Data Integration Schema Migration
-- This migration creates all tables and indexes for drone data storage
-- Requires PostGIS and pgPointcloud extensions

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_raster;
CREATE EXTENSION IF NOT EXISTS pointcloud;
CREATE EXTENSION IF NOT EXISTS pointcloud_postgis;

-- Create point cloud format schema for drone data
-- This defines the structure of point cloud data storage
INSERT INTO pointcloud_formats (pcid, srid, schema)
VALUES (
  1, 4326,
  '<?xml version="1.0" encoding="UTF-8"?>
  <pc:PointCloudSchema xmlns:pc="http://pointcloud.org/schemas/PC/1.1">
    <pc:dimension>
      <pc:position>1</pc:position>
      <pc:size>8</pc:size>
      <pc:name>X</pc:name>
      <pc:interpretation>double</pc:interpretation>
      <pc:description>X coordinate (Longitude)</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>2</pc:position>
      <pc:size>8</pc:size>
      <pc:name>Y</pc:name>
      <pc:interpretation>double</pc:interpretation>
      <pc:description>Y coordinate (Latitude)</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>3</pc:position>
      <pc:size>8</pc:size>
      <pc:name>Z</pc:name>
      <pc:interpretation>double</pc:interpretation>
      <pc:description>Z coordinate (Elevation)</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>4</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Intensity</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
      <pc:description>Return intensity</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>5</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Red</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
      <pc:description>Red color channel</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>6</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Green</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
      <pc:description>Green color channel</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>7</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Blue</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
      <pc:description>Blue color channel</pc:description>
    </pc:dimension>
    <pc:dimension>
      <pc:position>8</pc:position>
      <pc:size>1</pc:size>
      <pc:name>Classification</pc:name>
      <pc:interpretation>uint8_t</pc:interpretation>
      <pc:description>LAS classification code</pc:description>
    </pc:dimension>
  </pc:PointCloudSchema>'
) ON CONFLICT (pcid) DO NOTHING;

-- Create drone surveys table
-- Stores metadata about each drone survey/flight
CREATE TABLE IF NOT EXISTS drone_surveys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    survey_date TIMESTAMP NOT NULL,
    flight_altitude_m DOUBLE PRECISION,
    ground_resolution_cm DOUBLE PRECISION,
    coverage_area GEOMETRY(Polygon, 4326),
    area_sqm DOUBLE PRECISION,
    point_count BIGINT DEFAULT 0,
    orthophoto_url TEXT,
    dem_url TEXT,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    created_by VARCHAR(255),

    CONSTRAINT positive_altitude CHECK (flight_altitude_m IS NULL OR flight_altitude_m > 0),
    CONSTRAINT positive_resolution CHECK (ground_resolution_cm IS NULL OR ground_resolution_cm > 0),
    CONSTRAINT positive_area CHECK (area_sqm IS NULL OR area_sqm > 0)
);

-- Create spatial index on coverage area
CREATE INDEX IF NOT EXISTS idx_drone_surveys_coverage
    ON drone_surveys USING GIST(coverage_area);

-- Create index on survey date for temporal queries
CREATE INDEX IF NOT EXISTS idx_drone_surveys_date
    ON drone_surveys(survey_date DESC);

-- Create point cloud table with patches
-- Stores point cloud data in compressed patches
CREATE TABLE IF NOT EXISTS drone_point_clouds (
    id SERIAL PRIMARY KEY,
    survey_id UUID NOT NULL REFERENCES drone_surveys(id) ON DELETE CASCADE,
    tile_id VARCHAR(50),
    pa PCPATCH(1),  -- Point cloud patch using format 1
    lod_level INTEGER DEFAULT 0,
    point_count INTEGER,
    created_at TIMESTAMP DEFAULT NOW(),

    CONSTRAINT valid_lod CHECK (lod_level >= 0 AND lod_level <= 3),
    CONSTRAINT fk_survey FOREIGN KEY (survey_id)
        REFERENCES drone_surveys(id) ON DELETE CASCADE
);

-- Create spatial index on point cloud patches
CREATE INDEX IF NOT EXISTS idx_drone_pc_geom
    ON drone_point_clouds USING GIST(PC_EnvelopeGeometry(pa));

-- Create index on survey_id for fast lookups
CREATE INDEX IF NOT EXISTS idx_drone_pc_survey
    ON drone_point_clouds(survey_id);

-- Create index on LOD level for multi-resolution queries
CREATE INDEX IF NOT EXISTS idx_drone_pc_lod
    ON drone_point_clouds(survey_id, lod_level);

-- Create orthomosaics table
-- Stores references to orthophoto raster data
CREATE TABLE IF NOT EXISTS drone_orthomosaics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_id UUID NOT NULL REFERENCES drone_surveys(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    raster_path TEXT NOT NULL,
    storage_url TEXT,
    bounds GEOMETRY(Polygon, 4326),
    resolution_cm DOUBLE PRECISION,
    tile_matrix_set VARCHAR(50) DEFAULT 'WebMercatorQuad',
    format VARCHAR(20) DEFAULT 'COG',
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW(),

    CONSTRAINT positive_ortho_resolution CHECK (resolution_cm > 0),
    CONSTRAINT fk_ortho_survey FOREIGN KEY (survey_id)
        REFERENCES drone_surveys(id) ON DELETE CASCADE
);

-- Create spatial index on orthomosaic bounds
CREATE INDEX IF NOT EXISTS idx_drone_ortho_bounds
    ON drone_orthomosaics USING GIST(bounds);

-- Create index on survey_id
CREATE INDEX IF NOT EXISTS idx_drone_ortho_survey
    ON drone_orthomosaics(survey_id);

-- Create 3D models table
-- Stores references to 3D mesh models (OBJ, glTF, 3D Tiles)
CREATE TABLE IF NOT EXISTS drone_3d_models (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    survey_id UUID NOT NULL REFERENCES drone_surveys(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    model_type VARCHAR(50) NOT NULL, -- 'OBJ', 'GLTF', '3DTILES'
    model_path TEXT NOT NULL,
    storage_url TEXT,
    bounds GEOMETRY(Polygon, 4326),
    vertex_count BIGINT,
    texture_count INTEGER,
    file_size_bytes BIGINT,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW(),

    CONSTRAINT valid_model_type CHECK (model_type IN ('OBJ', 'GLTF', 'GLB', '3DTILES')),
    CONSTRAINT fk_model_survey FOREIGN KEY (survey_id)
        REFERENCES drone_surveys(id) ON DELETE CASCADE
);

-- Create spatial index on 3D model bounds
CREATE INDEX IF NOT EXISTS idx_drone_models_bounds
    ON drone_3d_models USING GIST(bounds);

-- Create index on survey_id
CREATE INDEX IF NOT EXISTS idx_drone_models_survey
    ON drone_3d_models(survey_id);

-- Create materialized view for LOD 1 (10% decimation)
CREATE MATERIALIZED VIEW IF NOT EXISTS drone_point_clouds_lod1 AS
SELECT
    id,
    survey_id,
    tile_id,
    PC_FilterBetween(pa, 'X', PC_PatchMin(pa, 'X'), PC_PatchMax(pa, 'X'), 0.1) AS pa,
    1 AS lod_level,
    PC_NumPoints(PC_FilterBetween(pa, 'X', PC_PatchMin(pa, 'X'), PC_PatchMax(pa, 'X'), 0.1)) AS point_count
FROM drone_point_clouds
WHERE lod_level = 0;

-- Create spatial index on LOD1 view
CREATE INDEX IF NOT EXISTS idx_drone_pc_lod1_geom
    ON drone_point_clouds_lod1 USING GIST(PC_EnvelopeGeometry(pa));

-- Create materialized view for LOD 2 (1% decimation)
CREATE MATERIALIZED VIEW IF NOT EXISTS drone_point_clouds_lod2 AS
SELECT
    id,
    survey_id,
    tile_id,
    PC_FilterBetween(pa, 'X', PC_PatchMin(pa, 'X'), PC_PatchMax(pa, 'X'), 0.01) AS pa,
    2 AS lod_level,
    PC_NumPoints(PC_FilterBetween(pa, 'X', PC_PatchMin(pa, 'X'), PC_PatchMax(pa, 'X'), 0.01)) AS point_count
FROM drone_point_clouds
WHERE lod_level = 0;

-- Create spatial index on LOD2 view
CREATE INDEX IF NOT EXISTS idx_drone_pc_lod2_geom
    ON drone_point_clouds_lod2 USING GIST(PC_EnvelopeGeometry(pa));

-- Create function to update survey statistics
CREATE OR REPLACE FUNCTION update_drone_survey_stats(p_survey_id UUID)
RETURNS VOID AS $$
BEGIN
    UPDATE drone_surveys
    SET
        point_count = (
            SELECT COALESCE(SUM(PC_NumPoints(pa)), 0)
            FROM drone_point_clouds
            WHERE survey_id = p_survey_id AND lod_level = 0
        ),
        area_sqm = ST_Area(coverage_area::geography),
        updated_at = NOW()
    WHERE id = p_survey_id;
END;
$$ LANGUAGE plpgsql;

-- Create function to refresh LOD materialized views
CREATE OR REPLACE FUNCTION refresh_drone_lod_views()
RETURNS VOID AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY drone_point_clouds_lod1;
    REFRESH MATERIALIZED VIEW CONCURRENTLY drone_point_clouds_lod2;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to update survey stats on point cloud insert
CREATE OR REPLACE FUNCTION trigger_update_survey_stats()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM update_drone_survey_stats(NEW.survey_id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_drone_pc_stats
    AFTER INSERT OR UPDATE OR DELETE ON drone_point_clouds
    FOR EACH ROW
    EXECUTE FUNCTION trigger_update_survey_stats();

-- Create helper function to query point clouds by bounding box
CREATE OR REPLACE FUNCTION get_drone_points_in_bbox(
    p_survey_id UUID,
    p_min_x DOUBLE PRECISION,
    p_min_y DOUBLE PRECISION,
    p_max_x DOUBLE PRECISION,
    p_max_y DOUBLE PRECISION,
    p_lod_level INTEGER DEFAULT 0,
    p_classification_filter INTEGER[] DEFAULT NULL
)
RETURNS TABLE(
    x DOUBLE PRECISION,
    y DOUBLE PRECISION,
    z DOUBLE PRECISION,
    red INTEGER,
    green INTEGER,
    blue INTEGER,
    classification INTEGER,
    intensity INTEGER
) AS $$
DECLARE
    v_table_name TEXT;
BEGIN
    -- Select appropriate table based on LOD
    v_table_name := CASE p_lod_level
        WHEN 1 THEN 'drone_point_clouds_lod1'
        WHEN 2 THEN 'drone_point_clouds_lod2'
        ELSE 'drone_point_clouds'
    END;

    -- Execute dynamic query
    RETURN QUERY EXECUTE format('
        SELECT
            PC_Get(pt, ''X'')::double precision AS x,
            PC_Get(pt, ''Y'')::double precision AS y,
            PC_Get(pt, ''Z'')::double precision AS z,
            PC_Get(pt, ''Red'')::int AS red,
            PC_Get(pt, ''Green'')::int AS green,
            PC_Get(pt, ''Blue'')::int AS blue,
            PC_Get(pt, ''Classification'')::int AS classification,
            PC_Get(pt, ''Intensity'')::int AS intensity
        FROM (
            SELECT PC_Explode(pa) AS pt
            FROM %I
            WHERE survey_id = $1
              AND PC_Intersects(
                    pa,
                    ST_MakeEnvelope($2, $3, $4, $5, 4326)
                  )
        ) AS exploded
        WHERE $6 IS NULL OR PC_Get(pt, ''Classification'')::int = ANY($6)
    ', v_table_name)
    USING p_survey_id, p_min_x, p_min_y, p_max_x, p_max_y, p_classification_filter;
END;
$$ LANGUAGE plpgsql;

-- Create comments for documentation
COMMENT ON TABLE drone_surveys IS 'Stores metadata about drone survey missions and flights';
COMMENT ON TABLE drone_point_clouds IS 'Stores point cloud data from LiDAR/photogrammetry in compressed patches';
COMMENT ON TABLE drone_orthomosaics IS 'References to orthophoto raster data (Cloud Optimized GeoTIFFs)';
COMMENT ON TABLE drone_3d_models IS 'References to 3D mesh models generated from drone data';

COMMENT ON COLUMN drone_point_clouds.pa IS 'Compressed point cloud patch (PCPATCH)';
COMMENT ON COLUMN drone_point_clouds.lod_level IS 'Level of Detail: 0=Full, 1=Coarse(10%), 2=Sparse(1%)';

-- Grant permissions (adjust as needed for your security model)
-- GRANT SELECT ON ALL TABLES IN SCHEMA public TO readonly_user;
-- GRANT ALL ON ALL TABLES IN SCHEMA public TO app_user;
