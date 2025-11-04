#!/bin/bash

# Stop Process Framework Testing Stack
# This script stops all services and optionally cleans up volumes

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}Stopping Process Framework Testing Stack${NC}"
echo -e "${YELLOW}========================================${NC}"
echo ""

# Determine docker-compose command
if docker compose version &> /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

cd "$DOCKER_DIR"

# Parse arguments
CLEAN_VOLUMES=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --clean|-c)
            CLEAN_VOLUMES=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --clean, -c    Remove volumes (Redis data, Prometheus data, etc.)"
            echo "  --help, -h     Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Stop services
echo -e "${YELLOW}Stopping services...${NC}"
$DOCKER_COMPOSE down

if [ "$CLEAN_VOLUMES" = true ]; then
    echo -e "${YELLOW}Removing volumes...${NC}"
    $DOCKER_COMPOSE down -v
    echo -e "${GREEN}Volumes removed successfully${NC}"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Services Stopped Successfully!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

if [ "$CLEAN_VOLUMES" = false ]; then
    echo -e "${YELLOW}Note: Data volumes are preserved. Use --clean to remove them.${NC}"
    echo ""
fi
