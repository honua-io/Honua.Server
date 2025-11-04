-- Initialize Honua PostgreSQL database
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- Create a sample spatial table for testing
CREATE TABLE IF NOT EXISTS test_spatial (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    geom GEOMETRY(POINT, 4326),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample data
INSERT INTO test_spatial (name, geom) VALUES
    ('Test Point 1', ST_GeomFromText('POINT(-74.006 40.7128)', 4326)),
    ('Test Point 2', ST_GeomFromText('POINT(-73.935 40.7305)', 4326));

-- Create spatial index
CREATE INDEX IF NOT EXISTS idx_test_spatial_geom ON test_spatial USING GIST (geom);

-- Grant permissions
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;