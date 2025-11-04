-- ============================================================================
-- Honua GIS Server - PostgreSQL Optimization Tests - Test Data Generator
-- ============================================================================
-- This script creates test data for integration testing of optimized
-- PostgreSQL functions. It creates various geometry types, spatial indexes,
-- and realistic test datasets.
-- ============================================================================

-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;

-- ============================================================================
-- Test Schema
-- ============================================================================

DROP SCHEMA IF EXISTS test_optimizations CASCADE;
CREATE SCHEMA test_optimizations;

SET search_path TO test_optimizations, public;

-- ============================================================================
-- Table 1: Cities (Point Features)
-- ============================================================================
-- Tests point-based operations, clustering, and spatial queries

CREATE TABLE test_cities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    country TEXT,
    population INTEGER,
    area_km2 NUMERIC,
    founded_year INTEGER,
    is_capital BOOLEAN DEFAULT false,
    elevation_m INTEGER,
    timezone TEXT,
    geom GEOMETRY(Point, 4326) NOT NULL,
    properties JSONB,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- Create spatial index
CREATE INDEX idx_test_cities_geom ON test_cities USING GIST(geom);
CREATE INDEX idx_test_cities_population ON test_cities(population);
CREATE INDEX idx_test_cities_country ON test_cities(country);

-- Insert realistic test data (10,000 cities worldwide)
INSERT INTO test_cities (name, country, population, area_km2, founded_year, is_capital, elevation_m, timezone, geom, properties)
SELECT
    'City_' || i,
    CASE (i % 10)
        WHEN 0 THEN 'USA'
        WHEN 1 THEN 'China'
        WHEN 2 THEN 'India'
        WHEN 3 THEN 'Brazil'
        WHEN 4 THEN 'Russia'
        WHEN 5 THEN 'Japan'
        WHEN 6 THEN 'Germany'
        WHEN 7 THEN 'UK'
        WHEN 8 THEN 'France'
        ELSE 'Australia'
    END,
    (random() * 10000000)::INTEGER, -- Population 0-10M
    (random() * 1000)::NUMERIC, -- Area 0-1000 km²
    1500 + (random() * 500)::INTEGER, -- Founded 1500-2000
    (random() < 0.01), -- 1% are capitals
    (random() * 3000)::INTEGER, -- Elevation 0-3000m
    'UTC' || CASE WHEN random() < 0.5 THEN '+' ELSE '-' END || (random() * 12)::INTEGER,
    ST_SetSRID(ST_MakePoint(
        (random() * 360) - 180, -- Longitude -180 to 180
        (random() * 180) - 90   -- Latitude -90 to 90
    ), 4326),
    jsonb_build_object(
        'type', CASE (i % 5)
            WHEN 0 THEN 'megacity'
            WHEN 1 THEN 'large_city'
            WHEN 2 THEN 'medium_city'
            WHEN 3 THEN 'small_city'
            ELSE 'town'
        END,
        'has_airport', random() < 0.3,
        'has_seaport', random() < 0.1,
        'test_data', true
    )
FROM generate_series(1, 10000) AS i;

-- ============================================================================
-- Table 2: Countries (Polygon Features)
-- ============================================================================
-- Tests polygon-based operations, aggregation, and area calculations

CREATE TABLE test_countries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    iso_code CHAR(2) NOT NULL,
    continent TEXT,
    population INTEGER,
    area_km2 NUMERIC,
    gdp_usd BIGINT,
    capital_city TEXT,
    geom GEOMETRY(Polygon, 4326) NOT NULL,
    properties JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_test_countries_geom ON test_countries USING GIST(geom);
CREATE INDEX idx_test_countries_iso ON test_countries(iso_code);

-- Insert test country polygons (100 synthetic countries)
INSERT INTO test_countries (name, iso_code, continent, population, area_km2, gdp_usd, capital_city, geom, properties)
SELECT
    'Country_' || i,
    chr(65 + (i % 26)) || chr(65 + ((i / 26) % 26)),
    CASE (i % 6)
        WHEN 0 THEN 'Africa'
        WHEN 1 THEN 'Asia'
        WHEN 2 THEN 'Europe'
        WHEN 3 THEN 'North America'
        WHEN 4 THEN 'South America'
        ELSE 'Oceania'
    END,
    (random() * 500000000)::INTEGER,
    (random() * 10000000)::NUMERIC,
    (random() * 10000000000000)::BIGINT,
    'Capital_' || i,
    ST_SetSRID(ST_MakeEnvelope(
        -180 + (i % 36) * 10,
        -80 + (i % 16) * 10,
        -170 + (i % 36) * 10,
        -70 + (i % 16) * 10
    ), 4326),
    jsonb_build_object(
        'development_level', CASE (i % 3)
            WHEN 0 THEN 'developed'
            WHEN 1 THEN 'developing'
            ELSE 'least_developed'
        END,
        'un_member', true,
        'test_data', true
    )
