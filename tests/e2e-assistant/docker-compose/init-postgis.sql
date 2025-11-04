-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- Create sample table
CREATE TABLE IF NOT EXISTS roads_primary (
    road_id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    road_class VARCHAR(50),
    observed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    geom GEOMETRY(Point, 4326)
);

-- Create spatial index
CREATE INDEX IF NOT EXISTS idx_roads_primary_geom ON roads_primary USING GIST(geom);

-- Insert sample data
INSERT INTO roads_primary (name, road_class, geom) VALUES
    ('Main Street', 'highway', ST_SetSRID(ST_MakePoint(-122.45, 45.55), 4326)),
    ('Oak Avenue', 'local', ST_SetSRID(ST_MakePoint(-122.46, 45.56), 4326)),
    ('River Road', 'highway', ST_SetSRID(ST_MakePoint(-122.47, 45.57), 4326));
