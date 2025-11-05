-- OGC SensorThings API Database Setup
-- Run this script to create sample test data after running the schema migration

-- This script assumes:
-- 1. Database 'honua_sensors' exists
-- 2. PostGIS extension is enabled
-- 3. Schema migration (001_InitialSchema.sql) has been run

-- Sample Thing: Weather Station
INSERT INTO sta_things (name, description, properties)
VALUES
    ('Weather Station Alpha', 'Rooftop weather monitoring station',
     '{"location": "Building A Roof", "department": "Facilities", "installDate": "2025-01-15"}'::jsonb),
    ('Weather Station Beta', 'Ground level weather station',
     '{"location": "Parking Lot", "department": "Security", "installDate": "2025-02-01"}'::jsonb);

-- Sample Locations
INSERT INTO sta_locations (name, description, encoding_type, location)
VALUES
    ('Building A Roof', 'Rooftop of main building', 'application/geo+json',
     ST_GeomFromGeoJSON('{"type":"Point","coordinates":[-122.4194,37.7749]}')),
    ('Parking Lot', 'East parking lot', 'application/geo+json',
     ST_GeomFromGeoJSON('{"type":"Point","coordinates":[-122.4200,37.7755]}'));

-- Link Things to Locations (this will auto-create HistoricalLocations via trigger)
WITH thing1 AS (SELECT id FROM sta_things WHERE name = 'Weather Station Alpha'),
     loc1 AS (SELECT id FROM sta_locations WHERE name = 'Building A Roof')
INSERT INTO sta_thing_location (thing_id, location_id)
SELECT thing1.id, loc1.id FROM thing1, loc1;

WITH thing2 AS (SELECT id FROM sta_things WHERE name = 'Weather Station Beta'),
     loc2 AS (SELECT id FROM sta_locations WHERE name = 'Parking Lot')
INSERT INTO sta_thing_location (thing_id, location_id)
SELECT thing2.id, loc2.id FROM thing2, loc2;

-- Sample Sensors
INSERT INTO sta_sensors (name, description, encoding_type, metadata)
VALUES
    ('DHT22 Temperature Sensor', 'Digital humidity and temperature sensor',
     'application/pdf', 'https://www.sparkfun.com/datasheets/Sensors/Temperature/DHT22.pdf'),
    ('DHT22 Humidity Sensor', 'Digital humidity sensor',
     'application/pdf', 'https://www.sparkfun.com/datasheets/Sensors/Temperature/DHT22.pdf'),
    ('BMP280 Pressure Sensor', 'Barometric pressure sensor',
     'application/pdf', 'https://www.bosch-sensortec.com/media/boschsensortec/downloads/datasheets/bst-bmp280-ds001.pdf');

-- Sample ObservedProperties
INSERT INTO sta_observed_properties (name, description, definition)
VALUES
    ('Air Temperature', 'Temperature of the air',
     'http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature'),
    ('Relative Humidity', 'Relative humidity of the air',
     'http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#RelativeHumidity'),
    ('Atmospheric Pressure', 'Atmospheric air pressure',
     'http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AtmosphericPressure');

-- Sample Datastreams
-- Temperature Datastream for Weather Station Alpha
WITH thing_id AS (SELECT id FROM sta_things WHERE name = 'Weather Station Alpha'),
     sensor_id AS (SELECT id FROM sta_sensors WHERE name = 'DHT22 Temperature Sensor'),
     prop_id AS (SELECT id FROM sta_observed_properties WHERE name = 'Air Temperature')
INSERT INTO sta_datastreams (name, description, observation_type, unit_of_measurement, thing_id, sensor_id, observed_property_id)
SELECT
    'Alpha Temperature Stream',
    'Air temperature readings from Building A roof',
    'http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement',
    '{"name":"degree Celsius","symbol":"°C","definition":"http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"}'::jsonb,
    thing_id.id,
    sensor_id.id,
    prop_id.id
FROM thing_id, sensor_id, prop_id;

-- Humidity Datastream for Weather Station Alpha
WITH thing_id AS (SELECT id FROM sta_things WHERE name = 'Weather Station Alpha'),
     sensor_id AS (SELECT id FROM sta_sensors WHERE name = 'DHT22 Humidity Sensor'),
     prop_id AS (SELECT id FROM sta_observed_properties WHERE name = 'Relative Humidity')
