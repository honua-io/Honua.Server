#!/bin/bash
# Load test data into the Honua Server database

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the script directory and project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo -e "${BLUE}==>${NC} Loading test data into database..."
echo ""

# Check if PostgreSQL container is running
if ! docker ps | grep -q honua-postgres; then
    echo -e "${YELLOW}!${NC} PostgreSQL container not running. Starting Docker containers..."
    docker compose up -d postgres
    sleep 5
fi

# Check if seed data file exists
if [ -f "$PROJECT_ROOT/.env.seed" ]; then
    echo -e "${BLUE}==>${NC} Found .env.seed file"

    # Use docker-compose.seed.yml if available
    if [ -f "$PROJECT_ROOT/docker-compose.seed.yml" ]; then
        echo -e "${BLUE}==>${NC} Using docker-compose.seed.yml..."
        docker compose -f docker-compose.seed.yml up --build --exit-code-from seed
    else
        echo -e "${YELLOW}!${NC} docker-compose.seed.yml not found"
        echo -e "${YELLOW}!${NC} Please create seed data manually or use the Honua CLI"
    fi
else
    echo -e "${YELLOW}!${NC} .env.seed file not found"
    echo ""
    echo "To seed data, you can:"
    echo "  1. Copy .env.seed.example to .env.seed and customize"
    echo "  2. Run: docker compose -f docker-compose.seed.yml up"
    echo ""
    echo "Or use the Honua CLI to import data:"
    echo "  • Import GeoJSON: dotnet run --project src/Honua.Cli -- import geojson data.json"
    echo "  • Import Shapefile: dotnet run --project src/Honua.Cli -- import shapefile data.shp"
    echo "  • Import GeoPackage: dotnet run --project src/Honua.Cli -- import gpkg data.gpkg"
fi

echo ""
echo -e "${GREEN}✓${NC} Seed data operation completed!"
echo ""