FROM generate_series(1, 100) AS i;

-- ============================================================================
-- Table 3: Roads (LineString Features)
-- ============================================================================
-- Tests line-based operations, length calculations, and buffering

CREATE TABLE test_roads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    road_type TEXT,
    length_km NUMERIC,
    lanes INTEGER,
    max_speed_kmh INTEGER,
    is_toll BOOLEAN DEFAULT false,
    surface TEXT,
    geom GEOMETRY(LineString, 4326) NOT NULL,
    properties JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_test_roads_geom ON test_roads USING GIST(geom);
CREATE INDEX idx_test_roads_type ON test_roads(road_type);

-- Insert test road segments (5,000 roads)
INSERT INTO test_roads (name, road_type, length_km, lanes, max_speed_kmh, is_toll, surface, geom, properties)
SELECT
    'Road_' || i,
    CASE (i % 5)
        WHEN 0 THEN 'highway'
        WHEN 1 THEN 'arterial'
        WHEN 2 THEN 'collector'
        WHEN 3 THEN 'local'
        ELSE 'alley'
    END,
    (random() * 100)::NUMERIC,
    2 + (random() * 6)::INTEGER,
    CASE (i % 5)
        WHEN 0 THEN 120
        WHEN 1 THEN 80
        WHEN 2 THEN 60
        WHEN 3 THEN 40
        ELSE 20
    END,
    (random() < 0.2),
    CASE (i % 3)
        WHEN 0 THEN 'asphalt'
        WHEN 1 THEN 'concrete'
        ELSE 'gravel'
    END,
    ST_SetSRID(ST_MakeLine(
        ST_MakePoint(
            (random() * 360) - 180,
            (random() * 180) - 90
        ),
        ST_MakePoint(
            (random() * 360) - 180,
            (random() * 180) - 90
        )
    ), 4326),
    jsonb_build_object(
        'condition', CASE (i % 3)
            WHEN 0 THEN 'excellent'
            WHEN 1 THEN 'good'
            ELSE 'fair'
        END,
        'test_data', true
    )
FROM generate_series(1, 5000) AS i;

-- ============================================================================
-- Table 4: Parks (Polygon Features with Holes)
-- ============================================================================
-- Tests complex polygon operations and hole handling

CREATE TABLE test_parks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    park_type TEXT,
    area_hectares NUMERIC,
    established_year INTEGER,
    visitor_count_annual INTEGER,
    geom GEOMETRY(Polygon, 4326) NOT NULL,
    properties JSONB
);

CREATE INDEX idx_test_parks_geom ON test_parks USING GIST(geom);

-- Insert test parks (1,000 parks)
INSERT INTO test_parks (name, park_type, area_hectares, established_year, visitor_count_annual, geom, properties)
SELECT
    'Park_' || i,
    CASE (i % 4)
        WHEN 0 THEN 'national_park'
        WHEN 1 THEN 'state_park'
        WHEN 2 THEN 'city_park'
        ELSE 'nature_reserve'
    END,
    (random() * 100000)::NUMERIC,
    1900 + (random() * 120)::INTEGER,
    (random() * 1000000)::INTEGER,
    ST_SetSRID(ST_Buffer(
        ST_MakePoint(
            (random() * 360) - 180,
            (random() * 170) - 85
        ),
        random() * 5
    ), 4326),
    jsonb_build_object(
        'has_camping', random() < 0.5,
        'has_trails', random() < 0.8,
        'test_data', true
    )
FROM generate_series(1, 1000) AS i;

-- ============================================================================
-- Table 5: Invalid Geometries (For Validation Testing)
-- ============================================================================
-- Tests geometry validation and repair functions

CREATE TABLE test_invalid_geoms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    error_type TEXT,
    geom GEOMETRY(Polygon, 4326)
);

-- Insert some invalid geometries (self-intersecting, duplicate points, etc.)
-- Note: PostGIS may auto-fix some of these, but we'll try to create problematic ones

