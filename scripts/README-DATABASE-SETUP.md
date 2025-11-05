# SensorThings API Database Setup

This directory contains scripts to set up the PostgreSQL database for the OGC SensorThings API v1.1 implementation.

## Quick Start

### Linux/macOS

```bash
# From repository root
chmod +x scripts/setup-sensorthings-db.sh
./scripts/setup-sensorthings-db.sh
```

### Windows

```cmd
REM From repository root
scripts\setup-sensorthings-db.bat
```

### Manual Setup

If you prefer manual setup or the scripts don't work:

```bash
# 1. Create database
createdb honua_sensors

# 2. Enable PostGIS
psql -d honua_sensors -c "CREATE EXTENSION IF NOT EXISTS postgis;"

# 3. Run schema migration
psql -d honua_sensors -f src/Honua.Server.Enterprise/Sensors/Data/Migrations/001_InitialSchema.sql

# 4. (Optional) Add sample data
psql -d honua_sensors -f scripts/setup-sensorthings-db.sql
```

## What Gets Created

### Database Schema (001_InitialSchema.sql)

**8 Entity Tables:**
- `sta_things` - IoT devices/systems
- `sta_locations` - Physical locations
- `sta_historical_locations` - Location history
- `sta_sensors` - Sensor metadata
- `sta_observed_properties` - Phenomena being observed
- `sta_datastreams` - Observation streams
- `sta_observations` - Sensor measurements (partitioned by month)
- `sta_features_of_interest` - Features being observed

**Indexes:**
- Primary keys (UUID) on all tables
- Foreign key indexes for relationships
- PostGIS spatial indexes (GIST) on geometry columns
- Temporal indexes on observation times

**Triggers:**
- Auto-create HistoricalLocation when Thing location changes
- Auto-update `updated_at` timestamps

**Functions:**
- `create_observation_partitions()` - Auto-create monthly partitions
- `get_or_create_foi()` - Get or create FeatureOfInterest by geometry

### Sample Data (setup-sensorthings-db.sql)

**2 Weather Stations:**
- Weather Station Alpha (Building A Roof)
- Weather Station Beta (Parking Lot)

**2 Locations:**
- Building A Roof (Point: -122.4194, 37.7749)
- Parking Lot (Point: -122.4200, 37.7755)

**3 Sensors:**
- DHT22 Temperature Sensor
- DHT22 Humidity Sensor
- BMP280 Pressure Sensor

**3 ObservedProperties:**
- Air Temperature
- Relative Humidity
- Atmospheric Pressure

**3 Datastreams:**
- Alpha Temperature Stream (°C)
- Alpha Humidity Stream (%)
- Beta Pressure Stream (hPa)

**72 Observations:**
- 24 hours of temperature readings (hourly)
- 24 hours of humidity readings (hourly)
- 24 hours of pressure readings (hourly)

## Configuration

The setup scripts use these environment variables (optional):

```bash
# Database name (default: honua_sensors)
export SENSORTHINGS_DB_NAME=honua_sensors

# Database user (default: honua)
export SENSORTHINGS_DB_USER=honua

# Database password (default: honua_dev)
export SENSORTHINGS_DB_PASSWORD=honua_dev

# Database host (default: localhost)
export SENSORTHINGS_DB_HOST=localhost

# Database port (default: 5432)
export SENSORTHINGS_DB_PORT=5432
```

## Updating Configuration

After setup, update `src/Honua.Server.Host/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "SensorThingsDatabase": "Host=localhost;Port=5432;Database=honua_sensors;Username=honua;Password=honua_dev"
  },
  "SensorThings": {
    "Enabled": true,
    "BasePath": "/sta/v1.1",
    "Storage": {
      "Provider": "PostgreSQL",
      "ConnectionString": "ConnectionStrings:SensorThingsDatabase"
    }
  }
}
```

## Verification

### Check Schema

