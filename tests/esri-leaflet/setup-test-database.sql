-- Setup test database for Esri Leaflet tests
-- Run this against your PostgreSQL database with PostGIS extension

-- Create database (if not exists)
-- CREATE DATABASE honua_test;

-- Connect to honua_test database and enable PostGIS
CREATE EXTENSION IF NOT EXISTS postgis;

-- Create parks table for testing
DROP TABLE IF EXISTS parks CASCADE;

CREATE TABLE parks (
    "OBJECTID" SERIAL PRIMARY KEY,
    name VARCHAR(255),
    type VARCHAR(100),
    status VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    geom GEOMETRY(Point, 4326)
);

-- Create spatial index
CREATE INDEX idx_parks_geom ON parks USING GIST (geom);

-- Insert sample test data (Portland, Oregon area)
INSERT INTO parks (name, type, status, geom) VALUES
    ('Forest Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.7694, 45.5589), 4326)),
    ('Washington Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.7158, 45.5095), 4326)),
    ('Mount Tabor Park', 'Nature', 'active', ST_SetSRID(ST_MakePoint(-122.5931, 45.5122), 4326)),
    ('Laurelhurst Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.6214, 45.5204), 4326)),
    ('Peninsula Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.6750, 45.5689), 4326)),
    ('Sellwood Park', 'Recreation', 'maintenance', ST_SetSRID(ST_MakePoint(-122.6514, 45.4644), 4326)),
    ('Pier Park', 'Sports', 'active', ST_SetSRID(ST_MakePoint(-122.7644, 45.5944), 4326)),
    ('Kelley Point Park', 'Nature', 'active', ST_SetSRID(ST_MakePoint(-122.7628, 45.6489), 4326)),
    ('Powell Butte', 'Nature', 'active', ST_SetSRID(ST_MakePoint(-122.5008, 45.5000), 4326)),
    ('Oaks Bottom', 'Nature', 'active', ST_SetSRID(ST_MakePoint(-122.6578, 45.4739), 4326)),
    ('Smith and Bybee Lakes', 'Nature', 'active', ST_SetSRID(ST_MakePoint(-122.7375, 45.6239), 4326)),
    ('Gabriel Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.7194, 45.4619), 4326)),
    ('Cathedral Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.7633, 45.5856), 4326)),
    ('Waterfront Park', 'Recreation', 'active', ST_SetSRID(ST_MakePoint(-122.6708, 45.5239), 4326)),
    ('Holladay Park', 'Recreation', 'planned', ST_SetSRID(ST_MakePoint(-122.6569, 45.5303), 4326));

-- Create basemap_features table for MapServer tests
DROP TABLE IF EXISTS basemap_features CASCADE;

CREATE TABLE basemap_features (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    feature_type VARCHAR(100),
    geom GEOMETRY(Polygon, 4326)
);

CREATE INDEX idx_basemap_geom ON basemap_features USING GIST (geom);

-- Insert sample basemap polygons
INSERT INTO basemap_features (name, feature_type, geom) VALUES
    ('Downtown', 'urban', ST_SetSRID(ST_MakePolygon(ST_GeomFromText('LINESTRING(-122.68 45.51, -122.67 45.51, -122.67 45.52, -122.68 45.52, -122.68 45.51)')), 4326)),
    ('Pearl District', 'urban', ST_SetSRID(ST_MakePolygon(ST_GeomFromText('LINESTRING(-122.69 45.52, -122.68 45.52, -122.68 45.53, -122.69 45.53, -122.69 45.52)')), 4326)),
    ('Eastside', 'urban', ST_SetSRID(ST_MakePolygon(ST_GeomFromText('LINESTRING(-122.66 45.51, -122.65 45.51, -122.65 45.52, -122.66 45.52, -122.66 45.51)')), 4326));

-- Grant permissions (adjust user as needed)
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO honua;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua;

-- Verify data
SELECT
    'parks' as table_name,
    COUNT(*) as row_count,
    ST_Extent(geom) as extent
FROM parks
UNION ALL
SELECT
    'basemap_features' as table_name,
    COUNT(*) as row_count,
    ST_Extent(geom) as extent
FROM basemap_features;

-- Show sample data
SELECT
    "OBJECTID",
    name,
    type,
    status,
    ST_AsText(geom) as geometry_wkt
FROM parks
LIMIT 5;
