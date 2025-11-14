#!/bin/bash
# Database Initialization Script for Honua IO
# Installs PostGIS and configures the database for geospatial workloads

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Required environment variables
: ${DATABASE_HOST:?Required: DATABASE_HOST}
: ${DATABASE_PORT:=5432}
: ${DATABASE_NAME:=honua}
: ${DATABASE_USER:=honua}
: ${DATABASE_PASSWORD:?Required: DATABASE_PASSWORD}

log_info "Initializing PostGIS for Honua IO database..."
log_info "Database: ${DATABASE_HOST}:${DATABASE_PORT}/${DATABASE_NAME}"

# Wait for database to be ready
log_info "Waiting for database to be ready..."
max_attempts=30
attempt=0

while [ $attempt -lt $max_attempts ]; do
    if PGPASSWORD=$DATABASE_PASSWORD psql -h $DATABASE_HOST -p $DATABASE_PORT -U $DATABASE_USER -d $DATABASE_NAME -c "SELECT 1" &> /dev/null; then
        log_info "Database is ready!"
        break
    fi

    attempt=$((attempt + 1))
    log_info "Waiting for database... (attempt $attempt/$max_attempts)"
    sleep 2
done

if [ $attempt -eq $max_attempts ]; then
    log_error "Database connection timeout"
    exit 1
fi

# Check if PostGIS is already installed
if PGPASSWORD=$DATABASE_PASSWORD psql -h $DATABASE_HOST -p $DATABASE_PORT -U $DATABASE_USER -d $DATABASE_NAME -c "SELECT PostGIS_version();" &> /dev/null; then
    log_warn "PostGIS is already installed. Skipping initialization."
    PGPASSWORD=$DATABASE_PASSWORD psql -h $DATABASE_HOST -p $DATABASE_PORT -U $DATABASE_USER -d $DATABASE_NAME -c "SELECT PostGIS_full_version();"
    exit 0
fi

# Install PostGIS extensions
log_info "Installing PostGIS extensions..."
PGPASSWORD=$DATABASE_PASSWORD psql -h $DATABASE_HOST -p $DATABASE_PORT -U $DATABASE_USER -d $DATABASE_NAME -f "$(dirname "$0")/init-postgis.sql"

if [ $? -eq 0 ]; then
    log_info "✓ PostGIS installed successfully"

    # Verify installation
    log_info "PostGIS version:"
    PGPASSWORD=$DATABASE_PASSWORD psql -h $DATABASE_HOST -p $DATABASE_PORT -U $DATABASE_USER -d $DATABASE_NAME -c "SELECT PostGIS_full_version();"
else
    log_error "Failed to install PostGIS"
    exit 1
fi

log_info "Database initialization complete!"
