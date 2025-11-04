-- Initialize Honua MySQL database
USE honua;

-- Create a sample spatial table for testing
CREATE TABLE IF NOT EXISTS test_spatial (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100),
    geom POINT SRID 4326,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    SPATIAL INDEX(geom)
);

-- Insert sample data
DELETE FROM test_spatial;
INSERT INTO test_spatial (name, geom) VALUES
    ('Test Point 1', ST_GeomFromText('POINT(-74.006 40.7128)', 4326)),
    ('Test Point 2', ST_GeomFromText('POINT(-73.935 40.7305)', 4326));