```bash
# List all SensorThings tables
psql -d honua_sensors -c "\dt sta_*"

# Count records in each table
psql -d honua_sensors -c "
SELECT 'Things' as entity, COUNT(*) FROM sta_things
UNION ALL SELECT 'Locations', COUNT(*) FROM sta_locations
UNION ALL SELECT 'HistoricalLocations', COUNT(*) FROM sta_historical_locations
UNION ALL SELECT 'Sensors', COUNT(*) FROM sta_sensors
UNION ALL SELECT 'ObservedProperties', COUNT(*) FROM sta_observed_properties
UNION ALL SELECT 'Datastreams', COUNT(*) FROM sta_datastreams
UNION ALL SELECT 'Observations', COUNT(*) FROM sta_observations
UNION ALL SELECT 'FeaturesOfInterest', COUNT(*) FROM sta_features_of_interest;
"
```

### Test PostGIS

```bash
# Verify PostGIS is working
psql -d honua_sensors -c "SELECT PostGIS_Version();"

# Query spatial data
psql -d honua_sensors -c "SELECT name, ST_AsText(location) FROM sta_locations;"
```

### Test API

```bash
# Start the application
cd src/Honua.Server.Host
dotnet run

# In another terminal, test endpoints
curl http://localhost:5000/sta/v1.1
curl http://localhost:5000/sta/v1.1/Things
curl http://localhost:5000/sta/v1.1/Observations?$top=10&$orderby=phenomenonTime%20desc
```

## Troubleshooting

### PostgreSQL not running

```bash
# Check status
pg_isready -h localhost -p 5432

# Start PostgreSQL (varies by OS)
# macOS (Homebrew):
brew services start postgresql

# Linux (systemd):
sudo systemctl start postgresql

# Docker:
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgis/postgis:16-3.4
```

### PostGIS not available

```bash
# Install PostGIS
# Ubuntu/Debian:
sudo apt-get install postgresql-16-postgis-3

# macOS (Homebrew):
brew install postgis

# Docker: Use postgis/postgis image
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgis/postgis:16-3.4
```

### Permission denied

```bash
# Grant permissions to user
psql -d postgres -c "ALTER USER honua WITH CREATEDB;"
psql -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE honua_sensors TO honua;"
```

### Migration fails

```bash
# Drop and recreate database
dropdb honua_sensors
createdb honua_sensors
psql -d honua_sensors -c "CREATE EXTENSION IF NOT EXISTS postgis;"
psql -d honua_sensors -f src/Honua.Server.Enterprise/Sensors/Data/Migrations/001_InitialSchema.sql
```

## Advanced Usage

### Custom Database Name

```bash
export SENSORTHINGS_DB_NAME=my_custom_db
./scripts/setup-sensorthings-db.sh
```

### Remote Database

```bash
export SENSORTHINGS_DB_HOST=db.example.com
export SENSORTHINGS_DB_PORT=5432
export SENSORTHINGS_DB_USER=admin
export SENSORTHINGS_DB_PASSWORD=secure_password
./scripts/setup-sensorthings-db.sh
```

### Docker PostgreSQL

```bash
# Start PostgreSQL with PostGIS
docker run -d \
  --name sensorthings-db \
  -p 5432:5432 \
  -e POSTGRES_DB=honua_sensors \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=honua_dev \
  postgis/postgis:16-3.4

# Wait for startup
sleep 5

# Run migration
psql -h localhost -U honua -d honua_sensors -f src/Honua.Server.Enterprise/Sensors/Data/Migrations/001_InitialSchema.sql
```

### Production Setup

For production deployments:

1. **Use strong passwords** - Don't use default passwords
2. **Enable SSL** - Add `sslmode=require` to connection string
3. **Backup strategy** - Set up automated backups
4. **Monitor partitions** - Ensure monthly partitions are created
5. **Index tuning** - Monitor query performance and add indexes as needed

```bash
# Production connection string example
Host=prod-db.example.com;Port=5432;Database=honua_sensors;Username=honua_prod;Password=STRONG_PASSWORD;SslMode=Require
```

## References

- Schema Migration: `src/Honua.Server.Enterprise/Sensors/Data/Migrations/001_InitialSchema.sql`
- Integration Guide: `docs/design/SENSORTHINGS_INTEGRATION.md`
- API Documentation: `docs/design/ogc-sensorthings-api-design.md`
