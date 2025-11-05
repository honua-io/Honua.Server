#!/bin/bash
# OGC SensorThings API Database Setup Script
# Creates PostgreSQL database with PostGIS and runs SensorThings schema migration

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
DB_NAME="${SENSORTHINGS_DB_NAME:-honua_sensors}"
DB_USER="${SENSORTHINGS_DB_USER:-honua}"
DB_PASSWORD="${SENSORTHINGS_DB_PASSWORD:-honua_dev}"
DB_HOST="${SENSORTHINGS_DB_HOST:-localhost}"
DB_PORT="${SENSORTHINGS_DB_PORT:-5432}"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}OGC SensorThings API Database Setup${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Function to check if PostgreSQL is running
check_postgres() {
    echo -e "${BLUE}Checking PostgreSQL availability...${NC}"
    if ! pg_isready -h $DB_HOST -p $DB_PORT > /dev/null 2>&1; then
        echo -e "${RED}ERROR: PostgreSQL is not running on ${DB_HOST}:${DB_PORT}${NC}"
        echo "Please start PostgreSQL and try again."
        exit 1
    fi
    echo -e "${GREEN}✓ PostgreSQL is running${NC}"
}

# Function to check if database exists
db_exists() {
    psql -h $DB_HOST -p $DB_PORT -U postgres -lqt | cut -d \| -f 1 | grep -qw $DB_NAME
}

# Function to create database
create_database() {
    echo ""
    echo -e "${BLUE}Creating database: ${DB_NAME}${NC}"

    if db_exists; then
        echo -e "${YELLOW}Database ${DB_NAME} already exists.${NC}"
        read -p "Drop and recreate? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            echo "Dropping existing database..."
            dropdb -h $DB_HOST -p $DB_PORT -U postgres $DB_NAME
            echo -e "${GREEN}✓ Database dropped${NC}"
        else
            echo "Using existing database."
            return 0
        fi
    fi

    createdb -h $DB_HOST -p $DB_PORT -U postgres $DB_NAME
    echo -e "${GREEN}✓ Database created: ${DB_NAME}${NC}"
}

# Function to enable PostGIS
enable_postgis() {
    echo ""
    echo -e "${BLUE}Enabling PostGIS extension...${NC}"

    psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -c "CREATE EXTENSION IF NOT EXISTS postgis;" > /dev/null

    # Verify PostGIS version
    local version=$(psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -t -c "SELECT PostGIS_Version();" | xargs)
    echo -e "${GREEN}✓ PostGIS enabled: ${version}${NC}"
}

# Function to run migration
run_migration() {
    echo ""
    echo -e "${BLUE}Running SensorThings schema migration...${NC}"

    # Find migration file
    local migration_file="src/Honua.Server.Enterprise/Sensors/Data/Migrations/001_InitialSchema.sql"

    if [ ! -f "$migration_file" ]; then
        echo -e "${RED}ERROR: Migration file not found: ${migration_file}${NC}"
        echo "Please ensure you're running this script from the repository root."
        exit 1
    fi

    # Run migration
    psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -f $migration_file > /dev/null 2>&1

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Schema migration completed${NC}"
    else
        echo -e "${RED}ERROR: Schema migration failed${NC}"
        exit 1
    fi
}

# Function to verify schema
verify_schema() {
    echo ""
    echo -e "${BLUE}Verifying schema...${NC}"

    # Check for all 8 entity tables
    local tables=("sta_things" "sta_locations" "sta_historical_locations" "sta_sensors" "sta_observed_properties" "sta_datastreams" "sta_observations" "sta_features_of_interest")
    local missing_tables=()

    for table in "${tables[@]}"; do
        local exists=$(psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '$table';" | xargs)
        if [ "$exists" -eq 0 ]; then
            missing_tables+=($table)
        fi
    done

    if [ ${#missing_tables[@]} -eq 0 ]; then
        echo -e "${GREEN}✓ All 8 entity tables created${NC}"
    else
        echo -e "${RED}ERROR: Missing tables: ${missing_tables[*]}${NC}"
        exit 1
    fi

    # Check for indexes
    local index_count=$(psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -t -c "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename LIKE 'sta_%';" | xargs)
    echo -e "${GREEN}✓ Created ${index_count} indexes${NC}"

    # Check for triggers
    local trigger_count=$(psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -t -c "SELECT COUNT(*) FROM pg_trigger WHERE tgname LIKE '%sta_%';" | xargs)
    echo -e "${GREEN}✓ Created ${trigger_count} triggers${NC}"

    # Check for functions
    local function_count=$(psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -t -c "SELECT COUNT(*) FROM pg_proc WHERE proname LIKE '%sta_%' OR proname LIKE '%observation%' OR proname LIKE '%foi%';" | xargs)
    echo -e "${GREEN}✓ Created ${function_count} functions${NC}"
}

# Function to create sample data
create_sample_data() {
    echo ""
    read -p "Create sample test data? (Y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        echo -e "${BLUE}Creating sample data...${NC}"

        psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME <<EOF > /dev/null
-- Create a sample Thing
INSERT INTO sta_things (name, description, properties)
VALUES ('Weather Station Alpha', 'Rooftop weather monitoring station', '{"location": "Building A Roof", "department": "Facilities"}');

-- Create a sample Location
INSERT INTO sta_locations (name, description, encoding_type, location)
VALUES ('Building A Roof', 'Rooftop of main building', 'application/geo+json', ST_GeomFromGeoJSON('{"type":"Point","coordinates":[-122.4194,37.7749]}'));

-- Create a sample Sensor
INSERT INTO sta_sensors (name, description, encoding_type, metadata)
VALUES ('DHT22 Temperature Sensor', 'Digital humidity and temperature sensor', 'application/pdf', 'https://www.sparkfun.com/datasheets/Sensors/Temperature/DHT22.pdf');

-- Create a sample ObservedProperty
INSERT INTO sta_observed_properties (name, description, definition)
VALUES ('Air Temperature', 'Temperature of the air', 'http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature');

-- Get the IDs (they're UUIDs)
WITH thing_id AS (SELECT id FROM sta_things LIMIT 1),
     sensor_id AS (SELECT id FROM sta_sensors LIMIT 1),
     prop_id AS (SELECT id FROM sta_observed_properties LIMIT 1)
INSERT INTO sta_datastreams (name, description, observation_type, unit_of_measurement, thing_id, sensor_id, observed_property_id)
SELECT
    'Temperature Stream',
    'Air temperature readings from Building A roof',
    'http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement',
    '{"name":"degree Celsius","symbol":"°C","definition":"http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"}'::jsonb,
    thing_id.id,
    sensor_id.id,
    prop_id.id
FROM thing_id, sensor_id, prop_id;

-- Create sample observations
WITH datastream_id AS (SELECT id FROM sta_datastreams LIMIT 1)
INSERT INTO sta_observations (phenomenon_time, result, datastream_id)
SELECT
    NOW() - interval '1 hour' * i,
    20 + random() * 10,  -- Random temp between 20-30°C
    datastream_id.id
FROM datastream_id, generate_series(1, 10) i;
EOF

        echo -e "${GREEN}✓ Sample data created${NC}"
        echo "  - 1 Thing: Weather Station Alpha"
        echo "  - 1 Location: Building A Roof"
        echo "  - 1 Sensor: DHT22 Temperature Sensor"
        echo "  - 1 ObservedProperty: Air Temperature"
        echo "  - 1 Datastream: Temperature Stream"
        echo "  - 10 Observations: Temperature readings"
    fi
}

# Function to display connection info
display_connection_info() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${GREEN}Setup Complete!${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
    echo "Database Connection Details:"
    echo "  Host:     $DB_HOST"
    echo "  Port:     $DB_PORT"
    echo "  Database: $DB_NAME"
    echo "  User:     $DB_USER"
    echo ""
    echo "Connection String:"
    echo "  Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
    echo ""
    echo "Update your appsettings.Development.json:"
    echo "  \"ConnectionStrings\": {"
    echo "    \"SensorThingsDatabase\": \"Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD\""
    echo "  }"
    echo ""
    echo "Test the API:"
    echo "  1. Start the application: cd src/Honua.Server.Host && dotnet run"
    echo "  2. Test service root: curl http://localhost:5000/sta/v1.1"
    echo "  3. Test Things: curl http://localhost:5000/sta/v1.1/Things"
    echo ""
}

# Function to display quick test commands
display_test_commands() {
    echo -e "${BLUE}Quick Database Tests:${NC}"
    echo ""
    echo "# Count records in each table:"
    echo "psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -c \"SELECT 'Things' as entity, COUNT(*) FROM sta_things UNION ALL SELECT 'Locations', COUNT(*) FROM sta_locations UNION ALL SELECT 'Sensors', COUNT(*) FROM sta_sensors UNION ALL SELECT 'ObservedProperties', COUNT(*) FROM sta_observed_properties UNION ALL SELECT 'Datastreams', COUNT(*) FROM sta_datastreams UNION ALL SELECT 'Observations', COUNT(*) FROM sta_observations;\""
    echo ""
    echo "# Query sample data:"
    echo "psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -c \"SELECT name, description FROM sta_things;\""
    echo ""
    echo "# Check PostGIS is working:"
    echo "psql -h $DB_HOST -p $DB_PORT -U postgres -d $DB_NAME -c \"SELECT name, ST_AsText(location) FROM sta_locations;\""
    echo ""
}

# Main execution
main() {
    check_postgres
    create_database
    enable_postgis
    run_migration
    verify_schema
    create_sample_data
    display_connection_info
    display_test_commands
}

# Run main function
main

exit 0