INSERT INTO sta_datastreams (name, description, observation_type, unit_of_measurement, thing_id, sensor_id, observed_property_id)
SELECT
    'Alpha Humidity Stream',
    'Relative humidity readings from Building A roof',
    'http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement',
    '{"name":"percent","symbol":"%","definition":"http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Percent"}'::jsonb,
    thing_id.id,
    sensor_id.id,
    prop_id.id
FROM thing_id, sensor_id, prop_id;

-- Pressure Datastream for Weather Station Beta
WITH thing_id AS (SELECT id FROM sta_things WHERE name = 'Weather Station Beta'),
     sensor_id AS (SELECT id FROM sta_sensors WHERE name = 'BMP280 Pressure Sensor'),
     prop_id AS (SELECT id FROM sta_observed_properties WHERE name = 'Atmospheric Pressure')
INSERT INTO sta_datastreams (name, description, observation_type, unit_of_measurement, thing_id, sensor_id, observed_property_id)
SELECT
    'Beta Pressure Stream',
    'Atmospheric pressure readings from parking lot',
    'http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement',
    '{"name":"hectopascal","symbol":"hPa","definition":"http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Hectopascal"}'::jsonb,
    thing_id.id,
    sensor_id.id,
    prop_id.id
FROM thing_id, sensor_id, prop_id;

-- Sample Observations (temperature readings for past 24 hours, hourly)
WITH datastream_id AS (SELECT id FROM sta_datastreams WHERE name = 'Alpha Temperature Stream')
INSERT INTO sta_observations (phenomenon_time, result, datastream_id)
SELECT
    NOW() - interval '1 hour' * i,
    20 + (SIN(i::numeric / 4) * 5) + (random() * 2),  -- Simulated daily temperature curve with noise
    datastream_id.id
FROM datastream_id, generate_series(0, 23) i;

-- Sample Observations (humidity readings for past 24 hours, hourly)
WITH datastream_id AS (SELECT id FROM sta_datastreams WHERE name = 'Alpha Humidity Stream')
INSERT INTO sta_observations (phenomenon_time, result, datastream_id)
SELECT
    NOW() - interval '1 hour' * i,
    60 + (SIN(i::numeric / 3) * 20) + (random() * 5),  -- Simulated humidity variation
    datastream_id.id
FROM datastream_id, generate_series(0, 23) i;

-- Sample Observations (pressure readings for past 24 hours, hourly)
WITH datastream_id AS (SELECT id FROM sta_datastreams WHERE name = 'Beta Pressure Stream')
INSERT INTO sta_observations (phenomenon_time, result, datastream_id)
SELECT
    NOW() - interval '1 hour' * i,
    1013 + (SIN(i::numeric / 6) * 5) + (random() * 2),  -- Simulated pressure variation around 1013 hPa
    datastream_id.id
FROM datastream_id, generate_series(0, 23) i;

-- Verify the data
SELECT 'Setup Complete!' as status;
SELECT 'Things' as entity, COUNT(*) as count FROM sta_things
UNION ALL SELECT 'Locations', COUNT(*) FROM sta_locations
UNION ALL SELECT 'HistoricalLocations', COUNT(*) FROM sta_historical_locations
UNION ALL SELECT 'Sensors', COUNT(*) FROM sta_sensors
UNION ALL SELECT 'ObservedProperties', COUNT(*) FROM sta_observed_properties
UNION ALL SELECT 'Datastreams', COUNT(*) FROM sta_datastreams
UNION ALL SELECT 'Observations', COUNT(*) FROM sta_observations
UNION ALL SELECT 'FeaturesOfInterest', COUNT(*) FROM sta_features_of_interest;

-- Display sample data
SELECT '--- Sample Things ---' as info;
SELECT name, description FROM sta_things;

SELECT '--- Sample Datastreams ---' as info;
SELECT name, description FROM sta_datastreams;

SELECT '--- Recent Observations (5 most recent) ---' as info;
SELECT
    d.name as datastream,
    o.phenomenon_time,
    o.result,
    d.unit_of_measurement->>'symbol' as unit
FROM sta_observations o
JOIN sta_datastreams d ON o.datastream_id = d.id
ORDER BY o.phenomenon_time DESC
LIMIT 5;