INSERT INTO test_invalid_geoms (name, error_type, geom)
VALUES
    ('Self-Intersecting Bowtie', 'self_intersection',
     ST_GeomFromText('POLYGON((0 0, 0 2, 2 0, 2 2, 0 0))', 4326)),
    ('Spike', 'spike',
     ST_GeomFromText('POLYGON((0 0, 0 1, 1 1, 0.5 0.5, 1 0, 0 0))', 4326)),
    ('Duplicate Points', 'duplicate',
     ST_GeomFromText('POLYGON((0 0, 0 0, 1 0, 1 1, 0 1, 0 0))', 4326));

-- ============================================================================
-- Table 6: Temporal Data (For Temporal Testing)
-- ============================================================================
-- Tests temporal queries and filtering

CREATE TABLE test_temporal_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    event_type TEXT,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP,
    geom GEOMETRY(Point, 4326) NOT NULL,
    properties JSONB
);

CREATE INDEX idx_test_temporal_events_geom ON test_temporal_events USING GIST(geom);
CREATE INDEX idx_test_temporal_events_start ON test_temporal_events(start_time);
CREATE INDEX idx_test_temporal_events_end ON test_temporal_events(end_time);

-- Insert temporal events spanning 2020-2025
INSERT INTO test_temporal_events (name, event_type, start_time, end_time, geom, properties)
SELECT
    'Event_' || i,
    CASE (i % 4)
        WHEN 0 THEN 'conference'
        WHEN 1 THEN 'festival'
        WHEN 2 THEN 'concert'
        ELSE 'exhibition'
    END,
    '2020-01-01'::TIMESTAMP + (random() * INTERVAL '5 years'),
    '2020-01-01'::TIMESTAMP + (random() * INTERVAL '5 years') + INTERVAL '7 days',
    ST_SetSRID(ST_MakePoint(
        (random() * 360) - 180,
        (random() * 180) - 90
    ), 4326),
    jsonb_build_object(
        'capacity', (random() * 10000)::INTEGER,
        'test_data', true
    )
FROM generate_series(1, 2000) AS i;

-- ============================================================================
-- Table 7: Empty Table (Edge Case Testing)
-- ============================================================================

CREATE TABLE test_empty_table (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT,
    geom GEOMETRY(Point, 4326)
);

CREATE INDEX idx_test_empty_table_geom ON test_empty_table USING GIST(geom);

-- ============================================================================
-- Table 8: Null Geometries (Edge Case Testing)
-- ============================================================================

CREATE TABLE test_null_geoms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    geom GEOMETRY(Point, 4326) -- Allow NULL
);

-- Insert some records with NULL geometries
INSERT INTO test_null_geoms (name, geom)
VALUES
    ('Valid Point', ST_SetSRID(ST_MakePoint(0, 0), 4326)),
    ('Null Geom 1', NULL),
    ('Valid Point 2', ST_SetSRID(ST_MakePoint(1, 1), 4326)),
    ('Null Geom 2', NULL);

-- ============================================================================
-- Statistics and Analysis
-- ============================================================================

-- Update table statistics for better query planning
ANALYZE test_cities;
ANALYZE test_countries;
ANALYZE test_roads;
ANALYZE test_parks;
ANALYZE test_invalid_geoms;
ANALYZE test_temporal_events;
ANALYZE test_empty_table;
ANALYZE test_null_geoms;

-- ============================================================================
-- Summary Statistics
-- ============================================================================

DO $$
BEGIN
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'PostgreSQL Optimization Test Data - Summary';
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'test_cities:          % rows', (SELECT COUNT(*) FROM test_cities);
    RAISE NOTICE 'test_countries:       % rows', (SELECT COUNT(*) FROM test_countries);
    RAISE NOTICE 'test_roads:           % rows', (SELECT COUNT(*) FROM test_roads);
    RAISE NOTICE 'test_parks:           % rows', (SELECT COUNT(*) FROM test_parks);
    RAISE NOTICE 'test_invalid_geoms:   % rows', (SELECT COUNT(*) FROM test_invalid_geoms);
    RAISE NOTICE 'test_temporal_events: % rows', (SELECT COUNT(*) FROM test_temporal_events);
    RAISE NOTICE 'test_empty_table:     % rows', (SELECT COUNT(*) FROM test_empty_table);
    RAISE NOTICE 'test_null_geoms:      % rows', (SELECT COUNT(*) FROM test_null_geoms);
    RAISE NOTICE '============================================================';
END $$;
