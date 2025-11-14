-- PostGIS Initialization Script for Honua IO
-- This script sets up PostGIS extensions and required spatial functions

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
CREATE EXTENSION IF NOT EXISTS postgis_raster;
CREATE EXTENSION IF NOT EXISTS fuzzystrmatch;
CREATE EXTENSION IF NOT EXISTS postgis_tiger_geocoder;

-- Create spatial reference systems table if not exists
-- (PostGIS creates this automatically, but explicit for clarity)

-- Set up optimal PostGIS settings for geospatial workloads
ALTER DATABASE honua SET postgis.gdal_enabled_drivers TO 'ENABLE_ALL';
ALTER DATABASE honua SET postgis.enable_outdb_rasters TO 'true';

-- Create spatial indexes helper function
CREATE OR REPLACE FUNCTION create_spatial_index(table_name text, column_name text)
RETURNS void AS $$
BEGIN
    EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I USING GIST(%I)',
                   table_name || '_' || column_name || '_idx',
                   table_name,
                   column_name);
END;
$$ LANGUAGE plpgsql;

-- Grant necessary permissions
GRANT ALL ON SCHEMA public TO honua;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO honua;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO honua;

-- Enable required extensions for full-text search and advanced features
CREATE EXTENSION IF NOT EXISTS pg_trgm;  -- Trigram matching for fuzzy search
CREATE EXTENSION IF NOT EXISTS btree_gist;  -- B-tree and GiST index support
CREATE EXTENSION IF NOT EXISTS uuid-ossp;  -- UUID generation

-- Create metadata table for tracking PostGIS version and initialization
CREATE TABLE IF NOT EXISTS honua_spatial_metadata (
    id SERIAL PRIMARY KEY,
    postgis_version TEXT,
    initialized_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    initialized_by TEXT DEFAULT CURRENT_USER
);

-- Record initialization
INSERT INTO honua_spatial_metadata (postgis_version)
SELECT PostGIS_full_version()
ON CONFLICT DO NOTHING;

-- Display PostGIS information
SELECT PostGIS_full_version() as postgis_info;

-- Verify all extensions are installed
SELECT name, default_version, installed_version
FROM pg_available_extensions
WHERE name LIKE 'postgis%'
ORDER BY name;
