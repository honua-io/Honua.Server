#!/bin/bash
# Reset the Honua Server database (WARNING: Destroys all data!)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the script directory and project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo ""
echo -e "${RED}WARNING: This will destroy all data in the database!${NC}"
echo ""
read -p "Are you sure you want to reset the database? (yes/no): " -r
echo

if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
    echo "Database reset cancelled."
    exit 0
fi

echo -e "${BLUE}==>${NC} Stopping Honua Server if running..."
docker compose stop honua-server 2>/dev/null || true

echo -e "${BLUE}==>${NC} Dropping and recreating PostgreSQL database..."
docker exec honua-postgres psql -U honua -c "DROP DATABASE IF EXISTS honua;" || true
docker exec honua-postgres psql -U honua -c "CREATE DATABASE honua;"
docker exec honua-postgres psql -U honua -d honua -c "CREATE EXTENSION IF NOT EXISTS postgis;"

echo -e "${BLUE}==>${NC} Clearing Redis cache..."
docker exec honua-redis redis-cli FLUSHALL

echo ""
echo -e "${GREEN}✓${NC} Database has been reset!"
echo ""
echo "Next steps:"
echo "  • Run migrations: dotnet ef database update --project src/Honua.Server.Host"
echo "  • Load test data: ./scripts/seed-data.sh"
echo "  • Start server: dotnet run --project src/Honua.Server.Host"
echo ""
